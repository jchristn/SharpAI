namespace SharpAI.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Renders tool/function definitions into a system-prompt instruction that asks the model to emit tool
    /// calls in the <c>&lt;tool_call&gt;{...}&lt;/tool_call&gt;</c> format that <see cref="ToolCallParser"/>
    /// understands. This is the model-independent half of tool calling — the instruction is injected into
    /// the prompt, and the model's output is parsed back with <see cref="ToolCallParser.Parse"/>.
    /// </summary>
    public static class ToolPromptBuilder
    {
        #region Public-Methods

        /// <summary>
        /// Build the system-prompt instruction describing the available tools. Returns an empty string when
        /// no tools are supplied (so callers can inject unconditionally).
        /// </summary>
        /// <param name="tools">Tool definitions.</param>
        /// <returns>The instruction text, or an empty string.</returns>
        public static string BuildSystemInstruction(IReadOnlyList<ToolDefinition> tools)
        {
            if (tools == null || tools.Count == 0) return String.Empty;

            StringBuilder builder = new StringBuilder();
            builder.Append("You have access to the following tools. When you need to call a tool, respond with a JSON ");
            builder.Append("object wrapped in <tool_call></tool_call> tags, for example: ");
            builder.Append("<tool_call>{\"name\": \"tool_name\", \"arguments\": {\"key\": \"value\"}}</tool_call>. ");
            builder.Append("Only call a tool when it is needed, and use exactly the parameter names listed.");
            builder.Append('\n');
            builder.Append('\n');
            builder.Append("Available tools:");
            builder.Append('\n');

            foreach (ToolDefinition tool in tools)
            {
                if (tool == null || String.IsNullOrEmpty(tool.Name)) continue;

                builder.Append("- ");
                builder.Append(tool.Name);
                if (!String.IsNullOrEmpty(tool.Description))
                {
                    builder.Append(": ");
                    builder.Append(tool.Description);
                }
                if (!String.IsNullOrEmpty(tool.ParametersJson))
                {
                    builder.Append(" Parameters (JSON Schema): ");
                    builder.Append(tool.ParametersJson);
                }
                builder.Append('\n');
            }

            return builder.ToString();
        }

        #endregion
    }
}
