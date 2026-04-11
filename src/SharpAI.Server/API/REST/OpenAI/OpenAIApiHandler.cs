namespace SharpAI.Server.API.REST.OpenAI
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Engines;
    using SharpAI.Hosting;
    using SharpAI.Models;
    using SharpAI.Models.OpenAI;
    using SharpAI.Prompts;
    using SharpAI.Serialization;
    using SharpAI.Server.Classes.Settings;
    using SharpAI.Services;
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

        #endregion

        #region Constructors-and-Factories

        internal OpenAIApiHandler(
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

        internal async Task<object> GenerateEmbeddings(
            ApiRequest req,
            OpenAIGenerateEmbeddingsRequest ger,
            CancellationToken token = default)
        {
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
                _Logging.Warn(_Header + "no input supplied in embeddings request");

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
                _Logging.Warn(_Header + "model " + ger.Model + " not found");

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

            LlamaSharpEngine engine = _ModelEngineService.GetByModelFile(Path.Combine(_Settings.Storage.ModelsDirectory, modelFile.GUID.ToString()));

            if (!engine.SupportsEmbeddings)
            {
                _Logging.Warn(_Header + "model '" + ger.Model + "' does not support embeddings");

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
                    _Logging.Warn(_Header + "no input supplied in embeddings request");

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
                    _Logging.Warn(_Header + "null or empty inputs supplied in embeddings request");

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
                        _Logging.Warn(_Header + "inputs contains null or invalid entries");

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

            ModelFile modelFile = _ModelFileService.GetByName(gcr.Model);
            if (modelFile == null)
            {
                _Logging.Warn(_Header + "model " + gcr.Model + " not found");

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

            LlamaSharpEngine engine = _ModelEngineService.GetByModelFile(Path.Combine(_Settings.Storage.ModelsDirectory, modelFile.GUID.ToString()));

            if (!engine.SupportsGeneration)
            {
                _Logging.Warn(_Header + "model '" + gcr.Model + "' does not support completions");

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

            OpenAIGenerateCompletionResult ret = new OpenAIGenerateCompletionResult
            {
                Id = req.Http.Response.Headers.Get(Constants.RequestIdHeader),
                Object = "text_completion",
                Created = ToUnixTimestamp(DateTime.UtcNow),
                Model = gcr.Model,
                Usage = null,
                Choices = new List<OpenAICompletionChoice>()
            };

            if (gcr.Stream == null || !gcr.Stream.Value)
            {
                #region Non-Streaming

                if (gcr.IsSinglePrompt())
                {
                    #region Single-Prompt

                    string response = await engine.GenerateTextAsync(
                        gcr.GetPrompt(),
                        gcr.MaxTokens != null ? gcr.MaxTokens.Value : 128,
                        gcr.Temperature != null ? gcr.Temperature.Value : 0.6f,
                        NormalizeStop(gcr.Stop),
                        token).ConfigureAwait(false);

                    ret.Choices.Add(new OpenAICompletionChoice
                    {
                        Text = response,
                        Index = 0
                    });

                    return ret;

                    #endregion
                }
                else
                {
                    #region Multiple-Prompts

                    string[] prompts = gcr.GetPrompts();

                    for (int i = 0; i < prompts.Length; i++)
                    {
                        string response = await engine.GenerateTextAsync(
                            prompts[i],
                            gcr.MaxTokens != null ? gcr.MaxTokens.Value : 128,
                            gcr.Temperature != null ? gcr.Temperature.Value : 0.6f,
                            NormalizeStop(gcr.Stop),
                            token).ConfigureAwait(false);

                        ret.Choices.Add(new OpenAICompletionChoice
                        {
                            Text = response,
                            Index = i
                        });
                    }

                    return ret;

                    #endregion
                }

                #endregion
            }
            else
            {
                #region Streaming

                string nextToken = null;
                req.Http.Response.ContentType = Constants.EventStreamContentType;
                req.Http.Response.ServerSentEvents = true;

                if (gcr.IsSinglePrompt())
                {
                    #region Single-Prompt

                    await foreach (string curr in engine.GenerateTextStreamAsync(
                        gcr.GetPrompt(),
                        gcr.MaxTokens != null ? gcr.MaxTokens.Value : 128,
                        gcr.Temperature != null ? gcr.Temperature.Value : 0.6f,
                        NormalizeStop(gcr.Stop),
                        token).ConfigureAwait(false))
                    {
                        if (nextToken != null)
                        {
                            OpenAIGenerateCompletionResult currEvent = new OpenAIGenerateCompletionResult
                            {
                                Id = req.Http.Response.Headers.Get(Constants.RequestIdHeader),
                                Object = "text_completion",
                                Created = ToUnixTimestamp(DateTime.UtcNow),
                                Model = gcr.Model,
                                Usage = null,
                                Choices = new List<OpenAICompletionChoice>
                                {
                                    new OpenAICompletionChoice
                                    {
                                        Text = nextToken,
                                        Index = 0
                                    }
                                }
                            };

                            string currEventJson = _Serializer.SerializeJson(currEvent, false);
                            await req.Http.Response.SendEvent(new ServerSentEvent
                            {
                                Data = currEventJson
                            }, false, token).ConfigureAwait(false);
                        }

                        nextToken = curr;
                    }

                    if (nextToken != null)
                    {
                        OpenAIGenerateCompletionResult currEvent = new OpenAIGenerateCompletionResult
                        {
                            Id = req.Http.Response.Headers.Get(Constants.RequestIdHeader),
                            Object = "text_completion",
                            Created = ToUnixTimestamp(DateTime.UtcNow),
                            Model = gcr.Model,
                            Usage = null,
                            Choices = new List<OpenAICompletionChoice>
                                {
                                    new OpenAICompletionChoice
                                    {
                                        Text = nextToken,
                                        Index = 0
                                    }
                                }
                        };

                        string currEventJson = _Serializer.SerializeJson(currEvent, false);
                        await req.Http.Response.SendEvent(new ServerSentEvent
                        {
                            Data = currEventJson
                        }, false, token).ConfigureAwait(false);
                    }

                    await req.Http.Response.SendEvent(new ServerSentEvent
                    {
                        Data = "[DONE]"
                    }, true, token).ConfigureAwait(false);
                    return null;

                    #endregion
                }
                else
                {
                    #region Multiple-Prompts

                    string[] prompts = gcr.GetPrompts();

                    for (int i = 0; i < prompts.Length; i++)
                    {
                        await foreach (string curr in engine.GenerateTextStreamAsync(
                            prompts[i],
                            gcr.MaxTokens != null ? gcr.MaxTokens.Value : 128,
                            gcr.Temperature != null ? gcr.Temperature.Value : 0.6f,
                            NormalizeStop(gcr.Stop),
                            token).ConfigureAwait(false))
                        {
                            if (nextToken != null)
                            {
                                OpenAIGenerateCompletionResult currEvent = new OpenAIGenerateCompletionResult
                                {
                                    Id = req.Http.Response.Headers.Get(Constants.RequestIdHeader),
                                    Object = "text_completion",
                                    Created = ToUnixTimestamp(DateTime.UtcNow),
                                    Model = gcr.Model,
                                    Usage = null,
                                    Choices = new List<OpenAICompletionChoice>
                                    {
                                        new OpenAICompletionChoice
                                        {
                                            Text = nextToken,
                                            Index = i
                                        }
                                    }
                                };

                                string currEventJson = _Serializer.SerializeJson(currEvent, false);
                                await req.Http.Response.SendEvent(new ServerSentEvent
                                {
                                    Data = currEventJson
                                }, false, token).ConfigureAwait(false);
                            }

                            nextToken = curr;
                        }
                    }

                    if (nextToken != null)
                    {
                        OpenAIGenerateCompletionResult currEvent = new OpenAIGenerateCompletionResult
                        {
                            Id = req.Http.Response.Headers.Get(Constants.RequestIdHeader),
                            Object = "text_completion",
                            Created = ToUnixTimestamp(DateTime.UtcNow),
                            Model = gcr.Model,
                            Usage = null,
                            Choices = new List<OpenAICompletionChoice>
                                {
                                    new OpenAICompletionChoice
                                    {
                                        Text = nextToken,
                                        Index = 0
                                    }
                                }
                        };

                        string currEventJson = _Serializer.SerializeJson(currEvent, false);
                        await req.Http.Response.SendEvent(new ServerSentEvent
                        {
                            Data = currEventJson
                        }, false, token).ConfigureAwait(false);
                    }

                    await req.Http.Response.SendEvent(new ServerSentEvent
                    {
                        Data = "[DONE]"
                    }, true, token).ConfigureAwait(false);
                    return null;

                    #endregion
                }

                #endregion
            }
        }

        internal async Task<object> GenerateChatCompletion(
            ApiRequest req,
            OpenAIGenerateChatCompletionRequest gcr,
            CancellationToken token = default)
        {
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

            ModelFile modelFile = _ModelFileService.GetByName(gcr.Model);
            if (modelFile == null)
            {
                _Logging.Warn(_Header + "model " + gcr.Model + " not found");

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

            LlamaSharpEngine engine = _ModelEngineService.GetByModelFile(Path.Combine(_Settings.Storage.ModelsDirectory, modelFile.GUID.ToString()));

            if (!engine.SupportsGeneration)
            {
                _Logging.Warn(_Header + "model '" + gcr.Model + "' does not support completions");

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
                    Content = msg.Content.ToString(),
                    Timestamp = DateTime.UtcNow
                });
            }

            string prompt = ChatPromptBuilder.Build(
                ChatFormatHelper.ModelFamilyToChatFormat(modelFile.Family, ChatFormatEnum.Simple),
                messages);

            OpenAIGenerateChatCompletionResult ret = new OpenAIGenerateChatCompletionResult
            {
                Id = req.Http.Response.Headers.Get(Constants.RequestIdHeader),
                Object = "text_completion",
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
                    gcr.MaxTokens != null ? gcr.MaxTokens.Value : 128,
                    gcr.Temperature != null ? gcr.Temperature.Value : 0.6f,
                    NormalizeStop(gcr.Stop),
                    token).ConfigureAwait(false);

                ret.Choices.Add(new OpenAIChatChoice
                {
                    Index = 0,
                    Message = new OpenAIChatMessage
                    {
                        Role = "assistant",
                        Content = response
                    }
                });

                return ret;

                #endregion
            }
            else
            {
                #region Streaming

                string nextToken = null;

                req.Http.Response.ContentType = Constants.NdJsonContentType;
                req.Http.Response.ServerSentEvents = true;

                await foreach (string curr in engine.GenerateChatCompletionStreamAsync(
                    prompt,
                    gcr.MaxTokens != null ? gcr.MaxTokens.Value : 128,
                    gcr.Temperature != null ? gcr.Temperature.Value : 0.6f,
                    NormalizeStop(gcr.Stop),
                    token).ConfigureAwait(false))
                {
                    if (nextToken != null)
                    {
                        OpenAIGenerateCompletionResult currEvent = new OpenAIGenerateCompletionResult
                        {
                            Id = req.Http.Response.Headers.Get(Constants.RequestIdHeader),
                            Object = "text_completion",
                            Created = ToUnixTimestamp(DateTime.UtcNow),
                            Model = gcr.Model,
                            Usage = null,
                            Choices = new List<OpenAICompletionChoice>
                            {
                                new OpenAICompletionChoice
                                {
                                    Text = nextToken,
                                    Index = 0
                                }
                            }
                        };

                        string currEventJson = _Serializer.SerializeJson(currEvent, false);
                        await req.Http.Response.SendEvent(new ServerSentEvent
                        {
                            Data = currEventJson
                        }, false, token).ConfigureAwait(false);
                    }

                    nextToken = curr;
                }

                if (nextToken != null)
                {
                    OpenAIGenerateCompletionResult currEvent = new OpenAIGenerateCompletionResult
                    {
                        Id = req.Http.Response.Headers.Get(Constants.RequestIdHeader),
                        Object = "text_completion",
                        Created = ToUnixTimestamp(DateTime.UtcNow),
                        Model = gcr.Model,
                        Usage = null,
                        Choices = new List<OpenAICompletionChoice>
                            {
                                new OpenAICompletionChoice
                                {
                                    Text = nextToken,
                                    Index = 0
                                }
                            }
                    };

                    string currEventJson = _Serializer.SerializeJson(currEvent, false);
                    await req.Http.Response.SendEvent(new ServerSentEvent
                    {
                        Data = currEventJson
                    }, false, token).ConfigureAwait(false);
                }

                await req.Http.Response.SendEvent(new ServerSentEvent
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
