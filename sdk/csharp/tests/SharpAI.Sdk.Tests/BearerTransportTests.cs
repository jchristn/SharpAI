namespace SharpAI.Sdk.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    using SharpAI.Sdk;

    using Xunit;

    /// <summary>SDK bearer credentials reach the HTTP transport and sensitive response logs stay redacted.</summary>
    public sealed class SdkBearerTransportTests
    {
        /// <summary>Exercise both typed administration and raw-response methods over an actual HTTP listener.</summary>
        [Fact]
        public async Task BearerHeaderIsSentAndCredentialResponsesAreNotLogged()
        {
            TcpListener portProbe = new TcpListener(IPAddress.Loopback, 0);
            portProbe.Start();
            int port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
            portProbe.Stop();
            string endpoint = "http://127.0.0.1:" + port;
            using HttpListener listener = new HttpListener();
            listener.Prefixes.Add(endpoint + "/"); listener.Start();
            List<string?> headers = new List<string?>();
            string returnedToken = "sharpai_sdk_fixture_secret";
            Task server = Task.Run(async () =>
            {
                for (int index = 0; index < 2; index++)
                {
                    HttpListenerContext context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(10));
                    headers.Add(context.Request.Headers["Authorization"]);
                    byte[] response = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { token = returnedToken, tokenType = "Bearer" }));
                    context.Response.ContentType = "application/json"; context.Response.ContentLength64 = response.Length;
                    await context.Response.OutputStream.WriteAsync(response);
                    context.Response.Close();
                }
            });
            List<string> messages = new List<string>();
            using SharpAISdk sdk = new SharpAISdk(endpoint, token: "admin-fixture-token");
            sdk.TimeoutMs = 5000; sdk.LogResponses = true; sdk.Logger = (level, message) => messages.Add(message);
            JsonElement? result = await sdk.Admin.RevealCredentialTokenAsync("tenant-a", "credential-a");
            Assert.True(result.HasValue);
            Assert.Equal(returnedToken, result.Value.GetProperty("token").GetString());
            string? raw = await sdk.GetRawResponse(endpoint + "/v1.0/tenants/tenant-a/credentials/credential-a/token");
            Assert.Contains(returnedToken, raw);
            await server;
            Assert.Equal(2, headers.Count);
            Assert.All(headers, header => Assert.Equal("Bearer admin-fixture-token", header));
            Assert.DoesNotContain(messages, message => message.Contains(returnedToken, StringComparison.Ordinal));
            Assert.Contains(messages, message => message.Contains("sensitive response withheld", StringComparison.Ordinal));
        }
    }
}
