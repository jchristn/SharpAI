namespace SharpAI.Server
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using SharpAI;
    using SharpAI.Database;
    using SharpAI.Engines;
    using SharpAI.Classes.Runtime;
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
        private static string _Version = "5.0.0";
        private static Serializer _Serializer = new Serializer();
        private static Settings _Settings = null;
        private static LoggingModule _Logging = null;
        private static DatabaseDriverBase _Database = null;
        private static SharpAI.Server.Classes.Runtime.TelemetryHost _TelemetryHost = null;
        private static SharpAI.Server.Classes.Runtime.RequestHistoryCaptureService _RequestHistoryCapture = null;
        private static SharpAI.Server.Classes.Runtime.AuthenticationService _AuthService = null;
        private static SharpAI.Security.SessionTokenService _SessionTokens = null;
        private static SharpAI.Security.AuthenticationEngine _AuthEngine = null;
        private static SharpAI.Security.RbacEngine _RbacEngine = null;
        private static SharpAI.Server.API.REST.Routes.AuthorizationGate _AuthGate = null;
        private static SharpAI.Server.API.REST.Routes.RouteContext _RouteContext = null;
        private static Timer _PruneTimer = null;

        private static ModelFileService _ModelFileService = null;
        private static ModelEngineService _ModelEngineService = null;

        private static HuggingFaceClient _HuggingFaceClient = null;
        private static Webserver _Server = null;
        private static OllamaApiHandler _OllamaApiHandler = null;
        private static OpenAIApiHandler _OpenAIApiHandler = null;
        private static CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private static bool _ShutdownRequested = false;
        private static System.Runtime.InteropServices.PosixSignalRegistration? _SigTerm = null;
        private static int _GracefulShutdownMs = 3000;

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
            InitializeTelemetry();
            InitializeBootstrapper();
            InitializeGlobals();
            InitializeRestServer();

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                RequestShutdown("SIGINT (Ctrl+C)");
            };

            // Handle SIGTERM (e.g., `docker stop`) so the container shuts down gracefully rather than being
            // killed. POSIX signals are unavailable on some platforms; Ctrl+C handling still applies there.
            try
            {
                _SigTerm = System.Runtime.InteropServices.PosixSignalRegistration.Create(
                    System.Runtime.InteropServices.PosixSignal.SIGTERM,
                    context =>
                    {
                        context.Cancel = true;
                        RequestShutdown("SIGTERM");
                    });
            }
            catch (Exception)
            {
                // signal registration unsupported on this platform
            }

            _Logging.Debug(_Header + "starting SharpAI server");
            _Server.Start();

            LogStartupSummary();

            // Hourly request-history retention prune (first run after 5 minutes).
            _PruneTimer = new Timer(_ => PruneRequestHistory(), null, (int)TimeSpan.FromMinutes(5).TotalMilliseconds, (int)TimeSpan.FromHours(1).TotalMilliseconds);

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

            await GracefulShutdownAsync().ConfigureAwait(false);
        }

        private static void RequestShutdown(string reason)
        {
            if (_ShutdownRequested) return;
            _ShutdownRequested = true;
            _Logging.Info(_Header + "shutdown requested (" + reason + ")");
            _TokenSource.Cancel();
        }

        private static async Task GracefulShutdownAsync()
        {
            // Give in-flight requests a brief window to complete, then stop accepting connections and
            // release resources in order — telemetry last so its exporters flush.
            _Logging.Info(_Header + "draining in-flight requests (up to " + (_GracefulShutdownMs / 1000) + "s)");
            try { await Task.Delay(_GracefulShutdownMs).ConfigureAwait(false); } catch (Exception) { }

            try { _Server?.Stop(); } catch (Exception ex) { _Logging.Warn(_Header + "server stop failed: " + ex.Message); }
            _Server?.Dispose();
            _PruneTimer?.Dispose();
            _ModelEngineService?.Dispose();
            _Database?.Dispose();
            _TelemetryHost?.Dispose();
            try { _SigTerm?.Dispose(); } catch (Exception) { }

            _Logging.Info(_Header + "shutdown complete");
        }

        private static void LogStartupSummary()
        {
            try
            {
                string database = _Database != null ? _Database.DatabaseType.ToString() : "none";
                string telemetry = _Settings?.Telemetry != null && _Settings.Telemetry.Enable
                    ? "enabled -> " + _Settings.Telemetry.OtlpEndpoint
                    : "disabled";
                string auth = _Settings?.Auth != null && _Settings.Auth.Enabled ? "enabled" : "disabled (open)";

                _Logging.Info(_Header + "startup summary:"
                    + " version=" + _Version
                    + " backend=" + NativeLibraryBootstrapper.SelectedBackend
                    + " nativeInitialized=" + NativeLibraryBootstrapper.IsInitialized
                    + " database=" + database
                    + " modelsDir=" + (_Settings?.Storage?.ModelsDirectory ?? "?")
                    + " telemetry=" + telemetry
                    + " auth=" + auth);
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "unable to log startup summary: " + ex.Message);
            }
        }

        private static void PruneRequestHistory()
        {
            try
            {
                if (_Settings == null || _Settings.RequestHistory == null || !_Settings.RequestHistory.Enabled) return;
                if (_Database == null || !_Database.IsInitialized) return;

                int removed = _Database.RequestHistory.Prune(DateTime.UtcNow.AddDays(-_Settings.RequestHistory.RetentionDays));
                if (removed > 0) _Logging.Debug(_Header + "pruned " + removed + " request history row(s)");
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "request history prune failed:" + Environment.NewLine + ex.ToString());
            }
        }

        private static void InitializeTelemetry()
        {
            _TelemetryHost = new SharpAI.Server.Classes.Runtime.TelemetryHost(_Settings.Telemetry, _Logging);
        }

        private static void SeedAuthentication()
        {
            try
            {
                if (_Database == null || !_Database.IsInitialized) return;

                SharpAI.Security.Tenant tenant = _Database.Tenants.GetByName("default");
                if (tenant == null)
                {
                    tenant = _Database.Tenants.Create(new SharpAI.Security.Tenant
                    {
                        Name = "default",
                        IsProtected = true
                    });
                    _Logging.Info(_Header + "seeded default tenant " + tenant.Guid);
                }

                SharpAI.Security.User admin = _Database.Users.GetByEmail(tenant.Guid, "admin@sharpai.local");
                if (admin == null)
                {
                    string initialPassword = Guid.NewGuid().ToString("N").Substring(0, 16);
                    _Database.Users.Create(new SharpAI.Security.User
                    {
                        TenantGuid = tenant.Guid,
                        FirstName = "Administrator",
                        LastName = "Account",
                        Email = "admin@sharpai.local",
                        PasswordSha256 = SharpAI.Security.PasswordHasher.Hash(initialPassword),
                        IsAdmin = true,
                        IsTenantAdmin = true,
                        IsProtected = true
                    });

                    _Logging.Warn(
                        _Header + "seeded default administrator 'admin@sharpai.local' with initial password '" +
                        initialPassword + "' — change it after first login (this is shown only once)");
                }
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "authentication seeding failed:" + Environment.NewLine + ex.ToString());
            }
        }

        private static async Task RedetectModelCapabilitiesAsync(CancellationToken token)
        {
            try
            {
                System.Collections.Generic.List<Models.ModelFile> all = CollectAllModels();
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

                        string detectedArch = null;
                        bool embeddings = false;
                        bool completions = true;
                        bool detectedViaMetadata = false;

                        // Try lightweight GGUF header reader first
                        try
                        {
                            SharpAI.Helpers.GgufMetadataReader.DetectCapabilities(
                                path,
                                out detectedArch,
                                out embeddings,
                                out completions);
                            detectedViaMetadata = true;
                        }
                        catch (Exception metaEx)
                        {
                            _Logging.Warn(_Header + "capability detection: lightweight read failed for '" + mf.Name +
                                "', falling back to full model load:" + Environment.NewLine + metaEx.ToString());
                        }

                        // Fall back to full engine initialization
                        if (!detectedViaMetadata)
                        {
                            using (LlamaSharpEngine engine = _ModelEngineService.GetByModelFile(path))
                            {
                                detectedArch = engine.Architecture;
                                embeddings = engine.SupportsEmbeddings;
                                completions = engine.SupportsGeneration;
                            }
                        }

                        string arch = detectedArch ?? "unknown";

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
                    catch (Exception ex)
                    {
                        _Logging.Warn(_Header + "capability detection: failed to inspect '" + mf.Name + "':" + Environment.NewLine + ex.ToString());
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

                if (SharpAIEnvironment.GetBool(SharpAIEnvironment.RequireBackend, false))
                {
                    Console.WriteLine(SharpAIEnvironment.RequireBackend + "=true; terminating startup.");
                    Environment.Exit(1);
                    return;
                }

                Console.WriteLine("Continuing with default LlamaSharp library loading...");
            }
        }

        private static void InitializeGlobals()
        {

            #region Database

            _Database = DatabaseDriverFactory.Create(_Settings.Database, _Logging);
            _Database.InitializeAsync().GetAwaiter().GetResult();

            #endregion

            #region Services

            _ModelFileService = new ModelFileService(_Logging, _Database.Models, _Settings.Storage.ModelsDirectory);
            _ModelEngineService = new ModelEngineService(_Logging);
            _HuggingFaceClient = new HuggingFaceClient(_Logging, _Settings.HuggingFace.ApiKey);
            _RequestHistoryCapture = new SharpAI.Server.Classes.Runtime.RequestHistoryCaptureService(_Database, _Settings.RequestHistory, _Logging);

            string tokenKeyMaterial = !String.IsNullOrEmpty(_Settings.Auth.TokenSigningKey)
                ? _Settings.Auth.TokenSigningKey
                : Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            if (String.IsNullOrEmpty(_Settings.Auth.TokenSigningKey))
                _Logging.Warn(_Header + "no Auth.TokenSigningKey configured; using a random per-boot key (session tokens will not survive restarts)");

            _SessionTokens = new SharpAI.Security.SessionTokenService(tokenKeyMaterial);
            _AuthEngine = new SharpAI.Security.AuthenticationEngine(_Database, _SessionTokens);
            _AuthEngine.DefaultSessionTtlMinutes = _Settings.Auth.SessionTtlMinutes;
            _RbacEngine = new SharpAI.Security.RbacEngine(_Database);
            _AuthService = new SharpAI.Server.Classes.Runtime.AuthenticationService(_Settings.Auth, _AuthEngine, _Database, _Logging);

            SeedAuthentication();

            try
            {
                int seededRoles = SharpAI.Security.RbacSeeder.Seed(_Database);
                if (seededRoles > 0) _Logging.Info(_Header + "seeded " + seededRoles + " built-in RBAC role(s)");
            }
            catch (Exception rbacEx)
            {
                _Logging.Warn(_Header + "RBAC role seeding failed:" + Environment.NewLine + rbacEx.ToString());
            }

            #endregion

            #region Handlers

            _OllamaApiHandler = new OllamaApiHandler(
                _Settings,
                _Logging,
                _Serializer,
                _ModelFileService,
                _ModelEngineService,
                _HuggingFaceClient,
                _Database.Presets);

            _OpenAIApiHandler = new OpenAIApiHandler(
                _Settings,
                _Logging,
                _Serializer,
                _ModelFileService,
                _ModelEngineService,
                _HuggingFaceClient,
                _Database.Presets);

            #endregion
        }

        private static void InitializeRestServer()
        {
            // Enable Watson 7.1 native telemetry (HTTP server metrics + spans) and serve an in-process
            // Prometheus endpoint on the existing listener when telemetry is enabled. Watson metrics are
            // scraped from /metrics; Watson spans are exported via Radiant (see TelemetryHost).
            if (_Settings.Telemetry != null && _Settings.Telemetry.Enable)
            {
                _Settings.Rest.Telemetry.Enable = true;
                _Settings.Rest.Telemetry.Prometheus.Enable = true;
                _Settings.Rest.Telemetry.Prometheus.Path = "/metrics";
            }

            _Server = new Webserver(_Settings.Rest, DefaultRoute);
            _Server.Events.Logger = (msg) => _Logging.Debug(_Header + msg);

            // Authentication is resolved in PreRouting (see below), not via Watson's AuthenticateRequest /
            // AuthenticateApiRequest hooks — those only fire for routes registered with
            // requiresAuthentication: true, whereas PreRouting fires for every request. PreRouting attaches
            // the RequestContext to ctx.Metadata; the per-route Authorize() helper enforces the 401 challenge
            // (when disabled it installs the system principal and never challenges) and RBAC (403).

            #region OpenAPI

            _Server.UseOpenApi(openApi =>
            {
                // The OpenAPI document (/openapi.json) and the Swagger UI (/swagger) are always served
                // anonymously so tooling and the dashboard's API Explorer can introspect the surface
                // without credentials. When authentication is added (plan W9), these two paths must remain
                // outside the authenticated route set.
                openApi.DocumentPath = "/openapi.json";
                openApi.SwaggerUiPath = "/swagger";
                openApi.EnableSwaggerUi = true;

                openApi.Info.Title = "SharpAI Server API";
                openApi.Info.Version = _Version;
                openApi.Info.Description =
                    "Local AI inference server with Ollama- and OpenAI-compatible REST endpoints. " +
                    "Provides model management, embeddings, completions, and chat completions against " +
                    "locally hosted GGUF models via LlamaSharp. The OpenAPI document at /openapi.json and " +
                    "the Swagger UI at /swagger are served without authentication.";
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
                openApi.Tags.Add(new OpenApiTag { Name = "Request History", Description = "Captured request/response history" });
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

                // Resolve and attach the authenticated principal for every request. Enforcement (401/403)
                // happens per-route in Authorize(); this only establishes ctx.Metadata.
                _AuthService.AttachContext(ctx);

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

                if (_RequestHistoryCapture != null) _RequestHistoryCapture.Capture(ctx);

                await Task.CompletedTask.ConfigureAwait(false);
            };

            #endregion

            #region General-Routes

            // Extracted to a per-feature registrar over a shared RouteContext (W7.T2/T3). Additional route
            // groups migrate to the same pattern incrementally; the context supplies settings through an
            // accessor so a runtime settings replacement is always observed.
            _AuthGate = new SharpAI.Server.API.REST.Routes.AuthorizationGate(() => _Settings, _RbacEngine, _Database, _Logging);

            _RouteContext = new SharpAI.Server.API.REST.Routes.RouteContext(
                    _Server,
                    _Version,
                    () => _Settings,
                    _Database,
                    _ModelFileService,
                    _ModelEngineService,
                    _TelemetryHost)
                .ConfigureControlPlane(_AuthGate, _Serializer, s => _Settings = s, Constants.SettingsFile);

            SharpAI.Server.API.REST.Routes.GeneralRoutes.Register(_RouteContext);
            SharpAI.Server.API.REST.Routes.SettingsRoutes.Register(_RouteContext);
            SharpAI.Server.API.REST.Routes.RequestHistoryRoutes.Register(_RouteContext);
            SharpAI.Server.API.REST.Routes.OllamaInferenceRoutes.Register(_RouteContext, _OllamaApiHandler, _TokenSource.Token);
            SharpAI.Server.API.REST.Routes.OpenAIInferenceRoutes.Register(_RouteContext, _OpenAIApiHandler, _TokenSource.Token);

            #endregion


            #region Authentication-Endpoints

            _Server.Post("/v1.0/token", async (req) =>
            {
                string email = HeaderValue(req.Http, "x-email");
                string password = HeaderValue(req.Http, "x-password");
                string tenantGuid = HeaderValue(req.Http, "x-tenant-guid");

                if (String.IsNullOrEmpty(email) || String.IsNullOrEmpty(password))
                    throw new WebserverException(ApiResultEnum.BadRequest, "The x-email and x-password headers are required.");

                SharpAI.Security.AuthSession session;
                string token = _AuthEngine.Login(tenantGuid, email, password, 0, out session);
                if (token == null)
                    throw new WebserverException(ApiResultEnum.NotAuthorized, "Invalid credentials.");

                return new
                {
                    token = token,
                    sessionId = session.Guid,
                    tenantId = session.TenantGuid,
                    userId = session.UserGuid,
                    expiresUtc = session.ExpiresUtc
                };
            }, api => Describe(api, "Authentication", "Log in and create a session token")
                .WithDescription(
                    "Validates an email/password login and, on success, returns an opaque bearer token that " +
                    "references a revocable server-side session. Supply credentials in the x-email, x-password, " +
                    "and optional x-tenant-guid headers (the 'default' tenant is used when omitted). This " +
                    "endpoint is anonymous so it is reachable without a prior credential.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Session token", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()));

            _Server.Get("/v1.0/token", async (req) =>
            {
                SharpAI.Security.AuthSession session = _AuthEngine.ReadSession(ExtractBearerToken(req.Http));
                if (session == null)
                    throw new WebserverException(ApiResultEnum.NotAuthorized, "The bearer token is missing, invalid, or expired.");

                return new
                {
                    sessionId = session.Guid,
                    tenantId = session.TenantGuid,
                    userId = session.UserGuid,
                    createdUtc = session.CreatedUtc,
                    expiresUtc = session.ExpiresUtc
                };
            }, api => Describe(api, "Authentication", "Read the current session")
                .WithDescription("Returns details of the session referenced by the supplied bearer token (Authorization: Bearer, or x-token).")
                .WithResponse(200, OpenApiResponseMetadata.Json("Session details", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()));

            _Server.Delete("/v1.0/token", async (req) =>
            {
                SharpAI.Security.AuthSession session = _AuthEngine.ReadSession(ExtractBearerToken(req.Http));
                if (session == null)
                    throw new WebserverException(ApiResultEnum.NotAuthorized, "The bearer token is missing, invalid, or expired.");

                _AuthEngine.RevokeSession(session.Guid, "Revoked by session holder.");
                return new { revoked = true, sessionId = session.Guid };
            }, api => Describe(api, "Authentication", "Revoke the current session")
                .WithDescription("Revokes (logs out) the session referenced by the supplied bearer token. The token is immediately invalid.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Revocation result", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()));

            _Server.Get("/v1.0/api/audit", async (req) =>
            {
                Authorize(req, SharpAI.Security.ResourceTypes.Audit, SharpAI.Security.OperationTypeEnum.Read, null);
                SharpAI.Security.RequestContext context = req.Http.Metadata as SharpAI.Security.RequestContext;

                Models.EnumerationQuery query = new Models.EnumerationQuery();
                query.ApplyQuerystringOverrides(key => req.Http.Request.Query.Elements?[key]);

                // Global admins (and the system principal when auth is disabled) may scope by the tenantGuid
                // query parameter (null = all tenants); everyone else is constrained to their own tenant.
                bool isGlobal = context == null || context.IsAdmin;
                string tenantScope = isGlobal
                    ? req.Http.Request.Query.Elements?["tenantGuid"]
                    : context.TenantGuid;

                return _Database.Audit.Enumerate(tenantScope, query);
            }, api => Describe(api, "Authentication", "List security audit events")
                .WithDescription(
                    "Paginated list of security audit events (authentication failures and privileged operations). " +
                    "Requires administrator or tenant-administrator privileges. Global administrators may scope " +
                    "with the tenantGuid query parameter; tenant administrators are constrained to their tenant.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Audit event page", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(403, OpenApiResponseMetadata.Forbidden()));

            _Server.Get("/v1.0/tenants/{tenantGuid}/users/{userGuid}/permissions", async (req) =>
            {
                string tenantGuid = req.Http.Request.Url.Parameters?["tenantGuid"];
                string userGuid = req.Http.Request.Url.Parameters?["userGuid"];
                AuthorizeInspection(req, tenantGuid, SharpAI.Security.PrincipalTypeEnum.User, userGuid);
                return EffectivePermissionsResponse(SharpAI.Security.PrincipalTypeEnum.User, userGuid, tenantGuid);
            }, api => Describe(api, "Authentication", "Inspect a user's effective permissions")
                .WithDescription(
                    "Returns the computed effective permission set for a user within a tenant, as (resourceType, " +
                    "operation, effect, scope, resourceGuid) grants. Requires Admin on the Admin resource, or the " +
                    "principal reading their own permissions.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Effective permissions", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(403, OpenApiResponseMetadata.Forbidden()));

            _Server.Get("/v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}/permissions", async (req) =>
            {
                string tenantGuid = req.Http.Request.Url.Parameters?["tenantGuid"];
                string credentialGuid = req.Http.Request.Url.Parameters?["credentialGuid"];
                AuthorizeInspection(req, tenantGuid, SharpAI.Security.PrincipalTypeEnum.Credential, credentialGuid);
                return EffectivePermissionsResponse(SharpAI.Security.PrincipalTypeEnum.Credential, credentialGuid, tenantGuid);
            }, api => Describe(api, "Authentication", "Inspect a credential's effective permissions")
                .WithDescription(
                    "Returns the computed effective permission set for a credential within a tenant. Requires Admin " +
                    "on the Admin resource, or the credential's owner reading its own permissions.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Effective permissions", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(403, OpenApiResponseMetadata.Forbidden()));

            #endregion

            #region Management-Endpoints

            // ---- Tenants (platform administration) ----

            _Server.Get("/v1.0/tenants", async (req) =>
            {
                Authorize(req, SharpAI.Security.ResourceTypes.Admin, SharpAI.Security.OperationTypeEnum.Admin, null);
                return _Database.Tenants.Enumerate(ParseEnumQuery(req));
            }, api => Describe(api, "Management - Tenants", "List tenants")
                .WithDescription("Paginated list of tenants. Requires platform-administrator (Admin) privileges.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Tenant page", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(403, OpenApiResponseMetadata.Forbidden()));

            _Server.Post<SharpAI.Security.Tenant>("/v1.0/tenants", async (req) =>
            {
                Authorize(req, SharpAI.Security.ResourceTypes.Admin, SharpAI.Security.OperationTypeEnum.Admin, null);
                SharpAI.Security.Tenant body = req.GetData<SharpAI.Security.Tenant>();
                if (body == null || String.IsNullOrEmpty(body.Name)) throw new WebserverException(ApiResultEnum.BadRequest, "A tenant name is required.");
                if (_Database.Tenants.GetByName(body.Name) != null) throw new WebserverException(ApiResultEnum.Conflict, "A tenant with that name already exists.");
                SharpAI.Security.Tenant created = new SharpAI.Security.Tenant { Name = body.Name };
                return _Database.Tenants.Create(created);
            }, api => Describe(api, "Management - Tenants", "Create a tenant")
                .WithDescription("Creates a tenant. Requires platform-administrator (Admin) privileges.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Tenant", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Created tenant", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(403, OpenApiResponseMetadata.Forbidden()));

            _Server.Get("/v1.0/tenants/{tenantGuid}", async (req) =>
            {
                string tenantGuid = ResolveManagementTenant(req, RouteParam(req, "tenantGuid"));
                Authorize(req, SharpAI.Security.ResourceTypes.Tenant, SharpAI.Security.OperationTypeEnum.Read, null);
                SharpAI.Security.Tenant tenant = _Database.Tenants.Read(tenantGuid);
                if (tenant == null) throw new WebserverException(ApiResultEnum.NotFound, "The specified tenant was not found.");
                return tenant;
            }, api => Describe(api, "Management - Tenants", "Read a tenant")
                .WithDescription("Returns a single tenant. Callers are constrained to their own tenant unless they are platform administrators.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Tenant", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            _Server.Delete("/v1.0/tenants/{tenantGuid}", async (req) =>
            {
                Authorize(req, SharpAI.Security.ResourceTypes.Admin, SharpAI.Security.OperationTypeEnum.Admin, null);
                string tenantGuid = RouteParam(req, "tenantGuid");
                SharpAI.Security.Tenant tenant = _Database.Tenants.Read(tenantGuid);
                if (tenant == null) throw new WebserverException(ApiResultEnum.NotFound, "The specified tenant was not found.");
                if (tenant.IsProtected) throw new WebserverException(ApiResultEnum.Forbidden, "This tenant is protected and cannot be deleted.");
                _Database.Tenants.Delete(tenantGuid);
                return new { deleted = true, tenantId = tenantGuid };
            }, api => Describe(api, "Management - Tenants", "Delete a tenant")
                .WithDescription("Deletes a tenant. Protected tenants cannot be deleted. Requires platform-administrator (Admin) privileges.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Delete result", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            // ---- Users ----

            _Server.Get("/v1.0/tenants/{tenantGuid}/users", async (req) =>
            {
                string tenantGuid = ResolveManagementTenant(req, RouteParam(req, "tenantGuid"));
                Authorize(req, SharpAI.Security.ResourceTypes.User, SharpAI.Security.OperationTypeEnum.Read, null);
                Models.EnumerationResult<SharpAI.Security.User> page = _Database.Users.Enumerate(tenantGuid, ParseEnumQuery(req));
                if (page.Objects != null) foreach (SharpAI.Security.User u in page.Objects) Redact(u);
                return page;
            }, api => Describe(api, "Management - Users", "List users")
                .WithDescription("Paginated, tenant-scoped list of users. Password hashes are redacted.")
                .WithResponse(200, OpenApiResponseMetadata.Json("User page", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(403, OpenApiResponseMetadata.Forbidden()));

            _Server.Post<SharpAI.Server.Classes.Requests.CreateUserRequest>("/v1.0/tenants/{tenantGuid}/users", async (req) =>
            {
                string tenantGuid = ResolveManagementTenant(req, RouteParam(req, "tenantGuid"));
                Authorize(req, SharpAI.Security.ResourceTypes.User, SharpAI.Security.OperationTypeEnum.Admin, null);
                SharpAI.Server.Classes.Requests.CreateUserRequest body = req.GetData<SharpAI.Server.Classes.Requests.CreateUserRequest>();
                if (body == null || String.IsNullOrEmpty(body.Email)) throw new WebserverException(ApiResultEnum.BadRequest, "An email address is required.");
                if (_Database.Users.GetByEmail(tenantGuid, body.Email) != null) throw new WebserverException(ApiResultEnum.Conflict, "A user with that email already exists in this tenant.");

                SharpAI.Security.User user = new SharpAI.Security.User
                {
                    TenantGuid = tenantGuid,
                    Email = body.Email,
                    FirstName = body.FirstName,
                    LastName = body.LastName,
                    PasswordSha256 = SharpAI.Security.PasswordHasher.Hash(body.Password),
                    IsAdmin = body.IsAdmin,
                    IsTenantAdmin = body.IsTenantAdmin
                };
                _Database.Users.Create(user);
                return Redact(user);
            }, api => Describe(api, "Management - Users", "Create a user")
                .WithDescription("Creates a tenant user. The plaintext password is hashed server-side and never returned.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Create user request", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Created user", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(403, OpenApiResponseMetadata.Forbidden()));

            _Server.Get("/v1.0/tenants/{tenantGuid}/users/{userGuid}", async (req) =>
            {
                string tenantGuid = ResolveManagementTenant(req, RouteParam(req, "tenantGuid"));
                Authorize(req, SharpAI.Security.ResourceTypes.User, SharpAI.Security.OperationTypeEnum.Read, null);
                SharpAI.Security.User user = _Database.Users.Read(RouteParam(req, "userGuid"));
                if (user == null || !String.Equals(user.TenantGuid, tenantGuid, StringComparison.Ordinal))
                    throw new WebserverException(ApiResultEnum.NotFound, "The specified user was not found.");
                return Redact(user);
            }, api => Describe(api, "Management - Users", "Read a user")
                .WithDescription("Returns a single user (password hash redacted).")
                .WithResponse(200, OpenApiResponseMetadata.Json("User", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            _Server.Delete("/v1.0/tenants/{tenantGuid}/users/{userGuid}", async (req) =>
            {
                string tenantGuid = ResolveManagementTenant(req, RouteParam(req, "tenantGuid"));
                Authorize(req, SharpAI.Security.ResourceTypes.User, SharpAI.Security.OperationTypeEnum.Admin, null);
                string userGuid = RouteParam(req, "userGuid");
                SharpAI.Security.User user = _Database.Users.Read(userGuid);
                if (user == null || !String.Equals(user.TenantGuid, tenantGuid, StringComparison.Ordinal))
                    throw new WebserverException(ApiResultEnum.NotFound, "The specified user was not found.");
                if (user.IsProtected) throw new WebserverException(ApiResultEnum.Forbidden, "This user is protected and cannot be deleted.");

                // Cascade: remove the user's role assignments and owned credentials.
                foreach (SharpAI.Security.UserRoleAssignment a in _Database.UserRoleAssignments.GetForUser(tenantGuid, userGuid)) _Database.UserRoleAssignments.Delete(a.Guid);
                Models.EnumerationResult<SharpAI.Security.Credential> creds = _Database.Credentials.Enumerate(tenantGuid, new Models.EnumerationQuery { PageSize = 1000 });
                if (creds.Objects != null) foreach (SharpAI.Security.Credential c in creds.Objects) { if (String.Equals(c.UserGuid, userGuid, StringComparison.Ordinal)) _Database.Credentials.Delete(c.Guid); }
                _Database.Users.Delete(userGuid);
                return new { deleted = true, userId = userGuid };
            }, api => Describe(api, "Management - Users", "Delete a user")
                .WithDescription("Deletes a user and cascades to their credentials and role assignments. Protected users cannot be deleted.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Delete result", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            // ---- Credentials ----

            _Server.Get("/v1.0/tenants/{tenantGuid}/credentials", async (req) =>
            {
                string tenantGuid = ResolveManagementTenant(req, RouteParam(req, "tenantGuid"));
                Authorize(req, SharpAI.Security.ResourceTypes.Credential, SharpAI.Security.OperationTypeEnum.Read, null);
                Models.EnumerationResult<SharpAI.Security.Credential> page = _Database.Credentials.Enumerate(tenantGuid, ParseEnumQuery(req));
                if (page.Objects != null) foreach (SharpAI.Security.Credential c in page.Objects) Redact(c);
                return page;
            }, api => Describe(api, "Management - Credentials", "List credentials")
                .WithDescription("Paginated, tenant-scoped list of credentials. Secret hashes are redacted.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Credential page", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(403, OpenApiResponseMetadata.Forbidden()));

            _Server.Post<SharpAI.Server.Classes.Requests.CreateCredentialRequest>("/v1.0/tenants/{tenantGuid}/credentials", async (req) =>
            {
                string tenantGuid = ResolveManagementTenant(req, RouteParam(req, "tenantGuid"));
                Authorize(req, SharpAI.Security.ResourceTypes.Credential, SharpAI.Security.OperationTypeEnum.Admin, null);
                SharpAI.Server.Classes.Requests.CreateCredentialRequest body = req.GetData<SharpAI.Server.Classes.Requests.CreateCredentialRequest>();
                if (body == null || String.IsNullOrEmpty(body.UserGuid)) throw new WebserverException(ApiResultEnum.BadRequest, "A userGuid is required.");
                SharpAI.Security.User owner = _Database.Users.Read(body.UserGuid);
                if (owner == null || !String.Equals(owner.TenantGuid, tenantGuid, StringComparison.Ordinal))
                    throw new WebserverException(ApiResultEnum.BadRequest, "The specified owning user does not exist in this tenant.");

                string secretKey = SharpAI.Security.CredentialKeyGenerator.GenerateSecretKey();
                SharpAI.Security.Credential credential = new SharpAI.Security.Credential
                {
                    UserGuid = body.UserGuid,
                    TenantGuid = tenantGuid,
                    Name = body.Name,
                    AccessKey = SharpAI.Security.CredentialKeyGenerator.GenerateAccessKey(),
                    SecretSha256 = SharpAI.Security.PasswordHasher.Hash(secretKey),
                    ExpiresUtc = body.ExpiresUtc
                };
                _Database.Credentials.Create(credential);

                // The plaintext secret is returned exactly once.
                return new
                {
                    guid = credential.Guid,
                    userId = credential.UserGuid,
                    tenantId = credential.TenantGuid,
                    name = credential.Name,
                    accessKey = credential.AccessKey,
                    secretKey = secretKey,
                    expiresUtc = credential.ExpiresUtc
                };
            }, api => Describe(api, "Management - Credentials", "Create a credential")
                .WithDescription("Creates a credential and returns the access key plus the plaintext secret key ONCE. The secret is never retrievable again.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Create credential request", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Created credential (with one-time secret)", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(403, OpenApiResponseMetadata.Forbidden()));

            _Server.Get("/v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}", async (req) =>
            {
                string tenantGuid = ResolveManagementTenant(req, RouteParam(req, "tenantGuid"));
                Authorize(req, SharpAI.Security.ResourceTypes.Credential, SharpAI.Security.OperationTypeEnum.Read, null);
                SharpAI.Security.Credential credential = _Database.Credentials.Read(RouteParam(req, "credentialGuid"));
                if (credential == null || !String.Equals(credential.TenantGuid, tenantGuid, StringComparison.Ordinal))
                    throw new WebserverException(ApiResultEnum.NotFound, "The specified credential was not found.");
                return Redact(credential);
            }, api => Describe(api, "Management - Credentials", "Read a credential")
                .WithDescription("Returns a single credential (secret hash redacted).")
                .WithResponse(200, OpenApiResponseMetadata.Json("Credential", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            _Server.Delete("/v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}", async (req) =>
            {
                string tenantGuid = ResolveManagementTenant(req, RouteParam(req, "tenantGuid"));
                Authorize(req, SharpAI.Security.ResourceTypes.Credential, SharpAI.Security.OperationTypeEnum.Admin, null);
                string credentialGuid = RouteParam(req, "credentialGuid");
                SharpAI.Security.Credential credential = _Database.Credentials.Read(credentialGuid);
                if (credential == null || !String.Equals(credential.TenantGuid, tenantGuid, StringComparison.Ordinal))
                    throw new WebserverException(ApiResultEnum.NotFound, "The specified credential was not found.");
                _Database.Credentials.Delete(credentialGuid);
                return new { deleted = true, credentialId = credentialGuid };
            }, api => Describe(api, "Management - Credentials", "Delete a credential")
                .WithDescription("Deletes a credential.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Delete result", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            // ---- Roles ----

            _Server.Get("/v1.0/tenants/{tenantGuid}/roles", async (req) =>
            {
                string tenantGuid = ResolveManagementTenant(req, RouteParam(req, "tenantGuid"));
                Authorize(req, SharpAI.Security.ResourceTypes.Role, SharpAI.Security.OperationTypeEnum.Read, null);
                return _Database.Roles.Enumerate(tenantGuid, ParseEnumQuery(req));
            }, api => Describe(api, "Management - Roles", "List roles")
                .WithDescription("Paginated list of the tenant's custom roles plus the globally-visible built-in roles.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Role page", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(403, OpenApiResponseMetadata.Forbidden()));

            _Server.Post<SharpAI.Security.UserRole>("/v1.0/tenants/{tenantGuid}/roles", async (req) =>
            {
                string tenantGuid = ResolveManagementTenant(req, RouteParam(req, "tenantGuid"));
                Authorize(req, SharpAI.Security.ResourceTypes.Role, SharpAI.Security.OperationTypeEnum.Admin, null);
                SharpAI.Security.UserRole body = req.GetData<SharpAI.Security.UserRole>();
                if (body == null || String.IsNullOrEmpty(body.Name)) throw new WebserverException(ApiResultEnum.BadRequest, "A role name is required.");
                SharpAI.Security.UserRole role = new SharpAI.Security.UserRole { TenantGuid = tenantGuid, Name = body.Name, IsBuiltIn = false };
                return _Database.Roles.Create(role);
            }, api => Describe(api, "Management - Roles", "Create a custom role")
                .WithDescription("Creates a tenant-scoped custom role. Built-in roles are immutable; clone by creating a custom role and mapping permissions.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Role", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Created role", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(403, OpenApiResponseMetadata.Forbidden()));

            _Server.Delete("/v1.0/tenants/{tenantGuid}/roles/{roleGuid}", async (req) =>
            {
                string tenantGuid = ResolveManagementTenant(req, RouteParam(req, "tenantGuid"));
                Authorize(req, SharpAI.Security.ResourceTypes.Role, SharpAI.Security.OperationTypeEnum.Admin, null);
                SharpAI.Security.UserRole role = _Database.Roles.Read(RouteParam(req, "roleGuid"));
                if (role == null) throw new WebserverException(ApiResultEnum.NotFound, "The specified role was not found.");
                if (role.IsBuiltIn || role.IsProtected) throw new WebserverException(ApiResultEnum.Forbidden, "Built-in and protected roles cannot be deleted.");
                if (!String.Equals(role.TenantGuid, tenantGuid, StringComparison.Ordinal)) throw new WebserverException(ApiResultEnum.NotFound, "The specified role was not found.");
                _Database.Roles.Delete(role.Guid);
                return new { deleted = true, roleId = role.Guid };
            }, api => Describe(api, "Management - Roles", "Delete a custom role")
                .WithDescription("Deletes a tenant-scoped custom role. Built-in and protected roles cannot be deleted.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Delete result", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            // ---- Permissions ----

            _Server.Get("/v1.0/tenants/{tenantGuid}/permissions", async (req) =>
            {
                string tenantGuid = ResolveManagementTenant(req, RouteParam(req, "tenantGuid"));
                Authorize(req, SharpAI.Security.ResourceTypes.Permission, SharpAI.Security.OperationTypeEnum.Read, null);
                return _Database.Permissions.Enumerate(tenantGuid, ParseEnumQuery(req));
            }, api => Describe(api, "Management - Permissions", "List permissions")
                .WithDescription("Paginated list of the tenant's permissions plus globally-visible built-in permissions.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Permission page", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(403, OpenApiResponseMetadata.Forbidden()));

            _Server.Post<SharpAI.Security.Permission>("/v1.0/tenants/{tenantGuid}/permissions", async (req) =>
            {
                string tenantGuid = ResolveManagementTenant(req, RouteParam(req, "tenantGuid"));
                Authorize(req, SharpAI.Security.ResourceTypes.Permission, SharpAI.Security.OperationTypeEnum.Admin, null);
                SharpAI.Security.Permission body = req.GetData<SharpAI.Security.Permission>();
                if (body == null || body.ResourceTypes.Count == 0 || body.OperationTypes.Count == 0)
                    throw new WebserverException(ApiResultEnum.BadRequest, "resourceTypes and operationTypes are required.");
                SharpAI.Security.Permission permission = new SharpAI.Security.Permission
                {
                    TenantGuid = tenantGuid,
                    Name = body.Name,
                    ResourceTypes = body.ResourceTypes,
                    OperationTypes = body.OperationTypes,
                    Effect = body.Effect
                };
                return _Database.Permissions.Create(permission);
            }, api => Describe(api, "Management - Permissions", "Create a permission")
                .WithDescription("Creates a tenant-scoped permission (Permit or Deny) over resource types and operations.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Permission", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Created permission", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(403, OpenApiResponseMetadata.Forbidden()));

            _Server.Post("/v1.0/tenants/{tenantGuid}/roles/{roleGuid}/permissions/{permissionGuid}", async (req) =>
            {
                string tenantGuid = ResolveManagementTenant(req, RouteParam(req, "tenantGuid"));
                Authorize(req, SharpAI.Security.ResourceTypes.Role, SharpAI.Security.OperationTypeEnum.Admin, null);
                SharpAI.Security.UserRole role = _Database.Roles.Read(RouteParam(req, "roleGuid"));
                SharpAI.Security.Permission permission = _Database.Permissions.Read(RouteParam(req, "permissionGuid"));
                if (role == null || permission == null) throw new WebserverException(ApiResultEnum.NotFound, "The specified role or permission was not found.");
                if (role.IsBuiltIn) throw new WebserverException(ApiResultEnum.Forbidden, "Built-in roles are immutable.");
                if (!String.Equals(role.TenantGuid, tenantGuid, StringComparison.Ordinal)) throw new WebserverException(ApiResultEnum.NotFound, "The specified role was not found.");
                SharpAI.Security.RolePermissionMap map = _Database.RolePermissionMaps.Create(new SharpAI.Security.RolePermissionMap { TenantGuid = tenantGuid, RoleGuid = role.Guid, PermissionGuid = permission.Guid });
                return map;
            }, api => Describe(api, "Management - Roles", "Map a permission to a role")
                .WithDescription("Associates a permission with a custom role. Built-in roles are immutable.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Mapping", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            // ---- Role assignments ----

            _Server.Get("/v1.0/tenants/{tenantGuid}/users/{userGuid}/assignments", async (req) =>
            {
                string tenantGuid = ResolveManagementTenant(req, RouteParam(req, "tenantGuid"));
                Authorize(req, SharpAI.Security.ResourceTypes.Assignment, SharpAI.Security.OperationTypeEnum.Read, null);
                return _Database.UserRoleAssignments.Enumerate(tenantGuid, RouteParam(req, "userGuid"), ParseEnumQuery(req));
            }, api => Describe(api, "Management - Assignments", "List a user's role assignments")
                .WithDescription("Paginated list of a user's role assignments within the tenant.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Assignment page", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(403, OpenApiResponseMetadata.Forbidden()));

            _Server.Post<SharpAI.Server.Classes.Requests.CreateAssignmentRequest>("/v1.0/tenants/{tenantGuid}/assignments", async (req) =>
            {
                string tenantGuid = ResolveManagementTenant(req, RouteParam(req, "tenantGuid"));
                Authorize(req, SharpAI.Security.ResourceTypes.Assignment, SharpAI.Security.OperationTypeEnum.Admin, null);
                SharpAI.Server.Classes.Requests.CreateAssignmentRequest body = req.GetData<SharpAI.Server.Classes.Requests.CreateAssignmentRequest>();
                if (body == null || String.IsNullOrEmpty(body.UserGuid)) throw new WebserverException(ApiResultEnum.BadRequest, "A userGuid is required.");
                if (String.IsNullOrEmpty(body.RoleGuid) && String.IsNullOrEmpty(body.RoleName)) throw new WebserverException(ApiResultEnum.BadRequest, "A roleGuid or roleName is required.");
                SharpAI.Security.User target = _Database.Users.Read(body.UserGuid);
                if (target == null || !String.Equals(target.TenantGuid, tenantGuid, StringComparison.Ordinal)) throw new WebserverException(ApiResultEnum.BadRequest, "The specified user does not exist in this tenant.");

                SharpAI.Security.UserRoleAssignment assignment = new SharpAI.Security.UserRoleAssignment
                {
                    TenantGuid = tenantGuid,
                    UserGuid = body.UserGuid,
                    RoleGuid = body.RoleGuid,
                    RoleName = body.RoleName,
                    ResourceScope = body.ResourceScope,
                    ResourceGuid = body.ResourceGuid,
                    InheritsToChildren = body.InheritsToChildren
                };
                return _Database.UserRoleAssignments.Create(assignment);
            }, api => Describe(api, "Management - Assignments", "Assign a role to a user")
                .WithDescription("Grants a role to a user at tenant or resource scope. The role may be referenced by GUID or name.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Create assignment request", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Created assignment", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(403, OpenApiResponseMetadata.Forbidden()));

            _Server.Delete("/v1.0/tenants/{tenantGuid}/assignments/{assignmentGuid}", async (req) =>
            {
                string tenantGuid = ResolveManagementTenant(req, RouteParam(req, "tenantGuid"));
                Authorize(req, SharpAI.Security.ResourceTypes.Assignment, SharpAI.Security.OperationTypeEnum.Admin, null);
                SharpAI.Security.UserRoleAssignment assignment = _Database.UserRoleAssignments.Read(RouteParam(req, "assignmentGuid"));
                if (assignment == null || !String.Equals(assignment.TenantGuid, tenantGuid, StringComparison.Ordinal))
                    throw new WebserverException(ApiResultEnum.NotFound, "The specified assignment was not found.");
                _Database.UserRoleAssignments.Delete(assignment.Guid);
                return new { deleted = true, assignmentId = assignment.Guid };
            }, api => Describe(api, "Management - Assignments", "Revoke a role assignment")
                .WithDescription("Removes a role assignment from a user.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Delete result", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            #endregion

            #region Ollama-Endpoints

            _Server.Post<OllamaPullModelRequest>("/api/pull", async (req) =>
            {
                Authorize(req, SharpAI.Security.ResourceTypes.Model, SharpAI.Security.OperationTypeEnum.Write, null);
                OllamaPullModelRequest pmr = req.GetData<OllamaPullModelRequest>();
                return await _OllamaApiHandler.PullModel(req, pmr, _TokenSource.Token).ConfigureAwait(false);
            }, api => Describe(api, "Ollama - Models", "Pull a model")
                .WithDescription("Downloads a model from HuggingFace. Streams progress as newline-delimited JSON.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Pull model request", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Progress stream", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest()));

            _Server.Post<SharpAI.Server.Classes.Requests.ImportModelRequest>("/api/import", async (req) =>
            {
                Authorize(req, SharpAI.Security.ResourceTypes.Model, SharpAI.Security.OperationTypeEnum.Write, null);
                SharpAI.Server.Classes.Requests.ImportModelRequest imr = req.GetData<SharpAI.Server.Classes.Requests.ImportModelRequest>();
                return await _OllamaApiHandler.ImportModel(req, imr, _TokenSource.Token).ConfigureAwait(false);
            }, api => Describe(api, "Ollama - Models", "Import a local GGUF model")
                .WithDescription(
                    "Registers a GGUF model that already exists on the server's local filesystem — no download " +
                    "and no HuggingFace token. Supply the local file 'path' and an optional 'name' (defaults to " +
                    "the file name). The file is copied into the models directory and its capabilities are " +
                    "detected from GGUF metadata. Returns the registered model details.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Import model request", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Imported model details", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(404, OpenApiResponseMetadata.NotFound())
                .WithResponse(409, OpenApiResponseMetadata.Json("Conflict — model name already exists", OpenApiSchemaMetadata.Create("object"))));

            #region Model-Presets

            _Server.Get("/v1.0/models/presets", async (req) =>
            {
                Authorize(req, SharpAI.Security.ResourceTypes.Model, SharpAI.Security.OperationTypeEnum.Read, null);
                return _Database.Presets.Enumerate(ParseEnumQuery(req));
            }, api => Describe(api, "Ollama - Models", "List model presets")
                .WithDescription("Paginated list of Modelfile-equivalent presets (system prompt, sampling defaults, stop, template override).")
                .WithResponse(200, OpenApiResponseMetadata.Json("Preset page", OpenApiSchemaMetadata.Create("object"))));

            _Server.Post<SharpAI.Server.Classes.Requests.CreatePresetRequest>("/v1.0/models/presets", async (req) =>
            {
                Authorize(req, SharpAI.Security.ResourceTypes.Model, SharpAI.Security.OperationTypeEnum.Write, null);
                SharpAI.Server.Classes.Requests.CreatePresetRequest body = req.GetData<SharpAI.Server.Classes.Requests.CreatePresetRequest>();
                if (body == null || String.IsNullOrEmpty(body.Name)) throw new WebserverException(ApiResultEnum.BadRequest, "A preset name is required.");
                if (String.IsNullOrEmpty(body.Model)) throw new WebserverException(ApiResultEnum.BadRequest, "A base model name is required.");
                if (_ModelFileService.GetByName(body.Model) == null) throw new WebserverException(ApiResultEnum.NotFound, "The base model '" + body.Model + "' was not found.");
                if (_Database.Presets.GetByName(body.Name) != null) throw new WebserverException(ApiResultEnum.Conflict, "A preset with that name already exists.");

                Models.ModelPreset preset = new Models.ModelPreset
                {
                    Name = body.Name,
                    ModelName = body.Model,
                    SystemPrompt = body.System,
                    Temperature = body.Temperature,
                    MaxTokens = body.MaxTokens,
                    TopP = body.TopP,
                    TemplateOverride = body.Template,
                    Stop = body.Stop ?? new List<string>()
                };
                return _Database.Presets.Add(preset);
            }, api => Describe(api, "Ollama - Models", "Create a model preset")
                .WithDescription("Creates a Modelfile-equivalent preset. A request that names the preset as its model runs the base model with these defaults applied unless overridden.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Preset", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Created preset", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(404, OpenApiResponseMetadata.NotFound())
                .WithResponse(409, OpenApiResponseMetadata.Json("Conflict — preset name already exists", OpenApiSchemaMetadata.Create("object"))));

            _Server.Get("/v1.0/models/presets/{name}", async (req) =>
            {
                Authorize(req, SharpAI.Security.ResourceTypes.Model, SharpAI.Security.OperationTypeEnum.Read, null);
                string name = req.Http.Request.Url.Parameters?["name"];
                Models.ModelPreset preset = _Database.Presets.GetByName(name);
                if (preset == null) throw new WebserverException(ApiResultEnum.NotFound, "The specified preset was not found.");
                return preset;
            }, api => Describe(api, "Ollama - Models", "Read a model preset")
                .WithDescription("Returns a single preset by name.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Preset", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            _Server.Delete("/v1.0/models/presets/{name}", async (req) =>
            {
                Authorize(req, SharpAI.Security.ResourceTypes.Model, SharpAI.Security.OperationTypeEnum.Delete, null);
                string name = req.Http.Request.Url.Parameters?["name"];
                Models.ModelPreset preset = _Database.Presets.GetByName(name);
                if (preset == null) throw new WebserverException(ApiResultEnum.NotFound, "The specified preset was not found.");
                _Database.Presets.Delete(preset.GUID);
                return new { deleted = true, name = name };
            }, api => Describe(api, "Ollama - Models", "Delete a model preset")
                .WithDescription("Deletes a preset by name.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Delete result", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            #endregion

            _Server.Delete<OllamaDeleteModelRequest>("/api/delete", async (req) =>
            {
                Authorize(req, SharpAI.Security.ResourceTypes.Model, SharpAI.Security.OperationTypeEnum.Delete, null);
                OllamaDeleteModelRequest dmr = req.GetData<OllamaDeleteModelRequest>();
                return await _OllamaApiHandler.DeleteModel(req, dmr, _TokenSource.Token).ConfigureAwait(false);
            }, api => Describe(api, "Ollama - Models", "Delete a model")
                .WithDescription("Deletes a locally stored model by name.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Delete model request", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Deletion result", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            _Server.Post<OllamaUnloadModelRequest>("/api/unload", async (req) =>
            {
                Authorize(req, SharpAI.Security.ResourceTypes.Model, SharpAI.Security.OperationTypeEnum.Write, null);
                OllamaUnloadModelRequest umr = req.GetData<OllamaUnloadModelRequest>();
                return await _OllamaApiHandler.UnloadModel(req, umr, _TokenSource.Token).ConfigureAwait(false);
            }, api => Describe(api, "Ollama - Models", "Unload a model from memory")
                .WithDescription(
                    "Unloads a model from memory, freeing GPU/CPU resources. " +
                    "If no model name is provided, all loaded models are unloaded. " +
                    "This is useful when switching between models on memory-constrained systems.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Unload model request", false))
                .WithResponse(200, OpenApiResponseMetadata.Json("Unload result", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            _Server.Get("/api/tags", async (req) =>
            {
                Authorize(req, SharpAI.Security.ResourceTypes.Model, SharpAI.Security.OperationTypeEnum.Read, null);
                return await _OllamaApiHandler.ListLocalModels(req, _TokenSource.Token).ConfigureAwait(false);
            }, api => Describe(api, "Ollama - Models", "List local models")
                .WithDescription("Returns the list of locally available models.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Local models", OpenApiSchemaMetadata.Create("object"))));

            _Server.Get("/api/ps", async (req) =>
            {
                Authorize(req, SharpAI.Security.ResourceTypes.Model, SharpAI.Security.OperationTypeEnum.Read, null);
                return await _OllamaApiHandler.ListRunningModels(req, _TokenSource.Token).ConfigureAwait(false);
            }, api => Describe(api, "Ollama - Models", "List running (loaded) models")
                .WithDescription(
                    "Returns the list of models that are currently loaded in memory, matching the Ollama " +
                    "'/api/ps' (ollama ps) endpoint. The size_vram field reports the full model size when " +
                    "the CUDA or Metal backend is active and 0 when the CPU backend is active. When keep-alive " +
                    "eviction is enabled (SHARPAI_KEEP_ALIVE_SECONDS), expires_at reports the eviction time; " +
                    "otherwise it is null.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Running models", OpenApiSchemaMetadata.Create("object"))));

            _Server.Get("/api/version", async (req) =>
            {
                return new { version = _Version };
            }, api => Describe(api, "Ollama - Models", "Server version")
                .WithDescription("Returns the SharpAI server version, matching Ollama's /api/version.")
                .WithResponse(200, OpenApiResponseMetadata.Json("Version", OpenApiSchemaMetadata.Create("object"))));

            _Server.Post<OllamaShowModelInfoRequest>("/api/show", async (req) =>
            {
                Authorize(req, SharpAI.Security.ResourceTypes.Model, SharpAI.Security.OperationTypeEnum.Read, null);
                OllamaShowModelInfoRequest sr = req.GetData<OllamaShowModelInfoRequest>();
                if (sr == null || String.IsNullOrEmpty(sr.Model))
                    throw new WebserverException(ApiResultEnum.BadRequest, "A model name is required.");

                Models.ModelFile mf = _ModelFileService.GetByName(sr.Model);
                if (mf == null)
                    throw new WebserverException(ApiResultEnum.NotFound, "The specified model was not found.");

                return new
                {
                    model = mf.Name,
                    details = new
                    {
                        format = "gguf",
                        family = mf.Family,
                        parameter_size = mf.ParameterSize,
                        quantization_level = mf.Quantization
                    },
                    capabilities = new { embeddings = mf.Embeddings, completions = mf.Completions },
                    size = mf.ContentLength,
                    digest = mf.SHA256Hash,
                    created_at = (object)(mf.ModelCreationUtc ?? mf.CreatedUtc)
                };
            }, api => Describe(api, "Ollama - Models", "Show model information")
                .WithDescription("Returns metadata and capabilities for a local model, matching Ollama's /api/show.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.Create("object"), "Show request", true))
                .WithResponse(200, OpenApiResponseMetadata.Json("Model info", OpenApiSchemaMetadata.Create("object")))
                .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            #endregion

        }

        private static List<Models.ModelFile> CollectAllModels()
        {
            // The Ollama /api/tags and OpenAI /v1/models contracts return the full model list. There is no
            // "get all" data-access method; we page through the enumeration to materialize the full set.
            List<Models.ModelFile> all = new List<Models.ModelFile>();
            Models.EnumerationQuery query = new Models.EnumerationQuery
            {
                PageSize = 1000,
                Order = Models.EnumerationOrderEnum.CreatedDescending
            };

            while (true)
            {
                Models.EnumerationResult<Models.ModelFile> page = _ModelFileService.Enumerate(query);
                if (page.Objects != null && page.Objects.Count > 0) all.AddRange(page.Objects);
                if (page.EndOfResults) break;
                query.PageNumber = query.PageNumber + 1;
            }

            return all;
        }

        // Wraps an inference handler so every pre-stream error is emitted as an OpenAI/Ollama-shaped error
        // envelope ({"error":{"message":...,"type":...}}) rather than Watson's default error shape, so
        // OpenAI/Ollama client SDKs never throw on parse. Errors that occur after streaming has begun cannot
        // change the response and are swallowed by the writer.
        private static bool DirectoryExistsAndWritable(string directory)
        {
            return SharpAI.Server.API.REST.Routes.RouteHelpers.DirectoryExistsAndWritable(directory);
        }

        private static async Task DefaultRoute(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 404;
            ctx.Response.ContentType = Constants.JsonContentType;
            await ctx.Response.Send("{\"error\":\"NotFound\",\"message\":\"No route matched\"}").ConfigureAwait(false);
        }

        private static OpenApiRouteMetadata Describe(OpenApiRouteMetadata api, string tag, string summary)
        {
            return SharpAI.Server.API.REST.Routes.RouteHelpers.Describe(api, tag, summary);
        }

        private static string HeaderValue(WatsonWebserver.Core.HttpContextBase ctx, string name)
        {
            return SharpAI.Server.API.REST.Routes.RouteHelpers.HeaderValue(ctx, name);
        }

        private static string ExtractBearerToken(WatsonWebserver.Core.HttpContextBase ctx)
        {
            return SharpAI.Server.API.REST.Routes.RouteHelpers.ExtractBearerToken(ctx);
        }

        /// <summary>
        /// Enforce RBAC for a route. When authentication is disabled the request runs as the implicit
        /// system principal (<c>IsAdmin</c>) and this is a no-op — preserving Ollama-parity open access.
        /// When enabled, administrators bypass; every other principal is evaluated against its effective
        /// permissions, and a denial is audited and surfaced as HTTP 403.
        /// </summary>
        private static void Authorize(WatsonWebserver.Core.ApiRequest req, string resourceType, SharpAI.Security.OperationTypeEnum operation, string resourceGuid)
        {
            _AuthGate.Authorize(req, resourceType, operation, resourceGuid);
        }

        private static void AuthorizeInspection(
            WatsonWebserver.Core.ApiRequest req,
            string tenantGuid,
            SharpAI.Security.PrincipalTypeEnum principalType,
            string principalGuid)
        {
            _AuthGate.AuthorizeInspection(req, tenantGuid, principalType, principalGuid);
        }

        private static string RouteParam(WatsonWebserver.Core.ApiRequest req, string name)
        {
            return SharpAI.Server.API.REST.Routes.RouteHelpers.RouteParam(req, name);
        }

        private static Models.EnumerationQuery ParseEnumQuery(WatsonWebserver.Core.ApiRequest req)
        {
            return SharpAI.Server.API.REST.Routes.RouteHelpers.ParseEnumQuery(req);
        }

        /// <summary>
        /// Enforce tenant isolation for a management route and return the effective tenant. The implicit
        /// system principal (auth disabled) and global administrators may target any tenant; every other
        /// principal is constrained to its own tenant.
        /// </summary>
        private static string ResolveManagementTenant(WatsonWebserver.Core.ApiRequest req, string routeTenantGuid)
        {
            if (String.IsNullOrEmpty(routeTenantGuid))
                throw new WebserverException(ApiResultEnum.BadRequest, "A tenant identifier is required.");

            SharpAI.Security.RequestContext context = req.Http.Metadata as SharpAI.Security.RequestContext;
            if (context == null || context.IsAdmin) return routeTenantGuid;

            if (!String.IsNullOrEmpty(context.TenantGuid) &&
                String.Equals(context.TenantGuid, routeTenantGuid, StringComparison.Ordinal))
            {
                return routeTenantGuid;
            }

            throw new WebserverException(ApiResultEnum.Forbidden, "Cross-tenant access is not permitted.");
        }

        private static SharpAI.Security.User Redact(SharpAI.Security.User user)
        {
            if (user != null) user.PasswordSha256 = null;
            return user;
        }

        private static SharpAI.Security.Credential Redact(SharpAI.Security.Credential credential)
        {
            if (credential != null) credential.SecretSha256 = null;
            return credential;
        }

        private static object EffectivePermissionsResponse(
            SharpAI.Security.PrincipalTypeEnum principalType,
            string principalGuid,
            string tenantGuid)
        {
            System.Collections.Generic.List<SharpAI.Security.EffectivePermission> effective =
                _RbacEngine.GetEffectivePermissions(principalType, principalGuid, tenantGuid);

            System.Collections.Generic.List<object> grants = new System.Collections.Generic.List<object>();
            foreach (SharpAI.Security.EffectivePermission permission in effective)
            {
                grants.Add(new
                {
                    resourceType = permission.ResourceType,
                    operation = permission.Operation.ToString(),
                    effect = permission.Effect.ToString(),
                    scope = permission.ResourceScope.ToString(),
                    resourceGuid = permission.ResourceGuid,
                    inheritsToChildren = permission.InheritsToChildren
                });
            }

            return new
            {
                tenantId = tenantGuid,
                principalType = principalType.ToString(),
                principalId = principalGuid,
                count = grants.Count,
                permissions = grants
            };
        }

        #endregion

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}
