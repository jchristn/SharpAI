namespace SharpAI.Server.API.REST.Routes
{
    using System;
    using System.IO;

    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Shared helpers used by the per-feature route registrar classes (see <see cref="RouteContext"/>).
    /// Extracted from the server composition root so registrars and the host share one implementation of
    /// the OpenAPI describe convention and runtime directory checks.
    /// </summary>
    public static class RouteHelpers
    {
        #region Public-Methods

        /// <summary>
        /// Apply the standard OpenAPI summary + tag to a route's metadata and return it for further chaining.
        /// </summary>
        /// <param name="api">Route metadata to annotate.</param>
        /// <param name="tag">OpenAPI tag grouping the route.</param>
        /// <param name="summary">Human-readable summary.</param>
        /// <returns>The same metadata instance, annotated.</returns>
        public static OpenApiRouteMetadata Describe(OpenApiRouteMetadata api, string tag, string summary)
        {
            if (api == null) throw new ArgumentNullException(nameof(api));
            api.Summary = summary;
            api.WithTag(tag);
            return api;
        }

        /// <summary>
        /// Determine whether a directory exists and is writable by the current process, by creating and
        /// deleting a probe file. Returns false (never throws) on any failure.
        /// </summary>
        /// <param name="directory">Directory to test. Null/empty returns false.</param>
        /// <returns>True when the directory exists and a probe file could be written and deleted.</returns>
        public static bool DirectoryExistsAndWritable(string directory)
        {
            if (String.IsNullOrWhiteSpace(directory)) return false;
            if (!Directory.Exists(directory)) return false;

            string testFile = Path.Combine(directory, ".sharpai-write-test-" + Guid.NewGuid().ToString("N"));

            try
            {
                using (FileStream stream = new FileStream(testFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] buffer = new byte[] { 0 };
                    stream.Write(buffer, 0, buffer.Length);
                }

                File.Delete(testFile);
                return true;
            }
            catch
            {
                try
                {
                    if (File.Exists(testFile)) File.Delete(testFile);
                }
                catch
                {
                }

                return false;
            }
        }

        /// <summary>
        /// Return a URL route parameter value by name, or null.
        /// </summary>
        /// <param name="req">API request.</param>
        /// <param name="name">Parameter name.</param>
        /// <returns>Value, or null.</returns>
        public static string RouteParam(ApiRequest req, string name)
        {
            return req?.Http?.Request?.Url?.Parameters?[name];
        }

        /// <summary>
        /// Build an <see cref="SharpAI.Models.EnumerationQuery"/> from the request's query string.
        /// </summary>
        /// <param name="req">API request.</param>
        /// <returns>Enumeration query with query-string overrides applied.</returns>
        public static SharpAI.Models.EnumerationQuery ParseEnumQuery(ApiRequest req)
        {
            SharpAI.Models.EnumerationQuery query = new SharpAI.Models.EnumerationQuery();
            query.ApplyQuerystringOverrides(key => req.Http.Request.Query.Elements?[key]);
            return query;
        }

        /// <summary>
        /// Case-insensitive lookup of a request header value, or null when absent.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="name">Header name.</param>
        /// <returns>Header value, or null.</returns>
        public static string HeaderValue(HttpContextBase ctx, string name)
        {
            System.Collections.Specialized.NameValueCollection headers = ctx.Request.Headers;
            if (headers == null) return null;

            foreach (string key in headers.AllKeys)
            {
                if (key != null && key.Equals(name, StringComparison.OrdinalIgnoreCase)) return headers[key];
            }

            return null;
        }

        /// <summary>
        /// Extract a bearer token from the Authorization header (Bearer scheme) or the x-token header.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Token, or null.</returns>
        public static string ExtractBearerToken(HttpContextBase ctx)
        {
            string authorization = HeaderValue(ctx, "authorization");
            if (!String.IsNullOrEmpty(authorization) &&
                authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authorization.Substring(7).Trim();
            }

            return HeaderValue(ctx, "x-token");
        }

        #endregion
    }
}
