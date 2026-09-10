namespace SharpAI.Server.API.REST.Routes
{
    using System;
    using System.Threading.Tasks;

    using WatsonWebserver.Core;

    /// <summary>
    /// Shared error handling for inference routes: runs a handler and, on a pre-stream failure, writes the
    /// OpenAI/Ollama-shaped error envelope with the correct status and type so client SDKs never throw when
    /// parsing an error. Mid-stream failures (after the response has begun) are swallowed. Extracted from the
    /// composition root so the Ollama and OpenAI inference registrars share one implementation.
    /// </summary>
    internal static class InferenceErrors
    {
        #region Public-Methods

        /// <summary>
        /// Run an inference handler, translating exceptions into the error envelope.
        /// </summary>
        /// <param name="req">API request.</param>
        /// <param name="handler">Handler to run.</param>
        /// <returns>The handler's result, or null after an error was written.</returns>
        public static async Task<object> RunInference(ApiRequest req, Func<Task<object>> handler)
        {
            try
            {
                return await handler().ConfigureAwait(false);
            }
            catch (SharpAI.Exceptions.EngineBusyException ex)
            {
                return await WriteInferenceError(req, 429, "server_busy", ex.Message).ConfigureAwait(false);
            }
            catch (SharpAI.Exceptions.ModelAdmissionException ex)
            {
                return await WriteInferenceError(req, 429, "server_busy", ex.Message).ConfigureAwait(false);
            }
            catch (WebserverException ex)
            {
                int status = StatusForResult(ex.Result);
                return await WriteInferenceError(req, status, TypeForStatus(status), ex.Message).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return await WriteInferenceError(req, 500, "internal_error", ex.Message).ConfigureAwait(false);
            }
        }

        #endregion

        #region Private-Methods

        private static int StatusForResult(ApiResultEnum result)
        {
            switch (result)
            {
                case ApiResultEnum.BadRequest: return 400;
                case ApiResultEnum.NotAuthorized: return 401;
                case ApiResultEnum.Forbidden: return 403;
                case ApiResultEnum.NotFound: return 404;
                case ApiResultEnum.Conflict: return 409;
                case ApiResultEnum.SlowDown: return 429;
                default: return 500;
            }
        }

        private static string TypeForStatus(int status)
        {
            switch (status)
            {
                case 400: return "invalid_request_error";
                case 401: return "authentication_error";
                case 403: return "permission_error";
                case 404: return "not_found_error";
                case 409: return "conflict_error";
                case 429: return "rate_limit_exceeded";
                default: return "internal_error";
            }
        }

        private static async Task<object> WriteInferenceError(ApiRequest req, int status, string type, string message)
        {
            try
            {
                req.Http.Response.StatusCode = status;
                req.Http.Response.ContentType = "application/json";
                string body = "{\"error\":{\"message\":" + System.Text.Json.JsonSerializer.Serialize(message ?? string.Empty)
                    + ",\"type\":\"" + type + "\"}}";
                await req.Http.Response.Send(body).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Response already began streaming; nothing more can be written.
            }
            return null;
        }

        #endregion
    }
}
