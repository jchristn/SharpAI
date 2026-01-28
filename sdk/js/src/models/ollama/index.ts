/**
 * Ollama pull model request.
 */
export interface OllamaPullModelRequest {
  /** Model name to pull (required). */
  model?: string;
  /** Model name to pull (alternative field). */
  name?: string;
  /** Allow insecure connections to the registry. */
  insecure?: boolean;
  /** Enable streaming of pull progress. */
  stream?: boolean;
  /** Username for registry authentication. */
  username?: string;
  /** Password for registry authentication. */
  password?: string;
}

/**
 * SharpAI server pull model response message.
 */
export interface SharpAIPullModelResponse {
  /** Current status of the pull operation. */
  status: string;
  /** Number of bytes downloaded. */
  downloaded?: number;
  /** Download progress as a decimal (0.0 to 1.0). */
  percent?: number;
  /** Error message if the pull operation failed. */
  error?: string;
}

/**
 * Helper functions for SharpAIPullModelResponse.
 */
export const SharpAIPullModelResponseHelpers = {
  /**
   * Gets the download progress as a percentage (0-100).
   */
  getProgressPercentage(response: SharpAIPullModelResponse): number | null {
    if (response.percent !== undefined) {
      return response.percent * 100;
    }
    return null;
  },

  /**
   * Gets a formatted progress string.
   */
  getFormattedProgress(response: SharpAIPullModelResponse): string {
    if (response.downloaded !== undefined && response.percent !== undefined) {
      const downloadedStr = formatBytes(response.downloaded);
      const percentStr = (response.percent * 100).toFixed(1);
      return `${downloadedStr} (${percentStr}%)`;
    }
    return response.status ?? 'Unknown';
  },

  /**
   * Checks if the operation is complete.
   */
  isComplete(response: SharpAIPullModelResponse): boolean {
    return response.status?.toLowerCase() === 'success';
  },

  /**
   * Checks if the operation has failed.
   */
  hasError(response: SharpAIPullModelResponse): boolean {
    return !!response.error;
  },
};

/**
 * Ollama delete model request.
 */
export interface OllamaDeleteModelRequest {
  /** Name of the model to delete (maps to 'name' in JSON). */
  model: string;
}

/**
 * Ollama local model information.
 */
export interface OllamaLocalModel {
  /** Model name including tag. */
  name?: string;
  /** Model digest/hash. */
  digest?: string;
  /** Model size in bytes. */
  size?: number;
  /** When the model was last modified. */
  modified_at?: string;
  /** Model details. */
  details?: OllamaModelDetails;
}

/**
 * Helper functions for OllamaLocalModel.
 */
export const OllamaLocalModelHelpers = {
  /**
   * Gets the model's base name without tag.
   */
  getBaseName(model: OllamaLocalModel): string {
    if (!model.name) return '';
    const colonIndex = model.name.indexOf(':');
    return colonIndex > 0 ? model.name.substring(0, colonIndex) : model.name;
  },

  /**
   * Gets the model's tag.
   */
  getTag(model: OllamaLocalModel): string {
    if (!model.name) return 'latest';
    const colonIndex = model.name.indexOf(':');
    return colonIndex > 0 ? model.name.substring(colonIndex + 1) : 'latest';
  },

  /**
   * Gets the formatted size of the model.
   */
  getFormattedSize(model: OllamaLocalModel): string {
    if (model.size === undefined) return 'Unknown';
    return formatBytes(model.size);
  },

  /**
   * Gets a short digest identifier.
   */
  getShortDigest(model: OllamaLocalModel): string {
    if (!model.digest) return '';
    const parts = model.digest.split(':');
    const hash = parts.length === 2 ? parts[1] : model.digest;
    return hash && hash.length > 12 ? hash.substring(0, 12) : (hash ?? '');
  },
};

/**
 * Ollama model details.
 */
export interface OllamaModelDetails {
  /** Model format (e.g., "gguf"). */
  format?: string;
  /** Model family. */
  family?: string;
  /** Families supported by the model. */
  families?: string[];
  /** Parameter size (e.g., "7B"). */
  parameter_size?: string;
  /** Quantization level (e.g., "Q4_0"). */
  quantization_level?: string;
}

/**
 * Ollama generate embeddings request.
 */
export interface OllamaGenerateEmbeddingsRequest {
  /** Model name to use for generating embeddings. */
  model: string;
  /** Input text(s) to generate embeddings for. Can be a string or array. */
  input: string | string[];
  /** Options for generating embeddings. */
  options?: OllamaEmbeddingsOptions;
  /** How long to keep the model loaded in memory. */
  keep_alive?: string;
  /** Truncate inputs that exceed the model's context window. */
  truncate?: boolean;
}

/**
 * Ollama embeddings options.
 */
export interface OllamaEmbeddingsOptions {
  /** Number of threads to use. */
  num_thread?: number;
}

