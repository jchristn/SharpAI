namespace SharpAI.Serve
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Classes.Runtime;
    using SharpAI.Http;
    using SharpAI.Operations;
    using SyslogLogging;
    using WatsonWebserver.Core;

    internal sealed class ServeRequestLog : InferenceTransport
    {
        private readonly LoggingModule _Logging;
        private readonly ServeOptions _Options;
        private readonly ConditionalWeakTable<HttpContextBase, ServeRequestLogState> _States = new ConditionalWeakTable<HttpContextBase, ServeRequestLogState>();
        internal ServeRequestLog(LoggingModule logging, ServeOptions options) { _Logging = logging; _Options = options; }
        internal void Begin(HttpContextBase context)
        {
            ServeRequestLogState state = _States.GetValue(context, key => new ServeRequestLogState());
            context.Response.Headers.Add(SharpAI.Constants.RequestIdHeader, state.RequestId);
            if (_Options.LogDetail is "off" or "minimal") return;
            string message = "processing " + Identity(context) + " (" + context.Request.ContentLength.ToString(CultureInfo.InvariantCulture) + " bytes)";
            if (_Options.LogDetail == "detailed" && _Options.LogFormat == "text") message += " request_id=" + state.RequestId;
            ServeLogOutput.Emit(_Logging, _Options, "request.started", message, Fields(context, state));
        }
        internal void Describe(HttpContextBase context, string model, bool stream)
        {
            if (_States.TryGetValue(context, out ServeRequestLogState? state)) { state.Model = model; state.Stream = stream; }
        }
        public override void Attach(HttpContextBase context, InferenceStatistics statistics)
        { _States.GetValue(context, key => new ServeRequestLogState()).Statistics = statistics; }
        public override async Task SendChunk(ApiRequest request, byte[] data, bool final, CancellationToken cancellationToken)
        {
            await base.SendChunk(request, data, final, cancellationToken).ConfigureAwait(false);
            CountWrite(request.Http, data.Length);
        }
        public override async Task SendEvent(ApiRequest request, ServerSentEvent data, bool final, CancellationToken cancellationToken)
        {
            await base.SendEvent(request, data, final, cancellationToken).ConfigureAwait(false);
            CountWrite(request.Http, Encoding.UTF8.GetByteCount(data.Data ?? String.Empty));
        }
        private void CountWrite(HttpContextBase context, int bytes)
        {
            if (!_States.TryGetValue(context, out ServeRequestLogState? state)) return;
            state.StreamChunks++; state.StreamPayloadBytes += bytes;
            if (bytes > 0) { state.FirstStreamWriteMs ??= state.Elapsed.Elapsed.TotalMilliseconds; state.LastStreamWriteMs = state.Elapsed.Elapsed.TotalMilliseconds; }
        }
        internal void Complete(HttpContextBase context)
        {
            if (!_States.TryGetValue(context, out ServeRequestLogState? state)) return;
            _States.Remove(context);
            if (_Options.LogDetail == "off") return;
            Dictionary<string, object?> fields = Fields(context, state);
            fields["status"] = context.Response.StatusCode;
            fields["duration_ms"] = state.Elapsed.Elapsed.TotalMilliseconds;
            string details = state.Elapsed.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture) + "ms";
            InferenceStatistics? statistics = state.Statistics;
            if (_Options.LogDetail != "minimal")
            {
                fields["model"] = state.Model; fields["stream"] = state.Stream;
                fields["statistics_available"] = statistics != null;
                fields["ttft_ms"] = statistics?.FirstTokenDurationNanoseconds > 0 ? statistics.FirstTokenDurationNanoseconds / 1000000.0 : null;
                fields["prompt_tokens"] = statistics?.PromptTokens;
                fields["completion_tokens"] = statistics?.CompletionTokens;
                fields["total_tokens"] = statistics == null ? null : (long)statistics.PromptTokens + statistics.CompletionTokens;
                fields["cancelled"] = statistics?.Cancelled;
                if (statistics != null)
                {
                    if (statistics.FirstTokenDurationNanoseconds > 0) details += ", TTFT " + (statistics.FirstTokenDurationNanoseconds / 1000000).ToString(CultureInfo.InvariantCulture) + "ms";
                    details += ", " + statistics.PromptTokens.ToString(CultureInfo.InvariantCulture) + " input tok, " + statistics.CompletionTokens.ToString(CultureInfo.InvariantCulture) + " output tok";
                    if (statistics.Cancelled) details += ", cancelled";
                }
            }
            if (_Options.LogDetail == "detailed")
            {
                fields["outcome"] = statistics?.Cancelled == true ? "cancelled" : context.Response.StatusCode >= 500 ? "server_error" : context.Response.StatusCode >= 400 ? "client_error" : "ok";
                fields["backend"] = NativeBackendInitializer.SelectedBackend;
                fields["context_size"] = SharpAIEnvironment.GetInt(SharpAIEnvironment.ContextSize, 0, 0);
                fields["parallel"] = _Options.Parallel;
                fields["response_content_length"] = state.StreamChunks > 0 ? null : context.Response.ContentLength;
                fields["stream_chunks"] = state.StreamChunks;
                fields["stream_payload_bytes"] = state.StreamPayloadBytes;
                fields["first_stream_write_ms"] = state.FirstStreamWriteMs;
                fields["stream_duration_ms"] = state.LastStreamWriteMs - state.FirstStreamWriteMs;
                fields["engine_total_ms"] = Ms(statistics?.TotalDurationNanoseconds);
                fields["queue_ms"] = Ms(statistics?.QueueDurationNanoseconds);
                fields["load_ms"] = Ms(statistics?.LoadDurationNanoseconds);
                fields["prompt_eval_ms"] = Ms(statistics?.PromptDurationNanoseconds);
                fields["generation_ms"] = Ms(statistics?.GenerationDurationNanoseconds);
                fields["prompt_tokens_per_second"] = Rate(statistics?.PromptTokens, statistics?.PromptDurationNanoseconds);
                fields["generation_tokens_per_second"] = Rate(statistics?.CompletionTokens, statistics?.GenerationDurationNanoseconds);
                fields["engine_timing_available"] = statistics?.TotalDurationNanoseconds > 0;
                fields["timing_source"] = statistics == null || statistics.TotalDurationNanoseconds <= 0 ? null : statistics.PhaseTimingsApproximate ? "backend_step_wall_clock" : "native";
                fields["trace_id"] = statistics?.TraceId; fields["span_id"] = statistics?.SpanId;
                if (_Options.LogFormat == "text")
                    foreach (KeyValuePair<string, object?> field in fields)
                        if (field.Key is not ("method" or "path" or "source_ip" or "request_content_length" or "status" or "duration_ms" or "prompt_tokens" or "completion_tokens" or "ttft_ms"))
                            details += ", " + field.Key + "=" + (Convert.ToString(field.Value, CultureInfo.InvariantCulture) ?? "unavailable");
            }
            ServeLogOutput.Emit(_Logging, _Options, "request.completed", "completed " + Identity(context) + ": " + context.Response.StatusCode.ToString(CultureInfo.InvariantCulture) + " (" + details + ")", fields);
        }
        private static double? Ms(long? nanoseconds) { return nanoseconds.HasValue ? nanoseconds.Value / 1000000.0 : null; }
        private static double? Rate(int? tokens, long? nanoseconds) { return nanoseconds > 0 && tokens.HasValue ? tokens.Value * 1000000000.0 / nanoseconds.Value : null; }
        private static Dictionary<string, object?> Fields(HttpContextBase context, ServeRequestLogState state)
        {
            return new Dictionary<string, object?> { ["request_id"] = state.RequestId, ["method"] = context.Request.Method.ToString(), ["path"] = Path(context), ["source_ip"] = context.Request.Source?.IpAddress, ["request_content_length"] = context.Request.ContentLength };
        }
        private static string Path(HttpContextBase context)
        {
            string path = context.Request.Url.RawWithQuery.Split('?')[0];
            return String.Concat(System.Linq.Enumerable.Select(path, character => Char.IsControl(character) ? '?' : character));
        }
        private static string Identity(HttpContextBase context) { return context.Request.Method + " " + Path(context) + " from " + (context.Request.Source?.IpAddress ?? "unknown"); }
    }
}
