namespace Test.Xunit
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Database;
    using SharpAI.Database.Sqlite;
    using SharpAI.Logging;
    using SharpAI.Models;
    using SyslogLogging;
    using Xunit;

    /// <summary>Durable diagnostic fields and uniformly parseable logging regressions.</summary>
    public sealed class HistoryTelemetryTests
    {
        /// <summary>All timing/usage fields survive restart while list responses omit payload bodies.</summary>
        [Fact]
        public async Task TelemetryRoundTripsAndReopens()
        {
            CancellationToken token = CancellationToken.None;
            string filename = Path.Combine(Path.GetTempPath(), "sharpai-history-" + Guid.NewGuid().ToString("N") + ".db");
            using LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;
            try
            {
                RequestHistoryEntry entry = new RequestHistoryEntry
                {
                    ModelName = "test-model", InputTokens = 12, OutputTokens = 3, CachedTokens = null,
                    IsStreaming = true, TtftMs = 123.5, StreamDurationMs = 600, QueueMs = 2, LoadMs = 10,
                    PromptMs = 100, GenerationMs = 600, EngineTotalMs = 712, Cancelled = false,
                    PhaseTimingsApproximate = true, TraceId = "trace-1", SpanId = "span-1",
                    RequestBody = "request payload", ResponseBody = "response payload", ResponseBodyBytes = 16
                };
                using (SqliteDatabaseDriver db = new SqliteDatabaseDriver(new DatabaseSettings { Filename = filename }, logging))
                {
                    await db.InitializeAsync(token);
                    db.RequestHistory.Create(entry);
                    RequestHistoryEntry list = db.RequestHistory.Enumerate(new RequestHistoryQuery()).Objects.Single();
                    Assert.Null(list.ResponseBody);
                    Assert.Equal(12, list.InputTokens);
                    Assert.Null(list.CachedTokens);
                    Assert.Equal(123.5, list.TtftMs);
                }
                using (SqliteDatabaseDriver db = new SqliteDatabaseDriver(new DatabaseSettings { Filename = filename }, logging))
                {
                    await db.InitializeAsync(token);
                    RequestHistoryEntry result = db.RequestHistory.Read(entry.Id);
                    Assert.Equal(JsonSerializer.Serialize(entry), JsonSerializer.Serialize(result));
                }
            }
            finally { if (File.Exists(filename)) File.Delete(filename); }
        }

        /// <summary>Existing engine payload fields stay queryable instead of being escaped inside a message string.</summary>
        [Fact]
        public void PrefixedEngineJsonRetainsItsFields()
        {
            string input = "[EngineOperation] " + JsonSerializer.Serialize(new { event_name = "engine.inference.completed", trace_id = "0123456789abcdef0123456789abcdef", prompt_tokens = 12 });
            using JsonDocument record = JsonDocument.Parse(JsonLog.Format(input));
            Assert.Equal("engine.inference.completed", record.RootElement.GetProperty("evt").GetString());
            Assert.Equal(12, record.RootElement.GetProperty("prompt_tokens").GetInt32());
            Assert.Equal("0123456789abcdef0123456789abcdef", record.RootElement.GetProperty("trace_id").GetString());
        }

        /// <summary>Multiline and non-ASCII events remain JSON and oversized messages can be reconstructed exactly.</summary>
        [Fact]
        public void LogBodiesAreJsonAndLongMessagesReassemble()
        {
            using LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;
            List<string> bodies = new List<string>();
            logging.MessageLogged += entry => bodies.Add(entry.Message);
            string text = "[Tests] failure\n" + String.Concat(Enumerable.Repeat("日本語🙂", 1000));
            JsonLog.Warn(logging, text);
            Assert.True(bodies.Count > 1);
            using MemoryStream assembled = new MemoryStream();
            foreach (string body in bodies)
            {
                using JsonDocument parsed = JsonDocument.Parse(body);
                Assert.Equal("log.chunk", parsed.RootElement.GetProperty("evt").GetString());
                byte[] chunk = Convert.FromBase64String(parsed.RootElement.GetProperty("chunk").GetProperty("data").GetString()!);
                assembled.Write(chunk, 0, chunk.Length);
            }
            using JsonDocument record = JsonDocument.Parse(Encoding.UTF8.GetString(assembled.ToArray()));
            Assert.Equal(text, record.RootElement.GetProperty("msg").GetString());
            Assert.Equal("sharpai.tests.message", record.RootElement.GetProperty("evt").GetString());
        }
    }
}
