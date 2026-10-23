namespace SharpAI.Serve
{
    using System.Diagnostics;
    using SharpAI.Operations;

    internal sealed class ServeRequestLogState
    {
        internal Stopwatch Elapsed { get; } = Stopwatch.StartNew();
        internal string RequestId { get; } = System.Guid.NewGuid().ToString();
        internal string? Model { get; set; }
        internal bool? Stream { get; set; }
        internal long StreamChunks;
        internal long StreamPayloadBytes;
        internal double? FirstStreamWriteMs { get; set; }
        internal double? LastStreamWriteMs { get; set; }
        internal InferenceStatistics? Statistics { get; set; }
    }
}
