// Centralized tooltip text for every label, column, and input in the dashboard.
// Keep these explanations user-friendly and concrete: say what the thing is and
// how it behaves, not just a restatement of the label.

// One-line (or short paragraph) description shown under each page title so a
// first-time visitor immediately knows what the page is for and how to use it.
export const pageDescriptions = {
  models:
    "Manage the local GGUF models available to SharpAI. Pull new models from HuggingFace, delete ones you no longer need, see which models are currently loaded into memory, and monitor active downloads.",
  embeddings:
    "Generate vector embeddings for one or more input strings. Pick an embedding-capable model, enter text, and choose between the Ollama (/api/embed) or OpenAI (/v1/embeddings) response format. Use these vectors for semantic search, clustering, or RAG indexing.",
  completions:
    "Send a raw prompt to a completion-capable model and stream its response. Unlike Chat Completion, there is no conversation template — the model continues your text directly. Use this for base models or when you want full control over the prompt format. Sampling parameters are in the settings sidebar.",
  chatCompletion:
    "Have a multi-turn conversation with a chat-tuned model. The dashboard automatically wraps your messages in the model's chat template (ChatML for Qwen, Llama2/3 format for Llama, etc.). Use this for instruction-following, Q&A, and assistant-style interaction.",
  configuration:
    "Edit the server's in-memory settings and write them back to sharpai.json. Some sections (REST server, Database) require a server restart to take effect — the header will flag them. Changes are saved only when you click Save.",
};

