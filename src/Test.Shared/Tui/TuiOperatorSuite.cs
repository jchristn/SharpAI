namespace Test.Shared.Tui
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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

    /// <summary>Simulated operators drive the actual host parser and pointer hit map against a live isolated server.</summary>
    public static class TuiOperatorSuite
    {
        /// <summary>Build live operator descriptors, sharing the test implementation across runners.</summary>
        public static TestSuiteDescriptor Build()
        {
            bool skip = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SHARPAI_TUI_LIVE_URL"));
            return new TestSuiteDescriptor("TuiOperator", "Terminal operator workflows", new[]
            {
                new TestCaseDescriptor("TuiOperator", "Chat", "Choose a real model and send a native chat with keyboard and pointer input", ChatAsync, skip: skip, skipReason: "Requires isolated live server and token file."),
                new TestCaseDescriptor("TuiOperator", "Access", "Create and edit a tenant role through terminal forms; delete with confirmation", AccessAsync, skip: skip, skipReason: "Requires isolated live server and token file."),
                new TestCaseDescriptor("TuiOperator", "Prompt", "Create a personal saved prompt through the typed terminal form", PromptAsync, skip: skip, skipReason: "Requires isolated live server and token file.")
            });
        }
        private static ServerClient Client()
        {
            return new ServerClient(Environment.GetEnvironmentVariable("SHARPAI_TUI_LIVE_URL")!) { Token = File.ReadAllText(Environment.GetEnvironmentVariable("SHARPAI_TUI_TOKEN_FILE")!).Trim() };
        }
        private static async Task ChatAsync(CancellationToken ct)
        {
            using ServerClient client = Client(); using HeadlessBackend backend = new HeadlessBackend(120, 40) { ClaimsTerminal = false };
            using TestProfileStore profileStore = new TestProfileStore(); using SharpAITuiApp app = new SharpAITuiApp(client, backend, new ConnectionProfile { Server = Environment.GetEnvironmentVariable("SHARPAI_TUI_LIVE_URL")! }, profileStore.Store);
            app.Host.Start(); await app.ConnectAsync().ConfigureAwait(false); await app.DrainAsync(ct).ConfigureAwait(false);
            app.Navigate("/playground"); await app.DrainAsync(ct).ConfigureAwait(false); Record(app, "chat-01-open");
            TestAssert.Contains(Frame(app), "Model: tui-stories");
            Record(app, "chat-02-model"); Click(app, backend, "Composer", 1); backend.FeedInput("Once upon a time"); app.Host.PumpInputOnce(); backend.FeedKey("Enter"); app.Host.PumpInputOnce();
            await app.DrainAsync(ct).ConfigureAwait(false); Record(app, "chat-03-complete");
            TestAssert.Contains(Frame(app), "Token limit reached"); TestAssert.Contains(Frame(app), "Assistant");
        }
        private static async Task PromptAsync(CancellationToken ct)
        {
            using ServerClient client = Client(); using HeadlessBackend backend = new HeadlessBackend(120, 40) { ClaimsTerminal = false };
            using TestProfileStore profileStore = new TestProfileStore(); using SharpAITuiApp app = new SharpAITuiApp(client, backend, new ConnectionProfile { Server = Environment.GetEnvironmentVariable("SHARPAI_TUI_LIVE_URL")! }, profileStore.Store);
            string name = "TUI operator " + Guid.NewGuid().ToString("N");
            app.Host.Start(); await app.ConnectAsync().ConfigureAwait(false); await app.DrainAsync(ct).ConfigureAwait(false);
            try
            {
                app.Navigate("/system-prompts"); await app.DrainAsync(ct).ConfigureAwait(false); Click(app, backend, "Create", 0); app.Host.RenderOnce();
                Record(app, "prompt-01-form"); Click(app, backend, "Name", 1); backend.FeedInput(name); app.Host.PumpInputOnce();
                Click(app, backend, "Content", 1); backend.FeedInput("Reply concisely."); app.Host.PumpInputOnce(); backend.FeedKey("ctrl+s"); app.Host.PumpInputOnce(); await app.DrainAsync(ct).ConfigureAwait(false);
                Record(app, "prompt-02-saved");
                JsonNodeResult result = await FindAsync(client, name, ct).ConfigureAwait(false);
                TestAssert.True(result.Id != null, "typed terminal form created server record"); TestAssert.Equal("Reply concisely.", result.Content);
            }
            finally
            {
                JsonNodeResult result = await FindAsync(client, name, ct).ConfigureAwait(false);
                if (result.Id != null) await client.DeleteLibraryAsync("system-prompts", result.Id, ct).ConfigureAwait(false);
            }
        }
        private static async Task AccessAsync(CancellationToken ct)
        {
            using ServerClient client = Client();
            JsonNode tenant = (await client.JsonAsync("POST", "/v1.0/tenants", new JsonObject { ["Name"] = "Terminal operator " + Guid.NewGuid().ToString("N") }, ct).ConfigureAwait(false))!;
            string tenantId = JsonData.Id(tenant); string path = "/v1.0/tenants/" + ServerClient.Segment(tenantId) + "/roles";
            try
            {
                using HeadlessBackend backend = new HeadlessBackend(120, 40) { ClaimsTerminal = false };
                using TestProfileStore profileStore = new TestProfileStore();
                using SharpAITuiApp app = new SharpAITuiApp(client, backend, new ConnectionProfile { Server = Environment.GetEnvironmentVariable("SHARPAI_TUI_LIVE_URL")!, Tenant = tenantId }, profileStore.Store);
                app.Host.Start(); await app.ConnectAsync().ConfigureAwait(false); await app.DrainAsync(ct).ConfigureAwait(false);
                app.Navigate("/access/roles"); await app.DrainAsync(ct).ConfigureAwait(false); Click(app, backend, "Create", 0); Click(app, backend, "Name", 1);
                backend.FeedInput("Terminal role"); app.Host.PumpInputOnce(); backend.FeedKey("ctrl+s"); app.Host.PumpInputOnce(); await app.DrainAsync(ct).ConfigureAwait(false);
                JsonNode? page = await client.JsonAsync("GET", path, null, ct).ConfigureAwait(false);
                JsonNode? created = null; foreach (JsonNode row in JsonData.Rows(page)) if (JsonData.Text(row, "Name") == "Terminal role") created = row;
                TestAssert.True(created != null, "terminal role create reached the public API");
                Record(app, "access-01-created");
                Click(app, backend, "Terminal role", 0); backend.FeedKey("Enter"); app.Host.PumpInputOnce(); await app.DrainAsync(ct).ConfigureAwait(false);
                Click(app, backend, "Edit", 0); Click(app, backend, "Name", 1); backend.FeedKey("End"); foreach (char character in "Terminal role") backend.FeedKey("Backspace"); backend.FeedInput("Updated terminal role"); app.Host.PumpInputOnce(); backend.FeedKey("ctrl+s"); app.Host.PumpInputOnce(); await app.DrainAsync(ct).ConfigureAwait(false);
                page = await client.JsonAsync("GET", path, null, ct).ConfigureAwait(false);
                JsonNode? updated = null; foreach (JsonNode row in JsonData.Rows(page)) if (JsonData.Id(row) == JsonData.Id(created)) updated = row;
                TestAssert.Equal("Updated terminal role", JsonData.Text(updated, "Name"));
                Click(app, backend, "Updated terminal role", 0); backend.FeedKey("Enter"); app.Host.PumpInputOnce(); await app.DrainAsync(ct).ConfigureAwait(false);
                Click(app, backend, "Delete", 0); Record(app, "access-02-confirm"); Click(app, backend, "Yes", 0); backend.FeedKey("Enter"); app.Host.PumpInputOnce(); await app.DrainAsync(ct).ConfigureAwait(false);
                page = await client.JsonAsync("GET", path, null, ct).ConfigureAwait(false);
                foreach (JsonNode row in JsonData.Rows(page)) TestAssert.True(JsonData.Id(row) != JsonData.Id(created), "confirmed terminal delete removed record");
                Record(app, "access-03-deleted");
            }
            finally { using ServerClient cleanup = Client(); await cleanup.JsonAsync("DELETE", "/v1.0/tenants/" + ServerClient.Segment(tenantId), null, ct).ConfigureAwait(false); }
        }
        private static async Task<JsonNodeResult> FindAsync(ServerClient client, string name, CancellationToken ct)
        {
            System.Text.Json.Nodes.JsonNode? page = await client.JsonAsync("GET", "/v1.0/system-prompts?name=" + ServerClient.Segment(name), null, ct).ConfigureAwait(false);
            System.Collections.Generic.IReadOnlyList<System.Text.Json.Nodes.JsonNode> rows = JsonData.Rows(page);
            return rows.Count == 0 ? new JsonNodeResult() : new JsonNodeResult { Id = JsonData.Id(rows[0]), Content = rows[0]["Content"]?.ToString() };
        }
        private static void Click(SharpAITuiApp app, HeadlessBackend backend, string label, int below)
        {
            app.Host.RenderOnce(); CellBuffer frame = app.Host.CaptureFrame()!;
            for (int y = 0; y < frame.Height - below; y++)
            {
                StringBuilder row = new StringBuilder(); for (int x = 0; x < frame.Width; x++) row.Append(frame.Get(x, y).Grapheme);
                int column = row.ToString().IndexOf(label, StringComparison.Ordinal);
                if (column >= 0) { backend.FeedClick(column + 1, y + below); app.Host.PumpInputOnce(); app.Host.RenderOnce(); return; }
            }
            throw new InvalidOperationException("Visible action not found: " + label + "\n" + Frame(app));
        }
        private static string Frame(SharpAITuiApp app)
        {
            CellBuffer frame = app.Host.CaptureFrame()!; StringBuilder text = new StringBuilder();
            for (int y = 0; y < frame.Height; y++) { for (int x = 0; x < frame.Width; x++) text.Append(frame.Get(x, y).Grapheme); text.AppendLine(); } return text.ToString();
        }
        private static void Record(SharpAITuiApp app, string step)
        {
            string? directory = Environment.GetEnvironmentVariable("SHARPAI_TUI_RECORDINGS"); if (directory == null) return;
            Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, step + ".txt"), Frame(app));
        }
    }
}
