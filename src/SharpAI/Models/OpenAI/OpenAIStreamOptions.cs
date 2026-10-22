namespace SharpAI.Models.OpenAI
{
    using System.Text.Json.Serialization;

    /// <summary>Options for OpenAI-compatible streams.</summary>
    public sealed class OpenAIStreamOptions
    {
        private bool _IncludeUsage;
        /// <summary>Emit an aggregate usage chunk with empty choices immediately before DONE. Default false.</summary>
        [JsonPropertyName("include_usage")]
        public bool IncludeUsage { get { return _IncludeUsage; } set { _IncludeUsage = value; } }
    }
}
