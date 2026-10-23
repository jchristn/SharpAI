namespace SharpAI.Http
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
    using SharpAI.Models;
    using SharpAI.Models.Ollama;
    using SharpAI.Operations;
    using SharpAI.Prompts;
    using SharpAI.Serialization;
    using SharpAI.Tools;
    using SyslogLogging;
    using WatsonWebserver.Core;
    using Constants = SharpAI.Constants;

    /// <summary>Shared Ollama inference behavior. Host policy owns model access; request state remains local.</summary>
    public sealed class OllamaInferenceHandler
    {
        private readonly IInferenceModels _Models;
        private readonly InferenceTransport _Transport;
        private readonly LoggingModule _Logging;
        private readonly Serializer _Serializer;
        private readonly string _Header = "[Inference] ";
        private const string _TimestampFormat = "yyyy-MM-ddTHH:mm:ss.ffffffZ";
        /// <summary>Create a shared inference handler. Dependencies are host-owned and must not be null.</summary>
        public OllamaInferenceHandler(IInferenceModels models, InferenceTransport transport, LoggingModule logging, Serializer serializer)
        {
            _Models = models ?? throw new ArgumentNullException(nameof(models));
            _Transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        /// <summary>Execute the compatible inference request. Returns null after streaming, otherwise a response object.</summary>
        public async Task<object?> GenerateEmbeddings(
            ApiRequest req,
            OllamaGenerateEmbeddingsRequest ger,
            CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(req);
            ArgumentNullException.ThrowIfNull(ger);
            using System.Diagnostics.Activity requestSpan = SharpAI.Telemetry.SharpAITelemetry.StartInference("embedding", ger.Model);

            if (String.IsNullOrEmpty(ger.Model)) throw new ArgumentNullException(nameof(ger.Model));

            req.Http.Response.ContentType = Constants.JsonContentType;

            OllamaGenerateEmbeddingsResult ret = new OllamaGenerateEmbeddingsResult
            {
                Model = ger.Model
            };

            ModelFile? modelFile = _Models.Find(ger.Model);
            if (modelFile == null)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "model " + ger.Model + " not found");

                req.Http.Response.StatusCode = 404;

                return new
                {
                    error = $"model '{ger.Model}' not found, try pulling it first"
                };
            }

            LlamaSharpEngine engine = await _Models.LoadAsync(_Models.GetPath(modelFile), token).ConfigureAwait(false);

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

        /// <summary>Execute the compatible inference request. Returns null after streaming, otherwise a response object.</summary>
        public async Task<object?> GenerateCompletion(
            ApiRequest req,
            OllamaGenerateCompletionRequest gcr,
            CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(req);
            ArgumentNullException.ThrowIfNull(gcr);
            using System.Diagnostics.Activity requestSpan = SharpAI.Telemetry.SharpAITelemetry.StartInference("completion", gcr.Model);

            if (String.IsNullOrEmpty(gcr.Model)) throw new ArgumentNullException(nameof(gcr.Model));

            req.Http.Response.ContentType = Constants.JsonContentType;

            ModelFile? modelFile = _Models.Find(gcr.Model);
            if (modelFile == null)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "model " + gcr.Model + " not found");

                req.Http.Response.StatusCode = 404;

                return new
                {
                    error = $"model '{gcr.Model}' not found"
                };
            }

            string enginePath = _Models.GetPath(modelFile);
            bool wasLoaded = _Models.IsLoaded(enginePath);
            LlamaSharpEngine engine = await _Models.LoadAsync(enginePath, token).ConfigureAwait(false);
            InferenceStatistics statistics = new InferenceStatistics(wasLoaded ? 0 : engine.LoadDurationNanoseconds);
            _Transport.Attach(req.Http, statistics);

            string? json = null;

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
            string[]? genStopSequences = gcr.Options.Stop?.ToArray();
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
                ThinkingFilter? genThinkFilter = genDisplayThinking ? null : new ThinkingFilter();

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

                            await _Transport.SendChunk(req, Encoding.UTF8.GetBytes(json), false, token).ConfigureAwait(false);
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

                await _Transport.SendChunk(req, Encoding.UTF8.GetBytes(json), true, token).ConfigureAwait(false);

                return null;
            }
        }

        /// <summary>Execute the compatible inference request. Returns null after streaming, otherwise a response object.</summary>
        public async Task<object?> GenerateChatCompletion(
            ApiRequest req,
            OllamaGenerateChatCompletionRequest gcr,
            CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(req);
            ArgumentNullException.ThrowIfNull(gcr);
            using System.Diagnostics.Activity requestSpan = SharpAI.Telemetry.SharpAITelemetry.StartInference("chat", gcr.Model);

            if (String.IsNullOrEmpty(gcr.Model)) throw new ArgumentNullException(nameof(gcr.Model));
            if (gcr.Messages == null) gcr.Messages = new List<OllamaChatMessage>();

            req.Http.Response.ContentType = Constants.JsonContentType;

            // Modelfile-equivalent presets (W5.T4): resolve a preset by the requested model name and run its
            // base model with the preset's defaults applied below.
            ModelPreset? preset = _Models.FindPreset(gcr.Model);
            string effectiveModel = preset != null ? preset.ModelName : gcr.Model;

            ModelFile? modelFile = _Models.Find(effectiveModel);
            if (modelFile == null)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "model " + effectiveModel + " not found");

                req.Http.Response.StatusCode = 404;

                return new
                {
                    error = $"model '{gcr.Model}' not found"
                };
            }

            string enginePath = _Models.GetPath(modelFile);
            bool wasLoaded = _Models.IsLoaded(enginePath);
            LlamaSharpEngine engine = await _Models.LoadAsync(enginePath, token).ConfigureAwait(false);
            InferenceStatistics statistics = new InferenceStatistics(wasLoaded ? 0 : engine.LoadDurationNanoseconds);
            _Transport.Attach(req.Http, statistics);

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
            ChatTemplateResult templateResult = _Models.FormatChat(engine, modelFile, messages);
            string prompt = templateResult.Prompt;

            // Use client-provided stop sequences, or those the resolver derived (empty for embedded
            // templates, which rely on the model's native end-of-turn tokens).
            string[]? stopSequences = gcr.Options.Stop?.ToArray();
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
            string? jsonGrammar = toolsRequested ? null : JsonGrammar.ForOllamaFormat(gcr.Format);

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
                    List<ParsedToolCall>? parsedCalls = ToolCallParser.Parse(response);
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
                string? json = null;
                ThinkingFilter? thinkFilter = displayThinking ? null : new ThinkingFilter();

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
                    await _Transport.SendChunk(req, Encoding.UTF8.GetBytes(terminalJson), true, token).ConfigureAwait(false);
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

                            await _Transport.SendChunk(req, Encoding.UTF8.GetBytes(json), false, token).ConfigureAwait(false);
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

                await _Transport.SendChunk(req, Encoding.UTF8.GetBytes(json), true, token).ConfigureAwait(false);

                return null;
            }
        }

    }
}
