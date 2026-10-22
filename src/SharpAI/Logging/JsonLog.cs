namespace SharpAI.Logging
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using System.Text.Json;
    using SyslogLogging;

    /// <summary>Uniform JSON message bodies on console, file, syslog, and telemetry sinks.</summary>
    public static class JsonLog
    {
        private static readonly string _Instance = Guid.NewGuid().ToString();
        /// <summary>Format a message as one JSON document. Multiline exception text is escaped inside msg.</summary>
        public static string Format(string message, string eventName = "application.message")
        {
            if (eventName == "application.message" && message?.StartsWith("[", StringComparison.Ordinal) == true)
            {
                int end = message.IndexOf(']');
                if (end > 1) eventName = "sharpai." + message.Substring(1, end - 1).Replace(" ", "_").ToLowerInvariant() + ".message";
            }
            Dictionary<string, object> record = new Dictionary<string, object>();
            try
            {
                string candidate = message ?? String.Empty;
                int jsonStart = candidate.IndexOf('{');
                if (candidate.StartsWith("[", StringComparison.Ordinal) && jsonStart > 0) candidate = candidate.Substring(jsonStart);
                using JsonDocument document = JsonDocument.Parse(candidate);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty property in document.RootElement.EnumerateObject()) record[property.Name] = property.Value.Clone();
                    if (record.TryGetValue("event_name", out object value)) eventName = value.ToString();
                }
            }
            catch (JsonException) { }
            record["v"] = 1;
            record["evt"] = eventName;
            record["ts"] = DateTime.UtcNow;
            record["svc"] = "sharpai";
            record["inst"] = _Instance;
            if (!record.ContainsKey("msg")) record["msg"] = record.Count > 6 ? eventName : message ?? String.Empty;
            record["trace_id"] = record.ContainsKey("trace_id") ? record["trace_id"] : Activity.Current?.TraceId.ToString();
            record["corr"] = new { id = Activity.Current?.TraceId.ToString() ?? _Instance,
                trace = Activity.Current?.TraceId.ToString(), span = Activity.Current?.SpanId.ToString() };
            return JsonSerializer.Serialize(record);
        }
        /// <summary>Emit one logical JSON message, framing oversized records as reconstructable JSON chunks.</summary>
        public static void Emit(LoggingModule logging, Severity severity, string message)
        {
            if (logging == null) return;
            // Prevent SyslogLogging from splitting one JSON document into invalid string fragments.
            if (logging.Settings.MaxMessageLength < 1024) logging.Settings.MaxMessageLength = 1024;
            string json = Format(message);
            if (Encoding.UTF8.GetByteCount(json) <= Math.Min(8192, logging.Settings.MaxMessageLength) - 100)
            {
                logging.Log(severity, json);
                return;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            string id = Guid.NewGuid().ToString();
            int count = (bytes.Length + 255) / 256;
            for (int i = 0; i < count; i++)
            {
                string frame = JsonSerializer.Serialize(new
                {
                    v = 1, evt = "log.chunk", svc = "sharpai", inst = _Instance,
                    ts = DateTime.UtcNow, msg = "Log fragment", err = new { id },
                    chunk = new { i, n = count, enc = "base64-utf8", data = Convert.ToBase64String(bytes, i * 256, Math.Min(256, bytes.Length - i * 256)) }
                });
                logging.Log(severity, frame);
            }
        }
        /// <summary>Emit a debug event.</summary>
        public static void Debug(LoggingModule logging, string message) { Emit(logging, Severity.Debug, message); }
        /// <summary>Emit an informational event.</summary>
        public static void Info(LoggingModule logging, string message) { Emit(logging, Severity.Info, message); }
        /// <summary>Emit a warning event.</summary>
        public static void Warn(LoggingModule logging, string message) { Emit(logging, Severity.Warn, message); }
        /// <summary>Emit an error event.</summary>
        public static void Error(LoggingModule logging, string message) { Emit(logging, Severity.Error, message); }
        /// <summary>Emit an alert event.</summary>
        public static void Alert(LoggingModule logging, string message) { Emit(logging, Severity.Alert, message); }
    }
}
