namespace SharpAI.Client
{
    using System.Collections.Generic;
    using System.Net;
    using System.Text.Json.Nodes;

    /// <summary>Bounded response inspection. Instances belong to one request and are not thread safe.</summary>
    public sealed class ApiResponse
    {
        /// <summary>Server status code.</summary>
        public HttpStatusCode StatusCode { get; internal set; }
        /// <summary>Response and content headers.</summary>
        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>();
        /// <summary>Response text, bounded by the transport limit.</summary>
        public string Body { get; internal set; } = string.Empty;
        /// <summary>True when bytes beyond the inspection limit were withheld.</summary>
        public bool Truncated { get; internal set; }
        /// <summary>Parse JSON when a structured response is expected. Null for an empty response.</summary>
        public JsonNode? Json() { return string.IsNullOrWhiteSpace(Body) ? null : JsonNode.Parse(Body); }
    }
}
