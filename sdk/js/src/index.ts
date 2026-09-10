// Main SDK class
export { SharpAISdk, type LoggerCallback, type SharpAIAuthOptions } from './SharpAISdk';

// Interfaces
export type { IOllamaMethods } from './interfaces/IOllamaMethods';
export type { IOpenAIMethods } from './interfaces/IOpenAIMethods';
export type {
  IAdminMethods,
  QueryParams,
  LoginResponse,
  CreateUserRequest,
  CreateAssignmentRequest,
  CreatedCredential,
} from './interfaces/IAdminMethods';

// Ollama models and helpers
export type {
  OllamaPullModelRequest,
  SharpAIPullModelResponse,
  OllamaDeleteModelRequest,
  OllamaLocalModel,
  OllamaModelDetails,
  OllamaGenerateEmbeddingsRequest,
  OllamaEmbeddingsOptions,
  OllamaGenerateEmbeddingsResult,
  OllamaCompletionOptions,
  OllamaGenerateCompletionRequest,
  OllamaGenerateCompletionResult,
  OllamaStreamingCompletionResult,
  OllamaChatMessage,
  OllamaToolCall,
  OllamaToolCallFunction,
  OllamaTool,
  OllamaToolFunction,
  OllamaToolParameters,
  OllamaGenerateChatCompletionRequest,
  OllamaGenerateChatCompletionChunk,
} from './models/ollama';

export {
  SharpAIPullModelResponseHelpers,
  OllamaLocalModelHelpers,
  OllamaGenerateEmbeddingsResultHelpers,
  OllamaGenerateCompletionResultHelpers,
} from './models/ollama';

// OpenAI models and helpers
export type {
  OpenAIGenerateEmbeddingsRequest,
  OpenAIEmbedding,
  OpenAIEmbeddingUsage,
  OpenAIGenerateEmbeddingsResult,
  OpenAIGenerateCompletionRequest,
  OpenAICompletionChoice,
  OpenAILogprobs,
  OpenAIUsage,
  OpenAIGenerateCompletionResult,
  OpenAIChatMessage,
  OpenAIToolCall,
  OpenAIToolCallFunction,
  OpenAITool,
  OpenAIToolFunction,
  OpenAIResponseFormat,
  OpenAIGenerateChatCompletionRequest,
  OpenAIChatChoice,
  OpenAIChatLogprobs,
  OpenAIChatLogprobContent,
  OpenAITopLogprob,
  OpenAIGenerateChatCompletionResult,
  OpenAIStreamingCompletionResult,
} from './models/openai';

export {
  OpenAIGenerateEmbeddingsResultHelpers,
  OpenAIGenerateCompletionResultHelpers,
  OpenAIGenerateChatCompletionResultHelpers,
} from './models/openai';
