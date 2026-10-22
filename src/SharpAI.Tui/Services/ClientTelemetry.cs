namespace SharpAI.Tui.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.IO;
    using System.Text.Json;
    using Microsoft.Extensions.Logging;
    using Radiant;

    internal sealed class ClientTelemetry : IDisposable
    {
        private readonly StreamWriter _Log;
        private readonly RadiantHost? _Host;
        private readonly ILogger? _Logger;
        private readonly ActivitySource _Activities = new ActivitySource("SharpAI.Tui");
        private readonly Meter _Meter = new Meter("SharpAI.Tui");
        private readonly Counter<long> _Failures;
        internal ClientTelemetry(string directory, bool enabled, string? endpoint)
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "tui-" + DateTime.UtcNow.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture) + ".jsonl");
            _Log = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            _Failures = _Meter.CreateCounter<long>("sharpai.tui.failures");
            if (enabled)
            {
                RadiantSettings settings = new RadiantSettings("sharpai-tui"); settings.Enable = true;
                settings.Otlp.Enable = true; settings.Otlp.Endpoint = endpoint ?? "http://127.0.0.1:4317";
                settings.Prometheus.Enable = false;
                settings.Sources.AddMeter("SharpAI.Tui"); settings.Sources.AddMeter("SharpAI.Client");
                settings.Sources.AddActivitySource("SharpAI.Tui"); settings.Sources.AddActivitySource("SharpAI.Client");
                _Host = RadiantHost.Start(settings); _Logger = _Host.CreateLogger("SharpAI.Tui");
            }
            Record("startup", "ok");
        }
        internal void Screen(string route)
        {
            using Activity? activity = _Activities.StartActivity("screen.open"); activity?.SetTag("screen", route); Record("screen", route);
        }
        internal void Failure(Exception error)
        {
            string type = error.GetType().Name; _Failures.Add(1, new KeyValuePair<string, object?>("error.type", type)); Record("failure", type);
        }
        private void Record(string name, string value)
        {
            lock (_Log) _Log.WriteLine(JsonSerializer.Serialize(new { timestamp = DateTimeOffset.UtcNow, service = "sharpai-tui", name, value, trace_id = Activity.Current?.TraceId.ToString(), span_id = Activity.Current?.SpanId.ToString() }));
            _Logger?.LogInformation("{Event} {Outcome}", name, value);
        }
        public void Dispose() { Record("shutdown", "ok"); _Host?.Dispose(); _Activities.Dispose(); _Meter.Dispose(); _Log.Dispose(); }
    }
}
