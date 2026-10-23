namespace SharpAI.Tui.Widgets
{
    using System;
    using System.Collections.Generic;
    using SharpAI.Tui.Services;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    internal sealed class Composer : IWidget, IFocusable, IFocusAware, IMouseAware, IPasteTarget, ITextEntry, ITextEntryKeys, IKeyHintSource, IThemeable
    {
        internal TextEditor Editor { get; } = new TextEditor { ConsumeUnboundControlKeys = false, WordWrap = true };
        internal Action? Send { get; set; }
        public void ApplyTheme(Theme theme) { Editor.ApplyTheme(theme); }
        public KeyHint? LeaveHint { get { return new KeyHint("Tab", "Next control"); } }
        public bool AcceptsText { get { return true; } }
        public bool ConsumesChord(KeyChord chord) { return Editor.ConsumesChord(chord); }
        public void OnFocusChanged(bool focused) { Editor.OnFocusChanged(focused); }
        public bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Enter && key.Modifiers == KeyModifiers.None) { Send?.Invoke(); return true; }
            if (key.Code == KeyCode.Character && key.Rune == 'j' && (key.Modifiers & KeyModifiers.Ctrl) != 0) { Editor.InsertNewline(); return true; }
            if (Editor.Text.Length >= 1048576 && key.Code == KeyCode.Character) return true;
            return Editor.HandleKey(key);
        }
        public bool HandlePaste(string text) { return text.Length + Editor.Text.Length <= 1048576 && Editor.HandlePaste(SafeText.Clean(text)); }
        public bool HandleMouse(MouseEvent mouse) { return Editor.HandleMouse(mouse); }
        public Size Measure(Size available) { return Editor.Measure(available); }
        public void Render(ISurface surface) { Editor.Render(surface); }
        public IReadOnlyList<KeyHint>? GetKeyHints() { return new[] { new KeyHint("Enter", "Send"), new KeyHint("/?", "Commands"), new KeyHint("Ctrl+J", "Newline"), new KeyHint("Tab", "Next control") }; }
    }
}
