namespace SharpAI.Client
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>HTTP-only SharpAI SDK. No database or inference dependency. Configure before use; do not mutate session state during requests.</summary>
    public sealed partial class ServerClient : IDisposable
    {
        private readonly HttpClient _Http;
        private readonly Uri _Endpoint;
        private readonly bool _OwnsHttp;
        private string? _Token;
        private int _MaxResponseBytes = 4 * 1024 * 1024;
        private int _MaxFrameCharacters = 1024 * 1024;
        private static readonly Meter _Meter = new Meter("SharpAI.Client");
        private static readonly Histogram<double> _Duration = _Meter.CreateHistogram<double>("sharpai.client.request.duration", "s");
        private static readonly ActivitySource _Activities = new ActivitySource("SharpAI.Client");
        /// <summary>Raised after an authentication or permission failure. Handlers must marshal UI changes to their dispatcher.</summary>
        public event Action<HttpStatusCode>? AuthorizationFailed;
        /// <summary>Session bearer token. Memory only; never logged.</summary>
        public string? Token
        {
            get { return _Token; }
            set
            {
                if (value != null && value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character))) throw new ArgumentException("Bearer token must not contain whitespace or control characters.", nameof(value));
                _Token = value;
            }
        }
        /// <summary>Administrator key, when supported by the server.</summary>
        public string? ApiKey { get; set; }
        /// <summary>Requested locale, defaults to English.</summary>
        public string Locale { get; set; } = "en";
        /// <summary>Read/mutation timeout. Defaults to 30 seconds.</summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
        /// <summary>Generation/download timeout, defaults to 30 minutes.</summary>
        public TimeSpan InferenceTimeout { get; set; } = TimeSpan.FromMinutes(30);
        /// <summary>Maximum response inspection bytes. Defaults to 4 MiB.</summary>
        public int MaxResponseBytes
        {
            get { return _MaxResponseBytes; }
            set { if (value < 1 || value >= int.MaxValue) throw new ArgumentOutOfRangeException(nameof(value)); _MaxResponseBytes = value; }
        }
        /// <summary>Maximum UTF-16 characters in one NDJSON/SSE line. Defaults to 1 MiB.</summary>
        public int MaxFrameCharacters
        {
            get { return _MaxFrameCharacters; }
            set { if (value < 1) throw new ArgumentOutOfRangeException(nameof(value)); _MaxFrameCharacters = value; }
        }
        /// <summary>Initialize an HTTP(S) connection. Supplied HttpClient remains owned by its caller.</summary>
        public ServerClient(string endpoint, HttpClient? http = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
            if (!Uri.TryCreate(endpoint.TrimEnd('/') + "/", UriKind.Absolute, out Uri? parsed)
                || (parsed.Scheme != "http" && parsed.Scheme != "https") || !string.IsNullOrEmpty(parsed.UserInfo)
                || !string.IsNullOrEmpty(parsed.Query) || !string.IsNullOrEmpty(parsed.Fragment))
                throw new ArgumentException("Use an absolute HTTP(S) server URL without credentials, query or fragment.", nameof(endpoint));
            _Endpoint = parsed;
            _OwnsHttp = http == null;
            _Http = http ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
        }
        /// <summary>Encode a single opaque path component.</summary>
        public static string Segment(string value) { return Uri.EscapeDataString(value); }
        /// <summary>Encode query values exactly once, omitting null values.</summary>
        public static string Query(IDictionary<string, string?> values)
        {
            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, string?> entry in values)
                if (entry.Value != null) parts.Add(Segment(entry.Key) + "=" + Segment(entry.Value));
            return parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
        }
        /// <summary>Execute once, including mutations. Redirects are never followed by the owned transport.</summary>
        public async Task<ApiResponse> ExecuteAsync(string method, string path, JsonNode? body, CancellationToken cancellationToken, IDictionary<string, string>? headers = null)
        {
            long started = Stopwatch.GetTimestamp();
            using Activity? activity = _Activities.StartActivity("http.request", ActivityKind.Client);
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(IsInference(path) ? InferenceTimeout : RequestTimeout);
            using HttpRequestMessage request = CreateRequest(method, path, body);
            if (headers != null) foreach (KeyValuePair<string, string> header in headers)
            {
                if (header.Value.Any(char.IsControl)) throw new ArgumentException("Request headers must not contain control characters.", nameof(headers));
                request.Headers.Add(header.Key, header.Value);
            }
            using HttpResponseMessage response = await _Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            ApiResponse result = new ApiResponse { StatusCode = response.StatusCode };
            if (!path.StartsWith("/v1.0/auth/context", StringComparison.Ordinal)) ReportAuthorizationFailure(response, request);
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers) result.Headers[header.Key] = string.Join(", ", header.Value);
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers) result.Headers[header.Key] = string.Join(", ", header.Value);
            using Stream stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using MemoryStream buffer = new MemoryStream();
            byte[] chunk = new byte[8192];
            while (true)
            {
                int count = await stream.ReadAsync(chunk.AsMemory(0, Math.Min(chunk.Length, Math.Max(1, MaxResponseBytes + 1 - (int)buffer.Length))), timeout.Token).ConfigureAwait(false);
                if (count == 0) break;
                buffer.Write(chunk, 0, count);
                if (buffer.Length > MaxResponseBytes) { result.Truncated = true; break; }
            }
            result.Body = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, Math.Min((int)buffer.Length, MaxResponseBytes));
            activity?.SetTag("http.response.status_code", (int)response.StatusCode);
            activity?.SetStatus((int)response.StatusCode >= 400 ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
            _Duration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds);
            return result;
        }
        /// <summary>Get a structured response. Non-success and truncated JSON responses fail explicitly.</summary>
        public async Task<JsonNode?> JsonAsync(string method, string path, JsonNode? body, CancellationToken cancellationToken, IDictionary<string, string>? headers = null)
        {
            ApiResponse response = await ExecuteAsync(method, path, body, cancellationToken, headers).ConfigureAwait(false);
            if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300) throw new ApiException(response.StatusCode, response.Body);
            if (response.Truncated) throw new InvalidDataException("JSON response exceeds the inspection limit. Reduce page size.");
            return response.Json();
        }
        /// <summary>Read bounded NDJSON or SSE records without buffering the entire stream. UTF-8 is decoded incrementally.</summary>
        public async IAsyncEnumerable<JsonNode> StreamAsync(string path, JsonNode body, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(InferenceTimeout);
            using HttpRequestMessage request = CreateRequest("POST", path, body);
            using HttpResponseMessage response = await _Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                ReportAuthorizationFailure(response, request);
                throw new ApiException(response.StatusCode, "Streaming request rejected.");
            }
            bool sse = response.Content.Headers.ContentType?.MediaType == "text/event-stream";
            using Stream stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using StreamReader reader = new StreamReader(stream, new UTF8Encoding(false, true));
            StringBuilder line = new StringBuilder();
            StringBuilder data = new StringBuilder();
            char[] characters = new char[4096];
            while (true)
            {
                int count = await reader.ReadAsync(characters.AsMemory(), timeout.Token).ConfigureAwait(false);
                for (int index = 0; index < count; index++)
                {
                    char character = characters[index];
                    if (character != '\n')
                    {
                        line.Append(character);
                        if (line.Length > MaxFrameCharacters) throw new InvalidDataException("Stream frame exceeds the configured limit.");
                        continue;
                    }
                    string record = line.ToString().TrimEnd('\r');
                    line.Clear();
                    if (sse)
                    {
                        if (record.StartsWith("data:", StringComparison.Ordinal))
                        {
                            if (data.Length > 0) data.Append('\n');
                            data.Append(record.Substring(5).TrimStart(' '));
                            if (data.Length > MaxFrameCharacters) throw new InvalidDataException("SSE event exceeds the configured limit.");
                        }
                        if (record.Length != 0) continue;
                        record = data.ToString(); data.Clear();
                    }
                    if (record == "[DONE]") yield break;
                    if (!string.IsNullOrWhiteSpace(record)) yield return ParseFrame(record);
                }
                if (count == 0) break;
            }
            string remaining = line.ToString().TrimEnd('\r');
            if (sse && remaining.StartsWith("data:", StringComparison.Ordinal)) data.Append(remaining.Substring(5).TrimStart(' '));
            string final = sse ? data.ToString() : remaining;
            if (!string.IsNullOrWhiteSpace(final) && final != "[DONE]") yield return ParseFrame(final);
        }
        private void ReportAuthorizationFailure(HttpResponseMessage response, HttpRequestMessage request)
        {
            if (response.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)) return;
            if (!string.Equals(request.Headers.Authorization?.Parameter, Token, StringComparison.Ordinal)) return;
            string? requestKey = request.Headers.TryGetValues("x-api-key", out IEnumerable<string>? values) ? values.FirstOrDefault() : null;
            if (string.Equals(requestKey, ApiKey, StringComparison.Ordinal)) AuthorizationFailed?.Invoke(response.StatusCode);
        }
        private static JsonNode ParseFrame(string record)
        {
            JsonNode node = JsonNode.Parse(record) ?? throw new InvalidDataException("Empty stream record.");
            if (node["error"] != null && !string.IsNullOrWhiteSpace(node["error"]!.ToString())) throw new InvalidDataException("The server reported a streaming error.");
            return node;
        }
        private HttpRequestMessage CreateRequest(string method, string path, JsonNode? body)
        {
            if (!path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal) || path.Contains('\\') || path.Contains('#'))
                throw new ArgumentException("API paths must be server-relative paths.", nameof(path));
            Uri target = new Uri(_Endpoint, _Endpoint.AbsolutePath.TrimEnd('/') + path);
            if (target.Authority != _Endpoint.Authority) throw new ArgumentException("Cross-origin requests are prohibited.", nameof(path));
            HttpRequestMessage request = new HttpRequestMessage(new HttpMethod(method), target);
            request.Headers.AcceptLanguage.ParseAdd(Locale);
            if (Activity.Current?.IdFormat == ActivityIdFormat.W3C)
            {
                request.Headers.TryAddWithoutValidation("traceparent", Activity.Current.Id);
                if (Activity.Current.TraceStateString != null) request.Headers.TryAddWithoutValidation("tracestate", Activity.Current.TraceStateString);
            }
            if (!string.IsNullOrEmpty(Token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            if (!string.IsNullOrEmpty(ApiKey)) request.Headers.Add("x-api-key", ApiKey);
            if (body != null) request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            return request;
        }
        private static bool IsInference(string path)
        {
            return path.StartsWith("/api/chat", StringComparison.Ordinal) || path.StartsWith("/api/generate", StringComparison.Ordinal)
                || path.StartsWith("/api/pull", StringComparison.Ordinal) || path.StartsWith("/api/embed", StringComparison.Ordinal)
                || path.StartsWith("/v1/chat", StringComparison.Ordinal) || path.StartsWith("/v1/completions", StringComparison.Ordinal);
        }
        /// <inheritdoc/>
        public void Dispose() { Token = null; ApiKey = null; if (_OwnsHttp) _Http.Dispose(); }
    }
}
