namespace SharpAI.Server.API.REST.OpenAI
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Engines;
    using SharpAI.Grammars;
    using SharpAI.Hosting;
    using SharpAI.Models;
    using SharpAI.Models.OpenAI;
    using SharpAI.Operations;
    using SharpAI.Prompts;
    using SharpAI.Serialization;
    using SharpAI.Server.Classes.Settings;
    using SharpAI.Services;
    using SharpAI.Telemetry;
    using SharpAI.Tools;
    using SyslogLogging;
    using WatsonWebserver.Core;
    using Constants = SharpAI.Constants;

    internal class OpenAIApiHandler
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        #region Public-Members

        #endregion

        #region Private-Members

        private string _Header = "[OpenAIApiHandler] ";
        private Settings _Settings = null;
        private LoggingModule _Logging = null;
        private Serializer _Serializer = null;
        private ModelFileService _ModelFileService = null;
        private ModelEngineService _ModelEngineService = null;
        private HuggingFaceClient _HuggingFaceClient = null;
        private SharpAI.Database.Interfaces.IModelPresetMethods _Presets = null;

        #endregion

        #region Constructors-and-Factories

        internal OpenAIApiHandler(
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

        internal async Task<object> GenerateEmbeddings(
            ApiRequest req,
            OpenAIGenerateEmbeddingsRequest ger,
            CancellationToken token = default)
        {
            using Activity requestSpan = SharpAITelemetry.StartInference("embedding", ger?.Model);

            if (String.IsNullOrEmpty(ger.Model))
            {
                req.Http.Response.StatusCode = 400;

                return new OpenAIError
                {
                    Error = new OpenAIErrorDetails
                    {
                        Message = "you must provide a model parameter",
                        Type = "invalid_request_error",
                        Parameters = null,
                        Code = null
                    }
                };
            }

            if (ger.Input == null)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "no input supplied in embeddings request");

                req.Http.Response.StatusCode = 400;

                return new OpenAIError
                {
                    Error = new OpenAIErrorDetails
                    {
                        Message = "'input' is a required property",
                        Type = "invalid_request_error",
                        Parameters = null,
                        Code = null
                    }
                };
            }

            req.Http.Response.ContentType = Constants.JsonContentType;

            OpenAIGenerateEmbeddingsResult ret = new OpenAIGenerateEmbeddingsResult
            {
                Model = ger.Model,
                Object = "list",
                Data = new List<OpenAIEmbedding>()
            };

            ModelFile modelFile = _ModelFileService.GetByName(ger.Model);
            if (modelFile == null)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "model " + ger.Model + " not found");

                req.Http.Response.StatusCode = 404;

                return new OpenAIError
                {
                    Error = new OpenAIErrorDetails
                    {
                        Message = "The model `" + ger.Model + "` does not exist or you do not have access to it.",
                        Type = "invalid_request_error",
                        Parameters = null,
                        Code = "model_not_found"
                    }
                };
            }

            LlamaSharpEngine engine = _ModelEngineService.GetByModelFile(SharpAI.Operations.GgufShards.Resolve(_Settings.Storage.ModelsDirectory, modelFile.GUID));

            if (!engine.SupportsEmbeddings)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "model '" + ger.Model + "' does not support embeddings");

                req.Http.Response.StatusCode = 403;

                return new OpenAIError
                {
                    Error = new OpenAIErrorDetails
                    {
                        Message = "You are not allowed to generate embeddings from this model",
                        Type = "invalid_request_error",
                        Parameters = null,
                        Code = null
                    }
                };
            }

            if (ger.IsSingleInput())
            {
                string input = ger.GetInput();

                if (String.IsNullOrEmpty(input))
                {
                    SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "no input supplied in embeddings request");

                    req.Http.Response.StatusCode = 400;

                    return new OpenAIError
                    {
                        Error = new OpenAIErrorDetails
                        {
                            Message = "'input' is a required property",
                            Type = "invalid_request_error",
                            Parameters = null,
                            Code = null
                        }
                    };
                }
                else
                {
                    float[][] embeddings = new float[1][];
                    embeddings[0] = await engine.GenerateEmbeddingsAsync(input, token).ConfigureAwait(false);
                    ret.Data.Add(new OpenAIEmbedding
                    {
                        Object = "embedding",
                        Index = 0,
                        Embedding = embeddings[0]
                    });
                }
            }
            else
            {
                string[] inputs = ger.GetInputs();

                if (inputs == null || inputs.Length < 1)
                {
                    SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "null or empty inputs supplied in embeddings request");

                    req.Http.Response.StatusCode = 400;

                    return new OpenAIError
                    {
                        Error = new OpenAIErrorDetails
                        {
                            Message = "'$.input' is invalid. Please check the API reference: https://platform.openai.com/docs/api-reference.",
                            Type = "invalid_request_error",
                            Parameters = null,
                            Code = null
                        }
                    };
                }

                for (int i = 0; i < inputs.Length; i++)
                {
                    if (String.IsNullOrEmpty(inputs[i]))
                    {
                        SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "inputs contains null or invalid entries");

                        req.Http.Response.StatusCode = 400;

                        return new OpenAIError
                        {
                            Error = new OpenAIErrorDetails
                            {
                                Message = "'$.input' is invalid. Please check the API reference: https://platform.openai.com/docs/api-reference.",
                                Type = "invalid_request_error",
                                Parameters = null,
                                Code = null
                            }
                        };
                    }
                }

                for (int i = 0; i < inputs.Length; i++)
                {
                    float[][] embeddings = new float[1][];
                    embeddings[0] = await engine.GenerateEmbeddingsAsync(inputs[i], token).ConfigureAwait(false);
                    ret.Data.Add(new OpenAIEmbedding
                    {
                        Object = "embedding",
                        Index = i,
                        Embedding = embeddings[0]
                    });
                }
            }

            return ret;
        }

        internal async Task<object> GenerateCompletion(
            ApiRequest req,
            OpenAIGenerateCompletionRequest gcr,
            CancellationToken token = default)
        {
            using Activity requestSpan = SharpAITelemetry.StartInference("completion", gcr?.Model);

            if (String.IsNullOrEmpty(gcr.Model))
            {
                req.Http.Response.StatusCode = 400;

                return new OpenAIError
                {
                    Error = new OpenAIErrorDetails
                    {
                        Message = "you must provide a model parameter",
                        Type = "invalid_request_error",
                        Parameters = null,
                        Code = null
                    }
                };
            }

            req.Http.Response.ContentType = Constants.JsonContentType;

            // Modelfile-equivalent presets (W5.T4): when the requested model names a preset, run its base
            // model and apply the preset's defaults (system prompt, temperature, max tokens, stop) below.
            ModelPreset preset = _Presets != null ? _Presets.GetByName(gcr.Model) : null;
            string effectiveModel = preset != null ? preset.ModelName : gcr.Model;

            ModelFile modelFile = _ModelFileService.GetByName(effectiveModel);
            if (modelFile == null)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "model " + effectiveModel + " not found");

                req.Http.Response.StatusCode = 404;

                return new OpenAIError
                {
                    Error = new OpenAIErrorDetails
                    {
                        Message = "The model `" + gcr.Model + "` does not exist or you do not have access to it.",
                        Type = "invalid_request_error",
                        Parameters = null,
                        Code = "model_not_found"
                    }
                };
            }

            string enginePath = SharpAI.Operations.GgufShards.Resolve(_Settings.Storage.ModelsDirectory, modelFile.GUID);
            bool wasLoaded = _ModelEngineService.GetLoadedModelPaths().Contains(Path.GetFullPath(enginePath));
            LlamaSharpEngine engine = await _ModelEngineService.GetByModelFileAsync(enginePath, token).ConfigureAwait(false);
            InferenceStatistics statistics = new InferenceStatistics(wasLoaded ? 0 : engine.LoadDurationNanoseconds);
            SharpAI.Server.Classes.Runtime.RequestDiagnostics.Attach(req.Http, statistics);

            if (!engine.SupportsGeneration)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "model '" + gcr.Model + "' does not support completions");

                req.Http.Response.StatusCode = 403;

                return new OpenAIError
                {
                    Error = new OpenAIErrorDetails
                    {
                        Message = "You are not allowed to generate completions from this model",
                        Type = "invalid_request_error",
                        Parameters = "model",
                        Code = null
                    }
                };
            }

            string[] prompts = gcr.GetPrompts();
            if (prompts == null || prompts.Length == 0 || prompts.Any(String.IsNullOrEmpty))
            {
                req.Http.Response.StatusCode = 400;
                return new OpenAIError { Error = new OpenAIErrorDetails { Message = "A non-empty prompt or prompt array is required.", Type = "invalid_request_error", Parameters = "prompt" } };
            }
            foreach (string renderedPrompt in prompts) engine.ValidateContext(renderedPrompt, gcr.MaxTokens ?? 128);
            OpenAIGenerateCompletionResult result = new OpenAIGenerateCompletionResult
            {
                Id = req.Http.Response.Headers.Get(Constants.RequestIdHeader), Object = "text_completion",
                Created = ToUnixTimestamp(DateTime.UtcNow), Model = gcr.Model, Choices = new List<OpenAICompletionChoice>()
            };
            if (gcr.Stream != true)
            {
                for (int i = 0; i < prompts.Length; i++)
                {
                    int before = statistics.CompletionTokens;
                    string output = await engine.GenerateTextAsync(prompts[i], gcr.MaxTokens ?? 128, gcr.Temperature ?? 0.6f,
                        NormalizeStop(gcr.Stop), null, token, statistics, new SharpAI.Models.Ollama.OllamaCompletionOptions { TopP = gcr.TopP, PresencePenalty = gcr.PresencePenalty, FrequencyPenalty = gcr.FrequencyPenalty, Seed = gcr.Seed }).ConfigureAwait(false);
                    result.Choices.Add(new OpenAICompletionChoice { Text = output, Index = i,
                        FinishReason = statistics.CompletionTokens - before >= (gcr.MaxTokens ?? 128) ? "length" : "stop" });
                }
                result.Usage = statistics.ToUsage();
                return result;
            }
            req.Http.Response.ContentType = Constants.EventStreamContentType;
            req.Http.Response.ServerSentEvents = true;
            for (int i = 0; i < prompts.Length; i++)
            {
                int before = statistics.CompletionTokens;
                await foreach (string text in engine.GenerateTextStreamAsync(prompts[i], gcr.MaxTokens ?? 128, gcr.Temperature ?? 0.6f,
                    NormalizeStop(gcr.Stop), null, token, statistics, new SharpAI.Models.Ollama.OllamaCompletionOptions { TopP = gcr.TopP, PresencePenalty = gcr.PresencePenalty, FrequencyPenalty = gcr.FrequencyPenalty, Seed = gcr.Seed }).ConfigureAwait(false))
                {
                    await SharpAI.Server.Classes.Runtime.RequestDiagnostics.SendEvent(req, new ServerSentEvent { Data = _Serializer.SerializeJson(new
                    {
                        id = result.Id, @object = result.Object, created = result.Created, model = result.Model,
                        choices = new object[] { new { text = text, index = i, finish_reason = (string)null } }, usage = (object)null
                    }, false) }, false, token).ConfigureAwait(false);
                }
                await SharpAI.Server.Classes.Runtime.RequestDiagnostics.SendEvent(req, new ServerSentEvent { Data = _Serializer.SerializeJson(new
                {
                    id = result.Id, @object = result.Object, created = result.Created, model = result.Model,
                    choices = new object[] { new { text = "", index = i, finish_reason = statistics.CompletionTokens - before >= (gcr.MaxTokens ?? 128) ? "length" : "stop" } }, usage = (object)null
                }, false) }, false, token).ConfigureAwait(false);
            }
            if (gcr.StreamOptions?.IncludeUsage == true)
            {
                await SharpAI.Server.Classes.Runtime.RequestDiagnostics.SendEvent(req, new ServerSentEvent { Data = _Serializer.SerializeJson(new
                {
                    id = result.Id, @object = result.Object, created = result.Created, model = result.Model,
                    choices = Array.Empty<object>(), usage = statistics.ToUsage()
                }, false) }, false, token).ConfigureAwait(false);
            }
            await SharpAI.Server.Classes.Runtime.RequestDiagnostics.SendEvent(req, new ServerSentEvent { Data = "[DONE]" }, true, token).ConfigureAwait(false);
            return null;
        }

        internal async Task<object> GenerateChatCompletion(
            ApiRequest req,
            OpenAIGenerateChatCompletionRequest gcr,
            CancellationToken token = default)
        {
            using Activity requestSpan = SharpAITelemetry.StartInference("chat", gcr?.Model);

            if (String.IsNullOrEmpty(gcr.Model))
            {
                req.Http.Response.StatusCode = 400;

                return new OpenAIError
                {
                    Error = new OpenAIErrorDetails
                    {
                        Message = "you must provide a model parameter",
                        Type = "invalid_request_error",
                        Parameters = null,
                        Code = null
                    }
                };
            }

            req.Http.Response.ContentType = Constants.JsonContentType;

            // Modelfile-equivalent presets (W5.T4): when the requested model names a preset, run its base
            // model and apply the preset's defaults (system prompt, temperature, max tokens, stop) below.
            ModelPreset preset = _Presets != null ? _Presets.GetByName(gcr.Model) : null;
            string effectiveModel = preset != null ? preset.ModelName : gcr.Model;

            ModelFile modelFile = _ModelFileService.GetByName(effectiveModel);
            if (modelFile == null)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "model " + effectiveModel + " not found");

                req.Http.Response.StatusCode = 404;

                return new OpenAIError
                {
                    Error = new OpenAIErrorDetails
                    {
                        Message = "The model `" + gcr.Model + "` does not exist or you do not have access to it.",
                        Type = "invalid_request_error",
                        Parameters = null,
                        Code = "model_not_found"
                    }
                };
            }

            string enginePath = SharpAI.Operations.GgufShards.Resolve(_Settings.Storage.ModelsDirectory, modelFile.GUID);
            bool wasLoaded = _ModelEngineService.GetLoadedModelPaths().Contains(Path.GetFullPath(enginePath));
            LlamaSharpEngine engine = await _ModelEngineService.GetByModelFileAsync(enginePath, token).ConfigureAwait(false);
            InferenceStatistics statistics = new InferenceStatistics(wasLoaded ? 0 : engine.LoadDurationNanoseconds);
            SharpAI.Server.Classes.Runtime.RequestDiagnostics.Attach(req.Http, statistics);

            if (!engine.SupportsGeneration)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "model '" + gcr.Model + "' does not support completions");

                req.Http.Response.StatusCode = 403;

                return new OpenAIError
                {
                    Error = new OpenAIErrorDetails
                    {
                        Message = "You are not allowed to generate completions from this model",
                        Type = "invalid_request_error",
                        Parameters = "model",
                        Code = null
                    }
                };
            }

            List<ChatMessage> messages = new List<ChatMessage>();
            foreach (OpenAIChatMessage msg in gcr.Messages)
            {
                messages.Add(new ChatMessage
                {
                    Role = msg.Role,
                    Content = RenderOpenAIMessageContent(msg),
                    Timestamp = DateTime.UtcNow
                });
            }

            // Preset defaults (W5.T4): a preset system prompt is applied when the request carries no system
            // message; temperature/max-tokens/stop fall back to the preset when the request omits them.
            if (preset != null) ApplyPresetSystemPrompt(messages, preset.SystemPrompt);
            int effectiveMaxTokens = gcr.MaxTokens ?? preset?.MaxTokens ?? 128;
            float effectiveTemperature = gcr.Temperature ?? preset?.Temperature ?? 0.6f;
            string[] effectiveStop = MergeStop(NormalizeStop(gcr.Stop), preset?.Stop);

            // Tool/function calling (W4.T1): when the caller supplies tools and does not disable them via
            // tool_choice, inject a system instruction describing the tools in the <tool_call>{...} format
            // that ToolCallParser understands, then parse the model's output back into tool_calls below.
            List<ToolDefinition> toolDefinitions = ToolRequestMapper.FromOpenAI(gcr.Tools);
            bool toolsRequested = toolDefinitions.Count > 0 && ToolChoiceAllowsCalls(gcr.ToolChoice);
            if (toolsRequested) InjectToolInstruction(messages, toolDefinitions);

            // JSON mode / structured outputs (W4.T4): when response_format requests JSON and tools are not in
            // play, constrain decoding with a JSON GBNF grammar so the output is guaranteed valid JSON.
            string jsonGrammar = toolsRequested ? null : JsonGrammar.ForOpenAIResponseFormat(gcr.ResponseFormat);

            // Prefer the model's embedded GGUF chat template; fall back to the family template.
            ChatTemplateResult templateResult = ChatTemplateResolver.Resolve(engine, modelFile.Family, messages);
            string prompt = templateResult.Prompt;

            engine.ValidateContext(prompt, effectiveMaxTokens);

            OpenAIGenerateChatCompletionResult ret = new OpenAIGenerateChatCompletionResult
            {
                Id = req.Http.Response.Headers.Get(Constants.RequestIdHeader),
                Object = "chat.completion",
                Created = ToUnixTimestamp(DateTime.UtcNow),
                Model = gcr.Model,
                Usage = null,
                Choices = new List<OpenAIChatChoice>()
            };

            if (gcr.Stream == null || !gcr.Stream.Value)
            {
                #region Non-Streaming

                string response = await engine.GenerateChatCompletionAsync(
                    prompt,
                    effectiveMaxTokens,
                    effectiveTemperature,
                    effectiveStop,
                    jsonGrammar,
                    token, statistics, new SharpAI.Models.Ollama.OllamaCompletionOptions { TopP = gcr.TopP, PresencePenalty = gcr.PresencePenalty, FrequencyPenalty = gcr.FrequencyPenalty, Seed = gcr.Seed }).ConfigureAwait(false);

                List<ParsedToolCall> parsedCalls = toolsRequested ? ToolCallParser.Parse(response) : null;
                if (parsedCalls != null && parsedCalls.Count > 0)
                {
                    ret.Choices.Add(new OpenAIChatChoice
                    {
                        Index = 0,
                        Message = new OpenAIChatMessage
                        {
                            Role = "assistant",
                            Content = null,
                            ToolCalls = ToolResponseMapper.ToOpenAI(parsedCalls)
                        },
                        FinishReason = "tool_calls"
                    });
                }
                else
                {
                    ret.Choices.Add(new OpenAIChatChoice
                    {
                        Index = 0,
                        Message = new OpenAIChatMessage
                        {
                            Role = "assistant",
                            Content = response
                        },
                        FinishReason = statistics.CompletionTokens >= effectiveMaxTokens ? "length" : "stop"
                    });
                }

                ret.Usage = statistics.ToUsage();
                return ret;

                #endregion
            }
            else
            {
                #region Streaming

                string nextToken = null;

                req.Http.Response.ContentType = Constants.EventStreamContentType;
                req.Http.Response.ServerSentEvents = true;

                if (toolsRequested)
                {
                    // With tools, a partial <tool_call> fragment cannot be safely streamed token-by-token, so
                    // the full completion is buffered, parsed, and emitted as a single terminal chunk carrying
                    // either tool_calls (finish_reason: tool_calls) or plain content (finish_reason: stop).
                    StringBuilder buffered = new StringBuilder();
                    await foreach (string curr in engine.GenerateChatCompletionStreamAsync(
                        prompt,
                        effectiveMaxTokens,
                        effectiveTemperature,
                        effectiveStop,
                        null, token, statistics, new SharpAI.Models.Ollama.OllamaCompletionOptions { TopP = gcr.TopP, PresencePenalty = gcr.PresencePenalty, FrequencyPenalty = gcr.FrequencyPenalty, Seed = gcr.Seed }).ConfigureAwait(false))
                    {
                        buffered.Append(curr);
                    }

                    List<ParsedToolCall> streamedCalls = ToolCallParser.Parse(buffered.ToString());
                    OpenAIChatChoice toolChoice = streamedCalls.Count > 0
                        ? new OpenAIChatChoice
                        {
                            Index = 0,
                            Delta = new OpenAIChatMessage { Role = "assistant", Content = null, ToolCalls = ToolResponseMapper.ToOpenAI(streamedCalls) },
                            FinishReason = "tool_calls"
                        }
                        : new OpenAIChatChoice
                        {
                            Index = 0,
                            Delta = new OpenAIChatMessage { Role = "assistant", Content = buffered.ToString().Trim() },
                            FinishReason = statistics.CompletionTokens >= effectiveMaxTokens ? "length" : "stop"
                        };

                    OpenAIGenerateChatCompletionResult toolEvent = new OpenAIGenerateChatCompletionResult
                    {
                        Id = req.Http.Response.Headers.Get(Constants.RequestIdHeader),
                        Object = "chat.completion.chunk",
                        Created = ToUnixTimestamp(DateTime.UtcNow),
                        Model = gcr.Model,
                        Usage = null,
                        Choices = new List<OpenAIChatChoice> { toolChoice }
                    };

                    await SharpAI.Server.Classes.Runtime.RequestDiagnostics.SendEvent(req, new ServerSentEvent
                    {
                        Data = _Serializer.SerializeJson(toolEvent, false)
                    }, false, token).ConfigureAwait(false);

                    if (gcr.StreamOptions?.IncludeUsage == true)
                {
                    await SharpAI.Server.Classes.Runtime.RequestDiagnostics.SendEvent(req, new ServerSentEvent { Data = _Serializer.SerializeJson(new
                    {
                        id = req.Http.Response.Headers.Get(Constants.RequestIdHeader),
                        @object = "chat.completion.chunk",
                        created = ToUnixTimestamp(DateTime.UtcNow), model = gcr.Model,
                        choices = Array.Empty<object>(), usage = statistics.ToUsage()
                    }, false) }, false, token).ConfigureAwait(false);
                }


                    await SharpAI.Server.Classes.Runtime.RequestDiagnostics.SendEvent(req, new ServerSentEvent { Data = "[DONE]" }, true, token).ConfigureAwait(false);
                    return null;
                }

                await foreach (string curr in engine.GenerateChatCompletionStreamAsync(
                    prompt,
                    effectiveMaxTokens,
                    effectiveTemperature,
                    effectiveStop,
                    jsonGrammar,
                    token, statistics, new SharpAI.Models.Ollama.OllamaCompletionOptions { TopP = gcr.TopP, PresencePenalty = gcr.PresencePenalty, FrequencyPenalty = gcr.FrequencyPenalty, Seed = gcr.Seed }).ConfigureAwait(false))
                {
                    if (nextToken != null)
                    {
                        OpenAIGenerateChatCompletionResult currEvent = new OpenAIGenerateChatCompletionResult
                        {
                            Id = req.Http.Response.Headers.Get(Constants.RequestIdHeader),
                            Object = "chat.completion.chunk",
                            Created = ToUnixTimestamp(DateTime.UtcNow),
                            Model = gcr.Model,
                            Usage = null,
                            Choices = new List<OpenAIChatChoice>
                            {
                                new OpenAIChatChoice
                                {
                                    Delta = new OpenAIChatMessage
                                    {
                                        Role = "assistant",
                                        Content = nextToken
                                    },
                                    Index = 0
                                }
                            }
                        };

                        string currEventJson = _Serializer.SerializeJson(currEvent, false);
                        await SharpAI.Server.Classes.Runtime.RequestDiagnostics.SendEvent(req, new ServerSentEvent
                        {
                            Data = currEventJson
                        }, false, token).ConfigureAwait(false);
                    }

                    nextToken = curr;
                }

                if (nextToken != null)
                {
                    OpenAIGenerateChatCompletionResult currEvent = new OpenAIGenerateChatCompletionResult
                    {
                        Id = req.Http.Response.Headers.Get(Constants.RequestIdHeader),
                        Object = "chat.completion.chunk",
                        Created = ToUnixTimestamp(DateTime.UtcNow),
                        Model = gcr.Model,
                        Usage = null,
                        Choices = new List<OpenAIChatChoice>
                            {
                                new OpenAIChatChoice
                                {
                                    Delta = new OpenAIChatMessage
                                    {
                                        Role = "assistant",
                                        Content = nextToken
                                    },
                                    Index = 0
                                }
                            }
                    };

                    string currEventJson = _Serializer.SerializeJson(currEvent, false);
                    await SharpAI.Server.Classes.Runtime.RequestDiagnostics.SendEvent(req, new ServerSentEvent
                    {
                        Data = currEventJson
                    }, false, token).ConfigureAwait(false);
                }

                await SharpAI.Server.Classes.Runtime.RequestDiagnostics.SendEvent(req, new ServerSentEvent { Data = _Serializer.SerializeJson(new
                {
                    id = req.Http.Response.Headers.Get(Constants.RequestIdHeader), @object = "chat.completion.chunk",
                    created = ToUnixTimestamp(DateTime.UtcNow), model = gcr.Model,
                    choices = new object[] { new { index = 0, delta = new { }, finish_reason = statistics.CompletionTokens >= effectiveMaxTokens ? "length" : "stop" } }, usage = (object)null
                }, false) }, false, token).ConfigureAwait(false);

                if (gcr.StreamOptions?.IncludeUsage == true)
                {
                    await SharpAI.Server.Classes.Runtime.RequestDiagnostics.SendEvent(req, new ServerSentEvent { Data = _Serializer.SerializeJson(new
                    {
                        id = req.Http.Response.Headers.Get(Constants.RequestIdHeader),
                        @object = "chat.completion.chunk",
                        created = ToUnixTimestamp(DateTime.UtcNow), model = gcr.Model,
                        choices = Array.Empty<object>(), usage = statistics.ToUsage()
                    }, false) }, false, token).ConfigureAwait(false);
                }


                await SharpAI.Server.Classes.Runtime.RequestDiagnostics.SendEvent(req, new ServerSentEvent
                {
                    Data = "[DONE]"
                }, true, token).ConfigureAwait(false);
                return null;

                #endregion
            }
        }

        #endregion

        #region Private-Methods

        private long ToUnixTimestamp(DateTime dateTime)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var unixTime = (dateTime.ToUniversalTime() - epoch).TotalSeconds;
            return (long)unixTime;
        }

        // Render an incoming OpenAI chat message into plain text for the prompt. Guards against a null
        // content (valid for assistant tool-call turns and some tool results) and, when a prior assistant
        // turn carried tool_calls, replays them in the <tool_call>{...} format so multi-turn tool
        // conversations retain context.
        private static string RenderOpenAIMessageContent(OpenAIChatMessage msg)
        {
            if (msg == null) return String.Empty;

            string content = msg.Content != null ? msg.Content.ToString() : String.Empty;

            if (String.IsNullOrEmpty(content) && msg.ToolCalls != null && msg.ToolCalls.Count > 0)
            {
                StringBuilder builder = new StringBuilder();
                foreach (OpenAIToolCall call in msg.ToolCalls)
                {
                    if (call == null || call.Function == null) continue;
                    builder.Append("<tool_call>{\"name\": \"");
                    builder.Append(call.Function.Name);
                    builder.Append("\", \"arguments\": ");
                    builder.Append(String.IsNullOrEmpty(call.Function.Arguments) ? "{}" : call.Function.Arguments);
                    builder.Append("}</tool_call>");
                }
                return builder.ToString();
            }

            return content;
        }

        // OpenAI tool_choice may be "none" (disable), "auto"/"required" (allow), or an object naming a
        // specific function (allow). Only "none" disables tool calling; anything else permits it.
        private static bool ToolChoiceAllowsCalls(object toolChoice)
        {
            if (toolChoice == null) return true;

            if (toolChoice is string s)
            {
                return !String.Equals(s, "none", StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        // Apply a preset's default system prompt: prepend it as a system message only when the request did
        // not already provide one (an explicit request system message takes precedence).
        private static void ApplyPresetSystemPrompt(List<ChatMessage> messages, string systemPrompt)
        {
            if (String.IsNullOrEmpty(systemPrompt)) return;

            for (int i = 0; i < messages.Count; i++)
            {
                if (String.Equals(messages[i].Role, "system", StringComparison.OrdinalIgnoreCase)) return;
            }

            messages.Insert(0, new ChatMessage { Role = "system", Content = systemPrompt, Timestamp = DateTime.UtcNow });
        }

        // Merge request stop sequences with a preset's, de-duplicating; returns null when the result is empty.
        private static string[] MergeStop(string[] requestStop, List<string> presetStop)
        {
            List<string> merged = new List<string>();
            if (requestStop != null) merged.AddRange(requestStop);
            if (presetStop != null)
            {
                foreach (string s in presetStop)
                {
                    if (!String.IsNullOrEmpty(s) && !merged.Contains(s)) merged.Add(s);
                }
            }
            return merged.Count > 0 ? merged.ToArray() : null;
        }

        // Inject the tool-description system instruction. Appends to the first existing system message when
        // present (so a caller-provided system prompt is preserved), otherwise inserts a new system message
        // at the front.
        private static void InjectToolInstruction(List<ChatMessage> messages, List<ToolDefinition> tools)
        {
            string instruction = ToolPromptBuilder.BuildSystemInstruction(tools);
            if (String.IsNullOrEmpty(instruction)) return;

            for (int i = 0; i < messages.Count; i++)
            {
                if (String.Equals(messages[i].Role, "system", StringComparison.OrdinalIgnoreCase))
                {
                    messages[i].Content = (messages[i].Content ?? String.Empty).TrimEnd() + "\n\n" + instruction;
                    return;
                }
            }

            messages.Insert(0, new ChatMessage
            {
                Role = "system",
                Content = instruction,
                Timestamp = DateTime.UtcNow
            });
        }

        // OpenAI's `stop` field can be a string, an array of strings, or null.
        // Normalize to string[] for the engine.
        private static string[] NormalizeStop(object stop)
        {
            if (stop == null) return null;

            if (stop is string s)
            {
                return string.IsNullOrEmpty(s) ? null : new[] { s };
            }

            if (stop is System.Collections.IEnumerable enumerable)
            {
                List<string> list = new List<string>();
                foreach (object item in enumerable)
                {
                    if (item == null) continue;
                    string itemStr = item.ToString();
                    if (!string.IsNullOrEmpty(itemStr)) list.Add(itemStr);
                }
                return list.Count > 0 ? list.ToArray() : null;
            }

            return null;
        }

        #endregion

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}
