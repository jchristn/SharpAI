namespace SharpAI.Tui.Services
{
    using System.Text.RegularExpressions;

    internal static class SafeText
    {
        private static readonly Regex _Controls = new Regex(@"\x1B\][^\x07\x1B]*(?:\x07|\x1B\\)|\x1B\[[0-?]*[ -/]*[@-~]|\x1B.|[\x00-\x08\x0B\x0C\x0E-\x1F\x7F-\x9F\u202A-\u202E\u2066-\u2069]", RegexOptions.Compiled);
        internal static string Clean(string? text) { return _Controls.Replace(text ?? string.Empty, string.Empty); }
    }
}
