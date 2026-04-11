namespace SharpAI.Server
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using SharpAI;
    using SharpAI.Engines;
    using SharpAI.Hosting;
    using SharpAI.Models.Ollama;
    using SharpAI.Models.OpenAI;
    using SharpAI.Serialization;
    using SharpAI.Server.API.REST.Ollama;
    using SharpAI.Server.API.REST.OpenAI;
    using SharpAI.Server.Classes.Runtime;
    using SharpAI.Server.Classes.Settings;
    using SharpAI.Services;
    using SyslogLogging;
    using Watson.ORM.Sqlite;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    using Constants = SharpAI.Constants;

    /// <summary>
    /// SharpAI Server.  We are happy to see you.
    /// </summary>
    public static class Program
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        #region Public-Members

        #endregion

        #region Private-Members

        private static string _Header = "[SharpAI] ";
        private static string _Version = "4.0.0";
        private static Serializer _Serializer = new Serializer();
        private static Settings _Settings = null;
        private static LoggingModule _Logging = null;
        private static WatsonORM _ORM = null;

        private static ModelFileService _ModelFileService = null;
        private static ModelEngineService _ModelEngineService = null;

        private static HuggingFaceClient _HuggingFaceClient = null;
        private static Webserver _Server = null;
        private static OllamaApiHandler _OllamaApiHandler = null;
        private static OpenAIApiHandler _OpenAIApiHandler = null;
        private static CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private static bool _ShutdownRequested = false;

        #endregion

        #region Entrypoint

        /// <summary>
        /// SharpAI Server.  We are happy to see you.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>Task.</returns>
        public static async Task Main(string[] args)
        {
            Welcome();
            ParseArguments(args);
            LoadSettings();
            InitializeLogging();
            InitializeBootstrapper();
            InitializeGlobals();
            InitializeRestServer();

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;

                if (!_ShutdownRequested)
                {
                    _ShutdownRequested = true;
                    _TokenSource.Cancel();
                    _Logging.Debug(_Header + "shutdown requested");
                }
            };

            _Logging.Debug(_Header + "starting SharpAI server");
            _Server.Start();

            // Fire-and-forget: re-detect capabilities for existing models so the
            // DB reflects the authoritative GGUF-derived values. This runs in the
            // background so it doesn't delay server startup.
            _ = Task.Run(() => RedetectModelCapabilitiesAsync(_TokenSource.Token));

            try
            {
                await Task.Delay(Timeout.Infinite, _TokenSource.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // graceful shutdown
            }

            _Server.Stop();
            _Server.Dispose();
        }

        private static async Task RedetectModelCapabilitiesAsync(CancellationToken token)
        {
            try
            {
                System.Collections.Generic.List<Models.ModelFile> all = _ModelFileService.All();
                if (all == null || all.Count == 0)
                {
                    _Logging.Debug(_Header + "capability detection: no local models to inspect");
                    return;
                }

                _Logging.Info(_Header + "capability detection: starting for " + all.Count + " local model(s); " +
                    "reading GGUF metadata to determine embedding vs completion support");

                int inspected = 0;
                int updated = 0;
                int unchanged = 0;
                int skipped = 0;
                int failed = 0;

                foreach (Models.ModelFile mf in all)
                {
                    if (token.IsCancellationRequested)
                    {
                        _Logging.Warn(_Header + "capability detection: cancelled after " + inspected + " of " + all.Count + " model(s)");
                        return;
                    }

                    string path = Path.Combine(_Settings.Storage.ModelsDirectory, mf.GUID.ToString());
                    if (!File.Exists(path))
                    {
                        _Logging.Warn(_Header + "capability detection: skipping '" + mf.Name + "' - GGUF file missing at " + path);
                        skipped++;
                        continue;
                    }

                    try
                    {
                        _Logging.Debug(_Header + "capability detection: inspecting '" + mf.Name + "' (" + path + ")");

                        using (LlamaSharpEngine engine = _ModelEngineService.GetByModelFile(path))
                        {
                            string detectedArch = engine.Architecture;
                            string arch = detectedArch ?? "unknown";
                            bool embeddings = engine.SupportsEmbeddings;
                            bool completions = engine.SupportsGeneration;

                            string capabilityDesc =
                                (embeddings && completions) ? "embeddings + completions" :
                                (embeddings ? "embeddings only" :
                                (completions ? "completions only" : "neither"));

                            bool familyChanged =
                                !String.IsNullOrEmpty(detectedArch) &&
                                !String.Equals(mf.Family, detectedArch, StringComparison.OrdinalIgnoreCase);

                            if (mf.Embeddings != embeddings || mf.Completions != completions || familyChanged)
                            {
                                _Logging.Info(_Header + "capability detection: '" + mf.Name +
                                    "' architecture='" + arch + "' - " + capabilityDesc +
                                    " (was family='" + (mf.Family ?? "unknown") + "'" +
                                    ", embeddings=" + mf.Embeddings + ", completions=" + mf.Completions +
                                    "; now family='" + arch + "'" +
                                    ", embeddings=" + embeddings + ", completions=" + completions +
                                    ") - updating database");

                                mf.Embeddings = embeddings;
                                mf.Completions = completions;
                                if (!String.IsNullOrEmpty(detectedArch)) mf.Family = detectedArch;
                                _ModelFileService.Update(mf);
                                updated++;
                            }
                            else
                            {
                                _Logging.Debug(_Header + "capability detection: '" + mf.Name +
                                    "' architecture='" + arch + "' - " + capabilityDesc + " (already correct in database)");
                                unchanged++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _Logging.Warn(_Header + "capability detection: failed to inspect '" + mf.Name + "': " + ex.Message);
                        failed++;
                    }

                    inspected++;
                    await Task.Yield();
                }

                _Logging.Info(_Header + "capability detection: complete - inspected " + inspected + ", updated " + updated +
                    ", unchanged " + unchanged + ", skipped " + skipped + ", failed " + failed);
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "capability detection: aborted with error: " + ex.ToString());
            }
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        private static void Welcome()
        {
            Console.WriteLine("");
            Console.WriteLine(Constants.Logo);
            Console.WriteLine(" SharpAI Server v" + _Version);
            Console.WriteLine(" (c)2025 Joel Christner");
            Console.WriteLine("");
        }

        private static void ParseArguments(string[] args)
        {

        }

        private static void LoadSettings()
        {
            if (!File.Exists(Constants.SettingsFile))
            {
                Console.WriteLine("Settings file " + Constants.SettingsFile + " does not exist, creating");

                _Settings = new Settings();
                _Settings.SoftwareVersion = _Version;

                _Serializer.SerializeJsonToFile(Constants.SettingsFile, _Settings, true);
            }
            else
            {
                _Settings = _Serializer.DeserializeJsonFromFile<Settings>(Constants.SettingsFile);
            }
        }

        private static void InitializeLogging()
        {
            List<SyslogLogging.SyslogServer> servers = new List<SyslogLogging.SyslogServer>();

            if (_Settings.Logging.Servers != null && _Settings.Logging.Servers.Count > 0)
            {
                foreach (SharpAI.Server.Classes.Settings.SyslogServer server in _Settings.Logging.Servers)
                {
                    servers.Add(new SyslogLogging.SyslogServer(server.Hostname, server.Port));
                }
            }

            if (!Directory.Exists(_Settings.Logging.LogDirectory)) Directory.CreateDirectory(_Settings.Logging.LogDirectory);

            _Logging = new LoggingModule(servers, _Settings.Logging.ConsoleLogging);
            _Logging.Settings.FileLogging = FileLoggingMode.FileWithDate;
            _Logging.Settings.LogFilename = _Settings.Logging.LogDirectory + _Settings.Logging.LogFilename;
            _Logging.Settings.EnableColors = _Settings.Logging.EnableColors;
            _Logging.Settings.EnableConsole = _Settings.Logging.ConsoleLogging;
            _Logging.Settings.MinimumSeverity = (Severity)_Settings.Logging.MinimumSeverity;
        }

        private static void InitializeBootstrapper()
        {
            // This must happen before any LlamaSharp types are referenced
            try
            {
                NativeLibraryBootstrapper.Initialize(_Settings, _Logging);
            }
            catch (Exception ex)
            {
                Console.WriteLine("WARNING: Native library bootstrapper initialization failed:");
                Console.WriteLine(ex.ToString());
                Console.WriteLine("Continuing with default LlamaSharp library loading...");
            }
        }

        private static void InitializeGlobals()
        {

            #region ORM

            _ORM = new WatsonORM(_Settings.Database);

            _ORM.InitializeDatabase();
            _ORM.InitializeTables(new List<Type>
            {
                typeof(Models.ModelFile)
            });

            #endregion

            #region Services

            _ModelFileService = new ModelFileService(_Logging, _ORM, _Settings.Storage.ModelsDirectory);
            _ModelEngineService = new ModelEngineService(_Logging);
            _HuggingFaceClient = new HuggingFaceClient(_Logging, _Settings.HuggingFace.ApiKey);

            #endregion

            #region Handlers

            _OllamaApiHandler = new OllamaApiHandler(
                _Settings,
                _Logging,
                _Serializer,
                _ModelFileService,
                _ModelEngineService,
                _HuggingFaceClient);

            _OpenAIApiHandler = new OpenAIApiHandler(
                _Settings,
                _Logging,
                _Serializer,
                _ModelFileService,
                _ModelEngineService,
                _HuggingFaceClient);

            #endregion
        }

        private static void InitializeRestServer()
        {
            _Server = new Webserver(_Settings.Rest, DefaultRoute);
            _Server.Events.Logger = (msg) => _Logging.Debug(_Header + msg);

            #region OpenAPI

            _Server.UseOpenApi(openApi =>
            {
                openApi.Info.Title = "SharpAI Server API";
                openApi.Info.Version = _Version;
                openApi.Info.Description =
                    "Local AI inference server with Ollama- and OpenAI-compatible REST endpoints. " +
                    "Provides model management, embeddings, completions, and chat completions against " +
                    "locally hosted GGUF models via LlamaSharp.";
                openApi.Info.Contact = new OpenApiContact
                {
                    Name = "SharpAI",
                    Url = "https://github.com/jchristn/sharpai"
                };
                openApi.Info.License = new OpenApiLicense
                {
                    Name = "MIT",
                    Url = "https://opensource.org/licenses/MIT"
                };

                openApi.Tags.Add(new OpenApiTag { Name = "General", Description = "General server endpoints" });
                openApi.Tags.Add(new OpenApiTag { Name = "Settings", Description = "Server configuration management" });
                openApi.Tags.Add(new OpenApiTag { Name = "Ollama - Models", Description = "Ollama-compatible model management" });
                openApi.Tags.Add(new OpenApiTag { Name = "Ollama - Inference", Description = "Ollama-compatible inference endpoints" });
                openApi.Tags.Add(new OpenApiTag { Name = "OpenAI - Inference", Description = "OpenAI-compatible inference endpoints" });
            });

            #endregion

            #region Middleware

            _Server.Routes.Preflight = async (ctx) =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                ctx.Response.Headers.Add("Access-Control-Allow-Methods", "OPTIONS, HEAD, GET, PUT, POST, DELETE, PATCH");
                ctx.Response.Headers.Add("Access-Control-Allow-Headers", "*");
                ctx.Response.Headers.Add("Access-Control-Max-Age", "86400");
                await ctx.Response.Send().ConfigureAwait(false);
            };

            _Server.Routes.PreRouting = async (ctx) =>
            {
                ctx.Response.Headers.Add(Constants.RequestIdHeader, Guid.NewGuid().ToString());

                if (_Settings.Debug.RequestBody)
                {
                    if (ctx.Request.ChunkedTransfer) _Logging.Debug(_Header + "chunked request body detected, skipping logging");
                    else if (!String.IsNullOrEmpty(ctx.Request.DataAsString))
                    {
                        _Logging.Debug(_Header + "request body:" + Environment.NewLine + ctx.Request.DataAsString);
                    }
                    else
                    {
                        _Logging.Debug(_Header + "no request body");
                    }
                }
            };

            _Server.Routes.PostRouting = async (ctx) =>
            {
                ctx.Timestamp.End = DateTime.UtcNow;

                _Logging.Debug(
                    _Header
                    + ctx.Request.Method + " " + ctx.Request.Url.RawWithQuery + " "
                    + ctx.Response.StatusCode + " "
                    + "(" + (ctx.Timestamp.TotalMs.HasValue ? ctx.Timestamp.TotalMs.Value.ToString("F2") : "?") + "ms)");
            };

            #endregion

            #region General-Routes

            _Server.Get("/", async (req) =>
            {
                req.Http.Response.ContentType = Constants.HtmlContentType;
                return Constants.HtmlHomepage;
            }, api => Describe(api, "General", "Server homepage")
                .WithDescription("Returns the default HTML homepage indicating the node is operational.")
                .WithResponse(200, OpenApiResponseMetadata.Text("Operational HTML page")));

            _Server.Head("/", async (req) => null, api => Describe(api, "General", "Server liveness check")
                .WithDescription("HEAD probe that returns 200 OK when the server is reachable."));

            _Server.Head("/favicon.ico", async (req) => null, api => Describe(api, "General", "Favicon HEAD probe"));

            _Server.Get("/favicon.ico", async (req) =>
            {
                req.Http.Response.ContentType = Constants.FaviconContentType;
                return File.ReadAllBytes(Constants.FaviconFilename);
            }, api => Describe(api, "General", "Serve favicon")
                .WithDescription("Returns the SharpAI favicon image.")
                .WithResponse(200, OpenApiResponseMetadata.Binary("PNG favicon", "image/png")));

            #endregion

            #region Settings-Endpoints

            _Server.Get("/api/settings", async (req) =>
            {
                return _Settings;
            }, api => Describe(api, "Settings", "Get current server settings")
                .WithDescription("Returns the current in-memory server settings loaded from sharpai.json.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Current settings", OpenApiSchemaMetadata.Create("object"))));

            _Server.Put<Settings>("/api/settings", async (req) =>
            {
                Settings updated = req.GetData<Settings>();
                if (updated == null) throw new WebserverException(ApiResultEnum.BadRequest, "Request body is required.");

                updated.CreatedUtc = _Settings.CreatedUtc;
                updated.SoftwareVersion = _Settings.SoftwareVersion;

                _Settings = updated;

                _Serializer.SerializeJsonToFile(Constants.SettingsFile, _Settings, true);

                _Logging.Info(_Header + "settings updated and saved to " + Constants.SettingsFile);

                return _Settings;
            }, api => Describe(api, "Settings", "Update server settings")
                .WithDescription(
                    "Replaces the in-memory server settings and rewrites sharpai.json on disk. " +
                    "CreatedUtc and SoftwareVersion are preserved from the current settings. " +
                    "Some settings (REST hostname, port, SSL, Database) require a server restart to take effect.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Updated settings", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Updated settings", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest()));

            #endregion

            #region Ollama-Endpoints

            _Server.Post<OllamaPullModelRequest>("/api/pull", async (req) =>
            {
                OllamaPullModelRequest pmr = req.GetData<OllamaPullModelRequest>();
                return await _OllamaApiHandler.PullModel(req, pmr, _TokenSource.Token).ConfigureAwait(false);
            }, api => Describe(api, "Ollama - Models", "Pull a model")
                .WithDescription("Downloads a model from HuggingFace. Streams progress as newline-delimited JSON.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Pull model request", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Progress stream", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest()));

            _Server.Delete<OllamaDeleteModelRequest>("/api/delete", async (req) =>
            {
                OllamaDeleteModelRequest dmr = req.GetData<OllamaDeleteModelRequest>();
                return await _OllamaApiHandler.DeleteModel(req, dmr, _TokenSource.Token).ConfigureAwait(false);
            }, api => Describe(api, "Ollama - Models", "Delete a model")
                .WithDescription("Deletes a locally stored model by name.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Delete model request", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Deletion result", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            _Server.Get("/api/tags", async (req) =>
            {
                return await _OllamaApiHandler.ListLocalModels(req, _TokenSource.Token).ConfigureAwait(false);
            }, api => Describe(api, "Ollama - Models", "List local models")
                .WithDescription("Returns the list of locally available models.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Local models", OpenApiSchemaMetadata.Create("object"))));

            _Server.Get("/api/ps", async (req) =>
            {
                return await _OllamaApiHandler.ListRunningModels(req, _TokenSource.Token).ConfigureAwait(false);
            }, api => Describe(api, "Ollama - Models", "List running (loaded) models")
                .WithDescription(
                    "Returns the list of models that are currently loaded in memory, matching the Ollama " +
                    "'/api/ps' (ollama ps) endpoint. The size_vram field reports the full model size when " +
                    "the CUDA backend is active and 0 when the CPU backend is active. SharpAI does not " +
                    "implement keep-alive unloads, so expires_at is always null.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Running models", OpenApiSchemaMetadata.Create("object"))));

            _Server.Post<OllamaGenerateEmbeddingsRequest>("/api/embed", async (req) =>
            {
                OllamaGenerateEmbeddingsRequest ger = req.GetData<OllamaGenerateEmbeddingsRequest>();
                return await _OllamaApiHandler.GenerateEmbeddings(req, ger, _TokenSource.Token).ConfigureAwait(false);
            }, api => Describe(api, "Ollama - Inference", "Generate embeddings")
                .WithDescription("Generates vector embeddings for a single input or array of inputs.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Embeddings request", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Embeddings", OpenApiSchemaMetadata.Create("object"))));

            _Server.Post<OllamaGenerateCompletionRequest>("/api/generate", async (req) =>
            {
                OllamaGenerateCompletionRequest gcr = req.GetData<OllamaGenerateCompletionRequest>();
                object ret = await _OllamaApiHandler.GenerateCompletion(req, gcr, _TokenSource.Token).ConfigureAwait(false);
                if (req.Http.Response.ChunkedTransfer) return null;
                else return ret;
            }, api => Describe(api, "Ollama - Inference", "Generate text completion")
                .WithDescription("Generates a text completion for the given prompt. Supports streaming via chunked transfer.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Completion request", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Completion response", OpenApiSchemaMetadata.Create("object"))));

            _Server.Post<OllamaGenerateChatCompletionRequest>("/api/chat", async (req) =>
            {
                OllamaGenerateChatCompletionRequest gccr = req.GetData<OllamaGenerateChatCompletionRequest>();
                object ret = await _OllamaApiHandler.GenerateChatCompletion(req, gccr, _TokenSource.Token).ConfigureAwait(false);
                if (req.Http.Response.ChunkedTransfer) return null;
                else return ret;
            }, api => Describe(api, "Ollama - Inference", "Generate chat completion")
                .WithDescription("Generates a chat completion from a sequence of messages. Supports streaming via chunked transfer.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Chat completion request", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Chat completion response", OpenApiSchemaMetadata.Create("object"))));

            #endregion

            #region OpenAI-Endpoints

            _Server.Post<OpenAIGenerateEmbeddingsRequest>("/v1/embeddings", async (req) =>
            {
                OpenAIGenerateEmbeddingsRequest ger = req.GetData<OpenAIGenerateEmbeddingsRequest>();
                return await _OpenAIApiHandler.GenerateEmbeddings(req, ger, _TokenSource.Token).ConfigureAwait(false);
            }, api => Describe(api, "OpenAI - Inference", "Generate embeddings (OpenAI-compatible)")
                .WithDescription("OpenAI-compatible embeddings endpoint.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Embeddings request", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Embeddings response", OpenApiSchemaMetadata.Create("object"))));

            _Server.Post<OpenAIGenerateCompletionRequest>("/v1/completions", async (req) =>
            {
                OpenAIGenerateCompletionRequest gcr = req.GetData<OpenAIGenerateCompletionRequest>();
                object ret = await _OpenAIApiHandler.GenerateCompletion(req, gcr, _TokenSource.Token).ConfigureAwait(false);
                if (req.Http.Response.ServerSentEvents) return null;
                else return ret;
            }, api => Describe(api, "OpenAI - Inference", "Generate text completion (OpenAI-compatible)")
                .WithDescription("OpenAI-compatible text completion endpoint. Supports streaming via server-sent events.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Completion request", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Completion response", OpenApiSchemaMetadata.Create("object"))));

            _Server.Post<OpenAIGenerateChatCompletionRequest>("/v1/chat/completions", async (req) =>
            {
                OpenAIGenerateChatCompletionRequest gccr = req.GetData<OpenAIGenerateChatCompletionRequest>();
                object ret = await _OpenAIApiHandler.GenerateChatCompletion(req, gccr, _TokenSource.Token).ConfigureAwait(false);
                if (req.Http.Response.ServerSentEvents) return null;
                else return ret;
            }, api => Describe(api, "OpenAI - Inference", "Generate chat completion (OpenAI-compatible)")
                .WithDescription("OpenAI-compatible chat completion endpoint. Supports streaming via server-sent events.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Chat completion request", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Chat completion response", OpenApiSchemaMetadata.Create("object"))));

            #endregion
        }

        private static async Task DefaultRoute(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 404;
            ctx.Response.ContentType = Constants.JsonContentType;
            await ctx.Response.Send("{\"error\":\"NotFound\",\"message\":\"No route matched\"}").ConfigureAwait(false);
        }

        private static OpenApiRouteMetadata Describe(OpenApiRouteMetadata api, string tag, string summary)
        {
            api.Summary = summary;
            api.WithTag(tag);
            return api;
        }

        #endregion

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}
