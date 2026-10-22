namespace Test.Xunit
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Xunit;

    /// <summary>Live response-contract assertions for usage, terminal chunks, context errors, and diagnostics.</summary>
    public sealed class ServerOperationsTests
    {
        private static string Root { get { return Environment.GetEnvironmentVariable("SHARPAI_TEST_SERVER_URL")!; } }
        private static object Payload(string endpoint, bool stream, bool includeUsage = false, int maxTokens = 8)
        {
            if (endpoint == "/v1/completions") return new { model = "ops-story", prompt = "Once upon a time", stream, max_tokens = maxTokens, temperature = 0, stream_options = new { include_usage = includeUsage } };
            if (endpoint == "/v1/chat/completions") return new { model = "ops-story", messages = new[] { new { role = "user", content = "Once upon a time" } }, stream, max_tokens = maxTokens, temperature = 0, stream_options = new { include_usage = includeUsage } };
            if (endpoint == "/api/chat") return new { model = "ops-story", messages = new[] { new { role = "user", content = "Once upon a time" } }, stream, options = new { num_predict = maxTokens, temperature = 0 } };
            return new { model = "ops-story", prompt = "Once upon a time", stream, options = new { num_predict = maxTokens, temperature = 0 } };
        }
        private static void Usage(JsonElement value, int completion)
        {
            Assert.Equal(completion, value.GetProperty("completion_tokens").GetInt32());
            int prompt = value.GetProperty("prompt_tokens").GetInt32(); Assert.True(prompt > 0);
            Assert.Equal(prompt + completion, value.GetProperty("total_tokens").GetInt32());
        }

        [ServerTheory]
        [InlineData("/v1/completions")] [InlineData("/v1/chat/completions")]
        public async Task OpenAINonStreamingUsage(string endpoint)
        {
            using HttpClient client = new HttpClient(); using HttpResponseMessage response = await client.PostAsJsonAsync(Root + endpoint, Payload(endpoint, false));
            Assert.Equal(200, (int)response.StatusCode); using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Usage(document.RootElement.GetProperty("usage"), 8);
            Assert.Equal("length", document.RootElement.GetProperty("choices")[0].GetProperty("finish_reason").GetString());
        }

        [ServerTheory]
        [InlineData("/v1/completions", true)] [InlineData("/v1/completions", false)]
        [InlineData("/v1/chat/completions", true)] [InlineData("/v1/chat/completions", false)]
        public async Task OpenAIStreamingHasTerminalChoiceAndOptionalUsageBeforeDone(string endpoint, bool includeUsage)
        {
            using HttpClient client = new HttpClient(); using HttpResponseMessage response = await client.PostAsJsonAsync(Root + endpoint, Payload(endpoint, true, includeUsage));
            Assert.Equal(200, (int)response.StatusCode);
            string[] events = (await response.Content.ReadAsStringAsync()).Split('\n').Select(line => line.Trim()).Where(line => line.StartsWith("data:")).Select(line => line.Substring(5).Trim()).ToArray();
            Assert.Equal("[DONE]", events[^1]); int usageChunks = 0; bool terminal = false;
            for (int i = 0; i < events.Length - 1; i++)
            {
                using JsonDocument document = JsonDocument.Parse(events[i]); JsonElement root = document.RootElement;
                if (root.TryGetProperty("usage", out JsonElement usage) && usage.ValueKind == JsonValueKind.Object)
                {
                    Usage(usage, 8); Assert.Equal(0, root.GetProperty("choices").GetArrayLength()); Assert.Equal(events.Length - 2, i); usageChunks++;
                }
                else if (root.GetProperty("choices")[0].TryGetProperty("finish_reason", out JsonElement reason) && reason.ValueKind == JsonValueKind.String) terminal = true;
            }
            Assert.True(terminal); Assert.Equal(includeUsage ? 1 : 0, usageChunks);
        }

        [ServerTheory]
        [InlineData("/api/generate", false)] [InlineData("/api/generate", true)]
        [InlineData("/api/chat", false)] [InlineData("/api/chat", true)]
        public async Task OllamaTerminalCarriesCountsAndNanosecondTimings(string endpoint, bool stream)
        {
            using HttpClient client = new HttpClient(); using HttpResponseMessage response = await client.PostAsJsonAsync(Root + endpoint, Payload(endpoint, stream));
            Assert.Equal(200, (int)response.StatusCode);
            string[] lines = (await response.Content.ReadAsStringAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            using JsonDocument document = JsonDocument.Parse(lines[^1]); JsonElement terminal = document.RootElement;
            Assert.True(terminal.GetProperty("done").GetBoolean()); Assert.Equal(8, terminal.GetProperty("eval_count").GetInt32());
            Assert.True(terminal.GetProperty("prompt_eval_count").GetInt32() > 0);
            Assert.True(terminal.GetProperty("total_duration").GetInt64() > 0); Assert.True(terminal.GetProperty("prompt_eval_duration").GetInt64() > 0);
            Assert.True(terminal.GetProperty("eval_duration").GetInt64() > 0); Assert.True(terminal.GetProperty("load_duration").GetInt64() >= 0);
            if (endpoint == "/api/chat") Assert.Equal("assistant", terminal.GetProperty("message").GetProperty("role").GetString());
            Assert.Equal("length", terminal.GetProperty("done_reason").GetString());
        }

        [ServerTheory]
        [InlineData("/v1/completions")] [InlineData("/v1/chat/completions")]
        [InlineData("/api/generate")] [InlineData("/api/chat")]
        public async Task OverflowIs400BeforeStreamStarts(string endpoint)
        {
            using HttpClient client = new HttpClient(); using HttpResponseMessage response = await client.PostAsJsonAsync(Root + endpoint, Payload(endpoint, true, maxTokens: 100000));
            Assert.Equal(400, (int)response.StatusCode); string body = await response.Content.ReadAsStringAsync();
            Assert.Contains("prompt=", body); Assert.Contains("requested_output=", body); Assert.Contains("limit=", body); Assert.DoesNotContain("data:", body);
            Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        }

        [ServerTheory]
        [InlineData(false)] [InlineData(true)]
        public async Task PromptArraysAggregateUsageAndPreserveIndices(bool stream)
        {
            using HttpClient client = new HttpClient(); using HttpResponseMessage response = await client.PostAsJsonAsync(Root + "/v1/completions", new
            {
                model = "ops-story", prompt = new[] { "Once upon a time", "One day" }, max_tokens = 3, temperature = 0,
                stream, stream_options = new { include_usage = true }
            });
            Assert.Equal(200, (int)response.StatusCode); string body = await response.Content.ReadAsStringAsync();
            if (!stream)
            {
                using JsonDocument document = JsonDocument.Parse(body); Usage(document.RootElement.GetProperty("usage"), 6);
                Assert.Equal(2, document.RootElement.GetProperty("choices").GetArrayLength());
            }
            else
            {
                string[] events = body.Split('\n').Select(line => line.Trim()).Where(line => line.StartsWith("data:") && !line.Contains("[DONE]")).Select(line => line.Substring(5).Trim()).ToArray();
                using JsonDocument usage = JsonDocument.Parse(events[^1]); Usage(usage.RootElement.GetProperty("usage"), 6);
                int[] indices = events.Take(events.Length - 1).Select(text => { using JsonDocument doc = JsonDocument.Parse(text); return doc.RootElement.GetProperty("choices")[0].GetProperty("index").GetInt32(); }).ToArray();
                Assert.Contains(0, indices); Assert.Contains(1, indices); Assert.Equal(indices.OrderBy(i => i), indices);
            }
        }

        [ServerTheory]
        [InlineData("/api/diagnostics/runtime")] [InlineData("/api/diagnostics/memory")]
        public async Task DiagnosticsAreExplicitAboutObservationLimits(string endpoint)
        {
            using HttpClient client = new HttpClient(); using HttpResponseMessage response = await client.GetAsync(Root + endpoint);
            Assert.Equal(200, (int)response.StatusCode); using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (endpoint.EndsWith("runtime")) { Assert.Equal(Environment.GetEnvironmentVariable("SHARPAI_TEST_EXPECTED_BACKEND") ?? "cpu", document.RootElement.GetProperty("backend_selected").GetString()); Assert.False(document.RootElement.GetProperty("gpu_offload_verified").GetBoolean()); Assert.Equal(SharpAI.Classes.Runtime.NativeBackendInfo.LlamaSharpVersion, document.RootElement.GetProperty("llamasharp_version").GetString()); }
            else { Assert.True(document.RootElement.GetProperty("working_set_bytes").GetInt64() > 0); Assert.False(document.RootElement.GetProperty("per_model_observed_available").GetBoolean()); }
        }

        [ServerTheory]
        [InlineData("bad-id", 400)] [InlineData("00000000-0000-0000-0000-000000000000", 404)]
        public async Task InspectionRejectsInvalidOrMissingModels(string id, int status)
        {
            using HttpClient client = new HttpClient(); using HttpResponseMessage response = await client.GetAsync(Root + "/api/models/" + id + "/inspect"); Assert.Equal(status, (int)response.StatusCode);
        }
        [ServerTheory]
        [InlineData("ops-story", 200)] [InlineData("missing-model", 404)] [InlineData("", 400)]
        public async Task ModelNameInspectionDoesNotLoadWeights(string model, int status)
        {
            using HttpClient client = new HttpClient(); using HttpResponseMessage response = await client.PostAsJsonAsync(Root + "/api/inspect", new { model });
            Assert.Equal(status, (int)response.StatusCode);
            if (status == 200)
            {
                using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.Equal(model, document.RootElement.GetProperty("model").GetString());
                Assert.Equal(64, document.RootElement.GetProperty("sha256").GetString()!.Length);
                Assert.True(document.RootElement.GetProperty("inspection").GetProperty("ParameterCount").GetInt64() > 0);
            }
        }

        [ServerTheory]
        [InlineData(false)] [InlineData(true)]
        public async Task SplitImportsAreCompleteAndInvalidSetsAreNotRegistered(bool incomplete)
        {
            string directory = Path.Combine(Path.GetTempPath(), "sharpai-split-import-" + Guid.NewGuid()); Directory.CreateDirectory(directory);
            string first = Path.Combine(directory, "model-00001-of-00002.GGUF");
            string second = Path.Combine(directory, "model-00002-of-00002.GGUF");
            string name = "split-import-" + Guid.NewGuid();
            File.WriteAllBytes(first, GgufFixture.Create()); if (!incomplete) File.WriteAllBytes(second, GgufFixture.Create());
            using HttpClient client = new HttpClient();
            try
            {
                using HttpResponseMessage response = await client.PostAsJsonAsync(Root + "/api/import", new { path = first, name });
                Assert.Equal(incomplete ? 400 : 200, (int)response.StatusCode);
                using HttpResponseMessage inspection = await client.PostAsJsonAsync(Root + "/api/inspect", new { model = name });
                Assert.Equal(incomplete ? 404 : 200, (int)inspection.StatusCode);
                if (!incomplete)
                {
                    using JsonDocument document = JsonDocument.Parse(await inspection.Content.ReadAsStringAsync());
                    Assert.Equal(2, document.RootElement.GetProperty("inspection").GetProperty("Files").GetArrayLength());
                }
            }
            finally
            {
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, Root + "/api/delete") { Content = JsonContent.Create(new { model = name }) };
                using HttpResponseMessage deleted = await client.SendAsync(request);
                Directory.Delete(directory, true);
            }
        }

        [ServerTheory]
        [InlineData("/v1/completions")] [InlineData("/v1/chat/completions")]
        [InlineData("/api/generate")] [InlineData("/api/chat")]
        public async Task ClientDisconnectDoesNotStrandGenerationCapacity(string endpoint)
        {
            using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            using (HttpRequestMessage? request = new HttpRequestMessage(HttpMethod.Post, Root + endpoint) { Content = JsonContent.Create(Payload(endpoint, true, maxTokens: 128)) })
            using (HttpResponseMessage? response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                using Stream? stream = await response.Content.ReadAsStreamAsync();
                using StreamReader? reader = new StreamReader(stream);
                Assert.NotNull(await reader.ReadLineAsync());
                // Dispose a live response before its terminal event, closing the client connection.
            }
            using HttpResponseMessage? followup = await client.PostAsJsonAsync(Root + "/v1/completions", Payload("/v1/completions", false, maxTokens: 2));
            Assert.Equal(200, (int)followup.StatusCode);
            using JsonDocument? document = JsonDocument.Parse(await followup.Content.ReadAsStringAsync());
            Usage(document.RootElement.GetProperty("usage"), 2);
        }

    }
}
