namespace SharpAI.Tui
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;
    using SharpAI.Tui.Services;
    using SharpAI.Tui.Widgets;
    using TUIKit.Content;
    using TUIKit.Layout;
    using TUIKit.Widgets;

    internal sealed partial class SharpAITuiApp
    {
        private readonly List<ChatTurn> _Turns = new List<ChatTurn>();
        private readonly JsonArray _Messages = new JsonArray();
        private JsonNode? _SelectedPrompt;
        private JsonNode? _SelectedConfiguration;
        private string _SelectedModel = string.Empty;
        private string _Transcript = string.Empty;
        private string _ContextSummary = string.Empty;
        private bool _PlaygroundInitialized;
        private string _ComposerDraft = string.Empty;
        private JsonNode? _LastTurnRecord;
        private void Playground()
        {
            if (!_PlaygroundInitialized)
            {
                ShowText("Playground", "Loading saved libraries"); long revision = _Revision;
                Work(async ct =>
                {
                    JsonNode? models = await _Client.JsonAsync("GET", "/api/tags", null, ct).ConfigureAwait(false);
                    IReadOnlyList<JsonNode> choices = JsonData.Rows(models).Where(row => !string.IsNullOrWhiteSpace(JsonData.Text(row, "name", "model"))).ToList();
                    JsonNode? preferred = choices.FirstOrDefault(row => JsonData.Flag(row["capabilities"], "completions")) ?? choices.FirstOrDefault();
                    string selectedModel = JsonData.Text(preferred, "name", "model");
                    JsonNode? prompts = await _Client.JsonAsync("GET", Scoped("/v1.0/system-prompts?pageSize=25"), null, ct).ConfigureAwait(false);
                    JsonNode? configurations = await _Client.JsonAsync("GET", Scoped("/v1.0/model-configurations?pageSize=25"), null, ct).ConfigureAwait(false);
                    Apply(revision, () => { _SelectedModel = selectedModel; _SelectedPrompt = JsonData.Rows(prompts).FirstOrDefault()?.DeepClone(); _SelectedConfiguration = JsonData.Rows(configurations).FirstOrDefault()?.DeepClone(); _PlaygroundInitialized = true; Playground(); });
                });
                return;
            }
            MarkdownView transcript = new MarkdownView(SafeText.Clean(_Transcript));
            transcript.TailFollow.Mode = TailFollowMode.FollowAtBottom;
            transcript.ScrollToEnd();
            Composer composer = new Composer(); composer.Editor.Text = _ComposerDraft; composer.Editor.TextChanged += (sender, args) => _ComposerDraft = composer.Editor.Text;
            DefinitionList telemetry = Definitions();
            composer.Send = () => SendChat(composer.Editor, transcript, telemetry);
            ButtonRow controls = new ButtonRow();
            controls.Add(ButtonFor("Model: " + _SelectedModel, () => Pick("/api/tags", "Model", row => { ResetGenerationScope(); _SelectedModel = JsonData.Text(row, "name", "model"); Playground(); })));
            controls.Add(ButtonFor("System prompt: " + JsonData.Text(_SelectedPrompt, "Name"), () => Pick(Scoped("/v1.0/system-prompts"), "System prompt", row => { _SelectedPrompt = row.DeepClone(); Playground(); })));
            controls.Add(ButtonFor("Edit prompt", () => { if (_SelectedPrompt != null) LibraryEditor("system-prompts", _SelectedPrompt, !JsonData.Flag(_SelectedPrompt, "CanEdit"), saved => { _SelectedPrompt = saved.DeepClone(); Playground(); }); }));
            controls.Add(ButtonFor("Configuration: " + JsonData.Text(_SelectedConfiguration, "Name"), () => Pick(Scoped("/v1.0/model-configurations"), "Model configuration", row => { _SelectedConfiguration = row.DeepClone(); Playground(); })));
            controls.Add(ButtonFor("Edit configuration", () => { if (_SelectedConfiguration != null) LibraryEditor("model-configurations", _SelectedConfiguration, !JsonData.Flag(_SelectedConfiguration, "CanEdit"), saved => { _SelectedConfiguration = saved.DeepClone(); Playground(); }); }));
            ButtonRow actions = new ButtonRow();
            actions.Add(ButtonFor("Send", () => SendChat(composer.Editor, transcript, telemetry)));
            actions.Add(ButtonFor("Cancel", CancelActive));
            actions.Add(ButtonFor("New conversation", () => { if (_Busy) { _Host.Notify(_Localizer.Phrase("Cancel the active generation first.")); return; } _Messages.Clear(); _ContextSummary = string.Empty; _Turns.Clear(); _LastTurnRecord = null; _Transcript = string.Empty; Playground(); }));
            actions.Add(ButtonFor("Embeddings", Embeddings));
            actions.Add(ButtonFor("Copy code", CopyCode));
            if (_LastTurnRecord != null) actions.Add(ButtonFor("Turn details", TurnHistory));
            Pane warning = new Pane("warning"); warning.Write(_Localizer.Text("chat.warning"));
            PaddedStack wide = new PaddedStack().Add(controls, StackSize.Fixed(4), "Saved libraries").Add(transcript, StackSize.Weighted(1), "Conversation").Add(telemetry, StackSize.Fixed(4), "Turn telemetry").Add(composer, StackSize.Fixed(2), "Composer | /? commands | Ctrl+J newline").Add(actions, StackSize.Fixed(2)).Add(warning, StackSize.Fixed(1));
            PaddedStack narrow = new PaddedStack().Add(transcript, StackSize.Weighted(1), "Conversation | Ctrl+P model and saved libraries").Add(composer, StackSize.Fixed(2), "Composer | /? commands | Enter sends").Add(actions, StackSize.Fixed(2)).Add(warning, StackSize.Fixed(1));
            Show(new ResponsiveScreen(wide, narrow), "Playground");
        }
        private void Pick(string path, string title, Action<JsonNode> selected)
        {
            if (_Busy) { _Host.Notify(_Localizer.Phrase("Cancel the active generation first.")); return; }
            string[] columns = path == "/v1.0/tenants" ? new[] { "Guid", "Name", "Active" } : path == "/api/tags" ? new[] { "name", "size" } : path.Contains("/users", StringComparison.Ordinal) ? new[] { "Guid", "Email", "FirstName", "LastName" } : path.Contains("/roles", StringComparison.Ordinal) ? new[] { "Guid", "Name", "IsBuiltIn" } : path.Contains("/permissions", StringComparison.Ordinal) ? new[] { "Guid", "Name", "Effect" } : path.Contains("/credentials", StringComparison.Ordinal) ? new[] { "Guid", "Name", "UserGuid" } : new[] { "Id", "Name", "Scope" };
            DataTable<JsonNode> table = Table(columns);
            LoadTable(table, path, row => { selected(row); return Task.CompletedTask; });
            Show(ViewTable(table), title);
        }
        private void SendChat(TextEditor composer, MarkdownView transcript, DefinitionList telemetry)
        {
            ChatCommand command = ChatCommands.Parse(composer.Text);
            if (command.IsCommand) { RunChatCommand(command, composer, transcript, telemetry); return; }
            if (_Busy) { _Host.Notify(_Localizer.Phrase("One generation is already active.")); return; }
            if (string.IsNullOrWhiteSpace(composer.Text)) return;
            if (string.IsNullOrEmpty(_SelectedModel)) { _Host.Notify(_Localizer.Phrase("Select a model.")); return; }
            if (_Route.IsCancellationRequested) { _Route.Dispose(); _Route = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(_Lifetime.Token); }
            string prompt = command.Prompt;
            if (_SelectedConfiguration?["Configuration"] is not JsonObject configuration) { _Host.Notify(_Localizer.Phrase("Select a saved model configuration.")); return; }
            JsonObject options = GenerationOptions(configuration);
            JsonArray messages = (JsonArray)_Messages.DeepClone();
            if (_SelectedPrompt?["Content"] != null) messages.Insert(0, new JsonObject { ["role"] = "system", ["content"] = _SelectedPrompt["Content"]!.ToString() });
            if (_ContextSummary.Length > 0) messages.Insert(_SelectedPrompt?["Content"] != null ? 1 : 0, new JsonObject { ["role"] = "system", ["content"] = ChatCommands.SummaryPrefix + _ContextSummary });
            messages.Add(new JsonObject { ["role"] = "user", ["content"] = prompt });
            JsonObject payload = new JsonObject { ["model"] = _SelectedModel, ["messages"] = messages, ["options"] = options, ["stream"] = JsonData.Flag(configuration, "StreamResponse") };
            if (JsonData.Flag(configuration, "JsonOutput")) payload["format"] = "json";
            string prefix = _Transcript + "\n\n## User\n" + SafeText.Clean(prompt) + "\n\n## Assistant\n";
            transcript.Markdown = prefix; transcript.ScrollToEnd(); composer.Text = string.Empty; _Busy = true; Status(_Localizer.Text("apiExplorer.sending"));
            long revision = _Revision; bool waiting = true; int quip = 0;
            _ScreenTimer?.Dispose();
            _ScreenTimer = new System.Threading.Timer(state => Apply(revision, () => { if (_Busy && waiting) { telemetry.Set("Status", _Localizer.Text("quips." + (quip++ % 6))); } }), null, 0, 2500);
            Work(async ct =>
            {
                Stopwatch clock = Stopwatch.StartNew(); double? firstToken = null; JsonNode? final = null; StringBuilder answer = new StringBuilder();
                string outcome = "Complete";
                try
                {
                    if (JsonData.Flag(configuration, "StreamResponse"))
                    {
                        await foreach (JsonNode frame in _Client.StreamAsync("/api/chat", payload, ct).ConfigureAwait(false))
                        {
                            string fragment = JsonData.Text(frame["message"], "content");
                            if (fragment.Length > 0 && !firstToken.HasValue) firstToken = clock.Elapsed.TotalMilliseconds;
                            answer.Append(fragment); final = frame;
                            if (answer.Length > 1024 * 1024) throw new System.IO.InvalidDataException("Conversation turn exceeds the display limit.");
                            string snapshot = prefix + SafeText.Clean(answer.ToString()); Apply(revision, () => { waiting = false; transcript.Markdown = snapshot; });
                        }
                    }
                    else
                    {
                        final = await _Client.JsonAsync("POST", "/api/chat", payload, ct).ConfigureAwait(false);
                        answer.Append(JsonData.Text(final?["message"], "content"));
                    }
                    if (final?["done"]?.GetValue<bool>() != true) throw new System.IO.InvalidDataException("Generation ended without a completion marker. Inspect history before retrying.");
                    if (JsonData.Text(final, "done_reason") == "length") outcome = "Token limit reached";
                }
                catch (OperationCanceledException) { outcome = "Stopped"; }
                catch (Exception error) when (IsExpected(error)) { outcome = "Failed: " + ErrorText(error); }
                double elapsed = clock.Elapsed.TotalMilliseconds;
                string completed = prefix + SafeText.Clean(answer.ToString()) + "\n\n[" + _Localizer.Phrase(outcome) + "]";
                Apply(revision, () =>
                {
                    _Busy = false; waiting = false; _ScreenTimer?.Dispose(); _ScreenTimer = null;
                    JsonObject turnData = new JsonObject { ["Outcome"] = outcome, ["ClientTotalMs"] = elapsed, ["ClientTtftMs"] = firstToken, ["EngineEnvelope"] = final?.DeepClone() };
                    _Turns.Add(new ChatTurn { Number = _Turns.Count == 0 ? 1 : _Turns[_Turns.Count - 1].Number + 1, CompletedUtc = _Clock.GetUtcNow(), Text = answer.ToString(), Outcome = outcome, Measurements = turnData });
                    if (_Turns.Count > 50) _Turns.RemoveAt(0);
                    _LastTurnRecord = turnData; _CopyText = answer.ToString(); _Transcript = completed.Length > 2 * 1024 * 1024 ? completed.Substring(completed.Length - 2 * 1024 * 1024) : completed;
                    transcript.Markdown = _Transcript;
                    if (outcome == "Complete" || outcome == "Token limit reached")
                    {
                        _Messages.Add(new JsonObject { ["role"] = "user", ["content"] = prompt }); _Messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = answer.ToString() });
                        while (_Messages.Count > 100) { _Messages.RemoveAt(0); _Messages.RemoveAt(0); }
                    }
                    Status(outcome); telemetry.Set("Outcome", outcome); telemetry.Set("Total ms", elapsed.ToString("N1", _Localizer.Culture));
                    telemetry.Set("TTFT ms", firstToken?.ToString("N1", _Localizer.Culture) ?? "Unavailable");
                    double? input = Measurement(final, "prompt_eval_count"); double? output = Measurement(final, "eval_count");
                    double? serverFirst = Measurement(final, "first_token_duration") / 1000000;
                    telemetry.Set("TTFT ms", FormatMeasurement(serverFirst > 0 ? serverFirst : firstToken));
                    telemetry.Set("Input / output / total tokens", FormatMeasurement(input) + " / " + FormatMeasurement(output) + " / " + FormatMeasurement(input + output));
                    telemetry.Set("Generation tokens / second", FormatMeasurement(Measurement(final, "eval_duration") > 0 ? output / (Measurement(final, "eval_duration") / 1000000000) : null));
                    telemetry.Set("Stream ms", FormatMeasurement(firstToken.HasValue ? elapsed - firstToken.Value : null));
                });
            });
        }
        private void TurnHistory()
        {
            if (_Turns.Count == 0) return;
            ListView<ChatTurn> turns = new ListView<ChatTurn>(turn => turn.Number.ToString(_Localizer.Culture) + " | " + turn.CompletedUtc.ToString("u", _Localizer.Culture) + " | " + turn.Outcome);
            turns.SetItems(_Turns);
            turns.ItemActivated += index => { ChatTurn turn = _Turns[index]; Inspect(turn.Measurements, "Turn measurements"); };
            Show(turns, "Turn details");
        }
        private void CopyCode()
        {
            string text = _Turns.Count == 0 ? string.Empty : _Turns[_Turns.Count - 1].Text;
            List<string> blocks = System.Text.RegularExpressions.Regex.Matches(text, @"```[^\n]*\n(.*?)```", System.Text.RegularExpressions.RegexOptions.Singleline).Select(match => match.Groups[1].Value).Take(100).ToList();
            if (blocks.Count == 0) { _Host.Notify(_Localizer.Phrase("No code blocks in the latest response.")); return; }
            ListView<string> picker = new ListView<string>(code => SafeText.Clean(code.Split('\n')[0])); picker.SetItems(blocks);
            picker.ItemActivated += index => { _CopyText = blocks[index]; Track(CopyAsync()); }; Show(picker, "Copy code");
        }
        private static double? Measurement(JsonNode? node, string key)
        {
            return double.TryParse(JsonData.Text(node, key), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : null;
        }
        private void ResetGenerationScope()
        {
            _Route.Cancel(); _Route.Dispose(); _Route = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(_Lifetime.Token); _Revision++; _Busy = false; DisposeLoaders();
        }
        internal static JsonObject GenerationOptions(JsonObject configuration)
        {
            Dictionary<string, string> mapping = new Dictionary<string, string> { ["Temperature"] = "temperature", ["MaxTokens"] = "num_predict", ["TopP"] = "top_p", ["TopK"] = "top_k", ["MinP"] = "min_p", ["RepeatPenalty"] = "repeat_penalty", ["RepeatLastN"] = "repeat_last_n", ["PresencePenalty"] = "presence_penalty", ["FrequencyPenalty"] = "frequency_penalty", ["Seed"] = "seed", ["Stop"] = "stop", ["DisplayThinking"] = "display_thinking" };
            JsonObject result = new JsonObject(); foreach (KeyValuePair<string, string> field in mapping) if (configuration[field.Key] != null) result[field.Value] = configuration[field.Key]!.DeepClone(); return result;
        }
        private void Embeddings()
        {
            Edit(new JsonObject { ["Model"] = _SelectedModel, ["Inputs"] = new JsonArray("") }, "Embeddings", draft =>
            {
                _Form = null; long revision = _Revision;
                Mutate(async ct =>
                {
                    JsonArray inputs = draft["Inputs"] as JsonArray ?? throw new ArgumentException("Enter one or more inputs.");
                    if (inputs.Count == 0) throw new ArgumentException("Enter one or more inputs.");
                    JsonNode? result = await _Client.JsonAsync("POST", "/api/embed", new JsonObject { ["model"] = draft["Model"]!.ToString(), ["input"] = inputs.Count == 1 ? inputs[0]?.DeepClone() : inputs.DeepClone() }, ct).ConfigureAwait(false);
                    Apply(revision, () => { if (result != null) EmbeddingsInspector(result); });
                });
            });
        }
        private void EmbeddingsInspector(JsonNode result)
        {
            DefinitionList metadata = Definitions();
            JsonArray vectors = result["embeddings"] as JsonArray ?? throw new InvalidOperationException("The response contains no embedding vectors.");
            metadata.Set("Vectors", vectors.Count.ToString(_Localizer.Culture));
            StringBuilder preview = new StringBuilder();
            for (int index = 0; index < vectors.Count; index++)
            {
                JsonArray vector = vectors[index] as JsonArray ?? throw new InvalidOperationException("Invalid embedding vector.");
                metadata.Set("Dimensions [" + index + "]", vector.Count.ToString(_Localizer.Culture));
                preview.Append('[').Append(index).Append("] ").AppendJoin(", ", vector.Take(16).Select(number => number?.ToString())).Append(vector.Count > 16 ? ", ..." : "").AppendLine();
            }
            TextEditor values = new TextEditor { ConsumeUnboundControlKeys = false, Text = preview.ToString(), IsReadOnly = true, WordWrap = true };
            ButtonRow actions = new ButtonRow(); actions.Add(ButtonFor("Copy result", () => { _CopyText = result.ToJsonString(); Track(CopyAsync()); })); actions.Add(ButtonFor("Back", Playground));
            Show(new PaddedStack().Add(metadata, StackSize.Fixed(Math.Min(8, 2 + vectors.Count))).Add(values, StackSize.Weighted(1), "Vector preview").Add(actions, StackSize.Fixed(3)), "Embeddings");
        }
    }
}
