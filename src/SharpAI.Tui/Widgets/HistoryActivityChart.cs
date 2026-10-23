namespace SharpAI.Tui.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using SharpAI.Tui.Localization;
    using SharpAI.Tui.Services;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    internal sealed class HistoryActivityChart : IWidget, IFocusable, IFocusAware, IMouseAware, IThemeable, IKeyHintSource
    {
        private readonly Localizer _Localizer;
        private readonly bool _Ascii;
        private readonly List<HistoryActivityBucket> _Bars = new List<HistoryActivityBucket>();
        private HistoryActivityData? _Data;
        private string? _Message;
        private Theme _Theme = TuiTheme.Create();
        private bool _Focused;
        private bool _Inspect;
        private int _Selected;
        private DateTimeOffset? _SelectionTime;
        private int _PlotLeft;
        private int _PlotWidth;
        private int _PlotHeight;
        private int _Width;
        internal Action<HistoryActivityBucket>? Activated { get; set; }
        internal HistoryActivityChart(Localizer localizer, bool ascii) { _Localizer = localizer; _Ascii = ascii; Loading(); }
        internal void Loading() { RememberSelection(); _Data = null; _Bars.Clear(); _Message = _Localizer.Text("common.loading"); _Width = 0; }
        internal void Failed(string message) { _SelectionTime = null; _Data = null; _Bars.Clear(); _Message = SafeText.Clean(message).Replace('\n', ' ').Replace('\r', ' '); _Width = 0; }
        internal void Bind(HistoryActivityData data) { RememberSelection(); _Bars.Clear(); _Data = data; _Message = null; _Width = 0; _Selected = 0; _Inspect = _Focused || _Inspect; }
        private void RememberSelection() { if (_Bars.Count > 0) _SelectionTime = _Bars[_Selected].Start; }
        /// <inheritdoc/>
        public void OnFocusChanged(bool focused) { _Focused = focused; _Inspect = focused; }
        /// <inheritdoc/>
        public void ApplyTheme(Theme theme) { _Theme = theme; }
        /// <inheritdoc/>
        public Size Measure(Size available) { return available; }
        /// <inheritdoc/>
        public IReadOnlyList<KeyHint>? GetKeyHints() { return new[] { new KeyHint("Left/Right", _Localizer.Phrase("Select interval")), new KeyHint("Enter", _Localizer.Phrase("Filter requests")), new KeyHint("Tab", _Localizer.Phrase("Next control")) }; }
        /// <inheritdoc/>
        public bool HandleKey(KeyEvent key)
        {
            if (_Bars.Count == 0 || key.Modifiers != KeyModifiers.None) return false;
            switch (key.Code)
            {
                case KeyCode.Left: _Selected = Math.Max(0, _Selected - 1); break;
                case KeyCode.Right: _Selected = Math.Min(_Bars.Count - 1, _Selected + 1); break;
                case KeyCode.Home: _Selected = 0; break;
                case KeyCode.End: _Selected = _Bars.Count - 1; break;
                case KeyCode.Enter: Activated?.Invoke(_Bars[_Selected]); break;
                default: return false;
            }
            _Inspect = true; return true;
        }
        /// <inheritdoc/>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse.Kind == MouseEventKind.Leave) { _Inspect = _Focused; return false; }
            if (_Bars.Count == 0 || mouse.X < _PlotLeft || mouse.X >= _PlotLeft + _PlotWidth || mouse.Y < 2 || mouse.Y >= 2 + _PlotHeight) return false;
            _Selected = Math.Min(_Bars.Count - 1, (mouse.X - _PlotLeft) * _Bars.Count / _PlotWidth); _Inspect = true;
            if (mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left) Activated?.Invoke(_Bars[_Selected]);
            return mouse.Kind is MouseEventKind.Move or MouseEventKind.Press;
        }
        private void FitBars(int width)
        {
            if (_Data == null || width == _Width) return;
            DateTimeOffset? selectedTime = _Bars.Count > 0 ? _Bars[_Selected].Start : _SelectionTime;
            _Width = width; _Bars.Clear();
            int perBar = Math.Max(1, (int)Math.Ceiling(_Data.Buckets.Count / (double)Math.Max(1, width / 2)));
            for (int index = 0; index < _Data.Buckets.Count; index += perBar)
            {
                int end = Math.Min(_Data.Buckets.Count, index + perBar); decimal success = 0; decimal failure = 0;
                for (int item = index; item < end; item++) { success += _Data.Buckets[item].Success; failure += _Data.Buckets[item].Failure; }
                _Bars.Add(new HistoryActivityBucket(_Data.Buckets[index].Start, _Data.Buckets[end - 1].End, success, failure, end - index));
            }
            int retained = selectedTime.HasValue ? _Bars.FindIndex(bucket => bucket.Start <= selectedTime.Value && selectedTime.Value < bucket.End) : -1;
            _Selected = retained >= 0 ? retained : Math.Clamp(_Selected, 0, Math.Max(0, _Bars.Count - 1));
        }
        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface.Size.Width < 20 || surface.Size.Height < 6) return;
            CellStyle text = _Theme.Text; string successGlyph = _Ascii ? "S" : "█"; string failureGlyph = _Ascii ? "F" : "▒";
            if (_Message != null) { surface.DrawText(0, 0, _Message, text); return; }
            if (_Data == null) return;
            string rate = _Data.Total == 0 ? "-" : (_Data.Success / _Data.Total).ToString("P0", _Localizer.Culture);
            surface.DrawText(0, 0, _Localizer.Text("requestHistory.requests") + " " + _Data.Total.ToString("N0", _Localizer.Culture) + " | " + _Localizer.Text("requestHistory.successRate") + " " + rate + " | " + _Localizer.Text("requestHistory.avgDuration") + " " + _Data.AverageDurationMs.ToString("N1", _Localizer.Culture) + " ms", text);
            if (_Data.Total == 0 || _Data.Buckets.Count == 0) { surface.DrawText(0, 2, _Localizer.Text("chart.empty"), text); return; }
            _PlotLeft = 9; _PlotWidth = Math.Max(1, surface.Size.Width - _PlotLeft);
            FitBars(_PlotWidth);
            decimal maximum = Math.Max(1, _Bars.Max(bucket => bucket.Total));
            _PlotHeight = surface.Size.Height - 5;
            CellStyle successStyle = _Theme.Accent; CellStyle failureStyle = _Theme.Error;
            surface.DrawText(0, 1, successGlyph + " " + _Localizer.Phrase("Success") + "  " + failureGlyph + " " + _Localizer.Phrase("Failure") + " | UTC", text);
            for (int row = 0; row < _PlotHeight; row++)
            {
                decimal level = maximum * (_PlotHeight - 1 - row) / Math.Max(1, _PlotHeight - 1);
                if (row == 0 || row == _PlotHeight / 2 || row == _PlotHeight - 1) surface.DrawText(0, 2 + row, TUIKit.Unicode.TextFit.Ellipsize(level.ToString(level >= 1000000 ? "0.#E+0" : "N0", _Localizer.Culture), _PlotLeft - 3), text);
                surface.DrawText(_PlotLeft - 2, 2 + row, "|", text);
            }
            for (int index = 0; index < _Bars.Count; index++)
            {
                HistoryActivityBucket bucket = _Bars[index]; int left = _PlotLeft + index * _PlotWidth / _Bars.Count; int end = _PlotLeft + (index + 1) * _PlotWidth / _Bars.Count;
                int totalHeight = bucket.Total == 0 ? 0 : Math.Max(1, (int)Math.Round(bucket.Total / maximum * _PlotHeight));
                int failureHeight = bucket.Failure == 0 ? 0 : Math.Min(totalHeight, Math.Max(1, (int)Math.Round(bucket.Failure / maximum * _PlotHeight)));
                int successHeight = totalHeight - failureHeight;
                for (int column = left; column < end - (end - left > 1 ? 1 : 0); column++)
                    for (int row = 0; row < totalHeight; row++) surface.DrawText(column, 2 + _PlotHeight - 1 - row, row < successHeight ? successGlyph : failureGlyph, row < successHeight ? successStyle : failureStyle);
                if (_Inspect && index == _Selected) surface.DrawText(left, 2 + _PlotHeight, "^", _Theme.Accent);
            }
            int timeRow = surface.Size.Height - 2;
            string start = _Bars[0].Start.ToString("MM-dd HH:mm", _Localizer.Culture); string finish = _Bars[_Bars.Count - 1].End.ToString("MM-dd HH:mm", _Localizer.Culture);
            surface.DrawText(_PlotLeft, timeRow, start, text); surface.DrawText(Math.Max(_PlotLeft + start.Length + 1, surface.Size.Width - finish.Length), timeRow, finish, text);
            string detail = _Localizer.Phrase("Left/Right: inspect; Enter/click: filter. Adjacent intervals merge to fit.");
            if (_Inspect)
            {
                HistoryActivityBucket bucket = _Bars[_Selected];
                detail = bucket.Start.ToString("MM-dd HH:mm", _Localizer.Culture) + " - " + bucket.End.ToString("MM-dd HH:mm", _Localizer.Culture) + " UTC | " + _Localizer.Text("chart.counts", new Dictionary<string, object?> { ["success"] = bucket.Success, ["failure"] = bucket.Failure });
            }
            surface.DrawText(0, surface.Size.Height - 1, detail, text);
        }
    }
}
