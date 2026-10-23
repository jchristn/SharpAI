namespace Test.Shared.Tui
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
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
    using TUIKit.Input;
    using TUIKit.Terminal;

    /// <summary>History charts retain counts, UTC drilldown, keyboard access and explicit unavailable states.</summary>
    public static class TuiHistoryChartSuite
    {
        /// <summary>Build shared chart and real HTTP operator checks.</summary>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor("TuiHistoryCharts", "Rendering", "Stacked counts, merged intervals and resize preserve the selected UTC range", RenderingAsync),
                new TestCaseDescriptor("TuiHistoryCharts", "States", "Loading, empty and failed charts cannot activate stale data", StatesAsync)
            };
            foreach (string category in new[] { "api", "inference", "embeddings" })
            {
                string selected = category;
                cases.Add(new TestCaseDescriptor("TuiHistoryCharts", category, category + " chart loads, drills and clears through the public HTTP contract", ct => OperatorAsync(selected, ct), skip: string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SHARPAI_TUI_TEST_URL")), skipReason: "Start the checked Postman TUI simulation."));
            }
            return new TestSuiteDescriptor("TuiHistoryCharts", "History activity charts", cases);
        }
        private static JsonObject Summary(int count)
        {
            JsonArray buckets = new JsonArray(); DateTimeOffset start = DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture);
            for (int index = 0; index < count; index++) buckets.Add(new JsonObject { ["BucketStartUtc"] = start.AddMinutes(index).ToString("O"), ["BucketEndUtc"] = start.AddMinutes(index + 1).ToString("O"), ["SuccessCount"] = 3, ["FailureCount"] = 1 });
            return new JsonObject { ["TotalCount"] = count * 4, ["TotalSuccess"] = count * 3, ["AverageDurationMs"] = 12.5, ["Buckets"] = buckets };
        }
        private static Task RenderingAsync(CancellationToken ct)
        {
            foreach (bool ascii in new[] { false, true })
            {
                HistoryActivityChart chart = new HistoryActivityChart(new Localizer("en"), ascii); chart.Bind(HistoryActivityData.Parse(Summary(96))); chart.OnFocusChanged(true);
                HistoryActivityBucket? selected = null; chart.Activated = bucket => selected = bucket;
                string frame = Render(chart, 60, 12); TestAssert.Contains(frame, "Requests 384"); TestAssert.Contains(frame, "75%"); TestAssert.Contains(frame, "12.5 ms");
                TestAssert.Contains(frame, ascii ? "S" : "█"); TestAssert.Contains(frame, ascii ? "F" : "▒");
                Dictionary<DateTimeOffset, HistoryActivityBucket> visible = new Dictionary<DateTimeOffset, HistoryActivityBucket>();
                chart.HandleKey(KeyEvent.Special(KeyCode.Home));
                for (int item = 0; item < 96; item++) { chart.HandleKey(KeyEvent.Special(KeyCode.Enter)); visible[selected!.Start] = selected; chart.HandleKey(KeyEvent.Special(KeyCode.Right)); }
                TestAssert.Equal(384m, visible.Values.Sum(bucket => bucket.Total)); TestAssert.Equal(288m, visible.Values.Sum(bucket => bucket.Success)); TestAssert.Equal(96m, visible.Values.Sum(bucket => bucket.Failure));
                chart.HandleKey(KeyEvent.Special(KeyCode.End)); chart.HandleKey(KeyEvent.Special(KeyCode.Enter));
                TestAssert.True(selected != null && selected.End == DateTimeOffset.Parse("2026-01-01T01:36:00Z", CultureInfo.InvariantCulture), "last rendered bar reaches the range end");
                TestAssert.Equal(selected!.Intervals * 4m, selected.Total);
                DateTimeOffset previous = selected.Start; Render(chart, 160, 18); chart.HandleKey(KeyEvent.Special(KeyCode.Enter));
                TestAssert.True(selected!.Start <= previous && selected.End > previous, "resize keeps the selected time in view");
                chart.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 10, 3, KeyModifiers.None, 1));
                TestAssert.Equal(DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture), selected!.Start);
            }
            return Task.CompletedTask;
        }
        private static Task StatesAsync(CancellationToken ct)
        {
            HistoryActivityChart chart = new HistoryActivityChart(new Localizer("en"), false); int activated = 0; chart.Activated = bucket => activated++;
            TestAssert.Contains(Render(chart, 80, 8), "Loading"); chart.Bind(HistoryActivityData.Parse(Summary(2))); Render(chart, 80, 8);
            chart.Bind(HistoryActivityData.Parse(Summary(0))); TestAssert.Contains(Render(chart, 80, 8), "No activity"); chart.HandleKey(KeyEvent.Special(KeyCode.Enter)); TestAssert.Equal(0, activated);
            chart.Failed("HTTP 503: unavailable"); TestAssert.Contains(Render(chart, 80, 8), "503"); TestAssert.DoesNotContain(Render(chart, 80, 8), "Requests 0");
            chart.HandleKey(KeyEvent.Special(KeyCode.Enter)); TestAssert.Equal(0, activated);
            return Task.CompletedTask;
        }
        private static async Task OperatorAsync(string category, CancellationToken ct)
        {
            string endpoint = Environment.GetEnvironmentVariable("SHARPAI_TUI_TEST_URL")!;
            using ServerClient client = new ServerClient(endpoint); using HeadlessBackend backend = new HeadlessBackend(80, 24) { ClaimsTerminal = false };
            using TestProfileStore store = new TestProfileStore(); using SharpAITuiApp app = new SharpAITuiApp(client, backend, new ConnectionProfile { Server = endpoint }, store.Store);
            app.Host.Start(); await app.ConnectAsync().ConfigureAwait(false); await app.DrainAsync(ct).ConfigureAwait(false);
            app.Navigate(category == "api" ? "/request-history" : "/request-history/" + category); await app.DrainAsync(ct).ConfigureAwait(false);
            string frame = Frame(app); TestAssert.Contains(frame, "Request activity"); TestAssert.Contains(frame, "Success"); TestAssert.Contains(frame, "Failure"); TestAssert.Contains(frame, "█");
            JsonNode initial = await LastQueryAsync(client, true, ct).ConfigureAwait(false);
            TestAssert.Equal(category == "api" ? "" : category == "inference" ? "generation" : "embeddings", JsonData.Text(initial, "category")); TestAssert.Equal("15", JsonData.Text(initial, "bucketMinutes"));
            Click(app, backend, "Hour"); await app.DrainAsync(ct).ConfigureAwait(false);
            JsonNode hour = await LastQueryAsync(client, true, ct).ConfigureAwait(false); TestAssert.Equal("1", JsonData.Text(hour, "bucketMinutes"));
            DateTimeOffset start = DateTimeOffset.Parse(JsonData.Text(hour, "fromUtc"), CultureInfo.InvariantCulture); DateTimeOffset end = DateTimeOffset.Parse(JsonData.Text(hour, "toUtc"), CultureInfo.InvariantCulture); TestAssert.Equal(60d, (end - start).TotalMinutes);
            // The narrow layout starts with the chart. A click selects and drills an actual rendered bar.
            app.Host.RenderOnce(); string[] lines = Frame(app).Split('\n'); int legend = Array.FindIndex(lines, line => line.Contains("Success", StringComparison.Ordinal) && line.Contains("Failure", StringComparison.Ordinal));
            backend.FeedClick(20, legend + 1); app.Host.PumpInputOnce(); await app.DrainAsync(ct).ConfigureAwait(false);
            JsonNode drilled = await LastQueryAsync(client, false, ct).ConfigureAwait(false);
            TestAssert.True(DateTimeOffset.Parse(JsonData.Text(drilled, "toUtc"), CultureInfo.InvariantCulture) - DateTimeOffset.Parse(JsonData.Text(drilled, "fromUtc"), CultureInfo.InvariantCulture) <= TimeSpan.FromMinutes(2), "chart limits the table to the rendered interval");
            TestAssert.True(drilled["bucketMinutes"] == null, "list query has no summary bucket size");
            backend.FeedKey("End"); app.Host.PumpInputOnce(); backend.FeedKey("Enter"); app.Host.PumpInputOnce(); await app.DrainAsync(ct).ConfigureAwait(false);
            JsonNode keyboardDrill = await LastQueryAsync(client, false, ct).ConfigureAwait(false);
            TestAssert.Equal(end.ToUnixTimeMilliseconds(), DateTimeOffset.Parse(JsonData.Text(keyboardDrill, "toUtc"), CultureInfo.InvariantCulture).ToUnixTimeMilliseconds());
            Click(app, backend, "Clear range"); await app.DrainAsync(ct).ConfigureAwait(false);
            JsonNode cleared = await LastQueryAsync(client, false, ct).ConfigureAwait(false); TestAssert.Equal(JsonData.Text(hour, "fromUtc"), JsonData.Text(cleared, "fromUtc")); TestAssert.Equal(JsonData.Text(hour, "toUtc"), JsonData.Text(cleared, "toUtc"));
            backend.FeedKey("F5"); app.Host.PumpInputOnce(); await app.DrainAsync(ct).ConfigureAwait(false);
            JsonNode refreshed = await LastQueryAsync(client, true, ct).ConfigureAwait(false);
            TestAssert.True(DateTimeOffset.Parse(JsonData.Text(refreshed, "toUtc"), CultureInfo.InvariantCulture) > end, "F5 refreshes summary data after drilling and clearing");
            Click(app, backend, "Activity chart"); await app.DrainAsync(ct).ConfigureAwait(false); TestAssert.Contains(Frame(app), "Request activity"); TestAssert.DoesNotContain(Frame(app), "ID: req_fixture");
            Click(app, backend, "Back"); await app.DrainAsync(ct).ConfigureAwait(false); TestAssert.Contains(Frame(app), "ID: req_fixture");
            string? directory = Environment.GetEnvironmentVariable("SHARPAI_TUI_RECORDINGS"); if (directory != null) { Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, "chart-" + category + "-80.txt"), Frame(app)); }
            backend.Resize(120, 40); app.Host.RenderOnce(); TestAssert.Contains(Frame(app), "Request activity");
            if (directory != null) File.WriteAllText(Path.Combine(directory, "chart-" + category + "-120.txt"), Frame(app));
        }
        private static async Task<JsonNode> LastQueryAsync(ServerClient client, bool summary, CancellationToken ct)
        {
            JsonArray queries = (JsonArray)(await client.JsonAsync("GET", "/fixture/history-queries", null, ct).ConfigureAwait(false))!;
            return queries.Last(query => JsonData.Text(query, "path").EndsWith(summary ? "/summary" : "/request-history", StringComparison.Ordinal))!["query"]!.DeepClone();
        }
        private static void Click(SharpAITuiApp app, HeadlessBackend backend, string label)
        {
            app.Host.RenderOnce(); string[] lines = Frame(app).Split('\n');
            for (int row = 0; row < lines.Length; row++) { int column = lines[row].IndexOf(label, StringComparison.Ordinal); if (column >= 0) { backend.FeedClick(column + 1, row); app.Host.PumpInputOnce(); return; } }
            throw new InvalidOperationException("Visible history action not found: " + label + "\n" + Frame(app));
        }
        private static string Render(HistoryActivityChart chart, int width, int height)
        {
            CellBuffer cells = new CellBuffer(width, height); chart.Render(new BufferSurface(cells)); return Cells(cells);
        }
        private static string Frame(SharpAITuiApp app) { return Cells(app.Host.CaptureFrame()!); }
        private static string Cells(CellBuffer cells)
        {
            StringBuilder text = new StringBuilder(); for (int row = 0; row < cells.Height; row++) { for (int column = 0; column < cells.Width; column++) text.Append(cells.Get(column, row).Grapheme); text.AppendLine(); } return text.ToString();
        }
    }
}
