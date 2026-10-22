namespace SharpAI.Tui
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using SharpAI.Client;
    using SharpAI.Tui.Services;
    using TUIKit.Layout;
    using TUIKit.Widgets;

    internal sealed partial class SharpAITuiApp
    {
        private Timer? _ScreenTimer;
        private void Observability()
        {
            DefinitionList data = new DefinitionList(); ButtonRow links = new ButtonRow();
            Uri server = new Uri(_Profile.Server);
            foreach (string service in new[] { "Grafana", "Prometheus", "Loki", "Tempo" })
            {
                int port = service switch { "Grafana" => 9400, "Prometheus" => 9090, "Loki" => 3100, _ => 3200 };
                string url = _Profile.ServiceLinks.TryGetValue(service, out string? link) ? link : new UriBuilder(server.Scheme, server.Host, port, service == "Grafana" ? "/dashboards/f/sharpai" : service == "Prometheus" ? "/graph" : "/ready").Uri.AbsoluteUri;
                data.Set(service, SafeText.Clean(url)); links.Add(ButtonFor("Copy " + service + " URL", () => { _CopyText = url; Track(CopyAsync()); }));
            }
            Show(new FramedStack().Add(new ScrollView(data, 100, 150), StackSize.Weighted(1)).Add(links, StackSize.Fixed(4)), "Observability"); long revision = _Revision;
            MetricsSample? previous = null; bool polling = false;
            _Refresh = () =>
            {
                if (polling) return; polling = true;
                Work(async ct =>
                {
                    try
                    {
                        foreach (string path in new[] { "/health", "/ready", "/metrics" })
                        {
                            try { ApiResponse result = await _Client.ExecuteAsync("GET", path, null, ct).ConfigureAwait(false); Apply(revision, () => { data.Set(path, "HTTP " + (int)result.StatusCode); if (path == "/health") _HealthStatus = (int)result.StatusCode; if (path == "/ready") _ReadyStatus = (int)result.StatusCode; }); }
                            catch (Exception error) when (IsExpected(error)) { Apply(revision, () => data.Set(path, "Unavailable: " + ErrorText(error))); }
                        }
                        if (string.IsNullOrWhiteSpace(_Profile.EngineMetrics)) { Apply(revision, () => data.Set("Engine metrics", "Unavailable. Configure a collector endpoint separately from Watson HTTP metrics.")); return; }
                        Uri endpoint = new Uri(_Profile.EngineMetrics);
                        using ServerClient engine = new ServerClient(endpoint.GetLeftPart(UriPartial.Authority));
                        ApiResponse metrics = await engine.ExecuteAsync("GET", endpoint.PathAndQuery, null, ct).ConfigureAwait(false);
                        if ((int)metrics.StatusCode != 200 || metrics.Truncated) throw new InvalidOperationException("Engine metric collection failed.");
                        MetricsSample sample = MetricsSample.Parse(metrics.Body);
                        double? requestRate = sample.Rate(previous, "sharpai_inference_requests_total");
                        double? tokenRate = sample.Rate(previous, "sharpai_inference_tokens_generated_total");
                        double? sum = sample.Find("sharpai_inference_latency_seconds_sum"); double? count = sample.Find("sharpai_inference_latency_seconds_count");
                        previous = sample;
                        Apply(revision, () =>
                        {
                            data.Set("Engine metrics", sample.Find("sharpai_inference_requests_total").HasValue ? sample.Captured.ToString("O", _Localizer.Culture) : "Unavailable: engine series absent.");
                            data.Set("Requests / second", FormatMeasurement(requestRate)); data.Set("Tokens / second", FormatMeasurement(tokenRate));
                            data.Set("Resident models", FormatMeasurement(sample.Find("sharpai_models_resident")));
                            data.Set("Average latency ms", FormatMeasurement(count > 0 ? sum / count * 1000 : null));
                        });
                    }
                    catch (Exception error) when (IsExpected(error)) { previous = null; Apply(revision, () => { data.Set("Engine metrics", "Unavailable: " + ErrorText(error)); foreach (string key in new[] { "Requests / second", "Tokens / second", "Resident models", "Average latency ms" }) data.Set(key, "Unavailable"); }); }
                    finally { Apply(revision, () => polling = false); }
                });
            };
            _ScreenTimer?.Dispose(); _ScreenTimer = new Timer(state => Apply(revision, () => _Refresh?.Invoke()), null, 5000, 5000); _Refresh();
        }
        private string FormatMeasurement(double? value) { return value?.ToString("N2", _Localizer.Culture) ?? "Unavailable"; }
    }
}
