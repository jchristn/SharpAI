namespace SharpAI.Tui
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json.Nodes;
    using SharpAI.Tui.Services;
    using TUIKit.Widgets;

    internal sealed partial class SharpAITuiApp
    {
        private void ChatNotice(MarkdownView transcript, string text)
        {
            _Transcript += "\n\n" + SafeText.Clean(text);
            if (_Transcript.Length > 2 * 1024 * 1024) _Transcript = _Transcript.Substring(_Transcript.Length - 2 * 1024 * 1024);
            transcript.Markdown = _Transcript; transcript.ScrollToEnd();
        }
        private void RunChatCommand(ChatCommand command, TextEditor composer, MarkdownView transcript, DefinitionList telemetry)
        {
            if (_Busy && command.Id != "stop") { _Host.Notify(_Localizer.Text("chatCommands.busy")); return; }
            composer.Text = string.Empty;
            if (command.Arguments.Length > 0 && command.Id != "model") { ChatNotice(transcript, _Localizer.Text("chatCommands.unknown")); return; }
            switch (command.Id)
            {
                case "help":
                    ChatNotice(transcript, string.Join("\n", ChatCommands.Commands.Select(item => "- `" + item!["usage"] + "` - " + _Localizer.Text(item["description"]!.ToString()))) + "\n\n" + _Localizer.Text("chatCommands.literal"));
                    break;
                case "clear":
                    _Messages.Clear(); _ContextSummary = string.Empty; _Turns.Clear(); _LastTurnRecord = null; _CopyText = null; _Transcript = string.Empty;
                    transcript.Markdown = string.Empty; telemetry.Clear(); Status(_Localizer.Text("chatCommands.cleared"));
                    break;
                case "stop":
                    if (_Busy) CancelActive(); else _Host.Notify(_Localizer.Text("chatCommands.idle"));
                    break;
                case "status":
                    ChatNotice(transcript, "**" + _Localizer.Text("playground.model") + ":** " + SafeText.Clean(_SelectedModel) + "\n\nMaxTokens: " + JsonData.Text(_SelectedConfiguration?["Configuration"], "MaxTokens") + "\n\n" + _Localizer.Text("chatCommands.context") + ": " + _Messages.Count + (_ContextSummary.Length > 0 ? " + " + _Localizer.Text("chatCommands.summary") : string.Empty));
                    break;
                case "model":
                    if (command.Arguments.Length == 0) Pick("/api/tags", "Model", row => { ResetGenerationScope(); _SelectedModel = JsonData.Text(row, "name", "model"); Playground(); });
                    else
                    {
                        long revision = _Revision; _Busy = true;
                        Work(async ct =>
                        {
                            try
                            {
                                JsonNode? result = await _Client.JsonAsync("GET", "/api/tags", null, ct).ConfigureAwait(false);
                                JsonNode? selected = JsonData.Rows(result).FirstOrDefault(row => JsonData.Text(row, "name", "model").Equals(command.Arguments, StringComparison.OrdinalIgnoreCase));
                                Apply(revision, () => { _Busy = false; if (selected == null) ChatNotice(transcript, _Localizer.Text("chatCommands.modelUnknown")); else { _SelectedModel = JsonData.Text(selected, "name", "model"); Playground(); } });
                            }
                            finally { Apply(revision, () => _Busy = false); }
                        });
                    }
                    break;
                case "compact": CompactChat(transcript); break;
                default: ChatNotice(transcript, _Localizer.Text("chatCommands.unknown")); break;
            }
        }

        private void CompactChat(MarkdownView transcript)
        {
            if (_Messages.Count == 0) { ChatNotice(transcript, _Localizer.Text("chatCommands.noConversation")); return; }
            if (string.IsNullOrEmpty(_SelectedModel)) { _Host.Notify(_Localizer.Phrase("Select a model.")); return; }
            int keep = _Messages.Count > 2 ? 2 : 0;
            JsonArray earlier = new JsonArray(); JsonArray recent = new JsonArray();
            for (int index = 0; index < _Messages.Count; index++) (index < _Messages.Count - keep ? earlier : recent).Add(_Messages[index]!.DeepClone());
            JsonObject data = new JsonObject { ["previousSummary"] = _ContextSummary, ["earlierMessages"] = earlier };
            JsonObject payload = new JsonObject
            {
                ["model"] = _SelectedModel, ["stream"] = false,
                ["messages"] = new JsonArray(new JsonObject { ["role"] = "system", ["content"] = ChatCommands.CompactInstruction }, new JsonObject { ["role"] = "user", ["content"] = data.ToJsonString() }),
                ["options"] = new JsonObject { ["temperature"] = 0.2, ["num_predict"] = ChatCommands.CompactMaxTokens, ["display_thinking"] = false, ["stop"] = new JsonArray() }
            };
            _Busy = true; long revision = _Revision; Status(_Localizer.Text("chatCommands.compacting"));
            Work(async ct =>
            {
                try
                {
                    JsonNode? result = await _Client.JsonAsync("POST", "/api/chat", payload, ct).ConfigureAwait(false);
                    string summary = JsonData.Text(result?["message"], "content");
                    if (!JsonData.Flag(result, "done") || JsonData.Flag(result, "cancelled") || JsonData.Text(result, "done_reason") == "length" || string.IsNullOrWhiteSpace(summary)) throw new InvalidDataException(_Localizer.Text("chatCommands.summaryFailed"));
                    ct.ThrowIfCancellationRequested();
                    Apply(revision, () =>
                    {
                        if (ct.IsCancellationRequested) return;
                        _ContextSummary = summary; _Messages.Clear(); foreach (JsonNode? message in recent) _Messages.Add(message!.DeepClone());
                        _Busy = false; ChatNotice(transcript, _Localizer.Text("chatCommands.compacted")); Status(_Localizer.Text("chatCommands.compacted"));
                    });
                }
                catch (OperationCanceledException) { Apply(revision, () => { _Busy = false; Status(_Localizer.Text("chatCommands.summaryFailed")); }); }
                catch (Exception error) when (IsExpected(error)) { Apply(revision, () => { _Busy = false; ChatNotice(transcript, _Localizer.Text("chatCommands.summaryFailed") + " " + ErrorText(error)); }); }
            });
        }
    }
}
