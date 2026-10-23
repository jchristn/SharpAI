namespace SharpAI.Tui.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using SharpAI.Client;
    using SharpAI.Tui.Services;
    using SharpAI.Tui.Localization;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    internal sealed class JsonForm
    {
        private readonly JsonObject _Draft;
        private readonly Action<JsonObject> _Save;
        private readonly Action _Cancel;
        private readonly List<Action> _Read = new List<Action>();
        private bool _StructureChanged;
        private string _CleanJson = string.Empty;
        private readonly List<bool> _EnabledBefore = new List<bool>();
        private readonly string[] _Scopes;
        private readonly Localizer _Localizer;
        internal Form Form { get; } = new Form();
        internal PaddedScrollView View { get; }
        internal bool IsDirty { get { return Form.IsDirty || _StructureChanged && _Draft.ToJsonString() != _CleanJson; } }
        internal JsonObject Draft { get { foreach (Action read in _Read.ToArray()) read(); return _Draft; } }
        internal JsonForm(JsonObject value, Action<JsonObject> save, Action cancel, string[]? scopes = null, Localizer? localizer = null)
        {
            _Localizer = localizer ?? new Localizer("en");
            _Scopes = scopes ?? new[] { "User", "Tenant", "System" };
            _Draft = (JsonObject)value.DeepClone(); _Save = save; _Cancel = cancel;
            Form.SubmitLabel = _Localizer.Text("common.save"); Form.CancelLabel = _Localizer.Text("common.cancel");
            Form.ShowButtons = true; Form.ShowFieldErrors = true; Form.DirtyTracking = FormDirtyTracking.Snapshot;
            Form.SubmitKey = KeyChord.Parse("ctrl+s"); Form.CancelKey = KeyChord.Parse("escape");
            Form.SubmitRequested += (sender, args) => _Save(Draft);
            Form.CancelRequested += (sender, args) => _Cancel();
            View = new PaddedScrollView(Form, 70, 30) { ForwardKeys = true };
            Build(); _CleanJson = _Draft.ToJsonString();
        }
        internal void MarkClean() { _StructureChanged = false; _CleanJson = _Draft.ToJsonString(); Form.MarkClean(); }
        internal void SetEnabled(bool enabled)
        {
            if (!enabled)
            {
                _EnabledBefore.Clear();
                for (int index = 0; index < Form.FieldCount; index++) { _EnabledBefore.Add(Form.GetField(index) is not IEnableable control || control.IsEnabled); Form.SetFieldEnabled(index, false); }
            }
            else for (int index = 0; index < Form.FieldCount; index++) Form.SetFieldEnabled(index, index < _EnabledBefore.Count ? _EnabledBefore[index] : true);
            if (Form.Buttons != null) foreach (Button button in Form.Buttons.Buttons) button.IsEnabled = enabled;
        }
        private void Build()
        {
            _Read.Clear(); Form.Clear(); AddObject(_Draft, string.Empty);
            Form.MarkClean(); View.SetContentSize(70, Math.Max(20, Form.FieldCount * 5 + 10));
        }
        private void Rebuild(Action change)
        {
            if (Form.ValidateAll().Count > 0) return;
            foreach (Action read in _Read.ToArray()) read(); change(); _StructureChanged = true; Build();
        }
        private void AddObject(JsonObject value, string prefix)
        {
            foreach (KeyValuePair<string, JsonNode?> entry in value.ToList())
            {
                string key = entry.Key; string label = prefix + key;
                bool managed = key is "Id" or "Guid" or "SoftwareVersion" or "SchemaVersion" or "CreatedUtc" or "UpdatedUtc" or "CanEdit" or "CanDelete" or "IsBuiltIn" or "IsProtected";
                AddValue(label, key, entry.Value, node => value[key] = node, managed);
            }
        }
        private void AddValue(string label, string key, JsonNode? original, Action<JsonNode?> assign, bool managed)
        {
            if (original is JsonObject child)
            {
                Form.AddSection(_Localizer.Phrase(label)); AddObject(child, label + ".");
                if (key.Contains("Header", StringComparison.OrdinalIgnoreCase) || key.Contains("Priority", StringComparison.OrdinalIgnoreCase) || key is "ServiceLinks")
                {
                    bool numericValues = key.Contains("Priority", StringComparison.OrdinalIgnoreCase) || child.Any(entry => entry.Value is JsonValue scalar && scalar.GetValueKind() == JsonValueKind.Number);
                    TextField name = new TextField(); TextField value = new TextField(); Form.Add("New key", name); Form.Add("New value", value, () => numericValues && value.Value.Length > 0 && !int.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int _) ? "Enter an integer priority." : null);
                    Form.Add("", new Button(_Localizer.Phrase("+ Add entry"), () => { if (!string.IsNullOrWhiteSpace(name.Value) && (!numericValues || value.Value.Length > 0)) Rebuild(() => child[name.Value] = numericValues ? JsonValue.Create(int.Parse(value.Value, CultureInfo.InvariantCulture)) : JsonValue.Create(value.Value)); }));
                    foreach (string childKey in child.Select(entry => entry.Key).ToArray()) Form.Add(childKey, new Button(_Localizer.Phrase("- Remove"), () => Rebuild(() => child.Remove(childKey))));
                }
                return;
            }
            if (original is JsonArray array)
            {
                Form.AddSection(_Localizer.Phrase(label));
                for (int index = 0; index < array.Count; index++)
                {
                    int itemIndex = index; AddValue(label + "[" + index + "]", key, array[index], node => array[itemIndex] = node, managed);
                    if (!managed) Form.Add("", new Button(_Localizer.Phrase("- Remove row ") + (index + 1), () => Rebuild(() => array.RemoveAt(itemIndex))));
                }
                if (!managed) Form.Add("", new Button(_Localizer.Phrase("+ Add row"), () => Rebuild(() => array.Add(array.FirstOrDefault() is JsonObject template ? template.DeepClone() : JsonValue.Create("")))));
                return;
            }
            if (original is JsonValue scalar && scalar.TryGetValue<bool>(out bool boolean))
            {
                Checkbox checkbox = Form.AddCheckbox(_Localizer.Phrase(label), boolean); checkbox.IsEnabled = !managed; _Read.Add(() => assign(JsonValue.Create(checkbox.Checked))); return;
            }
            string[]? choices = key switch { "Scope" => _Scopes, "Locale" => SharpAI.Tui.Localization.Localizer.Languages, "Theme" => new[] { "dark", "light", "contrast" }, "Effect" => new[] { "Permit", "Deny" }, "ResourceScope" => new[] { "Tenant", "Resource" }, _ => null };
            if (choices != null)
            {
                Dropdown<string> dropdown = new Dropdown<string>(choices, choice => key == "Locale" ? choice == "qps-ploc" ? "Expansion test" : CultureInfo.GetCultureInfo(choice).NativeName : choice) { SelectedIndex = Math.Max(0, Array.IndexOf(choices, original?.ToString())), IsEnabled = !managed };
                Form.Add(_Localizer.Phrase(label), dropdown); _Read.Add(() => assign(JsonValue.Create(dropdown.SelectedItem))); return;
            }
            if (key == "Content")
            {
                MultilineField editor = new MultilineField(); editor.Editor.Text = SafeText.Clean(original?.ToString()); editor.Editor.IsReadOnly = managed;
                Form.Add(_Localizer.Phrase(label), editor); Form.SetFieldHint(Form.FieldCount - 1, _Localizer.Text("library.contentHelp"));
                _Read.Add(() => assign(editor.Editor.Text == SafeText.Clean(original?.ToString()) ? original?.DeepClone() : JsonValue.Create(editor.Editor.Text))); return;
            }
            bool numeric = original is JsonValue number && number.GetValueKind() == JsonValueKind.Number || IsGenerationNumber(key);
            TextField field = new TextField { Value = SafeText.Clean(original?.ToString()), IsReadOnly = managed, MaskChar = IsSecret(key) ? '*' : '\0', MaxLength = 1048576 };
            Form.Add(_Localizer.Phrase(label), field, () => ValidateNumber(key, numeric, field.Value));
            Form.SetFieldHint(Form.FieldCount - 1, numeric ? "Number. Leave blank to use the engine default (null)." : managed ? "Managed by the server." : "Edit " + _Localizer.Phrase(label) + ". Changes are sent only when you save.");
            _Read.Add(() =>
            {
                if (managed) return;
                if (field.Value == SafeText.Clean(original?.ToString())) { assign(original?.DeepClone()); return; }
                if (numeric) assign(field.Value.Length == 0 ? null : JsonNode.Parse(field.Value));
                else if (original == null && field.Value.Length == 0) assign(null);
                else assign(JsonValue.Create(field.Value));
            });
        }
        private static bool IsGenerationNumber(string key) { return key is "Temperature" or "MaxTokens" or "TopP" or "TopK" or "MinP" or "RepeatPenalty" or "RepeatLastN" or "PresencePenalty" or "FrequencyPenalty" or "Seed"; }
        private static string? ValidateNumber(string key, bool numeric, string value)
        {
            if (!numeric || string.IsNullOrWhiteSpace(value)) return null;
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) || !double.IsFinite(number)) return "Use a finite number or leave blank for null.";
            try
            {
                JsonNode? parsed = JsonNode.Parse(value);
                if (parsed is not JsonValue scalar || scalar.GetValueKind() != JsonValueKind.Number) return "Use a valid JSON number.";
                if (IsGenerationNumber(key)) { JsonObject configuration = new JsonObject { [key] = parsed }; configuration.Deserialize<GenerationConfiguration>(); }
            }
            catch (Exception error) when (error is ArgumentException or JsonException) { return SafeText.Clean(error.Message); }
            return null;
        }
        private static bool IsSecret(string key) { return System.Text.RegularExpressions.Regex.IsMatch(key, "password|secret|apikey|token|accesskey|authorization|cookie", System.Text.RegularExpressions.RegexOptions.IgnoreCase); }
    }
}
