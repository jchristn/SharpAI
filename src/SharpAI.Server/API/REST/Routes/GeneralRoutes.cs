namespace SharpAI.Server.API.REST.Routes
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    using SharpAI.Server.Classes.Runtime;
    using SharpAI.Server.Classes.Settings;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    using Constants = SharpAI.Constants;

    /// <summary>
    /// Registrar for the general, unauthenticated server endpoints: homepage, liveness (<c>/health</c>),
    /// readiness (<c>/ready</c>), and the favicon. First registrar extracted from the composition root as
    /// part of the W7 backend-architecture refactor (per-feature registrars over a shared
    /// <see cref="RouteContext"/>); additional groups follow the same pattern.
    /// </summary>
    public static class GeneralRoutes
    {
        #region Public-Methods

        /// <summary>
        /// Register the general routes on the context's webserver.
        /// </summary>
        /// <param name="ctx">Route context carrying the server and runtime services. Required.</param>
        public static void Register(RouteContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            ctx.Server.Get("/", async (req) =>
            {
                req.Http.Response.ContentType = Constants.HtmlContentType;
                return Constants.HtmlHomepage;
            }, api => RouteHelpers.Describe(api, "General", "Server homepage")
                .WithDescription("Returns the default HTML homepage indicating the node is operational.")
                .WithResponse(200, OpenApiResponseMetadata.Text("Operational HTML page")));

            ctx.Server.Head("/", async (req) => null, api => RouteHelpers.Describe(api, "General", "Server liveness check")
                .WithDescription("HEAD probe that returns 200 OK when the server is reachable."));

            ctx.Server.Get("/health", async (req) =>
            {
                req.Http.Response.ContentType = Constants.JsonContentType;
                return new
                {
                    status = "healthy",
                    version = ctx.Version,
                    backend = NativeLibraryBootstrapper.SelectedBackend,
                    native_initialized = NativeLibraryBootstrapper.IsInitialized,
                    utc = DateTime.UtcNow
                };
            }, api => RouteHelpers.Describe(api, "General", "Health check")
                .WithDescription("Lightweight liveness check for container and process monitoring.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Health status", OpenApiSchemaMetadata.Create("object"))));

            ctx.Server.Head("/health", async (req) => null, api => RouteHelpers.Describe(api, "General", "Health HEAD probe"));

            ctx.Server.Get("/ready", async (req) =>
            {
                Settings currentSettings = ctx.Settings();

                bool modelsDirectoryReady = RouteHelpers.DirectoryExistsAndWritable(currentSettings?.Storage?.ModelsDirectory);

                bool logsDirectoryReady = RouteHelpers.DirectoryExistsAndWritable(currentSettings?.Logging?.LogDirectory);

                // Telemetry readiness: satisfied when telemetry is disabled, or the host has started.
                bool telemetryReady = currentSettings?.Telemetry == null
                    || !currentSettings.Telemetry.Enable
                    || (ctx.Telemetry != null && ctx.Telemetry.IsActive);

                bool ready = NativeLibraryBootstrapper.IsInitialized
                    && ctx.Database != null && ctx.Database.IsInitialized
                    && ctx.ModelFiles != null
                    && ctx.ModelEngines != null
                    && modelsDirectoryReady
                    && logsDirectoryReady
                    && telemetryReady;

                req.Http.Response.ContentType = Constants.JsonContentType;
                if (!ready) req.Http.Response.StatusCode = 503;

                return new
                {
                    status = ready ? "ready" : "not_ready",
                    version = ctx.Version,
                    backend = NativeLibraryBootstrapper.SelectedBackend,
                    native_initialized = NativeLibraryBootstrapper.IsInitialized,
                    database_initialized = ctx.Database != null && ctx.Database.IsInitialized,
                    models_directory = currentSettings?.Storage?.ModelsDirectory,
                    models_directory_ready = modelsDirectoryReady,
                    logs_directory = currentSettings?.Logging?.LogDirectory,
                    logs_directory_ready = logsDirectoryReady,
                    telemetry_enabled = currentSettings?.Telemetry != null && currentSettings.Telemetry.Enable,
                    telemetry_ready = telemetryReady,
                    utc = DateTime.UtcNow
                };
            }, api => RouteHelpers.Describe(api, "General", "Readiness check")
                .WithDescription("Readiness check that verifies startup initialization and writable runtime directories.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Ready status", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(503, OpenApiResponseMetadata.Json("Not ready status", OpenApiSchemaMetadata.Create("object"))));

            ctx.Server.Head("/favicon.ico", async (req) => null, api => RouteHelpers.Describe(api, "General", "Favicon HEAD probe"));

            ctx.Server.Get("/favicon.ico", async (req) =>
            {
                req.Http.Response.ContentType = Constants.FaviconContentType;
                return File.ReadAllBytes(Constants.FaviconFilename);
            }, api => RouteHelpers.Describe(api, "General", "Serve favicon")
                .WithDescription("Returns the SharpAI favicon image.")
                .WithResponse(200, OpenApiResponseMetadata.Binary("PNG favicon", "image/png")));
        }

        #endregion
    }
}
