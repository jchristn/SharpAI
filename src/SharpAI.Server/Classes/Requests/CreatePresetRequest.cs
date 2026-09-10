namespace SharpAI.Server.Classes.Requests
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request to create or update a Modelfile-equivalent preset (system prompt, sampling defaults, stop
    /// sequences, and optional template override) layered over a base model.
    /// </summary>
    public class CreatePresetRequest
    {
        #region Public-Members

        /// <summary>
        /// Preset name (used as the model value in inference requests). Required.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = null;

        /// <summary>
        /// Base model name in the registry that the preset runs against. Required.
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = null;

        /// <summary>
        /// Default system prompt, or null.
        /// </summary>
        [JsonPropertyName("system")]
        public string System { get; set; } = null;

        /// <summary>
        /// Default sampling temperature, or null.
        /// </summary>
        [JsonPropertyName("temperature")]
        public float? Temperature { get; set; } = null;

        /// <summary>
        /// Default maximum tokens to generate, or null.
        /// </summary>
        [JsonPropertyName("max_tokens")]
        public int? MaxTokens { get; set; } = null;

        /// <summary>
        /// Default top-p value, or null.
        /// </summary>
        [JsonPropertyName("top_p")]
        public float? TopP { get; set; } = null;

        /// <summary>
        /// Optional raw chat-template override, or null.
        /// </summary>
        [JsonPropertyName("template")]
        public string Template { get; set; } = null;

        /// <summary>
        /// Default stop sequences, or null.
        /// </summary>
        [JsonPropertyName("stop")]
        public List<string> Stop { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Request to create or update a model preset.
        /// </summary>
        public CreatePresetRequest()
        {
        }

        #endregion
    }
}
