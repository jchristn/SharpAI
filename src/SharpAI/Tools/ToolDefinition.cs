namespace SharpAI.Tools
{
    using System;

    /// <summary>
    /// A model-independent description of a callable tool/function, used to render tool definitions into a
    /// prompt. The Ollama and OpenAI request handlers map their respective tool shapes onto this type.
    /// </summary>
    public class ToolDefinition
    {
        #region Public-Members

        /// <summary>
        /// Tool/function name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Human-readable description of what the tool does.
        /// </summary>
        public string Description { get; set; } = String.Empty;

        /// <summary>
        /// JSON Schema (as a JSON string) describing the tool's parameters, or null when it takes none.
        /// </summary>
        public string ParametersJson { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ToolDefinition()
        {
        }

        /// <summary>
        /// Instantiate with values.
        /// </summary>
        /// <param name="name">Tool name.</param>
        /// <param name="description">Tool description.</param>
        /// <param name="parametersJson">Parameters JSON schema, or null.</param>
        public ToolDefinition(string name, string description, string parametersJson)
        {
            Name = name ?? String.Empty;
            Description = description ?? String.Empty;
            ParametersJson = parametersJson;
        }

        #endregion
    }
}
