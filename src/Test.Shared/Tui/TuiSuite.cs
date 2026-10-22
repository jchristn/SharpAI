namespace Test.Shared.Tui
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Text;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Client;
    using SharpAI.Tui;
    using SharpAI.Tui.Localization;
    using SharpAI.Tui.Services;
    using SharpAI.Tui.Widgets;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Terminal;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>Shared terminal and transport contracts. Synthetic HTTP cases require SHARPAI_TUI_TEST_URL pointing to the checked Postman simulation.</summary>
    public static class TuiSuite
    {
        /// <summary>Build runner-independent descriptors. Does not open a native inference engine.</summary>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Case("Sanitize", ct => { TestAssert.Equal("hello\nworld", SafeText.Clean("\u001b]52;c;evil\ahello\u001b[31m\nworld\u0000")); return Task.CompletedTask; }),
                Case("Profile", ct =>
                {
                    string directory = Path.Combine(Path.GetTempPath(), "sharpai-tui-" + Guid.NewGuid().ToString("N"));
                    try { ProfileStore store = new ProfileStore(directory); ConnectionProfile profile = store.Load(); profile.Server = "https://example.test:8000"; store.Save(profile); TestAssert.Equal(profile.Server, store.Load().Server); TestAssert.True(JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "tui.json")))!["Token"] == null, "bearer value never stored"); }
                    finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
                    return Task.CompletedTask;
                }),
                Case("CjkPageFilter", ct =>
                {
                    using ServerClient client = new ServerClient("http://127.0.0.1:1"); using HeadlessBackend backend = new HeadlessBackend(120, 40) { ClaimsTerminal = false };
                    using TestProfileStore profileStore = new TestProfileStore(); using SharpAITuiApp app = new SharpAITuiApp(client, backend, new ConnectionProfile(), profileStore.Store);
                    DataTable<JsonNode> table = new DataTable<JsonNode>().Column("Name", row => JsonData.Text(row, "Name"));
                    table.Bind(new JsonNode[] { new JsonObject { ["Name"] = "你好世界" }, new JsonObject { ["Name"] = "Other" } });
                    TableViewport viewport = new TableViewport(table, new Localizer("zh"), () => { });
                    app.Host.Start(); app.Host.Bind("body", viewport); app.Host.Focus("body"); app.Host.RenderOnce();
                    viewport.FocusEdge(false); backend.FeedInput("你好"); app.Host.PumpInputOnce(); app.Host.RenderOnce();
                    TestAssert.Equal(1, table.Rows.Count); TestAssert.Equal("你好世界", JsonData.Text(table.Rows[0], "Name")); return Task.CompletedTask;
                }),
                Case("Locales", ct =>
                {
                    foreach (string language in Localizer.Languages) { Localizer localizer = new Localizer(language); TestAssert.True(localizer.Text("nav.models") != "nav.models", "embedded web locale resolves " + language); }
                    return Task.CompletedTask;
                }),
                Case("Generation", ct =>
                {
                    JsonObject configuration = new JsonObject { ["Temperature"] = 0.7, ["Seed"] = 42, ["Stop"] = new JsonArray("END"), ["DisplayThinking"] = true, ["TopP"] = null };
                    JsonObject options = SharpAITuiApp.GenerationOptions(configuration);
                    TestAssert.Equal("0.7", options["temperature"]!.ToString()); TestAssert.Equal("42", options["seed"]!.ToString()); TestAssert.True(options["top_p"] == null, "null retains engine default"); TestAssert.True(options["stop"]!.Parent == options, "stop is cloned");
                    return Task.CompletedTask;
                }),
                Case("TypedRoundTrip", ct =>
                {
                    JsonObject input = new JsonObject { ["Unknown"] = new JsonObject { ["Enabled"] = true, ["Count"] = 42 }, ["Nullable"] = null, ["Array"] = new JsonArray("a", "b") };
                    JsonForm form = new JsonForm(input, result => { }, () => { });
                    TestAssert.True(JsonNode.DeepEquals(input, form.Draft), "unknown fields, arrays, booleans and null survive"); TestAssert.True(!form.IsDirty, "initial form clean");
                    TextField count = (TextField)form.Form.GetField(1); count.Value = "43"; TestAssert.Equal(43, form.Draft["Unknown"]!["Count"]!.GetValue<int>()); form.MarkClean(); count.Value = "42";
                    TestAssert.Equal(42, form.Draft["Unknown"]!["Count"]!.GetValue<int>()); TestAssert.True(form.IsDirty, "reverting a saved numeric value remains an edit");
                    count.Value = "042"; TestAssert.True(form.Form.ValidateAll().Count > 0, "invalid JSON numbers fail before submission"); return Task.CompletedTask;
                }),
                Case("Catalog", ct =>
                {
                    Assembly assembly = typeof(SharpAITuiApp).Assembly;
                    using Stream catalog = assembly.GetManifestResourceStream("SharpAI.Tui.operations.json")!;
                    using Stream parity = assembly.GetManifestResourceStream("SharpAI.Tui.parity.json")!;
                    JsonArray operations = (JsonArray)JsonNode.Parse(catalog)!; JsonArray mapped = (JsonArray)JsonNode.Parse(parity)!["operations"]!;
                    TestAssert.Equal(operations.Count, mapped.Count);
                    foreach (JsonNode? operation in operations) TestAssert.True(mapped.Any(item => item!["method"]!.ToString() == operation!["method"]!.ToString() && item["path"]!.ToString() == operation["path"]!.ToString()), "every operation reachable");
                    return Task.CompletedTask;
                }),
                Case("ShellFrames", ShellFramesAsync),
                Case("RestoreAfterFailure", ct =>
                {
                    using FailingTerminal backend = new FailingTerminal(); using ServerClient client = new ServerClient("http://127.0.0.1:1");
                    try { using TestProfileStore profileStore = new TestProfileStore(); using SharpAITuiApp app = new SharpAITuiApp(client, backend, new ConnectionProfile(), profileStore.Store); app.Host.Start(); app.Host.RenderOnce(); }
                    catch (IOException) { }
                    TestAssert.True(backend.Restored, "terminal restored after an injected write failure"); return Task.CompletedTask;
                }),
                Case("SecretDismissal", ct =>
                {
                    using HeadlessBackend backend = new HeadlessBackend(120, 40) { ClaimsTerminal = false }; using ServerClient client = new ServerClient("http://127.0.0.1:1");
                    using TestProfileStore profileStore = new TestProfileStore(); using SharpAITuiApp app = new SharpAITuiApp(client, backend, new ConnectionProfile(), profileStore.Store); app.Host.Start();
                    SecretModal secret = new SecretModal("fixture_secret_never_visible", value => { }); Task<object?> completion = app.Host.ShowAsync(secret); app.Host.RenderOnce();
                    TestAssert.DoesNotContain(Frame(app), "fixture_secret_never_visible"); backend.FeedKey("Escape"); app.Host.PumpInputOnce(); app.Host.PumpInputOnce();
                    TestAssert.True(completion.IsCompleted && !secret.HasValue, "dismiss clears the retained token field"); return Task.CompletedTask;
                }),
                HttpCase("ClipboardFallback", ClipboardFallbackAsync),
                HttpCase("ClipboardRace", ClipboardRaceAsync),
                HttpCase("Streams", StreamsAsync),
                HttpCase("Errors", ErrorsAsync),
                HttpCase("SessionRace", SessionRaceAsync),
                HttpCase("Bounded", BoundedAsync),
                HttpCase("NoMutationRetry", NoMutationRetryAsync),
                HttpCase("Cancellation", CancellationAsync),
                HttpCase("Routes", RoutesAsync)
            };
            return new TestSuiteDescriptor("Tui", "Terminal dashboard", cases);
        }
        private static TestCaseDescriptor Case(string id, Func<CancellationToken, Task> run) { return new TestCaseDescriptor("Tui", id, id, run); }
        private static TestCaseDescriptor HttpCase(string id, Func<CancellationToken, Task> run)
        {
            return new TestCaseDescriptor("Tui", id, id, run, skip: string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SHARPAI_TUI_TEST_URL")), skipReason: "Start postman/simulations/tui.sim.yaml and set SHARPAI_TUI_TEST_URL.");
        }
        private static ServerClient Client() { return new ServerClient(Environment.GetEnvironmentVariable("SHARPAI_TUI_TEST_URL")!); }
        private static async Task ShellFramesAsync(CancellationToken ct)
        {
            foreach (int width in new[] { 80, 100, 120, 160 })
            {
                using HeadlessBackend backend = new HeadlessBackend(width, 40) { ClaimsTerminal = false };
                using ServerClient client = new ServerClient("http://127.0.0.1:1");
                using TestProfileStore profileStore = new TestProfileStore(); using SharpAITuiApp app = new SharpAITuiApp(client, backend, new ConnectionProfile(), profileStore.Store);
                app.Host.Start(); app.Navigate("/api-explorer"); app.Host.RenderOnce();
                TestAssert.Contains(Frame(app), "API Explorer");
                backend.FeedKey("F2"); app.Host.PumpInputOnce(); app.Host.RenderOnce(); TestAssert.Contains(Frame(app), "Models");
                backend.FeedKey("Escape"); app.Host.PumpInputOnce(); app.Host.PumpInputOnce();
                backend.Resize(60, 20); app.Host.RenderOnce(); TestAssert.Contains(Frame(app), "Resize");
            }
            await Task.CompletedTask.ConfigureAwait(false);
        }
        private static async Task ClipboardFallbackAsync(CancellationToken ct)
        {
            using ServerClient client = Client(); using HeadlessBackend backend = new HeadlessBackend(120, 40) { ClaimsTerminal = false };
            TestClipboard clipboard = new TestClipboard();
            using TestProfileStore profileStore = new TestProfileStore(); using SharpAITuiApp app = new SharpAITuiApp(client, backend, new ConnectionProfile(), profileStore.Store, clipboard: clipboard); app.Host.Start();
            app.Navigate("/system-prompts"); await app.DrainAsync(ct).ConfigureAwait(false); backend.FeedKey("Enter"); app.Host.PumpInputOnce(); app.Host.RenderOnce();
            backend.FeedKey("ctrl+y"); app.Host.PumpInputOnce(); await app.DrainAsync(ct).ConfigureAwait(false);
            TestAssert.Contains(clipboard.Text, "Helpful assistant"); TestAssert.Contains(Frame(app), "Copy result");
        }
        private static async Task ClipboardRaceAsync(CancellationToken ct)
        {
            using ServerClient client = Client(); using HeadlessBackend backend = new HeadlessBackend(120, 40) { ClaimsTerminal = false };
            TestClipboard clipboard = new TestClipboard { Pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously) };
            using TestProfileStore profileStore = new TestProfileStore(); using SharpAITuiApp app = new SharpAITuiApp(client, backend, new ConnectionProfile(), profileStore.Store, clipboard: clipboard);
            app.Host.Start(); app.Navigate("/system-prompts"); await app.DrainAsync(ct).ConfigureAwait(false);
            backend.FeedKey("Enter"); app.Host.PumpInputOnce(); backend.FeedKey("ctrl+y"); app.Host.PumpInputOnce();
            TestAssert.Contains(clipboard.Text, "Helpful assistant");
            app.Navigate("/api-explorer"); clipboard.Pending.SetResult(false); await app.DrainAsync(ct).ConfigureAwait(false);
            TestAssert.Contains(Frame(app), "API Explorer"); TestAssert.DoesNotContain(Frame(app), "Copy result"); TestAssert.DoesNotContain(Frame(app), "Be helpful.");
        }
        private static async Task StreamsAsync(CancellationToken ct)
        {
            using ServerClient client = Client();
            foreach (string mode in new[] { "ndjson", "sse" })
            {
                List<JsonNode> frames = new List<JsonNode>();
                await foreach (JsonNode frame in client.StreamAsync("/fixture/stream/" + mode, new JsonObject(), ct).ConfigureAwait(false)) frames.Add(frame);
                TestAssert.Contains(frames[0].ToJsonString(new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }), "你好");
                TestAssert.Equal(mode == "sse" ? 1 : 2, frames.Count);
            }
            foreach (string mode in new[] { "error", "malformed", "oversize" })
            {
                client.MaxFrameCharacters = 1024; bool failed = false;
                try { await foreach (JsonNode frame in client.StreamAsync("/fixture/stream/" + mode, new JsonObject(), ct).ConfigureAwait(false)) { } }
                catch (Exception error) when (error is InvalidDataException or System.Text.Json.JsonException) { failed = true; }
                TestAssert.True(failed, mode + " is explicit");
            }
        }
        private static async Task ErrorsAsync(CancellationToken ct)
        {
            using ServerClient client = Client();
            foreach (int status in new[] { 401, 403, 404, 429, 500, 503 })
            {
                bool failed = false;
                try { await client.JsonAsync("GET", "/fixture/status/" + status, null, ct).ConfigureAwait(false); }
                catch (ApiException error) { TestAssert.Equal((HttpStatusCode)status, error.StatusCode); failed = true; }
                TestAssert.True(failed, "typed HTTP failure");
            }
        }
        private static async Task SessionRaceAsync(CancellationToken ct)
        {
            using ServerClient client = Client(); client.Token = "old-session";
            int failures = 0; client.AuthorizationFailed += status => failures++;
            Task<ApiResponse> pending = client.ExecuteAsync("GET", "/fixture/delayed401", null, ct);
            client.Token = "new-session";
            ApiResponse response = await pending.ConfigureAwait(false);
            TestAssert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); TestAssert.Equal(0, failures);
            await client.ExecuteAsync("GET", "/fixture/status/401", null, ct).ConfigureAwait(false);
            TestAssert.Equal(1, failures);
        }
        private static async Task BoundedAsync(CancellationToken ct)
        {
            using ServerClient client = Client(); client.MaxResponseBytes = 100;
            ApiResponse result = await client.ExecuteAsync("GET", "/fixture/large", null, ct).ConfigureAwait(false);
            TestAssert.Equal(100, result.Body.Length); TestAssert.True(result.Truncated, "large response bounded");
        }
        private static async Task NoMutationRetryAsync(CancellationToken ct)
        {
            using ServerClient client = Client();
            JsonNode? before = await client.JsonAsync("GET", "/fixture/count", null, ct).ConfigureAwait(false);
            await client.ExecuteAsync("POST", "/fixture/mutation", new JsonObject(), ct).ConfigureAwait(false);
            JsonNode? after = await client.JsonAsync("GET", "/fixture/count", null, ct).ConfigureAwait(false);
            TestAssert.Equal(before!["mutations"]!.GetValue<int>() + 1, after!["mutations"]!.GetValue<int>());
        }
        private static async Task CancellationAsync(CancellationToken ct)
        {
            using ServerClient client = Client(); using CancellationTokenSource cancelled = CancellationTokenSource.CreateLinkedTokenSource(ct); cancelled.CancelAfter(100);
            bool stopped = false;
            try { await client.JsonAsync("GET", "/fixture/hang", null, cancelled.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { stopped = true; }
            TestAssert.True(stopped, "request cancellation propagates");
        }
        private static async Task RoutesAsync(CancellationToken ct)
        {
            using ServerClient client = Client(); using HeadlessBackend backend = new HeadlessBackend(120, 40) { ClaimsTerminal = false };
            using TestProfileStore profileStore = new TestProfileStore(); using SharpAITuiApp app = new SharpAITuiApp(client, backend, new ConnectionProfile(), profileStore.Store); app.Host.Start(); await app.ConnectAsync().ConfigureAwait(false); await app.DrainAsync(ct).ConfigureAwait(false);
            foreach (string route in new[] { "/", "/models", "/system-prompts", "/model-configurations", "/playground", "/request-history", "/request-history/inference", "/settings", "/observability", "/access/tenants" })
            {
                app.Navigate(route); await app.DrainAsync(ct).ConfigureAwait(false); app.Host.RenderOnce(); TestAssert.True(Frame(app).Length > 0, "rendered " + route);
                FocusAudit.Run(app.Host).ThrowIfProblems();
            }
        }
        private static string Frame(SharpAITuiApp app)
        {
            CellBuffer frame = app.Host.CaptureFrame()!; StringBuilder text = new StringBuilder();
            for (int y = 0; y < frame.Height; y++) { for (int x = 0; x < frame.Width; x++) text.Append(frame.Get(x, y).Grapheme); text.AppendLine(); }
            return text.ToString();
        }
    }
}
