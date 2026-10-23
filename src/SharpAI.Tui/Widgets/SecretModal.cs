namespace SharpAI.Tui.Widgets
{
    using System;
    using SharpAI.Tui.Services;
    using TUIKit;
    using TUIKit.Theming;
    using TUIKit.Input;
    using TUIKit.Modals;
    using TUIKit.Widgets;

    internal sealed class SecretModal : Modal, IThemeable
    {
        private Theme _Theme = TuiTheme.Create();
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
        /// <inheritdoc/>
        public void ApplyTheme(Theme theme) { _Theme = theme; _Form.ApplyTheme(theme); }
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
            Rect box = new Rect(x, y, width, height); surface.Fill(new Rect(x - 1, y, width + 2, height), Cell.Blank(_Theme.Text)); surface.Fill(box, Cell.Blank(_Theme.Text)); surface.DrawBox(box, IsTopmost ? _Theme.Resolve(Theme.FocusBorderRole, _Theme.Accent) : _Theme.Border, "Credential token");
            _Content = new Rect(x + 2, y + 1, width - 4, height - 2);
            if (surface is BufferSurface buffer) _Form.Render(buffer.CreateView(_Content));
        }
    }
}
