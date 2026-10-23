namespace SharpAI.Serve
{
    internal static class ServeUsage
    {
        internal const string Text = """
SharpAI - Serve one local GGUF model through familiar inference APIs

USAGE
  sharpai serve <model.gguf> [options]
  sharpai serve --help
  sharpai /?
  sharpai usage
  sharpai --version

SERVER
  --host <address>               IP address or localhost. Default: 127.0.0.1
  --port <number>                Listen port, 1-65535. Default: 8000
  --served-model-name <name>     API name. Default: filename without extension
  --api <ollama|openai|both>     Enabled API families. Default: both

INFERENCE
  --backend <auto|cpu|cuda|metal> Default: auto; explicit choices must load
  --context-size <tokens>       Default: model trained context, or 4096 if absent
  --max-tokens <tokens>         Default output budget: 2048 (includes reasoning)
  --threads <number>            Generation threads. Default: automatic
  --batch-threads <number>      Prompt threads. Default: automatic
  --gpu-layers <number|all>     Offload layers. Default: automatic
  --main-gpu <index>            CUDA device index. Default: 0
  --parallel <number>           Concurrent generation slots, 1-1024. Default: 1
  --temperature <number>        Default sampling temperature, 0-2. Default: 0.6
  --seed <number>               Default signed 32-bit sampling seed

FILES AND LOGGING
  --chat-template <path>        Override chat template; validated before serving
  --native-library <path>       Backend library override; requires --backend
                               CUDA requires external matching native binaries
  --log-file <path>             Optional log file; no file logging by default
  --log-level <level>           debug, info, warning, error. Default: debug
  --log-format <text|json>      Text or compact JSON per line. Default: text
  --log-detail <depth>          off, minimal, standard, detailed. Default: standard
  --quiet                       Suppress application console logs
  -h, --help, /?, usage         Show help

BEHAVIOR
  No authentication, dashboard, management APIs, config file, or database.
  One local model; no downloads, model switching, copying, or registry.
  Paths resolve relative to your working directory; installed tools run anywhere.
  Inference requests may override generation defaults; context limits still apply.
  The model loads before the listener starts and /ready succeeds.
  Ctrl+C or SIGTERM cancels requests and gracefully releases the model.
  Exit codes: 0 normal/help, 2 invalid arguments, 1 startup/runtime failure.

EXAMPLES
  sharpai serve ./models/assistant.gguf
  sharpai serve ./models/assistant.gguf --port 8080 --served-model-name assistant
  sharpai serve /models/assistant.gguf --backend cuda --gpu-layers all --parallel 4
  sharpai serve ./models/embeddings.gguf --api openai --served-model-name embeddings

INSTALL / UPDATE / REMOVE (published package feed)
  dotnet tool install --global SharpAI.Serve
  dotnet tool update --global SharpAI.Serve
  dotnet tool uninstall --global SharpAI.Serve
""";
    }
}
