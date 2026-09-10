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
