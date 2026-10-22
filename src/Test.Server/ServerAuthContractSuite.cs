namespace Test.Server
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;

    /// <summary>
    /// Live HTTP contracts for mandatory bearer authentication. Verifies challenges, infrastructure
    /// probes, rejection of legacy admin keys, interactive login and RBAC. Run with --auth against a
    /// disposable test server. SHARPAI_TEST_ADMIN_EMAIL/PASSWORD may override initial credentials.
    /// </summary>
    public static class ServerAuthContractSuite
    {
        /// <summary>A legacy key that the server must reject.</summary>
        public const string AdminKey = "test-admin-key";

        /// <summary>
        /// Build the auth contract suite bound to a base URL.
        /// </summary>
        /// <param name="client">HTTP client.</param>
        /// <param name="baseUrl">Server base URL.</param>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build(HttpClient client, string baseUrl)
        {
            string root = baseUrl.TrimEnd('/');

            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor("ServerAuth", "Challenge_Unauthenticated", "Protected routes challenge with 401",
                    async ct =>
                    {
                        HttpResponseMessage r = await Send(client, HttpMethod.Get, root + "/v1.0/tenants", null, null, ct);
                        Assert.Status(r, 401);
                        foreach (string path in new[] { "/api/diagnostics/runtime", "/api/diagnostics/memory" })
                            Assert.Status(await Send(client, HttpMethod.Get, root + path, null, null, ct), 401);
                    }),

                new TestCaseDescriptor("ServerAuth", "Anonymous_Open", "Health stays open and API documentation requires authentication",
                    async ct =>
                    {
                        HttpResponseMessage h = await Send(client, HttpMethod.Get, root + "/health", null, null, ct);
                        Assert.Status(h, 200);
                        HttpResponseMessage o = await Send(client, HttpMethod.Get, root + "/openapi.json", null, null, ct);
                        Assert.Status(o, 401);
                    }),

                new TestCaseDescriptor("ServerAuth", "Bearer_Allows", "A bearer session is authorized; legacy keys are rejected",
                    async ct =>
                    {
                        Dictionary<string, string> headers = await AdministratorHeaders(client, root, ct);
                        Assert.Status(await Send(client, HttpMethod.Get, root + "/v1.0/tenants", new Dictionary<string, string> { { "x-api-key", AdminKey } }, null, ct), 401);
                        HttpResponseMessage r = await Send(client, HttpMethod.Get, root + "/v1.0/tenants", headers, null, ct);
                        Assert.Status(r, 200);
                        foreach (string path in new[] { "/api/diagnostics/runtime", "/api/diagnostics/memory" })
                            Assert.Status(await Send(client, HttpMethod.Get, root + path, headers, null, ct), 200);
                        Assert.Contains(await r.Content.ReadAsStringAsync(ct), "default");
                    }),

                new TestCaseDescriptor("ServerAuth", "Login_And_Rbac", "Login yields a token; an unprivileged user is denied (403)",
                    async ct =>
                    {
                        Dictionary<string, string> adminHeader = await AdministratorHeaders(client, root, ct);

                        // Resolve the seeded "default" tenant.
                        HttpResponseMessage tenantsResp = await Send(client, HttpMethod.Get, root + "/v1.0/tenants", adminHeader, null, ct);
                        Assert.Status(tenantsResp, 200);
                        string tenantGuid = FindDefaultTenantGuid(await tenantsResp.Content.ReadAsStringAsync(ct));
                        Assert.True(!string.IsNullOrEmpty(tenantGuid), "default tenant resolved");

                        // Create a fresh, unprivileged user with a known password (admin-authenticated).
                        string email = "contract-" + Guid.NewGuid().ToString("N") + "@sharpai.local";
                        string password = "pw-" + Guid.NewGuid().ToString("N");
                        string createBody = "{\"Email\":\"" + email + "\",\"Password\":\"" + password + "\"}";
                        HttpResponseMessage createResp = await Send(client, HttpMethod.Post, root + "/v1.0/tenants/" + tenantGuid + "/users", adminHeader, createBody, ct);
                        Assert.Status(createResp, 200);

                        // Log in as that user via the header envelope.
                        Dictionary<string, string> loginHeaders = new Dictionary<string, string>
                        {
                            { "x-email", email },
                            { "x-password", password },
                            { "x-tenant-guid", tenantGuid }
                        };
                        HttpResponseMessage loginResp = await Send(client, HttpMethod.Post, root + "/v1.0/token", loginHeaders, null, ct);
                        Assert.Status(loginResp, 200);
                        string token = ReadStringProperty(await loginResp.Content.ReadAsStringAsync(ct), "token");
                        Assert.True(!string.IsNullOrEmpty(token), "login returned a bearer token");

                        Dictionary<string, string> bearer = new Dictionary<string, string> { { "Authorization", "Bearer " + token } };

                        // The token resolves the session.
                        HttpResponseMessage sessionResp = await Send(client, HttpMethod.Get, root + "/v1.0/token", bearer, null, ct);
                        Assert.Status(sessionResp, 200);

                        // The unprivileged user is authenticated but not authorized for a platform-admin route.
                        HttpResponseMessage deniedResp = await Send(client, HttpMethod.Get, root + "/v1.0/tenants", bearer, null, ct);
                        Assert.Status(deniedResp, 403);
                        foreach (string path in new[] { "/api/diagnostics/runtime", "/api/diagnostics/memory" })
                            Assert.Status(await Send(client, HttpMethod.Get, root + path, bearer, null, ct), 403);
                    })
            };

            return new TestSuiteDescriptor("ServerAuth", "Live server auth/RBAC contracts", cases, null, null);
        }

        private static async Task<Dictionary<string, string>> AdministratorHeaders(HttpClient client, string root, CancellationToken ct)
        {
            Dictionary<string, string> credentials = new Dictionary<string, string>
            {
                { "x-email", Environment.GetEnvironmentVariable("SHARPAI_TEST_ADMIN_EMAIL") ?? "admin@sharpai.local" },
                { "x-password", Environment.GetEnvironmentVariable("SHARPAI_TEST_ADMIN_PASSWORD") ?? "password" },
                { "x-tenant-guid", "default" }
            };
            HttpResponseMessage response = await Send(client, HttpMethod.Post, root + "/v1.0/token", credentials, null, ct);
            Assert.Status(response, 200);
            string token = ReadStringProperty(await response.Content.ReadAsStringAsync(ct), "token");
            return new Dictionary<string, string> { { "Authorization", "Bearer " + token } };
        }

        private static async Task<HttpResponseMessage> Send(
            HttpClient client, HttpMethod method, string url, Dictionary<string, string>? headers, string? body, CancellationToken ct)
        {
            using HttpRequestMessage request = new HttpRequestMessage(method, url);
            if (headers != null)
            {
                foreach (KeyValuePair<string, string> h in headers) request.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }
            if (body != null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            return await client.SendAsync(request, ct).ConfigureAwait(false);
        }

        private static string FindDefaultTenantGuid(string json)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Objects", out JsonElement objects)) return string.Empty;
            foreach (JsonElement tenant in objects.EnumerateArray())
            {
                if (tenant.TryGetProperty("Name", out JsonElement name) && name.GetString() == "default"
                    && tenant.TryGetProperty("Guid", out JsonElement guid))
                {
                    return guid.GetString() ?? string.Empty;
                }
            }
            return string.Empty;
        }

        private static string ReadStringProperty(string json, string property)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(property, out JsonElement value) ? value.GetString() ?? string.Empty : string.Empty;
        }
    }
}
