namespace SharpAI.Sdk.Implementations
{
    using System.Text;
    using System.Text.Json;
    using RestWrapper;
    using SharpAI.Models.Ollama;
    using SharpAI.Sdk.Interfaces;
    using SharpAI.Sdk.Models;

    /// <summary>
    /// Implementation of Ollama API methods.
    /// </summary>
    public class OllamaMethods : IOllamaMethods
    {
        #region Private-Members

        private readonly SharpAISdk _Sdk;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize the Ollama methods implementation.
        /// </summary>
        /// <param name="sdk">SharpAI SDK instance.</param>
        public OllamaMethods(SharpAISdk sdk)
        {
            _Sdk = sdk ?? throw new ArgumentNullException(nameof(sdk));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async IAsyncEnumerable<SharpAIPullModelResponse> PullModel(OllamaPullModelRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            string url = _Sdk.Endpoint + "/api/pull";

            string json = JsonSerializer.Serialize(request);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

            using (RestRequest req = new RestRequest(url, HttpMethod.Post))
            {
                req.TimeoutMilliseconds = _Sdk.TimeoutMs;
                req.ContentType = "application/json";

                if (_Sdk.LogRequests)
                    _Sdk.Log("DEBUG", $"POST request to {url} with {jsonBytes.Length} bytes");

                using (RestResponse resp = await req.SendAsync(jsonBytes, cancellationToken).ConfigureAwait(false))
                {
                    if (resp != null)
                    {
                        if (resp.StatusCode >= 200 && resp.StatusCode <= 299)
                        {
                            if (_Sdk.LogResponses)
                                _Sdk.Log("DEBUG", $"Success from {url}: {resp.StatusCode}, {resp.ContentLength ?? 0} bytes");

                            if (resp.ChunkedTransferEncoding)
                            {
                                _Sdk.Log("DEBUG", "reading chunked response from " + url);

                                ChunkData? chunk = null;
                                while ((chunk = await resp.ReadChunkAsync(cancellationToken).ConfigureAwait(false)) != null)
                                {
                                    if (chunk.Data != null && chunk.Data.Length > 0)
                                    {
                                        string chunkText = Encoding.UTF8.GetString(chunk.Data);

                                        var lines = chunkText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                                        foreach (var line in lines)
                                        {
                                            if (string.IsNullOrWhiteSpace(line)) continue;

                                            SharpAIPullModelResponse? pullResult = null;
                                            try
                                            {
                                                pullResult = JsonSerializer.Deserialize<SharpAIPullModelResponse>(line);
                                            }
                                            catch (JsonException ex)
                                            {
                                                _Sdk.Log("DEBUG", $"Failed to parse JSON line: {line}");
                                                _Sdk.Log("DEBUG", $"JSON error: {ex.Message}");
                                                continue;
                                            }

                                            if (pullResult != null)
                                            {
                                                if (_Sdk.LogResponses)
                                                    _Sdk.Log("DEBUG", $"Parsed pull result: {line}");

                                                yield return pullResult;

                                                if (pullResult.IsComplete())
                                                    yield break;
                                            }
                                        }
                                    }
                                    if (chunk.IsFinal) break;
                                }
                            }
                            else
                            {
                                string responseData = resp.DataAsString;
                                if (!string.IsNullOrEmpty(responseData))
                                {
                                    SharpAIPullModelResponse? result = null;
                                    try
                                    {
                                        result = JsonSerializer.Deserialize<SharpAIPullModelResponse>(responseData);
                                    }
                                    catch (JsonException ex)
                                    {
                                        _Sdk.Log("DEBUG", $"Failed to parse non-chunked response: {responseData}");
                                        _Sdk.Log("DEBUG", $"JSON error: {ex.Message}");
                                    }

                                    if (result != null)
                                        yield return result;
                                }
                            }
                        }
                        else
                        {
                            _Sdk.Log("WARN", $"Non-success from {url}: {resp.StatusCode}, {resp.ContentLength ?? 0} bytes");
                            string? responseData = await _Sdk.ReadResponse(resp, cancellationToken);
                            if (!string.IsNullOrEmpty(responseData))
                            {
                                SharpAIPullModelResponse? result = null;
                                try
                                {
                                    result = JsonSerializer.Deserialize<SharpAIPullModelResponse>(responseData);
                                }
                                catch (JsonException ex)
                                {
                                    _Sdk.Log("DEBUG", $"Failed to parse non-chunked response: {responseData}");
                                    _Sdk.Log("DEBUG", $"JSON error: {ex.Message}");
                                }

                                if (result != null)
                                    yield return result;
                            }
                        }
                    }
                    else
                    {
                        _Sdk.Log("WARN", $"No response from {url}");
                    }
                }
            }
        }

        /// <inheritdoc />
        public async Task<object?> DeleteModel(OllamaDeleteModelRequest request, CancellationToken cancellationToken = default)
        {
            string url = _Sdk.Endpoint + "/api/delete";
            return await _Sdk.DeleteAsync<object>(url, request, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<OllamaLocalModel>?> ListLocalModels(CancellationToken cancellationToken = default)
        {
            string url = _Sdk.Endpoint + "/api/tags";

            string? jsonResponse = await _Sdk.GetRawResponse(url, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(jsonResponse))
                return new List<OllamaLocalModel>();

            using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
            {
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    return JsonSerializer.Deserialize<List<OllamaLocalModel>>(jsonResponse);
                else if (doc.RootElement.TryGetProperty("models", out JsonElement modelsElement))
                    return JsonSerializer.Deserialize<List<OllamaLocalModel>>(modelsElement.GetRawText());
            }

            return new List<OllamaLocalModel>();
        }

        /// <inheritdoc />
        public async Task<OllamaGenerateEmbeddingsResult?> GenerateEmbeddings(OllamaGenerateEmbeddingsRequest request, CancellationToken cancellationToken = default)
        {
            string url = _Sdk.Endpoint + "/api/embed";
            return await _Sdk.PostAsync<OllamaGenerateEmbeddingsResult>(url, request, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<OllamaGenerateEmbeddingsResult?> GenerateMultipleEmbeddings(OllamaGenerateEmbeddingsRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            string url = _Sdk.Endpoint + "/api/embed";
            string json;
            var inputs = request.GetInputs();
            if (inputs != null && inputs.Length > 1)
            {
                var customRequest = new
                {
                    model = request.Model,
                    input = inputs
                };
                json = JsonSerializer.Serialize(customRequest, _Sdk._JsonOptions);
            }
            else
            {
                json = JsonSerializer.Serialize(request, _Sdk._JsonOptions);
            }
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            using (RestRequest req = new RestRequest(url, HttpMethod.Post))
            {
                req.TimeoutMilliseconds = _Sdk.TimeoutMs;
                req.ContentType = "application/json";

                if (_Sdk.LogRequests)
                    _Sdk.Log("DEBUG", $"POST request to {url} with {jsonBytes.Length} bytes");

                using (RestResponse resp = await req.SendAsync(jsonBytes, cancellationToken).ConfigureAwait(false))
                {
                    if (resp != null)
                    {
                        string? responseData = await _Sdk.ReadResponse(resp, cancellationToken).ConfigureAwait(false);

                        if (_Sdk.LogResponses)
                            _Sdk.Log("DEBUG", $"Response from {url} (status {resp.StatusCode}): {responseData}");

                        if (resp.StatusCode >= 200 && resp.StatusCode <= 299)
                        {
                            _Sdk.Log("DEBUG", $"Success from {url}: {resp.StatusCode}, {resp.ContentLength ?? 0} bytes");

                            if (!string.IsNullOrEmpty(responseData))
                            {
                                _Sdk.Log("DEBUG", "Deserializing response body");
                                return JsonSerializer.Deserialize<OllamaGenerateEmbeddingsResult>(responseData, _Sdk._JsonOptions);
                            }
                            else
                            {
                                _Sdk.Log("DEBUG", "Empty response body, returning null");
                                return null;
                            }
                        }
                        else
                        {
                            _Sdk.Log("WARN", $"Non-success from {url}: {resp.StatusCode}, {resp.ContentLength ?? 0} bytes");
                            return null;
                        }
                    }
                    else
                    {
                        _Sdk.Log("WARN", $"No response from {url}");
                        return null;
                    }
                }
            }
        }

        /// <inheritdoc />
        public async Task<OllamaGenerateCompletionResult?> GenerateCompletion(OllamaGenerateCompletionRequest request, CancellationToken cancellationToken = default)
        {
            string url = _Sdk.Endpoint + "/api/generate";
            return await _Sdk.PostAsync<OllamaGenerateCompletionResult>(url, request, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<OllamaGenerateCompletionResult?> GenerateChatCompletion(OllamaGenerateChatCompletionRequest request, CancellationToken cancellationToken = default)
        {
            string url = _Sdk.Endpoint + "/api/chat";
            return await _Sdk.PostAsync<OllamaGenerateCompletionResult>(url, request, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<OllamaGenerateChatCompletionChunk> GenerateChatCompletionStream(OllamaGenerateChatCompletionRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            request.Stream = true;

            string url = _Sdk.Endpoint + "/api/chat";

            await foreach (var streamingResult in _Sdk.PostStreamAsync<OllamaStreamingCompletionResult>(
                url,
                request,
                cancellationToken))
            {
                var result = new OllamaGenerateChatCompletionChunk
                {
                    Model = streamingResult.Model,
                    CreatedAt = streamingResult.CreatedAt,
                    Message = new OllamaChatMessage
                    {
                        Role = "assistant",
                        Content = streamingResult.Response
                    },
                    Done = streamingResult.Done
                };

                yield return result;
            }
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<OllamaStreamingCompletionResult> GenerateCompletionStream(OllamaGenerateCompletionRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Set streaming to true
            request.Stream = true;

            string url = _Sdk.Endpoint + "/api/generate";

            await foreach (var result in _Sdk.PostStreamAsync<OllamaStreamingCompletionResult>(
                url,
                request,
                cancellationToken))
            {
                yield return result;
            }
        }

        #endregion
    }
}
