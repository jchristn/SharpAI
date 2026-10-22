namespace SharpAI.Server.API.REST.Routes
{
    using System;
    using SharpAI.Security;
    using SharpAI.Server.Classes.Settings;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Registrar for the server settings endpoints (<c>GET/PUT /api/settings</c>). Extracted from the
    /// composition root as part of the W7 registrar refactor.
    /// </summary>
    public static class SettingsRoutes
    {
        #region Public-Methods

        /// <summary>
        /// Register the settings routes on the context's webserver.
        /// </summary>
        /// <param name="ctx">Route context. Must have control-plane dependencies configured.</param>
        public static void Register(RouteContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            ctx.Server.Get("/api/diagnostics/runtime", async (req) =>
            {
                ctx.Auth.Authorize(req, ResourceTypes.Admin, OperationTypeEnum.Admin, null);
                return new
                {
                    backend_selected = SharpAI.Classes.Runtime.NativeBackendInfo.SelectedBackend,
                    gpu_offload_verified = false,
                    gpu_offload_note = "Backend selection does not prove tensor placement. Consult native model-load logs.",
                    framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                    os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                    architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                    processors = Environment.ProcessorCount,
                    apis = new string[] { "ollama", "openai" },
                    generation_slots = SharpAI.Classes.Runtime.SharpAIEnvironment.GetInt(SharpAI.Classes.Runtime.SharpAIEnvironment.MaxConcurrentGenerations, 1, 1),
                    context_override = SharpAI.Classes.Runtime.SharpAIEnvironment.GetInt(SharpAI.Classes.Runtime.SharpAIEnvironment.ContextSize, 0, 0),
                    llamasharp_version = SharpAI.Classes.Runtime.NativeBackendInfo.LlamaSharpVersion
                };
            }, api => RouteHelpers.Describe(api, "Diagnostics", "Runtime capabilities and configuration")
                .WithResponse(200, OpenApiResponseMetadata.Json("Runtime diagnostics", OpenApiSchemaMetadata.Create("object"))));

            ctx.Server.Get("/api/diagnostics/memory", async (req) =>
            {
                ctx.Auth.Authorize(req, ResourceTypes.Admin, OperationTypeEnum.Admin, null);
                using System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess();
                return new
                {
                    working_set_bytes = process.WorkingSet64, peak_working_set_bytes = process.PeakWorkingSet64,
                    managed_heap_bytes = GC.GetTotalMemory(false), gc_available_bytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
                    model_budget_bytes = ctx.ModelEngines.ModelMemoryBudgetBytes,
                    models = ctx.ModelEngines.GetLoadedModelPaths().ConvertAll(path => SharpAI.Operations.ModelInspector.Inspect(path)),
                    per_model_observed_available = false,
                    per_model_observed_bytes = (long?)null,
                    attribution_note = "Shared native allocations cannot be reliably attributed to individual models. Estimates are not observations."
                };
            }, api => RouteHelpers.Describe(api, "Diagnostics", "Process memory and model estimates")
                .WithResponse(200, OpenApiResponseMetadata.Json("Memory diagnostics", OpenApiSchemaMetadata.Create("object"))));

            ctx.Server.Post<SharpAI.Models.Ollama.OllamaShowModelInfoRequest>("/api/inspect", async (req) =>
            {
                ctx.Auth.Authorize(req, ResourceTypes.Model, OperationTypeEnum.Read, null);
                SharpAI.Models.Ollama.OllamaShowModelInfoRequest request = req.GetData<SharpAI.Models.Ollama.OllamaShowModelInfoRequest>();
                if (String.IsNullOrWhiteSpace(request?.Model)) throw new WebserverException(ApiResultEnum.BadRequest, "A model name is required.");
                SharpAI.Models.ModelFile model = ctx.ModelFiles.GetByName(request.Model);
                if (model == null) throw new WebserverException(ApiResultEnum.NotFound, "Model not found.");
                return new { model = model.Name, sha256 = model.SHA256Hash,
                    inspection = SharpAI.Operations.ModelInspector.Inspect(SharpAI.Operations.GgufShards.Resolve(ctx.Settings().Storage.ModelsDirectory, model.GUID)) };
            }, api => RouteHelpers.Describe(api, "Models", "Inspect a registered model by name without loading weights")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Model name", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Model preflight", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(400, OpenApiResponseMetadata.Json("Missing model name", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(404, OpenApiResponseMetadata.Json("Model not found", OpenApiSchemaMetadata.Create("object"))));

            ctx.Server.Get("/api/models/{id}/inspect", async (req) =>
            {
                ctx.Auth.Authorize(req, ResourceTypes.Model, OperationTypeEnum.Read, null);
                string id = RouteHelpers.RouteParam(req, "id");
                if (!Guid.TryParse(id, out Guid guid)) throw new WebserverException(ApiResultEnum.BadRequest, "A model GUID is required.");
                SharpAI.Models.ModelFile model = ctx.ModelFiles.GetByGuid(guid);
                if (model == null) throw new WebserverException(ApiResultEnum.NotFound, "Model not found.");
                return SharpAI.Operations.ModelInspector.Inspect(SharpAI.Operations.GgufShards.Resolve(ctx.Settings().Storage.ModelsDirectory, guid));
            }, api => RouteHelpers.Describe(api, "Models", "Inspect GGUF metadata and memory without loading weights")
                .WithResponse(200, OpenApiResponseMetadata.Json("Model preflight", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(400, OpenApiResponseMetadata.Json("Invalid identifier", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(404, OpenApiResponseMetadata.Json("Model not found", OpenApiSchemaMetadata.Create("object"))));

            ctx.Server.Get("/api/settings", async (req) =>
            {
                ctx.Auth.Authorize(req, ResourceTypes.Settings, OperationTypeEnum.Read, null);
                return ctx.Settings();
            }, api => RouteHelpers.Describe(api, "Settings", "Get current server settings")
                .WithDescription("Returns the current in-memory server settings loaded from sharpai.json.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Current settings", OpenApiSchemaMetadata.Create("object"))));

            ctx.Server.Put<Settings>("/api/settings", async (req) =>
            {
                ctx.Auth.Authorize(req, ResourceTypes.Admin, OperationTypeEnum.Admin, null);
                Settings updated = req.GetData<Settings>();
                if (updated == null) throw new WebserverException(ApiResultEnum.BadRequest, "Request body is required.");

                Settings current = ctx.Settings();
                updated.CreatedUtc = current.CreatedUtc;
                updated.SoftwareVersion = current.SoftwareVersion;

                ctx.ReplaceSettings(updated);
                ctx.Serializer.SerializeJsonToFile(ctx.SettingsFilePath, updated, true);

                return updated;
            }, api => RouteHelpers.Describe(api, "Settings", "Update server settings")
                .WithDescription(
                    "Replaces the in-memory server settings and rewrites sharpai.json on disk. " +
                    "CreatedUtc and SoftwareVersion are preserved from the current settings. " +
                    "Some settings (REST hostname, port, SSL, Database) require a server restart to take effect.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Updated settings", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Updated settings", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest()));
        }

        #endregion
    }
}
