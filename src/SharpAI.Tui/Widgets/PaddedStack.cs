namespace SharpAI.Tui.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using SharpAI.Tui.Services;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    // Keep TUIKit's shared borders and focus layout; inset only leaf content horizontally.
    internal sealed class PaddedStack : IWidget, IFocusContainer, IFocusAware, IFocusPathNode, IFocusStop, IMouseAware, IThemeable
    {
        private readonly FramedStack _Inner;
        private readonly Dictionary<IWidget, IWidget> _Wrapped = new Dictionary<IWidget, IWidget>();
        private IWidget? _Overlay;
        private CellStyle _Background = TuiTheme.Create().Text;
        internal PaddedStack(SplitOrientation orientation = SplitOrientation.Vertical) { _Inner = new FramedStack(orientation); }
        internal IReadOnlyList<IWidget> Children { get { return _Wrapped.Keys.ToArray(); } }
        internal int Count { get { return _Inner.Count; } }
        internal IWidget? FocusedWidget { get { return _Wrapped.FirstOrDefault(entry => ReferenceEquals(entry.Value, _Inner.FocusedWidget)).Key; } }
        internal IWidget? Overlay { get { return _Overlay; } set { _Overlay = value; _Inner.Overlay = value == null ? null : Wrap(value); } }
        internal PaddedStack Add(IWidget child, StackSize size, string? title = null)
        {
            IWidget wrapped = Wrap(child); _Wrapped.Add(child, wrapped); _Inner.Add(wrapped, size, title); return this;
        }
        internal bool Remove(IWidget child)
        {
            if (!_Wrapped.TryGetValue(child, out IWidget? wrapped)) return false;
            _Wrapped.Remove(child); return _Inner.Remove(wrapped);
        }
        internal void Focus(IWidget child) { if (_Wrapped.TryGetValue(child, out IWidget? wrapped)) _Inner.Focus(wrapped); }
        private static IWidget Wrap(IWidget child)
        {
            return child is PaddedStack stack ? stack._Inner : child is IFocusable ? new FocusableInset(child) : new HorizontalInset(child);
        }
        public IFocusable? FocusedChild { get { return _Inner; } }
        public IFocusable? FocusedLeaf { get { return _Inner.FocusedLeaf; } }
        public bool IsFocusStop { get { return _Inner.Children.Any(child => child is IFocusable && FocusScope.IsTabStop(child)); } }
        public bool MoveFocus(bool forward) { return _Inner.MoveFocus(forward); }
        public void FocusEdge(bool first) { _Inner.FocusEdge(first); }
        public void OnFocusChanged(bool focused) { _Inner.OnFocusChanged(focused); }
        public bool HandleKey(KeyEvent key) { return _Inner.HandleKey(key); }
        public bool HandleMouse(MouseEvent mouse) { return _Inner.HandleMouse(mouse); }
        public void ApplyTheme(Theme theme) { _Background = theme.Text; _Inner.ApplyTheme(theme); }
        public Size Measure(Size available) { return _Inner.Measure(available); }
        public void Render(ISurface surface)
        {
            _Inner.Render(surface);
            if (_Overlay != null && surface.Size.Width > 1) surface.Fill(new Rect(surface.Size.Width / 2 - 1, 0, 1, surface.Size.Height), Cell.Blank(_Background));
        }

        private class HorizontalInset : IWidget, IMouseAware, IThemeable, IHideable, ITooltipProvider
        {
            protected readonly IWidget Child;
            private CellStyle _Background = TuiTheme.Create().Text;
            private int _Width;
            internal HorizontalInset(IWidget child) { Child = child; }
            public bool IsVisible { get { return Child is not IHideable hideable || hideable.IsVisible; } }
            public void ApplyTheme(Theme theme)
            {
                if (Child is IThemeable themed) themed.ApplyTheme(theme);
                _Background = Child is ListView<string> ? theme.GetStyle(Theme.SidebarRole) : theme.Text;
                if (Child is ListView<string> navigation) navigation.NormalStyle = _Background;
            }
            public Size Measure(Size available)
            {
                Size measured = Child.Measure(new Size(Math.Max(0, available.Width - 2), available.Height));
                return new Size(Math.Min(available.Width, measured.Width + 2), measured.Height);
            }
            public void Render(ISurface surface)
            {
                _Width = surface.Size.Width;
                if (_Width < 3) return;
                surface.Fill(new Rect(0, 0, 1, surface.Size.Height), Cell.Blank(_Background));
                surface.Fill(new Rect(_Width - 1, 0, 1, surface.Size.Height), Cell.Blank(_Background));
                Child.Render(new SurfaceView(surface, new Rect(1, 0, _Width - 2, surface.Size.Height)));
            }
            public string? GetTooltip(int x, int y) { return x >= 1 && x < _Width - 1 && Child is ITooltipProvider provider ? provider.GetTooltip(x - 1, y) : null; }
            public bool HandleMouse(MouseEvent mouse)
            {
                if (mouse.X < 1 || mouse.X >= _Width - 1) return false;
                return Child is IMouseAware aware && aware.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X - 1, mouse.Y, mouse.Modifiers, mouse.ClickCount));
            }
        }

        private sealed class FocusableInset : HorizontalInset, IFocusContainer, IFocusPathNode, IFocusAware, IFocusStop, IEnableable
        {
            internal FocusableInset(IWidget child) : base(child) { }
            public IFocusable? FocusedChild { get { return (IFocusable)Child; } }
            public IFocusable? FocusedLeaf { get { return Child is IFocusContainer container ? container.FocusedLeaf : (IFocusable)Child; } }
            public bool IsFocusStop { get { return FocusScope.IsTabStop(Child); } }
            public bool IsEnabled { get { return Child is not IEnableable enableable || enableable.IsEnabled; } set { if (Child is IEnableable enableable) enableable.IsEnabled = value; } }
            public bool MoveFocus(bool forward) { return Child is IFocusContainer container && container.MoveFocus(forward); }
            public void FocusEdge(bool first) { if (Child is IFocusContainer container) container.FocusEdge(first); }
            public void OnFocusChanged(bool focused) { if (Child is IFocusAware aware) aware.OnFocusChanged(focused); }
            public bool HandleKey(KeyEvent key) { return ((IFocusable)Child).HandleKey(key); }
        }
    }
}
