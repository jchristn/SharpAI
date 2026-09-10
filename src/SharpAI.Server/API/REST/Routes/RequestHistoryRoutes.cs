namespace SharpAI.Server.API.REST.Routes
{
    using System;

    using SharpAI.Models;
    using SharpAI.Security;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Registrar for the request-history endpoints under <c>/v1.0/api/request-history</c> (list, summary,
    /// read, delete, bulk delete). Extracted from the composition root as part of the W7 registrar refactor.
    /// </summary>
    public static class RequestHistoryRoutes
    {
        #region Public-Methods

        /// <summary>
        /// Register the request-history routes on the context's webserver.
        /// </summary>
        /// <param name="ctx">Route context. Must have control-plane dependencies configured.</param>
        public static void Register(RouteContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            ctx.Server.Get("/v1.0/api/request-history", async (req) =>
            {
                ctx.Auth.Authorize(req, ResourceTypes.RequestHistory, OperationTypeEnum.Read, null);
                RequestHistoryQuery query = new RequestHistoryQuery();
                query.ApplyQuerystringOverrides(key => req.Http.Request.Query.Elements?[key]);
                return ctx.Database.RequestHistory.Enumerate(query);
            }, api => RouteHelpers.Describe(api, "Request History", "List captured requests")
                .WithDescription("Paginated list of captured requests (bodies omitted). Filters: method, statusCode, pathContains, fromUtc, toUtc, tenantId, userId, pageNumber, pageSize.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Request history page", OpenApiSchemaMetadata.Create("object"))));

            ctx.Server.Get("/v1.0/api/request-history/summary", async (req) =>
            {
                ctx.Auth.Authorize(req, ResourceTypes.RequestHistory, OperationTypeEnum.Read, null);
                RequestHistoryQuery query = new RequestHistoryQuery();
                query.ApplyQuerystringOverrides(key => req.Http.Request.Query.Elements?[key]);
                return ctx.Database.RequestHistory.Summarize(query);
            }, api => RouteHelpers.Describe(api, "Request History", "Summarize captured requests")
                .WithDescription("Time-bucketed counts and average durations for chart rendering. Emits a bucket for every interval including empty ones. Query: fromUtc, toUtc, bucketMinutes, plus the list filters.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Request history summary", OpenApiSchemaMetadata.Create("object"))));

            ctx.Server.Get("/v1.0/api/request-history/{id}", async (req) =>
            {
                ctx.Auth.Authorize(req, ResourceTypes.RequestHistory, OperationTypeEnum.Read, null);
                string id = RouteHelpers.RouteParam(req, "id");
                RequestHistoryEntry entry = ctx.Database.RequestHistory.Read(id);
                if (entry == null) throw new WebserverException(ApiResultEnum.NotFound, "The specified request history entry was not found.");
                return entry;
            }, api => RouteHelpers.Describe(api, "Request History", "Read a captured request")
                .WithDescription("Returns a single captured request including headers and bodies.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Request history entry", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            ctx.Server.Delete("/v1.0/api/request-history/{id}", async (req) =>
            {
                ctx.Auth.Authorize(req, ResourceTypes.RequestHistory, OperationTypeEnum.Delete, null);
                string id = RouteHelpers.RouteParam(req, "id");
                bool deleted = ctx.Database.RequestHistory.Delete(id);
                return new { deleted = deleted };
            }, api => RouteHelpers.Describe(api, "Request History", "Delete a captured request")
                .WithDescription("Deletes a single captured request by identifier.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Delete result", OpenApiSchemaMetadata.Create("object"))));

            ctx.Server.Delete("/v1.0/api/request-history", async (req) =>
            {
                ctx.Auth.Authorize(req, ResourceTypes.RequestHistory, OperationTypeEnum.Delete, null);
                RequestHistoryQuery query = new RequestHistoryQuery();
                query.ApplyQuerystringOverrides(key => req.Http.Request.Query.Elements?[key]);
                int deletedCount = ctx.Database.RequestHistory.DeleteMany(query);
                return new { deletedCount = deletedCount };
            }, api => RouteHelpers.Describe(api, "Request History", "Bulk delete captured requests")
                .WithDescription("Deletes all captured requests matching the supplied filter. Returns the number of rows deleted.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Bulk delete result", OpenApiSchemaMetadata.Create("object"))));
        }

        #endregion
    }
}
