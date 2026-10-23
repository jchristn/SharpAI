namespace Test.Shared.Tui
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Client;
    using SharpAI.Tui;
    using SharpAI.Tui.Services;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Terminal;

    /// <summary>Composer commands stay local; compaction uses the real HTTP client and commits atomically.</summary>
    public static class TuiChatCommandSuite
    {
        /// <summary>Build command and Markdown operator checks.</summary>
        public static TestSuiteDescriptor Build()
        {
            bool skip = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SHARPAI_TUI_TEST_URL"));
            return new TestSuiteDescriptor("TuiChatCommands", "Chat commands and Markdown", new[]
            {
                new TestCaseDescriptor("TuiChatCommands", "Parser", "Shared aliases and literal slash escaping", ct =>
                {
                    TestAssert.Equal("help", ChatCommands.Parse(" /HELP ").Id); TestAssert.Equal("clear", ChatCommands.Parse("/new").Id);
                    TestAssert.Equal("stop", ChatCommands.Parse("/cancel").Id); TestAssert.Equal("compact", ChatCommands.Parse("/compact").Id);
                    TestAssert.Equal("example/model", ChatCommands.Parse("/model example/model").Arguments);
                    TestAssert.Equal("unknown", ChatCommands.Parse("/missing").Id); TestAssert.True(!ChatCommands.Parse("//literal").IsCommand, "double slash is literal");
                    TestAssert.Equal("/literal", ChatCommands.Parse("//literal").Prompt); return Task.CompletedTask;
                }),
                new TestCaseDescriptor("TuiChatCommands", "Commands", "Help, model, compact and clear affect the correct context", CommandsAsync, skip: skip, skipReason: "Start the checked Postman TUI simulation."),
                new TestCaseDescriptor("TuiChatCommands", "Failures", "Failed, truncated and cancelled summaries preserve original context", FailuresAsync, skip: skip, skipReason: "Start the checked Postman TUI simulation."),
                new TestCaseDescriptor("TuiChatCommands", "Markdown", "Chat uses TUIKit Markdown rendering for headings, links, lists, tables and code", MarkdownAsync, skip: skip, skipReason: "Start the checked Postman TUI simulation.")
            });
        }

        private static async Task CommandsAsync(CancellationToken ct)
        {
            using ServerClient client = Client("chat-commands"); using HeadlessBackend backend = new HeadlessBackend(120, 60) { ClaimsTerminal = false };
            using TestProfileStore store = new TestProfileStore(); using SharpAITuiApp app = new SharpAITuiApp(client, backend, new ConnectionProfile(), store.Store);
            await OpenAsync(app, ct).ConfigureAwait(false);
            await SendAsync(app, backend, "/?", ct).ConfigureAwait(false); TestAssert.Contains(Frame(app), "/compact");
            await SendAsync(app, backend, "/missing", ct).ConfigureAwait(false); TestAssert.Contains(Frame(app), "Unknown command");
            await SendAsync(app, backend, "/status", ct).ConfigureAwait(false); TestAssert.Contains(Frame(app), "Context messages");
            TestAssert.Equal(0, (await RequestsAsync(client, ct).ConfigureAwait(false)).Count);
            await SendAsync(app, backend, "remember violet", ct).ConfigureAwait(false);
            await SendAsync(app, backend, "add detail", ct).ConfigureAwait(false);
            await SendAsync(app, backend, "/compact", ct).ConfigureAwait(false); TestAssert.Contains(Frame(app), "Context compacted");
            await SendAsync(app, backend, "next", ct).ConfigureAwait(false);
            JsonArray requests = await RequestsAsync(client, ct).ConfigureAwait(false);
            JsonNode compact = requests[2]!["body"]!; TestAssert.Equal("fixture-model", compact["model"]!.ToString()); TestAssert.True(!compact["stream"]!.GetValue<bool>(), "summary is nonstreaming");
            TestAssert.Equal(ChatCommands.CompactInstruction, compact["messages"]![0]!["content"]!.ToString());
            JsonObject data = (JsonObject)JsonNode.Parse(compact["messages"]![1]!["content"]!.ToString())!; TestAssert.Equal(2, ((JsonArray)data["earlierMessages"]!).Count);
            JsonArray next = (JsonArray)requests[3]!["body"]!["messages"]!;
            TestAssert.True(next.Any(message => message!["content"]!.ToString().StartsWith(ChatCommands.SummaryPrefix, StringComparison.Ordinal)), "summary reaches the next request");
            TestAssert.True(!next.Any(message => message!["content"]!.ToString() == "remember violet"), "summarized turns leave the context");
            TestAssert.True(next.Any(message => message!["content"]!.ToString() == "add detail"), "latest exchange is retained");
            await SendAsync(app, backend, "/compact", ct).ConfigureAwait(false);
            requests = await RequestsAsync(client, ct).ConfigureAwait(false);
            JsonObject repeated = (JsonObject)JsonNode.Parse(requests.Last()!["body"]!["messages"]![1]!["content"]!.ToString())!;
            TestAssert.Contains(repeated["previousSummary"]!.ToString(), "prefers violet");
            TestAssert.Equal("add detail", repeated["earlierMessages"]![0]!["content"]!.ToString());
            await SendAsync(app, backend, "/clear", ct).ConfigureAwait(false); await SendAsync(app, backend, "//literal", ct).ConfigureAwait(false);
            requests = await RequestsAsync(client, ct).ConfigureAwait(false); JsonArray fresh = (JsonArray)requests.Last()!["body"]!["messages"]!;
            TestAssert.Equal(2, fresh.Count); TestAssert.Equal("/literal", fresh.Last()!["content"]!.ToString());
        }

        private static async Task FailuresAsync(CancellationToken ct)
        {
            foreach (string mode in new[] { "compact-failure", "compact-length", "compact-cancel" })
            {
                using ServerClient client = Client(mode); using HeadlessBackend backend = new HeadlessBackend(120, 60) { ClaimsTerminal = false };
                using TestProfileStore store = new TestProfileStore(); using SharpAITuiApp app = new SharpAITuiApp(client, backend, new ConnectionProfile(), store.Store);
                await OpenAsync(app, ct).ConfigureAwait(false); await SendAsync(app, backend, "keep this context", ct).ConfigureAwait(false);
                Submit(app, backend, "/compact");
                if (mode == "compact-cancel")
                {
                    for (int attempt = 0; attempt < 100 && (await RequestsAsync(client, ct).ConfigureAwait(false)).Count < 2; attempt++) { app.Host.PumpInputOnce(); await Task.Delay(10, ct).ConfigureAwait(false); }
                    Submit(app, backend, "/stop");
                }
                await app.DrainAsync(ct).ConfigureAwait(false);
                await SendAsync(app, backend, "continue", ct).ConfigureAwait(false);
                JsonArray requests = await RequestsAsync(client, ct).ConfigureAwait(false); JsonArray messages = (JsonArray)requests.Last()!["body"]!["messages"]!;
                TestAssert.True(messages.Any(message => message!["content"]!.ToString() == "keep this context"), mode + " preserves original messages");
                TestAssert.True(!messages.Any(message => message!["content"]!.ToString().StartsWith(ChatCommands.SummaryPrefix, StringComparison.Ordinal)), "no partial summary is committed");
            }
        }

        private static async Task MarkdownAsync(CancellationToken ct)
        {
            using ServerClient client = Client("chat-markdown"); using HeadlessBackend backend = new HeadlessBackend(120, 60) { ClaimsTerminal = false };
            using TestProfileStore store = new TestProfileStore(); using SharpAITuiApp app = new SharpAITuiApp(client, backend, new ConnectionProfile(), store.Store);
            await OpenAsync(app, ct).ConfigureAwait(false); await SendAsync(app, backend, "render markdown", ct).ConfigureAwait(false);
            string frame = Frame(app);
            foreach (string text in new[] { "Heading example", "bold phrase", "first item", "docs", "Table A", "string sample" }) TestAssert.Contains(frame, text);
            foreach (string syntax in new[] { "# Heading example", "**bold phrase**", "[docs](", "```csharp", "| --- |" }) TestAssert.DoesNotContain(frame, syntax);
            TuiThemeSuite.ValidatePadding(app.Host.CaptureFrame()!, "Markdown chat");
            TestAssert.True(app.Host.CurrentFocusPath.Nodes.Any(node => node is SharpAI.Tui.Widgets.Composer), "composer keeps focus after sending");
        }
        private static ServerClient Client(string mode) { return new ServerClient(Environment.GetEnvironmentVariable("SHARPAI_TUI_TEST_URL")!) { Token = "fixture-" + mode + "-" + Guid.NewGuid().ToString("N") }; }
        private static async Task OpenAsync(SharpAITuiApp app, CancellationToken ct) { app.Host.Start(); await app.ConnectAsync().ConfigureAwait(false); await app.DrainAsync(ct).ConfigureAwait(false); app.Navigate("/playground"); await app.DrainAsync(ct).ConfigureAwait(false); }
        private static async Task<JsonArray> RequestsAsync(ServerClient client, CancellationToken ct) { return (JsonArray)(await client.JsonAsync("GET", "/fixture/chat-requests", null, ct).ConfigureAwait(false))!; }
        private static async Task SendAsync(SharpAITuiApp app, HeadlessBackend backend, string text, CancellationToken ct) { Submit(app, backend, text); await app.DrainAsync(ct).ConfigureAwait(false); }
        private static void Submit(SharpAITuiApp app, HeadlessBackend backend, string text)
        {
            app.Host.RenderOnce(); string[] lines = Frame(app).Split('\n'); int row = Array.FindIndex(lines, line => line.Contains("Composer |", StringComparison.Ordinal));
            int column = lines[row].IndexOf("Composer", StringComparison.Ordinal); backend.FeedClick(column + 2, row + 1); app.Host.PumpInputOnce(); backend.FeedInput(text); app.Host.PumpInputOnce(); backend.FeedKey("Enter"); app.Host.PumpInputOnce();
        }
        private static string Frame(SharpAITuiApp app)
        {
            app.Host.RenderOnce(); CellBuffer cells = app.Host.CaptureFrame()!; StringBuilder text = new StringBuilder();
            for (int row = 0; row < cells.Height; row++) { for (int column = 0; column < cells.Width; column++) text.Append(cells.Get(column, row).Grapheme); text.AppendLine(); }
            return text.ToString();
        }
    }
}
