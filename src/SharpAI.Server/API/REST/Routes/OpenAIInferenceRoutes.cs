namespace SharpAI.Server.API.REST.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    using SharpAI.Models;
    using SharpAI.Models.OpenAI;
    using SharpAI.Security;
    using SharpAI.Server.API.REST.OpenAI;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Registrar for the OpenAI-compatible endpoints: model list (<c>/v1/models</c>) and inference
    /// (<c>/v1/embeddings</c>, <c>/v1/completions</c>, <c>/v1/chat/completions</c>). Inference runs through
    /// <see cref="InferenceErrors.RunInference"/> for error-envelope parity.
    /// </summary>
    internal static class OpenAIInferenceRoutes
    {
        #region Public-Methods

        /// <summary>
        /// Register the OpenAI routes.
        /// </summary>
        /// <param name="ctx">Route context (control-plane configured).</param>
        /// <param name="handler">OpenAI API handler.</param>
        /// <param name="token">Server cancellation token.</param>
        public static void Register(RouteContext ctx, OpenAIApiHandler handler, CancellationToken token)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            ctx.Server.Get("/v1/models", async (req) =>
            {
                ctx.Auth.Authorize(req, ResourceTypes.Model, OperationTypeEnum.Read, null);
                List<object> data = new List<object>();
                foreach (ModelFile m in EnumerateAll(ctx))
                {
                    data.Add(new
                    {
                        id = m.Name,
                        @object = "model",
                        created = new DateTimeOffset(DateTime.SpecifyKind(m.CreatedUtc, DateTimeKind.Utc)).ToUnixTimeSeconds(),
                        owned_by = "sharpai"
                    });
                }
                return new { @object = "list", data = data };
            }, api => RouteHelpers.Describe(api, "OpenAI - Models", "List models (OpenAI-compatible)")
                .WithDescription("OpenAI-compatible model list; returns locally available models.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Model list", OpenApiSchemaMetadata.Create("object"))));

            ctx.Server.Post<OpenAIGenerateEmbeddingsRequest>("/v1/embeddings", async (req) =>
            {
                ctx.Auth.Authorize(req, ResourceTypes.Inference, OperationTypeEnum.Execute, null);
                OpenAIGenerateEmbeddingsRequest ger = req.GetData<OpenAIGenerateEmbeddingsRequest>();
                return await InferenceErrors.RunInference(req, async () =>
                    await handler.GenerateEmbeddings(req, ger, token).ConfigureAwait(false)).ConfigureAwait(false);
            }, api => RouteHelpers.Describe(api, "OpenAI - Inference", "Generate embeddings (OpenAI-compatible)")
                .WithDescription("OpenAI-compatible embeddings endpoint.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Embeddings request", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Embeddings response", OpenApiSchemaMetadata.Create("object"))));

            ctx.Server.Post<OpenAIGenerateCompletionRequest>("/v1/completions", async (req) =>
            {
                ctx.Auth.Authorize(req, ResourceTypes.Inference, OperationTypeEnum.Execute, null);
                return await InferenceErrors.RunInference(req, async () =>
                {
                    OpenAIGenerateCompletionRequest gcr = req.GetData<OpenAIGenerateCompletionRequest>();
                    object ret = await handler.GenerateCompletion(req, gcr, token).ConfigureAwait(false);
                    if (req.Http.Response.ServerSentEvents) return null;
                    else return ret;
                }).ConfigureAwait(false);
            }, api => RouteHelpers.Describe(api, "OpenAI - Inference", "Generate text completion (OpenAI-compatible)")
                .WithDescription("OpenAI-compatible text completion endpoint. Supports streaming via server-sent events.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Completion request", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Completion response", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(429, OpenApiResponseMetadata.Json("Server busy or at capacity — retry later", OpenApiSchemaMetadata.Create("object"))));

            ctx.Server.Post<OpenAIGenerateChatCompletionRequest>("/v1/chat/completions", async (req) =>
            {
                ctx.Auth.Authorize(req, ResourceTypes.Inference, OperationTypeEnum.Execute, null);
                return await InferenceErrors.RunInference(req, async () =>
                {
                    OpenAIGenerateChatCompletionRequest gccr = req.GetData<OpenAIGenerateChatCompletionRequest>();
                    object ret = await handler.GenerateChatCompletion(req, gccr, token).ConfigureAwait(false);
                    if (req.Http.Response.ServerSentEvents) return null;
                    else return ret;
                }).ConfigureAwait(false);
            }, api => RouteHelpers.Describe(api, "OpenAI - Inference", "Generate chat completion (OpenAI-compatible)")
                .WithDescription("OpenAI-compatible chat completion endpoint. Supports streaming via server-sent events.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Chat completion request", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Chat completion response", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(429, OpenApiResponseMetadata.Json("Server busy or at capacity — retry later", OpenApiSchemaMetadata.Create("object"))));
        }

        #endregion

        #region Private-Methods

        private static List<ModelFile> EnumerateAll(RouteContext ctx)
        {
            List<ModelFile> all = new List<ModelFile>();
            EnumerationQuery query = new EnumerationQuery
            {
                PageSize = 1000,
                Order = EnumerationOrderEnum.CreatedDescending
            };

            while (true)
            {
                EnumerationResult<ModelFile> page = ctx.ModelFiles.Enumerate(query);
                if (page.Objects != null && page.Objects.Count > 0) all.AddRange(page.Objects);
                if (page.EndOfResults) break;
                query.PageNumber = query.PageNumber + 1;
            }

            return all;
        }

        #endregion
    }
}
