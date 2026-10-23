namespace SharpAI.Serve
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Globalization;
    using System.Text;
    using System.Text.Json;
    using SyslogLogging;

    internal sealed class ServeLogOutput : IDisposable
    {
        private readonly LoggingModule _Logging;
        private readonly ServeOptions _Options;
        private readonly StreamWriter? _File;
        private readonly object _Lock = new object();
        internal ServeLogOutput(LoggingModule logging, ServeOptions options)
        {
            _Logging = logging; _Options = options;
            if (options.LogFile != null) _File = new StreamWriter(options.LogFile, true, new System.Text.UTF8Encoding(false)) { AutoFlush = true };
            logging.MessageLogged += Write;
        }
        private void Write(LogEntry entry)
        {
            Dictionary<string, object?> record = new Dictionary<string, object?>
            {
                ["schema_version"] = 1, ["timestamp"] = entry.Timestamp.ToUniversalTime(),
                ["severity"] = entry.Severity.ToString(), ["service"] = "sharpai",
                ["version"] = ServeVersion.Current, ["event"] = "application.message", ["message"] = entry.Message
            };
            try
            {
                using JsonDocument document = JsonDocument.Parse(entry.Message);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty property in document.RootElement.EnumerateObject())
                        if (property.Name is not ("timestamp" or "severity" or "service" or "version" or "schema_version")) record[property.Name] = property.Value.Clone();
                    if (!document.RootElement.TryGetProperty("event", out JsonElement eventValue) && document.RootElement.TryGetProperty("evt", out JsonElement engineEvent)) record["event"] = engineEvent.Clone();
                    if (!document.RootElement.TryGetProperty("message", out JsonElement messageValue) && document.RootElement.TryGetProperty("msg", out JsonElement engineMessage)) record["message"] = engineMessage.Clone();
                }
            }
            catch (JsonException) { }
            string line = _Options.LogFormat == "json" ? JsonSerializer.Serialize(record) : Text(entry, record);
            lock (_Lock)
            {
                if (!_Options.Quiet) Console.WriteLine(line);
                _File?.WriteLine(line);
            }
        }
        private static string Text(LogEntry entry, Dictionary<string, object?> record)
        {
            string message = Convert.ToString(record["message"], CultureInfo.InvariantCulture) ?? String.Empty;
            StringBuilder text = new StringBuilder(entry.Timestamp.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture) + " " + entry.Severity + " " + message.Replace("\r", "\\r").Replace("\n", "\\n"));
            foreach (KeyValuePair<string, object?> field in record)
            {
                if (field.Key is "schema_version" or "timestamp" or "severity" or "service" or "version" or "event" or "message" or "v" or "evt" or "ts" or "svc" or "inst" or "msg" or "corr" or "event_name") continue;
                if (field.Value is JsonElement value) Append(text, field.Key, value);
            }
            return text.ToString();
        }
        private static void Append(StringBuilder text, string name, JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Null) return;
            if (value.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in value.EnumerateObject()) Append(text, name + "." + property.Name, property.Value);
            }
            else if (value.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (JsonElement item in value.EnumerateArray()) Append(text, name + "[" + (index++).ToString(CultureInfo.InvariantCulture) + "]", item);
            }
            else text.Append(' ').Append(name).Append('=').Append(value.ToString().Replace("\r", "\\r").Replace("\n", "\\n"));
        }
        internal static void Emit(LoggingModule logging, ServeOptions options, string eventName, string message, Dictionary<string, object?> fields)
        {
            if (options.LogFormat == "json")
            {
                fields["event"] = eventName; fields["message"] = message;
                logging.Log(Severity.Debug, JsonSerializer.Serialize(fields));
            }
            else logging.Log(Severity.Debug, message);
        }
        public void Dispose()
        {
            _Logging.MessageLogged -= Write;
            lock (_Lock) _File?.Dispose();
        }
    }
}
