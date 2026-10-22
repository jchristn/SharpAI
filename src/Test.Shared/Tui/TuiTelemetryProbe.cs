namespace Test.Shared.Tui
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Client;
    using SharpAI.Tui.Services;

    /// <summary>Exports one real client request, screen, failure and correlated log into an isolated OTLP stack.</summary>
    public static class TuiTelemetryProbe
    {
        /// <summary>Emit telemetry without opening a terminal or logging request data. Returns the root trace identifier.</summary>
        public static async Task<string> RunAsync(string collector, string logs, string api, CancellationToken cancellationToken)
        {
            using ClientTelemetry telemetry = new ClientTelemetry(logs, true, collector);
            using ActivitySource source = new ActivitySource("SharpAI.Tui");
            using Activity? root = source.StartActivity("tui.probe", ActivityKind.Internal);
            string trace = root?.TraceId.ToString() ?? throw new InvalidOperationException("Telemetry source was not subscribed.");
            telemetry.Screen("/models"); telemetry.Failure(new InvalidOperationException("Synthetic telemetry probe"));
            using ServerClient client = new ServerClient(api);
            await client.JsonAsync("GET", "/health", null, cancellationToken).ConfigureAwait(false);
            return trace;
        }
    }
}
