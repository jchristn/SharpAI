/**
 * OpenAI generate embeddings request.
 */
export interface OpenAIGenerateEmbeddingsRequest {
  /** ID of the model to use. */
  model: string;
  /** Input text to embed. Can be a string or array of strings. */
  input: string | string[];
  /** The format to return the embeddings in. */
  encoding_format?: 'float' | 'base64';
  /** The number of dimensions for the output embeddings. */
  dimensions?: number;
  /** A unique identifier representing your end-user. */
  user?: string;
}

/**
 * OpenAI embedding object.
 */
export interface OpenAIEmbedding {
  /** Object type (always "embedding"). */
  object: string;
  /** The embedding vector. */
  embedding: number[];
  /** The index of the embedding in the list. */
  index: number;
}

/**
 * OpenAI embedding usage statistics.
 */
export interface OpenAIEmbeddingUsage {
  /** Number of tokens in the prompt. */
  prompt_tokens: number;
  /** Total tokens used. */
  total_tokens: number;
}

/**
 * OpenAI generate embeddings result.
 */
export interface OpenAIGenerateEmbeddingsResult {
  /** Object type (always "list"). */
  object: string;
  /** List of embedding objects. */
  data: OpenAIEmbedding[];
  /** Model used to generate embeddings. */
  model: string;
  /** Usage statistics. */
  usage: OpenAIEmbeddingUsage;
}

/**
 * Helper functions for OpenAIGenerateEmbeddingsResult.
 */
export const OpenAIGenerateEmbeddingsResultHelpers = {
  /**
   * Gets a single embedding array.
   */
  getEmbedding(result: OpenAIGenerateEmbeddingsResult): number[] | null {
    if (!result.data || result.data.length === 0) return null;
    if (result.data.length > 1) {
      throw new Error(
        `Result contains ${result.data.length} embeddings. Use getEmbeddings() instead.`
      );
    }
    return result.data[0]?.embedding ?? null;
  },

  /**
   * Gets all embeddings as a list of arrays.
   */
  getEmbeddings(result: OpenAIGenerateEmbeddingsResult): number[][] {
    if (!result.data || result.data.length === 0) return [];
    return result.data
      .sort((a, b) => a.index - b.index)
      .map((e) => e.embedding);
  },

  /**
   * Gets the number of embeddings in the result.
   */
  getEmbeddingCount(result: OpenAIGenerateEmbeddingsResult): number {
    return result.data?.length ?? 0;
  },

  /**
   * Gets the dimension of the embeddings.
   */
  getEmbeddingDimension(result: OpenAIGenerateEmbeddingsResult): number | null {
    if (!result.data || result.data.length === 0) return null;
    return result.data[0]?.embedding?.length ?? null;
  },
};

/**
 * OpenAI generate completion request.
 */
export interface OpenAIGenerateCompletionRequest {
  /** ID of the model to use. */
  model: string;
  /** The prompt(s) to generate completions for. */
  prompt: string | string[];
  /** The suffix that comes after a completion. */
  suffix?: string;
  /** The maximum number of tokens to generate. */
  max_tokens?: number;
  /** Temperature for sampling (0-2). */
  temperature?: number;
  /** Nucleus sampling parameter (0-1). */
  top_p?: number;
  /** Number of completions to generate. */
  n?: number;
  /** Whether to stream partial progress. */
  stream?: boolean;
  /** Include log probabilities. */
  logprobs?: number;
  /** Echo back the prompt. */
  echo?: boolean;
  /** Stop sequences (up to 4). */
  stop?: string | string[];
  /** Presence penalty (-2.0 to 2.0). */
  presence_penalty?: number;
  /** Frequency penalty (-2.0 to 2.0). */
  frequency_penalty?: number;
  /** Generates best_of completions and returns the best. */
  best_of?: number;
  /** Modify the likelihood of specified tokens. */
  logit_bias?: Record<string, number>;
  /** A unique identifier representing your end-user. */
  user?: string;
  /** Random seed for deterministic generation. */
  seed?: number;
}

/**
 * OpenAI completion choice.
 */
export interface OpenAICompletionChoice {
  /** Generated text. */
  text: string;
  /** Index of the choice. */
  index: number;
  /** Log probabilities. */
  logprobs?: OpenAILogprobs | null;
  /** Reason for completion finish. */
  finish_reason: string | null;
}

