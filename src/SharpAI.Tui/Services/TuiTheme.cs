namespace SharpAI.Tui.Services
{
    using TUIKit;
    using TUIKit.Theming;

    internal static class TuiTheme
    {
        internal static Theme Create(string name = "dark", bool ascii = false)
        {
            if (name is "light" or "contrast")
            {
                Theme source = name == "light" ? Theme.Light : Theme.HighContrast;
                if (!ascii || source.UseAsciiBorders) return source;
                Theme copy = new Theme(source.Name, source.Text, source.Accent, source.Border, source.Muted, true,
                    source.Success, source.Warning, source.Error, source.Info, source.Selection, source.Disabled);
                foreach (string role in Roles) if (source.HasStyle(role)) copy.SetStyle(role, source.GetStyle(role));
                return copy;
            }

            Color background = Color.FromRgb(0x11, 0x10, 0x1A);
            Color panel = Color.FromRgb(0x19, 0x16, 0x24);
            Color foreground = Color.FromRgb(0xF4, 0xF0, 0xFA);
            Color lavender = Color.FromRgb(0xBD, 0xA9, 0xF3);
            CellStyle text = new CellStyle(foreground, background);
            CellStyle accent = new CellStyle(lavender, background);
            CellStyle muted = new CellStyle(Color.FromRgb(0xB3, 0xA6, 0xC4), background);
            CellStyle selection = new CellStyle(lavender, Color.FromRgb(0x35, 0x25, 0x4F));
            Theme theme = new Theme("Midnight Violet", text, accent,
                new CellStyle(Color.FromRgb(0x65, 0x55, 0x7E), background), muted, ascii,
                success: new CellStyle(Color.FromRgb(0x8A, 0xCD, 0xB3), background),
                warning: new CellStyle(Color.FromRgb(0xE4, 0xC1, 0x8A), background),
                error: new CellStyle(Color.FromRgb(0xEF, 0x85, 0xB9), background),
                info: accent, selection: selection, disabled: muted);
            theme.SetStyle(Theme.SidebarRole, text.WithBackground(panel));
            theme.SetStyle(Theme.StatusBarRole, muted.WithBackground(panel));
            theme.SetStyle(Theme.FocusBorderRole, accent.WithAttribute(CellAttributes.Bold, true));
            theme.SetStyle(Theme.FocusTitleRole, accent.WithAttribute(CellAttributes.Bold, true));
            theme.SetStyle(Theme.ButtonFocusedRole, selection.WithAttribute(CellAttributes.Bold, true));
            theme.SetStyle(Theme.TabFocusedRole, selection.WithAttribute(CellAttributes.Underline, true));
            theme.SetStyle(Theme.InlineButtonHoverRole, accent.WithAttribute(CellAttributes.Underline, true));
            theme.SetStyle(Theme.SearchMatchRole, new CellStyle(background, lavender).WithAttribute(CellAttributes.Bold, true));
            theme.SetStyle(Theme.ToastRole, text.WithBackground(panel));
            theme.SetStyle(Theme.MarkdownHeadingRole, accent.WithAttribute(CellAttributes.Bold, true));
            theme.SetStyle(Theme.MarkdownSubheadingRole, accent.WithAttribute(CellAttributes.Bold, true));
            theme.SetStyle(Theme.MarkdownCodeRole, text.WithBackground(panel));
            theme.SetStyle(Theme.MarkdownInlineCodeRole, accent.WithBackground(panel));
            theme.SetStyle(Theme.MarkdownQuoteRole, muted);
            return theme;
        }

        private static readonly string[] Roles =
        {
            Theme.SidebarRole, Theme.StatusBarRole, Theme.FocusBorderRole, Theme.FocusTitleRole,
            Theme.ButtonFocusedRole, Theme.TabFocusedRole, Theme.InlineButtonHoverRole, Theme.SearchMatchRole,
            Theme.ToastRole, Theme.MarkdownHeadingRole, Theme.MarkdownSubheadingRole, Theme.MarkdownCodeRole,
            Theme.MarkdownInlineCodeRole, Theme.MarkdownQuoteRole
        };
    }
}
