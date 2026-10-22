namespace SharpAI.Exceptions
{
    using System;

    /// <summary>A rendered prompt and requested generation cannot fit in the configured context.</summary>
    public sealed class ContextLimitException : ArgumentException
    {
        /// <summary>Create an overflow error with explicit token counts.</summary>
        /// <param name="promptTokens">Rendered prompt tokens.</param><param name="outputTokens">Requested output tokens.</param>
        /// <param name="limit">Effective context limit.</param>
        public ContextLimitException(int promptTokens, int outputTokens, int limit)
            : base($"Context limit exceeded: prompt={promptTokens}, requested_output={outputTokens}, limit={limit}.") { }

        /// <summary>Reject a request before streaming or native evaluation when its reservation exceeds context.</summary>
        /// <param name="promptTokens">Rendered prompt tokens including special tokens.</param>
        /// <param name="outputTokens">Requested generation budget.</param><param name="limit">Effective context.</param>
        public static void Validate(int promptTokens, int outputTokens, int limit)
        {
            if (promptTokens < 0) throw new ArgumentOutOfRangeException(nameof(promptTokens));
            if (outputTokens <= 0) throw new ArgumentOutOfRangeException(nameof(outputTokens));
            if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
            if ((long)promptTokens + outputTokens > limit) throw new ContextLimitException(promptTokens, outputTokens, limit);
        }
    }
}