/**
 * OpenAI log probabilities.
 */
export interface OpenAILogprobs {
  /** Tokens. */
  tokens?: string[];
  /** Token log probabilities. */
  token_logprobs?: number[];
  /** Top log probabilities. */
  top_logprobs?: Record<string, number>[];
  /** Text offsets. */
  text_offset?: number[];
}

/**
 * OpenAI usage statistics.
 */
export interface OpenAIUsage {
  /** Number of tokens in the prompt. */
  prompt_tokens: number;
  /** Number of tokens in the completion. */
  completion_tokens: number;
  /** Total tokens used. */
  total_tokens: number;
}

/**
 * OpenAI generate completion result.
 */
export interface OpenAIGenerateCompletionResult {
  /** Unique identifier for the completion. */
  id: string;
  /** Object type (always "text_completion"). */
  object: string;
  /** Unix timestamp when created. */
  created: number;
  /** Model used for the completion. */
  model: string;
  /** System fingerprint. */
  system_fingerprint?: string;
  /** List of completion choices. */
  choices: OpenAICompletionChoice[];
  /** Usage statistics. */
  usage: OpenAIUsage;
}

/**
 * Helper functions for OpenAIGenerateCompletionResult.
 */
export const OpenAIGenerateCompletionResultHelpers = {
  /**
   * Gets the created timestamp as Date.
   */
  getCreatedDate(result: OpenAIGenerateCompletionResult): Date | null {
    if (!result.created) return null;
    return new Date(result.created * 1000);
  },

  /**
   * Gets the primary completion text.
   */
  getCompletionText(result: OpenAIGenerateCompletionResult): string | null {
    return result.choices?.[0]?.text ?? null;
  },

  /**
   * Checks if any choice was truncated due to length.
   */
  hasTruncatedChoices(result: OpenAIGenerateCompletionResult): boolean {
    return result.choices?.some((c) => c.finish_reason === 'length') ?? false;
  },
};

/**
 * OpenAI chat message.
 */
export interface OpenAIChatMessage {
  /** Role of the message sender. */
  role: 'system' | 'user' | 'assistant' | 'tool';
  /** Content of the message. */
  content: string | null;
  /** Name of the author (for tool messages). */
  name?: string;
  /** Tool calls made by the assistant. */
  tool_calls?: OpenAIToolCall[];
  /** ID of the tool call (for tool messages). */
  tool_call_id?: string;
}

/**
 * OpenAI tool call.
 */
export interface OpenAIToolCall {
  /** The ID of the tool call. */
  id: string;
  /** Type of tool (always "function"). */
  type: 'function';
  /** The function that was called. */
  function: OpenAIToolCallFunction;
}

/**
 * OpenAI tool call function.
 */
export interface OpenAIToolCallFunction {
  /** Name of the function. */
  name: string;
  /** Arguments as a JSON string. */
  arguments: string;
}

/**
 * OpenAI tool definition.
 */
export interface OpenAITool {
  /** Type of tool (always "function"). */
  type: 'function';
  /** Function definition. */
  function: OpenAIToolFunction;
}

/**
 * OpenAI tool function definition.
 */
export interface OpenAIToolFunction {
  /** Name of the function. */
  name: string;
  /** Description of what the function does. */
  description?: string;
  /** Parameters schema (JSON Schema object). */
  parameters?: Record<string, unknown>;
}

/**
 * OpenAI response format.
 */
export interface OpenAIResponseFormat {
  /** Type of response format. */
  type: 'text' | 'json_object';
}

/**
 * OpenAI generate chat completion request.
 */
