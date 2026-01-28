namespace SharpAI.Sdk.Interfaces
{
    using SharpAI.Models.OpenAI;

    /// <summary>
    /// Interface for OpenAI API methods.
    /// </summary>
    public interface IOpenAIMethods
    {
        /// <summary>
        /// Generate embeddings for the given input text(s).
        /// </summary>
        /// <param name="request">The embeddings request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Embeddings result.</returns>
        Task<OpenAIGenerateEmbeddingsResult?> GenerateEmbeddingsAsync(
            OpenAIGenerateEmbeddingsRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate embeddings for multiple input texts.
        /// </summary>
        /// <param name="request">The embeddings request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Embeddings result.</returns>
        Task<OpenAIGenerateEmbeddingsResult?> GenerateMultipleEmbeddingsAsync(
            OpenAIGenerateEmbeddingsRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate text completion for the given prompt(s).
        /// </summary>
        /// <param name="request">The completion request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Completion result.</returns>
        Task<OpenAIGenerateCompletionResult?> GenerateCompletionAsync(
            OpenAIGenerateCompletionRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate streaming text completion for the given prompt(s).
        /// </summary>
        /// <param name="request">The completion request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Streaming completion results.</returns>
        IAsyncEnumerable<OpenAIStreamingCompletionResult> GenerateCompletionStreamAsync(
            OpenAIGenerateCompletionRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate chat completion for the given messages.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Chat completion result.</returns>
        Task<OpenAIGenerateChatCompletionResult?> GenerateChatCompletionAsync(
            OpenAIGenerateChatCompletionRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate streaming chat completion for the given messages.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Streaming chat completion results.</returns>
        IAsyncEnumerable<OpenAIStreamingCompletionResult> GenerateChatCompletionStreamAsync(
            OpenAIGenerateChatCompletionRequest request,
            CancellationToken cancellationToken = default);
    }
}
