namespace SharpAI.Tui.Widgets
{
    using System;
    using SharpAI.Tui.Services;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    // Reserve a blank column before a vertical scrollbar, including when content is scrolled horizontally.
    internal sealed class PaddedScrollView : IWidget, IFocusContainer, IFocusAware, IFocusPathNode, IMouseAware, IThemeable
    {
        private readonly ScrollView _Inner;
        private int _ContentHeight;
        private int _Width;
        private bool _Gap;
        private CellStyle _Background = TuiTheme.Create().Text;
        internal PaddedScrollView(IWidget child, int width, int height) { _Inner = new ScrollView(child, width, height); _ContentHeight = height; }
        internal bool ForwardKeys { get { return _Inner.ForwardKeys; } set { _Inner.ForwardKeys = value; } }
        internal bool ShowVerticalScrollbar { get { return _Inner.ShowVerticalScrollbar; } set { _Inner.ShowVerticalScrollbar = value; } }
        internal bool AutoScrollToFocus { get { return _Inner.AutoScrollToFocus; } set { _Inner.AutoScrollToFocus = value; } }
        internal void SetContentSize(int width, int height) { _ContentHeight = height; _Inner.SetContentSize(width, height); }
        public IFocusable? FocusedChild { get { return _Inner; } }
        public IFocusable? FocusedLeaf { get { return _Inner.FocusedLeaf; } }
        public bool MoveFocus(bool forward) { return _Inner.MoveFocus(forward); }
        public void FocusEdge(bool first) { _Inner.FocusEdge(first); }
        public void OnFocusChanged(bool focused) { _Inner.OnFocusChanged(focused); }
        public bool HandleKey(KeyEvent key) { return _Inner.HandleKey(key); }
        public bool HandleMouse(MouseEvent mouse)
        {
            if (_Gap && mouse.X == _Width - 2 && mouse.Kind != MouseEventKind.Wheel) return false;
            int x = _Gap && mouse.X >= _Width - 2 ? mouse.X - 1 : mouse.X;
            return _Inner.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, x, mouse.Y, mouse.Modifiers, mouse.ClickCount));
        }
        public void ApplyTheme(Theme theme) { _Background = theme.Text; _Inner.ApplyTheme(theme); }
        public Size Measure(Size available) { return _Inner.Measure(available); }
        public void Render(ISurface surface)
        {
            _Width = surface.Size.Width; _Gap = _Width >= 3 && ShowVerticalScrollbar && _ContentHeight > surface.Size.Height;
            if (!_Gap) { _Inner.Render(surface); return; }
            CellBuffer buffer = new CellBuffer(_Width - 1, surface.Size.Height); _Inner.Render(new BufferSurface(buffer));
            surface.Fill(new Rect(_Width - 2, 0, 1, surface.Size.Height), Cell.Blank(_Background));
            for (int row = 0; row < buffer.Height; row++)
                for (int column = 0; column < buffer.Width; column++) surface.Set(column == buffer.Width - 1 ? _Width - 1 : column, row, buffer.Get(column, row));
        }
    }
}
