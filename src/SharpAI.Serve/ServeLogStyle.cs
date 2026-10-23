namespace SharpAI.Serve
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using SyslogLogging;

    internal static class ServeLogStyle
    {
        internal static void Normalize(LogEntry entry, Dictionary<string, object?> record)
        {
            string message = Convert.ToString(record["message"], CultureInfo.InvariantCulture) ?? String.Empty;
            string eventName = Convert.ToString(record["event"], CultureInfo.InvariantCulture) ?? String.Empty;
            string subsystem = record.TryGetValue("subsystem", out object? value) ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? "Serve" : entry.Source ?? "Serve";
            if (message.StartsWith("[", StringComparison.Ordinal))
            {
                int end = message.IndexOf(']');
                if (end > 1) { subsystem = message.Substring(1, end - 1); message = message.Substring(end + 1).TrimStart(); }
            }
            if (eventName.StartsWith("engine.", StringComparison.Ordinal))
            {
                subsystem = "LlamaSharpEngine";
                if (message == eventName) message = eventName.Substring("engine.".Length).Replace('.', ' ');
            }
            else if (eventName.StartsWith("request.", StringComparison.Ordinal)) subsystem = "REST";
            if (message == "LlamaSharp provider initialized successfully") message = "initialized LlamaSharp provider successfully";
            record["subsystem"] = subsystem;
            record["message"] = message;
        }
        internal static string Header(DateTime timestamp, string severity, string subsystem)
        {
            return timestamp.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture) + " " + severity + " [" + subsystem + "] ";
        }
        internal static string SingleLine(string value) { return value.Replace("\r", "\\r").Replace("\n", "\\n"); }
    }
}
