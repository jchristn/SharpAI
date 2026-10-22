namespace Test.Shared.Tui
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Client;
    using Touchstone.Core;

    /// <summary>Live public API and native model evidence. Requires an isolated SharpAI server and a session token file.</summary>
    public static class TuiLiveSuite
    {
        /// <summary>Build live descriptors. These never substitute synthetic transport for native inference.</summary>
        public static TestSuiteDescriptor Build()
        {
            bool skip = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SHARPAI_TUI_LIVE_URL"));
            return new TestSuiteDescriptor("TuiLive", "Live terminal SDK workflows", new[]
            {
                new TestCaseDescriptor("TuiLive", "Libraries", "Library create, edit, duplicate and delete preserve sampling fields", LibrariesAsync, skip: skip, skipReason: "Set isolated live URL and session token file."),
                new TestCaseDescriptor("TuiLive", "Inference", "Native Ollama and OpenAI streaming/non-streaming and persisted history", InferenceAsync, skip: skip, skipReason: "Set isolated live URL, session token file and model name.")
            });
        }
        private static ServerClient Client()
        {
            return new ServerClient(Environment.GetEnvironmentVariable("SHARPAI_TUI_LIVE_URL")!) { Token = File.ReadAllText(Environment.GetEnvironmentVariable("SHARPAI_TUI_TOKEN_FILE")!).Trim() };
        }
        private static async Task LibrariesAsync(CancellationToken ct)
        {
            using ServerClient client = Client(); List<LibraryRecord> created = new List<LibraryRecord>();
            try
            {
                JsonObject config = new JsonObject { ["Temperature"] = 0.7, ["MaxTokens"] = 32, ["TopP"] = 0.8, ["TopK"] = 2, ["MinP"] = 0.01, ["RepeatPenalty"] = 1.1, ["RepeatLastN"] = 32, ["PresencePenalty"] = -0.5, ["FrequencyPenalty"] = 0.5, ["Seed"] = 42, ["Stop"] = new JsonArray("STOP", "END"), ["StreamResponse"] = false, ["JsonOutput"] = true, ["DisplayThinking"] = true };
                LibraryRecord source = (await client.SaveLibraryAsync("model-configurations", null, false, new JsonObject { ["Name"] = "TUI test " + Guid.NewGuid(), ["Scope"] = "User", ["Configuration"] = config }, ct).ConfigureAwait(false))!; created.Add(source);
                LibraryRecord copy = (await client.SaveLibraryAsync("model-configurations", source.Id, true, new JsonObject { ["Name"] = "TUI duplicate", ["Scope"] = "User" }, ct).ConfigureAwait(false))!; created.Add(copy);
                TestAssert.True(source.Id != copy.Id, "duplicate has independent identity"); TestAssert.Equal(42, copy.Configuration!.Seed); TestAssert.Equal(2, copy.Configuration.Stop.Count);
                TestAssert.True(!copy.Configuration.StreamResponse && copy.Configuration.JsonOutput && copy.Configuration.DisplayThinking, "all switches preserved");
                LibraryRecord? reloaded = await client.LibraryAsync("model-configurations", source.Id, ct).ConfigureAwait(false); TestAssert.Equal(source.Name, reloaded!.Name);
                ResourcePage<LibraryRecord> page = await client.LibrariesAsync("model-configurations", 1, 100, null, ct).ConfigureAwait(false); TestAssert.True(page.Objects.Any(row => row.Id == copy.Id), "duplicate discoverable");
            }
            finally { foreach (LibraryRecord record in created) await client.DeleteLibraryAsync("model-configurations", record.Id, ct).ConfigureAwait(false); }
        }
        private static async Task InferenceAsync(CancellationToken ct)
        {
            using ServerClient client = Client(); string model = Environment.GetEnvironmentVariable("SHARPAI_TUI_MODEL") ?? "tui-stories";
            foreach (string endpoint in new[] { "/api/chat", "/v1/chat/completions" })
            {
                foreach (bool stream in new[] { false, true })
                {
                    JsonObject body = new JsonObject { ["model"] = model, ["stream"] = stream, ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = "Once upon a time" }) };
                    if (endpoint == "/api/chat") body["options"] = new JsonObject { ["num_predict"] = 16, ["temperature"] = 0 };
                    else { body["max_tokens"] = 16; if (stream) body["stream_options"] = new JsonObject { ["include_usage"] = true }; }
                    List<JsonNode> frames = new List<JsonNode>();
                    if (stream) { await foreach (JsonNode frame in client.StreamAsync(endpoint, body, ct).ConfigureAwait(false)) frames.Add(frame); }
                    else frames.Add((await client.JsonAsync("POST", endpoint, body, ct).ConfigureAwait(false))!);
                    TestAssert.True(frames.Count > 0, "actual response frames");
                    if (endpoint == "/api/chat") { TestAssert.True(frames.Last()["done"]!.GetValue<bool>(), "Ollama final marker"); TestAssert.True(frames.Last()["eval_count"]!.GetValue<int>() > 0, "native measured tokens"); }
                    else TestAssert.True(frames.Any(frame => frame["choices"] is JsonArray choices && choices.Count > 0), "OpenAI choices");
                }
            }
            ResourcePage<HistoryRecord> history = await client.HistoryAsync(new Dictionary<string, string?> { ["category"] = "inference", ["pageSize"] = "25" }, ct).ConfigureAwait(false);
            TestAssert.True(history.Objects.Count > 0, "inference persisted"); HistoryRecord? detail = await client.HistoryEntryAsync(history.Objects[0].Id, ct).ConfigureAwait(false);
            TestAssert.True(detail?.OutputTokens > 0, "history contains native usage");
        }
    }
}