/**
 * Ollama generate embeddings result.
 */
export interface OllamaGenerateEmbeddingsResult {
  /** The model that generated the embeddings. */
  model?: string;
  /** Generated embedding(s). */
  embeddings?: number[] | number[][];
  /** Total duration in nanoseconds. */
  total_duration?: number;
  /** Model load duration in nanoseconds. */
  load_duration?: number;
  /** Prompt evaluation count. */
  prompt_eval_count?: number;
}

/**
 * Helper functions for OllamaGenerateEmbeddingsResult.
 */
export const OllamaGenerateEmbeddingsResultHelpers = {
  /**
   * Gets a single embedding array.
   */
  getEmbedding(result: OllamaGenerateEmbeddingsResult): number[] | null {
    if (!result.embeddings) return null;
    if (Array.isArray(result.embeddings[0])) {
      const multipleEmbeddings = result.embeddings as number[][];
      if (multipleEmbeddings.length === 1) return multipleEmbeddings[0] ?? null;
      throw new Error(
        `Result contains ${multipleEmbeddings.length} embeddings. Use getEmbeddings() instead.`
      );
    }
    return result.embeddings as number[];
  },

  /**
   * Gets all embeddings as a list of arrays.
   */
  getEmbeddings(result: OllamaGenerateEmbeddingsResult): number[][] | null {
    if (!result.embeddings) return null;
    if (Array.isArray(result.embeddings[0])) {
      return result.embeddings as number[][];
    }
    return [result.embeddings as number[]];
  },

  /**
   * Gets the number of embeddings in the result.
   */
  getEmbeddingCount(result: OllamaGenerateEmbeddingsResult): number {
    if (!result.embeddings) return 0;
    if (Array.isArray(result.embeddings[0])) {
      return (result.embeddings as number[][]).length;
    }
    return 1;
  },

  /**
   * Gets the dimension (length) of the embeddings.
   */
  getEmbeddingDimension(result: OllamaGenerateEmbeddingsResult): number | null {
    if (!result.embeddings) return null;
    if (Array.isArray(result.embeddings[0])) {
      const first = (result.embeddings as number[][])[0];
      return first?.length ?? null;
    }
    return (result.embeddings as number[]).length;
  },
};

/**
 * Ollama completion options.
 */
export interface OllamaCompletionOptions {
  /** Number of tokens to predict. */
  num_predict?: number;
  /** Temperature for sampling. */
  temperature?: number;
  /** Top-p sampling parameter. */
  top_p?: number;
  /** Top-k sampling parameter. */
  top_k?: number;
  /** Repeat penalty. */
  repeat_penalty?: number;
  /** Number of context tokens. */
  num_ctx?: number;
  /** Number of threads to use. */
  num_thread?: number;
  /** Stop sequences. */
  stop?: string[];
  /** Seed for random generation. */
  seed?: number;
}

/**
 * Ollama generate completion request.
 */
export interface OllamaGenerateCompletionRequest {
  /** Model name to use for generation. */
  model: string;
  /** The prompt to generate a response for. */
  prompt: string;
  /** Additional model parameters. */
  options?: OllamaCompletionOptions;
  /** System message to use. */
  system?: string;
  /** The full prompt or prompt template. */
  template?: string;
  /** The context from a previous request. */
  context?: number[];
  /** Enable streaming of generated text. */
  stream?: boolean;
  /** If false, the response will not include the prompt. */
  raw?: boolean;
  /** Format to return the response in. */
  format?: string;
  /** Base64-encoded images for multimodal models. */
  images?: string[];
  /** How long to keep the model loaded in memory. */
  keep_alive?: string;
}

/**
 * Ollama generate completion result.
 */
export interface OllamaGenerateCompletionResult {
  /** The model that generated the response. */
  model?: string;
  /** The timestamp of when the response was created. */
  created_at?: string;
  /** The generated text response. */
  response?: string;
  /** Context for maintaining conversation state. */
  context?: number[];
  /** Total duration in nanoseconds. */
  total_duration?: number;
  /** Model load duration in nanoseconds. */
  load_duration?: number;
  /** Number of tokens in the prompt. */
  prompt_eval_count?: number;
  /** Prompt evaluation duration in nanoseconds. */
  prompt_eval_duration?: number;
  /** Number of tokens generated in the response. */
  eval_count?: number;
  /** Response generation duration in nanoseconds. */
  eval_duration?: number;
}

/**
 * Helper functions for OllamaGenerateCompletionResult.
 */
