namespace SharpAI.Serve
{
    using System.Diagnostics;
    using SharpAI.Operations;

    internal sealed class ServeRequestLogState
    {
        internal Stopwatch Elapsed { get; } = Stopwatch.StartNew();
        internal InferenceStatistics? Statistics { get; set; }
    }
}
