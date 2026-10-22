namespace Test.Xunit
{
    using System;
    using System.Reflection;
    using SharpAI.Engines;
    using SharpAI.Models.Ollama;
    using Xunit;

    /// <summary>Sampling controls must reach the per-request pipeline without changing unrelated defaults.</summary>
    public sealed class SamplingOptionsTests
    {
        /// <summary>The pipeline applies each requested control and keeps requests isolated.</summary>
        [Fact]
        public void RequestedSamplingControlsAreAppliedAndIsolated()
        {
            Type type = typeof(LlamaSharpEngine).Assembly.GetType("SharpAI.Operations.MeasuredSamplingPipeline", true)!;
            ConstructorInfo constructor = Assert.Single(type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic));
            object baseline = constructor.Invoke(new object?[] { null, null });
            OllamaCompletionOptions options = new OllamaCompletionOptions
            {
                TopP = 0.35f, TopK = 12, MinP = 0.15f, RepeatPenalty = 1.25f,
                RepeatLastN = 96, PresencePenalty = 0.2f, FrequencyPenalty = 0.4f, Seed = 42
            };
            object requested = constructor.Invoke(new object?[] { null, options });
            Assert.Equal(0.35f, type.GetProperty("TopP")!.GetValue(requested));
            Assert.Equal(12, type.GetProperty("TopK")!.GetValue(requested));
            Assert.Equal(0.15f, type.GetProperty("MinP")!.GetValue(requested));
            Assert.Equal(1.25f, type.GetProperty("RepeatPenalty")!.GetValue(requested));
            Assert.Equal(96, type.GetProperty("PenaltyCount")!.GetValue(requested));
            Assert.Equal(0.2f, type.GetProperty("PresencePenalty")!.GetValue(requested));
            Assert.Equal(0.4f, type.GetProperty("FrequencyPenalty")!.GetValue(requested));
            Assert.Equal(42u, type.GetProperty("Seed")!.GetValue(requested));
            object next = constructor.Invoke(new object?[] { null, new OllamaCompletionOptions() });
            foreach (string name in new[] { "TopP", "TopK", "MinP", "RepeatPenalty", "PenaltyCount", "PresencePenalty", "FrequencyPenalty" })
                Assert.Equal(type.GetProperty(name)!.GetValue(baseline), type.GetProperty(name)!.GetValue(next));
            ((IDisposable)baseline).Dispose();
            ((IDisposable)requested).Dispose();
            ((IDisposable)next).Dispose();
        }
    }
}
