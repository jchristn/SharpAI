namespace SharpAI.Tui.Services
{
    using System.Collections.Generic;

    internal sealed class ConnectionProfile
    {
        public string Server { get; set; } = "http://127.0.0.1:8000";
        public string Locale { get; set; } = "en";
        public string Theme { get; set; } = "dark";
        public string? TokenFile { get; set; }
        public string? Tenant { get; set; }
        public bool Ascii { get; set; }
        public bool TelemetryEnabled { get; set; }
        public string? OtlpEndpoint { get; set; }
        public string? EngineMetrics { get; set; }
        public Dictionary<string, string> ServiceLinks { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, int> PageSizes { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, string[]> Columns { get; set; } = new Dictionary<string, string[]>();
    }
}
