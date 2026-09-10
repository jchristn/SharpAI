namespace Test.Server
{
    using System;
    using System.Net.Http;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Live HTTP contract suite for a server running with <c>Telemetry.Enable = true</c>. Verifies that the
    /// Watson-native Prometheus endpoint (<c>/metrics</c>) is served, emits valid Prometheus exposition
    /// (0.0.4) with the expected HTTP server series, and that request traffic is actually recorded (the
    /// per-route request counter increases after additional requests). Run with the <c>--telemetry</c> flag
    /// against a server whose <c>Telemetry.Enable</c> is true. No model is required.
    /// </summary>
    public static class ServerTelemetryContractSuite
    {
        /// <summary>
        /// Build the telemetry contract suite bound to a base URL.
        /// </summary>
        /// <param name="client">HTTP client.</param>
        /// <param name="baseUrl">Server base URL.</param>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build(HttpClient client, string baseUrl)
        {
            string root = baseUrl.TrimEnd('/');

            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor("ServerTelemetry", "Metrics_Served", "GET /metrics serves Prometheus exposition",
                    async ct =>
                    {
                        HttpResponseMessage r = await client.GetAsync(root + "/metrics", ct).ConfigureAwait(false);
                        Assert.Status(r, 200);

                        string contentType = r.Content.Headers.ContentType != null ? r.Content.Headers.ContentType.ToString() : "";
                        Assert.Contains(contentType, "text/plain");

                        string body = await r.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                        Assert.Contains(body, "# TYPE");
                    }),

                new TestCaseDescriptor("ServerTelemetry", "Http_Series_Present", "Watson HTTP server series are exported",
                    async ct =>
                    {
                        // Drive some traffic so the series exist.
                        for (int i = 0; i < 3; i++)
                        {
                            HttpResponseMessage h = await client.GetAsync(root + "/health", ct).ConfigureAwait(false);
                            h.Dispose();
                        }

                        string body = await Scrape(client, root, ct).ConfigureAwait(false);
                        Assert.Contains(body, "http_server_request_duration_seconds");
                        Assert.Contains(body, "watson_route_matches_total");
                    }),

                new TestCaseDescriptor("ServerTelemetry", "Counter_Increments", "The per-route request counter increases with traffic",
                    async ct =>
                    {
                        // Warm the /health route so the counter series exists, then take a baseline.
                        HttpResponseMessage warm = await client.GetAsync(root + "/health", ct).ConfigureAwait(false);
                        warm.Dispose();

                        double before = ReadHealthCount(await Scrape(client, root, ct).ConfigureAwait(false));
                        Assert.True(before >= 1.0, "baseline /health request count is recorded");

                        int extra = 5;
                        for (int i = 0; i < extra; i++)
                        {
                            HttpResponseMessage h = await client.GetAsync(root + "/health", ct).ConfigureAwait(false);
                            h.Dispose();
                        }

                        double after = ReadHealthCount(await Scrape(client, root, ct).ConfigureAwait(false));
                        Assert.True(after >= before + extra,
                            "request counter grew by at least " + extra + " (before=" + before + ", after=" + after + ")");
                    })
            };

            return new TestSuiteDescriptor("ServerTelemetry", "Live server telemetry/Prometheus contracts", cases, null, null);
        }

        private static async Task<string> Scrape(HttpClient client, string root, CancellationToken ct)
        {
            HttpResponseMessage r = await client.GetAsync(root + "/metrics", ct).ConfigureAwait(false);
            Assert.Status(r, 200);
            return await r.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }

        // Sum the /health samples of http_server_request_duration_seconds_count across status/method labels.
        // Prometheus histograms expose one _count series per label set; summing is robust to label ordering.
        private static double ReadHealthCount(string metrics)
        {
            double total = 0.0;
            string[] lines = metrics.Split('\n');
            foreach (string line in lines)
            {
                if (!line.StartsWith("http_server_request_duration_seconds_count", StringComparison.Ordinal)) continue;
                if (line.IndexOf("http_route=\"/health\"", StringComparison.Ordinal) < 0) continue;

                int space = line.LastIndexOf(' ');
                if (space < 0 || space + 1 >= line.Length) continue;

                double value;
                if (Double.TryParse(line.Substring(space + 1).Trim(),
                    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
                {
                    total += value;
                }
            }
            return total;
        }
    }
}
