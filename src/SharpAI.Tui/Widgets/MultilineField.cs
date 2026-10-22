namespace SharpAI.Tui.Widgets
{
    using System.Collections.Generic;
    using SharpAI.Tui.Services;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    internal sealed class MultilineField : IWidget, IFocusable, IFocusAware, IMouseAware, IPasteTarget, ITextEntry, ITextEntryKeys, IValueSnapshot, IKeyHintSource, IEnableable
    {
        internal TextEditor Editor { get; } = new TextEditor { ConsumeUnboundControlKeys = false, WordWrap = true };
        /// <inheritdoc/>
        public bool IsEnabled { get { return Editor.IsEnabled; } set { Editor.IsEnabled = value; } }
        /// <inheritdoc/>
        public bool AcceptsText { get { return !Editor.IsReadOnly; } }
        /// <inheritdoc/>
        public KeyHint? LeaveHint { get { return new KeyHint("Tab", "Next field"); } }
        /// <inheritdoc/>
        public bool ConsumesChord(KeyChord chord) { return Editor.ConsumesChord(chord); }
        /// <inheritdoc/>
        public void OnFocusChanged(bool focused) { Editor.OnFocusChanged(focused); }
        /// <inheritdoc/>
        public bool HandleKey(KeyEvent key) { return Editor.HandleKey(key); }
        /// <inheritdoc/>
        public bool HandlePaste(string text) { return text.Length + Editor.Text.Length <= 1048576 && Editor.HandlePaste(SafeText.Clean(text)); }
        /// <inheritdoc/>
        public bool HandleMouse(MouseEvent mouse) { return Editor.HandleMouse(mouse); }
        /// <inheritdoc/>
        public string? GetSnapshot() { return Editor.Text; }
        /// <inheritdoc/>
        public IReadOnlyList<KeyHint>? GetKeyHints() { return Editor.GetKeyHints(); }
        /// <inheritdoc/>
        public Size Measure(Size available) { return new Size(available.Width, System.Math.Min(available.Height, System.Math.Clamp(Editor.VisualLineCount(available.Width), 3, 12))); }
        /// <inheritdoc/>
        public void Render(ISurface surface) { Editor.Render(surface); }
    }
}
