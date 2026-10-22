namespace SharpAI.Tui.Widgets
{
    using System;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Modals;
    using TUIKit.Widgets;

    internal sealed class SecretModal : Modal
    {
        private readonly Form _Form = new Form();
        private readonly TextField _Field;
        private Rect _Content;
        internal SecretModal(string secret, Action<string> copy)
        {
            _Field = new TextField { Value = secret, MaskChar = '*', IsReadOnly = true, RevealKey = KeyChord.Parse("ctrl+r") };
            _Form.Add("Bearer token | Ctrl+R reveal, select text if clipboard is unavailable", _Field);
            _Form.Add("", new Button("Copy token", () => copy(_Field.Value)));
            _Form.Add("", new Button("Dismiss", () => RequestClose(null)));
            _Form.FocusEdge(true);
        }
        internal bool HasValue { get { return _Field.Value.Length > 0; } }
        internal void Clear() { _Field.Value = string.Empty; _Field.IsRevealed = false; }
        /// <inheritdoc/>
        public override bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Escape) { Clear(); RequestClose(null); return true; }
            return _Form.HandleKey(key);
        }
        /// <inheritdoc/>
        public override bool HandleMouse(MouseEvent mouse)
        {
            return _Form.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X - _Content.X, mouse.Y - _Content.Y, mouse.Modifiers, mouse.ClickCount));
        }
        /// <inheritdoc/>
        public override void Render(ISurface surface)
        {
            int width = Math.Min(90, surface.Size.Width - 4); int height = Math.Min(14, surface.Size.Height - 4);
            int x = (surface.Size.Width - width) / 2; int y = (surface.Size.Height - height) / 2;
            Rect box = new Rect(x, y, width, height); surface.Fill(box, Cell.Blank(CellStyle.Default)); surface.DrawBox(box, CellStyle.Default, "Credential token");
            _Content = new Rect(x + 1, y + 1, width - 2, height - 2);
            if (surface is BufferSurface buffer) _Form.Render(buffer.CreateView(_Content));
        }
    }
}
