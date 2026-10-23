namespace SharpAI.Server.API.REST.OpenAI
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Database.Interfaces;
    using SharpAI.Http;
    using SharpAI.Models.OpenAI;
    using SharpAI.Hosting;
    using SharpAI.Serialization;
    using SharpAI.Server.Classes.Runtime;
    using SharpAI.Server.Classes.Settings;
    using SharpAI.Services;
    using SyslogLogging;
    using WatsonWebserver.Core;

    internal class OpenAIApiHandler
    {
        private readonly OpenAIInferenceHandler _Inference;

        internal OpenAIApiHandler(
            Settings settings,
            LoggingModule logging,
            Serializer serializer,
            ModelFileService modelFileService,
            ModelEngineService modelEngineService,
            HuggingFaceClient huggingFaceClient,
            IModelPresetMethods presets)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (logging == null) throw new ArgumentNullException(nameof(logging));
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));
            if (modelFileService == null) throw new ArgumentNullException(nameof(modelFileService));
            if (modelEngineService == null) throw new ArgumentNullException(nameof(modelEngineService));
            if (huggingFaceClient == null) throw new ArgumentNullException(nameof(huggingFaceClient));
            _Inference = new OpenAIInferenceHandler(
                new ManagedInferenceModels(settings, modelFileService, modelEngineService, presets),
                new CapturedInferenceTransport(), logging, serializer);
            SharpAI.Logging.JsonLog.Debug(logging, "[OpenAIApiHandler] initialized");
        }

        internal Task<object> GenerateEmbeddings(ApiRequest request, OpenAIGenerateEmbeddingsRequest body, CancellationToken token = default)
        { return _Inference.GenerateEmbeddings(request, body, token); }
        internal Task<object> GenerateCompletion(ApiRequest request, OpenAIGenerateCompletionRequest body, CancellationToken token = default)
        { return _Inference.GenerateCompletion(request, body, token); }
        internal Task<object> GenerateChatCompletion(ApiRequest request, OpenAIGenerateChatCompletionRequest body, CancellationToken token = default)
        { return _Inference.GenerateChatCompletion(request, body, token); }
    }
}
