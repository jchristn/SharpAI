namespace Test.Shared.Tui
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Client;
    using SharpAI.Tui;
    using SharpAI.Tui.Services;
    using SharpAI.Tui.Widgets;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Terminal;
    using TUIKit.Theming;

    /// <summary>Brand colors reach rendered screens, charts, dialogs and ASCII terminals.</summary>
    public static class TuiThemeSuite
    {
        /// <summary>Build palette and operator checks.</summary>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor("TuiTheme", "Midnight Violet", new[]
            {
                new TestCaseDescriptor("TuiTheme", "Roles", "Readable brand roles survive ASCII mode; alternate themes remain available", RolesAsync),
                new TestCaseDescriptor("TuiTheme", "Surfaces", "All routes and the credential modal use the shared palette", SurfacesAsync,
                    skip: string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SHARPAI_TUI_TEST_URL")), skipReason: "Start the checked Postman TUI simulation.")
            });
        }

        private static Task RolesAsync(CancellationToken ct)
        {
            Theme theme = TuiTheme.Create(); Theme ascii = TuiTheme.Create("dark", true);
            TestAssert.Equal("Midnight Violet", theme.Name);
            TestAssert.Equal(Color.FromRgb(0x11, 0x10, 0x1A), theme.Text.Background);
            TestAssert.Equal(Color.FromRgb(0xBD, 0xA9, 0xF3), theme.Accent.Foreground);
            TestAssert.Equal(Color.FromRgb(0xEF, 0x85, 0xB9), theme.Error.Foreground);
            TestAssert.True(ascii.UseAsciiBorders, "ASCII borders remain available");
            foreach (CellStyle style in new[] { theme.Text, theme.Accent, theme.Muted, theme.Success, theme.Warning, theme.Error, theme.Selection })
                TestAssert.True(Contrast(style.Foreground, style.Background) >= 4.5, "Text and semantic roles retain readable contrast");
            TestAssert.Equal(theme.Selection, ascii.Selection); TestAssert.Equal(theme.Error, ascii.Error);
            foreach (string role in new[] { Theme.FocusBorderRole, Theme.SidebarRole, Theme.MarkdownCodeRole, Theme.SearchMatchRole, Theme.ToastRole })
                TestAssert.Equal(theme.GetStyle(role), ascii.GetStyle(role));
            foreach (bool asciiMode in new[] { false, true })
            {
                ThemedProgressBar progress = new ThemedProgressBar(System.Globalization.CultureInfo.GetCultureInfo("en"), asciiMode) { Value = 0.5 };
                progress.ApplyTheme(theme); CellBuffer cells = new CellBuffer(40, 1); progress.Render(new BufferSurface(cells));
                StringBuilder label = new StringBuilder();
                for (int column = 0; column < 40; column++) label.Append(cells.Get(column, 0).Grapheme);
                TestAssert.Contains(label.ToString(), "50%");
                TestAssert.Equal(theme.Accent, cells.Get(0, 0).Style); TestAssert.Equal(theme.Muted, cells.Get(39, 0).Style);
                TestAssert.Equal(asciiMode ? "#" : "█", cells.Get(0, 0).Grapheme);
                progress.Value = 2; TestAssert.Equal(1.0, progress.Value);
                progress.Value = double.NaN; TestAssert.Equal(0.0, progress.Value);
            }
            foreach (string name in new[] { "light", "contrast" })
            {
                Theme normal = TuiTheme.Create(name); Theme fallback = TuiTheme.Create(name, true);
                TestAssert.Equal(normal.Success, fallback.Success); TestAssert.Equal(normal.Selection, fallback.Selection);
                TestAssert.Equal(normal.GetStyle(Theme.FocusBorderRole), fallback.GetStyle(Theme.FocusBorderRole));
            }
            return Task.CompletedTask;
        }

        private static double Contrast(Color foreground, Color background)
        {
            double front = Luminance(foreground); double back = Luminance(background);
            return (Math.Max(front, back) + 0.05) / (Math.Min(front, back) + 0.05);
        }
        private static double Luminance(Color color) { return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B); }
        private static double Channel(byte value) { double channel = value / 255.0; return channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4); }

        private static async Task SurfacesAsync(CancellationToken ct)
        {
            foreach (bool ascii in new[] { false, true })
            {
                string endpoint = Environment.GetEnvironmentVariable("SHARPAI_TUI_TEST_URL")!;
                using ServerClient client = new ServerClient(endpoint);
                using HeadlessBackend backend = new HeadlessBackend(120, 40) { ClaimsTerminal = false };
                using TestProfileStore store = new TestProfileStore();
                using SharpAITuiApp app = new SharpAITuiApp(client, backend, new ConnectionProfile { Ascii = ascii }, store.Store);
                app.Host.Start(); await app.ConnectAsync().ConfigureAwait(false); await app.DrainAsync(ct).ConfigureAwait(false);
                foreach (string route in new[] { "/", "/models", "/system-prompts", "/model-configurations", "/playground", "/request-history", "/request-history/inference", "/request-history/embeddings", "/api-explorer", "/observability", "/access/tenants", "/access/users", "/access/credentials", "/access/roles", "/access/permissions", "/access/assignments", "/access/sessions", "/access/audit", "/settings" })
                {
                    app.Navigate(route); await app.DrainAsync(ct).ConfigureAwait(false);
                    Validate(app, route + (ascii ? "-ascii" : ""));
                }
                SecretModal modal = new SecretModal("fixture-token", value => { });
                Task<object?> closing = app.Host.ShowAsync(modal); app.Host.RenderOnce(); Validate(app, "credential-dialog" + (ascii ? "-ascii" : ""));
                backend.FeedKey("Escape"); app.Host.PumpInputOnce(); app.Host.PumpInputOnce(); await closing.WaitAsync(ct).ConfigureAwait(false);
                backend.Resize(80, 24); app.Navigate("/request-history"); await app.DrainAsync(ct).ConfigureAwait(false); Validate(app, "compact" + (ascii ? "-ascii" : ""));
                backend.FeedKey("F2"); app.Host.PumpInputOnce(); Validate(app, "navigation-drawer" + (ascii ? "-ascii" : ""));
            }
        }

        private static void Validate(SharpAITuiApp app, string name)
        {
            app.Host.RenderOnce(); CellBuffer frame = app.Host.CaptureFrame()!; ValidatePadding(frame, name); Theme theme = app.Host.Theme;
            HashSet<Color> allowed = new HashSet<Color> { Color.Default, Color.FromRgb(0, 0, 0) };
            foreach (CellStyle style in new[] { theme.Text, theme.Accent, theme.Border, theme.Muted, theme.Success, theme.Warning, theme.Error, theme.Info, theme.Selection, theme.GetStyle(Theme.SidebarRole), theme.GetStyle(Theme.StatusBarRole) })
            { allowed.Add(style.Foreground); allowed.Add(style.Background); }
            StringBuilder html = new StringBuilder("<!doctype html><meta charset=\"utf-8\"><style>body{background:#11101A;color:#F4F0FA}pre{font:14px/1.2 monospace}</style><pre>");
            for (int row = 0; row < frame.Height; row++)
            {
                for (int column = 0; column < frame.Width; column++)
                {
                    Cell cell = frame.Get(column, row);
                    TestAssert.True(allowed.Contains(cell.Style.Foreground) && allowed.Contains(cell.Style.Background), "Unbranded color on " + name + " at " + column + "," + row + " glyph " + cell.Grapheme + ": " + cell.Style.Foreground + " / " + cell.Style.Background);
                    if (cell.IsContinuation) continue;
                    Color foreground = cell.Style.Foreground.Kind == ColorKind.Default ? theme.Text.Foreground : cell.Style.Foreground;
                    Color background = cell.Style.Background.Kind == ColorKind.Default ? theme.Text.Background : cell.Style.Background;
                    if ((cell.Style.Attributes & CellAttributes.Reverse) != 0) { Color temporary = foreground; foreground = background; background = temporary; }
                    html.Append("<span style=\"color:").Append(Hex(foreground)).Append(";background:").Append(Hex(background)).Append("\">").Append(WebUtility.HtmlEncode(cell.Grapheme)).Append("</span>");
                }
                html.AppendLine();
            }
            html.Append("</pre>"); string? directory = Environment.GetEnvironmentVariable("SHARPAI_TUI_RECORDINGS");
            if (directory != null) { Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, "theme-" + name.Replace('/', '_') + ".html"), html.ToString()); }
        }
        internal static void ValidatePadding(CellBuffer frame, string name)
        {
            const string borders = "│┃║─━═┌┐└┘┏┓┗┛├┤┬┴┼┣┫┳┻╋╭╮╰╯╞╡+|-#";
            for (int row = 0; row < frame.Height; row++)
            {
                for (int column = 0; column < frame.Width; column++)
                {
                    string glyph = frame.Get(column, row).Grapheme;
                    if (glyph is not ("│" or "┃" or "║" or "|")) continue;
                    foreach (int adjacent in new[] { column - 1, column + 1 })
                    {
                        if (adjacent < 0 || adjacent >= frame.Width) continue;
                        Cell neighbor = frame.Get(adjacent, row);
                        bool blank = !neighbor.IsContinuation && string.IsNullOrWhiteSpace(neighbor.Grapheme);
                        bool line = neighbor.Grapheme.Length == 1 && borders.Contains(neighbor.Grapheme, StringComparison.Ordinal);
                        TestAssert.True(blank || line, "Content touches a vertical line on " + name + " at " + column + "," + row + ": " + neighbor.Grapheme);
                    }
                }
            }
        }
        private static string Hex(Color color) { return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2"); }
    }
}
