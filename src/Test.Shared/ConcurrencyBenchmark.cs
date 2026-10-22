namespace Test.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Engines;
    using SharpAI.Operations;

    /// <summary>
    /// A throughput/latency benchmark (W2.T5) that measures single-request vs N-concurrent generation against
    /// the fixture model. It is not a Touchstone suite (it produces measurements, not pass/fail assertions);
    /// the runner invokes it explicitly via <c>--benchmark</c> and prints the <see cref="BenchmarkResult"/>.
    /// </summary>
    public static class ConcurrencyBenchmark
    {
        #region Public-Methods

        /// <summary>
        /// Run the benchmark against a model.
        /// </summary>
        /// <param name="modelPath">Path to a GGUF model.</param>
        /// <param name="iterations">Number of generations per phase (default 8).</param>
        /// <param name="concurrency">Concurrent generation slots for the concurrent phase (default 4).</param>
        /// <param name="maxTokens">Tokens to generate per request (default 64).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The measured result.</returns>
        public static async Task<BenchmarkResult> RunAsync(
            string modelPath,
            int iterations = 8,
            int concurrency = 4,
            int maxTokens = 64,
            CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(modelPath)) throw new ArgumentNullException(nameof(modelPath));

            if (iterations < 1) throw new ArgumentOutOfRangeException(nameof(iterations));
            if (concurrency < 1) throw new ArgumentOutOfRangeException(nameof(concurrency));
            if (maxTokens < 1) throw new ArgumentOutOfRangeException(nameof(maxTokens));
            string? prevSlots = Environment.GetEnvironmentVariable("SHARPAI_MAX_CONCURRENT_GENERATIONS");
            Environment.SetEnvironmentVariable("SHARPAI_MAX_CONCURRENT_GENERATIONS", concurrency.ToString());

            LlamaSharpEngine engine = new LlamaSharpEngine();
            try
            {
                await engine.InitializeAsync(modelPath).ConfigureAwait(false);

                // Warm up (first inference pays one-time costs we don't want in the measurement).
                await engine.GenerateTextAsync("Warm up.", 8, 0.0f, null, token).ConfigureAwait(false);

                string prompt = "Write one sentence about the ocean.";

                // Sequential phase.
                Stopwatch seqWatch = Stopwatch.StartNew();
                long seqTokens = 0;
                ConcurrentDictionary<InferenceStatistics, string> failures = new ConcurrentDictionary<InferenceStatistics, string>();
                List<InferenceStatistics> sequential = new List<InferenceStatistics>();
                for (int i = 0; i < iterations; i++)
                {
                    InferenceStatistics sample = new InferenceStatistics();
                    await RunSampleAsync(engine, prompt, maxTokens, sample, failures, token).ConfigureAwait(false);
                    sequential.Add(sample);
                    if (!failures.ContainsKey(sample)) seqTokens += sample.CompletionTokens;
                }
                seqWatch.Stop();

                // Concurrent phase.
                Stopwatch conWatch = Stopwatch.StartNew();
                Task<string>[] tasks = new Task<string>[iterations];
                InferenceStatistics[] concurrent = new InferenceStatistics[iterations];
                for (int i = 0; i < iterations; i++)
                {
                    concurrent[i] = new InferenceStatistics();
                    tasks[i] = RunSampleAsync(engine, prompt, maxTokens, concurrent[i], failures, token);
                }
                string[] outputs = await Task.WhenAll(tasks).ConfigureAwait(false);
                conWatch.Stop();

                long conTokens = 0;
                foreach (InferenceStatistics sample in concurrent) if (!failures.ContainsKey(sample)) conTokens += sample.CompletionTokens;

                double seqSeconds = seqWatch.Elapsed.TotalSeconds;
                double conSeconds = conWatch.Elapsed.TotalSeconds;

                return new BenchmarkResult
                {
                    Receipt = new
                    {
                        schema_version = 1, created_utc = DateTime.UtcNow,
                        sharpai_version = typeof(LlamaSharpEngine).Assembly.GetName().Version?.ToString(),
                        llamasharp_version = SharpAI.Classes.Runtime.NativeBackendInfo.LlamaSharpVersion,
                        backend_selected = SharpAI.Classes.Runtime.NativeBackendInfo.SelectedBackend,
                        native_build = (string?)null,
                        native_build_note = "Native build identifier unavailable; LlamaSharp package version is recorded separately.",
                        hardware = new { os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), processors = Environment.ProcessorCount, available_memory_bytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes },
                        model_files = GgufShards.Files(modelPath).Select(path => { using FileStream stream = File.OpenRead(path); return new { file = Path.GetFileName(path), sha256 = Convert.ToHexString(SHA256.HashData(stream)) }; }).ToArray(),
                        settings = engine.GetRuntimeSettings(),
                        workload_id = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(prompt))),
                        warm_state = "one warmup generation", iterations = iterations, concurrency = concurrency, max_output_tokens = maxTokens,
                        failure_count = failures.Count, failures = failures.Values.ToArray(),
                        sequential = Summarize(sequential, failures), concurrent = Summarize(concurrent, failures),
                        percentile_note = "Nearest rank; p95 requires 20 samples, p99 requires 100. Missing tails are null. Token counts precede text filtering."
                    },
                    FailureCount = failures.Count,
                    Iterations = iterations,
                    Concurrency = concurrency,
                    MaxTokens = maxTokens,
                    SequentialSeconds = seqSeconds,
                    SequentialAvgLatencyMs = seqSeconds / iterations * 1000.0,
                    SequentialTokensPerSecond = seqSeconds > 0 ? seqTokens / seqSeconds : 0,
                    ConcurrentSeconds = conSeconds,
                    ConcurrentTokensPerSecond = conSeconds > 0 ? conTokens / conSeconds : 0,
                    Speedup = conSeconds > 0 ? seqSeconds / conSeconds : 0
                };
            }
            finally
            {
                engine.Dispose();
                Environment.SetEnvironmentVariable("SHARPAI_MAX_CONCURRENT_GENERATIONS", prevSlots);
            }
        }

        private static async Task<string> RunSampleAsync(LlamaSharpEngine engine, string prompt, int maxTokens, InferenceStatistics statistics,
            ConcurrentDictionary<InferenceStatistics, string> failures, CancellationToken token)
        {
            try { return await engine.GenerateTextAsync(prompt, maxTokens, 0.2f, null, null, token, statistics).ConfigureAwait(false); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex) { failures[statistics] = ex.GetType().Name + ": " + ex.Message; return String.Empty; }
        }

        private static object Summarize(IEnumerable<InferenceStatistics> samples, ConcurrentDictionary<InferenceStatistics, string> failures)
        {
            InferenceStatistics[] all = samples.ToArray();
            InferenceStatistics[] successful = all.Where(sample => !failures.ContainsKey(sample)).ToArray();
            return new
            {
                sample_count = all.Length, samples = all.Select(sample => new { statistics = sample, success = !failures.ContainsKey(sample), error = failures.TryGetValue(sample, out string? error) ? error : null }).ToArray(),
                latency_ms = new { p50 = BenchmarkMath.Percentile(successful.Select(s => s.TotalDurationNanoseconds / 1e6), 50), p95 = BenchmarkMath.Percentile(successful.Select(s => s.TotalDurationNanoseconds / 1e6), 95), p99 = BenchmarkMath.Percentile(successful.Select(s => s.TotalDurationNanoseconds / 1e6), 99) },
                ttft_ms = new { p50 = BenchmarkMath.Percentile(successful.Where(s => s.FirstTokenDurationNanoseconds > 0).Select(s => s.FirstTokenDurationNanoseconds / 1e6), 50), p95 = BenchmarkMath.Percentile(successful.Where(s => s.FirstTokenDurationNanoseconds > 0).Select(s => s.FirstTokenDurationNanoseconds / 1e6), 95), p99 = BenchmarkMath.Percentile(successful.Where(s => s.FirstTokenDurationNanoseconds > 0).Select(s => s.FirstTokenDurationNanoseconds / 1e6), 99) }
            };
        }

        #endregion

        #region Public-Types

        /// <summary>
        /// The measured benchmark result.
        /// </summary>
        public class BenchmarkResult
        {
            /// <summary>Portable versioned receipt; null before a successful benchmark.</summary>
            public object? Receipt { get { return _Receipt; } set { _Receipt = value; } }
            private object? _Receipt;
            /// <summary>Number of failed measured requests; failed runs still produce a receipt.</summary>
            public int FailureCount { get { return _FailureCount; } set { _FailureCount = value; } }
            private int _FailureCount;
            /// <summary>Generations per phase.</summary>
            public int Iterations { get; set; }

            /// <summary>Concurrent slots used in the concurrent phase.</summary>
            public int Concurrency { get; set; }

            /// <summary>Tokens generated per request.</summary>
            public int MaxTokens { get; set; }

            /// <summary>Total wall-clock seconds for the sequential phase.</summary>
            public double SequentialSeconds { get; set; }

            /// <summary>Average per-request latency (ms) in the sequential phase.</summary>
            public double SequentialAvgLatencyMs { get; set; }

            /// <summary>Aggregate generated tokens/second in the sequential phase.</summary>
            public double SequentialTokensPerSecond { get; set; }

            /// <summary>Total wall-clock seconds for the concurrent phase.</summary>
            public double ConcurrentSeconds { get; set; }

            /// <summary>Aggregate generated tokens/second in the concurrent phase.</summary>
            public double ConcurrentTokensPerSecond { get; set; }

            /// <summary>Wall-clock speedup of the concurrent phase over the sequential phase.</summary>
            public double Speedup { get; set; }
        }

        #endregion
    }
}
