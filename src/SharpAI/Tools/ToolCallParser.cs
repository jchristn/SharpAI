namespace SharpAI.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>
    /// Extracts tool/function calls from a model's raw text output. Handles the common formats emitted by
    /// tool-capable GGUF models: one or more <c>&lt;tool_call&gt;{...}&lt;/tool_call&gt;</c> blocks
    /// (Qwen/Hermes style), a bare JSON object, or a JSON array of calls. Each call may use either
    /// <c>{"name","arguments"}</c> or a nested <c>{"function":{"name",...}}</c> shape. Output that
    /// contains no recognizable tool call yields an empty list, so ordinary text responses are unaffected.
    /// </summary>
    public static class ToolCallParser
    {
        #region Public-Methods

        /// <summary>
        /// Parse tool calls from model output.
        /// </summary>
        /// <param name="modelOutput">The raw text produced by the model.</param>
        /// <returns>The tool calls found, in order. Empty when the output is not a tool call.</returns>
        public static List<ParsedToolCall> Parse(string modelOutput)
        {
            List<ParsedToolCall> results = new List<ParsedToolCall>();
            if (String.IsNullOrWhiteSpace(modelOutput)) return results;

            // Preferred: one or more <tool_call> tags. Small models frequently omit the closing </tool_call>,
            // so rather than requiring a matched pair we scan from each opening tag and extract the first
            // balanced JSON object (or array of objects) that follows.
            if (modelOutput.IndexOf("<tool_call>", StringComparison.Ordinal) >= 0)
            {
                int searchFrom = 0;
                while (true)
                {
                    int tagIndex = modelOutput.IndexOf("<tool_call>", searchFrom, StringComparison.Ordinal);
                    if (tagIndex < 0) break;

                    int jsonStart = tagIndex + "<tool_call>".Length;
                    string extracted = ExtractBalancedJson(modelOutput, jsonStart, out int consumedTo);
                    if (extracted != null)
                    {
                        string inner = extracted.TrimStart();
                        if (inner.StartsWith("[", StringComparison.Ordinal)) TryParseArray(inner, results);
                        else
                        {
                            ParsedToolCall call = TryParseObject(inner);
                            if (call != null) results.Add(call);
                        }
                        searchFrom = consumedTo > tagIndex ? consumedTo : jsonStart;
                    }
                    else
                    {
                        searchFrom = jsonStart;
                    }
                }

                if (results.Count > 0) return results;
            }

            string trimmed = modelOutput.Trim();
            if (trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                TryParseArray(trimmed, results);
                return results;
            }

            if (trimmed.StartsWith("{", StringComparison.Ordinal))
            {
                ParsedToolCall call = TryParseObject(trimmed);
                if (call != null) results.Add(call);
            }

            return results;
        }

        // Extract the first balanced JSON object or array beginning at or after startIndex, respecting
        // braces/brackets inside strings and escape sequences. Returns null when none is found.
        private static string ExtractBalancedJson(string text, int startIndex, out int endIndex)
        {
            endIndex = startIndex;

            int i = startIndex;
            while (i < text.Length && text[i] != '{' && text[i] != '[') i++;
            if (i >= text.Length) return null;

            char open = text[i];
            char close = open == '{' ? '}' : ']';
            int depth = 0;
            bool inString = false;
            bool escape = false;
            int begin = i;

            for (; i < text.Length; i++)
            {
                char c = text[i];

                if (inString)
                {
                    if (escape) escape = false;
                    else if (c == '\\') escape = true;
                    else if (c == '"') inString = false;
                    continue;
                }

                if (c == '"') { inString = true; continue; }
                if (c == open) depth++;
                else if (c == close)
                {
                    depth--;
                    if (depth == 0)
                    {
                        endIndex = i + 1;
                        return text.Substring(begin, endIndex - begin);
                    }
                }
            }

            return null;
        }

        #endregion

        #region Private-Methods

        private static void TryParseArray(string json, List<ParsedToolCall> results)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement element in doc.RootElement.EnumerateArray())
                        {
                            ParsedToolCall call = FromElement(element);
                            if (call != null) results.Add(call);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Not valid JSON; treat as no tool calls.
            }
        }

        private static ParsedToolCall TryParseObject(string json)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    return FromElement(doc.RootElement);
                }
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static ParsedToolCall FromElement(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object) return null;

            string name = null;

            if (element.TryGetProperty("name", out JsonElement nameElement) && nameElement.ValueKind == JsonValueKind.String)
            {
                name = nameElement.GetString();
            }
            else if (element.TryGetProperty("function", out JsonElement functionElement)
                && functionElement.ValueKind == JsonValueKind.Object
                && functionElement.TryGetProperty("name", out JsonElement functionName)
                && functionName.ValueKind == JsonValueKind.String)
            {
                name = functionName.GetString();

                if (functionElement.TryGetProperty("arguments", out JsonElement functionArgs))
                {
                    return new ParsedToolCall
                    {
                        Name = name,
                        ArgumentsJson = functionArgs.ValueKind == JsonValueKind.String ? functionArgs.GetString() : functionArgs.GetRawText()
                    };
                }
            }

            if (String.IsNullOrEmpty(name)) return null;

            string argumentsJson = "{}";
            if (element.TryGetProperty("arguments", out JsonElement argsElement))
            {
                argumentsJson = argsElement.ValueKind == JsonValueKind.String ? argsElement.GetString() : argsElement.GetRawText();
            }
            else if (element.TryGetProperty("parameters", out JsonElement parametersElement))
            {
                argumentsJson = parametersElement.GetRawText();
            }

            return new ParsedToolCall { Name = name, ArgumentsJson = argumentsJson };
        }

        #endregion
    }
}
