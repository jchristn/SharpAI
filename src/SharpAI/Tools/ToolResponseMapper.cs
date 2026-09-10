namespace SharpAI.Tools
{
    using System.Collections.Generic;

    using SharpAI.Models.Ollama;
    using SharpAI.Models.OpenAI;

    /// <summary>
    /// Maps the parsed, model-independent tool calls from <see cref="ToolCallParser"/> onto the OpenAI- and
    /// Ollama-compatible tool-call response shapes emitted by the chat handlers.
    /// </summary>
    public static class ToolResponseMapper
    {
        #region Public-Methods

        /// <summary>
        /// Map parsed tool calls to the OpenAI <c>tool_calls</c> shape. Each call receives a stable id
        /// (<c>call_{index}</c>) unique within the response.
        /// </summary>
        /// <param name="calls">Parsed tool calls.</param>
        /// <returns>OpenAI tool calls; never null.</returns>
        public static List<OpenAIToolCall> ToOpenAI(IReadOnlyList<ParsedToolCall> calls)
        {
            List<OpenAIToolCall> result = new List<OpenAIToolCall>();
            if (calls == null) return result;

            for (int i = 0; i < calls.Count; i++)
            {
                ParsedToolCall call = calls[i];
                if (call == null) continue;

                result.Add(new OpenAIToolCall
                {
                    Id = "call_" + i,
                    Type = "function",
                    Function = new OpenAIToolCallFunction
                    {
                        Name = call.Name,
                        Arguments = call.ArgumentsJson
                    }
                });
            }

            return result;
        }

        /// <summary>
        /// Map parsed tool calls to the Ollama tool-call shape.
        /// </summary>
        /// <param name="calls">Parsed tool calls.</param>
        /// <returns>Ollama tool calls; never null.</returns>
        public static List<OllamaToolCall> ToOllama(IReadOnlyList<ParsedToolCall> calls)
        {
            List<OllamaToolCall> result = new List<OllamaToolCall>();
            if (calls == null) return result;

            foreach (ParsedToolCall call in calls)
            {
                if (call == null) continue;

                result.Add(new OllamaToolCall
                {
                    Function = new OllamaToolCallFunction
                    {
                        Name = call.Name,
                        Arguments = call.ArgumentsJson
                    }
                });
            }

            return result;
        }

        #endregion
    }
}
