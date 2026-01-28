namespace SharpAI.Sdk.Implementations
{
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using RestWrapper;
    using SharpAI.Models.OpenAI;
    using SharpAI.Sdk.Interfaces;

    /// <summary>
    /// Implementation of OpenAI API methods using RestWrapper.
    /// </summary>
    public class OpenAIMethods : IOpenAIMethods
    {
        #region Private-Members

        private readonly SharpAISdk _Sdk;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize the OpenAI methods with a SharpAI SDK instance.
        /// </summary>
        /// <param name="sdk">SharpAI SDK instance for making HTTP requests.</param>
        public OpenAIMethods(SharpAISdk sdk)
        {
            _Sdk = sdk ?? throw new ArgumentNullException(nameof(sdk));
        }

        #endregion

        #region Public-Methods


        /// <inheritdoc />
        public async Task<OpenAIGenerateEmbeddingsResult?> GenerateEmbeddingsAsync(OpenAIGenerateEmbeddingsRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            string url = _Sdk.Endpoint + "/v1/embeddings";
            return await _Sdk.PostAsync<OpenAIGenerateEmbeddingsResult>(url, request, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<OpenAIGenerateEmbeddingsResult?> GenerateMultipleEmbeddingsAsync(OpenAIGenerateEmbeddingsRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            string url = _Sdk.Endpoint + "/v1/embeddings";
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
                                return JsonSerializer.Deserialize<OpenAIGenerateEmbeddingsResult>(responseData, _Sdk._JsonOptions);
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
        public async Task<OpenAIGenerateCompletionResult?> GenerateCompletionAsync(
            OpenAIGenerateCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            string url = _Sdk.Endpoint + "/v1/completions";
            return await _Sdk.PostAsync<OpenAIGenerateCompletionResult>(url, request, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<OpenAIStreamingCompletionResult> GenerateCompletionStreamAsync(
            OpenAIGenerateCompletionRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            request.Stream = true;
            string url = _Sdk.Endpoint + "/v1/completions";

            await foreach (var result in _Sdk.PostStreamAsync<OpenAIStreamingCompletionResult>(
                url, 
                request, 
                cancellationToken,
                processLine: line => 
                {
                    if (line.StartsWith("data: "))
                        return line.Substring(6);
                    return line;
                }))
            {
                yield return result;
            }
        }

        /// <inheritdoc />
        public async Task<OpenAIGenerateChatCompletionResult?> GenerateChatCompletionAsync(
            OpenAIGenerateChatCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            return await _Sdk.PostAsync<OpenAIGenerateChatCompletionResult>(
                $"{_Sdk.Endpoint}/v1/chat/completions",
                request,
                cancellationToken).ConfigureAwait(false);

        }

        /// <inheritdoc />
        public async IAsyncEnumerable<OpenAIStreamingCompletionResult> GenerateChatCompletionStreamAsync(
            OpenAIGenerateChatCompletionRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            request.Stream = true;

            string url = _Sdk.Endpoint + "/v1/chat/completions";

            await foreach (var result in _Sdk.PostStreamAsync<OpenAIStreamingCompletionResult>(
                url, 
                request, 
                cancellationToken,
                processLine: line => 
                {
                    if (line.StartsWith("data: "))
                        return line.Substring(6);
                    return line;
                }))
            {
                yield return result;
            }
        }


        #endregion
    }
}
