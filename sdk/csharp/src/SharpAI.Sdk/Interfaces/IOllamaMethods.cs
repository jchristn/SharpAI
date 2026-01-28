namespace SharpAI.Sdk.Interfaces
{
    using SharpAI.Models.Ollama;
    using SharpAI.Sdk.Models;

    /// <summary>
    /// Interface for Ollama API methods.
    /// </summary>
    public interface IOllamaMethods
    {
        /// <summary>
        /// Pull a model from the registry with streaming progress updates.
        /// </summary>
        /// <param name="request">Pull model request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Async enumerable of pull model progress updates.</returns>
        IAsyncEnumerable<SharpAIPullModelResponse> PullModel(OllamaPullModelRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a model.
        /// </summary>
        /// <param name="request">Delete model request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Delete model result.</returns>
        Task<object?> DeleteModel(OllamaDeleteModelRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// List local models.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of local models.</returns>
        Task<List<OllamaLocalModel>?> ListLocalModels(CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate embeddings for the given input text(s).
        /// </summary>
        /// <param name="request">The embeddings request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Embeddings result.</returns>
        Task<OllamaGenerateEmbeddingsResult?> GenerateEmbeddings(OllamaGenerateEmbeddingsRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate embeddings for multiple input texts.
        /// </summary>
        /// <param name="request">The embeddings request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Embeddings result.</returns>
        Task<OllamaGenerateEmbeddingsResult?> GenerateMultipleEmbeddings(OllamaGenerateEmbeddingsRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate text completion.
        /// </summary>
        /// <param name="request">Generate completion request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Generated completion result.</returns>
        Task<OllamaGenerateCompletionResult?> GenerateCompletion(OllamaGenerateCompletionRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate chat completion.
        /// </summary>
        /// <param name="request">Generate chat completion request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Generated chat completion result.</returns>
        Task<OllamaGenerateCompletionResult?> GenerateChatCompletion(OllamaGenerateChatCompletionRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate chat completion with streaming.
        /// </summary>
        /// <param name="request">Generate chat completion request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Async enumerable of streaming chat completion results.</returns>
        IAsyncEnumerable<OllamaGenerateChatCompletionChunk> GenerateChatCompletionStream(OllamaGenerateChatCompletionRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate text completion with streaming.
        /// </summary>
        /// <param name="request">Generate completion request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Async enumerable of streaming completion results.</returns>
        IAsyncEnumerable<OllamaStreamingCompletionResult> GenerateCompletionStream(OllamaGenerateCompletionRequest request, CancellationToken cancellationToken = default);
    }
}
