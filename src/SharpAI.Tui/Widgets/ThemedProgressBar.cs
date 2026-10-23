namespace SharpAI.Tui.Widgets
{
    using System;
    using System.Globalization;
    using SharpAI.Tui.Services;
    using TUIKit;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    internal sealed class ThemedProgressBar : IWidget, IThemeable
    {
        private Theme _Theme = TuiTheme.Create();
        private readonly CultureInfo _Culture;
        private readonly bool _Ascii;
        private double _Value;
        internal ThemedProgressBar(CultureInfo culture, bool ascii) { _Culture = culture; _Ascii = ascii; }
        internal double Value { get { return _Value; } set { _Value = double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0; } }
        /// <inheritdoc/>
        public void ApplyTheme(Theme theme) { _Theme = theme; }
        /// <inheritdoc/>
        public Size Measure(Size available) { return new Size(available.Width, Math.Min(1, available.Height)); }
        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface.Size.Height == 0) return;
            int width = surface.Size.Width;
            int filled = (int)Math.Round(_Value * width);
            string label = _Value.ToString("P0", _Culture);
            int start = Math.Max(0, (width - label.Length) / 2);
            for (int column = 0; column < width; column++)
            {
                bool inLabel = column >= start && column < start + label.Length;
                string glyph = inLabel ? label[column - start].ToString() : column < filled ? (_Ascii ? "#" : "█") : (_Ascii ? "-" : "░");
                CellStyle style = inLabel && column < filled ? new CellStyle(_Theme.Text.Background, _Theme.Accent.Foreground) : column < filled ? _Theme.Accent : _Theme.Muted;
                surface.Set(column, 0, Cell.Glyph(glyph, style, 1));
            }
        }
    }
}
