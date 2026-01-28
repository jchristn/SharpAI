import type { IOllamaMethods } from '../interfaces/IOllamaMethods';
import type { SharpAISdk } from '../SharpAISdk';
import type {
  OllamaPullModelRequest,
  OllamaDeleteModelRequest,
  OllamaGenerateEmbeddingsRequest,
  OllamaGenerateCompletionRequest,
  OllamaGenerateChatCompletionRequest,
  SharpAIPullModelResponse,
  OllamaLocalModel,
  OllamaGenerateEmbeddingsResult,
  OllamaGenerateCompletionResult,
  OllamaGenerateChatCompletionChunk,
  OllamaStreamingCompletionResult,
  OllamaChatMessage,
} from '../models/ollama';

/**
 * Implementation of Ollama API methods.
 */
export class OllamaMethods implements IOllamaMethods {
  private readonly sdk: SharpAISdk;

  /**
   * Initialize the Ollama methods implementation.
   * @param sdk - SharpAI SDK instance.
   */
  constructor(sdk: SharpAISdk) {
    this.sdk = sdk;
  }

  /**
   * Pull a model from the registry with streaming progress updates.
   */
  async *pullModel(
    request: OllamaPullModelRequest,
    signal?: AbortSignal
  ): AsyncGenerator<SharpAIPullModelResponse, void, unknown> {
    const url = `${this.sdk.endpoint}/api/pull`;

    yield* this.sdk.postStreamAsync<SharpAIPullModelResponse>(url, request, signal);
  }

  /**
   * Delete a model.
   */
  async deleteModel(
    request: OllamaDeleteModelRequest,
    signal?: AbortSignal
  ): Promise<object | null> {
    const url = `${this.sdk.endpoint}/api/delete`;
    // Ollama uses 'name' instead of 'model' for the delete endpoint
    const body = { name: request.model };
    return this.sdk.deleteAsync<object>(url, body, signal);
  }

  /**
   * List local models.
   */
  async listLocalModels(signal?: AbortSignal): Promise<OllamaLocalModel[] | null> {
    const url = `${this.sdk.endpoint}/api/tags`;
    const response = await this.sdk.getRawResponse(url, signal);

    if (!response) {
      return [];
    }

    try {
      const parsed = JSON.parse(response) as unknown;

      if (Array.isArray(parsed)) {
        return parsed as OllamaLocalModel[];
      }

      if (
        typeof parsed === 'object' &&
        parsed !== null &&
        'models' in parsed &&
        Array.isArray((parsed as { models: unknown }).models)
      ) {
        return (parsed as { models: OllamaLocalModel[] }).models;
      }

      return [];
    } catch {
      this.sdk.log('WARN', `Failed to parse listLocalModels response: ${response}`);
      return [];
    }
  }

  /**
   * Generate embeddings for the given input text(s).
   */
  async generateEmbeddings(
    request: OllamaGenerateEmbeddingsRequest,
    signal?: AbortSignal
  ): Promise<OllamaGenerateEmbeddingsResult | null> {
    const url = `${this.sdk.endpoint}/api/embed`;
    return this.sdk.postAsync<OllamaGenerateEmbeddingsResult>(url, request, signal);
  }

  /**
   * Generate text completion.
   */
  async generateCompletion(
    request: OllamaGenerateCompletionRequest,
    signal?: AbortSignal
  ): Promise<OllamaGenerateCompletionResult | null> {
    const url = `${this.sdk.endpoint}/api/generate`;
    // Ensure streaming is disabled for non-streaming call
    const body = { ...request, stream: false };
    return this.sdk.postAsync<OllamaGenerateCompletionResult>(url, body, signal);
  }

  /**
   * Generate chat completion.
   */
  async generateChatCompletion(
    request: OllamaGenerateChatCompletionRequest,
    signal?: AbortSignal
  ): Promise<OllamaGenerateCompletionResult | null> {
    const url = `${this.sdk.endpoint}/api/chat`;
    // Ensure streaming is disabled for non-streaming call
    const body = { ...request, stream: false };
    return this.sdk.postAsync<OllamaGenerateCompletionResult>(url, body, signal);
  }

  /**
   * Generate chat completion with streaming.
   */
  async *generateChatCompletionStream(
    request: OllamaGenerateChatCompletionRequest,
    signal?: AbortSignal
  ): AsyncGenerator<OllamaGenerateChatCompletionChunk, void, unknown> {
    const url = `${this.sdk.endpoint}/api/chat`;
    const body = { ...request, stream: true };

    for await (const streamingResult of this.sdk.postStreamAsync<OllamaStreamingCompletionResult>(
      url,
      body,
      signal
    )) {
      const message: OllamaChatMessage = {
        role: 'assistant',
        content: streamingResult.response ?? '',
      };

      const chunk: OllamaGenerateChatCompletionChunk = {
        model: streamingResult.model,
        created_at: streamingResult.created_at,
        message,
        done: streamingResult.done,
      };

      yield chunk;
    }
  }

  /**
   * Generate text completion with streaming.
   */
  async *generateCompletionStream(
    request: OllamaGenerateCompletionRequest,
    signal?: AbortSignal
  ): AsyncGenerator<OllamaStreamingCompletionResult, void, unknown> {
    const url = `${this.sdk.endpoint}/api/generate`;
    const body = { ...request, stream: true };

    yield* this.sdk.postStreamAsync<OllamaStreamingCompletionResult>(url, body, signal);
  }
}
