namespace SharpAI.Operations
{
    using System;
    using LLama.Native;
    using LLama.Sampling;

    internal sealed class MeasuredSamplingPipeline : DefaultSamplingPipeline
    {
        private readonly Action<SafeLLamaContextHandle, LLamaToken> _Sampled;
        internal MeasuredSamplingPipeline(Action<SafeLLamaContextHandle, LLamaToken> sampled, SharpAI.Models.Ollama.OllamaCompletionOptions sampling = null)
        {
            _Sampled = sampled;
            if (sampling != null)
            {
                TopP = sampling.TopP ?? TopP;
                TopK = sampling.TopK ?? TopK;
                MinP = sampling.MinP ?? MinP;
                RepeatPenalty = sampling.RepeatPenalty ?? RepeatPenalty;
                PenaltyCount = sampling.RepeatLastN ?? PenaltyCount;
                PresencePenalty = sampling.PresencePenalty ?? PresencePenalty;
                FrequencyPenalty = sampling.FrequencyPenalty ?? FrequencyPenalty;
                if (sampling.Seed.HasValue) Seed = unchecked((uint)sampling.Seed.Value);
            }
        }
        public override LLamaToken Sample(SafeLLamaContextHandle context, int index)
        {
            LLamaToken token = base.Sample(context, index);
            _Sampled(context, token);
            return token;
        }
    }
}
