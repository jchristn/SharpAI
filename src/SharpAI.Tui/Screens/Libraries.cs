namespace SharpAI.Tui
{
    using System;
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;
    using SharpAI.Client;
    using SharpAI.Tui.Services;
    using TUIKit.Layout;
    using TUIKit.Widgets;

    internal sealed partial class SharpAITuiApp
    {
        private void Library(string kind)
        {
            DataTable<JsonNode> table = Table(new[] { "Id", "Name", "Scope", "Description", "TenantId", "UserId", "UpdatedUtc" });
            LoadTable(table, Scoped("/v1.0/" + kind), row => { Inspect(row, RouteTitle("/" + kind)); return Task.CompletedTask; });
            ButtonRow actions = new ButtonRow();
            actions.Add(ButtonFor("Create", () => LibraryEditor(kind, null, false)));
            actions.Add(ButtonFor("Edit", () => { if (table.SelectedRow != null) LibraryEditor(kind, table.SelectedRow, !JsonData.Flag(table.SelectedRow, "CanEdit")); }));
            actions.Add(ButtonFor("Duplicate", () => { if (table.SelectedRow != null) LibraryEditor(kind, table.SelectedRow, true); }));
            actions.Add(ButtonFor("Delete", () => { if (table.SelectedRow != null && JsonData.Flag(table.SelectedRow, "CanDelete")) Track(DeleteLibraryAsync(kind, table.SelectedRow)); }));
            Show(new FramedStack().Add(ViewTable(table), StackSize.Weighted(1)).Add(actions, StackSize.Fixed(4)), RouteTitle("/" + kind));
        }
        private static JsonObject DefaultConfiguration()
        {
            return new JsonObject { ["Temperature"] = 0.6, ["MaxTokens"] = 128, ["TopP"] = 0.9, ["TopK"] = 40, ["MinP"] = 0.05, ["RepeatPenalty"] = 1.1, ["RepeatLastN"] = 64, ["PresencePenalty"] = 0.0, ["FrequencyPenalty"] = 0.0, ["Seed"] = 0, ["Stop"] = new JsonArray(), ["StreamResponse"] = true, ["JsonOutput"] = false, ["DisplayThinking"] = false };
        }
        private void LibraryEditor(string kind, JsonNode? source, bool duplicate, Action<JsonNode>? saved = null)
        {
            JsonObject draft = new JsonObject { ["Name"] = source?["Name"]?.DeepClone() ?? JsonValue.Create(""), ["Description"] = source?["Description"]?.DeepClone() ?? JsonValue.Create(""), ["Scope"] = duplicate || source == null ? "User" : JsonData.Text(source, "Scope") };
            if (kind == "system-prompts") draft["Content"] = source?["Content"]?.DeepClone() ?? JsonValue.Create("");
            else draft["Configuration"] = source?["Configuration"]?.DeepClone() ?? DefaultConfiguration();
            if (source != null && !duplicate) draft.Remove("Scope");
            string[] scopes = JsonData.Flag(_Context, "isAdmin") ? new[] { "User", "Tenant", "System" } : JsonData.Flag(_Context, "isTenantAdmin") ? new[] { "User", "Tenant" } : new[] { "User" };
            string path = "/v1.0/" + kind + (source == null ? "" : "/" + ServerClient.Segment(JsonData.Id(source)) + (duplicate ? "/duplicate" : ""));
            long revision = _Revision;
            Edit(draft, (duplicate ? "Duplicate " : "Edit ") + kind, value => Mutate(async ct =>
            {
                if (source != null && !duplicate && value["Scope"] != null && JsonData.Text(value, "Scope") != JsonData.Text(source, "Scope")) throw new ArgumentException("Visibility changes require Duplicate.");
                string scope = JsonData.Text(value, "Scope");
                if (scope == "Tenant" && !string.IsNullOrEmpty(_Profile.Tenant)) value["TenantId"] = _Profile.Tenant;
                JsonNode? result = await _Client.JsonAsync(source != null && !duplicate ? "PUT" : "POST", Scoped(path), value, ct).ConfigureAwait(false);
                Apply(revision, () => { _Form?.MarkClean(); _Form = null; if (saved != null && result != null) saved(result); else Library(kind); });
            }), scopes);
        }
        private async Task DeleteLibraryAsync(string kind, JsonNode row)
        {
            if (!await _Host.ConfirmAsync("Delete " + JsonData.Text(row, "Name") + "?").ConfigureAwait(false)) return;
            _Host.Post(() => Work(async ct =>
            {
                await _Client.JsonAsync("DELETE", Scoped("/v1.0/" + kind + "/" + ServerClient.Segment(JsonData.Id(row))), null, ct).ConfigureAwait(false);
                _Host.Post(() => Library(kind));
            }));
        }
    }
}