export interface OpenAIGenerateChatCompletionRequest {
  /** ID of the model to use. */
  model: string;
  /** Messages for the chat conversation. */
  messages: OpenAIChatMessage[];
  /** Response format specification. */
  response_format?: OpenAIResponseFormat;
  /** The maximum number of tokens to generate. */
  max_tokens?: number;
  /** Temperature for sampling (0-2). */
  temperature?: number;
  /** Nucleus sampling parameter (0-1). */
  top_p?: number;
  /** Number of completions to generate. */
  n?: number;
  /** Whether to stream partial progress. */
  stream?: boolean;
  /** Stop sequences (up to 4). */
  stop?: string | string[];
  /** Presence penalty (-2.0 to 2.0). */
  presence_penalty?: number;
  /** Frequency penalty (-2.0 to 2.0). */
  frequency_penalty?: number;
  /** Modify the likelihood of specified tokens. */
  logit_bias?: Record<string, number>;
  /** Include log probabilities. */
  logprobs?: boolean;
  /** Number of most likely tokens to return. */
  top_logprobs?: number;
  /** A unique identifier representing your end-user. */
  user?: string;
  /** Functions/tools the model may call. */
  tools?: OpenAITool[];
  /** Controls which tool is called by the model. */
  tool_choice?: 'none' | 'auto' | 'required' | { type: 'function'; function: { name: string } };
  /** Whether to enable parallel function calling. */
  parallel_tool_calls?: boolean;
  /** Random seed for deterministic generation. */
  seed?: number;
}

/**
 * OpenAI chat choice.
 */
export interface OpenAIChatChoice {
  /** Index of the choice. */
  index: number;
  /** The chat message. */
  message: OpenAIChatMessage;
  /** Log probabilities. */
  logprobs?: OpenAIChatLogprobs | null;
  /** Reason for completion finish. */
  finish_reason: string | null;
}

/**
 * OpenAI chat log probabilities.
 */
export interface OpenAIChatLogprobs {
  /** Content log probabilities. */
  content?: OpenAIChatLogprobContent[];
}

/**
 * OpenAI chat log probability content.
 */
export interface OpenAIChatLogprobContent {
  /** Token. */
  token: string;
  /** Log probability. */
  logprob: number;
  /** Bytes. */
  bytes?: number[];
  /** Top log probabilities. */
  top_logprobs?: OpenAITopLogprob[];
}

/**
 * OpenAI top log probability.
 */
export interface OpenAITopLogprob {
  /** Token. */
  token: string;
  /** Log probability. */
  logprob: number;
  /** Bytes. */
  bytes?: number[];
}

/**
 * OpenAI generate chat completion result.
 */
export interface OpenAIGenerateChatCompletionResult {
  /** Unique identifier for the chat completion. */
  id: string;
  /** Object type (always "chat.completion"). */
  object: string;
  /** Unix timestamp when created. */
  created: number;
  /** Model used for the completion. */
  model: string;
  /** System fingerprint. */
  system_fingerprint?: string;
  /** List of chat completion choices. */
  choices: OpenAIChatChoice[];
  /** Usage statistics. */
  usage: OpenAIUsage;
}

/**
 * Helper functions for OpenAIGenerateChatCompletionResult.
 */
export const OpenAIGenerateChatCompletionResultHelpers = {
  /**
   * Gets the created timestamp as Date.
   */
  getCreatedDate(result: OpenAIGenerateChatCompletionResult): Date | null {
    if (!result.created) return null;
    return new Date(result.created * 1000);
  },

  /**
   * Gets the primary message from the assistant.
   */
  getAssistantMessage(result: OpenAIGenerateChatCompletionResult): OpenAIChatMessage | null {
    return result.choices?.[0]?.message ?? null;
  },

  /**
   * Checks if any choice contains tool calls.
   */
  hasToolCalls(result: OpenAIGenerateChatCompletionResult): boolean {
    return (
      result.choices?.some(
        (c) => c.message?.tool_calls && c.message.tool_calls.length > 0
      ) ?? false
    );
  },

  /**
   * Gets all tool calls from all choices.
   */
  getAllToolCalls(result: OpenAIGenerateChatCompletionResult): OpenAIToolCall[] {
    return (
      result.choices
        ?.filter((c) => c.message?.tool_calls)
        .flatMap((c) => c.message.tool_calls ?? []) ?? []
    );
  },
};

/**
 * OpenAI streaming completion result.
 */
export interface OpenAIStreamingCompletionResult {
  /** Unique identifier for the completion. */
  id: string;
  /** Object type. */
  object: string;
  /** Unix timestamp when created. */
  created: number;
  /** Model used for the completion. */
  model: string;
  /** List of completion choices. */
  choices: OpenAICompletionChoice[];
}
