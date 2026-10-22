namespace SharpAI.Tui.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Nodes;
    using SharpAI.Tui.Localization;
    using SharpAI.Tui.Services;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    internal sealed class TableViewport : IWidget, IFocusContainer, IFocusAware, IFocusPathNode, IMouseAware, IThemeable, IKeyHintSource
    {
        private static readonly System.Text.Json.JsonSerializerOptions _SearchJson = new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        private readonly DataTable<JsonNode> _Table;
        private readonly ScrollView _Scroll;
        private readonly TextField _Filter = new TextField { MaxLength = 256 };
        private readonly Localizer _Localizer;
        private readonly Action _Activate;
        private IReadOnlyList<JsonNode> _Source = Array.Empty<JsonNode>();
        private IReadOnlyList<JsonNode> _Visible = Array.Empty<JsonNode>();
        private int _Page;
        private long _Total;
        private string _Applied = string.Empty;
        private bool _FilterFocused;
        private bool _Focused;
        private int _FilterX;
        internal TableViewport(DataTable<JsonNode> table, Localizer localizer, Action activate)
        {
            _Table = table; _Localizer = localizer; _Activate = activate;
            _Scroll = new ScrollView(table, 100, 20) { ForwardKeys = true, ShowVerticalScrollbar = false, AutoScrollToFocus = false };
        }
        private void FocusFilter(bool filter)
        {
            _Scroll.OnFocusChanged(_Focused && !filter); _Filter.OnFocusChanged(_Focused && filter); _FilterFocused = filter;
        }
        /// <inheritdoc/>
        public IFocusable? FocusedChild { get { return _FilterFocused ? _Filter : _Scroll; } }
        /// <inheritdoc/>
        public IFocusable? FocusedLeaf { get { return _FilterFocused ? _Filter : _Table; } }
        /// <inheritdoc/>
        public bool MoveFocus(bool forward)
        {
            if (forward && !_FilterFocused) { FocusFilter(true); return true; }
            if (!forward && _FilterFocused) { FocusFilter(false); return true; }
            return false;
        }
        /// <inheritdoc/>
        public void FocusEdge(bool first) { FocusFilter(!first); }
        /// <inheritdoc/>
        public void OnFocusChanged(bool focused) { _Focused = focused; if (focused) _Activate(); FocusFilter(_FilterFocused); }
        /// <inheritdoc/>
        public bool HandleKey(KeyEvent key) { return _FilterFocused ? _Filter.HandleKey(key) : _Scroll.HandleKey(key); }
        /// <inheritdoc/>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse.Y == 1) { FocusFilter(true); return _Filter.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X - _FilterX, 0, mouse.Modifiers, mouse.ClickCount)); }
            if (mouse.Y < 2) return false;
            FocusFilter(false); return _Scroll.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X, mouse.Y - 2, mouse.Modifiers, mouse.ClickCount));
        }
        /// <inheritdoc/>
        public Size Measure(Size available) { return available; }
        /// <inheritdoc/>
        public void ApplyTheme(Theme theme) { _Scroll.ApplyTheme(theme); _Filter.ApplyTheme(theme); }
        /// <inheritdoc/>
        public IReadOnlyList<KeyHint>? GetKeyHints() { return new[] { new KeyHint("Left/Right", "Scroll columns"), new KeyHint("Tab", "Table / page filter") }; }
        private void FilterPage()
        {
            if (_Table.State != DataTableState.Ready) return;
            bool sourceChanged = _Page != _Table.PageNumber || _Total != _Table.TotalRows || !_Table.Rows.SequenceEqual(_Visible);
            if (sourceChanged) { _Source = _Table.Rows.ToArray(); _Page = _Table.PageNumber; _Total = _Table.TotalRows; }
            if (!sourceChanged && _Applied == _Filter.Value) return;
            _Applied = _Filter.Value;
            _Visible = string.IsNullOrEmpty(_Applied) ? _Source : _Source.Where(row => row.ToJsonString(_SearchJson).Contains(_Applied, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (_Table.PagingMode == DataTablePagingMode.Remote) _Table.BindPage(_Visible, _Page, _Total);
            else _Table.Bind(_Visible);
            _Total = _Table.TotalRows; _Page = _Table.PageNumber;
        }
        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface.Size.Height < 5) return;
            FilterPage();
            int width = Math.Max(surface.Size.Width, Math.Min(8192, _Table.VisibleColumns.Sum(column => Math.Max(column.Width ?? column.MinWidth, column.MinWidth) + 2)));
            _Scroll.SetContentSize(width, surface.Size.Height - 3);
            string identity = _Table.SelectedRow == null ? string.Empty : JsonData.Id(_Table.SelectedRow);
            surface.DrawText(0, 0, "ID: " + identity, CellStyle.Default);
            _FilterX = Math.Min(35, surface.Size.Width / 2);
            surface.DrawText(0, 1, _Localizer.Text("admin.filterPage") + " (" + _Visible.Count.ToString(_Localizer.Culture) + "/" + _Source.Count.ToString(_Localizer.Culture) + ")", CellStyle.Default);
            _Filter.Render(new SurfaceView(surface, new Rect(_FilterX, 1, surface.Size.Width - _FilterX, 1)));
            _Scroll.Render(new SurfaceView(surface, new Rect(0, 2, surface.Size.Width, surface.Size.Height - 2)));
        }
    }
}
