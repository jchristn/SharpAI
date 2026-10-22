namespace SharpAI.Client
{
    using System;
    using System.Collections.Generic;

    /// <summary>Persisted public history fields. Nullable measurements distinguish unavailable values from zero. Mutable; not thread safe.</summary>
    public sealed class HistoryRecord
    {
        /// <summary>Server Id value. Timing fields use milliseconds.</summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>Server TenantId value. Timing fields use milliseconds.</summary>
        public string TenantId { get; set; } = string.Empty;
        /// <summary>Server UserId value. Timing fields use milliseconds.</summary>
        public string UserId { get; set; } = string.Empty;
        /// <summary>Server PrincipalName value. Timing fields use milliseconds.</summary>
        public string PrincipalName { get; set; } = string.Empty;
        /// <summary>Server Method value. Timing fields use milliseconds.</summary>
        public string Method { get; set; } = string.Empty;
        /// <summary>Server Path value. Timing fields use milliseconds.</summary>
        public string Path { get; set; } = string.Empty;
        /// <summary>Server Url value. Timing fields use milliseconds.</summary>
        public string Url { get; set; } = string.Empty;
        /// <summary>Server StatusCode value. Timing fields use milliseconds.</summary>
        public int StatusCode { get; set; }
        /// <summary>Server DurationMs value. Timing fields use milliseconds.</summary>
        public double DurationMs { get; set; }
        /// <summary>Server SourceIp value. Timing fields use milliseconds.</summary>
        public string SourceIp { get; set; } = string.Empty;
        /// <summary>Server RequestBody value. Timing fields use milliseconds.</summary>
        public string RequestBody { get; set; } = string.Empty;
        /// <summary>Server RequestBodyBytes value. Timing fields use milliseconds.</summary>
        public long RequestBodyBytes { get; set; }
        /// <summary>Server RequestBodyTruncated value. Timing fields use milliseconds.</summary>
        public bool RequestBodyTruncated { get; set; }
        /// <summary>Server ResponseBody value. Timing fields use milliseconds.</summary>
        public string ResponseBody { get; set; } = string.Empty;
        /// <summary>Server ResponseBodyBytes value. Timing fields use milliseconds.</summary>
        public long ResponseBodyBytes { get; set; }
        /// <summary>Server ResponseBodyTruncated value. Timing fields use milliseconds.</summary>
        public bool ResponseBodyTruncated { get; set; }
        /// <summary>Server CreatedUtc value. Timing fields use milliseconds.</summary>
        public DateTime CreatedUtc { get; set; }
        /// <summary>Server CompletedUtc value. Timing fields use milliseconds.</summary>
        public DateTime? CompletedUtc { get; set; }
        /// <summary>Server ModelName value. Timing fields use milliseconds.</summary>
        public string ModelName { get; set; } = string.Empty;
        /// <summary>Server IsStreaming value. Timing fields use milliseconds.</summary>
        public bool? IsStreaming { get; set; }
        /// <summary>Server InputTokens value. Timing fields use milliseconds.</summary>
        public int? InputTokens { get; set; }
        /// <summary>Server OutputTokens value. Timing fields use milliseconds.</summary>
        public int? OutputTokens { get; set; }
        /// <summary>Server CachedTokens value. Timing fields use milliseconds.</summary>
        public int? CachedTokens { get; set; }
        /// <summary>Server TtftMs value. Timing fields use milliseconds.</summary>
        public double? TtftMs { get; set; }
        /// <summary>Server StreamDurationMs value. Timing fields use milliseconds.</summary>
        public double? StreamDurationMs { get; set; }
        /// <summary>Server QueueMs value. Timing fields use milliseconds.</summary>
        public double? QueueMs { get; set; }
        /// <summary>Server LoadMs value. Timing fields use milliseconds.</summary>
        public double? LoadMs { get; set; }
        /// <summary>Server PromptMs value. Timing fields use milliseconds.</summary>
        public double? PromptMs { get; set; }
        /// <summary>Server GenerationMs value. Timing fields use milliseconds.</summary>
        public double? GenerationMs { get; set; }
        /// <summary>Server EngineTotalMs value. Timing fields use milliseconds.</summary>
        public double? EngineTotalMs { get; set; }
        /// <summary>Server Cancelled value. Timing fields use milliseconds.</summary>
        public bool? Cancelled { get; set; }
        /// <summary>Server PhaseTimingsApproximate value. Timing fields use milliseconds.</summary>
        public bool? PhaseTimingsApproximate { get; set; }
        /// <summary>Server TraceId value. Timing fields use milliseconds.</summary>
        public string TraceId { get; set; } = string.Empty;
        /// <summary>Server SpanId value. Timing fields use milliseconds.</summary>
        public string SpanId { get; set; } = string.Empty;
        /// <summary>Server-redacted RequestHeaders.</summary>
        public Dictionary<string, string> RequestHeaders { get; set; } = new Dictionary<string, string>();
        /// <summary>Server-redacted ResponseHeaders.</summary>
        public Dictionary<string, string> ResponseHeaders { get; set; } = new Dictionary<string, string>();
    }
}