export const tooltips = {
  // -------- Models page: main Local Models table ----------
  models: {
    name: "The model name as stored by the server. This is the identifier you pass in API requests (e.g. via the 'model' field in /api/chat).",
    model: "The full model identifier. For HuggingFace pulls this is the organization/repo slug.",
    family:
      "Model architecture family detected from the GGUF file (e.g. llama, qwen2, mistral, bert). Drives which chat template is used for chat completions.",
    format:
      "Model file format. SharpAI currently supports GGUF, the quantized format used by llama.cpp.",
    size: "Size of the model file on disk.",
    quantization:
      "Quantization level of the weights (e.g. Q4_K_M, Q8_0). Lower bits = smaller file and faster inference, at some cost to accuracy.",
    modified:
      "Last-modified timestamp reported by the original HuggingFace repo when the model was pulled.",
    actions: "Delete this model. This removes the GGUF file from disk and the database record.",
  },

  // -------- Running Models table --------
  running: {
    heading:
      "Models currently loaded in memory. Models load lazily on first use and stay resident until the server is restarted.",
    name: "The model name as stored by the server.",
    family:
      "Model architecture detected from the GGUF file (llama, qwen2, bert, etc.).",
    quantization: "Quantization level of the loaded weights.",
    size: "Full size of the model file on disk.",
    vram:
      "Video memory currently occupied by this model. Zero when SharpAI is running on CPU — the model lives entirely in system RAM instead. Set Runtime.ForceBackend to 'cuda' or 'metal' in Configuration to use GPU.",
  },

  // -------- Pulling Models table --------
  pulling: {
    heading: "Model pulls currently in progress. You can navigate away and they continue in the background.",
    modelName: "The model being pulled from HuggingFace.",
    status:
      "Current stage of the pull: 'starting', 'pulling manifest', 'pulling <model>' (downloading), 'writing manifest', or 'success'.",
    progress:
      "Download progress as a percentage of the total GGUF file size.",
    size: "Bytes downloaded so far out of the total GGUF file size.",
    cancel: "Cancel the pull. The partial file is deleted.",
  },

  // -------- Pull Model modal --------
  pullModal: {
    modelName:
      "A HuggingFace model slug in 'organization/repo' form. The server queries HuggingFace for the repo's GGUF files and picks the preferred quantization. Example: 'Qwen/Qwen2.5-3B-Instruct-GGUF'.",
  },

  // -------- Landing page --------
  landing: {
    url: "Base URL of the SharpAI server to connect to. Defaults to http://localhost:8000. Change this if the server is running on a different host or port.",
  },

  // -------- Embeddings page --------
  embeddings: {
    model: "The embedding model to use. Only models whose GGUF metadata declares a pooling type (BERT-family architectures) appear here.",
    requestFormat:
      "API flavor: Ollama uses /api/embed, OpenAI uses /v1/embeddings. The response shape differs but the inputs are the same.",
    input:
      "Text to embed. Press Enter after each input to add another. The model returns one vector per input.",
    generate:
      "Send the request to the server and render the returned vectors below.",
    clear: "Clear the displayed embeddings.",
  },

  // -------- Completion / Chat Completion common --------
  completionsCommon: {
    model:
      "The completion-capable model to use. Embedding-only models are excluded.",
    clearChat: "Clear the conversation history from this page. Does not affect the server.",
    settings:
      "Show or hide the settings sidebar containing sampling, penalty, context, and stop parameters.",
    streamEnabled:
      "When on, tokens are delivered as they're generated (chunked transfer for Ollama, SSE for OpenAI). When off, the server waits until generation is complete and returns one response.",
    requestType:
      "API flavor. Ollama uses /api/generate and /api/chat. OpenAI uses /v1/completions and /v1/chat/completions.",
  },

  // -------- Chat settings sidebar (sampling + generation options) --------
  chatSettings: {
    numKeep:
      "Number of tokens to keep from the initial prompt when the context window fills up.",
    seed:
      "Random seed for sampling. Use a fixed value for reproducible outputs; change it for variation.",
    numPredict:
      "Maximum number of tokens to generate. The model may stop earlier if it produces an end-of-turn token. -1 means unlimited, -2 means fill the context window.",
    topK:
      "Sample only from the top-K most likely tokens. Smaller = more focused, larger = more diverse. Set to 0 to disable.",
    topP:
      "Nucleus sampling: sample from the smallest set of tokens whose cumulative probability >= top_p. Lower = more focused.",
    minP:
      "Minimum probability threshold relative to the top token. Filters out tokens below this relative probability floor.",
    tfsZ:
      "Tail-free sampling: removes low-probability tail of the distribution. 1.0 disables it, lower values make filtering more aggressive.",
    typicalP:
      "Locally typical sampling: keeps tokens whose log-probability is closest to the entropy of the distribution.",
    repeatLastN:
      "Number of recent tokens the repeat penalty considers. Larger values penalize repetition over a longer window.",
    temperature:
      "Sampling temperature. Lower (e.g. 0.2) = more deterministic and focused. Higher (e.g. 1.0+) = more diverse and creative.",
    repeatPenalty:
      "Penalty multiplier applied to tokens that have appeared in the recent window. Values above 1.0 discourage repetition.",
    presencePenalty:
      "Discourages the model from using any token it has already used in this response, regardless of frequency.",
    frequencyPenalty:
      "Discourages the model from overusing the same tokens by penalizing them in proportion to how often they've appeared.",
    mirostat:
      "Mirostat sampling mode: 0 = disabled, 1 = Mirostat v1, 2 = Mirostat v2. Alternative to top-p/top-k that targets a constant perplexity.",
    mirostatTau:
      "Target cross-entropy for Mirostat. Higher = more varied output.",
    mirostatEta:
      "Learning rate for Mirostat. Controls how fast the sampler adapts to hit the target entropy.",
    penalizeNewline:
      "When on, newlines are included in the repetition penalty. Turn off to allow the model to format responses with blank lines.",
    stop:
      "Stop sequences. Generation halts as soon as any of these strings appears in the output. Add chat template markers here (e.g. '<|im_end|>', '[/INST]', 'User:').",
    numa:
      "Enable NUMA support for multi-socket CPU systems. Usually leave off unless you're on a multi-socket server.",
    numCtx:
      "Context window size in tokens. Larger windows let the model see more of the prompt and prior conversation, at the cost of RAM and latency.",
    numBatch: "Batch size for prompt processing. Larger = faster prompt evaluation, more memory.",
    numGpu:
      "Number of model layers to offload to GPU. 0 = CPU only. Ignored if Runtime.ForceBackend is 'cpu'. Applies to both CUDA and Metal backends.",
    mainGpu: "Index of the primary GPU to use when multiple GPUs are available.",
    lowVram: "When on, uses a memory-saving code path suitable for GPUs with limited VRAM.",
    displayThinking:
      "When on, shows the model's internal reasoning tokens (e.g. <think>...</think> blocks from Qwen3 and similar models). When off, thinking tokens are stripped from the response.",
    f16Kv:
      "Store the key/value cache in 16-bit floats instead of 32-bit. Halves the KV cache memory with minimal accuracy loss.",
    vocabOnly:
      "Load only the tokenizer and vocab, not the weights. Only useful for tokenization, cannot generate.",
    useMmap:
      "Memory-map the model file instead of copying it into RAM. Usually leave on for faster load and lower memory pressure.",
    useMlock:
      "Lock the model in physical RAM so the OS never swaps it out. Requires sufficient available memory.",
    numThread:
      "Number of CPU threads to use for generation. Default is reasonable; raising this above your physical core count usually hurts.",
    resetDefaults: "Reset all generation options back to their defaults.",
  },

  // -------- Configuration page --------
  config: {
    save: "Save the current values. In-memory settings are replaced and sharpai.json is rewritten.",
    reset: "Discard pending changes and reload the current saved values from the server.",

    // Logging
    loggingDirectory:
      "Directory where log files are written. Created automatically if it does not exist.",
    loggingFilename:
      "Base filename for the log file inside the log directory. The date is appended automatically.",
    loggingSeverity:
      "Minimum severity to log. 0=Debug, 1=Info, 2=Warning, 3=Error, 4=Alert, 5=Critical, 6=Emergency, 7=Notice.",
    loggingConsole:
      "When on, log lines are also written to the server's console.",
    loggingColors:
      "When on, console log lines include ANSI color codes for severity.",
    syslogServers:
      "List of syslog servers to forward log messages to. Each entry specifies a hostname and UDP port.",
    syslogHostname: "Syslog server hostname or IP address.",
    syslogPort:
      "Syslog server UDP port. Standard syslog is 514.",
    syslogRandomizePorts:
      "When on, the server picks a random port in the min/max range for outbound syslog. Usually off.",
    syslogMinPort: "Lower bound for randomized source port selection.",
    syslogMaxPort: "Upper bound for randomized source port selection.",

    // Storage
    tempDirectory:
      "Scratch space for transient files (partial downloads, etc.).",
    modelsDirectory:
      "Directory where downloaded GGUF model files are stored. Each model is saved under its assigned GUID.",

    // Database
    dbFilename:
      "Path to the SQLite database file used for model metadata.",
    dbType:
      "Database backend. SharpAI currently stores metadata in SQLite; other options are reserved for future use.",
    dbPort:
      "Database server port. Not used for SQLite (which is file-based).",
    dbRequireEncryption:
      "Require an encrypted connection to the database server. SQLite ignores this.",
    dbDebugQueries:
      "Log every SQL query the server executes. Noisy — use for debugging only.",
    dbDebugResults:
      "Log every SQL result row. Extremely noisy.",

    // HuggingFace
    hfApiKey:
      "HuggingFace API token. Required to pull gated or rate-limited models. Get one from https://huggingface.co/settings/tokens.",

    // REST
    restHostname:
      "Host the web server binds to. Use '*' or '+' to bind all interfaces, '127.0.0.1' for loopback only. Restart required.",
    restPort:
      "TCP port the web server listens on. Restart required.",
    restStreamBuffer:
      "Size of the internal streaming buffer in bytes. Affects streamed responses like completions and model pulls.",
    restMaxRequests:
      "Maximum number of concurrent HTTP connections before new connections are queued.",
    restReadTimeout:
      "Maximum time in milliseconds the server waits for a client to finish sending a request before closing the connection.",
    restMaxHeaders:
      "Maximum total size in bytes of all inbound request headers.",
    restKeepAlive:
      "When on, HTTP/1.1 keep-alive is honored so clients can reuse the same TCP connection for multiple requests.",
    restSslEnable:
      "Enable TLS. Requires a certificate configured below. Restart required.",
    restSslMutual:
      "Require the client to present a valid certificate (mutual TLS / mTLS).",
    restSslAcceptInvalid:
      "Accept self-signed or otherwise invalid client certificates during development.",
    restSslPfxFile:
      "Path to a PFX/PKCS#12 certificate file used for TLS.",
    restSslPfxPassword:
      "Password that unlocks the PFX certificate file.",
    restDebugAccessControl:
      "Log every access-control decision (permit/deny) made by the server.",
    restDebugRouting:
      "Log every route match attempt. Use to troubleshoot 404s.",
    restDebugRequests:
      "Log every incoming request line, headers, and body.",
    restDebugResponses:
      "Log every outgoing response status line, headers, and body.",

    // Runtime
    runtimeForceBackend:
      "Force a specific inference backend. 'Auto' picks GPU if CUDA or Metal is available, else CPU. 'CPU' disables GPU even on CUDA/Metal machines. 'CUDA' requires NVIDIA CUDA runtime. 'Metal' uses Apple Silicon GPU (macOS ARM64 only).",
    runtimeCpuBackendPath:
      "Absolute path to the CPU native library (llama.dll/libllama.so). Leave blank to use the bundled default.",
    runtimeGpuBackendPath:
      "Absolute path to the CUDA native library. Leave blank to use the bundled default.",
    runtimeMetalBackendPath:
      "Absolute path to the Metal native library (libllama.dylib on macOS ARM64). Leave blank to use the bundled default.",
    runtimeNativeLogging:
      "When on, log output from the underlying llama.cpp native library is forwarded to the server log.",

    // Debug
    debugRequestBody:
      "When on, the raw body of every non-chunked request is written to the log. Noisy and may contain sensitive data.",

    // Quantization priority
    quantizationPriority:
      "Custom ordering for picking a GGUF file from a HuggingFace repo when multiple quantizations are available. Higher numbers are preferred. Leave empty to use Ollama's default ordering (Q4_K_M first).",
    quantizationKey:
      "Quantization type, e.g. 'Q4_K_M', 'Q5_K_S', 'Q8_0'.",
    quantizationValue:
      "Priority for this quantization. Higher wins.",
  },
};
