namespace SharpAI.Helpers
{
    using System;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Filters model thinking/reasoning tokens from generated output.
    /// Models like Qwen3 emit &lt;think&gt;...&lt;/think&gt; blocks containing chain-of-thought
    /// reasoning that should typically be hidden from end users.
    /// </summary>
    public class ThinkingFilter
    {
        #region Public-Members

        /// <summary>
        /// Gets a value indicating whether the filter is currently inside a thinking block.
        /// </summary>
        public bool InsideThinkingBlock
        {
            get { return _InsideThinkingBlock; }
        }

        #endregion

        #region Private-Members

        private bool _InsideThinkingBlock = false;
        private StringBuilder _Buffer = new StringBuilder();

        private static readonly string _ThinkOpen = "<think>";
        private static readonly string _ThinkClose = "</think>";

        private static readonly Regex _ThinkBlockRegex = new Regex(
            @"<think>[\s\S]*?</think>\s*",
            RegexOptions.Compiled);

        #endregion

        #region Public-Methods

        /// <summary>
        /// Filter a complete (non-streaming) response, removing all thinking blocks.
        /// </summary>
        /// <param name="text">The full response text.</param>
        /// <returns>Text with thinking blocks removed.</returns>
        public static string RemoveThinkingBlocks(string text)
        {
            if (String.IsNullOrEmpty(text)) return text;
            return _ThinkBlockRegex.Replace(text, "").TrimStart();
        }

        /// <summary>
        /// Process a streaming token. Returns the token to emit, or null/empty if it should be suppressed.
        /// Call this for each token in the stream to filter thinking blocks in real time.
        /// </summary>
        /// <param name="token">The next token from the stream.</param>
        /// <returns>The text to emit (may be empty if inside a thinking block), or the buffered text if a partial tag match was resolved.</returns>
        public string ProcessToken(string token)
        {
            if (String.IsNullOrEmpty(token)) return token;

            _Buffer.Append(token);
            string buffered = _Buffer.ToString();

            if (_InsideThinkingBlock)
            {
                int closeIdx = buffered.IndexOf(_ThinkClose, StringComparison.Ordinal);
                if (closeIdx >= 0)
                {
                    // End of thinking block found — discard everything up to and including </think>
                    _InsideThinkingBlock = false;
                    string remainder = buffered.Substring(closeIdx + _ThinkClose.Length).TrimStart();
                    _Buffer.Clear();

                    if (remainder.Length > 0)
                    {
                        // Recurse in case there's another <think> in the remainder
                        return ProcessToken(remainder);
                    }

                    return "";
                }

                // Still inside thinking, keep buffering but don't emit
                // Trim buffer to avoid unbounded growth — keep only last N chars where closing tag could span
                if (_Buffer.Length > _ThinkClose.Length * 2)
                {
                    string keep = buffered.Substring(buffered.Length - _ThinkClose.Length);
                    _Buffer.Clear();
                    _Buffer.Append(keep);
                }

                return "";
            }
            else
            {
                int openIdx = buffered.IndexOf(_ThinkOpen, StringComparison.Ordinal);
                if (openIdx >= 0)
                {
                    // Found opening tag
                    _InsideThinkingBlock = true;
                    string beforeThink = buffered.Substring(0, openIdx);
                    string afterOpen = buffered.Substring(openIdx + _ThinkOpen.Length);
                    _Buffer.Clear();
                    _Buffer.Append(afterOpen);

                    // Check if closing tag is already in the buffer
                    string result = ProcessToken("");
                    return beforeThink + result;
                }

                // Check if buffer ends with a partial match of "<think>"
                bool couldBePartialTag = false;
                for (int len = 1; len < _ThinkOpen.Length && len <= buffered.Length; len++)
                {
                    if (buffered.EndsWith(_ThinkOpen.Substring(0, len), StringComparison.Ordinal))
                    {
                        couldBePartialTag = true;
                        break;
                    }
                }

                if (couldBePartialTag)
                {
                    // Don't emit the potential partial tag yet, keep buffering
                    return "";
                }

                // No tag, emit everything
                _Buffer.Clear();
                return buffered;
            }
        }

        /// <summary>
        /// Flush any remaining buffered content. Call this when the stream ends.
        /// </summary>
        /// <returns>Any remaining buffered text that should be emitted.</returns>
        public string Flush()
        {
            string remaining = _Buffer.ToString();
            _Buffer.Clear();
            _InsideThinkingBlock = false;

            // If we were inside a thinking block, discard the remaining
            // If not, return whatever was buffered (partial tag that never completed)
            return remaining;
        }

        #endregion
    }
}