export const OllamaGenerateCompletionResultHelpers = {
  /**
   * Gets the prompt evaluation rate in tokens per second.
   */
  getPromptTokensPerSecond(result: OllamaGenerateCompletionResult): number | null {
    if (
      result.prompt_eval_count !== undefined &&
      result.prompt_eval_duration !== undefined &&
      result.prompt_eval_duration > 0
    ) {
      return result.prompt_eval_count / (result.prompt_eval_duration / 1_000_000_000);
    }
    return null;
  },

  /**
   * Gets the response generation rate in tokens per second.
   */
  getGenerationTokensPerSecond(result: OllamaGenerateCompletionResult): number | null {
    if (
      result.eval_count !== undefined &&
      result.eval_duration !== undefined &&
      result.eval_duration > 0
    ) {
      return result.eval_count / (result.eval_duration / 1_000_000_000);
    }
    return null;
  },

  /**
   * Gets the total response time in milliseconds.
   */
  getTotalDurationMilliseconds(result: OllamaGenerateCompletionResult): number | null {
    if (result.total_duration !== undefined) {
      return result.total_duration / 1_000_000;
    }
    return null;
  },

  /**
   * Checks if context is available for continuing the conversation.
   */
  hasContext(result: OllamaGenerateCompletionResult): boolean {
    return result.context !== undefined && result.context.length > 0;
  },
};

/**
 * Ollama streaming completion result.
 */
export interface OllamaStreamingCompletionResult {
  /** The model that generated the response. */
  model?: string;
  /** The timestamp of when the response was created. */
  created_at?: string;
  /** The partial response text. */
  response?: string;
  /** Whether this is the final chunk in the stream. */
  done: boolean;
}

/**
 * Ollama chat message.
 */
export interface OllamaChatMessage {
  /** Role of the message sender. */
  role: 'system' | 'user' | 'assistant' | 'tool';
  /** Content of the message. */
  content: string;
  /** Base64-encoded images for multimodal models (only for user messages). */
  images?: string[];
  /** Tool calls made by the assistant (only for assistant messages). */
  tool_calls?: OllamaToolCall[];
}

/**
 * Ollama tool call.
 */
export interface OllamaToolCall {
  /** Function to call. */
  function: OllamaToolCallFunction;
}

/**
 * Ollama tool call function.
 */
export interface OllamaToolCallFunction {
  /** Name of the function. */
  name: string;
  /** Arguments to pass to the function. */
  arguments: Record<string, unknown>;
}

/**
 * Ollama tool definition.
 */
export interface OllamaTool {
  /** Type of tool (always "function"). */
  type: 'function';
  /** Function definition. */
  function: OllamaToolFunction;
}

/**
 * Ollama tool function definition.
 */
export interface OllamaToolFunction {
  /** Name of the function. */
  name: string;
  /** Description of what the function does. */
  description?: string;
  /** Parameters schema. */
  parameters?: OllamaToolParameters;
}

/**
 * Ollama tool parameters schema.
 */
export interface OllamaToolParameters {
  /** Type (always "object"). */
  type: 'object';
  /** Properties of the parameters. */
  properties?: Record<string, unknown>;
  /** Required parameter names. */
  required?: string[];
}

/**
 * Ollama generate chat completion request.
 */
export interface OllamaGenerateChatCompletionRequest {
  /** Model name to use for chat completion. */
  model: string;
  /** Messages for the chat. */
  messages: OllamaChatMessage[];
  /** Additional model parameters. */
  options?: OllamaCompletionOptions;
  /** Format to return the response in. */
  format?: string;
  /** The full prompt template. */
  template?: string;
  /** Enable streaming of generated text. */
  stream?: boolean;
  /** How long to keep the model loaded in memory. */
  keep_alive?: string;
  /** Tools/functions available for the model to use. */
  tools?: OllamaTool[];
}

/**
 * Ollama generate chat completion chunk (single chunk from streaming response).
 */
export interface OllamaGenerateChatCompletionChunk {
  /** The model that generated the response. */
  model?: string;
  /** The timestamp of when the response was created. */
  created_at?: string;
  /** The partial message generated by the model. */
  message?: OllamaChatMessage;
  /** Whether the response is complete. */
  done: boolean;
  /** Reason why the response is done. */
  done_reason?: string;
  /** Total duration in nanoseconds (only in final chunk). */
  total_duration?: number;
  /** Model load duration in nanoseconds (only in final chunk). */
  load_duration?: number;
  /** Number of tokens in the prompt (only in final chunk). */
  prompt_eval_count?: number;
  /** Prompt evaluation duration in nanoseconds (only in final chunk). */
  prompt_eval_duration?: number;
  /** Number of tokens generated (only in final chunk). */
  eval_count?: number;
  /** Response generation duration in nanoseconds (only in final chunk). */
  eval_duration?: number;
}

/**
 * Formats bytes into human-readable format.
 */
function formatBytes(bytes: number): string {
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
  let len = bytes;
  let order = 0;

  while (len >= 1024 && order < sizes.length - 1) {
    order++;
    len = len / 1024;
  }

  return `${len.toFixed(2)} ${sizes[order]}`;
}
