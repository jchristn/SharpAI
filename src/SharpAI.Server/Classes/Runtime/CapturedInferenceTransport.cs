namespace SharpAI.Server.Classes.Runtime
{
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Http;
    using SharpAI.Operations;
    using WatsonWebserver.Core;

    internal sealed class CapturedInferenceTransport : InferenceTransport
    {
        public override void Attach(HttpContextBase context, InferenceStatistics statistics) { RequestDiagnostics.Attach(context, statistics); }
        public override Task SendChunk(ApiRequest request, byte[] data, bool final, CancellationToken cancellationToken)
        { return RequestDiagnostics.SendChunk(request, data, final, cancellationToken); }
        public override Task SendEvent(ApiRequest request, ServerSentEvent data, bool final, CancellationToken cancellationToken)
        { return RequestDiagnostics.SendEvent(request, data, final, cancellationToken); }
    }
}
