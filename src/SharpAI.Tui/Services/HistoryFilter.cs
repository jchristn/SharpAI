namespace SharpAI.Tui.Services
{
    using System;

    internal sealed class HistoryFilter
    {
        internal string Method { get; set; } = string.Empty;
        internal string Status { get; set; } = string.Empty;
        internal string Path { get; set; } = string.Empty;
        internal string Endpoint { get; set; } = string.Empty;
        internal int Hours { get; set; } = 24;
        internal int BucketMinutes { get; set; } = 15;
        internal DateTimeOffset Anchor { get; set; } = DateTimeOffset.UtcNow;
        internal string? DrillFrom { get; set; }
        internal string? DrillTo { get; set; }
    }
}
