namespace SharpAI.Client
{
    using System.Text.Json.Serialization;

    /// <summary>One public NDJSON pull update. Progress is reported by the server, never estimated by the client.</summary>
    public sealed class PullProgress
    {
        /// <summary>Phase name. Only success confirms publication.</summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
        /// <summary>Downloaded bytes, or null when unavailable.</summary>
        [JsonPropertyName("downloaded")]
        public long? Downloaded { get; set; }
        /// <summary>Server progress fraction from zero through one, or null when unavailable.</summary>
        [JsonPropertyName("percent")]
        public decimal? Percent { get; set; }
    }
}
