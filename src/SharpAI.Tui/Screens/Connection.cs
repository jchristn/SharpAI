namespace SharpAI.Tui
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;
    using SharpAI.Client;
    using SharpAI.Tui.Services;
    using SharpAI.Tui.Widgets;
    using TUIKit.Widgets;
    using TUIKit.Modals;

    internal sealed partial class SharpAITuiApp
    {
        private void Login()
        {
            Edit(new JsonObject { ["Email"] = "", ["Password"] = "", ["Tenant"] = "" }, "Sign in", draft =>
            {
                long revision = _Revision;
                Mutate(async ct =>
                {
                    Dictionary<string, string> headers = new Dictionary<string, string> { ["x-email"] = draft["Email"]!.ToString(), ["x-password"] = draft["Password"]!.ToString() };
                    if (!string.IsNullOrEmpty(draft["Tenant"]?.ToString())) headers["x-tenant-guid"] = draft["Tenant"]!.ToString();
                    JsonNode? login = await _Client.JsonAsync("POST", "/v1.0/token", null, ct, headers).ConfigureAwait(false);
                    string token = JsonData.Text(login, "Token", "token");
                    Apply(revision, () => { _Client.Token = token; ClearIdentity(); Track(ConnectAsync()); });
                });
            });
        }
        private void TokenLogin()
        {
            Edit(new JsonObject { ["Token"] = "" }, "Use bearer token", draft =>
            {
                _Client.Token = draft["Token"]!.ToString(); ClearIdentity(); Track(ConnectAsync());
            });
        }
        private async Task LogoutAsync()
        {
            string? token = _Client.Token;
            _Client.Token = null; ClearIdentity(); Status("Signed out");
            if (string.IsNullOrEmpty(token) || token.StartsWith("sharpai_", StringComparison.Ordinal)) return;
            using ServerClient logout = new ServerClient(_Profile.Server) { Token = token };
            await logout.JsonAsync("DELETE", "/v1.0/token", null, _Lifetime.Token).ConfigureAwait(false);
        }
        private void ClearIdentity()
        {
            CloseModals(); DisposeLoaders(); _ScreenTimer?.Dispose(); _ScreenTimer = null; _Route.Cancel(); _Revision++; _Context = null; _Profile.Tenant = null; _Form = null; _Busy = false;
            _Turns.Clear(); _Messages.Clear(); _ContextSummary = string.Empty; _ComposerDraft = string.Empty; _LastTurnRecord = null; _Transcript = string.Empty; _PlaygroundInitialized = false; _HistoryFilters.Clear(); _CopyText = null; _SelectedPrompt = null; _SelectedConfiguration = null;
            ShowText("Session", "Revalidating identity and permissions.");
        }
        private void CloseModals()
        {
            while (_Host.Modals.Top is Modal modal)
            {
                if (modal is SecretModal secret) secret.Clear();
                if (!modal.RequestClose(null)) break;
                _Host.Modals.RemoveClosed();
            }
        }
        private async Task SelectTenantAsync()
        {
            if (!JsonData.Flag(_Context, "isAdmin")) { _Host.Notify(_Localizer.Phrase("Tenant selection requires a platform administrator.")); return; }
            Pick("/v1.0/tenants", "Select tenant", tenant =>
            {
                _Profile.Tenant = JsonData.Id(tenant); _Store.Save(_Profile); _Turns.Clear(); _Messages.Clear(); _ContextSummary = string.Empty; _ComposerDraft = string.Empty; _LastTurnRecord = null; _Transcript = string.Empty; _PlaygroundInitialized = false; _HistoryFilters.Clear(); _CopyText = null; _SelectedPrompt = null; _SelectedConfiguration = null; OpenRoute(_Current); Status("Tenant selected");
            });
            await Task.CompletedTask.ConfigureAwait(false);
        }

        private void EditProfile()
        {
            JsonObject value = new JsonObject { ["Server"] = _Profile.Server, ["Locale"] = _Profile.Locale, ["Theme"] = _Profile.Theme, ["Ascii"] = _Profile.Ascii, ["EngineMetrics"] = _Profile.EngineMetrics, ["TokenFile"] = _Profile.TokenFile, ["TelemetryEnabled"] = _Profile.TelemetryEnabled, ["OtlpEndpoint"] = _Profile.OtlpEndpoint, ["ServiceLinks"] = System.Text.Json.JsonSerializer.SerializeToNode(_Profile.ServiceLinks) };
            Edit(value, "Connection profile", draft =>
            {
                string server = draft["Server"]!.ToString();
                using ServerClient validation = new ServerClient(server);
                _Profile.ServiceLinks = draft["ServiceLinks"]?.Deserialize<Dictionary<string, string>>() ?? new Dictionary<string, string>();
                _Profile.Ascii = JsonData.Flag(draft, "Ascii"); _Profile.TokenFile = draft["TokenFile"]?.ToString(); _Profile.TelemetryEnabled = JsonData.Flag(draft, "TelemetryEnabled"); _Profile.OtlpEndpoint = draft["OtlpEndpoint"]?.ToString(); _Profile.Server = server; _Profile.Locale = draft["Locale"]!.ToString(); _Profile.Theme = draft["Theme"]!.ToString(); _Profile.EngineMetrics = draft["EngineMetrics"]?.ToString();
                _Store.Save(_Profile); _Form = null;
                ShowText("Connection profile", "Saved. Restart sharpai-tui to use the selected server, language and theme. Sessions are held in memory.");
            });
        }
        private void Edit(JsonObject value, string title, Action<JsonObject> save, string[]? scopes = null)
        {
            _Form = new JsonForm(value, save, () => Navigate(_Current), scopes, _Localizer);
            Show(_Form.View, title + " | Ctrl+S save, Esc cancel");
        }
    }
}
