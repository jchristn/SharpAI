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

export interface LocalModelCapabilities {
  embeddings: boolean;
  completions: boolean;
}

export interface LocalModel {
  name: string;
  model: string;
  modified_at: string;
  size: number;
  digest: string;
  details: LocalModelDetails;
  capabilities?: LocalModelCapabilities;
}

export interface RunningModel {
  name: string;
  digest: string;
  size: number;
  size_vram: number;
  expires_at?: string | null;
  details: LocalModelDetails;
}

export interface RunningModelsResponse {
  models: RunningModel[];
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
// Settings types

export interface SyslogServer {
  Hostname: string;
  Port: number;
  RandomizePorts: boolean;
  MinimumPort: number;
  MaximumPort: number;
}

export interface LoggingSettings {
  Servers: SyslogServer[];
  LogDirectory: string;
  LogFilename: string;
  ConsoleLogging: boolean;
  EnableColors: boolean;
  MinimumSeverity: number;
}

export interface StorageSettings {
  TempDirectory: string;
  ModelsDirectory: string;
}

export interface DatabaseDebugSettings {
  EnableForQueries: boolean;
  EnableForResults: boolean;
}

export interface DatabaseSettings {
  Filename: string;
  Type: string;
  Port: number;
  RequireEncryption: boolean;
  Debug: DatabaseDebugSettings;
}

export interface HuggingFaceSettings {
  ApiKey: string;
}

export interface RestIoSettings {
  StreamBufferSize: number;
  MaxRequests: number;
  ReadTimeoutMs: number;
  MaxIncomingHeadersSize: number;
  EnableKeepAlive: boolean;
}

export interface RestSslSettings {
  Enable: boolean;
  PfxCertificateFile: string | null;
  PfxCertificatePassword: string | null;
  MutuallyAuthenticate: boolean;
  AcceptInvalidAcertificates: boolean;
}

export interface RestHeadersSettings {
  IncludeContentLength: boolean;
  DefaultHeaders: Record<string, string>;
}

export interface RestAccessControlSettings {
  DenyList: Record<string, unknown>;
  PermitList: Record<string, unknown>;
  Mode: string;
}

export interface RestDebugSettings {
  AccessControl: boolean;
  Routing: boolean;
  Requests: boolean;
  Responses: boolean;
}

export interface RestSettings {
  Hostname: string;
  Port: number;
  IO: RestIoSettings;
  Ssl: RestSslSettings;
  Headers: RestHeadersSettings;
  AccessControl: RestAccessControlSettings;
  Debug: RestDebugSettings;
}

export interface RuntimeSettings {
  ForceBackend: string | null;
  CpuBackendPath: string | null;
  GpuBackendPath: string | null;
  EnableNativeLogging: boolean;
}

export interface DebugSettings {
  RequestBody: boolean;
}

export interface SharpAISettings {
  CreatedUtc: string;
  SoftwareVersion: string;
  Logging: LoggingSettings;
  Storage: StorageSettings;
  Database: DatabaseSettings;
  HuggingFace: HuggingFaceSettings;
  Rest: RestSettings;
  Runtime: RuntimeSettings;
  Debug: DebugSettings;
  QuantizationPriority: Record<string, number>;
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
