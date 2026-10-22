namespace SharpAI.Tui.Widgets
{
    using System.Linq;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    internal sealed class ResponsiveScreen : IWidget, IFocusContainer, IFocusAware, IFocusPathNode, IMouseAware, IThemeable
    {
        private readonly FramedStack _Wide;
        private readonly FramedStack _Narrow;
        private FramedStack _Current;
        private bool _Focused;
        internal ResponsiveScreen(FramedStack wide, FramedStack narrow) { _Wide = wide; _Narrow = narrow; _Current = wide; }
        private void Choose(Size size)
        {
            FramedStack next = size.Width < 90 || size.Height < 30 ? _Narrow : _Wide;
            if (ReferenceEquals(next, _Current)) return;
            IFocusable? previous = _Current.FocusedLeaf;
            _Current.OnFocusChanged(false); _Current = next;
            if (previous is IWidget widget && _Current.Children.Contains(widget)) _Current.Focus(widget);
            _Current.OnFocusChanged(_Focused);
        }
        /// <inheritdoc/>
        public IFocusable? FocusedChild { get { return _Current; } }
        /// <inheritdoc/>
        public IFocusable? FocusedLeaf { get { return _Current.FocusedLeaf; } }
        /// <inheritdoc/>
        public bool MoveFocus(bool forward) { return _Current.MoveFocus(forward); }
        /// <inheritdoc/>
        public void FocusEdge(bool first) { _Current.FocusEdge(first); }
        /// <inheritdoc/>
        public void OnFocusChanged(bool focused) { _Focused = focused; _Current.OnFocusChanged(focused); }
        /// <inheritdoc/>
        public bool HandleKey(KeyEvent key) { return _Current.HandleKey(key); }
        /// <inheritdoc/>
        public bool HandleMouse(MouseEvent mouse) { return _Current.HandleMouse(mouse); }
        /// <inheritdoc/>
        public Size Measure(Size available) { Choose(available); return _Current.Measure(available); }
        /// <inheritdoc/>
        public void Render(ISurface surface) { Choose(surface.Size); _Current.Render(surface); }
        /// <inheritdoc/>
        public void ApplyTheme(Theme theme) { _Wide.ApplyTheme(theme); _Narrow.ApplyTheme(theme); }
    }
}
