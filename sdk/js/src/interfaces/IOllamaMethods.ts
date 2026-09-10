import type {
  OllamaPullModelRequest,
  OllamaDeleteModelRequest,
  OllamaGenerateEmbeddingsRequest,
  OllamaGenerateCompletionRequest,
  OllamaGenerateChatCompletionRequest,
} from '../models/ollama';
import type {
  SharpAIPullModelResponse,
  OllamaLocalModel,
  OllamaGenerateEmbeddingsResult,
  OllamaGenerateCompletionResult,
  OllamaGenerateChatCompletionChunk,
  OllamaStreamingCompletionResult,
} from '../models/ollama';

/**
 * Interface for Ollama API methods.
 */
export interface IOllamaMethods {
  /**
   * Pull a model from the registry with streaming progress updates.
   * @param request - Pull model request.
   * @param signal - Optional AbortSignal for cancellation.
   * @returns AsyncGenerator yielding pull model progress updates.
   */
  pullModel(
    request: OllamaPullModelRequest,
    signal?: AbortSignal
  ): AsyncGenerator<SharpAIPullModelResponse, void, unknown>;

  /**
   * Delete a model.
   * @param request - Delete model request.
   * @param signal - Optional AbortSignal for cancellation.
   * @returns Delete model result or null on failure.
   */
  deleteModel(
    request: OllamaDeleteModelRequest,
    signal?: AbortSignal
  ): Promise<object | null>;

  /**
   * List local models.
   * @param signal - Optional AbortSignal for cancellation.
   * @returns List of local models or null on failure.
   */
  listLocalModels(signal?: AbortSignal): Promise<OllamaLocalModel[] | null>;

  /**
   * Generate embeddings for the given input text(s).
   * @param request - The embeddings request.
   * @param signal - Optional AbortSignal for cancellation.
   * @returns Embeddings result or null on failure.
   */
  generateEmbeddings(
    request: OllamaGenerateEmbeddingsRequest,
    signal?: AbortSignal
  ): Promise<OllamaGenerateEmbeddingsResult | null>;

  /**
   * Generate text completion.
   * @param request - Generate completion request.
   * @param signal - Optional AbortSignal for cancellation.
   * @returns Generated completion result or null on failure.
   */
  generateCompletion(
    request: OllamaGenerateCompletionRequest,
    signal?: AbortSignal
  ): Promise<OllamaGenerateCompletionResult | null>;

  /**
   * Generate chat completion.
   * @param request - Generate chat completion request.
   * @param signal - Optional AbortSignal for cancellation.
   * @returns Generated chat completion result or null on failure.
   */
  generateChatCompletion(
    request: OllamaGenerateChatCompletionRequest,
    signal?: AbortSignal
  ): Promise<OllamaGenerateCompletionResult | null>;

  /**
   * Generate chat completion with streaming.
   * @param request - Generate chat completion request.
   * @param signal - Optional AbortSignal for cancellation.
   * @returns AsyncGenerator yielding streaming chat completion results.
   */
  generateChatCompletionStream(
    request: OllamaGenerateChatCompletionRequest,
    signal?: AbortSignal
  ): AsyncGenerator<OllamaGenerateChatCompletionChunk, void, unknown>;

  /**
   * Generate text completion with streaming.
   * @param request - Generate completion request.
   * @param signal - Optional AbortSignal for cancellation.
   * @returns AsyncGenerator yielding streaming completion results.
   */
  generateCompletionStream(
    request: OllamaGenerateCompletionRequest,
    signal?: AbortSignal
  ): AsyncGenerator<OllamaStreamingCompletionResult, void, unknown>;

  /** Import a GGUF model already present on the server's local filesystem (no download, no token). */
  importModel(
    request: { path: string; name?: string },
    signal?: AbortSignal
  ): Promise<object | null>;

  /** List Modelfile-equivalent presets. */
  listPresets(signal?: AbortSignal): Promise<object | null>;

  /** Create a Modelfile-equivalent preset. */
  createPreset(
    request: {
      name: string;
      model: string;
      system?: string;
      temperature?: number;
      max_tokens?: number;
      top_p?: number;
      template?: string;
      stop?: string[];
    },
    signal?: AbortSignal
  ): Promise<object | null>;

  /** Read a preset by name. */
  getPreset(name: string, signal?: AbortSignal): Promise<object | null>;

  /** Delete a preset by name. */
  deletePreset(name: string, signal?: AbortSignal): Promise<object | null>;
}
