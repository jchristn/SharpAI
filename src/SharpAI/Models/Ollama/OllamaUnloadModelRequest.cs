namespace SharpAI.Models.Ollama
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ollama unload model request.
    /// Unloads a model from memory, freeing GPU/CPU resources.
    /// </summary>
    public class OllamaUnloadModelRequest
    {
        /// <summary>
        /// Name of the model to unload (required).
        /// Following Ollama convention, this uses 'name' in JSON but 'Model' in code.
        /// </summary>
        [JsonPropertyName("name")]
        public string Model
        {
            get => _Model;
            set
            {
                if (String.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("Model name cannot be null or empty", nameof(Model));

                // Validate model name format if it contains a tag
                if (value.Contains(':'))
                {
                    string[] parts = value.Split(':');
                    if (parts.Length != 2)
                        throw new ArgumentException("Model name format should be 'name' or 'name:tag'", nameof(Model));

                    if (String.IsNullOrWhiteSpace(parts[0]))
                        throw new ArgumentException("Model base name cannot be empty", nameof(Model));

                    if (String.IsNullOrWhiteSpace(parts[1]))
                        throw new ArgumentException("Model tag cannot be empty when colon is present", nameof(Model));
                }

                _Model = value;
            }
        }

        private string _Model;

        /// <summary>
        /// Ollama unload model request.
        /// </summary>
        public OllamaUnloadModelRequest()
        {
        }
    }
}
