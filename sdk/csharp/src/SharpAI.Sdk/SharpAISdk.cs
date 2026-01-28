namespace SharpAI.Sdk
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using SharpAI.Sdk.Implementations;
    using SharpAI.Sdk.Interfaces;
    using RestWrapper;

    /// <summary>
    /// SharpAI SDK for interacting with SharpAI server.
    /// </summary>
    public class SharpAISdk : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Enable or disable logging of request bodies.
        /// </summary>
        public bool LogRequests { get; set; } = false;

        /// <summary>
        /// Enable or disable logging of response bodies.
        /// </summary>
        public bool LogResponses { get; set; } = false;

        /// <summary>
        /// Method to invoke to send log messages.
        /// </summary>
        public Action<string, string>? Logger { get; set; }

        /// <summary>
        /// Endpoint URL for the SharpAI server.
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// Timeout in milliseconds for HTTP requests.
        /// </summary>
        public int TimeoutMs { get; set; } = 300000; // 5 minutes

        /// <summary>
        /// Ollama API methods.
        /// </summary>
        public IOllamaMethods Ollama { get; private set; }

        /// <summary>
        /// OpenAI API methods.
        /// </summary>
        public IOpenAIMethods OpenAI { get; private set; }

        /// <summary>
        /// JSON serializer options for API requests and responses.
        /// </summary>
        public readonly JsonSerializerOptions _JsonOptions;

        #endregion

        #region Private-Members

        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize the SharpAI SDK.
        /// </summary>
        /// <param name="endpoint">SharpAI server endpoint URL.</param>
        public SharpAISdk(string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint))
                throw new ArgumentNullException(nameof(endpoint));

            Endpoint = endpoint.TrimEnd('/');

            _JsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };
            _JsonOptions.Converters.Add(new JsonStringEnumConverter());

            Ollama = new OllamaMethods(this);
            OpenAI = new OpenAIMethods(this);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Dispose of the SDK and clean up resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose of the SDK and clean up resources.
        /// </summary>
        /// <param name="disposing">True if disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_Disposed)
                _Disposed = true;
        }

        /// <summary>
        /// Log a message using the configured logger.
        /// </summary>
        /// <param name="level">Log level.</param>
        /// <param name="message">Message to log.</param>
        public void Log(string level, string message)
        {
            if (!string.IsNullOrEmpty(message))
                Logger?.Invoke(level, message);
        }

        /// <summary>
        /// Send a POST request with JSON data and return typed response.
        /// </summary>
        /// <typeparam name="T">Response type.</typeparam>
        /// <param name="url">Full URL to send request to.</param>
        /// <param name="data">Object to serialize and send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Deserialized response of type T.</returns>
        public async Task<T?> PostAsync<T>(string url, object data, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentNullException(nameof(url));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            string json;
            
            if (data is SharpAI.Models.OpenAI.OpenAIGenerateEmbeddingsRequest embeddingsRequest)
            {
                var inputs = embeddingsRequest.GetInputs();
                if (inputs != null && inputs.Length > 1)
                {
                    // Create a custom JSON structure for multiple inputs
                    var customRequest = new
                    {
                        model = embeddingsRequest.Model,
                        input = inputs
                    };
                    json = JsonSerializer.Serialize(customRequest, _JsonOptions);
                }
                else
                {
                    json = JsonSerializer.Serialize(data, _JsonOptions);
                }
            }
            else
            {
                json = JsonSerializer.Serialize(data, _JsonOptions);
            }
            byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);

            using (RestRequest req = new RestRequest(url, HttpMethod.Post))
            {
                req.TimeoutMilliseconds = TimeoutMs;
                req.ContentType = "application/json";

                if (LogRequests)
                    Log("DEBUG", $"POST request to {url} with {jsonBytes.Length} bytes");

                using (RestResponse resp = await req.SendAsync(jsonBytes, cancellationToken).ConfigureAwait(false))
                {
                    if (resp != null)
                    {
                        string? responseData = await ReadResponse(resp, cancellationToken).ConfigureAwait(false);

                        if (LogResponses)
                            Log("DEBUG", $"Response from {url} (status {resp.StatusCode}): {responseData}");

                        if (resp.StatusCode >= 200 && resp.StatusCode <= 299)
                        {
                            Log("DEBUG", $"Success from {url}: {resp.StatusCode}, {resp.ContentLength ?? 0} bytes");
                            
                            if (!string.IsNullOrEmpty(responseData))
                            {
                                Log("DEBUG", "Deserializing response body");
                                return JsonSerializer.Deserialize<T>(responseData, _JsonOptions);
                            }
                            else
                            {
                                Log("DEBUG", "Empty response body, returning null");
                                return default(T);
                            }
                        }
                        else
                        {
                            Log("WARN", $"Non-success from {url}: {resp.StatusCode}, {resp.ContentLength ?? 0} bytes");
                            return default(T);
                        }
                    }
                    else
                    {
                        Log("WARN", $"No response from {url}");
                        return default(T);
                    }
                }
            }
        }

        /// <summary>
        /// Send a GET request and return typed response.
        /// </summary>
        /// <typeparam name="T">Response type.</typeparam>
        /// <param name="url">Full URL to send request to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Deserialized response of type T.</returns>
        public async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentNullException(nameof(url));

            using (RestRequest req = new RestRequest(url, HttpMethod.Get))
            {
                req.TimeoutMilliseconds = TimeoutMs;

                if (LogRequests)
                    Log("DEBUG", $"GET request to {url}");

                using (RestResponse resp = await req.SendAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (resp != null)
                    {
                        string? responseData = await ReadResponse(resp, cancellationToken).ConfigureAwait(false);

                        if (LogResponses)
                            Log("DEBUG", $"Response from {url} (status {resp.StatusCode}): {responseData}");

                        if (resp.StatusCode >= 200 && resp.StatusCode <= 299)
                        {
                            Log("DEBUG", $"Success from {url}: {resp.StatusCode}, {resp.ContentLength ?? 0} bytes");

                            if (!string.IsNullOrEmpty(responseData))
                            {
                                Log("DEBUG", "Deserializing response body");
                                return JsonSerializer.Deserialize<T>(responseData, _JsonOptions);
                            }
                            else
                            {
                                Log("DEBUG", "Empty response body, returning null");
                                return default(T);
                            }
                        }
                        else
                        {
                            Log("WARN", $"Non-success from {url}: {resp.StatusCode}, {resp.ContentLength ?? 0} bytes");
                            return default(T);
                        }
                    }
                    else
                    {
                        Log("WARN", $"No response from {url}");
                        return default(T);
                    }
                }
            }
        }

        /// <summary>
        /// Send a DELETE request with JSON data and return typed response.
        /// </summary>
        /// <typeparam name="T">Response type.</typeparam>
        /// <param name="url">Full URL to send request to.</param>
        /// <param name="data">Object to serialize and send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Deserialized response of type T.</returns>
        public async Task<T?> DeleteAsync<T>(string url, object data, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentNullException(nameof(url));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            string json = JsonSerializer.Serialize(data, _JsonOptions);
            byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);

            using (RestRequest req = new RestRequest(url, HttpMethod.Delete))
            {
                req.TimeoutMilliseconds = TimeoutMs;
                req.ContentType = "application/json";

                if (LogRequests)
                    Log("DEBUG", $"DELETE request to {url} with {jsonBytes.Length} bytes");

                using (RestResponse resp = await req.SendAsync(jsonBytes, cancellationToken).ConfigureAwait(false))
                {
                    if (resp != null)
                    {
                        string? responseData = await ReadResponse(resp, cancellationToken).ConfigureAwait(false);

                        if (LogResponses)
                            Log("DEBUG", $"Response from {url} (status {resp.StatusCode}): {responseData}");

                        if (resp.StatusCode >= 200 && resp.StatusCode <= 299)
                        {
                            Log("DEBUG", $"Success from {url}: {resp.StatusCode}, {resp.ContentLength ?? 0} bytes");

                            if (!string.IsNullOrEmpty(responseData))
                            {
                                Log("DEBUG", "Deserializing response body");
                                return JsonSerializer.Deserialize<T>(responseData, _JsonOptions);
                            }
                            else
                            {
                                Log("DEBUG", "Empty response body, returning null");
                                return default(T);
                            }
                        }
                        else
                        {
                            Log("WARN", $"Non-success from {url}: {resp.StatusCode}, {resp.ContentLength ?? 0} bytes");
                            return default(T);
                        }
                    }
                    else
                    {
                        Log("WARN", $"No response from {url}");
                        return default(T);
                    }
                }
            }
        }

        /// <summary>
        /// Send a POST request and return raw response as string.
        /// </summary>
        /// <param name="url">Full URL to send request to.</param>
        /// <param name="data">Object to serialize and send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Raw response data as string.</returns>
        public async Task<string?> GetRawPostResponse(string url, object data, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentNullException(nameof(url));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            string json = JsonSerializer.Serialize(data, _JsonOptions);
            byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);

            using (RestRequest req = new RestRequest(url, HttpMethod.Post))
            {
                req.TimeoutMilliseconds = TimeoutMs;
                req.ContentType = "application/json";

                if (LogRequests)
                    Log("DEBUG", $"POST request to {url} with {jsonBytes.Length} bytes");

                using (RestResponse resp = await req.SendAsync(jsonBytes, cancellationToken).ConfigureAwait(false))
                {
                    if (resp != null)
                    {
                        string? responseData = await ReadResponse(resp, cancellationToken).ConfigureAwait(false);

                        if (LogResponses)
                            Log("DEBUG", $"Response from {url} (status {resp.StatusCode}): {responseData}");

                        if (resp.StatusCode >= 200 && resp.StatusCode <= 299)
                        {
                            Log("DEBUG", $"Success from {url}: {resp.StatusCode}, {resp.ContentLength ?? 0} bytes");
                            return responseData;
                        }
                        else
                        {
                            Log("WARN", $"Non-success from {url}: {resp.StatusCode}, {resp.ContentLength ?? 0} bytes");
                            return null;
                        }
                    }
                    else
                    {
                        Log("WARN", $"No response from {url}");
                        return null;
                    }
                }
            }
        }

        /// <summary>
        /// Send a GET request and return raw response as string.
        /// </summary>
        /// <param name="url">Full URL to send request to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Raw response data as string.</returns>
        public async Task<string?> GetRawResponse(string url, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentNullException(nameof(url));

            using (RestRequest req = new RestRequest(url, HttpMethod.Get))
            {
                req.TimeoutMilliseconds = TimeoutMs;

                if (LogRequests)
                    Log("DEBUG", $"GET request to {url}");

                using (RestResponse resp = await req.SendAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (resp != null)
                    {
                        string? responseData = await ReadResponse(resp, cancellationToken).ConfigureAwait(false);

                        if (LogResponses)
                            Log("DEBUG", $"Response from {url} (status {resp.StatusCode}): {responseData}");

                        if (resp.StatusCode >= 200 && resp.StatusCode <= 299)
                        {
                            Log("DEBUG", $"Success from {url}: {resp.StatusCode}, {resp.ContentLength ?? 0} bytes");
                            return responseData;
                        }
                        else
                        {
                            Log("WARN", $"Non-success from {url}: {resp.StatusCode}, {resp.ContentLength ?? 0} bytes");
                            return null;
                        }
                    }
                    else
                    {
                        Log("WARN", $"No response from {url}");
                        return null;
                    }
                }
            }
        }

        /// <summary>
        /// Read response data from RestResponse, handling both chunked and non-chunked responses.
        /// </summary>
        /// <param name="resp">REST response.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response data as string.</returns>
        public async Task<string?> ReadResponse(RestResponse resp, CancellationToken token = default)
        {
            if (resp == null) return null;

            string str = string.Empty;

            if (resp.ChunkedTransferEncoding)
            {
                List<string> chunks = new List<string>();
                ChunkData? chunk = null;

                while ((chunk = await resp.ReadChunkAsync(token).ConfigureAwait(false)) != null)
                {
                    if (chunk.Data != null && chunk.Data.Length > 0)
                    {
                        chunks.Add(System.Text.Encoding.UTF8.GetString(chunk.Data));
                    }
                    if (chunk.IsFinal) break;
                }

                str = string.Join("", chunks);
            }
            else
            {
                str = resp.DataAsString;
            }

            return str;
        }

        /// <summary>
        /// Send a streaming POST request and yield deserialized objects of type T.
        /// </summary>
        /// <typeparam name="T">Type to deserialize streaming responses to.</typeparam>
        /// <param name="url">Full URL to send request to.</param>
        /// <param name="data">Object to serialize and send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="processLine">Optional function to process each line before deserialization.</param>
        /// <param name="shouldStop">Optional function to determine if streaming should stop.</param>
        /// <returns>Async enumerable of deserialized objects.</returns>
        public async IAsyncEnumerable<T> PostStreamAsync<T>(
            string url, 
            object data, 
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default,
            Func<string, string>? processLine = null,
            Func<T, bool>? shouldStop = null)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentNullException(nameof(url));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            string json = JsonSerializer.Serialize(data, _JsonOptions);
            byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);

            using (RestRequest req = new RestRequest(url, HttpMethod.Post))
            {
                req.TimeoutMilliseconds = TimeoutMs;
                req.ContentType = "application/json";

                if (LogRequests)
                    Log("DEBUG", $"POST request to {url} with {jsonBytes.Length} bytes");

                using (RestResponse resp = await req.SendAsync(jsonBytes, cancellationToken).ConfigureAwait(false))
                {
                    if (resp != null)
                    {
                        if (resp.StatusCode >= 200 && resp.StatusCode <= 299)
                        {
                            if (LogResponses)
                                Log("DEBUG", $"Success from {url}: {resp.StatusCode}, {resp.ContentLength ?? 0} bytes");

                            if (resp.ChunkedTransferEncoding)
                            {
                                Log("DEBUG", "reading chunked response from " + url);

                                ChunkData? chunk = null;
                                while ((chunk = await resp.ReadChunkAsync(cancellationToken).ConfigureAwait(false)) != null)
                                {
                                    if (chunk.Data != null && chunk.Data.Length > 0)
                                    {
                                        string chunkText = System.Text.Encoding.UTF8.GetString(chunk.Data);
                                        var lines = chunkText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                                        foreach (var line in lines)
                                        {
                                            if (string.IsNullOrWhiteSpace(line)) continue;

                                            string processedLine = processLine?.Invoke(line) ?? line;

                                            T? result = default(T);
                                            try
                                            {
                                                result = JsonSerializer.Deserialize<T>(processedLine, _JsonOptions);
                                            }
                                            catch (JsonException ex)
                                            {
                                                Log("DEBUG", $"Failed to parse JSON line: {processedLine}");
                                                Log("DEBUG", $"JSON error: {ex.Message}");
                                                continue;
                                            }

                                            if (result != null)
                                            {
                                                if (LogResponses)
                                                    Log("DEBUG", $"Parsed streaming result: {line}");

                                                yield return result;

                                                if (shouldStop?.Invoke(result) == true)
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
                                    string processedData = processLine?.Invoke(responseData) ?? responseData;

                                    T? result = default(T);
                                    try
                                    {
                                        result = JsonSerializer.Deserialize<T>(processedData, _JsonOptions);
                                    }
                                    catch (JsonException ex)
                                    {
                                        Log("DEBUG", $"Failed to parse non-chunked response: {processedData}");
                                        Log("DEBUG", $"JSON error: {ex.Message}");
                                    }

                                    if (result != null)
                                        yield return result;
                                }
                            }
                        }
                        else
                        {
                            Log("WARN", $"Non-success from {url}: {resp.StatusCode}, {resp.ContentLength ?? 0} bytes");
                        }
                    }
                    else
                    {
                        Log("WARN", $"No response from {url}");
                    }
                }
            }
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
