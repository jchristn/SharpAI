namespace SharpAI.Tui
{
    using System;
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;
    using SharpAI.Tui.Services;
    using SharpAI.Tui.Widgets;
    using SharpAI.Client;
    using TUIKit.Layout;
    using TUIKit.Widgets;

    internal sealed partial class SharpAITuiApp
    {
        private void Models()
        {
            DataTable<JsonNode> registered = Table(new[] { "name", "size", "modified_at" });
            DataTable<JsonNode> resident = Table(new[] { "name", "size", "expires_at" });
            LoadTable(registered, "/api/tags", row => { Inspect(row, "Registered model"); return Task.CompletedTask; });
            Action? reloadTags = _Refresh;
            LoadTable(resident, "/api/ps", row => { Inspect(row, "Resident model"); return Task.CompletedTask; });
            Action? reloadResident = _Refresh;
            _Refresh = () => { reloadTags?.Invoke(); reloadResident?.Invoke(); };
            ButtonRow actions = new ButtonRow();
            actions.Add(ButtonFor("Pull", () => Edit(new JsonObject { ["Model"] = "" }, "Pull model", draft => Pull(draft["Model"]!.ToString()))));
            actions.Add(ButtonFor("Delete selected local model", () => MutateModel(registered.SelectedRow, "/api/delete")));
            actions.Add(ButtonFor("Unload selected resident model", () => MutateModel(resident.SelectedRow, "/api/unload")));
            Show(new PaddedStack().Add(ViewTable(registered), StackSize.Weighted(1), "Registered").Add(ViewTable(resident), StackSize.Weighted(1), "Resident").Add(actions, StackSize.Fixed(4)), "Models");
        }
        private void MutateModel(JsonNode? row, string path)
        {
            if (row == null) return;
            string model = JsonData.Text(row, "name", "model");
            Track(ConfirmModelAsync(model, path));
        }
        private async Task ConfirmModelAsync(string model, string path)
        {
            if (!await _Host.ConfirmAsync(path + " " + model + "?").ConfigureAwait(false)) return;
            _Host.Post(() => Work(async ct => { await _Client.JsonAsync(path == "/api/delete" ? "DELETE" : "POST", path, new JsonObject { ["model"] = model }, ct).ConfigureAwait(false); _Host.Post(() => OpenRoute("/models")); }));
        }
        private void Pull(string model)
        {
            _Form = null;
            if (string.IsNullOrWhiteSpace(model)) { _Host.Notify(_Localizer.Phrase("Enter a repository name or full HTTP(S) model URL.")); return; }
            if (model.Contains("://", StringComparison.Ordinal) && (!Uri.TryCreate(model, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https"))) { _Host.Notify(_Localizer.Phrase("Invalid model URL.")); return; }
            ThemedProgressBar progress = new ThemedProgressBar(_Localizer.Culture, _Profile.Ascii); DefinitionList state = Definitions();
            ButtonRow actions = new ButtonRow(); actions.Add(ButtonFor("Cancel", CancelActive));
            Show(new PaddedStack().Add(state, StackSize.Weighted(1)).Add(progress, StackSize.Fixed(3)).Add(actions, StackSize.Fixed(3)), "Pull " + model);
            long revision = _Revision; _Busy = true;
            Work(async ct =>
            {
                bool success = false;
                await foreach (PullProgress frame in _Client.PullAsync(model, ct).ConfigureAwait(false))
                {
                    success = frame.Status.Equals("success", StringComparison.OrdinalIgnoreCase);
                    Apply(revision, () =>
                    {
                        state.Set("Status", frame.Status);
                        progress.Value = Math.Clamp((double)(frame.Percent ?? 0), 0, 1);
                        state.Set("Downloaded bytes", frame.Downloaded?.ToString(_Localizer.Culture) ?? "Unavailable");
                    });
                }
                if (!success) throw new System.IO.InvalidDataException("Download ended without a success marker. Inspect the model inventory before retrying.");
                Apply(revision, () => { _Busy = false; state.Set("Status", "Complete"); });
            });
        }
    }
}
