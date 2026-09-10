namespace SharpAI.Sdk.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request to import a GGUF model already present on the server's local filesystem (no download).
    /// </summary>
    public class SharpAIImportModelRequest
    {
        /// <summary>
        /// Local filesystem path to the .gguf file, readable by the server process. Required.
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Registry name to assign. When null, the file name (without extension) is used.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; } = null;
    }
}
