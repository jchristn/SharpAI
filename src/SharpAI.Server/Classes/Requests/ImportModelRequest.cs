namespace SharpAI.Server.Classes.Requests
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request to import a GGUF model that already exists on the server's local filesystem into the model
    /// registry, without downloading it from a remote source and without any HuggingFace token. The file is
    /// copied into the configured models directory and its capabilities are detected from GGUF metadata.
    /// </summary>
    public class ImportModelRequest
    {
        #region Public-Members

        /// <summary>
        /// Absolute or relative path to a local .gguf file readable by the server process. Required.
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; } = null;

        /// <summary>
        /// Registry name to assign to the imported model. When null or empty, the file name (without its
        /// extension) is used.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Request to import a local GGUF model into the registry.
        /// </summary>
        public ImportModelRequest()
        {

        }

        #endregion
    }
}
