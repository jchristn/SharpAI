namespace SharpAI.Sdk.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request to create a Modelfile-equivalent preset layered over a base model.
    /// </summary>
    public class SharpAICreatePresetRequest
    {
        /// <summary>
        /// Preset name (used as the model value in inference requests). Required.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Base model name in the registry. Required.
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Default system prompt, or null.
        /// </summary>
        [JsonPropertyName("system")]
        public string? System { get; set; } = null;

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
        public string? Template { get; set; } = null;

        /// <summary>
        /// Default stop sequences, or null.
        /// </summary>
        [JsonPropertyName("stop")]
        public List<string>? Stop { get; set; } = null;
    }
}
