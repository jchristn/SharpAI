namespace SharpAI.Tui
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Client;
    using SharpAI.Tui.Localization;
    using SharpAI.Tui.Services;
    using SharpAI.Tui.Shell;
    using SharpAI.Tui.Widgets;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Modals;
    using TUIKit.Terminal;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    internal sealed partial class SharpAITuiApp : IDisposable
    {
        private readonly ServerClient _Client;
        private readonly ClientTelemetry? _Telemetry;
        private readonly TimeProvider _Clock;
        private readonly TuiApplication _Host;
        private readonly ConnectionProfile _Profile;
        private readonly ProfileStore _Store;
        private readonly CancellationTokenSource _Lifetime = new CancellationTokenSource();
        private CancellationTokenSource _Route = new CancellationTokenSource();
        private readonly Localizer _Localizer;
        private readonly ListView<string> _Navigation;
        private readonly AdaptiveShell _Shell;
        private readonly Pane _Header = new Pane("header");
        private CommandRegistry _Commands = new CommandRegistry();
        private IReadOnlyList<Command> _GlobalCommands = Array.Empty<Command>();
        private readonly List<Command> _PendingCommands = new List<Command>();
        private readonly JsonArray _Operations;
        private long _Revision;
        private string _Current = "/";
        private JsonNode? _Context;
        private int? _HealthStatus;
        private int? _ReadyStatus;
        private JsonForm? _Form;
        private Action? _Refresh;
        private bool _Busy;
        private bool _ScreenDraft;
        private bool _Disposed;
        private Exception? _FatalError;
        private string? _CopyText;
        private readonly IClipboard _Clipboard;
        private readonly HashSet<Task> _Tasks = new HashSet<Task>();
        private readonly string[] _Routes = { "/", "/models", "/system-prompts", "/model-configurations", "/playground", "/request-history", "/request-history/inference", "/request-history/embeddings", "/api-explorer", "/observability", "/access/tenants", "/access/users", "/access/credentials", "/access/roles", "/access/permissions", "/access/assignments", "/access/sessions", "/access/audit", "/settings" };
        internal TuiApplication Host { get { return _Host; } }
        internal SharpAITuiApp(ServerClient client, ITerminalBackend backend, ConnectionProfile profile, ProfileStore store, ClientTelemetry? telemetry = null, TimeProvider? clock = null, IClipboard? clipboard = null)
        {
            _Client = client; _Profile = profile; _Store = store; _Telemetry = telemetry; _Clock = clock ?? TimeProvider.System; _Clipboard = clipboard ?? new ClipboardService();
            _Localizer = new Localizer(profile.Locale); _Client.Locale = profile.Locale == "qps-ploc" ? "en" : profile.Locale;
            _Operations = (JsonArray)ReadResource("operations.json");
            _Host = new TuiApplication(backend) { ApplyThemeToWidgets = true, IdleFps = 2, TargetFps = 30, CtrlCPolicy = CtrlCPolicy.InterruptFocusedPane, RoutePasteToFocus = true, HighlightFocusedRegion = true };
            _Host.Theme = TuiTheme.Create(profile.Theme, profile.Ascii);
            List<string> navigationItems = new List<string>();
            foreach (string route in _Routes)
            {
                navigationItems.Add(route);
                if (route is "/" or "/playground" or "/observability") navigationItems.Add(string.Empty);
            }
            _Navigation = new ListView<string>(route => route.Length == 0 ? string.Empty : RouteTitle(route)); _Navigation.SetItems(navigationItems);
            _Navigation.SelectionChanged += (sender, change) =>
            {
                int previous = change.OldValue; int selected = change.NewValue;
                if (selected >= 0 && navigationItems[selected].Length == 0) _Navigation.Select(selected + (selected > previous ? 1 : -1));
            };
            _Navigation.ItemActivated += index => { if (navigationItems[index].Length > 0) Navigate(navigationItems[index]); };
            _Shell = new AdaptiveShell(_Navigation);
            _Host.Layout = Layout.Create().DockTop("header", 1).DockBottom("hints", 1).Fill("body").Build();
            _Host.BindPane("header", _Header); _Host.Bind("body", _Shell);
            StatusBar hints = new StatusBar { KeyStyle = _Host.Theme.Accent.WithAttribute(CellAttributes.Bold, true), LabelStyle = _Host.Theme.Muted, RightTextStyle = _Host.Theme.Muted }; _Host.Bind("hints", hints);
            AddCommand("help", "Help", () => Track(ContextHelpAsync()), "F1");
            AddCommand("chat.model", "Select a model", () => Pick("/api/tags", "Model", row => { ResetGenerationScope(); _SelectedModel = JsonData.Text(row, "name", "model"); Playground(); }), isEnabled: () => _Current == "/playground");
            AddCommand("chat.prompt", "System prompt", () => Pick(Scoped("/v1.0/system-prompts"), "System prompt", row => { _SelectedPrompt = row.DeepClone(); Playground(); }), isEnabled: () => _Current == "/playground");
            AddCommand("chat.configuration", "Model configuration", () => Pick(Scoped("/v1.0/model-configurations"), "Model configuration", row => { _SelectedConfiguration = row.DeepClone(); Playground(); }), isEnabled: () => _Current == "/playground");
            AddCommand("chat.telemetry", "Turn details", TurnHistory, isEnabled: () => _Current == "/playground" && _Turns.Count > 0);
            AddCommand("repository", "Copy repository URL", () => { _CopyText = "https://github.com/jchristn/SharpAI"; Track(CopyAsync()); });
            AddCommand("documentation", "Copy documentation URL", () => { _CopyText = "https://github.com/jchristn/SharpAI/blob/main/DEPLOYMENT_GUIDE.md"; Track(CopyAsync()); });
            AddCommand("language", "Language", () => Track(ChangeLanguageAsync()));
            AddCommand("navigation", "Navigation", _Shell.ToggleNavigation, "F2");
            AddCommand("palette", "Commands", () => Track(PaletteAsync()), "ctrl+p");
            AddCommand("refresh", "Refresh", () => _Refresh?.Invoke(), "F5");
            AddCommand("login", "Sign in", () => OpenUtility(Login));
            AddCommand("token", "Use bearer token", () => OpenUtility(TokenLogin));
            AddCommand("logout", "Sign out", () => Track(LogoutAsync()));
            AddCommand("copy", "Copy current result", () => Track(CopyAsync()), "ctrl+y");
            AddCommand("copy.id", "Copy selected ID", () => { if (_ActiveTable?.SelectedRow != null) { _CopyText = JsonData.Id(_ActiveTable.SelectedRow); Track(CopyAsync()); } });
            AddCommand("page.size", "Cycle table page size", () => _ActiveTable?.CyclePageSize());
            AddCommand("columns", "Choose table columns", () => Track(ChooseColumnsAsync()));
            AddCommand("identity", "Identity and connection details", () => ReadDetails("/v1.0/auth/context", "Identity and permissions"));
            AddCommand("scope", "Select tenant", () => Track(SelectTenantAsync()));
            AddCommand("profile", "Connection profile", () => OpenUtility(EditProfile));
            AddCommand("cancel", "Cancel active operation", CancelActive, "ctrl+x");
            AddCommand("quit", "Quit", () => Track(QuitAsync()), "ctrl+q");
            foreach (string route in _Routes) AddCommand("route." + route, RouteTitle(route), () => Navigate(route));
            for (int index = 0; index < _Operations.Count; index++)
            {
                JsonNode operation = _Operations[index]!;
                AddCommand("explorer." + index, JsonData.Text(operation, "method") + " " + JsonData.Text(operation, "path"), () => { if (_Busy) return; OpenRoute("/api-explorer"); ExplorerOperation(operation); });
            }
            _GlobalCommands = _Commands.Commands.ToArray();
            _Commands.ApplyTo(_Host);
            KeyHintResolver resolver = new KeyHintResolver(); resolver.AddCommands(_Commands);
            hints.HintSource = () => resolver.Resolve(_Host.CurrentFocusPath).Select(hint => new KeyHint(hint.Key, _Localizer.Phrase(hint.Description), hint.Priority)).ToList();
            _Host.FocusPathChanged += path => { if (path.Leaf is DataTable<JsonNode> table) _ActiveTable = table; };
            _Host.Interrupted += CancelActive;
            _Client.AuthorizationFailed += AuthorizationFailed;
            Status("Connecting");
        }
        private void AddCommand(string id, string title, Action handler, string? chord = null, Func<bool>? isEnabled = null)
        {
            _Commands.Add(new Command(id, _Localizer.Phrase(title), handler, chord: chord == null ? null : KeyChord.Parse(chord), isEnabled: isEnabled));
        }
        private Button ButtonFor(string label, Action action)
        {
            string key = label.StartsWith("Copy", StringComparison.Ordinal) ? "help.copy" : label.Contains("Delete", StringComparison.Ordinal) || label.Contains("Remove", StringComparison.Ordinal) || label == "Revoke" ? "help.delete" : label.Contains("Edit", StringComparison.Ordinal) ? "library.editSelectionHelp" : label == "Pull" ? "models.pullHelp" : label == "Send" ? "help.prompt" : label == "Cancel" ? "help.stopGeneration" : _Current.StartsWith("/access/", StringComparison.Ordinal) ? "admin.help." + _Current.Split('/').Last() : "help.readonly";
            Command command = new Command("action." + _Current + "." + _PendingCommands.Count, _Localizer.Phrase(label), action, category: RouteTitle(_Current));
            _PendingCommands.Add(command);
            return new Button(command.Title, command.Handler) { TooltipText = _Localizer.Text(key) };
        }
        private string RouteTitle(string route)
        {
            string key = route switch { "/" => "nav.overview", "/models" => "nav.models", "/system-prompts" => "library.promptsTitle", "/model-configurations" => "library.configurationsTitle", "/playground" => "nav.playground", "/request-history" => "nav.requestHistory", "/api-explorer" => "nav.apiExplorer", "/observability" => "nav.observability", "/settings" => "nav.settings", _ => route.StartsWith("/access/", StringComparison.Ordinal) ? "admin." + route.Split('/').Last() : "history." + route.Split('/').Last() };
            return _Localizer.Text(key);
        }
        internal async Task RunAsync(CancellationToken cancellationToken)
        {
            Track(ConnectAsync());
            try { await _Host.RunAsync(cancellationToken).ConfigureAwait(false); }
            finally
            {
                _Lifetime.Cancel(); _Route.Cancel(); _ScreenTimer?.Dispose(); CloseModals();
                try { await Task.WhenAll(_Tasks.ToArray()).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
                catch (TimeoutException error) { throw new InvalidOperationException("The terminal was restored, but background work did not stop within the shutdown timeout.", error); }
            }
            if (_FatalError != null) throw new InvalidOperationException("Unexpected terminal failure. See the local lifecycle log for its error type.", _FatalError);
        }
        private void AuthorizationFailed(System.Net.HttpStatusCode status)
        {
            _Host.Post(() =>
            {
                if (_Disposed) return;
                if (status == System.Net.HttpStatusCode.Unauthorized) { _Client.Token = null; ClearIdentity(); Status("Session expired or revoked. Sign in again."); }
                else Track(RevalidateAsync());
            });
        }
        private async Task RevalidateAsync()
        {
            long revision = _Revision;
            JsonNode? context = await _Client.AuthContextAsync(_Lifetime.Token).ConfigureAwait(false);
            Apply(revision, () => { _Context = context; Status("Permissions revalidated"); });
        }
        internal async Task ConnectAsync()
        {
            long revision = _Revision;
            try
            {
                ApiResponse health = await _Client.ExecuteAsync("GET", "/health", null, _Lifetime.Token).ConfigureAwait(false);
                ApiResponse ready = await _Client.ExecuteAsync("GET", "/ready", null, _Lifetime.Token).ConfigureAwait(false);
                Apply(revision, () => { _HealthStatus = (int)health.StatusCode; _ReadyStatus = (int)ready.StatusCode; });
                JsonNode? context = await _Client.JsonAsync("GET", "/v1.0/auth/context", null, _Lifetime.Token).ConfigureAwait(false);
                Apply(revision, () => { _Context = context; Status(JsonData.Flag(_Context, "authEnabled") ? "Connected" : "Authentication disabled"); Navigate("/"); });
            }
            catch (ApiException error) when (error.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Apply(revision, () => { Status("Sign in"); Login(); });
            }
            catch (Exception error) when (IsExpected(error)) { Apply(revision, () => { ShowText("Connection", ErrorText(error)); Status("Disconnected"); }); }
        }
        internal void Navigate(string route)
        {
            if (_Form?.IsDirty == true || _ScreenDraft || _Busy) { Track(NavigateConfirmedAsync(route)); return; }
            OpenRoute(route);
        }
        private async Task NavigateConfirmedAsync(string route)
        {
            bool confirmed = await _Host.ConfirmAsync("Discard draft or cancel active work?").ConfigureAwait(false);
            if (confirmed) _Host.Post(() => OpenRoute(route));
        }
        private void OpenRoute(string route)
        {
            _ScreenTimer?.Dispose(); _ScreenTimer = null; DisposeLoaders(); _Route.Cancel(); _Route.Dispose(); _Route = CancellationTokenSource.CreateLinkedTokenSource(_Lifetime.Token);
            _PendingCommands.Clear(); _Telemetry?.Screen(route); _Revision++; _Current = route; _Form = null; _Refresh = null; _Busy = false; _ScreenDraft = false; _CopyText = null;
            if (route == "/") Overview();
            else if (route == "/models") Models();
            else if (route is "/system-prompts" or "/model-configurations") Library(route.Substring(1));
            else if (route == "/playground") Playground();
            else if (route.StartsWith("/request-history", StringComparison.Ordinal)) History(route.Split('/').Last());
            else if (route == "/api-explorer") Explorer();
            else if (route == "/observability") Observability();
            else if (route == "/settings") Settings();
            else if (route.StartsWith("/access/", StringComparison.Ordinal)) Access(route.Split('/').Last());
            _Host.Focus("body");
        }
        private void Show(IWidget widget, string title)
        {
            _Commands = new CommandRegistry();
            foreach (Command command in _GlobalCommands) _Commands.Add(command);
            foreach (Command command in _PendingCommands) _Commands.Add(command);
            _PendingCommands.Clear(); _CopyText = null;
            _Shell.SetContent(widget, _Localizer.Phrase(title)); _Host.RequestRender();
        }
        internal bool HasCommand(string id) { return _Commands.Commands.Any(command => command.Id == id); }
        private void ShowText(string title, string text)
        {
            TextEditor viewer = new TextEditor { ConsumeUnboundControlKeys = false, Text = SafeText.Clean(text), IsReadOnly = true, WordWrap = true };
            Show(viewer, title);
        }
        private void Status(string message)
        {
            string badge = _Context != null && !JsonData.Flag(_Context, "authEnabled") ? "AUTH OFF" : JsonData.Flag(_Context, "isAdmin") ? "ADMIN" : JsonData.Flag(_Context, "isTenantAdmin") ? "TENANT ADMIN" : "USER";
            Uri server = new Uri(_Profile.Server);
            string tenant = _Profile.Tenant ?? JsonData.Text(_Context, "tenantId");
            string identity = JsonData.Text(_Context, "userId");
            _Header.Clear();
            _Header.Write(SafeText.Clean((server.Scheme == "https" ? "TLS " : "HTTP ") + Compact(server.Authority, 24) + " | H" + (_HealthStatus?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?") + "/R" + (_ReadyStatus?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?") + " | " + badge + " | t:" + Compact(tenant, 12) + " | u:" + Compact(identity, 12) + " | " + _Localizer.Phrase(message)));
        }
        private static string Compact(string text, int length) { return text.Length <= length ? text : text.Substring(0, 4) + ".." + text.Substring(text.Length - length + 6); }
        internal async Task DrainAsync(CancellationToken cancellationToken)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(20);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Terminal workflows did not become idle. A modal or background task remains active.");
                _Host.PumpInputOnce(); _Host.RenderOnce();
                if (!_Tasks.Any(task => !task.IsCompleted))
                {
                    _Host.PumpInputOnce(); _Host.RenderOnce();
                    if (!_Tasks.Any(task => !task.IsCompleted)) return;
                }
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }
        }

        private void Track(Task task)
        {
            _Tasks.RemoveWhere(item => item.IsCompleted); _Tasks.Add(ObserveAsync(task));
        }
        private async Task ObserveAsync(Task task)
        {
            try { await task.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (Exception error) when (IsExpected(error)) { _Telemetry?.Failure(error); _Host.Post(() => { if (!_Disposed) _Host.Notify(ErrorText(error), NotificationSeverity.Error); }); }
            catch (Exception error)
            {
                _Telemetry?.Failure(error); _Host.Post(() => { _FatalError = error; _Lifetime.Cancel(); _Host.Quit(); });
            }
        }
        private Task<bool> ConfirmOnUiAsync(string message, CancellationToken cancellationToken)
        {
            TaskCompletionSource<bool> source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _Host.Post(() => Track(CompleteConfirmationAsync(message, source, cancellationToken)));
            return source.Task.WaitAsync(cancellationToken);
        }
        private async Task CompleteConfirmationAsync(string message, TaskCompletionSource<bool> source, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            source.TrySetResult(await _Host.ConfirmAsync(message).ConfigureAwait(false));
        }
        private void OpenUtility(Action open)
        {
            if (_Busy) { _Host.Notify(_Localizer.Phrase("Cancel the active generation first.")); return; }
            if (_Form?.IsDirty == true || _ScreenDraft) { Track(ConfirmUtilityAsync(open)); return; }
            _Form = null; _ScreenDraft = false; ResetGenerationScope(); open();
        }
        private async Task ConfirmUtilityAsync(Action open)
        {
            if (!await _Host.ConfirmAsync("Discard the current draft?").ConfigureAwait(false)) return;
            _Host.Post(() => { _Form = null; _ScreenDraft = false; ResetGenerationScope(); open(); });
        }
        private void CancelActive()
        {
            _Route.Cancel(); Status("Cancelled. A mutation may have reached the server; inspect history before retrying.");
            long revision = _Revision; Task[] pending = _Tasks.ToArray();
            Track(RearmRouteAsync(pending, revision));
        }
        private async Task RearmRouteAsync(Task[] pending, long revision)
        {
            await Task.WhenAll(pending).ConfigureAwait(false);
            Apply(revision, () => { _Route.Dispose(); _Route = CancellationTokenSource.CreateLinkedTokenSource(_Lifetime.Token); _Busy = false; });
        }
        private void Mutate(Func<CancellationToken, Task> work)
        {
            if (_Busy) { _Host.Notify(_Localizer.Phrase("An operation is already active. Cancel it before starting another.")); return; }
            _Busy = true; long revision = _Revision; JsonForm? form = _Form; form?.SetEnabled(false);
            Work(async ct => { try { await work(ct).ConfigureAwait(false); } finally { Apply(revision, () => { form?.SetEnabled(true); _Busy = false; }); } });
        }
        private void Work(Func<CancellationToken, Task> work)
        {
            long revision = _Revision; CancellationToken token = _Route.Token;
            Track(Task.Run(async () =>
            {
                try { await work(token).ConfigureAwait(false); }
                catch (OperationCanceledException) { Apply(revision, () => { _Busy = false; Status("Cancelled. A mutation may have reached the server; inspect history before retrying."); }); }
                catch (Exception error) when (IsExpected(error)) { _Host.Post(() => { if (revision == _Revision) { _Busy = false; _Telemetry?.Failure(error); _Host.Notify(ErrorText(error), NotificationSeverity.Error); Status("Operation failed. Inspect history before retrying a mutation."); } }); }
            }, token));
        }
        private static bool IsExpected(Exception error) { return error is HttpRequestException or ApiException or IOException or InvalidDataException or JsonException or ArgumentException or InvalidOperationException or OperationCanceledException; }
        private static string ErrorText(Exception error) { return SafeText.Clean(error is ApiException api ? api.Message + "\n" + api.Body : error.Message); }
        private void Apply(long revision, Action action) { _Host.Post(() => { if (!_Disposed && revision == _Revision) action(); }); }
        private static JsonNode ReadResource(string name)
        {
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SharpAI.Tui." + name) ?? throw new InvalidDataException("Missing " + name);
            return JsonNode.Parse(stream) ?? throw new InvalidDataException("Empty " + name);
        }
        private async Task ContextHelpAsync()
        {
            string help = _Current switch
            {
                "/models" => "Select a registered or resident model. Pull downloads from the server; Delete removes local weights, and Unload releases resident weights. Destructive actions ask for confirmation.",
                "/playground" => "Choose a model and saved libraries. Enter sends a prompt; Ctrl+J inserts a newline. One generation runs at a time. Ctrl+X cancels. Scroll the transcript to inspect earlier output. Use Ctrl+Y to copy the latest result.",
                "/settings" => "Edit typed settings and scroll through all sections. Ctrl+S saves; Escape cancels. Empty numeric values send null. Failed saves retain the draft. Some settings require a server restart.",
                "/api-explorer" => "Select an operation, replace path placeholders, and edit request JSON. Execute asks for confirmation before mutations. Streaming records appear incrementally. Cancel stops reading but may not undo a server mutation.",
                "/system-prompts" or "/model-configurations" => "Create or edit reusable items. User items are private. System and Tenant publication requires administrator rights. Duplicate creates an independent copy; changing visibility requires duplication.",
                _ when _Current.StartsWith("/request-history", StringComparison.Ordinal) => "Filter requests on the server and select a time preset. The stacked chart shows successful and failed requests with UTC time/count axes. Focus it and use Left/Right to inspect a bar, then Enter or click to filter the table. Adjacent intervals merge on small terminals; Activity chart opens a larger view. Enter opens separately fetched details. All list timestamps use UTC. Unavailable measurements differ from zero.",
                _ when _Current.StartsWith("/access/", StringComparison.Ordinal) => "Select a tenant explicitly using Commands. Inspect records before changing them. Protected records cannot be changed. Server permissions remain authoritative; password resets and revoked sessions take effect on the next request.",
                _ => "F2 opens navigation, Ctrl+P opens all commands, Tab and Shift+Tab move focus, Enter activates, and Ctrl+Q quits. F5 refreshes. Credentials remain in memory unless you select an existing protected token file in the connection profile."
            };
            string helpKey = _Current switch { "/models" => "models.pullHelp", "/playground" => "help.prompt", "/settings" => "settings.restartNote", "/system-prompts" => "library.promptsHelp", "/model-configurations" => "library.configurationsHelp", "/api-explorer" => "apiExplorer.confirmMessage", _ when _Current.StartsWith("/access/", StringComparison.Ordinal) => "admin.help." + _Current.Split('/').Last(), _ => "help.readonly" };
            if (_Localizer.Locale != "en") help = _Localizer.Text(helpKey);
            string? focusedHelp = (_Host.CurrentFocusPath.Leaf as ITooltipProvider)?.GetTooltip(0, 0);
            await _Host.ShowAsync(new MessageModal(RouteTitle(_Current), focusedHelp ?? help, new[] { _Localizer.Text("common.close") })).ConfigureAwait(false);
        }
        private async Task ChangeLanguageAsync()
        {
            string[] languages = Localizer.Languages.Select(locale => locale == "qps-ploc" ? "Expansion test" : System.Globalization.CultureInfo.GetCultureInfo(locale).NativeName).ToArray();
            int selected = await _Host.SelectAsync("Language", languages).ConfigureAwait(false);
            if (selected < 0 || selected >= languages.Length) return;
            _Host.Post(() => { _Profile.Locale = Localizer.Languages[selected]; _Store.Save(_Profile); _Host.Notify(_Localizer.Phrase("Language saved. Restart the terminal client to apply it.")); });
        }
        private async Task CopyAsync()
        {
            string? text = _CopyText;
            if (text == null) { _Host.Notify(_Localizer.Phrase("No result selected to copy.")); return; }
            long revision = _Revision; CancellationToken token = _Route.Token;
            bool copied = await _Clipboard.CopyAsync(text, token).ConfigureAwait(false);
            Apply(revision, () => { if (token.IsCancellationRequested) return; if (copied) _Host.Notify(_Localizer.Phrase("Copied")); else { _Host.Notify(_Localizer.Phrase("Clipboard unavailable. Select and copy the text below.")); ShowText("Copy result", text); } });
        }
        private async Task ChooseColumnsAsync()
        {
            DataTable<JsonNode>? table = _ActiveTable;
            if (table == null) return;
            long revision = _Revision;
            TUIKit.Modals.MultiSelectModal<DataColumn<JsonNode>> chooser = table.CreateColumnChooser("Columns");
            await _Host.ShowAsync(chooser).ConfigureAwait(false);
            Apply(revision, () => table.ApplyColumnChooser(chooser));
        }
        private async Task PaletteAsync() { await _Host.ShowCommandPaletteAsync(_Commands).ConfigureAwait(false); }
        private async Task QuitAsync()
        {
            if ((_Form?.IsDirty == true || _ScreenDraft || !string.IsNullOrEmpty(_ComposerDraft) || _Busy) && !await _Host.ConfirmAsync("Discard draft, cancel active work and quit?").ConfigureAwait(false)) return;
            _Host.Post(() => { _Lifetime.Cancel(); _Host.Quit(); });
        }
        public void Dispose()
        {
            if (_Disposed) return; _Disposed = true;
            _Lifetime.Cancel(); _ScreenTimer?.Dispose(); CloseModals(); DisposeLoaders(); _Route.Cancel(); _Host.Dispose(); _Route.Dispose(); _Lifetime.Dispose();
            _Client.AuthorizationFailed -= AuthorizationFailed; _Client.Token = null;
        }
    }
}
