namespace SharpAI.Serve
{
    using System;
    using System.Globalization;
    using System.Runtime.CompilerServices;
    using SharpAI.Http;
    using SharpAI.Operations;
    using SyslogLogging;
    using WatsonWebserver.Core;

    internal sealed class ServeRequestLog : InferenceTransport
    {
        private readonly LoggingModule _Logging;
        private readonly ConditionalWeakTable<HttpContextBase, ServeRequestLogState> _States = new ConditionalWeakTable<HttpContextBase, ServeRequestLogState>();
        internal ServeRequestLog(LoggingModule logging) { _Logging = logging; }
        internal void Begin(HttpContextBase context)
        {
            _States.GetValue(context, key => new ServeRequestLogState());
            _Logging.Log(Severity.Debug, "processing " + Identity(context) + " (" + context.Request.ContentLength.ToString(CultureInfo.InvariantCulture) + " bytes)");
        }
        public override void Attach(HttpContextBase context, InferenceStatistics statistics)
        {
            _States.GetValue(context, key => new ServeRequestLogState()).Statistics = statistics;
        }
        internal void Complete(HttpContextBase context)
        {
            if (!_States.TryGetValue(context, out ServeRequestLogState? state)) return;
            _States.Remove(context);
            string details = state.Elapsed.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture) + "ms";
            InferenceStatistics? statistics = state.Statistics;
            if (statistics != null)
            {
                if (statistics.FirstTokenDurationNanoseconds > 0) details += ", TTFT " + (statistics.FirstTokenDurationNanoseconds / 1000000).ToString(CultureInfo.InvariantCulture) + "ms";
                details += ", " + statistics.PromptTokens.ToString(CultureInfo.InvariantCulture) + " input tok, " + statistics.CompletionTokens.ToString(CultureInfo.InvariantCulture) + " output tok";
                if (statistics.Cancelled) details += ", cancelled";
            }
            _Logging.Log(Severity.Debug, "completed " + Identity(context) + ": " + context.Response.StatusCode.ToString(CultureInfo.InvariantCulture) + " (" + details + ")");
        }
        private static string Identity(HttpContextBase context)
        {
            string path = context.Request.Url.RawWithQuery.Split('?')[0];
            // Request paths are untrusted; keep every access event on a single line.
            path = String.Concat(System.Linq.Enumerable.Select(path, character => Char.IsControl(character) ? '?' : character));
            return context.Request.Method + " " + path + " from " + (context.Request.Source?.IpAddress ?? "unknown");
        }
    }
}
