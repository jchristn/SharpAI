namespace Test.Shared
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    using SharpAI.Engines;

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

            string prevSlots = Environment.GetEnvironmentVariable("SHARPAI_MAX_CONCURRENT_GENERATIONS");
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
                for (int i = 0; i < iterations; i++)
                {
                    string output = await engine.GenerateTextAsync(prompt, maxTokens, 0.2f, null, token).ConfigureAwait(false);
                    seqTokens += engine.CountTokens(output, false, false);
                }
                seqWatch.Stop();

                // Concurrent phase.
                Stopwatch conWatch = Stopwatch.StartNew();
                Task<string>[] tasks = new Task<string>[iterations];
                for (int i = 0; i < iterations; i++)
                {
                    tasks[i] = engine.GenerateTextAsync(prompt, maxTokens, 0.2f, null, token);
                }
                string[] outputs = await Task.WhenAll(tasks).ConfigureAwait(false);
                conWatch.Stop();

                long conTokens = 0;
                foreach (string o in outputs) conTokens += engine.CountTokens(o, false, false);

                double seqSeconds = seqWatch.Elapsed.TotalSeconds;
                double conSeconds = conWatch.Elapsed.TotalSeconds;

                return new BenchmarkResult
                {
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

        #endregion

        #region Public-Types

        /// <summary>
        /// The measured benchmark result.
        /// </summary>
        public class BenchmarkResult
        {
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
