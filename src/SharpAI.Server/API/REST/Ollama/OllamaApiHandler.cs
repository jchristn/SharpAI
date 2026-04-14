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

    using SharpAI.Engines;
    using SharpAI.Helpers;
    using SharpAI.Hosting;
    using SharpAI.Hosting.HuggingFace;
    using SharpAI.Models;
    using SharpAI.Models.Ollama;
    using SharpAI.Prompts;
    using SharpAI.Serialization;
    using SharpAI.Server.Classes.Settings;
    using SharpAI.Services;
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

        private static string _TimestampFormat = "yyyy-MM-ddTHH:mm:ss.ffffffZ";

        private ConcurrentDictionary<string, bool> _Pulls = new ConcurrentDictionary<string, bool>(StringComparer.InvariantCultureIgnoreCase);

        #endregion

        #region Constructors-and-Factories

        internal OllamaApiHandler(
            Settings settings,
            LoggingModule logging,
            Serializer serializer,
            ModelFileService modelFileService,
            ModelEngineService modelEngineService,
            HuggingFaceClient huggingFaceClient)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _ModelFileService = modelFileService ?? throw new ArgumentNullException(nameof(modelFileService));
            _ModelEngineService = modelEngineService ?? throw new ArgumentNullException(nameof(modelEngineService));
            _HuggingFaceClient = huggingFaceClient ?? throw new ArgumentNullException(nameof(huggingFaceClient));

            _Logging.Debug(_Header + "initialized");
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
                _Logging.Warn(_Header + "no model name supplied");

                req.Http.Response.StatusCode = 400;

                return new
                {
                    error = "invalid model name"
                };
            }

            #region Hold-Concurrent-Pulls

            int heldCount = 0;
            while (_Pulls.ContainsKey(modelName))
            {
                if (heldCount % 10 == 0)
                    _Logging.Debug(_Header + "holding pull request for " + modelName + " due to an existing pull");

                heldCount++;
                await Task.Delay(1000, token).ConfigureAwait(false);
            }

            _Pulls.TryAdd(modelName, true);

            #endregion

            try
            {
                #region Check-for-Existing

                ModelFile existing = _ModelFileService.GetByName(modelName);
                if (existing != null)
                {
                    _Logging.Debug(_Header + "model " + modelName + " already exists");

                    req.Http.Response.ContentType = Constants.JsonContentType;

                    return new OllamaPullModelResultMessage
                    {
                        Status = "success"
                    };
                }

                #endregion

                #region Identify-GGUF-Files

                req.Http.Response.ContentType = Constants.NdJsonContentType;
                req.Http.Response.ChunkedTransfer = true;

                List<GgufFileInfo> ggufFiles = await _HuggingFaceClient.GetGgufFilesAsync(modelName, token).ConfigureAwait(false);
                if (ggufFiles == null || ggufFiles.Count < 1)
                {
                    _Logging.Warn(_Header + "no GGUF files found for model " + modelName);

                    string notFound = _Serializer.SerializeJson(new
                    {
                        error = "pull model manifest: file does not exist"
                    }, false) + Environment.NewLine;

                    req.Http.Response.StatusCode = 404;
                    await req.Http.Response.SendChunk(Encoding.UTF8.GetBytes(notFound), true, token).ConfigureAwait(false);
                    return null;
                }

                string pullingManifest = _Serializer.SerializeJson(new
                {
                    status = "pulling manifest"
                }, false) + Environment.NewLine;

                await req.Http.Response.SendChunk(Encoding.UTF8.GetBytes(pullingManifest), false, token).ConfigureAwait(false);

                GgufFileInfo preferred = null;

                if (_Settings.QuantizationPriority == null || _Settings.QuantizationPriority.Count < 1)
                    preferred = GgufSelector.SortByOllamaPreference(ggufFiles).First();
                else
                    preferred = GgufSelector.SortByPreference(ggufFiles, _Settings.QuantizationPriority).First();

                _Logging.Debug(_Header + "using GGUF file " + preferred.Path + " as the preferred file for model " + modelName);

                #endregion

                #region Get-Download-URLs

                List<string> urls = _HuggingFaceClient.GetDownloadUrls(modelName, preferred);
                if (urls == null || urls.Count < 1)
                {
                    _Logging.Warn("no download URLs found for model " + modelName);
                    throw new WebserverException(ApiResultEnum.InternalError, "No download URLs found for the specified model " + modelName + ".");
                }

                string msg = _Header + "attempting download of model " + modelName + " from the following URLs:";
                foreach (string url in urls)
                {
                    msg += Environment.NewLine + "| " + url;
                }

                _Logging.Debug(_Header + msg);

                #endregion

                #region Download

                ModelFile modelFile = new ModelFile
                {
                    Name = modelName
                };

                bool success = false;
                string filename = null;
                string successUrl = null;

                long totalSize = preferred.Size != null ? preferred.Size.Value : 0;

                Action<string, long, decimal> progressCallback = async (filename, bytesDownloaded, percentComplete) =>
                {
                    if (percentComplete > 0 && percentComplete < 1)
                    {
                        string complete = percentComplete.ToString("F3");

                        string progress = _Serializer.SerializeJson(new
                        {
                            status = "pulling " + modelName,
                            downloaded = bytesDownloaded,
                            completed = bytesDownloaded,
                            total = totalSize,
                            percent = Convert.ToDecimal(complete)
                        }, false) + Environment.NewLine;

                        await req.Http.Response.SendChunk(Encoding.UTF8.GetBytes(progress), false, token).ConfigureAwait(false);
                    }
                };

                long fileLength = 0;

                filename = Path.Combine(_Settings.Storage.ModelsDirectory, modelFile.GUID.ToString());

                foreach (string url in urls)
                {
                    _Logging.Debug(_Header + "attempting download of model " + modelName + " using URL " + url + " to file " + modelFile.GUID.ToString());

                    success = await _HuggingFaceClient.TryDownloadFileAsync(url, filename, progressCallback, token).ConfigureAwait(false);
                    if (success)
                    {
                        fileLength = new FileInfo(filename).Length;
                        if (File.Exists(filename) && new FileInfo(filename).Length == preferred.Size)
                        {
                            _Logging.Info(_Header + "successfully downloaded model " + modelName + " using URL " + url + " to file " + filename);
                            successUrl = url;
                            success = true;
                            break;
                        }
                        else
                        {
                            success = false;
                        }
                    }
                }

                if (!success || String.IsNullOrEmpty(filename))
                {
                    _Logging.Warn(_Header + "unable to download model " + modelName + " using " + urls.Count + " URL(s)");
                    throw new WebserverException(ApiResultEnum.InternalError, "Unable to download model " + modelName + " using " + urls.Count + " URL(s).");
                }

                _Logging.Info(_Header + "downloaded GGUF file for " + modelName);

                #endregion

                #region Persist

                bool supportsEmbeddings = false;
                bool supportsCompletions = true;
                string detectedArchitecture = null;
                bool capabilitiesDetected = false;

                // Try lightweight GGUF header reader first (no native dependencies, milliseconds)
                try
                {
                    SharpAI.Helpers.GgufMetadataReader.DetectCapabilities(
                        filename,
                        out detectedArchitecture,
                        out supportsEmbeddings,
                        out supportsCompletions);

                    capabilitiesDetected = true;

                    _Logging.Debug(_Header + "detected capabilities for " + modelName +
                        " via GGUF metadata: architecture=" + (detectedArchitecture ?? "unknown") +
                        ", embeddings=" + supportsEmbeddings +
                        ", completions=" + supportsCompletions);
                }
                catch (Exception metaEx)
                {
                    _Logging.Warn(_Header + "lightweight GGUF metadata read failed for " + modelName +
                        ", falling back to full model load:" + Environment.NewLine + metaEx.ToString());
                }

                // Fall back to full engine initialization if lightweight reader failed
                if (!capabilitiesDetected)
                {
                    try
                    {
                        using (LlamaSharpEngine engine = _ModelEngineService.GetByModelFile(Path.Combine(_Settings.Storage.ModelsDirectory, modelFile.GUID.ToString())))
                        {
                            supportsEmbeddings = engine.SupportsEmbeddings;
                            supportsCompletions = engine.SupportsGeneration;
                            detectedArchitecture = engine.Architecture;
                            _Logging.Debug(_Header + "detected capabilities for " + modelName +
                                " via full model load: architecture=" + (detectedArchitecture ?? "unknown") +
                                ", embeddings=" + supportsEmbeddings +
                                ", completions=" + supportsCompletions);
                        }
                    }
                    catch (Exception capEx)
                    {
                        _Logging.Warn(_Header + "capability detection failed for " + modelName +
                            ", cleaning up downloaded file:" + Environment.NewLine + capEx.ToString());

                        try
                        {
                            if (File.Exists(filename))
                                File.Delete(filename);
                        }
                        catch (Exception cleanupEx)
                        {
                            _Logging.Warn(_Header + "failed to delete orphaned model file " + filename + ": " + cleanupEx.ToString());
                        }

                        throw;
                    }
                }

                using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
                {
                    (byte[] md5, byte[] sha1, byte[] sha256) = HashHelper.ComputeAllHashes(fs);

                    string writingManifest = _Serializer.SerializeJson(new
                    {
                        status = "writing manifest",
                        downloaded = fileLength,
                        percent = 1
                    }, false) + Environment.NewLine;

                    await req.Http.Response.SendChunk(Encoding.UTF8.GetBytes(writingManifest), false, token).ConfigureAwait(false);

                    HuggingFaceModelMetadata md = null;

                    try
                    {
                        md = await _HuggingFaceClient.GetModelMetadata(modelName, token).ConfigureAwait(false);
                        if (md == null)
                        {
                            _Logging.Warn(_Header + "unable to retrieve metadata for " + modelName);
                            throw new WebserverException(ApiResultEnum.InternalError, "Unable to retrieve metadata for model '" + modelName + "'.");
                        }
                    }
                    catch (Exception e)
                    {
                        _Logging.Warn(_Header + "exception retrieving model metadata:" + Environment.NewLine + e.ToString());
                    }

                    long parameterCount = 0;
                    if (md != null && md.SafeTensors != null) parameterCount = md.SafeTensors.Total;

                    modelFile.ContentLength = preferred.Size != null ? preferred.Size.Value : 0;
                    modelFile.MD5Hash = Convert.ToHexString(md5);
                    modelFile.SHA1Hash = Convert.ToHexString(sha1);
                    modelFile.SHA256Hash = Convert.ToHexString(sha256);
                    modelFile.Quantization = preferred.QuantizationType;
                    modelFile.ParameterCount = parameterCount;
                    modelFile.ModelCreationUtc = preferred.LastModified;
                    modelFile.SourceUrl = successUrl;
                    modelFile.Embeddings = supportsEmbeddings;
                    modelFile.Completions = supportsCompletions;
                    if (!String.IsNullOrEmpty(detectedArchitecture))
                        modelFile.Family = detectedArchitecture;

                    _ModelFileService.Add(modelFile);

                    req.Http.Response.ContentType = Constants.JsonContentType;

                    string complete = _Serializer.SerializeJson(new
                    {
                        status = "success",
                        downloaded = fileLength,
                        percent = 1
                    }, false) + Environment.NewLine;

                    _Logging.Info(_Header + "successfully pulled model " + modelName);

                    await req.Http.Response.SendChunk(Encoding.UTF8.GetBytes(complete), true, token).ConfigureAwait(false);

                    return null;
                }

                #endregion
            }
            catch (KeyNotFoundException)
            {
                _Logging.Warn(_Header + "unable to find repository or GGUF files for " + modelName);

                string notFound = _Serializer.SerializeJson(new
                {
                    error = "pull model manifest: file does not exist"
                }, false) + Environment.NewLine;

                await req.Http.Response.SendChunk(Encoding.UTF8.GetBytes(notFound), true, token).ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "pull failed for " + modelName + ":" + Environment.NewLine + ex.ToString());

                string errorMsg = _Serializer.SerializeJson(new
                {
                    error = "pull failed: " + ex.Message
                }, false) + Environment.NewLine;

                try
                {
                    await req.Http.Response.SendChunk(Encoding.UTF8.GetBytes(errorMsg), true, token).ConfigureAwait(false);
                }
                catch (Exception chunkEx)
                {
                    _Logging.Warn(_Header + "failed to send error chunk for " + modelName + ":" + Environment.NewLine + chunkEx.ToString());
                }

                return null;
            }
            finally
            {
                if (!String.IsNullOrEmpty(modelName))
                    _Pulls.TryRemove(modelName, out _);
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
                _Logging.Warn(_Header + "model " + dmr.Model + " not found");

                req.Http.Response.StatusCode = 404;

                return new
                {
                    error = $"model '{dmr.Model}' not found"
                };
            }
            else
            {
                _ModelFileService.Delete(modelFile.GUID);
                File.Delete(Path.Combine(_Settings.Storage.ModelsDirectory, modelFile.GUID.ToString()));
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
                _Logging.Info(_Header + "unloaded " + count + " model(s)");

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
                _Logging.Warn(_Header + "model " + umr.Model + " not found");

                req.Http.Response.StatusCode = 404;

                return new
                {
                    error = $"model '{umr.Model}' not found"
                };
            }

            // Build the file path and attempt to unload
            string modelPath = Path.Combine(_Settings.Storage.ModelsDirectory, modelFile.GUID.ToString());
            bool unloaded = _ModelEngineService.UnloadModel(modelPath);

            if (unloaded)
            {
                _Logging.Info(_Header + "unloaded model " + umr.Model);

                return new
                {
                    status = "success",
                    model = umr.Model,
                    message = $"Model '{umr.Model}' unloaded from memory"
                };
            }
            else
            {
                _Logging.Debug(_Header + "model " + umr.Model + " was not loaded");

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
            List<ModelFile> modelFiles = _ModelFileService.All();
            if (modelFiles == null || modelFiles.Count < 1)
            {
                _Logging.Debug(_Header + "no models downloaded");

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

            return ret;
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
                if (!Guid.TryParse(filename, out modelGuid))
                {
                    _Logging.Debug(_Header + "loaded engine path is not GUID-backed, skipping: " + loadedPath);
                    continue;
                }

                ModelFile modelFile = _ModelFileService.GetByGuid(modelGuid);
                if (modelFile == null)
                {
                    _Logging.Debug(_Header + "no model file record for loaded engine " + modelGuid.ToString());
                    continue;
                }

                OllamaRunningModel running = new OllamaRunningModel
                {
                    Name = modelFile.Name,
                    Digest = modelFile.SHA256Hash,
                    Size = modelFile.ContentLength,
                    SizeVRAM = isGpuBackend ? modelFile.ContentLength : 0,
                    ExpiresAt = null,
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
            if (String.IsNullOrEmpty(ger.Model)) throw new ArgumentNullException(nameof(ger.Model));

            req.Http.Response.ContentType = Constants.JsonContentType;

            OllamaGenerateEmbeddingsResult ret = new OllamaGenerateEmbeddingsResult
            {
                Model = ger.Model
            };

            ModelFile modelFile = _ModelFileService.GetByName(ger.Model);
            if (modelFile == null)
            {
                _Logging.Warn(_Header + "model " + ger.Model + " not found");

                req.Http.Response.StatusCode = 404;

                return new
                {
                    error = $"model '{ger.Model}' not found, try pulling it first"
                };
            }

            LlamaSharpEngine engine = _ModelEngineService.GetByModelFile(Path.Combine(_Settings.Storage.ModelsDirectory, modelFile.GUID.ToString()));

            if (!engine.SupportsEmbeddings)
            {
                _Logging.Warn(_Header + "model '" + ger.Model + "' does not support embeddings");

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
                            _Logging.Warn(_Header + "input contains null or invalid entries");

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
            if (String.IsNullOrEmpty(gcr.Model)) throw new ArgumentNullException(nameof(gcr.Model));

            req.Http.Response.ContentType = Constants.JsonContentType;

            ModelFile modelFile = _ModelFileService.GetByName(gcr.Model);
            if (modelFile == null)
            {
                _Logging.Warn(_Header + "model " + gcr.Model + " not found");

                req.Http.Response.StatusCode = 404;

                return new
                {
                    error = $"model '{gcr.Model}' not found"
                };
            }

            LlamaSharpEngine engine = _ModelEngineService.GetByModelFile(Path.Combine(_Settings.Storage.ModelsDirectory, modelFile.GUID.ToString()));

            string json = null;

            if (!engine.SupportsGeneration)
            {
                _Logging.Warn(_Header + "'" + gcr.Model + "' does not support generate");

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
                        token).ConfigureAwait(false);

                    if (!genDisplayThinking)
                        response = ThinkingFilter.RemoveThinkingBlocks(response);
                }

                return new
                {
                    model = gcr.Model,
                    created_at = DateTime.UtcNow.ToString(_TimestampFormat),
                    response = response,
                    done = true,
                    done_reason = "stop"
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
                        token).ConfigureAwait(false))
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

                            await req.Http.Response.SendChunk(Encoding.UTF8.GetBytes(json), false, token).ConfigureAwait(false);
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
                    done_reason = "stop"

                }, false);

                await req.Http.Response.SendChunk(Encoding.UTF8.GetBytes(json), true, token).ConfigureAwait(false);

                return null;
            }
        }

        internal async Task<object> GenerateChatCompletion(
            ApiRequest req,
            OllamaGenerateChatCompletionRequest gcr,
            CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(gcr.Model)) throw new ArgumentNullException(nameof(gcr.Model));
            if (gcr.Messages == null) gcr.Messages = new List<OllamaChatMessage>();

            req.Http.Response.ContentType = Constants.JsonContentType;

            ModelFile modelFile = _ModelFileService.GetByName(gcr.Model);
            if (modelFile == null)
            {
                _Logging.Warn(_Header + "model " + gcr.Model + " not found");

                req.Http.Response.StatusCode = 404;

                return new
                {
                    error = $"model '{gcr.Model}' not found"
                };
            }

            LlamaSharpEngine engine = _ModelEngineService.GetByModelFile(Path.Combine(_Settings.Storage.ModelsDirectory, modelFile.GUID.ToString()));

            if (!engine.SupportsGeneration)
            {
                _Logging.Warn(_Header + "'" + gcr.Model + "' does not support generate");

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

            ChatFormatEnum chatFormat = ChatFormatHelper.ModelFamilyToChatFormat(modelFile.Family, ChatFormatEnum.Simple);

            string prompt = ChatPromptBuilder.Build(chatFormat, messages);

            // Use client-provided stop sequences, or derive from chat format
            string[] stopSequences = gcr.Options.Stop?.ToArray();
            if (stopSequences == null || stopSequences.Length == 0)
                stopSequences = ChatFormatHelper.GetDefaultStopSequences(chatFormat);

            bool displayThinking = gcr.Options.DisplayThinking ?? false;

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
                        token).ConfigureAwait(false);

                    if (!displayThinking)
                        response = ThinkingFilter.RemoveThinkingBlocks(response);
                }

                return new
                {
                    model = gcr.Model,
                    created_at = DateTime.UtcNow.ToString(_TimestampFormat),
                    response = response,
                    done = true,
                    done_reason = "stop"
                };
            }
            else
            {
                string nextToken = "";
                string json = null;
                ThinkingFilter thinkFilter = displayThinking ? null : new ThinkingFilter();

                req.Http.Response.ContentType = Constants.NdJsonContentType;
                req.Http.Response.ChunkedTransfer = true;

                if (!String.IsNullOrEmpty(prompt))
                {
                    await foreach (string curr in engine.GenerateChatCompletionStreamAsync(
                        prompt,
                        gcr.Options.NumPredict != null ? gcr.Options.NumPredict.Value : 128,
                        gcr.Options.Temperature != null ? gcr.Options.Temperature.Value : 0.6f,
                        stopSequences,
                        token).ConfigureAwait(false))
                    {
                        string filtered = thinkFilter != null ? thinkFilter.ProcessToken(curr) : curr;

                        if (nextToken != null && nextToken.Length > 0)
                        {
                            json = _Serializer.SerializeJson(new
                            {
                                model = gcr.Model,
                                created_at = DateTime.UtcNow.ToString(_TimestampFormat),
                                response = nextToken,
                                done = false

                            }, false) + Environment.NewLine;

                            await req.Http.Response.SendChunk(Encoding.UTF8.GetBytes(json), false, token).ConfigureAwait(false);
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
                    response = nextToken,
                    done = true,
                    done_reason = "stop"

                }, false) + Environment.NewLine;

                await req.Http.Response.SendChunk(Encoding.UTF8.GetBytes(json), true, token).ConfigureAwait(false);

                return null;
            }
        }

        #endregion

        #region Private-Methods

        #endregion

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}
