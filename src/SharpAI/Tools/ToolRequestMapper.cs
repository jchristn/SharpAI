namespace SharpAI.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    using SharpAI.Models.Ollama;
    using SharpAI.Models.OpenAI;

    /// <summary>
    /// Maps the provider-specific tool request shapes (OpenAI <c>tools[]</c> and Ollama <c>tools[]</c>) onto
    /// the model-independent <see cref="ToolDefinition"/> list consumed by <see cref="ToolPromptBuilder"/>.
    /// Parameter schemas are carried through as their JSON text.
    /// </summary>
    public static class ToolRequestMapper
    {
        #region Private-Members

        private static readonly JsonSerializerOptions _JsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        #endregion

        #region Public-Methods

        /// <summary>
        /// Convert OpenAI tool definitions to the model-independent form. Returns an empty list when the
        /// input is null or empty.
        /// </summary>
        /// <param name="tools">OpenAI tools.</param>
        /// <returns>Model-independent tool definitions; never null.</returns>
        public static List<ToolDefinition> FromOpenAI(IReadOnlyList<OpenAITool> tools)
        {
            List<ToolDefinition> result = new List<ToolDefinition>();
            if (tools == null) return result;

            foreach (OpenAITool tool in tools)
            {
                if (tool == null || tool.Function == null || String.IsNullOrEmpty(tool.Function.Name)) continue;

                result.Add(new ToolDefinition(
                    tool.Function.Name,
                    tool.Function.Description,
                    SerializeParameters(tool.Function.Parameters)));
            }

            return result;
        }

        /// <summary>
        /// Convert Ollama tool definitions to the model-independent form. Returns an empty list when the
        /// input is null or empty.
        /// </summary>
        /// <param name="tools">Ollama tools.</param>
        /// <returns>Model-independent tool definitions; never null.</returns>
        public static List<ToolDefinition> FromOllama(IReadOnlyList<OllamaTool> tools)
        {
            List<ToolDefinition> result = new List<ToolDefinition>();
            if (tools == null) return result;

            foreach (OllamaTool tool in tools)
            {
                if (tool == null || tool.Function == null || String.IsNullOrEmpty(tool.Function.Name)) continue;

                result.Add(new ToolDefinition(
                    tool.Function.Name,
                    tool.Function.Description,
                    SerializeParameters(tool.Function.Parameters)));
            }

            return result;
        }

        #endregion

        #region Private-Methods

        private static string SerializeParameters(object parameters)
        {
            if (parameters == null) return null;

            try
            {
                return JsonSerializer.Serialize(parameters, _JsonOptions);
            }
            catch (Exception)
            {
                return null;
            }
        }

        #endregion
    }
}
