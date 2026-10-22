namespace SharpAI.Server.Classes.Runtime
{
    using System;
    using System.Text.Json.Nodes;
    using System.Linq;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Text;
    using System.Threading.Tasks;

    using SharpAI.Database;
    using SharpAI.Models;
    using SharpAI.Server.Classes.Settings;

    using SyslogLogging;

    using WatsonWebserver.Core;

    /// <summary>
    /// Captures a durable record of each HTTP request and its response. The entry is built synchronously
    /// from the request context (so request state is not lost) and written on a background task so capture
    /// never blocks the response. Secrets in headers are redacted and bodies are truncated to the
    /// configured limits. Response content is bounded, redacted, and retained along with measured engine phases.
    /// </summary>
    public class RequestHistoryCaptureService
    {
        #region Private-Members

        private readonly string _Header = "[RequestHistoryCapture] ";
        private readonly DatabaseDriverBase _Database;
        private readonly RequestHistorySettings _Settings;
        private readonly LoggingModule _Logging;

        private static readonly HashSet<string> _RedactExact = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "authorization", "proxy-authorization", "cookie", "set-cookie"
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="settings">Request-history settings.</param>
        /// <param name="logging">Logging module.</param>
        public RequestHistoryCaptureService(DatabaseDriverBase database, RequestHistorySettings settings, LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Capture the given request context. Builds the entry synchronously and writes it on a background
        /// task. Never throws.
        /// </summary>
        /// <param name="ctx">Request context.</param>
        public void Capture(HttpContextBase ctx)
        {
            if (ctx == null || !_Settings.Enabled) return;

            try
            {
                RequestHistoryEntry entry = BuildEntry(ctx);
                _ = Task.Run(() =>
                {
                    try
                    {
                        _Database.RequestHistory.Create(entry);
                    }
                    catch (Exception ex)
                    {
                        SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "failed to persist request history:" + Environment.NewLine + ex.ToString());
                    }
                });
            }
            catch (Exception ex)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "failed to build request history entry:" + Environment.NewLine + ex.ToString());
            }
        }

        #endregion

        #region Private-Methods

        private RequestHistoryEntry BuildEntry(HttpContextBase ctx)
        {
            string url = ctx.Request.Url != null ? ctx.Request.Url.RawWithQuery : String.Empty;
            string path = url;
            int queryIndex = url.IndexOf('?');
            if (queryIndex >= 0) path = url.Substring(0, queryIndex);

            RequestHistoryEntry entry = new RequestHistoryEntry
            {
                Method = ctx.Request.Method.ToString(),
                Path = path,
                Url = url,
                StatusCode = ctx.Response.StatusCode,
                DurationMs = ctx.Timestamp.TotalMs.HasValue ? ctx.Timestamp.TotalMs.Value : 0,
                SourceIp = ctx.Request.Source != null ? ctx.Request.Source.IpAddress : null,
                RequestHeaders = ToHeaderDictionary(ctx.Request.Headers),
                ResponseHeaders = ToHeaderDictionary(ctx.Response.Headers),
                CreatedUtc = ctx.Timestamp.Start,
                CompletedUtc = ctx.Timestamp.End
            };

            SharpAI.Security.RequestContext principal = ctx.Metadata as SharpAI.Security.RequestContext;
            entry.TenantId = principal?.TenantGuid;
            entry.UserId = principal?.OwnerUserGuid ?? principal?.PrincipalGuid;
            entry.PrincipalName = principal?.PrincipalType.ToString();
            entry.TraceId = System.Diagnostics.Activity.Current?.TraceId.ToString();
            entry.SpanId = System.Diagnostics.Activity.Current?.SpanId.ToString();
            RequestDiagnosticsState diagnostics = RequestDiagnostics.Read(ctx);
            SharpAI.Operations.InferenceStatistics stats = diagnostics?.Statistics;
            entry.IsStreaming = ctx.Response.ChunkedTransfer || ctx.Response.ServerSentEvents;
            try { entry.ModelName = JsonNode.Parse(ctx.Request.DataAsString ?? "{}")?["model"]?.ToString(); }
            catch (System.Text.Json.JsonException) { }
            if (stats != null)
            {
                entry.InputTokens = stats.PromptTokens;
                entry.OutputTokens = stats.CompletionTokens;
                // The engine does not expose cached-token counts. Null is not a measured zero.
                entry.TtftMs = stats.FirstTokenDurationNanoseconds > 0 ? stats.FirstTokenDurationNanoseconds / 1e6 : null;
                entry.QueueMs = stats.QueueDurationNanoseconds / 1e6;
                entry.LoadMs = stats.LoadDurationNanoseconds / 1e6;
                entry.PromptMs = stats.PromptDurationNanoseconds / 1e6;
                entry.GenerationMs = stats.GenerationDurationNanoseconds / 1e6;
                entry.EngineTotalMs = stats.TotalDurationNanoseconds / 1e6;
                entry.StreamDurationMs = entry.IsStreaming == true && entry.TtftMs.HasValue ? Math.Max(0, entry.DurationMs - entry.TtftMs.Value) : null;
                entry.Cancelled = stats.Cancelled;
                entry.PhaseTimingsApproximate = stats.PhaseTimingsApproximate;
            }
            string response = diagnostics?.ResponseBytes > 0 ? diagnostics.ResponseBody : ctx.Response.DataAsString;
            if (!String.IsNullOrEmpty(response))
            {
                entry.ResponseBodyBytes = diagnostics?.ResponseBytes > 0 ? diagnostics.ResponseBytes : Encoding.UTF8.GetByteCount(response);
                byte[] bytes = Encoding.UTF8.GetBytes(RedactBody(response, path));
                entry.ResponseBodyTruncated = diagnostics?.Truncated == true || bytes.Length > _Settings.MaxResponseBodyBytes;
                entry.ResponseBody = _Settings.MaxResponseBodyBytes == 0 ? null : Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, _Settings.MaxResponseBodyBytes));
            }
            CaptureRequestBody(ctx, entry);
            return entry;
        }

        private void CaptureRequestBody(HttpContextBase ctx, RequestHistoryEntry entry)
        {
            try
            {
                if (ctx.Request.ChunkedTransfer) return;

                string body = ctx.Request.DataAsString;
                if (String.IsNullOrEmpty(body)) return;

                long originalBytes = Encoding.UTF8.GetByteCount(body);
                body = RedactBody(body, entry.Path);
                entry.RequestBodyBytes = originalBytes;

                if (_Settings.MaxRequestBodyBytes <= 0)
                {
                    entry.RequestBody = null;
                    entry.RequestBodyTruncated = originalBytes > 0;
                    return;
                }

                if (Encoding.UTF8.GetByteCount(body) > _Settings.MaxRequestBodyBytes)
                {
                    byte[] retained = Encoding.UTF8.GetBytes(body);
                    entry.RequestBody = Encoding.UTF8.GetString(retained, 0, Math.Min(retained.Length, _Settings.MaxRequestBodyBytes));
                    entry.RequestBodyTruncated = true;
                }
                else
                {
                    entry.RequestBody = body;
                }
            }
            catch
            {
                // Body not available (streaming/chunked); leave it unset.
            }
        }

        private static string RedactBody(string body, string path)
        {
                // Never retain passwords, signing keys, or credential secrets in request history.
                try
                {
                    JsonNode json = JsonNode.Parse(body);
                    void Redact(JsonNode node)
                    {
                        if (node is JsonObject obj)
                        {
                            foreach (string key in System.Linq.Enumerable.ToArray(obj.Select(pair => pair.Key)))
                            {
                                string normalized = key.Replace("_", String.Empty).Replace("-", String.Empty).ToLowerInvariant();
                                if (normalized.Contains("password") || normalized.Contains("secret") || (normalized == "token" || normalized == "tokensigningkey" || normalized == "accesstoken" || normalized == "refreshtoken" || normalized == "bearertoken") || normalized.Contains("apikey") || normalized == "authorization") obj[key] = "[redacted]";
                                else Redact(obj[key]);
                            }
                        }
                        else if (node is JsonArray array) foreach (JsonNode child in array) Redact(child);
                    }
                    Redact(json); body = json?.ToJsonString() ?? body;
                }
                catch
                {
                    if (path.StartsWith("/v1.0/", StringComparison.Ordinal) || path == "/api/settings") body = "[redacted body]";
                }
            return body;
        }

        private Dictionary<string, string> ToHeaderDictionary(NameValueCollection headers)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (headers == null) return result;

            foreach (string key in headers.AllKeys)
            {
                if (String.IsNullOrEmpty(key)) continue;
                result[key] = IsSecretHeader(key) ? "[redacted]" : headers[key];
            }

            return result;
        }

        private static bool IsSecretHeader(string name)
        {
            if (_RedactExact.Contains(name)) return true;
            string lower = name.ToLowerInvariant();
            return lower.Contains("api-key") || lower.Contains("token") || lower.Contains("password") || lower.Contains("secret");
        }

        #endregion
    }
}
