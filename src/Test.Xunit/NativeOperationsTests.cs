namespace Test.Xunit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Engines;
    using SharpAI.Exceptions;
    using SharpAI.Operations;
    using Test.Shared;
    using Xunit;

    /// <summary>Operational assertions against real weights, independent of model instruction-following ability.</summary>
    public sealed class NativeOperationsTests
    {
        [ModelTheory]
        [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
        public async Task StatisticsArePerCallAndMatchActualSampling(bool chat, bool stream)
        {
            using LlamaSharpEngine engine = new LlamaSharpEngine();
            await engine.InitializeAsync(ModelFixture.Path!);
            InferenceStatistics stats = new InferenceStatistics();
            string prompt = "Once upon a time";
            int promptTokens = engine.CountTokens(prompt);
            int fragments = 0;
            if (stream)
            {
                IAsyncEnumerable<string> sequence = chat ? engine.GenerateChatCompletionStreamAsync(prompt, 8, 0, Array.Empty<string>(), null!, CancellationToken.None, stats)
                    : engine.GenerateTextStreamAsync(prompt, 8, 0, null!, null!, CancellationToken.None, stats);
                await foreach (string fragment in sequence) fragments++;
                Assert.Equal(fragments, stats.CompletionTokens);
            }
            else
            {
                string text = chat ? await engine.GenerateChatCompletionAsync(prompt, 8, 0, Array.Empty<string>(), null!, CancellationToken.None, stats)
                    : await engine.GenerateTextAsync(prompt, 8, 0, null!, null!, CancellationToken.None, stats);
                Assert.NotEmpty(text);
            }
            Assert.Equal(promptTokens, stats.PromptTokens); Assert.InRange(stats.CompletionTokens, 1, 8);
            Assert.True(stats.FirstTokenDurationNanoseconds > 0); Assert.True(stats.TotalDurationNanoseconds >= stats.FirstTokenDurationNanoseconds);
            Assert.False(stats.Cancelled); Assert.Equal(stats.PromptTokens + stats.CompletionTokens, stats.ToUsage().TotalTokens);
            Assert.True(stats.PromptDurationNanoseconds > 0); Assert.True(stats.GenerationDurationNanoseconds > 0);
            Assert.Equal(engine.GetContextSize(), engine.GetRuntimeSettings()["context"]);
            Assert.True(engine.LoadDurationNanoseconds > 0);
            InferenceStatistics second = new InferenceStatistics();
            await engine.GenerateTextAsync(prompt, 1, 0, null!, null!, CancellationToken.None, second);
            Assert.Equal(1, second.CompletionTokens); Assert.Equal(promptTokens, second.PromptTokens);
        }

        [ModelTheory]
        [InlineData(1)] [InlineData(3)]
        public async Task ParallelRequestsDoNotShareStats(int slots)
        {
            using LlamaSharpEngine engine = new LlamaSharpEngine();
            engine.MaxConcurrentGenerations = slots;
            await engine.InitializeAsync(ModelFixture.Path!);
            InferenceStatistics[] stats = Enumerable.Range(0, 6).Select(_ => new InferenceStatistics()).ToArray();
            Task<string>[] calls = stats.Select((s, index) => engine.GenerateTextAsync("Once upon a time", index + 1, 0, null!, null!, CancellationToken.None, s)).ToArray();
            await Task.WhenAll(calls);
            for (int i = 0; i < stats.Length; i++) Assert.Equal(i + 1, stats[i].CompletionTokens);
        }

        [ModelTheory]
        [InlineData(false)] [InlineData(true)]
        public async Task OverflowFailsBeforeOutputAndSubsequentRequestStillRuns(bool stream)
        {
            using LlamaSharpEngine engine = new LlamaSharpEngine(); await engine.InitializeAsync(ModelFixture.Path!);
            InferenceStatistics stats = new InferenceStatistics();
            if (stream)
            {
                await Assert.ThrowsAsync<ContextLimitException>(async () =>
                {
                    await foreach (string fragment in engine.GenerateTextStreamAsync("Once upon a time", engine.GetContextSize(), 0, null!, null!, CancellationToken.None, stats)) Assert.Fail("Overflow emitted content.");
                });
            }
            else await Assert.ThrowsAsync<ContextLimitException>(() => engine.GenerateTextAsync("Once upon a time", engine.GetContextSize(), 0, null!, null!, CancellationToken.None, stats));
            Assert.Equal(0, stats.CompletionTokens); Assert.Equal(0, stats.PromptTokens);
            Assert.NotEmpty(await engine.GenerateTextAsync("Once upon a time", 2));
        }

        [ModelTheory]
        [InlineData(false)] [InlineData(true)]
        public async Task CancellationAndEarlyDisposalReleaseSlot(bool cancel)
        {
            long cancellations = 0;
            using System.Diagnostics.Metrics.MeterListener? listener = new System.Diagnostics.Metrics.MeterListener();
            listener.InstrumentPublished = (instrument, owner) => { if (instrument.Name == "sharpai.inference.cancellations") owner.EnableMeasurementEvents(instrument); };
            listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) => Interlocked.Add(ref cancellations, value));
            listener.Start();
            using LlamaSharpEngine engine = new LlamaSharpEngine(); await engine.InitializeAsync(ModelFixture.Path!);
            using CancellationTokenSource source = new CancellationTokenSource();
            InferenceStatistics stats = new InferenceStatistics();
            await using (IAsyncEnumerator<string> sequence = engine.GenerateTextStreamAsync("Once upon a time", 16, 0, null!, null!, source.Token, stats).GetAsyncEnumerator())
            {
                Assert.True(await sequence.MoveNextAsync());
                if (cancel)
                {
                    source.Cancel();
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { await sequence.MoveNextAsync(); });
                }
            }
            Assert.Equal(cancel, stats.Cancelled); Assert.True(stats.CompletionTokens >= 1);
            Assert.Equal(cancel ? 1L : 0L, cancellations);
            Assert.NotEmpty(await engine.GenerateTextAsync("Once upon a time", 2));
        }
        [ModelTheory]
        [InlineData(5)]
        public async Task QueueTimeoutPreservesBusyExceptionAndDoesNotReleaseAnotherCallsSlot(int milliseconds)
        {
            using LlamaSharpEngine engine = new LlamaSharpEngine(); engine.GenerationQueueTimeoutMs = milliseconds;
            await engine.InitializeAsync(ModelFixture.Path!);
            await using IAsyncEnumerator<string> first = engine.GenerateTextStreamAsync("Once upon a time", 8).GetAsyncEnumerator();
            Assert.True(await first.MoveNextAsync()); Assert.Equal(1, engine.ActiveGenerations);
            InferenceStatistics rejected = new InferenceStatistics();
            await Assert.ThrowsAsync<EngineBusyException>(() => engine.GenerateTextAsync("Once upon a time", 8, 0, null!, null!, CancellationToken.None, rejected));
            Assert.True(rejected.QueueDurationNanoseconds > 0); Assert.Equal(0, rejected.CompletionTokens); Assert.Equal(1, engine.ActiveGenerations);
        }

        [ModelTheory]
        [InlineData(4, 0)] [InlineData(100000, 4)]
        public async Task BenchmarkReceiptsRetainSuccessfulAndFailedSamples(int maximum, int expectedFailures)
        {
            Test.Shared.ConcurrencyBenchmark.BenchmarkResult result = await Test.Shared.ConcurrencyBenchmark.RunAsync(ModelFixture.Path!, 2, 2, maximum);
            Assert.Equal(expectedFailures, result.FailureCount);
            using System.Text.Json.JsonDocument receipt = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(result.Receipt));
            Assert.Equal(1, receipt.RootElement.GetProperty("schema_version").GetInt32());
            Assert.Equal(SharpAI.Classes.Runtime.NativeBackendInfo.LlamaSharpVersion, receipt.RootElement.GetProperty("llamasharp_version").GetString());
            Assert.Equal(expectedFailures, receipt.RootElement.GetProperty("failure_count").GetInt32());
            Assert.Equal(2, receipt.RootElement.GetProperty("sequential").GetProperty("samples").GetArrayLength());
            Assert.Equal(System.Text.Json.JsonValueKind.Null, receipt.RootElement.GetProperty("sequential").GetProperty("latency_ms").GetProperty("p99").ValueKind);
            Assert.Equal(64, receipt.RootElement.GetProperty("model_files")[0].GetProperty("sha256").GetString()!.Length);
            Assert.Equal(maximum, receipt.RootElement.GetProperty("max_output_tokens").GetInt32());
        }

        [ModelTheory]
        [InlineData(1)]
        public async Task ExactlyFullContextDoesNotDecodePastOutputBudget(int outputTokens)
        {
            using LlamaSharpEngine engine = new LlamaSharpEngine(); await engine.InitializeAsync(ModelFixture.Path!);
            string prompt = "Once";
            while (engine.CountTokens(prompt) < engine.GetContextSize() - outputTokens) prompt += " a";
            Assert.Equal(engine.GetContextSize() - outputTokens, engine.CountTokens(prompt));
            InferenceStatistics stats = new InferenceStatistics();
            await engine.GenerateTextAsync(prompt, outputTokens, 0, null!, null!, CancellationToken.None, stats);
            Assert.InRange(stats.CompletionTokens, 0, outputTokens);
        }

        [ModelTheory]
        [InlineData(false, false)] [InlineData(true, false)]
        [InlineData(false, true)] [InlineData(true, true)]
        public async Task LoadModePreservesMmapAndMlockSettings(bool mmap, bool mlock)
        {
            string? previousMmap = Environment.GetEnvironmentVariable("SHARPAI_USE_MMAP");
            string? previousMlock = Environment.GetEnvironmentVariable("SHARPAI_USE_MLOCK");
            Environment.SetEnvironmentVariable("SHARPAI_USE_MMAP", mmap.ToString());
            Environment.SetEnvironmentVariable("SHARPAI_USE_MLOCK", mlock.ToString());
            try
            {
                using LlamaSharpEngine engine = new LlamaSharpEngine();
                await engine.InitializeAsync(ModelFixture.Path!);
                Dictionary<string, object> settings = engine.GetRuntimeSettings();
                Assert.Equal(mmap, settings["mmap"]); Assert.Equal(mlock, settings["mlock"]);
                Assert.NotEmpty(await engine.GenerateTextAsync("Once upon a time", 2));
            }
            finally
            {
                Environment.SetEnvironmentVariable("SHARPAI_USE_MMAP", previousMmap);
                Environment.SetEnvironmentVariable("SHARPAI_USE_MLOCK", previousMlock);
            }
        }

    }
}
