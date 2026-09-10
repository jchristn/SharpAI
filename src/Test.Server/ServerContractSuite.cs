namespace Test.Server
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>
    /// Live HTTP contract suite for a running SharpAI server. These assert the API shapes and error
    /// envelopes over the wire (no model required), formalizing the manual live smoke into a repeatable,
    /// CI-runnable check. The server must already be running at the supplied base URL, with authentication
    /// and telemetry disabled (the suite asserts <c>/metrics</c> → 404, the telemetry-disabled no-op).
    /// </summary>
    public static class ServerContractSuite
    {
        /// <summary>
        /// Build the server contract suite bound to a base URL.
        /// </summary>
        /// <param name="client">HTTP client.</param>
        /// <param name="baseUrl">Server base URL (for example http://127.0.0.1:8000).</param>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build(HttpClient client, string baseUrl)
        {
            string root = baseUrl.TrimEnd('/');

            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor("Server", "Health", "GET /health is a healthy liveness probe",
                    async ct =>
                    {
                        HttpResponseMessage r = await client.GetAsync(root + "/health", ct).ConfigureAwait(false);
                        Assert.Status(r, 200);
                        Assert.Contains(await Body(r), "healthy");
                    }),

                new TestCaseDescriptor("Server", "Ready", "GET /ready reports readiness with per-check detail",
                    async ct =>
                    {
                        HttpResponseMessage r = await client.GetAsync(root + "/ready", ct).ConfigureAwait(false);
                        Assert.True((int)r.StatusCode == 200 || (int)r.StatusCode == 503, "ready returns 200 or 503");
                        string body = await Body(r);
                        Assert.Contains(body, "database_initialized");
                        Assert.Contains(body, "telemetry_ready");
                    }),

                new TestCaseDescriptor("Server", "Version", "GET /api/version returns the server version",
                    async ct =>
                    {
                        HttpResponseMessage r = await client.GetAsync(root + "/api/version", ct).ConfigureAwait(false);
                        Assert.Status(r, 200);
                        Assert.Contains(await Body(r), "version");
                    }),

                new TestCaseDescriptor("Server", "OpenAI_Models_Shape", "GET /v1/models returns the OpenAI list shape",
                    async ct =>
                    {
                        HttpResponseMessage r = await client.GetAsync(root + "/v1/models", ct).ConfigureAwait(false);
                        Assert.Status(r, 200);
                        string body = await Body(r);
                        Assert.Contains(body, "\"object\":\"list\"");
                        Assert.Contains(body, "\"data\"");
                    }),

                new TestCaseDescriptor("Server", "Ollama_Tags_Shape", "GET /api/tags returns the Ollama models shape",
                    async ct =>
                    {
                        HttpResponseMessage r = await client.GetAsync(root + "/api/tags", ct).ConfigureAwait(false);
                        Assert.Status(r, 200);
                        Assert.Contains(await Body(r), "\"models\"");
                    }),

                new TestCaseDescriptor("Server", "OpenApi_Document", "GET /openapi.json serves the OpenAPI document",
                    async ct =>
                    {
                        HttpResponseMessage r = await client.GetAsync(root + "/openapi.json", ct).ConfigureAwait(false);
                        Assert.Status(r, 200);
                        Assert.Contains(await Body(r), "\"openapi\"");
                    }),

                new TestCaseDescriptor("Server", "Metrics_Disabled", "GET /metrics is 404 when telemetry is disabled (clean no-op)",
                    async ct =>
                    {
                        HttpResponseMessage r = await client.GetAsync(root + "/metrics", ct).ConfigureAwait(false);
                        Assert.Status(r, 404);
                    }),

                new TestCaseDescriptor("Server", "RequestHistory_Envelope", "GET /v1.0/api/request-history returns an enumeration envelope",
                    async ct =>
                    {
                        HttpResponseMessage r = await client.GetAsync(root + "/v1.0/api/request-history?pageSize=1", ct).ConfigureAwait(false);
                        Assert.Status(r, 200);
                        string body = await Body(r);
                        Assert.True(body.Contains("Objects") || body.Contains("objects"), "response has an Objects collection");
                    }),

                new TestCaseDescriptor("Server", "OpenAI_Error_Envelope", "A missing model yields the OpenAI error envelope (W4.T7)",
                    async ct =>
                    {
                        StringContent payload = new StringContent(
                            "{\"model\":\"does-not-exist-xyz\",\"messages\":[{\"role\":\"user\",\"content\":\"hi\"}]}",
                            System.Text.Encoding.UTF8, "application/json");
                        HttpResponseMessage r = await client.PostAsync(root + "/v1/chat/completions", payload, ct).ConfigureAwait(false);
                        Assert.True((int)r.StatusCode >= 400, "missing model is an error status");
                        string body = await Body(r);
                        Assert.Contains(body, "\"error\"");
                        Assert.Contains(body, "\"type\"");
                    }),

                new TestCaseDescriptor("Server", "Ollama_Error_Shape", "A missing model yields the Ollama error shape",
                    async ct =>
                    {
                        StringContent payload = new StringContent(
                            "{\"model\":\"does-not-exist-xyz\",\"prompt\":\"hi\"}",
                            System.Text.Encoding.UTF8, "application/json");
                        HttpResponseMessage r = await client.PostAsync(root + "/api/generate", payload, ct).ConfigureAwait(false);
                        Assert.True((int)r.StatusCode >= 400, "missing model is an error status");
                        Assert.Contains(await Body(r), "\"error\"");
                    })
            };

            return new TestSuiteDescriptor("Server", "Live server HTTP contracts", cases, null, null);
        }

        private static async Task<string> Body(HttpResponseMessage response)
        {
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
    }
}
