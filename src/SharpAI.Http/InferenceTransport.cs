namespace SharpAI.Http
{
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Operations;
    using WatsonWebserver.Core;

    /// <summary>HTTP streaming transport with optional host-owned measurement capture. Stateless and thread safe.</summary>
    public class InferenceTransport
    {
        /// <summary>Attach request statistics. The standalone transport retains no history.</summary>
        public virtual void Attach(HttpContextBase context, InferenceStatistics statistics) { }
        /// <summary>Send NDJSON bytes; final closes the stream. Cancellation propagates to the write.</summary>
        public virtual async Task SendChunk(ApiRequest request, byte[] data, bool final, CancellationToken cancellationToken)
        {
            await request.Http.Response.SendChunk(data, final, cancellationToken).ConfigureAwait(false);
        }
        /// <summary>Send an SSE event; final closes the stream. Cancellation propagates to the write.</summary>
        public virtual async Task SendEvent(ApiRequest request, ServerSentEvent data, bool final, CancellationToken cancellationToken)
        {
            await request.Http.Response.SendEvent(data, final, cancellationToken).ConfigureAwait(false);
        }
    }
}
