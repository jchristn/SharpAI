namespace SharpAI.Serve
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Http;
    using SharpAI.Models;
    using SharpAI.Models.Ollama;
    using SharpAI.Models.OpenAI;
    using SharpAI.Serialization;
    using SyslogLogging;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    internal sealed class ServeHost : IDisposable
    {
        private readonly ServeOptions _Options;
        private readonly SingleModel _Model;
        private readonly LoggingModule _Logging;
        private readonly Webserver _Server;
        private readonly Serializer _Serializer = new Serializer();
        private readonly CancellationTokenSource _Stopping = new CancellationTokenSource();
        private int _Active;
        private bool _Started;
        internal ServeHost(ServeOptions options, SingleModel model, LoggingModule logging)
        {
            _Options = options; _Model = model; _Logging = logging;
            ServeRequestLog transport = new ServeRequestLog(logging);
            WebserverSettings settings = new WebserverSettings { Hostname = options.Host, Port = options.Port };
            _Server = new Webserver(settings, NotFoundAsync);
            _Server.Routes.PreRouting = context =>
            {
                transport.Begin(context);
                context.Response.Headers.Add(SharpAI.Constants.RequestIdHeader, Guid.NewGuid().ToString());
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                return Task.CompletedTask;
            };
            _Server.Routes.PostRouting = context => { transport.Complete(context); return Task.CompletedTask; };
            _Server.Routes.Preflight = async context =>
            {
                context.Response.StatusCode = 200;
                context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, HEAD, POST, OPTIONS");
                context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
                await context.Response.Send().ConfigureAwait(false);
            };
            _Server.Get("/health", request => Task.FromResult<object>(new { status = "healthy", version = ServeVersion.Current }));
            _Server.Head("/health", request => Task.FromResult<object?>(null));
            _Server.Get("/ready", request => Task.FromResult<object>(new { status = "ready", model = options.ModelName, context_size = model.ContextSize }));
            if (options.Api != "openai")
            {
                OllamaInferenceHandler ollama = new OllamaInferenceHandler(model, transport, logging, _Serializer);
                _Server.Get("/api/tags", request => Task.FromResult<object>(new { models = new[] { model.Metadata.ToOllamaModelDetails() } }));
                _Server.Get("/api/ps", request => Task.FromResult<object>(new { models = new[] { model.Metadata.ToOllamaModelDetails() } }));
                _Server.Get("/api/version", request => Task.FromResult<object>(new { version = ServeVersion.Current }));
                Post<OllamaShowModelInfoRequest>("/api/show", (request, body, token) => Task.FromResult<object?>(new
                {
                    details = new { format = "gguf", family = model.Metadata.Family, parameter_size = model.Metadata.ParameterCount.ToString(CultureInfo.InvariantCulture) },
                    model_info = new { architecture = model.Metadata.Family, context_length = model.ContextSize },
                    capabilities = new { embeddings = model.Metadata.Embeddings, completions = model.Metadata.Completions }
                }));
                Post<OllamaGenerateEmbeddingsRequest>("/api/embed", ollama.GenerateEmbeddings);
                Post<OllamaGenerateCompletionRequest>("/api/generate", ollama.GenerateCompletion);
                Post<OllamaGenerateChatCompletionRequest>("/api/chat", ollama.GenerateChatCompletion);
            }
            if (options.Api != "ollama")
            {
                OpenAIInferenceHandler openai = new OpenAIInferenceHandler(model, transport, logging, _Serializer);
                _Server.Get("/v1/models", request => Task.FromResult<object>(new
                {
                    @object = "list", data = new[] { new { id = options.ModelName, @object = "model", created = new DateTimeOffset(model.Metadata.CreatedUtc).ToUnixTimeSeconds(), owned_by = "sharpai" } }
                }));
                Post<OpenAIGenerateEmbeddingsRequest>("/v1/embeddings", openai.GenerateEmbeddings);
                Post<OpenAIGenerateCompletionRequest>("/v1/completions", openai.GenerateCompletion);
                Post<OpenAIGenerateChatCompletionRequest>("/v1/chat/completions", openai.GenerateChatCompletion);
            }
        }
        internal async Task RunAsync(CancellationToken cancellationToken)
        {
            _Server.Start(); _Started = true;
            _Logging.Log(Severity.Info, "Ready: " + _Options.ModelName + " on " + _Options.Host + ":" + _Options.Port.ToString(CultureInfo.InvariantCulture) + " (" + _Options.Api + ", context " + _Model.ContextSize.ToString(CultureInfo.InvariantCulture) + ").");
            try { await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            finally
            {
                _Stopping.Cancel();
                _Server.Stop(); _Started = false;
                while (Volatile.Read(ref _Active) > 0) await Task.Delay(20, CancellationToken.None).ConfigureAwait(false);
            }
        }
        private void Post<T>(string path, Func<ApiRequest, T, CancellationToken, Task<object?>> handler) where T : class
        {
            _Server.Post(path, async request =>
            {
                Interlocked.Increment(ref _Active);
                using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(_Stopping.Token, request.CancellationToken);
                try
                {
                    return await InferenceErrors.RunInference(request, async () =>
                    {
                        T body;
                        try { body = ServeRequestDefaults.Read<T>(request.Http.Request.DataAsString, path, _Options, _Serializer); }
                        catch (JsonException error) { throw new WebserverException(ApiResultEnum.BadRequest, error.Message); }
                        catch (ArgumentException error) { throw new WebserverException(ApiResultEnum.BadRequest, error.Message); }
                        using JsonDocument raw = JsonDocument.Parse(request.Http.Request.DataAsString);
                        string name = raw.RootElement.GetProperty("model").GetString()!;
                        ModelFile? metadata = _Model.Find(name);
                        if (metadata == null) throw new WebserverException(ApiResultEnum.NotFound, "Only model '" + _Options.ModelName + "' is served.");
                        bool embedding = path is "/api/embed" or "/v1/embeddings";
                        if (embedding && !metadata.Embeddings || !embedding && path != "/api/show" && !metadata.Completions)
                            throw new WebserverException(ApiResultEnum.Forbidden, "The configured model does not support this operation.");
                        return await handler(request, body, linked.Token).ConfigureAwait(false);
                    }).ConfigureAwait(false);
                }
                finally { Interlocked.Decrement(ref _Active); }
            });
        }
        private static async Task NotFoundAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 404; context.Response.ContentType = "application/json";
            await context.Response.Send("{\"error\":\"No route matched this standalone serving mode.\"}").ConfigureAwait(false);
        }
        public void Dispose()
        {
            _Stopping.Cancel();
            if (_Started) _Server.Stop();
            _Server.Dispose(); _Stopping.Dispose();
        }
    }
}
