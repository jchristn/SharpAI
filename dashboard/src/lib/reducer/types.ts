export enum SliceTags {
  SHARP = "sharp",
}

export interface LocalModelDetails {
  parent_model: string;
  format: string;
  family: string;
  families: string[];
  parameter_size: string;
  quantization_level: string;
}

export interface LocalModel {
  name: string;
  model: string;
  modified_at: string;
  size: number;
  digest: string;
  details: LocalModelDetails;
}

export interface GenerateEmbeddingsPaylaod {
  model: string;
  input: string | string[];
}
export interface GenerateEmbeddingsResponse {
  model: string;
  embeddings: number[][];
}

export interface CompletionsOptions {
  f16_kv: boolean;
  frequency_penalty: number;
  low_vram: boolean;
  main_gpu: number;
  min_p: number;
  mirostat: number;
  mirostat_eta: number;
  mirostat_tau: number;
  num_batch: number;
  num_ctx: number;
  num_gpu: number;
  num_keep: number;
  num_predict: number;
  num_thread: number;
  numa: boolean;
  penalize_newline: boolean;
  presence_penalty: number;
  repeat_last_n: number;
  repeat_penalty: number;
  seed: number;
  stop: string[];
  temperature: number;
  tfs_z: number;
  top_k: number;
  top_p: number;
  typical_p: number;
  use_mlock: boolean;
  use_mmap: boolean;
  vocab_only: boolean;
}

export type ChatCompletionsOptions = Omit<CompletionsOptions, "stop">;

export interface ChatCompletionsMessagePaylaod {
  role: string;
  content: string;
}

export interface ChatCompletionsOpenAIMessagePaylaod {
  role: string;
  content: string;
  name: string;
}

export interface ChatCompletionsPaylaod {
  model: string;
  stream: boolean;
  messages: ChatCompletionsMessagePaylaod[];
  options: ChatCompletionsOptions;
}

export interface CompletionsPaylaod {
  model: string;
  prompt: string;
  stream: boolean;
  options: CompletionsOptions;
}

export interface CompletionsResponse {
  model: string;
  created_at: string;
  response: string;
  done: boolean;
  done_reason?: string;
}

export interface GenerateEmbeddingsOpenAIResponse {
  object: string;
  data: GenerateEmbeddingsOpenAIResponseData[];
  model: string;
}

export interface GenerateEmbeddingsOpenAIResponseData {
  object: string;
  index: number;
  embedding: number[];
}

export interface CompletionsOpenAIPayload {
  model: string;
  prompt: string;
  max_tokens: number;
  temperature: number;
  top_p: number;
  n: number;
  stream: boolean;
  presence_penalty: number;
  frequency_penalty: number;
  stop: string[];
}

export interface ChatCompletionsOpenAIPayload {
  model: string;
  messages: ChatCompletionsOpenAIMessagePaylaod[];
  temperature: number;
  top_p: number;
  n: number;
  stream: boolean;
  stop: string[];
  max_tokens: number;
  presence_penalty: number;
  frequency_penalty: number;
  user: string;
  seed: number;
}
export interface CompletionsOpenAIResponse {
  id: string;
  object: string;
  created: number;
  model: string;
  choices: Choice[];
}

export interface Choice {
  text: string;
  index: number;
}
