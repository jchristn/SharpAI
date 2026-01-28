import type { IOpenAIMethods } from '../interfaces/IOpenAIMethods';
import type { SharpAISdk } from '../SharpAISdk';
import type {
  OpenAIGenerateEmbeddingsRequest,
  OpenAIGenerateCompletionRequest,
  OpenAIGenerateChatCompletionRequest,
  OpenAIGenerateEmbeddingsResult,
  OpenAIGenerateCompletionResult,
  OpenAIGenerateChatCompletionResult,
  OpenAIStreamingCompletionResult,
} from '../models/openai';

/**
 * Implementation of OpenAI API methods.
 */
export class OpenAIMethods implements IOpenAIMethods {
  private readonly sdk: SharpAISdk;

  /**
   * Initialize the OpenAI methods implementation.
   * @param sdk - SharpAI SDK instance.
   */
  constructor(sdk: SharpAISdk) {
    this.sdk = sdk;
  }

  /**
   * Generate embeddings for the given input text(s).
   */
  async generateEmbeddingsAsync(
    request: OpenAIGenerateEmbeddingsRequest,
    signal?: AbortSignal
  ): Promise<OpenAIGenerateEmbeddingsResult | null> {
    const url = `${this.sdk.endpoint}/v1/embeddings`;
    return this.sdk.postAsync<OpenAIGenerateEmbeddingsResult>(url, request, signal);
  }

  /**
   * Generate text completion for the given prompt(s).
   */
  async generateCompletionAsync(
    request: OpenAIGenerateCompletionRequest,
    signal?: AbortSignal
  ): Promise<OpenAIGenerateCompletionResult | null> {
    const url = `${this.sdk.endpoint}/v1/completions`;
    // Ensure streaming is disabled for non-streaming call
    const body = { ...request, stream: false };
    return this.sdk.postAsync<OpenAIGenerateCompletionResult>(url, body, signal);
  }

  /**
   * Generate streaming text completion for the given prompt(s).
   */
  async *generateCompletionStreamAsync(
    request: OpenAIGenerateCompletionRequest,
    signal?: AbortSignal
  ): AsyncGenerator<OpenAIStreamingCompletionResult, void, unknown> {
    const url = `${this.sdk.endpoint}/v1/completions`;
    const body = { ...request, stream: true };

    yield* this.sdk.postStreamAsync<OpenAIStreamingCompletionResult>(
      url,
      body,
      signal,
      this.processSSELine
    );
  }

  /**
   * Generate chat completion for the given messages.
   */
  async generateChatCompletionAsync(
    request: OpenAIGenerateChatCompletionRequest,
    signal?: AbortSignal
  ): Promise<OpenAIGenerateChatCompletionResult | null> {
    const url = `${this.sdk.endpoint}/v1/chat/completions`;
    // Ensure streaming is disabled for non-streaming call
    const body = { ...request, stream: false };
    return this.sdk.postAsync<OpenAIGenerateChatCompletionResult>(url, body, signal);
  }

  /**
   * Generate streaming chat completion for the given messages.
   */
  async *generateChatCompletionStreamAsync(
    request: OpenAIGenerateChatCompletionRequest,
    signal?: AbortSignal
  ): AsyncGenerator<OpenAIStreamingCompletionResult, void, unknown> {
    const url = `${this.sdk.endpoint}/v1/chat/completions`;
    const body = { ...request, stream: true };

    yield* this.sdk.postStreamAsync<OpenAIStreamingCompletionResult>(
      url,
      body,
      signal,
      this.processSSELine
    );
  }

  /**
   * Process SSE line by removing "data: " prefix.
   */
  private processSSELine(line: string): string {
    if (line.startsWith('data: ')) {
      return line.substring(6);
    }
    return line;
  }
}
