namespace SharpAI.Server.Classes.Runtime
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Operations;
    using WatsonWebserver.Core;

    /// <summary>Request-scoped diagnostics. Weak keys release buffers after the HTTP context is collected.</summary>
    public static class RequestDiagnostics
    {
        private static readonly ConditionalWeakTable<HttpContextBase, RequestDiagnosticsState> _States = new ConditionalWeakTable<HttpContextBase, RequestDiagnosticsState>();
        /// <summary>Initialize bounded capture before routing. Zero disables retained response content.</summary>
        public static void Begin(HttpContextBase context, int maximumBytes)
        {
            _States.GetValue(context, _ => new RequestDiagnosticsState(maximumBytes));
        }
        /// <summary>Attach live caller-owned engine statistics to this request.</summary>
        public static void Attach(HttpContextBase context, InferenceStatistics statistics)
        {
            _States.GetValue(context, _ => new RequestDiagnosticsState(0)).Statistics = statistics;
        }
        /// <summary>Read recorded diagnostics, or null when capture was not initialized.</summary>
        public static RequestDiagnosticsState Read(HttpContextBase context)
        {
            return _States.TryGetValue(context, out RequestDiagnosticsState state) ? state : null;
        }
        /// <summary>Send an Ollama chunk and retain a bounded diagnostic copy after a successful write.</summary>
        public static async Task SendChunk(ApiRequest request, byte[] data, bool final, CancellationToken token)
        {
            await request.Http.Response.SendChunk(data, final, token).ConfigureAwait(false);
            Read(request.Http)?.Append(data);
        }
        /// <summary>Send an OpenAI event and retain its data for structured history inspection.</summary>
        public static async Task SendEvent(ApiRequest request, ServerSentEvent data, bool final, CancellationToken token)
        {
            await request.Http.Response.SendEvent(data, final, token).ConfigureAwait(false);
            Read(request.Http)?.Append(Encoding.UTF8.GetBytes(data.Data + "\n"));
        }
    }
}
