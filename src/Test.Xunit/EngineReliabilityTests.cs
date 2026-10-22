namespace Test.Xunit
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Engines;
    using SharpAI.Exceptions;
    using SharpAI.Operations;
    using SharpAI.Services;
    using SharpAI.Telemetry;
    using SyslogLogging;
    using Test.Shared;
    using Xunit;

    public sealed class EngineReliabilityTests
    {
        [Theory]
        [InlineData(true, false)] [InlineData(false, true)] [InlineData(false, false)]
        public void SpanAndLogCarryMatchingMeasurementsWithoutRequestText(bool success, bool cancelled)
        {
            Activity? finished = null;
            using ActivityListener listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == SharpAITelemetry.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => finished = activity
            };
            ActivitySource.AddActivityListener(listener);
            InferenceStatistics stats = new InferenceStatistics { PromptTokens = 7, CompletionTokens = 3, QueueDurationNanoseconds = 10,
                FirstTokenDurationNanoseconds = 20, PromptDurationNanoseconds = 30, GenerationDurationNanoseconds = 40,
                TotalDurationNanoseconds = 100, Cancelled = cancelled, PhaseTimingsApproximate = true };
            string record;
            using (Activity activity = SharpAITelemetry.StartInference("completion", "fixture"))
            {
                Assert.NotNull(activity);
                SharpAITelemetry.CompleteInference(activity, stats, success);
                record = SharpAITelemetry.FormatCompletion(activity, "completion", "fixture", stats, success);
            }
            Assert.NotNull(finished);
            Assert.Equal(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error, finished.Status);
            Assert.Equal(7, finished.GetTagItem("sharpai.prompt_tokens"));
            Assert.Equal(40L, finished.GetTagItem("sharpai.generation_ns"));
            Assert.Equal("backend_step_wall_clock", finished.GetTagItem("sharpai.timing_source"));
            using JsonDocument log = JsonDocument.Parse(record);
            Assert.Equal(finished.TraceId.ToHexString(), log.RootElement.GetProperty("trace_id").GetString());
            Assert.Equal(success ? "ok" : cancelled ? "cancelled" : "error", log.RootElement.GetProperty("outcome").GetString());
            Assert.False(log.RootElement.TryGetProperty("prompt", out _));
            Assert.False(log.RootElement.TryGetProperty("response", out _));
        }

        [ModelTheory]
        [InlineData(1)] [InlineData(3)]
        public async Task RepeatedConcurrentCancellationAndReloadLeaveNoOccupiedSlots(int slots)
        {
            int cycles = int.TryParse(Environment.GetEnvironmentVariable("SHARPAI_TEST_SOAK_CYCLES"), out int configured) ? configured : 8;
            Assert.InRange(cycles, 2, 10000);
            List<long> retained = new List<long>();
            for (int cycle = 0; cycle < cycles; cycle++)
            {
                using (LlamaSharpEngine engine = new LlamaSharpEngine { MaxConcurrentGenerations = slots, GenerationQueueTimeoutMs = 10 })
                {
                    await engine.InitializeAsync(ModelFixture.Path!);
                    // Hold all slots across yields, then prove timeout and cancellation release only their own waiters.
                    List<IAsyncEnumerator<string>> held = new List<IAsyncEnumerator<string>>();
                    try
                    {
                        for (int i = 0; i < slots; i++)
                        {
                            IAsyncEnumerator<string>? sequence = engine.GenerateTextStreamAsync("Once upon a time", 8).GetAsyncEnumerator();
                            held.Add(sequence); Assert.True(await sequence.MoveNextAsync());
                        }
                        Assert.Equal(slots, engine.ActiveGenerations);
                        await Assert.ThrowsAsync<EngineBusyException>(() => engine.GenerateTextAsync("Once", 2));
                        using CancellationTokenSource cancelled = new CancellationTokenSource(); cancelled.Cancel();
                        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.GenerateTextAsync("Once", 2, 0, null!, null!, cancelled.Token, new InferenceStatistics()));
                        Assert.Equal(slots, engine.ActiveGenerations);
                    }
                    finally { foreach (IAsyncEnumerator<string>? sequence in held) await sequence.DisposeAsync(); }
                    Assert.Equal(0, engine.ActiveGenerations);
                    await Task.WhenAll(Enumerable.Range(0, slots).Select(_ => engine.GenerateTextAsync("Once upon a time", 2)));
                    Assert.Equal(0, engine.ActiveGenerations);
                }
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                using Process process = Process.GetCurrentProcess(); retained.Add(process.PrivateMemorySize64);
            }
            // Allow allocator warmup/caching. Detect sustained large native leaks, not small RSS noise.
            long allowance = long.TryParse(Environment.GetEnvironmentVariable("SHARPAI_TEST_RETAINED_MEMORY_LIMIT_MB"), out long mb) ? mb * 1024 * 1024 : 64L * 1024 * 1024;
            Assert.True(retained.Skip(1).Max() - retained[1] < allowance, "Retained private memory exceeded allowance: " + String.Join(",", retained));
        }

        [ModelTheory]
        [InlineData(1)]
        public async Task ActiveModelCannotBeEvictedAndCanReloadAfterUnload(int outputTokens)
        {
            using ModelEngineService service = new ModelEngineService(new LoggingModule());
            LlamaSharpEngine engine = await service.GetByModelFileAsync(ModelFixture.Path!);
            await using (IAsyncEnumerator<string>? stream = engine.GenerateTextStreamAsync("Once upon a time", 8).GetAsyncEnumerator())
            {
                Assert.True(await stream.MoveNextAsync());
                Assert.False(service.UnloadModel(ModelFixture.Path!));
                Assert.False(engine.IsDisposed);
            }
            Assert.True(service.UnloadModel(ModelFixture.Path!)); Assert.True(engine.IsDisposed);
            LlamaSharpEngine? reloaded = await service.GetByModelFileAsync(ModelFixture.Path!);
            Assert.NotSame(engine, reloaded); Assert.NotEmpty(await reloaded.GenerateTextAsync("Once", outputTokens));
        }
    }
}
