import type {
  OpenAIGenerateEmbeddingsRequest,
  OpenAIGenerateCompletionRequest,
  OpenAIGenerateChatCompletionRequest,
} from '../models/openai';
import type {
  OpenAIGenerateEmbeddingsResult,
  OpenAIGenerateCompletionResult,
  OpenAIGenerateChatCompletionResult,
  OpenAIStreamingCompletionResult,
} from '../models/openai';

/**
 * Interface for OpenAI API methods.
 */
export interface IOpenAIMethods {
  /**
   * Generate embeddings for the given input text(s).
   * @param request - The embeddings request.
   * @param signal - Optional AbortSignal for cancellation.
   * @returns Embeddings result or null on failure.
   */
  generateEmbeddingsAsync(
    request: OpenAIGenerateEmbeddingsRequest,
    signal?: AbortSignal
  ): Promise<OpenAIGenerateEmbeddingsResult | null>;

  /**
   * Generate text completion for the given prompt(s).
   * @param request - The completion request.
   * @param signal - Optional AbortSignal for cancellation.
   * @returns Completion result or null on failure.
   */
  generateCompletionAsync(
    request: OpenAIGenerateCompletionRequest,
    signal?: AbortSignal
  ): Promise<OpenAIGenerateCompletionResult | null>;

  /**
   * Generate streaming text completion for the given prompt(s).
   * @param request - The completion request.
   * @param signal - Optional AbortSignal for cancellation.
   * @returns AsyncGenerator yielding streaming completion results.
   */
  generateCompletionStreamAsync(
    request: OpenAIGenerateCompletionRequest,
    signal?: AbortSignal
  ): AsyncGenerator<OpenAIStreamingCompletionResult, void, unknown>;

  /**
   * Generate chat completion for the given messages.
   * @param request - The chat completion request.
   * @param signal - Optional AbortSignal for cancellation.
   * @returns Chat completion result or null on failure.
   */
  generateChatCompletionAsync(
    request: OpenAIGenerateChatCompletionRequest,
    signal?: AbortSignal
  ): Promise<OpenAIGenerateChatCompletionResult | null>;

  /**
   * Generate streaming chat completion for the given messages.
   * @param request - The chat completion request.
   * @param signal - Optional AbortSignal for cancellation.
   * @returns AsyncGenerator yielding streaming chat completion results.
   */
  generateChatCompletionStreamAsync(
    request: OpenAIGenerateChatCompletionRequest,
    signal?: AbortSignal
  ): AsyncGenerator<OpenAIStreamingCompletionResult, void, unknown>;
}
