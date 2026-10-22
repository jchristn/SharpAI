namespace SharpAI
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Engines;
    using SharpAI.Serialization;
    using SyslogLogging;

    /// <summary>
    /// Chat driver for handling chat completions with context optimization.
    /// </summary>
    public class ChatDriver
    {
        #region Public-Members

        /// <summary>
        /// Default context tokens.  Default is 4000.  Minimum value is 256.
        /// </summary>
        public int DefaultContextTokens
        {
            get => _DefaultContextTokens;
            set => _DefaultContextTokens = (value >= 256 ? value : throw new ArgumentOutOfRangeException(nameof(DefaultContextTokens)));
        }

        /// <summary>
        /// Context utilization ratio.  Default is 0.3.  Value must be greater than 0 and less than or equal to 1.
        /// </summary>
        public double ContextUtilizationRatio
        {
            get => _ContextUtilizationRatio;
            set => _ContextUtilizationRatio = (value > 0d && value <= 1d ? value : throw new ArgumentOutOfRangeException(nameof(ContextUtilizationRatio)));
        }

        /// <summary>
        /// Chunk size when working with context.  Default is 512.  Minimum value is 256.
        /// </summary>
        public int ChunkSize
        {
            get => _ChunkSize;
            set => _ChunkSize = (value >= 256 ? value : throw new ArgumentOutOfRangeException(nameof(ChunkSize)));
        }

        /// <summary>
        /// The amount of overlap that should exist between tokens.  Default is 100.  Minimum value is 0.
        /// </summary>
        public int ChunkOverlap
        {
            get => _ChunkOverlap;
            set => _ChunkOverlap = (value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(ChunkOverlap)));
        }

        #endregion

        #region Private-Members

        private string _Header = "[ChatDriver] ";
        private LoggingModule _Logging = null;
        private Serializer _Serializer = null;
        private ModelDriver _Models = null;
        private int _DefaultContextTokens = 4000;
        private double _ContextUtilizationRatio = 0.30;
        private int _ChunkSize = 512;
        private int _ChunkOverlap = 100;
        private readonly ConcurrentDictionary<int, List<string>> _ContextCache = new ConcurrentDictionary<int, List<string>>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Chat driver.
        /// </summary>
        /// <param name="logging">Logging module.</param>
        /// <param name="serializer">Serializer.</param>
        /// <param name="models">Model driver.</param>
        public ChatDriver(LoggingModule logging, Serializer serializer, ModelDriver models)
        {
            _Logging = logging ?? new LoggingModule();
            _Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _Models = models ?? throw new ArgumentNullException(nameof(models));

            SharpAI.Logging.JsonLog.Debug(_Logging, _Header + "initialized");
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Generate a chat completion using streaming.
        /// Supports context extraction and optimization from formatted prompt strings.
        /// </summary>
        /// <param name="model">Model.</param>
        /// <param name="prompt">Formatted prompt string. Context can be provided in system messages with <context></context> tags.</param>
        /// <param name="maxTokens">Maximum number of tokens to generate.  Minimum is 100.</param>
        /// <param name="temperature">Temperature.  Minimum is 0.0, maximum is 1.0</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumerable of strings.</returns>
        public async IAsyncEnumerable<string> GenerateCompletionStreaming(
            string model,
            string prompt,
            int maxTokens = 512,
            float temperature = 0.7f,
            [EnumeratorCancellation] CancellationToken token = default)
        {
            if (maxTokens < 100) throw new ArgumentOutOfRangeException(nameof(maxTokens));
            if (temperature < 0.0f || temperature > 1.0f) throw new ArgumentOutOfRangeException(nameof(temperature));
            if (prompt == null) prompt = "";

            LlamaSharpEngine engine = GetModelEngine(model);
            string processedPrompt = ProcessPromptWithContext(prompt, model, maxTokens, engine);
            string optimizedPrompt = OptimizePromptForTokenLimitExact(model, processedPrompt, maxTokens, engine);
            await foreach (string curr in engine.GenerateChatCompletionStreamAsync(optimizedPrompt, maxTokens, temperature, null, token).ConfigureAwait(false))
            {
                yield return curr;
            }
        }

        /// <summary>
        /// Generate a chat completion without streaming.
        /// Supports context extraction and optimization from formatted prompt strings.
        /// </summary>
        /// <param name="model">Model.</param>
        /// <param name="prompt">Formatted prompt string. Context can be provided in system messages with <context></context> tags.</param>
        /// <param name="maxTokens">Maximum number of tokens to generate.  Minimum is 100.</param>
        /// <param name="temperature">Temperature.  Minimum is 0.0, maximum is 1.0</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>String.</returns>
        public async Task<string> GenerateCompletion(
            string model,
            string prompt,
            int maxTokens = 512,
            float temperature = 0.1f,
            CancellationToken token = default)
        {
            if (maxTokens < 100) throw new ArgumentOutOfRangeException(nameof(maxTokens));
            if (temperature < 0.0f || temperature > 1.0f) throw new ArgumentOutOfRangeException(nameof(temperature));
            if (prompt == null) prompt = "";

            LlamaSharpEngine engine = GetModelEngine(model);
            string processedPrompt = ProcessPromptWithContext(prompt, model, maxTokens, engine);
            string optimizedPrompt = OptimizePromptForTokenLimitExact(model, processedPrompt, maxTokens, engine);

            return await engine.GenerateChatCompletionAsync(optimizedPrompt, maxTokens, temperature, null, token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private LlamaSharpEngine GetModelEngine(string model) => _Models.GetEngine(model);

        private string ProcessPromptWithContext(string prompt, string model, int maxGenTokens, LlamaSharpEngine engine)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return prompt;

            PromptSources sources = ExtractSourcesFromPrompt(prompt);
            string docExtractRaw = sources.DocumentExtract;
            string vectorHitsRaw = sources.VectorHits;
            if (string.IsNullOrWhiteSpace(docExtractRaw) && string.IsNullOrWhiteSpace(vectorHitsRaw))
                return prompt;

            string userQuery = ExtractUserQueryFromPrompt(prompt);
            int nCtxUsable = GetMaxContextTokens(model, engine);
            const int safety = 64;

            int allowedPromptTokens = Math.Max(256, nCtxUsable - maxGenTokens - safety);
            string optimizedDoc = docExtractRaw;
            if (!string.IsNullOrWhiteSpace(docExtractRaw))
            {
                List<string> docChunks = GetCachedRelevantChunks(docExtractRaw, userQuery, model);
                optimizedDoc = string.Join("\n\n", docChunks);
                optimizedDoc = optimizedDoc.Length > 0 ? LightCompress(optimizedDoc) : optimizedDoc;
                optimizedDoc = TrimToTokens(optimizedDoc, allowedPromptTokens, engine);
                SharpAI.Logging.JsonLog.Debug(_Logging, $"{_Header}DOC_EXTRACT: chunks={docChunks.Count}, final≈{CountTokensFast(engine, optimizedDoc)}t (budget={allowedPromptTokens}t)");
            }

            string optimizedVec = vectorHitsRaw;
            if (!string.IsNullOrWhiteSpace(vectorHitsRaw))
            {
                List<string> vecChunks = GetCachedRelevantChunks(vectorHitsRaw, userQuery, model);
                optimizedVec = string.Join("\n\n", vecChunks);
                optimizedVec = optimizedVec.Length > 0 ? LightCompress(optimizedVec) : optimizedVec;
                optimizedVec = TrimToTokens(optimizedVec, allowedPromptTokens, engine);
                SharpAI.Logging.JsonLog.Debug(_Logging, $"{_Header}VECTOR_HITS: chunks={vecChunks.Count}, final≈{CountTokensFast(engine, optimizedVec)}t (budget={allowedPromptTokens}t)");
            }

            string processed = prompt;
            if (!string.IsNullOrWhiteSpace(docExtractRaw))
                processed = ReplaceSectionInPrompt(processed, "DOC_EXTRACT", optimizedDoc);
            if (!string.IsNullOrWhiteSpace(vectorHitsRaw))
                processed = ReplaceSectionInPrompt(processed, "VECTOR_HITS", optimizedVec);

            return processed;
        }

        private PromptSources ExtractSourcesFromPrompt(string prompt)
        {
            string doc = ExtractByTag(prompt, "DOC_EXTRACT");
            string vec = ExtractByTag(prompt, "VECTOR_HITS");
            return new PromptSources(doc, vec);
        }

        private string ExtractByTag(string prompt, string tagName)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return null;

            string pattern = $"(?is)<{tagName}>(.*?)</{tagName}>";
            MatchCollection matches = Regex.Matches(prompt, pattern);
            string[] parts = matches
                       .Select(m => m.Groups[1].Value.Trim())
                       .Where(s => !string.IsNullOrWhiteSpace(s))
                      .ToArray();

            string combined = string.Join("\n\n", parts);
            return string.IsNullOrWhiteSpace(combined) ? null : combined;
        }

        private string ReplaceSectionInPrompt(string originalPrompt, string tagName, string optimizedContent)
        {
            if (string.IsNullOrWhiteSpace(originalPrompt))
                return originalPrompt;

            string compressed = LightCompress(optimizedContent ?? string.Empty);
            string pattern = $"(?is)(<{tagName}>)(.*?)(</{tagName}>)";
            return Regex.Replace(
                originalPrompt,
                pattern,
                m =>
                {
                    string open = m.Groups[1].Value;
                    string inner = m.Groups[2].Value;
                    string close = m.Groups[3].Value;
                    if (string.IsNullOrWhiteSpace(inner))
                        return m.Value;
                    return $"{open}\n{compressed}\n{close}";
                }
            );
        }

        private string TruncatePromptByTokens(string prompt, int allowedTokens, LlamaSharpEngine engine)
        {
            if (string.IsNullOrEmpty(prompt)) return prompt;
            if (CountTokensFast(engine, prompt) <= allowedTokens) return prompt;

            string[] lines = prompt.Split('\n');
            if (lines.Length <= 2)
            {
                int approxChars = Math.Max(allowedTokens * 4 - 3, 0);
                return prompt.Length <= approxChars ? prompt : prompt.Substring(0, approxChars) + "...";
            }

            List<string> head = new List<string>(lines.Length / 2);
            List<string> tail = new List<string>(lines.Length / 2);
            int used = 0;
            int headBudget = (int)(allowedTokens * 0.67);

            foreach (string line in lines)
            {
                int t = CountTokensFast(engine, line) + 1;
                if (used + t > headBudget) break;
                head.Add(line);
                used += t;
            }

            int remaining = allowedTokens - used;
            for (int i = lines.Length - 1; i >= 0 && remaining > 0; i--)
            {
                int t = CountTokensFast(engine, lines[i]) + 1;
                if (t > remaining) break;
                tail.Insert(0, lines[i]);
                remaining -= t;
            }

            string joined = string.Join('\n', head) + "\n...\n" + string.Join('\n', tail);

            if (CountTokensFast(engine, joined) > allowedTokens)
            {
                int approxChars = Math.Max(allowedTokens * 4 - 3, 0);
                if (joined.Length > approxChars) joined = joined.Substring(0, approxChars) + "...";
            }
            return joined;
        }

        private string OptimizePromptForTokenLimitExact(string model, string prompt, int maxGenTokens, LlamaSharpEngine engine)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return prompt;

            int nCtxUsable = GetMaxContextTokens(model, engine);
            const int safety = 64;

            int promptTokens = CountTokensFast(engine, prompt);
            if (promptTokens + maxGenTokens + safety <= nCtxUsable)
            {
                if (promptTokens > nCtxUsable * 0.8)
                    SharpAI.Logging.JsonLog.Debug(_Logging, $"{_Header}prompt near token limit: ~{promptTokens} tokens (limit: {nCtxUsable})");
                return prompt;
            }

            int allowedPromptTokens = Math.Max(256, nCtxUsable - maxGenTokens - safety);
            string shrunk = TruncatePromptByTokens(prompt, allowedPromptTokens, engine);

            int shrunkTokens = CountTokensFast(engine, shrunk);
            SharpAI.Logging.JsonLog.Warn(_Logging, $"{_Header}prompt truncated token-aware: ~{shrunkTokens} tokens (from ~{promptTokens}, limit: {nCtxUsable})");
            return shrunk;
        }

        private List<string> GetCachedRelevantChunks(string context, string userQuery, string model)
        {
            int key = HashCode.Combine(
                context.GetHashCode(StringComparison.Ordinal),
                userQuery.GetHashCode(StringComparison.Ordinal));

            if (_ContextCache.TryGetValue(key, out List<string> cached))
                return cached;

            List<string> chunks = FindRelevantChunks(context, userQuery, model, CancellationToken.None);
            if (_ContextCache.Count > 100)
                _ContextCache.Clear();

            _ContextCache[key] = chunks;
            return chunks;
        }

        private string ExtractUserQueryFromPrompt(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return "";

            string[] userPatterns = new[] { "user:", "User:", "human:", "Human:" };

            foreach (string pattern in userPatterns)
            {
                int index = prompt.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                if (index != -1)
                {
                    int startIndex = index + pattern.Length;
                    int endIndex = prompt.IndexOf('\n', startIndex);
                    if (endIndex == -1)
                        endIndex = prompt.Length;

                    return prompt.Substring(startIndex, endIndex - startIndex).Trim();
                }
            }

            return "";
        }

        private int GetMaxContextTokens(string model, LlamaSharpEngine engine)
        {
            try
            {
                int contextSize = engine.GetContextSize();

                if (contextSize > 0)
                {
                    int maxTokens = (int)(contextSize * _ContextUtilizationRatio);
                    SharpAI.Logging.JsonLog.Debug(_Logging, $"{_Header}model '{model}' context size: {contextSize}, using {maxTokens} tokens ({_ContextUtilizationRatio:P0} utilization)");
                    return maxTokens;
                }
                else
                {
                    SharpAI.Logging.JsonLog.Debug(_Logging, $"{_Header}model '{model}' context size not available, using default: {_DefaultContextTokens} tokens");
                    return _DefaultContextTokens;
                }
            }
            catch (Exception ex)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, $"{_Header}Error getting context size for model '{model}', using default: {ex.Message}");
                return _DefaultContextTokens;
            }
        }

        private List<string> ChunkText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            int step = Math.Max(1, _ChunkSize - _ChunkOverlap);
            int estimated = Math.Max(1, text.Length / step);
            List<string> chunks = new List<string>(estimated);

            for (int i = 0; i < text.Length; i += step)
            {
                int len = Math.Min(_ChunkSize, text.Length - i);
                chunks.Add(text.Substring(i, len));
            }

            return chunks;
        }

        private List<string> FindRelevantChunks(string pdfContent, string userQuery, string model, CancellationToken token)
        {
            List<string> chunks = ChunkText(pdfContent);
            if (chunks.Count == 0) return new List<string>();

            string[] queryWords = userQuery
                                  .Split(new[] { ' ', '\t', '\r', '\n', '.', ',', '!', '?', ':', ';', '(', ')', '[', ']', '{', '}', '/', '\\', '"', '\'' },
                                   StringSplitOptions.RemoveEmptyEntries)
                                  .Select(w => w.Trim())
                                  .Where(w => w.Length > 2)
                                  .Select(w => w.ToLowerInvariant())
                                  .Distinct()
                                  .ToArray();

            if (queryWords.Length == 0)
                return chunks.Take(5).ToList();

            List<ScoredTextChunk> scored = new List<ScoredTextChunk>(chunks.Count);
            foreach (string chunk in chunks)
            {
                int s = 0;
                foreach (string qw in queryWords)
                {
                    if (chunk.AsSpan().IndexOf(qw, StringComparison.OrdinalIgnoreCase) >= 0)
                        s++;
                }
                if (s > 0) scored.Add(new ScoredTextChunk(s, chunk));
            }

            List<string> result;
            if (scored.Count > 0)
            {
                result = scored
                    .OrderByDescending(t => t.Score)
                    .Take(5)
                    .Select(t => t.Chunk)
                    .ToList();
            }
            else
            {
                result = chunks.Take(5).ToList();
            }

            SharpAI.Logging.JsonLog.Debug(_Logging, $"{_Header}selected {result.Count} relevant chunks from {chunks.Count} total chunks");
            return result;
        }

        private int CountTokensFast(LlamaSharpEngine engine, string text)
        {
            return engine.CountTokens(text);
        }

        private static string LightCompress(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            s = Regex.Replace(s, @"[ \t]+", " ");
            IEnumerable<string> lines = s.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Distinct();
            IEnumerable<string> trimmed = lines.Select(line =>
            {
                string[] parts = line.Split(new[] { ". ", "!\n", "?\n", ".\n" }, StringSplitOptions.None);
                return string.Join(". ", parts.Take(3)) + (parts.Length > 3 ? "..." : "");
            });
            return string.Join("\n", trimmed);
        }

        private string TrimToTokens(string text, int tokenBudget, LlamaSharpEngine engine)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            return CountTokensFast(engine, text) <= tokenBudget ? text : TruncatePromptByTokens(text, tokenBudget, engine);
        }
        #endregion
    }
}