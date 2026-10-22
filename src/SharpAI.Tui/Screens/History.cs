namespace SharpAI.Tui
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;
    using SharpAI.Client;
    using SharpAI.Tui.Services;
    using SharpAI.Tui.Widgets;
    using TUIKit;
    using TUIKit.Layout;
    using TUIKit.Widgets;

    internal sealed partial class SharpAITuiApp
    {
        private readonly Dictionary<string, HistoryFilter> _HistoryFilters = new Dictionary<string, HistoryFilter>();
        private void History(string category)
        {
            if (!_HistoryFilters.TryGetValue(category, out HistoryFilter? state)) { state = new HistoryFilter { Anchor = _Clock.GetUtcNow() }; _HistoryFilters[category] = state; }
            HistoryFilter filter = state;
            DataTable<JsonNode> table = Table(new[] { "CreatedUtc", "Method", "Path", "StatusCode", "DurationMs", "SourceIp", "Id", "InputTokens", "OutputTokens", "CachedTokens", "TtftMs", "QueueMs", "LoadMs", "PromptMs", "GenerationMs", "EngineTotalMs", "Cancelled", "PhaseTimingsApproximate" });
            TextField method = new TextField { Value = filter.Method }; TextField status = new TextField { Value = filter.Status }; TextField path = new TextField { Value = filter.Path }; TextField endpoint = new TextField { Value = filter.Endpoint };
            Form filters = new Form { LabelPlacement = FormLabelPlacement.Left }; filters.Add("Method", method); filters.Add("Status", status); filters.Add("Path contains", path); filters.Add("Endpoint", endpoint);
            DataTable<JsonNode> buckets = new DataTable<JsonNode>().Column("BucketStartUtc", row => JsonData.Text(row, "BucketStartUtc")).Column("Success", row => JsonData.Text(row, "SuccessCount")).Column("Failure", row => JsonData.Text(row, "FailureCount"));
            IWidget activityTable = ViewTable(buckets);
            Action load = () =>
            {
                filter.Method = method.Value; filter.Status = status.Value; filter.Path = path.Value; filter.Endpoint = endpoint.Value;
                Dictionary<string, string?> query = new Dictionary<string, string?> { ["method"] = string.IsNullOrEmpty(method.Value) ? null : method.Value, ["statusCode"] = string.IsNullOrEmpty(status.Value) ? null : status.Value, ["pathContains"] = string.IsNullOrEmpty(endpoint.Value) ? string.IsNullOrEmpty(path.Value) ? null : path.Value : endpoint.Value, ["fromUtc"] = filter.Anchor.AddHours(-filter.Hours).ToString("O", CultureInfo.InvariantCulture), ["toUtc"] = filter.Anchor.ToString("O", CultureInfo.InvariantCulture), ["bucketMinutes"] = filter.BucketMinutes.ToString(CultureInfo.InvariantCulture) };
                if (category != "request-history") query["category"] = category;
                string summaryQuery = ServerClient.Query(query); query.Remove("bucketMinutes");
                if (filter.DrillFrom != null) query["fromUtc"] = filter.DrillFrom;
                if (filter.DrillTo != null) query["toUtc"] = filter.DrillTo;
                string encoded = ServerClient.Query(query);
                LoadTable(table, Scoped("/v1.0/api/request-history" + encoded), row =>
                {
                    long revision = _Revision;
                    Work(async ct => { JsonNode? detail = await _Client.JsonAsync("GET", "/v1.0/api/request-history/" + ServerClient.Segment(JsonData.Id(row)), null, ct).ConfigureAwait(false); Apply(revision, () => { if (detail != null) HistoryInspector(detail, category); }); });
                    return Task.CompletedTask;
                });
                long scope = _Revision;
                Work(async ct => { JsonNode? summary = await _Client.JsonAsync("GET", Scoped("/v1.0/api/request-history/summary" + summaryQuery), null, ct).ConfigureAwait(false); Apply(scope, () => buckets.Bind(JsonData.Rows(summary))); });
            };
            buckets.RowActivated += index => { if (buckets.SelectedRow != null) { filter.DrillFrom = JsonData.Text(buckets.SelectedRow, "BucketStartUtc"); filter.DrillTo = JsonData.Text(buckets.SelectedRow, "BucketEndUtc"); History(category); } };
            ButtonRow actions = new ButtonRow();
            foreach (int hours in new[] { 1, 24, 168, 720 })
            {
                int range = hours; string label = hours switch { 1 => "Hour", 24 => "Day", 168 => "Week", _ => "Month" };
                actions.Add(ButtonFor(label, () => { filter.Hours = range; filter.BucketMinutes = range switch { 1 => 1, 24 => 15, 168 => 120, _ => 1440 }; filter.Anchor = _Clock.GetUtcNow(); filter.DrillFrom = null; filter.DrillTo = null; load(); }));
            }
            actions.Add(ButtonFor("Apply filters", load)); actions.Add(ButtonFor("Clear drilldown", () => { filter.DrillFrom = null; filter.DrillTo = null; load(); }));
            actions.Add(ButtonFor("Filters", () =>
            {
                ButtonRow controls = new ButtonRow(); controls.Add(ButtonFor("Apply filters", () => { filter.Method = method.Value; filter.Status = status.Value; filter.Path = path.Value; filter.Endpoint = endpoint.Value; History(category); })); controls.Add(ButtonFor("Back", () => History(category)));
                Show(new FramedStack().Add(filters, StackSize.Weighted(1)).Add(controls, StackSize.Fixed(3)), "History filters");
            }));
            actions.Add(ButtonFor("Activity intervals", () =>
            {
                ButtonRow controls = new ButtonRow(); controls.Add(ButtonFor("Back", () => History(category)));
                Show(new FramedStack().Add(activityTable, StackSize.Weighted(1)).Add(controls, StackSize.Fixed(3)), "Activity intervals | Enter to drill down");
            }));
            IWidget historyTable = ViewTable(table);
            FramedStack wide = new FramedStack().Add(filters, StackSize.Fixed(9), "Filters").Add(actions, StackSize.Fixed(3)).Add(activityTable, StackSize.Fixed(5), "Activity intervals | Enter to drill down").Add(historyTable, StackSize.Weighted(1));
            FramedStack narrow = new FramedStack().Add(historyTable, StackSize.Weighted(1)).Add(actions, StackSize.Fixed(3));
            Show(new ResponsiveScreen(wide, narrow), "History | UTC timestamps");
            load(); _Refresh = () => { filter.Anchor = _Clock.GetUtcNow(); load(); };
        }
        private void HistoryInspector(JsonNode entry, string category)
        {
            DefinitionList metadata = new DefinitionList();
            foreach (string key in new[] { "Id", "Url", "Method", "Path", "PrincipalName", "TenantId", "UserId", "CreatedUtc", "CompletedUtc", "StatusCode", "DurationMs", "TraceId", "SpanId", "RequestBodyBytes", "RequestBodyTruncated", "ResponseBodyBytes", "ResponseBodyTruncated" }) metadata.Set(key, SafeText.Clean(entry[key]?.ToString() ?? "Unavailable"));
            TextEditor request = new TextEditor { ConsumeUnboundControlKeys = false, Text = SafeText.Clean(entry["RequestHeaders"]?.ToJsonString() + "\n\n" + (entry["RequestBody"]?.ToString() ?? "Body unavailable or withheld by server policy.")), IsReadOnly = true, WordWrap = true };
            TextEditor response = new TextEditor { ConsumeUnboundControlKeys = false, Text = SafeText.Clean(entry["ResponseHeaders"]?.ToJsonString() + "\n\n" + (entry["ResponseBody"]?.ToString() ?? "Body unavailable or withheld by server policy.")), IsReadOnly = true, WordWrap = true };
            DefinitionList phases = new DefinitionList();
            foreach (string key in new[] { "InputTokens", "OutputTokens", "CachedTokens", "TtftMs", "StreamDurationMs", "QueueMs", "LoadMs", "PromptMs", "GenerationMs", "EngineTotalMs", "Cancelled", "PhaseTimingsApproximate" }) phases.Set(key, SafeText.Clean(entry[key]?.ToString() ?? "Unavailable"));
            ButtonRow actions = new ButtonRow(); actions.Add(ButtonFor("Back", () => History(category))); actions.Add(ButtonFor("Copy request", () => { _CopyText = entry["RequestBody"]?.ToString(); Track(CopyAsync()); })); actions.Add(ButtonFor("Copy response", () => { _CopyText = entry["ResponseBody"]?.ToString(); Track(CopyAsync()); }));
            IWidget[] panes = { new ScrollView(metadata, 100, 60), request, response, new ScrollView(phases, 100, 45) };
            FramedStack selected = new FramedStack().Add(panes[0], StackSize.Weighted(1));
            TabStrip tabs = new TabStrip().Add("Metadata").Add("Request").Add("Response").Add("Measurements");
            tabs.ActiveTabChanged += (sender, args) => { foreach (IWidget child in selected.Children.ToArray()) selected.Remove(child); selected.Add(panes[tabs.ActiveIndex], StackSize.Weighted(1)); };
            FramedStack wide = new FramedStack().Add(panes[0], StackSize.Weighted(1), "Request metadata").Add(request, StackSize.Weighted(1), "Request").Add(response, StackSize.Weighted(1), "Response").Add(panes[3], StackSize.Weighted(1), "Measured phases | milliseconds").Add(actions, StackSize.Fixed(3));
            FramedStack narrow = new FramedStack().Add(tabs, StackSize.Fixed(1)).Add(selected, StackSize.Weighted(1)).Add(actions, StackSize.Fixed(2));
            Show(new ResponsiveScreen(wide, narrow), "Request inspector");
        }
    }
}
