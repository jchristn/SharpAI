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

        private readonly SharpAI.Http.OllamaInferenceHandler _Inference;
        private string _Header = "[OllamaApiHandler] ";
        private Settings _Settings = null;
        private LoggingModule _Logging = null;
        private Serializer _Serializer = null;
        private ModelFileService _ModelFileService = null;
        private ModelEngineService _ModelEngineService = null;
        private HuggingFaceClient _HuggingFaceClient = null;
        private SharpAI.Database.Interfaces.IModelPresetMethods _Presets = null;



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
            _Inference = new SharpAI.Http.OllamaInferenceHandler(new SharpAI.Server.Classes.Runtime.ManagedInferenceModels(settings, modelFileService, modelEngineService, presets), new SharpAI.Server.Classes.Runtime.CapturedInferenceTransport(), logging, serializer);

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

        internal Task<object> GenerateEmbeddings(ApiRequest request, OllamaGenerateEmbeddingsRequest body, CancellationToken token = default)
        { return _Inference.GenerateEmbeddings(request, body, token); }
        internal Task<object> GenerateCompletion(ApiRequest request, OllamaGenerateCompletionRequest body, CancellationToken token = default)
        { return _Inference.GenerateCompletion(request, body, token); }
        internal Task<object> GenerateChatCompletion(ApiRequest request, OllamaGenerateChatCompletionRequest body, CancellationToken token = default)
        { return _Inference.GenerateChatCompletion(request, body, token); }

        #endregion
#pragma warning restore CS1998
    }
}
