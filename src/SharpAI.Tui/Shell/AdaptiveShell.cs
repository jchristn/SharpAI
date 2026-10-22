namespace SharpAI.Tui.Shell
{
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    internal sealed class AdaptiveShell : IWidget, IFocusContainer, IFocusAware, IFocusPathNode, IMouseAware, IThemeable
    {
        private readonly IWidget _Navigation;
        private readonly FramedStack _Content = new FramedStack();
        private FramedStack _Root = new FramedStack(SplitOrientation.Horizontal);
        private bool _Wide;
        private Theme _Theme = Theme.Dark;
        internal AdaptiveShell(IWidget navigation) { _Navigation = navigation; SetWidth(120); }
        internal void SetContent(IWidget widget, string title)
        {
            foreach (IWidget child in new List<IWidget>(_Content.Children)) _Content.Remove(child);
            _Content.Add(widget, StackSize.Weighted(1), title); _Content.ApplyTheme(_Theme);
            _Root.Focus(_Content);
        }
        internal void ToggleNavigation() { _Root.Overlay = _Root.Overlay == null ? _Navigation : null; }
        private void SetWidth(int width)
        {
            bool wide = width >= 100;
            if (_Root.Count > 0 && wide == _Wide) return;
            bool contentFocused = _Root.FocusedWidget == _Content;
            _Wide = wide;
            _Root = new FramedStack(SplitOrientation.Horizontal);
            if (wide) _Root.Add(_Navigation, StackSize.Fixed(18), "SharpAI");
            _Root.Add(_Content, StackSize.Weighted(1)); _Root.ApplyTheme(_Theme);
            if (contentFocused || !wide) _Root.Focus(_Content);
        }
        public IFocusable? FocusedLeaf { get { return _Root.FocusedLeaf; } }
        public IFocusable? FocusedChild { get { return _Root; } }
        public bool MoveFocus(bool forward) { return _Root.MoveFocus(forward); }
        public void FocusEdge(bool first) { _Root.FocusEdge(first); }
        public void OnFocusChanged(bool focused) { _Root.OnFocusChanged(focused); }
        public bool HandleKey(KeyEvent key) { return _Root.HandleKey(key); }
        public bool HandleMouse(MouseEvent mouse) { return _Root.HandleMouse(mouse); }
        public void ApplyTheme(Theme theme) { _Theme = theme; _Root.ApplyTheme(theme); }
        public Size Measure(Size available) { SetWidth(available.Width); return _Root.Measure(available); }
        public void Render(ISurface surface)
        {
            SetWidth(surface.Size.Width);
            if (surface.Size.Width < 80 || surface.Size.Height < 20) { surface.DrawText(0, 0, "Resize to at least 80x24. State is preserved.", CellStyle.Default); return; }
            _Root.Render(surface);
        }
    }
}
