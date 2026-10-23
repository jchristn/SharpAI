namespace SharpAI.Serve
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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
            if (options.LogFormat != "json") return;
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
            string line = JsonSerializer.Serialize(record);
            lock (_Lock)
            {
                if (!_Options.Quiet) Console.WriteLine(line);
                _File?.WriteLine(line);
            }
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
