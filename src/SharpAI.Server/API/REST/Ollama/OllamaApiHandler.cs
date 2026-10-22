namespace SharpAI.Server.API.REST.Ollama
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Channels;
    using SharpAI.Engines;
    using SharpAI.Grammars;
    using SharpAI.Helpers;
    using SharpAI.Hosting;
    using SharpAI.Hosting.HuggingFace;
    using SharpAI.Models;
    using SharpAI.Models.Ollama;
    using SharpAI.Operations;
    using SharpAI.Prompts;
    using SharpAI.Serialization;
    using SharpAI.Server.Classes.Settings;
    using SharpAI.Services;
    using SharpAI.Tools;
    using SyslogLogging;
    using WatsonWebserver.Core;
    using Constants = SharpAI.Constants;

    internal class OllamaApiHandler
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        #region Public-Members

        #endregion

        #region Private-Members

        private string _Header = "[OllamaApiHandler] ";
        private Settings _Settings = null;
        private LoggingModule _Logging = null;
        private Serializer _Serializer = null;
        private ModelFileService _ModelFileService = null;
        private ModelEngineService _ModelEngineService = null;
        private HuggingFaceClient _HuggingFaceClient = null;
        private SharpAI.Database.Interfaces.IModelPresetMethods _Presets = null;

        private static string _TimestampFormat = "yyyy-MM-ddTHH:mm:ss.ffffffZ";


        #endregion

        #region Constructors-and-Factories

        internal OllamaApiHandler(
            Settings settings,
            LoggingModule logging,
            Serializer serializer,
            ModelFileService modelFileService,
            ModelEngineService modelEngineService,
            HuggingFaceClient huggingFaceClient,
            SharpAI.Database.Interfaces.IModelPresetMethods presets)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _ModelFileService = modelFileService ?? throw new ArgumentNullException(nameof(modelFileService));
            _ModelEngineService = modelEngineService ?? throw new ArgumentNullException(nameof(modelEngineService));
            _HuggingFaceClient = huggingFaceClient ?? throw new ArgumentNullException(nameof(huggingFaceClient));
            _Presets = presets;

            SharpAI.Logging.JsonLog.Debug(_Logging, _Header + "initialized");
        }

        #endregion

        #region Public-Methods

        internal async Task<object> PullModel(
            ApiRequest req,
            OllamaPullModelRequest pmr,
            CancellationToken token = default)
        {
            string modelName = null;
            if (!String.IsNullOrEmpty(pmr.Name)) modelName = pmr.Name;
            if (!String.IsNullOrEmpty(pmr.Model)) modelName = pmr.Model;

            if (String.IsNullOrEmpty(modelName))
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "no model name supplied");

                req.Http.Response.StatusCode = 400;

                return new
                {
                    error = "invalid model name"
                };
            }

            req.Http.Response.ContentType = Constants.NdJsonContentType;
            req.Http.Response.ChunkedTransfer = true;
            try
            {
                await SharpAI.Server.Classes.Runtime.RequestDiagnostics.SendChunk(req, Encoding.UTF8.GetBytes("{\"status\":\"pulling manifest\"}\n"), false, token).ConfigureAwait(false);
                Channel<string> updates = Channel.CreateBounded<string>(new BoundedChannelOptions(1)
                {
                    SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.DropOldest
                });
                async Task SendUpdatesAsync()
                {
                    await foreach (string update in updates.Reader.ReadAllAsync(token).ConfigureAwait(false))
                        await SharpAI.Server.Classes.Runtime.RequestDiagnostics.SendChunk(req, Encoding.UTF8.GetBytes(update + "\n"), false, token).ConfigureAwait(false);
                }
                Task sending = SendUpdatesAsync();
                long lastUpdate = 0;
                ModelFile model;
                try
                {
                    model = await SharpAI.Operations.ModelPullService.PullAsync(_HuggingFaceClient, _ModelFileService,
                        _Settings.Storage.ModelsDirectory, modelName, _Settings.QuantizationPriority,
                        (path, downloaded, percent) =>
                        {
                            long now = Environment.TickCount64;
                            if (now - lastUpdate < 250 && percent < 1) return;
                            lastUpdate = now;
                            updates.Writer.TryWrite(_Serializer.SerializeJson(new
                            {
                                status = percent >= 1 ? "verifying" : "downloading",
                                file = Path.GetFileName(path), downloaded, percent
                            }, false));
                        }, token).ConfigureAwait(false);
                }
                finally
                {
                    updates.Writer.TryComplete();
                    await sending.ConfigureAwait(false);
                }
                await SharpAI.Server.Classes.Runtime.RequestDiagnostics.SendChunk(req, Encoding.UTF8.GetBytes(_Serializer.SerializeJson(new { status = "success", downloaded = model.ContentLength, percent = 1 }, false) + "\n"), true, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                await SharpAI.Server.Classes.Runtime.RequestDiagnostics.SendChunk(req, Encoding.UTF8.GetBytes(_Serializer.SerializeJson(new { error = "pull failed: " + ex.Message }, false) + "\n"), true, token).ConfigureAwait(false);
            }
            return null;
        }

        internal async Task<object> ImportModel(
            ApiRequest req,
            SharpAI.Server.Classes.Requests.ImportModelRequest imr,
            CancellationToken token = default)
        {
            if (imr == null) throw new ArgumentNullException(nameof(imr));

            #region Validate-Path

            if (String.IsNullOrEmpty(imr.Path))
            {
                req.Http.Response.StatusCode = 400;
                return new { error = "A local file path is required." };
            }

            string sourcePath = imr.Path;
            if (!File.Exists(sourcePath))
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "import requested for nonexistent file " + sourcePath);
                req.Http.Response.StatusCode = 404;
                return new { error = "The specified file does not exist or is not readable by the server: " + sourcePath };
            }

            if (!IsGgufFile(sourcePath))
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "import requested for non-GGUF file " + sourcePath);
                req.Http.Response.StatusCode = 400;
                return new { error = "The specified file is not a valid GGUF model (missing GGUF magic header)." };
            }

            #endregion

            #region Resolve-Name

            string modelName = !String.IsNullOrEmpty(imr.Name)
                ? imr.Name
                : Path.GetFileNameWithoutExtension(sourcePath);

            if (String.IsNullOrEmpty(modelName))
            {
                req.Http.Response.StatusCode = 400;
                return new { error = "Unable to determine a model name; supply 'name' explicitly." };
            }

            ModelFile existing = _ModelFileService.GetByName(modelName);
            if (existing != null)
            {
                SharpAI.Logging.JsonLog.Debug(_Logging, _Header + "import: model " + modelName + " already exists");
                req.Http.Response.StatusCode = 409;
                return new { error = "A model with the name '" + modelName + "' already exists." };
            }

            #endregion

            #region Copy-Into-Store

            ModelFile modelFile = new ModelFile { Name = modelName };

            if (!Directory.Exists(_Settings.Storage.ModelsDirectory))
                Directory.CreateDirectory(_Settings.Storage.ModelsDirectory);

            string storagePath = Path.Combine(_Settings.Storage.ModelsDirectory, modelFile.GUID.ToString());
            string stagingPath = storagePath + ".import";
            string destination = stagingPath;

            try
            {
                SharpAI.Logging.JsonLog.Info(_Logging, _Header + "importing local GGUF '" + sourcePath + "' as model '" + modelName + "' (" + modelFile.GUID + ")");

                List<string> sourceFiles = GgufShards.Files(sourcePath);
                if (sourceFiles.Count > 1)
                {
                    Directory.CreateDirectory(stagingPath);
                    foreach (string file in sourceFiles)
                    {
                        token.ThrowIfCancellationRequested();
                        await Task.Run(() => File.Copy(file, Path.Combine(stagingPath, Path.GetFileName(file)), false), token).ConfigureAwait(false);
                    }
                    destination = Path.Combine(stagingPath, Path.GetFileName(sourceFiles[0]));
                }
                else await Task.Run(() => File.Copy(sourcePath, destination, false), token).ConfigureAwait(false);
                ModelInspection inspection = ModelInspector.Inspect(destination);
                if (!inspection.Supported) throw new InvalidDataException("Model contains unsupported components.");
                modelFile.ParameterCount = inspection.ParameterCount;

                #endregion

                #region Detect-And-Persist

                SharpAI.Helpers.GgufMetadataReader.DetectCapabilities(destination, out string detectedArchitecture,
                    out bool supportsEmbeddings, out bool supportsCompletions);

                long fileLength = inspection.WeightsBytes;

                using (FileStream fs = new FileStream(destination, FileMode.Open, FileAccess.Read))
                {
                    HashResults hashResults = HashHelper.ComputeAllHashes(fs);
                    modelFile.MD5Hash = Convert.ToHexString(hashResults.MD5);
                    modelFile.SHA1Hash = Convert.ToHexString(hashResults.SHA1);
                    modelFile.SHA256Hash = Convert.ToHexString(hashResults.SHA256);
                }

                if (sourceFiles.Count > 1)
                {
                    List<string> hashes = new List<string>();
                    foreach (string file in inspection.Files)
                    {
                        using FileStream shard = File.OpenRead(file);
                        hashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(shard)));
                    }
                    modelFile.MD5Hash = null;
                    modelFile.SHA1Hash = null;
                    modelFile.SHA256Hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(String.Join("\n", hashes))));
                }
                token.ThrowIfCancellationRequested();
                if (Directory.Exists(stagingPath)) Directory.Move(stagingPath, storagePath);
                else File.Move(stagingPath, storagePath);
                modelFile.ContentLength = fileLength;
                modelFile.Embeddings = supportsEmbeddings;
                modelFile.Completions = supportsCompletions;
                modelFile.SourceUrl = "file://" + sourcePath.Replace("\\", "/");
                modelFile.ModelCreationUtc = File.GetLastWriteTimeUtc(sourcePath);
                if (!String.IsNullOrEmpty(detectedArchitecture)) modelFile.Family = detectedArchitecture;

                _ModelFileService.Add(modelFile);

                SharpAI.Logging.JsonLog.Info(_Logging, _Header + "successfully imported model " + modelName +
                    " (architecture=" + (detectedArchitecture ?? "unknown") +
                    ", embeddings=" + supportsEmbeddings + ", completions=" + supportsCompletions + ")");

                req.Http.Response.ContentType = Constants.JsonContentType;
                return modelFile.ToOllamaModelDetails();

                #endregion
            }
            catch (Exception ex)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "import failed for " + modelName + ":" + Environment.NewLine + ex.ToString());

                try
                {
                    if (Directory.Exists(stagingPath)) Directory.Delete(stagingPath, true);
                    else File.Delete(stagingPath);
                    GgufShards.Delete(_Settings.Storage.ModelsDirectory, modelFile.GUID);
                }
                catch (Exception cleanupEx)
                {
                    SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "failed to delete orphaned imported file " + destination + ": " + cleanupEx.Message);
                }

                req.Http.Response.StatusCode = ex is InvalidDataException ? 400 : 500;
                return new { error = "import failed: " + ex.Message };
            }
        }

        private static bool IsGgufFile(string path)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    byte[] magic = new byte[4];
                    int read = fs.Read(magic, 0, 4);
                    if (read < 4) return false;
                    return magic[0] == (byte)'G' && magic[1] == (byte)'G' && magic[2] == (byte)'U' && magic[3] == (byte)'F';
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal async Task<object> DeleteModel(
            ApiRequest req,
            OllamaDeleteModelRequest dmr,
            CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(dmr.Model)) throw new ArgumentNullException(nameof(dmr.Model));

            req.Http.Response.ContentType = Constants.JsonContentType;

            ModelFile modelFile = _ModelFileService.GetByName(dmr.Model);
            if (modelFile == null)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "model " + dmr.Model + " not found");

                req.Http.Response.StatusCode = 404;

                return new
                {
                    error = $"model '{dmr.Model}' not found"
                };
            }
            else
            {
                _ModelFileService.Delete(modelFile.GUID);
                _ModelEngineService.UnloadModel(SharpAI.Operations.GgufShards.Resolve(_Settings.Storage.ModelsDirectory, modelFile.GUID));
                SharpAI.Operations.GgufShards.Delete(_Settings.Storage.ModelsDirectory, modelFile.GUID);
                return null;
            }
        }

        internal async Task<object> UnloadModel(
            ApiRequest req,
            OllamaUnloadModelRequest umr,
            CancellationToken token = default)
        {
            req.Http.Response.ContentType = Constants.JsonContentType;

            // If no model specified, unload all models
            if (String.IsNullOrEmpty(umr?.Model))
            {
                int count = _ModelEngineService.UnloadAllModels();
                SharpAI.Logging.JsonLog.Info(_Logging, _Header + "unloaded " + count + " model(s)");

                return new
                {
                    status = "success",
                    unloaded = count,
                    message = $"Unloaded {count} model(s) from memory"
                };
            }

            // Find the model by name
            ModelFile modelFile = _ModelFileService.GetByName(umr.Model);
            if (modelFile == null)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "model " + umr.Model + " not found");

                req.Http.Response.StatusCode = 404;

                return new
                {
                    error = $"model '{umr.Model}' not found"
                };
            }

            // Build the file path and attempt to unload
            string modelPath = SharpAI.Operations.GgufShards.Resolve(_Settings.Storage.ModelsDirectory, modelFile.GUID);
            bool unloaded = _ModelEngineService.UnloadModel(modelPath);

            if (unloaded)
            {
                SharpAI.Logging.JsonLog.Info(_Logging, _Header + "unloaded model " + umr.Model);

                return new
                {
                    status = "success",
                    model = umr.Model,
                    message = $"Model '{umr.Model}' unloaded from memory"
                };
            }
            else
            {
                SharpAI.Logging.JsonLog.Debug(_Logging, _Header + "model " + umr.Model + " was not loaded");

                return new
                {
                    status = "not_loaded",
                    model = umr.Model,
                    message = $"Model '{umr.Model}' was not loaded in memory"
                };
            }
        }

        internal async Task<object> ListLocalModels(
            ApiRequest req,
            CancellationToken token = default)
        {
            List<ModelFile> modelFiles = CollectAllModels();
            if (modelFiles == null || modelFiles.Count < 1)
            {
                SharpAI.Logging.JsonLog.Debug(_Logging, _Header + "no models downloaded");

                return new
                {
                    models = new List<string>()
                };
            }

            List<object> ret = new List<object>();

            foreach (ModelFile modelFile in modelFiles)
            {
                ret.Add(modelFile.ToOllamaModelDetails());
            }

            return new { models = ret };
        }

        private List<ModelFile> CollectAllModels()
        {
            // The Ollama /api/tags contract returns the full model list. There is no "get all" data-access
            // method; page through the enumeration to materialize the full set.
            List<ModelFile> all = new List<ModelFile>();
            EnumerationQuery query = new EnumerationQuery
            {
                PageSize = 1000,
                Order = EnumerationOrderEnum.CreatedDescending
            };

            while (true)
            {
                EnumerationResult<ModelFile> page = _ModelFileService.Enumerate(query);
                if (page.Objects != null && page.Objects.Count > 0) all.AddRange(page.Objects);
                if (page.EndOfResults) break;
                query.PageNumber = query.PageNumber + 1;
            }

            return all;
        }

        internal async Task<object> ListRunningModels(
            ApiRequest req,
            CancellationToken token = default)
        {
            req.Http.Response.ContentType = Constants.JsonContentType;

            OllamaListRunningModelsResult ret = new OllamaListRunningModelsResult();

            List<string> loadedPaths = _ModelEngineService.GetLoadedModelPaths();
            if (loadedPaths == null || loadedPaths.Count < 1)
            {
                return ret;
            }

            string selectedBackend = SharpAI.Classes.Runtime.NativeBackendInfo.SelectedBackend;
            bool isGpuBackend = !String.IsNullOrEmpty(selectedBackend)
                && (selectedBackend.Equals("cuda", StringComparison.OrdinalIgnoreCase)
                    || selectedBackend.Equals("metal", StringComparison.OrdinalIgnoreCase));

            foreach (string loadedPath in loadedPaths)
            {
                string filename = Path.GetFileName(loadedPath);
                if (String.IsNullOrEmpty(filename)) continue;

                Guid modelGuid;
                if (!Guid.TryParse(filename, out modelGuid) && !Guid.TryParse(Path.GetFileName(Path.GetDirectoryName(loadedPath)), out modelGuid))
                {
                    SharpAI.Logging.JsonLog.Debug(_Logging, _Header + "loaded engine path is not GUID-backed, skipping: " + loadedPath);
                    continue;
                }

                ModelFile modelFile = _ModelFileService.GetByGuid(modelGuid);
                if (modelFile == null)
                {
                    SharpAI.Logging.JsonLog.Debug(_Logging, _Header + "no model file record for loaded engine " + modelGuid.ToString());
                    continue;
                }

                OllamaRunningModel running = new OllamaRunningModel
                {
                    Name = modelFile.Name,
                    Digest = modelFile.SHA256Hash,
                    Size = modelFile.ContentLength,
                    SizeVRAM = isGpuBackend ? modelFile.ContentLength : 0,
                    ExpiresAt = _ModelEngineService.GetExpiryUtc(loadedPath),
                    Details = new OllamaModelDetails
                    {
                        ParentModel = modelFile.ParentModel ?? String.Empty,
                        Format = modelFile.Format,
                        Family = modelFile.Family,
                        Families = new List<string> { modelFile.Family },
                        ParameterSize = modelFile.ParameterCount.ToString(),
                        QuantizationLevel = modelFile.Quantization
                    }
                };

                ret.Models.Add(running);
            }

            return ret;
        }

        internal async Task<object> GenerateEmbeddings(
            ApiRequest req,
            OllamaGenerateEmbeddingsRequest ger,
            CancellationToken token = default)
        {
            using System.Diagnostics.Activity requestSpan = SharpAI.Telemetry.SharpAITelemetry.StartInference("embedding", ger?.Model);

            if (String.IsNullOrEmpty(ger.Model)) throw new ArgumentNullException(nameof(ger.Model));

            req.Http.Response.ContentType = Constants.JsonContentType;

            OllamaGenerateEmbeddingsResult ret = new OllamaGenerateEmbeddingsResult
            {
                Model = ger.Model
            };

            ModelFile modelFile = _ModelFileService.GetByName(ger.Model);
            if (modelFile == null)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "model " + ger.Model + " not found");

                req.Http.Response.StatusCode = 404;

                return new
                {
                    error = $"model '{ger.Model}' not found, try pulling it first"
                };
            }

            LlamaSharpEngine engine = _ModelEngineService.GetByModelFile(SharpAI.Operations.GgufShards.Resolve(_Settings.Storage.ModelsDirectory, modelFile.GUID));

            if (!engine.SupportsEmbeddings)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "model '" + ger.Model + "' does not support embeddings");

                req.Http.Response.StatusCode = 500;

                return new
                {
                    error = $"model '{ger.Model}' does not support embeddings"
                };
            }

            if (ger.IsSingleInput())
            {
                string input = ger.GetInput();

                if (!String.IsNullOrEmpty(input))
                {
                    float[][] embeddings = new float[1][];

                    ret.Embeddings = new float[1][];

                    if (!String.IsNullOrEmpty(input))
                    {
                        embeddings[0] = await engine.GenerateEmbeddingsAsync(input, token).ConfigureAwait(false);
                        ret.Embeddings = embeddings;
                    }
                }
            }
            else
            {
                string[] inputs = ger.GetInputs();

                if (inputs.Length > 0)
                {
                    foreach (string input in inputs)
                    {
                        if (String.IsNullOrEmpty(input))
                        {
                            SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "input contains null or invalid entries");

                            req.Http.Response.StatusCode = 400;

                            return new
                            {
                                error = $"invalid input type"
                            };
                        }
                    }

                    ret.Embeddings = await engine.GenerateEmbeddingsAsync(inputs.ToArray(), token).ConfigureAwait(false);
                }
            }

            return ret;
        }

        internal async Task<object> GenerateCompletion(
            ApiRequest req,
            OllamaGenerateCompletionRequest gcr,
            CancellationToken token = default)
        {
            using System.Diagnostics.Activity requestSpan = SharpAI.Telemetry.SharpAITelemetry.StartInference("completion", gcr?.Model);

            if (String.IsNullOrEmpty(gcr.Model)) throw new ArgumentNullException(nameof(gcr.Model));

            req.Http.Response.ContentType = Constants.JsonContentType;

            ModelFile modelFile = _ModelFileService.GetByName(gcr.Model);
            if (modelFile == null)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "model " + gcr.Model + " not found");

                req.Http.Response.StatusCode = 404;

                return new
                {
                    error = $"model '{gcr.Model}' not found"
                };
            }

            string enginePath = SharpAI.Operations.GgufShards.Resolve(_Settings.Storage.ModelsDirectory, modelFile.GUID);
            bool wasLoaded = _ModelEngineService.GetLoadedModelPaths().Contains(Path.GetFullPath(enginePath));
            LlamaSharpEngine engine = await _ModelEngineService.GetByModelFileAsync(enginePath, token).ConfigureAwait(false);
            InferenceStatistics statistics = new InferenceStatistics(wasLoaded ? 0 : engine.LoadDurationNanoseconds);
            SharpAI.Server.Classes.Runtime.RequestDiagnostics.Attach(req.Http, statistics);

            string json = null;

            if (!engine.SupportsGeneration)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "'" + gcr.Model + "' does not support generate");

                req.Http.Response.StatusCode = 400;

                return new
                {
                    error = $"model '{gcr.Model}' does not support generate"
                };
            }

            // Use client-provided stop sequences, or derive from model family
            ChatFormatEnum genFormat = ChatFormatHelper.ModelFamilyToChatFormat(modelFile.Family, ChatFormatEnum.Simple);
            string[] genStopSequences = gcr.Options.Stop?.ToArray();
            if (genStopSequences == null || genStopSequences.Length == 0)
                genStopSequences = ChatFormatHelper.GetDefaultStopSequences(genFormat);

            if (!String.IsNullOrEmpty(gcr.Prompt)) engine.ValidateContext(gcr.Prompt, gcr.Options.NumPredict ?? 128);

            bool genDisplayThinking = gcr.Options.DisplayThinking ?? false;

            if (gcr.Stream == null || !gcr.Stream.Value)
            {
                string response = "";

                if (!String.IsNullOrEmpty(gcr.Prompt))
                {
                    response = await engine.GenerateTextAsync(
                        gcr.Prompt,
                        gcr.Options.NumPredict != null ? gcr.Options.NumPredict.Value : 128,
                        gcr.Options.Temperature != null ? gcr.Options.Temperature.Value : 0.6f,
                        genStopSequences,
                        null, token, statistics, gcr.Options).ConfigureAwait(false);

                    if (!genDisplayThinking)
                        response = ThinkingFilter.RemoveThinkingBlocks(response);
                }

                return new
                {
                    model = gcr.Model,
                    created_at = DateTime.UtcNow.ToString(_TimestampFormat),
                    response = response,
                    done = true,
                    total_duration = statistics.TotalDurationNanoseconds,
                    first_token_duration = statistics.FirstTokenDurationNanoseconds,
                    queue_duration = statistics.QueueDurationNanoseconds,
                    phase_timings_approximate = statistics.PhaseTimingsApproximate,
                    cancelled = statistics.Cancelled,
                    load_duration = statistics.LoadDurationNanoseconds,
                    prompt_eval_count = statistics.PromptTokens,
                    prompt_eval_duration = statistics.PromptDurationNanoseconds,
                    eval_count = statistics.CompletionTokens,
                    eval_duration = statistics.GenerationDurationNanoseconds,
                    done_reason = statistics.CompletionTokens >= (gcr.Options.NumPredict ?? 128) ? "length" : "stop"
                };
            }
            else
            {
                string nextToken = "";
                ThinkingFilter genThinkFilter = genDisplayThinking ? null : new ThinkingFilter();

                req.Http.Response.ContentType = Constants.NdJsonContentType;
                req.Http.Response.ChunkedTransfer = true;

                if (!String.IsNullOrEmpty(gcr.Prompt))
                {
                    await foreach (string curr in engine.GenerateTextStreamAsync(
                        gcr.Prompt,
                        gcr.Options.NumPredict != null ? gcr.Options.NumPredict.Value : 128,
                        gcr.Options.Temperature != null ? gcr.Options.Temperature.Value : 0.6f,
                        genStopSequences,
                        null, token, statistics, gcr.Options).ConfigureAwait(false))
                    {
                        string filtered = genThinkFilter != null ? genThinkFilter.ProcessToken(curr) : curr;

                        if (nextToken != null && nextToken.Length > 0)
                        {
                            json = _Serializer.SerializeJson(new
                            {
                                model = gcr.Model,
                                created_at = DateTime.UtcNow.ToString(_TimestampFormat),
                                response = nextToken,
                                done = false

                            }, false) + Environment.NewLine;

                            await SharpAI.Server.Classes.Runtime.RequestDiagnostics.SendChunk(req, Encoding.UTF8.GetBytes(json), false, token).ConfigureAwait(false);
                        }

                        nextToken = filtered;
                    }

                    if (genThinkFilter != null)
                    {
                        string flushed = genThinkFilter.Flush();
                        if (!String.IsNullOrEmpty(flushed))
                            nextToken = (nextToken ?? "") + flushed;
                    }
                }

                json = _Serializer.SerializeJson(new
                {
                    model = gcr.Model,
                    created_at = DateTime.UtcNow.ToString(_TimestampFormat),
                    response = nextToken,
                    done = true,
                    total_duration = statistics.TotalDurationNanoseconds,
                    first_token_duration = statistics.FirstTokenDurationNanoseconds,
                    queue_duration = statistics.QueueDurationNanoseconds,
                    phase_timings_approximate = statistics.PhaseTimingsApproximate,
                    cancelled = statistics.Cancelled,
                    load_duration = statistics.LoadDurationNanoseconds,
                    prompt_eval_count = statistics.PromptTokens,
                    prompt_eval_duration = statistics.PromptDurationNanoseconds,
                    eval_count = statistics.CompletionTokens,
                    eval_duration = statistics.GenerationDurationNanoseconds,
                    done_reason = statistics.CompletionTokens >= (gcr.Options.NumPredict ?? 128) ? "length" : "stop"

                }, false);

                await SharpAI.Server.Classes.Runtime.RequestDiagnostics.SendChunk(req, Encoding.UTF8.GetBytes(json), true, token).ConfigureAwait(false);

                return null;
            }
        }

        internal async Task<object> GenerateChatCompletion(
            ApiRequest req,
            OllamaGenerateChatCompletionRequest gcr,
            CancellationToken token = default)
        {
            using System.Diagnostics.Activity requestSpan = SharpAI.Telemetry.SharpAITelemetry.StartInference("chat", gcr?.Model);

            if (String.IsNullOrEmpty(gcr.Model)) throw new ArgumentNullException(nameof(gcr.Model));
            if (gcr.Messages == null) gcr.Messages = new List<OllamaChatMessage>();

            req.Http.Response.ContentType = Constants.JsonContentType;

            // Modelfile-equivalent presets (W5.T4): resolve a preset by the requested model name and run its
            // base model with the preset's defaults applied below.
            ModelPreset preset = _Presets != null ? _Presets.GetByName(gcr.Model) : null;
            string effectiveModel = preset != null ? preset.ModelName : gcr.Model;

            ModelFile modelFile = _ModelFileService.GetByName(effectiveModel);
            if (modelFile == null)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "model " + effectiveModel + " not found");

                req.Http.Response.StatusCode = 404;

                return new
                {
                    error = $"model '{gcr.Model}' not found"
                };
            }

            string enginePath = SharpAI.Operations.GgufShards.Resolve(_Settings.Storage.ModelsDirectory, modelFile.GUID);
            bool wasLoaded = _ModelEngineService.GetLoadedModelPaths().Contains(Path.GetFullPath(enginePath));
            LlamaSharpEngine engine = await _ModelEngineService.GetByModelFileAsync(enginePath, token).ConfigureAwait(false);
            InferenceStatistics statistics = new InferenceStatistics(wasLoaded ? 0 : engine.LoadDurationNanoseconds);
            SharpAI.Server.Classes.Runtime.RequestDiagnostics.Attach(req.Http, statistics);

            if (!engine.SupportsGeneration)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "'" + effectiveModel + "' does not support generate");

                req.Http.Response.StatusCode = 400;

                return new
                {
                    error = $"model '{gcr.Model}' does not support generate"
                };
            }

            List<ChatMessage> messages = new List<ChatMessage>();
            foreach (OllamaChatMessage msg in gcr.Messages)
            {
                messages.Add(new ChatMessage
                {
                    Role = msg.Role,
                    Content = msg.Content,
                    Timestamp = DateTime.UtcNow
                });
            }

            // Apply a preset system prompt when the request carries no system message.
            if (preset != null && !String.IsNullOrEmpty(preset.SystemPrompt))
            {
                bool hasSystem = false;
                for (int i = 0; i < messages.Count; i++)
                {
                    if (String.Equals(messages[i].Role, "system", StringComparison.OrdinalIgnoreCase)) { hasSystem = true; break; }
                }
                if (!hasSystem) messages.Insert(0, new ChatMessage { Role = "system", Content = preset.SystemPrompt, Timestamp = DateTime.UtcNow });
            }

            // Tool/function calling (W4.T1): inject a tool-description system instruction when tools are
            // supplied, then parse the model output back into tool_calls below.
            List<ToolDefinition> toolDefinitions = ToolRequestMapper.FromOllama(gcr.Tools);
            bool toolsRequested = toolDefinitions.Count > 0;
            if (toolsRequested)
            {
                string toolInstruction = ToolPromptBuilder.BuildSystemInstruction(toolDefinitions);
                if (!String.IsNullOrEmpty(toolInstruction))
                {
                    bool merged = false;
                    for (int i = 0; i < messages.Count; i++)
                    {
                        if (String.Equals(messages[i].Role, "system", StringComparison.OrdinalIgnoreCase))
                        {
                            messages[i].Content = (messages[i].Content ?? String.Empty).TrimEnd() + "\n\n" + toolInstruction;
                            merged = true;
                            break;
                        }
                    }
                    if (!merged) messages.Insert(0, new ChatMessage { Role = "system", Content = toolInstruction, Timestamp = DateTime.UtcNow });
                }
            }

            // Prefer the model's embedded GGUF chat template; fall back to the family template.
            ChatTemplateResult templateResult = ChatTemplateResolver.Resolve(engine, modelFile.Family, messages);
            string prompt = templateResult.Prompt;

            // Use client-provided stop sequences, or those the resolver derived (empty for embedded
            // templates, which rely on the model's native end-of-turn tokens).
            string[] stopSequences = gcr.Options.Stop?.ToArray();
            if (stopSequences == null || stopSequences.Length == 0)
                stopSequences = templateResult.StopSequences;

            // Preset param fallbacks (W5.T4): request options win; otherwise the preset supplies defaults.
            if (preset != null)
            {
                if (gcr.Options.NumPredict == null && preset.MaxTokens.HasValue) gcr.Options.NumPredict = preset.MaxTokens.Value;
                if (gcr.Options.Temperature == null && preset.Temperature.HasValue) gcr.Options.Temperature = preset.Temperature.Value;
                if (preset.Stop != null && preset.Stop.Count > 0)
                {
                    List<string> mergedStop = new List<string>(stopSequences ?? Array.Empty<string>());
                    foreach (string s in preset.Stop)
                        if (!String.IsNullOrEmpty(s) && !mergedStop.Contains(s)) mergedStop.Add(s);
                    stopSequences = mergedStop.ToArray();
                }
            }

            if (!String.IsNullOrEmpty(prompt)) engine.ValidateContext(prompt, gcr.Options.NumPredict ?? 128);

            bool displayThinking = gcr.Options.DisplayThinking ?? false;

            // JSON mode (W4.T4): honor the Ollama `format` field via a JSON GBNF grammar, unless tools are in
            // play (tool output is not plain JSON).
            string jsonGrammar = toolsRequested ? null : JsonGrammar.ForOllamaFormat(gcr.Format);

            if (gcr.Stream == null || !gcr.Stream.Value)
            {
                string response = "";

                if (!String.IsNullOrEmpty(prompt))
                {
                    response = await engine.GenerateChatCompletionAsync(
                        prompt,
                        gcr.Options.NumPredict != null ? gcr.Options.NumPredict.Value : 128,
                        gcr.Options.Temperature != null ? gcr.Options.Temperature.Value : 0.6f,
                        stopSequences,
                        jsonGrammar,
                        token, statistics, gcr.Options).ConfigureAwait(false);

                    if (!displayThinking)
                        response = ThinkingFilter.RemoveThinkingBlocks(response);
                }

                if (toolsRequested)
                {
                    List<ParsedToolCall> parsedCalls = ToolCallParser.Parse(response);
                    if (parsedCalls.Count > 0)
                    {
                        return new
                        {
                            model = gcr.Model,
                            created_at = DateTime.UtcNow.ToString(_TimestampFormat),
                            message = new
                            {
                                role = "assistant",
                                content = "",
                                tool_calls = ToolResponseMapper.ToOllama(parsedCalls)
                            },
                            done = true,
                    total_duration = statistics.TotalDurationNanoseconds,
                    first_token_duration = statistics.FirstTokenDurationNanoseconds,
                    queue_duration = statistics.QueueDurationNanoseconds,
                    phase_timings_approximate = statistics.PhaseTimingsApproximate,
                    cancelled = statistics.Cancelled,
                    load_duration = statistics.LoadDurationNanoseconds,
                    prompt_eval_count = statistics.PromptTokens,
                    prompt_eval_duration = statistics.PromptDurationNanoseconds,
                    eval_count = statistics.CompletionTokens,
                    eval_duration = statistics.GenerationDurationNanoseconds,
                            done_reason = "tool_calls"
                        };
                    }
                }

                return new
                {
                    model = gcr.Model,
                    created_at = DateTime.UtcNow.ToString(_TimestampFormat),
                    message = new { role = "assistant", content = response },
                    done = true,
                    total_duration = statistics.TotalDurationNanoseconds,
                    first_token_duration = statistics.FirstTokenDurationNanoseconds,
                    queue_duration = statistics.QueueDurationNanoseconds,
                    phase_timings_approximate = statistics.PhaseTimingsApproximate,
                    cancelled = statistics.Cancelled,
                    load_duration = statistics.LoadDurationNanoseconds,
                    prompt_eval_count = statistics.PromptTokens,
                    prompt_eval_duration = statistics.PromptDurationNanoseconds,
                    eval_count = statistics.CompletionTokens,
                    eval_duration = statistics.GenerationDurationNanoseconds,
                    done_reason = statistics.CompletionTokens >= (gcr.Options.NumPredict ?? 128) ? "length" : "stop"
                };
            }
            else
            {
                string nextToken = "";
                string json = null;
                ThinkingFilter thinkFilter = displayThinking ? null : new ThinkingFilter();

                req.Http.Response.ContentType = Constants.NdJsonContentType;
                req.Http.Response.ChunkedTransfer = true;

                if (toolsRequested && !String.IsNullOrEmpty(prompt))
                {
                    // With tools, buffer the full completion, parse it, and emit a single terminal NDJSON
                    // line carrying either message.tool_calls (done_reason: tool_calls) or the plain response.
                    StringBuilder buffered = new StringBuilder();
                    await foreach (string curr in engine.GenerateChatCompletionStreamAsync(
                        prompt,
                        gcr.Options.NumPredict != null ? gcr.Options.NumPredict.Value : 128,
                        gcr.Options.Temperature != null ? gcr.Options.Temperature.Value : 0.6f,
                        stopSequences,
                        null, token, statistics, gcr.Options).ConfigureAwait(false))
                    {
                        buffered.Append(curr);
                    }

                    string bufferedText = displayThinking ? buffered.ToString() : ThinkingFilter.RemoveThinkingBlocks(buffered.ToString());
                    List<ParsedToolCall> streamedCalls = ToolCallParser.Parse(bufferedText);

                    object terminal = streamedCalls.Count > 0
                        ? (object)new
                        {
                            model = gcr.Model,
                            created_at = DateTime.UtcNow.ToString(_TimestampFormat),
                            message = new { role = "assistant", content = "", tool_calls = ToolResponseMapper.ToOllama(streamedCalls) },
                            done = true,
                    total_duration = statistics.TotalDurationNanoseconds,
                    first_token_duration = statistics.FirstTokenDurationNanoseconds,
                    queue_duration = statistics.QueueDurationNanoseconds,
                    phase_timings_approximate = statistics.PhaseTimingsApproximate,
                    cancelled = statistics.Cancelled,
                    load_duration = statistics.LoadDurationNanoseconds,
                    prompt_eval_count = statistics.PromptTokens,
                    prompt_eval_duration = statistics.PromptDurationNanoseconds,
                    eval_count = statistics.CompletionTokens,
                    eval_duration = statistics.GenerationDurationNanoseconds,
                            done_reason = "tool_calls"
                        }
                        : new
                        {
                            model = gcr.Model,
                            created_at = DateTime.UtcNow.ToString(_TimestampFormat),
                            message = new { role = "assistant", content = bufferedText.Trim() },
                            done = true,
                    total_duration = statistics.TotalDurationNanoseconds,
                    first_token_duration = statistics.FirstTokenDurationNanoseconds,
                    queue_duration = statistics.QueueDurationNanoseconds,
                    phase_timings_approximate = statistics.PhaseTimingsApproximate,
                    cancelled = statistics.Cancelled,
                    load_duration = statistics.LoadDurationNanoseconds,
                    prompt_eval_count = statistics.PromptTokens,
                    prompt_eval_duration = statistics.PromptDurationNanoseconds,
                    eval_count = statistics.CompletionTokens,
                    eval_duration = statistics.GenerationDurationNanoseconds,
                            done_reason = statistics.CompletionTokens >= (gcr.Options.NumPredict ?? 128) ? "length" : "stop"
                        };

                    string terminalJson = _Serializer.SerializeJson(terminal, false) + Environment.NewLine;
                    await SharpAI.Server.Classes.Runtime.RequestDiagnostics.SendChunk(req, Encoding.UTF8.GetBytes(terminalJson), true, token).ConfigureAwait(false);
                    return null;
                }

                if (!String.IsNullOrEmpty(prompt))
                {
                    await foreach (string curr in engine.GenerateChatCompletionStreamAsync(
                        prompt,
                        gcr.Options.NumPredict != null ? gcr.Options.NumPredict.Value : 128,
                        gcr.Options.Temperature != null ? gcr.Options.Temperature.Value : 0.6f,
                        stopSequences,
                        jsonGrammar,
                        token, statistics, gcr.Options).ConfigureAwait(false))
                    {
                        string filtered = thinkFilter != null ? thinkFilter.ProcessToken(curr) : curr;

                        if (nextToken != null && nextToken.Length > 0)
                        {
                            json = _Serializer.SerializeJson(new
                            {
                                model = gcr.Model,
                                created_at = DateTime.UtcNow.ToString(_TimestampFormat),
                                message = new { role = "assistant", content = nextToken },
                                done = false

                            }, false) + Environment.NewLine;

                            await SharpAI.Server.Classes.Runtime.RequestDiagnostics.SendChunk(req, Encoding.UTF8.GetBytes(json), false, token).ConfigureAwait(false);
                        }

                        nextToken = filtered;
                    }

                    // Flush any remaining buffered content from the thinking filter
                    if (thinkFilter != null)
                    {
                        string flushed = thinkFilter.Flush();
                        if (!String.IsNullOrEmpty(flushed))
                            nextToken = (nextToken ?? "") + flushed;
                    }
                }

                json = _Serializer.SerializeJson(new
                {
                    model = gcr.Model,
                    created_at = DateTime.UtcNow.ToString(_TimestampFormat),
                    message = new { role = "assistant", content = nextToken },
                    done = true,
                    total_duration = statistics.TotalDurationNanoseconds,
                    first_token_duration = statistics.FirstTokenDurationNanoseconds,
                    queue_duration = statistics.QueueDurationNanoseconds,
                    phase_timings_approximate = statistics.PhaseTimingsApproximate,
                    cancelled = statistics.Cancelled,
                    load_duration = statistics.LoadDurationNanoseconds,
                    prompt_eval_count = statistics.PromptTokens,
                    prompt_eval_duration = statistics.PromptDurationNanoseconds,
                    eval_count = statistics.CompletionTokens,
                    eval_duration = statistics.GenerationDurationNanoseconds,
                    done_reason = statistics.CompletionTokens >= (gcr.Options.NumPredict ?? 128) ? "length" : "stop"

                }, false) + Environment.NewLine;

                await SharpAI.Server.Classes.Runtime.RequestDiagnostics.SendChunk(req, Encoding.UTF8.GetBytes(json), true, token).ConfigureAwait(false);

                return null;
            }
        }

        #endregion

        #region Private-Methods

        #endregion

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}
