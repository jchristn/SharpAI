namespace SharpAI.Server.API.REST.Routes
{
    using System;
    using System.Threading;

    using SharpAI.Models.Ollama;
    using SharpAI.Security;
    using SharpAI.Server.API.REST.Ollama;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Registrar for the Ollama-compatible inference endpoints (<c>/api/embed</c>, <c>/api/generate</c>,
    /// <c>/api/chat</c>). Each handler runs through <see cref="InferenceErrors.RunInference"/> so pre-stream
    /// failures return the Ollama/OpenAI error envelope.
    /// </summary>
    internal static class OllamaInferenceRoutes
    {
        #region Public-Methods

        /// <summary>
        /// Register the Ollama inference routes.
        /// </summary>
        /// <param name="ctx">Route context (control-plane configured).</param>
        /// <param name="handler">Ollama API handler.</param>
        /// <param name="token">Server cancellation token.</param>
        public static void Register(RouteContext ctx, OllamaApiHandler handler, CancellationToken token)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            ctx.Server.Post<OllamaGenerateEmbeddingsRequest>("/api/embed", async (req) =>
            {
                ctx.Auth.Authorize(req, ResourceTypes.Inference, OperationTypeEnum.Execute, null);
                OllamaGenerateEmbeddingsRequest ger = req.GetData<OllamaGenerateEmbeddingsRequest>();
                return await InferenceErrors.RunInference(req, async () =>
                    await handler.GenerateEmbeddings(req, ger, token).ConfigureAwait(false)).ConfigureAwait(false);
            }, api => RouteHelpers.Describe(api, "Ollama - Inference", "Generate embeddings")
                .WithDescription("Generates vector embeddings for a single input or array of inputs.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Embeddings request", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Embeddings", OpenApiSchemaMetadata.Create("object"))));

            ctx.Server.Post<OllamaGenerateCompletionRequest>("/api/generate", async (req) =>
            {
                return await InferenceErrors.RunInference(req, async () =>
                {
                    ctx.Auth.Authorize(req, ResourceTypes.Inference, OperationTypeEnum.Execute, null);
                    OllamaGenerateCompletionRequest gcr = req.GetData<OllamaGenerateCompletionRequest>();
                    object ret = await handler.GenerateCompletion(req, gcr, token).ConfigureAwait(false);
                    if (req.Http.Response.ChunkedTransfer) return null;
                    else return ret;
                }).ConfigureAwait(false);
            }, api => RouteHelpers.Describe(api, "Ollama - Inference", "Generate text completion")
                .WithDescription("Generates a text completion for the given prompt. Supports streaming via chunked transfer.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Completion request", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Completion response", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(429, OpenApiResponseMetadata.Json("Server busy or at capacity — retry later", OpenApiSchemaMetadata.Create("object"))));

            ctx.Server.Post<OllamaGenerateChatCompletionRequest>("/api/chat", async (req) =>
            {
                return await InferenceErrors.RunInference(req, async () =>
                {
                    ctx.Auth.Authorize(req, ResourceTypes.Inference, OperationTypeEnum.Execute, null);
                    OllamaGenerateChatCompletionRequest gccr = req.GetData<OllamaGenerateChatCompletionRequest>();
                    object ret = await handler.GenerateChatCompletion(req, gccr, token).ConfigureAwait(false);
                    if (req.Http.Response.ChunkedTransfer) return null;
                    else return ret;
                }).ConfigureAwait(false);
            }, api => RouteHelpers.Describe(api, "Ollama - Inference", "Generate chat completion")
                .WithDescription("Generates a chat completion from a sequence of messages. Supports streaming via chunked transfer.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Chat completion request", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Chat completion response", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(429, OpenApiResponseMetadata.Json("Server busy or at capacity — retry later", OpenApiSchemaMetadata.Create("object"))));
        }

        #endregion
    }
}
