namespace SharpAI.Tui
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using SharpAI.Client;
    using SharpAI.Tui.Services;
    using SharpAI.Tui.Widgets;
    using TUIKit;
    using TUIKit.Layout;
    using TUIKit.Widgets;

    internal sealed partial class SharpAITuiApp
    {
        private void Explorer()
        {
            ListView<JsonNode> operations = OperationPicker();
            Show(operations, "API Explorer | Select an operation");
        }
        private ListView<JsonNode> OperationPicker()
        {
            ListView<JsonNode> operations = new ListView<JsonNode>(row => JsonData.Text(row, "method") + " " + JsonData.Text(row, "path") + " " + JsonData.Text(row, "summary"));
            operations.SetItems(_Operations.Select(item => item!).ToList());
            operations.ItemActivated += index => { if (_Busy) { _Host.Notify(_Localizer.Phrase("Cancel active work first.")); return; } ResetGenerationScope(); ExplorerOperation(operations.SelectedItem!); };
            return operations;
        }
        private void ExplorerOperation(JsonNode operation)
        {
            string method = JsonData.Text(operation, "method"); string template = JsonData.Text(operation, "path");
            Form parameters = new Form { LabelPlacement = FormLabelPlacement.Left };
            Dictionary<string, TextField> placeholders = new Dictionary<string, TextField>();
            foreach (Match match in Regex.Matches(template, @"\{([^}]+)\}"))
            {
                string key = match.Groups[1].Value; if (placeholders.ContainsKey(key)) continue;
                TextField input = new TextField { Value = key.StartsWith("tenant", StringComparison.OrdinalIgnoreCase) ? _Profile.Tenant ?? JsonData.Text(_Context, "tenantId") : string.Empty };
                parameters.Add(key, input, () => string.IsNullOrWhiteSpace(input.Value) ? "A path value is required." : null); placeholders[key] = input;
            }
            TextField query = new TextField { MaxLength = 8192, Placeholder = "key=value&other=value (URL-encoded query)" }; parameters.Add("Query", query);
            TextEditor body = new TextEditor { ConsumeUnboundControlKeys = false, Text = operation["example"]?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "", WordWrap = true };
            body.TextChanged += (sender, args) => _ScreenDraft = true; query.ValueChanged += (sender, args) => _ScreenDraft = true;
            TextEditor response = new TextEditor { ConsumeUnboundControlKeys = false, IsReadOnly = true, WordWrap = true };
            ButtonRow actions = new ButtonRow();
            actions.Add(ButtonFor("Execute", () =>
            {
                if (_Busy) { _Host.Notify(_Localizer.Phrase("One operation is already active.")); return; }
                if (parameters.ValidateAll().Count > 0) return;
                string target = Regex.Replace(template, @"\{([^}]+)\}", match => ServerClient.Segment(placeholders[match.Groups[1].Value].Value));
                if (query.Value.Length > 0) target += (target.Contains('?') ? "&" : "?") + query.Value.TrimStart('?');
                JsonNode? payload;
                try { payload = string.IsNullOrWhiteSpace(body.Text) ? null : JsonNode.Parse(body.Text); }
                catch (JsonException error) { _Host.Notify(ErrorText(error)); return; }
                long revision = _Revision;
                _Busy = true;
                Work(async ct =>
                {
                    if (method != "GET" && method != "HEAD" && !await ConfirmOnUiAsync(method + " " + target + "? This may change server state.", ct).ConfigureAwait(false)) { Apply(revision, () => _Busy = false); return; }
                    Apply(revision, () => { _Busy = true; _ScreenDraft = false; });
                    if (payload is JsonObject obj && JsonData.Flag(obj, "stream"))
                    {
                        int frames = 0; string content = string.Empty;
                        await foreach (JsonNode frame in _Client.StreamAsync(target, payload, ct).ConfigureAwait(false))
                        {
                            content += frame.ToJsonString() + "\n"; frames++;
                            if (content.Length > 262144) content = content.Substring(content.Length - 262144);
                            string snapshot = content; Apply(revision, () => response.Text = SafeText.Clean(snapshot));
                        }
                        Apply(revision, () => { _Busy = false; _CopyText = content; Status("Stream complete: " + frames + " frames"); });
                    }
                    else
                    {
                        ApiResponse result = await _Client.ExecuteAsync(method, target, payload, ct).ConfigureAwait(false);
                        Apply(revision, () =>
                        {
                            _Busy = false;
                            if (target.Contains("/credentials", StringComparison.Ordinal) && result.Headers.Any(header => header.Key.Equals("Cache-Control", StringComparison.OrdinalIgnoreCase) && header.Value.Contains("no-store", StringComparison.OrdinalIgnoreCase)) && (result.Json() is JsonNode secret) && !string.IsNullOrEmpty(JsonData.Text(secret, "token", "Token")))
                            { response.Text = "Credential token is shown in a private modal."; Track(ShowSecretAsync(secret)); return; }
                            response.Text = "HTTP " + (int)result.StatusCode + "\n" + string.Join("\n", result.Headers.Select(entry => entry.Key + ": " + SafeText.Clean(entry.Value))) + "\n\n" + SafeText.Clean(result.Body) + (result.Truncated ? "\n[TRUNCATED]" : ""); _CopyText = result.Body;
                        });
                    }
                });
            }));
            actions.Add(ButtonFor("Copy request", () =>
            {
                string target = Regex.Replace(template, @"\{([^}]+)\}", match => ServerClient.Segment(placeholders[match.Groups[1].Value].Value));
                _CopyText = method + " " + _Profile.Server.TrimEnd('/') + target + (query.Value.Length == 0 ? "" : "?" + query.Value) + "\nContent-Type: application/json\n\n" + body.Text; Track(CopyAsync());
            }));
            actions.Add(ButtonFor("Copy response", () => { _CopyText = response.Text; Track(CopyAsync()); }));
            actions.Add(ButtonFor("Cancel", CancelActive));
            IWidget operationPicker = OperationPicker();
            IWidget[] panes = { operationPicker, parameters, body, response };
            FramedStack selected = new FramedStack().Add(panes[1], StackSize.Weighted(1));
            TabStrip tabs = new TabStrip().Add("Operations").Add("Parameters").Add("Request").Add("Response");
            tabs.ActiveIndex = 1;
            tabs.ActiveTabChanged += (sender, args) => { foreach (IWidget child in selected.Children.ToArray()) selected.Remove(child); selected.Add(panes[tabs.ActiveIndex], StackSize.Weighted(1)); };
            FramedStack wide = new FramedStack().Add(operationPicker, StackSize.Fixed(5), "Operations").Add(parameters, StackSize.Fixed(Math.Min(8, 2 + placeholders.Count * 2)), method + " " + template).Add(body, StackSize.Weighted(1), "Request JSON").Add(actions, StackSize.Fixed(3)).Add(response, StackSize.Weighted(2), "Response");
            FramedStack narrow = new FramedStack().Add(tabs, StackSize.Fixed(1)).Add(selected, StackSize.Weighted(1)).Add(actions, StackSize.Fixed(3));
            Show(new ResponsiveScreen(wide, narrow), "API Explorer | " + method + " " + template);
        }
    }
}
