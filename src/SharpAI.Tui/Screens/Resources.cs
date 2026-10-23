namespace SharpAI.Tui
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Client;
    using SharpAI.Tui.Services;
    using SharpAI.Tui.Widgets;
    using TUIKit;
    using TUIKit.Layout;
    using TUIKit.Widgets;

    internal sealed partial class SharpAITuiApp
    {
        private DefinitionList Definitions()
        {
            return new DefinitionList { LabelStyle = _Host.Theme.Muted, ValueStyle = _Host.Theme.Text, SectionStyle = _Host.Theme.Accent.WithAttribute(CellAttributes.Bold, true) };
        }
        private IWidget ViewTable(DataTable<JsonNode> table) { return new TableViewport(table, _Localizer, () => _ActiveTable = table); }
        private DataTable<JsonNode> Table(IEnumerable<string> columns)
        {
            DataTable<JsonNode> table = new DataTable<JsonNode> { KeySelector = JsonData.Id, PageSize = 25, PagingMode = DataTablePagingMode.Remote, EmptyText = _Localizer.Text("admin.empty"), LoadingText = _Localizer.Text("common.loading"), RefreshingText = _Localizer.Text("common.loading") };
            foreach (string column in columns)
            {
                bool identifier = column is "Id" or "Guid" || column.EndsWith("Id", StringComparison.Ordinal) || column.EndsWith("Guid", StringComparison.Ordinal);
                table.Column(new DataColumn<JsonNode>(column, ColumnTitle(column), row => JsonData.Text(row, column)) { MinWidth = identifier ? 40 : column == "CreatedUtc" ? 25 : 12, CanHide = column is not ("Id" or "Guid" or "CreatedUtc") });
            }
            string preferenceKey = _Current + ":" + string.Join(",", columns);
            if (_Profile.PageSizes.TryGetValue(preferenceKey, out int pageSize) && pageSize is >= 10 and <= 1000) table.PageSize = pageSize;
            if (_Profile.Columns.TryGetValue(preferenceKey, out string[]? visible)) table.SetVisibleColumns(visible);
            table.PageRequested += (sender, args) => { _Profile.PageSizes[preferenceKey] = table.PageSize; _Store.Save(_Profile); };
            table.ColumnsChanged += (sender, args) => { _Profile.Columns[preferenceKey] = table.VisibleColumns.Select(column => column.Key).ToArray(); _Store.Save(_Profile); };
            return table;
        }
        private string ColumnTitle(string key)
        {
            string? catalog = key switch { "Id" or "Guid" => null, "name" or "Name" => "common.name", "size" => "models.colSize", "modified_at" => "models.colModified", "expires_at" => "models.colExpires", "CreatedUtc" => "requestHistory.colCreated", "Method" => "requestHistory.colMethod", "Path" => "requestHistory.colPath", "StatusCode" => "requestHistory.colStatus", "DurationMs" => "requestHistory.colDuration", "SourceIp" => "requestHistory.colSourceIp", "UserGuid" => "admin.owner", "RoleGuid" => "admin.role", _ => null };
            string title = catalog == null ? _Localizer.Phrase(key) : _Localizer.Text(catalog);
            return key is "UserGuid" or "RoleGuid" ? title + " ID" : key == "size" ? title + " (B)" : title;
        }
        private readonly Dictionary<DataTable<JsonNode>, DataTableLoader<JsonNode>> _Loaders = new Dictionary<DataTable<JsonNode>, DataTableLoader<JsonNode>>();
        private DataTable<JsonNode>? _ActiveTable;
        private void DisposeLoaders()
        {
            foreach (DataTableLoader<JsonNode> loader in _Loaders.Values) loader.Dispose();
            _Loaders.Clear(); _ActiveTable = null;
        }
        private void LoadTable(DataTable<JsonNode> table, string path, Func<JsonNode, Task>? activate = null)
        {
            if (_Loaders.Remove(table, out DataTableLoader<JsonNode>? previous)) previous.Dispose();
            long revision = _Revision;
            DataTableLoader<JsonNode> loader = new DataTableLoader<JsonNode>(table, async (request, ct) =>
            {
                using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _Route.Token);
                string query = (path.Contains('?') ? "&" : "?") + "pageNumber=" + request.PageNumber.ToString(CultureInfo.InvariantCulture) + "&pageSize=" + request.PageSize.ToString(CultureInfo.InvariantCulture);
                JsonNode? result = await _Client.JsonAsync("GET", path + query, null, linked.Token).ConfigureAwait(false);
                IReadOnlyList<JsonNode> rows = JsonData.Rows(result);
                long total = long.TryParse(JsonData.Text(result, "TotalRecords"), out long value) ? value : rows.Count;
                return new DataTablePage<JsonNode>(rows, total);
            }, action => Apply(revision, action)) { ErrorText = ErrorText };
            _Loaders[table] = loader;
            if (activate != null && previous == null) table.RowActivated += index => { if (table.SelectedRow != null) Track(activate(table.SelectedRow)); };
            _ActiveTable = table;
            _Refresh = () => Track(loader.LoadAsync(_Route.Token)); _Refresh();
        }
        private void Inspect(JsonNode value, string title)
        {
            
            DefinitionList metadata = Definitions();
            if (value is JsonObject obj) foreach (KeyValuePair<string, JsonNode?> property in obj)
                metadata.Set(SafeText.Clean(property.Key), SafeText.Clean(property.Value is JsonObject or JsonArray ? property.Value.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) : property.Value?.ToString() ?? "Unavailable"));
            Show(new PaddedScrollView(metadata, 100, Math.Max(40, (value as JsonObject)?.Count * 5 ?? 40)), title);
            _CopyText = value.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
        private void Overview()
        {
            DefinitionList values = Definitions();
            ButtonRow actions = new ButtonRow();
            foreach (string route in new[] { "/models", "/playground", "/observability", "/api-explorer", "/system-prompts", "/model-configurations" }) actions.Add(ButtonFor(RouteTitle(route), () => Navigate(route)));
            Show(new PaddedStack().Add(values, StackSize.Weighted(1), RouteTitle("/")).Add(actions, StackSize.Fixed(5)), RouteTitle("/"));
            long revision = _Revision;
            Dictionary<string, string> endpoints = new Dictionary<string, string> { ["home.modelsOnDisk"] = "/api/tags", ["home.residentModels"] = "/api/ps", ["home.requests"] = "/v1.0/api/request-history/summary", ["library.promptsTitle"] = "/v1.0/system-prompts?pageSize=1", ["library.configurationsTitle"] = "/v1.0/model-configurations?pageSize=1" };
            _Refresh = () =>
            {
                foreach (KeyValuePair<string, string> endpoint in endpoints)
                    Work(async ct =>
                    {
                        string result;
                        try { JsonNode? node = await _Client.JsonAsync("GET", endpoint.Value, null, ct).ConfigureAwait(false); result = endpoint.Value.StartsWith("/api/", StringComparison.Ordinal) ? JsonData.Rows(node).Count.ToString(_Localizer.Culture) : JsonData.Text(node, "TotalRecords", "TotalCount"); }
                        catch (Exception error) when (IsExpected(error)) { result = "Unavailable: " + ErrorText(error); }
                        Apply(revision, () => values.Set(_Localizer.Text(endpoint.Key), result));
                    });
            }; _Refresh();
        }
        private string Scoped(string path)
        {
            string? tenant = _Profile.Tenant ?? (string.IsNullOrEmpty(JsonData.Text(_Context, "tenantId")) ? null : JsonData.Text(_Context, "tenantId"));
            return tenant == null ? path : path + (path.Contains('?') ? "&" : "?") + "tenantId=" + ServerClient.Segment(tenant);
        }
        private void Settings()
        {
            ShowText("Settings", _Localizer.Text("common.loading")); _Refresh = () => Navigate("/settings"); long revision = _Revision;
            Work(async ct =>
            {
                JsonObject settings = (await _Client.JsonAsync("GET", "/api/settings", null, ct).ConfigureAwait(false)) as JsonObject ?? throw new InvalidOperationException("Settings must be an object.");
                JsonNode? health = await _Client.JsonAsync("GET", "/health", null, ct).ConfigureAwait(false);
                Apply(revision, () =>
                {
                    Edit(settings, "Settings", draft => Mutate(async saveToken =>
                    {
                        await _Client.JsonAsync("PUT", "/api/settings", draft, saveToken).ConfigureAwait(false);
                        Apply(revision, () => { _Form?.MarkClean(); _Host.Notify(_Localizer.Phrase("Saved. Some changes require a server restart.")); });
                    }));
                    _Form!.Form.Add("", ButtonFor("Reload", () => Navigate("/settings")));
                    DefinitionList information = Definitions();
                    foreach (string key in new[] { "version", "backend", "native_initialized" }) information.Set(_Localizer.Phrase(key), JsonData.Text(health, key));
                    Show(new PaddedStack().Add(information, StackSize.Fixed(3), _Localizer.Text("settings.serverInfo")).Add(_Form.View, StackSize.Weighted(1)), _Localizer.Text("settings.title"));
                });
            });
        }
    }
}
