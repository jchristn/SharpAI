<div align="center">
  <img src="https://github.com/jchristn/sharpai/blob/main/assets/logo.png" width="256" height="256">
</div>

# SharpAI

**Transform your .NET applications into AI powerhouses - embed models directly or deploy as an Ollama-compatible and OpenAI-compatible API server. No cloud dependencies, no limits, just local embeddings and inference.**

<p align="center">
  <img src="https://img.shields.io/badge/.NET-5C2D91?style=for-the-badge&logo=.net&logoColor=white" />
  <img src="https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white" />
  <img src="https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge" />
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/SharpAI/">
    <img src="https://img.shields.io/nuget/v/SharpAI.svg?style=flat" alt="NuGet Version">
  </a>
  &nbsp;
  <a href="https://www.nuget.org/packages/SharpAI">
    <img src="https://img.shields.io/nuget/dt/SharpAI.svg" alt="NuGet Downloads">
  </a>
</p>

<p align="center">
  <strong>A .NET library for local AI model inference with Ollama-compatible and OpenAI-compatible REST APIs</strong>
</p>

<p align="center">
  Embeddings • Completions • Chat • Built on LlamaSharp • GGUF Models Only
</p>

---

## What's new in 2.0

Version 2.0 is a unified, enterprise-focused release across the library, server, dashboard, and SDKs. The
highlights:

- **Correct prompts by default.** Chat requests render through each model's own GGUF-embedded chat
  template when present, falling back to the built-in formatter otherwise.
- **A rebuilt dashboard.** A React 19 single-page app (no Ant Design, no axios) covering overview, models,
  an inference playground, request history with an activity chart, an API explorer, observability, and
  settings — with light/dark theming, sign-in, and internationalization (English, Arabic/RTL).
- **Authentication, authorization, and accounting — off by default.** Multi-tenant users, credentials,
  and sessions; an RBAC engine with explicit-deny-wins and built-in roles; a security audit trail; and a
  full account-management REST surface. When disabled, the server stays an open local Ollama.
- **Four databases.** A provider-neutral, hand-written data layer over SQLite (default), MySQL,
  PostgreSQL, and SQL Server with versioned migrations.
- **Observability.** Prometheus `/metrics`, OTLP metrics/traces/logs, and a Docker Compose stack
  (Prometheus, Loki, Tempo, Grafana with provisioned dashboards).
- **Request history.** Every request is captured and queryable through the API and dashboard.
- **SDKs at parity.** The C#, JavaScript/TypeScript, and Python clients all cover the Ollama/OpenAI APIs
  plus authentication, request history, and account management.

The task-by-task plan and status live in [IMPROVEMENTS.md](archive/IMPROVEMENTS.md); changes are logged in
[CHANGELOG.md](CHANGELOG.md).

---

## 📁 Monorepo Structure

SharpAI is organized as a monorepo containing the core library, server, dashboard, and client SDKs:

```
SharpAI/
├── src/                    # Core .NET library and server
│   ├── SharpAI/            # Core library (NuGet: SharpAI)
│   ├── SharpAI.Server/     # REST API server (Watson 7 + OpenAPI/Swagger)
│   └── Test.*/             # Test projects
├── dashboard/              # Vite + React + Ant Design web interface
├── sdk/
│   ├── csharp/             # C# SDK (NuGet: SharpAI.Sdk)
│   ├── python/             # Python SDK (coming soon)
│   └── js/                 # TypeScript/JavaScript SDK (npm: @sharpai/sdk)
├── docker/                 # Docker assets
└── README.md
```

### Sub-Projects

| Project | Description | Documentation |
|---------|-------------|---------------|
| **SharpAI** | Core .NET library for local AI inference | This README |
| **SharpAI.Server** | Ollama & OpenAI compatible REST API server on Watson 7 with built-in OpenAPI/Swagger | This README |
| **Dashboard** | Vite + React web interface for managing models, running inference, and editing settings | [dashboard/README.md](dashboard/README.md) |
| **C# SDK** | SDK for .NET applications to connect to SharpAI server | [sdk/csharp/README.md](sdk/csharp/README.md) |
| **TypeScript SDK** | SDK for Node.js/browser applications | [sdk/js/README.md](sdk/js/README.md) |
| **Python SDK** | SDK for Python applications | [sdk/python/README.md](sdk/python/README.md) |

---

## 🚀 Features

- **Ollama and OpenAI Compatible REST API Server** — Provides endpoints compatible with API from Ollama and OpenAI
- **Built-in OpenAPI / Swagger documentation** — Every REST route is documented with tags, summaries, request and response schemas; the server exposes `/openapi.json` and a live `/swagger` UI at startup
- **Settings API** — `GET /api/settings` returns the live in-memory configuration, `PUT /api/settings` replaces it and rewrites `sharpai.json` on disk (preserving `CreatedUtc` and `SoftwareVersion`)
- **Model Management** — Download and manage GGUF models from HuggingFace using Ollama APIs
- **Automatic Capability Detection** — Each pulled model's `general.architecture` and `general.pooling_type` GGUF metadata determines whether it supports embeddings, completions, or both, and drives the correct chat template selection
- **Multiple Inference Types**:
  - Text embeddings generation
  - Text completions
  - Chat completions
- **Prompt Engineering Tools** — Built-in helpers for formatting prompts for different model types
- **GPU Acceleration** — Automatic CUDA detection (Windows/Linux) and Metal acceleration (macOS Apple Silicon)
- **Streaming Support** — Real-time token streaming for completions with proper stop-sequence handling
- **SQLite Model Registry** — Tracks model metadata and file information
- **Web Dashboard** — Vite + React + Ant Design UI for pulling models, generating embeddings, running completions and chat, inspecting running models, and editing server configuration live

See [inference operations and verification](docs/OPERATIONS.md) for verified downloads, usage fields,
context preflight, memory admission, diagnostics, benchmark receipts, and the integration test harness.

## 📋 Table of Contents

- [Installation](#-installation)
- [Core Components](#-core-components)
- [Model Management](#-model-management)
- [Generating Embeddings](#-generating-embeddings)
- [Text Completions](#-text-completions)
- [Chat Completions](#-chat-completions)
- [Prompt Formatting](#-prompt-formatting)
- [API Server](#-api-server)
- [Requirements](#-requirements)
- [Version History](#-version-history)
- [License](#-license)
- [Acknowledgments](#-acknowledgments)

## 📦 Installation

Install SharpAI via NuGet:

```bash
dotnet add package SharpAI
```

Or via Package Manager Console:

```powershell
Install-Package SharpAI
```

## 📖 Core Components

### AIDriver

The main entry point that provides access to all functionality:

```csharp
using SharpAI;
using SyslogLogging;

// Initialize the AI driver
AIDriver ai = new AIDriver(
    logging: new LoggingModule(), 
    databaseFilename: "./sharpai.db",     
    huggingFaceApiKey: "hf_xxxxxxxxxxxx", 
    modelDirectory: "./models/"           
);

// Download a model from HuggingFace (GGUF format only)
await ai.Models.Add(
    name: "QuantFactory/Qwen2.5-3B-GGUF",
    quantizationPriority: null,
    progressCallback: (url, bytesDownloaded, percentComplete) =>
    {
        Console.WriteLine($"Progress: {percentComplete:P0}");
    });

// Generate a completion
string response = await ai.Completion.GenerateCompletion(
    model: "QuantFactory/Qwen2.5-3B-GGUF",
    prompt: "Once upon a time",
    maxTokens: 512,
    temperature: 0.7f
);
```

The AIDriver provides access to APIs via:
- `ai.Models` - Model management operations
- `ai.Embeddings` - Embedding generation
- `ai.Completion` - Text completion generation
- `ai.Chat` - Chat completion generation

### ModelDriver

Manages model downloads and lifecycle:

```csharp
// List all downloaded models
List<ModelFile> models = ai.Models.All();

// Get a specific model
ModelFile model = ai.Models.GetByName("QuantFactory/Qwen2.5-3B-GGUF");

// Download a new model from HuggingFace (GGUF format only)
ModelFile downloaded = await ai.Models.Add(
    name: "leliuga/all-MiniLM-L6-v2-GGUF",
    quantizationPriority: null,
    progressCallback: null);

// Delete a model
ai.Models.Delete("QuantFactory/Qwen2.5-3B-GGUF");

// Get the filesystem path for a model
string modelPath = ai.Models.GetFilename("QuantFactory/Qwen2.5-3B-GGUF");
```

## 🗄️ Model Management

SharpAI automatically handles downloading GGUF files from HuggingFace. Only GGUF format models are supported.

- Queries available GGUF files for a model
- Selects appropriate quantization based on file naming conventions
- Downloads and stores models with metadata
- Tracks model information in local Sqlite model registry

Model metadata includes:

- Model name and GUID
- File size and hashes (MD5, SHA1, SHA256)
- Quantization type
- Source URL
- Creation timestamps

## 🔢 Generating Embeddings

Generate vector embeddings for text:

```csharp
// Single text embedding
float[] embedding = await ai.Embeddings.Generate(
    model: "leliuga/all-MiniLM-L6-v2-GGUF",
    input: "This is a sample text"
);

// Multiple text embeddings
string[] texts = { "First text", "Second text", "Third text" };
float[][] embeddings = await ai.Embeddings.Generate(
    model: "leliuga/all-MiniLM-L6-v2-GGUF",
    inputs: texts
);
```

## 📝 Text Completions

> *Note*: for best results, structure your prompt in a manner appropriate for the model you are using.  See the prompt formatting section below.

Generate text continuations:

```csharp
// Non-streaming completion
string completion = await ai.Completion.GenerateCompletion(
    model: "QuantFactory/Qwen2.5-3B-GGUF",
    prompt: "The meaning of life is",
    maxTokens: 512,
    temperature: 0.7f
);

// Streaming completion
await foreach (string token in ai.Completion.GenerateCompletionStreaming(
    model: "QuantFactory/Qwen2.5-3B-GGUF",
    prompt: "Write a poem about",
    maxTokens: 512,
    temperature: 0.8f))
{
    Console.Write(token);
}
```

## 💬 Chat Completions

> *Note*: for best results, structure your prompt in a manner appropriate for the model you are using.  See the prompt formatting section below.

Generate conversational responses:

```csharp
// Non-streaming chat
string response = await ai.Chat.GenerateCompletion(
    model: "QuantFactory/Qwen2.5-3B-GGUF",
    prompt: chatFormattedPrompt,  // Prompt should be formatted for chat
    maxTokens: 512,
    temperature: 0.7f
);

// Streaming chat
await foreach (string token in ai.Chat.GenerateCompletionStreaming(
    model: "QuantFactory/Qwen2.5-3B-GGUF",
    prompt: chatFormattedPrompt,
    maxTokens: 512,
    temperature: 0.7f))
{
    Console.Write(token);
}
```

## 🛠️ Prompt Formatting

SharpAI includes prompt builders to format conversations for different model types:

### Chat Message Formatting

```csharp
using SharpAI.Prompts;

List<ChatMessage> messages = new List<ChatMessage>
{
    new ChatMessage { Role = "system", Content = "You are a helpful assistant." },
    new ChatMessage { Role = "user", Content = "What is the capital of France?" },
    new ChatMessage { Role = "assistant", Content = "The capital of France is Paris." },
    new ChatMessage { Role = "user", Content = "What is its population?" }
};

// Format for different model types
string chatMLPrompt = PromptBuilder.Build(ChatFormat.ChatML, messages);
/* Output:
<|im_start|>system
You are a helpful assistant.<|im_end|>
<|im_start|>user
What is the capital of France?<|im_end|>
<|im_start|>assistant
The capital of France is Paris.<|im_end|>
<|im_start|>user
What is its population?<|im_end|>
<|im_start|>assistant
*/

string llama2Prompt = PromptBuilder.Build(ChatFormat.Llama2, messages);
/* Output:
<s>[INST] <<SYS>>
You are a helpful assistant.
<</SYS>>

What is the capital of France? [/INST] The capital of France is Paris. </s><s>[INST] What is its population? [/INST] 
*/

string simplePrompt = PromptBuilder.Build(ChatFormat.Simple, messages);
/* Output:
system: You are a helpful assistant.
user: What is the capital of France?
assistant: The capital of France is Paris.
user: What is its population?
assistant:
*/
```

Supported chat formats:
- `Simple` - Basic role: content format (generic models, base models)
- `ChatML` - OpenAI ChatML format (GPT models, models fine-tuned with ChatML) including Qwen 2, Qwen 3, and Qwen 3.5
- `Llama2` - Llama 2 instruction format (Llama-2-Chat models)
- `Llama3` - Llama 3 format (Llama-3-Instruct models)
- `Alpaca` - Alpaca instruction format (Alpaca, Vicuna, WizardLM, and many Llama-based fine-tunes)
- `Mistral` - Mistral instruction format (Mistral-Instruct, Mixtral-Instruct models)
- `HumanAssistant` - Human/Assistant format (Anthropic Claude-style training, some chat models)
- `Zephyr` - Zephyr model format (Zephyr beta/alpha models)
- `Phi` - Microsoft Phi format (Phi-2, Phi-3 models)
- `DeepSeek` - DeepSeek format (DeepSeek-Coder, DeepSeek-LLM models)
- `Gemma` - Google Gemma turn-token format (Gemma 2, Gemma 3, and Gemma 4 models)

If you are unsure which your model supports, choose `Simple`.

SharpAI maps common architecture aliases automatically, including `qwen3.5`, `qwen-3.5`, `gemma4`, and `gemma-4`.

These mappings help newer Qwen3.5 and Gemma4 GGUFs pick the correct prompt template automatically.

### Text Generation Formatting

```csharp
using SharpAI.Prompts;

// Simple instruction
string instructionPrompt = TextPromptBuilder.Build(
    TextGenerationFormat.Instruction,
    "Write a haiku about programming"
);
/* Output:
### Instruction:
Write a haiku about programming

### Response:
*/

// Code generation with context
Dictionary<string, string> context = new Dictionary<string, string>
{
    ["language"] = "python",
    ["requirements"] = "Include error handling"
};

string codePrompt = TextPromptBuilder.Build(
    TextGenerationFormat.CodeGeneration,
    "Write a function to parse JSON",
    context
);
/* Output:
Language: python
Task: Write a function to parse JSON
Requirements: Include error handling

```python
*/

// Question-answer format
string qaPrompt = TextPromptBuilder.Build(
    TextGenerationFormat.QuestionAnswer,
    "What causes rain?"
);
/* Output:
Question: What causes rain?

Answer:
*/

// Few-shot examples
List<PromptExample> examples = new List<PromptExample>
{
    new PromptExample("2+2", "4"),
    new PromptExample("5*3", "15")
};

string fewShotPrompt = TextPromptBuilder.BuildWithExamples(
    TextGenerationFormat.QuestionAnswer,
    "7-3",
    examples
);
/* Output:
Examples:

Question: 2+2

Answer:
4

---

Question: 5*3

Answer:
15

---

Now complete the following:

Question: 7-3

Answer:
*/
```

Supported text generation formats:
- `Raw` - No formatting
- `Completion` - Continuation format
- `Instruction` - Instruction/response format
- `QuestionAnswer` - Q&A format
- `CreativeWriting` - Story/creative format
- `CodeGeneration` - Code generation format
- `Academic` - Academic writing format
- `ListGeneration` - List creation format
- `TemplateFilling` - Template completion
- `Dialogue` - Dialogue generation

## 🌐 API Server

SharpAI includes a fully-functional REST API server through the **SharpAI.Server** project, built on Watson 7. It provides Ollama-compatible endpoints, OpenAI-compatible endpoints, a settings-management API, and built-in OpenAPI 3.0 / Swagger UI.

Ollama API endpoints include:
- `GET /api/tags` — List available local models (returns a `capabilities` object indicating embedding and completion support per model)
- `POST /api/pull` — Download models from HuggingFace (streams NDJSON progress with `downloaded`, `completed`, `total`, and `percent`)
- `DELETE /api/delete` — Delete a local model
- `GET /api/ps` — List models currently loaded in memory (analogous to `ollama ps`)
- `POST /api/embed` — Generate embeddings
- `POST /api/generate` — Text completions (streaming and non-streaming; honors `options.stop`)
- `POST /api/chat` — Chat completions (automatically wraps messages in the correct chat template for the model's GGUF architecture)

OpenAI API endpoints include:
- `POST /v1/embeddings` — Generate embeddings
- `POST /v1/completions` — Text completions (streaming via SSE)
- `POST /v1/chat/completions` — Chat completions (streaming via SSE)

Settings API:
- `GET /api/settings` — Return the full live in-memory `Settings` object
- `PUT /api/settings` — Replace the in-memory settings and rewrite `sharpai.json` to disk. `CreatedUtc` and `SoftwareVersion` are preserved server-side so clients cannot overwrite them. Some settings (REST `Hostname`/`Port`/`Ssl`, `Database`) take effect only on the next restart.

Operational endpoints:
- `GET /health` - Lightweight liveness check for process monitoring
- `GET /ready` - Readiness check for native backend initialization, database initialization, and writable runtime directories

API documentation:
- `GET /openapi.json` — Complete OpenAPI 3.0 document describing every route, tag, request body, and response schema
- `GET /swagger` — Interactive Swagger UI served from the same server

CORS preflight `OPTIONS` requests are handled by the server so dashboard cross-origin calls work out of the box.

## ⚙️ Requirements

### System Requirements

**Minimum:**
- **OS**: Windows 10+, macOS 12+, or Linux (Ubuntu 20.04+, Debian 11+)
- **.NET**: 8.0 or higher
- **RAM**: Minimum 8GB of RAM recommended, have enough RAM for running models if using CPU
- **Disk**: 20GB+ of disk space recommended, have enough capacity for downloaded models
- **Internet**: Required for downloading models
- **HuggingFace API Key**: Required (free at https://huggingface.co/settings/tokens)

**For GPU Acceleration (Optional):**

*NVIDIA CUDA (Windows/Linux):*
- **NVIDIA GPU** with Compute Capability 6.0+ (Pascal or newer)
- 8GB+ VRAM (16GB+ for larger models)
- NVIDIA proprietary drivers installed
- CUDA Toolkit 12.x (for bare-metal deployments)
- NVIDIA Container Toolkit (for Docker deployments)

*Apple Metal (macOS Apple Silicon):*
- Apple M1, M2, M3, or M4 chip
- macOS 13 (Ventura) or later
- Bare-metal installation (not Docker — Docker containers run Linux and cannot access Metal)

**Important GPU Notes:**
- AMD and Intel GPUs are not supported
- Docker on Apple Silicon does **not** provide Metal acceleration — use bare-metal macOS for GPU

### Tested Platforms

SharpAI has been tested on:
- Windows 11 (x64)
- macOS Sequoia (Apple Silicon - Metal GPU)
- Ubuntu 24.04 LTS (x64)

### Full Deployment Guide

For detailed installation instructions, troubleshooting, and production deployment, see **[DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md)**.

## 📊 Model Information

When models are downloaded, the following information is tracked:

- Model name and unique GUID
- File size
- MD5, SHA1, and SHA256 hashes
- Quantization type (e.g., Q4_K_M, Q5_K_S)
- Source URL from HuggingFace
- Download and creation timestamps

## 🔧 Configuration

### Directory Structure

Models are stored in the specified `modelDirectory` with files named by their GUID. Model metadata is stored in the SQLite database specified by `databaseFilename`.

### GPU Support

SharpAI automatically detects GPU availability and optimizes layer allocation at startup.

| Platform | CPU | GPU |
|----------|-----|-----|
| Windows x64 | ✅ | ✅ (CUDA) |
| Linux x64 | ✅ | ✅ (CUDA) |
| macOS Apple Silicon (ARM64) | ✅ | ✅ (Metal) |
| macOS Intel (x64) | ✅ | ❌ |
| Docker on Apple Silicon | ✅ | ❌ (Metal requires bare-metal macOS) |

**Supported:**
- **NVIDIA GPUs** via CUDA (Windows and Linux)
- **Apple Silicon** via Metal (macOS ARM64, bare-metal only)

**Not Supported:**
- AMD GPUs (ROCm/Vulkan not supported)
- Intel GPUs (SYCL/Vulkan not supported)

The SharpAI library automatically selects CPU, CUDA, or Metal before loading a model. On native macOS ARM64, the packaged Metal backend is selected and model layers are offloaded by default. The server delegates to the same library initializer.

For embedded use, backend selection runs automatically through `LlamaSharpEngine` (including engines created by `AIDriver`):

```csharp
using SharpAI.Classes.Runtime;
using SharpAI.Engines;

using LlamaSharpEngine engine = new LlamaSharpEngine();
await engine.InitializeAsync("model.gguf");
Console.WriteLine(NativeBackendInfo.SelectedBackend);
```

`SHARPAI_FORCE_BACKEND=cpu` disables automatic acceleration. `SHARPAI_GPU_LAYERS=0` keeps the selected backend but loads model layers on the CPU; positive values limit offloaded layers, and `-1` requests all layers. Set `SHARPAI_ENABLE_NATIVE_LOGGING=true` to inspect native offload messages.

Embedded applications can call `NativeBackendInitializer.Initialize` with `NativeBackendOptions` before the first native operation to supply custom backend paths. `NativeLibraryLoader.Configure(path, "metal")` also supports an explicit library and backend identity. Configuration is process-wide; later automatic initialization preserves the initial selection. See the [Requirements](#-requirements) section for hardware requirements.

## 🐳 Running in Docker

SharpAI.Server is available as a Docker image, providing an easy way to deploy the Ollama-compatible API server without local installation.

### Quick Start (v4.0.1)

For the bundled stack, run `cd docker && ./update.sh`. This initializes a missing SQLite database
file, pulls the v4.0.1 server/dashboard images, and starts the stack. The UI is at
http://localhost:3000 and waits for the server at http://localhost:8000/ready to become healthy.
For a first manual `docker compose up -d`, run `touch sharpai.db` from `docker/` beforehand.

#### Using Docker Run

```bash
docker run -d --name sharpai -p 8000:8000 jchristn77/sharpai:v4.0.1
```

#### Using Docker Compose

Linux/macOS, from the repository root:

```bash
cd docker
./update.sh
```

Windows PowerShell, from the repository root:

```powershell
cd docker
if (!(Test-Path sharpai.db)) { New-Item -ItemType File sharpai.db | Out-Null }
docker compose pull
docker compose up -d
```

### Prerequisites

Before running the Docker container, decide what you want to persist:

1. **Configuration file**: The image includes a Docker-safe `/app/sharpai.json` default. Mount your own `sharpai.json` when you want persistent/custom settings.
2. **Directory structure**: The image creates `/app/logs`, `/app/models`, and `/app/temp`. Bind mount `./logs/` and `./models/` when you want logs and downloaded GGUF models to survive container replacement.

### Docker Image

The official Docker image is available at: [`jchristn77/sharpai`](https://hub.docker.com/r/jchristn77/sharpai).  Refer to the `docker` directory for assets useful for running in Docker and Docker Compose.

### Docker Runtime Controls

The Docker image contains CPU and CUDA-capable Linux native libraries and selects the backend at container startup/runtime. These environment variables are available in the image and are included with placeholder defaults in the compose files under `docker/`.

| Variable | Default | Description |
|----------|---------|-------------|
| `DOTNET_GC_SERVER` | `1` | Enables .NET server GC for sustained server workloads. The Docker entrypoint maps this to .NET's canonical `DOTNET_gcServer` setting. |
| `SHARPAI_FORCE_BACKEND` | `auto` | Backend selection: `auto`, `cpu`, `cuda`, or `metal`. In Docker, `metal` cannot be used because containers run Linux. |
| `SHARPAI_CPU_VARIANT` | `auto` | CPU native library variant: `auto`, `avx512`, `avx2`, `avx`, or `noavx`. |
| `SHARPAI_REQUIRE_BACKEND` | `false` | When `true`, startup fails if the selected backend cannot load instead of falling back to CPU. |
| `SHARPAI_ENABLE_NATIVE_LOGGING` | `false` | Enables llama.cpp native logging for backend troubleshooting. |
| `SHARPAI_NUM_THREADS` | `0` | Generation thread count. `0` means automatic sizing from the container CPU allocation. |
| `SHARPAI_BATCH_THREADS` | `0` | Batch evaluation thread count. `0` means use the generation thread count. |
| `SHARPAI_GPU_LAYERS` | `auto` | GPU offload layers for CUDA/Metal: `auto` or `-1` means all layers, `0` disables offload, positive values offload that many layers. |
| `SHARPAI_MAIN_GPU` | `0` | Main GPU index used by llama.cpp when multiple GPUs are visible. |
| `SHARPAI_CONTEXT_SIZE` | `0` | Context size override. `0` keeps model/library defaults. |
| `SHARPAI_BATCH_SIZE` | `0` | Prompt batch size override. `0` keeps library defaults. |
| `SHARPAI_UBATCH_SIZE` | `0` | Physical micro-batch size override. `0` keeps library defaults. |
| `SHARPAI_USE_MMAP` | `true` | Enables memory-mapped model loading for faster loads and lower duplicate memory pressure. |
| `SHARPAI_USE_MLOCK` | `false` | Locks model pages in RAM. If set to `true`, configure container `memlock` ulimits. |
| `SHARPAI_FLASH_ATTENTION` | `false` | Enables flash attention when supported by the selected backend/model. Leave off unless validated with your models. |

For NVIDIA Docker deployments, the CUDA compose file also sets `NVIDIA_VISIBLE_DEVICES=all` and `NVIDIA_DRIVER_CAPABILITIES=compute,utility`.

### Volume Mappings

The container uses several volume mappings for persistence:

| Host Path | Container Path | Description |
|-----------|---------------|-------------|
| `./sharpai.json` | `/app/sharpai.json` | Configuration file |
| `./sharpai.db` | `/app/sharpai.db` | SQLite database for model registry |
| `./logs/` | `/app/logs/` | Application logs |
| `./models/` | `/app/models/` | Downloaded GGUF model files |

### Configuration

Modify the `sharpai.json` file to supply your configuration.

### Networking

The container exposes port 8000 by default. 

You can access Ollama APIs at:
- `http://localhost:8000/api/tags` - List available models
- `http://localhost:8000/api/pull` - Pull a model
- `http://localhost:8000/api/generate` - Generate text
- `http://localhost:8000/api/chat` - Chat completions
- `http://localhost:8000/api/embed` - Generate embeddings

You can access OpenAI APIs at:
- `http://localhost:8000/v1/embeddings` - Generate embeddings
- `http://localhost:8000/v1/completions` - Generate text
- `http://localhost:8000/v1/chat/completions` - Chat completions

Operational endpoints:
- `http://localhost:8000/health` - Liveness check
- `http://localhost:8000/ready` - Readiness check

### Example Usage

1. Create persistent directories when you want host-side logs and models:
   ```bash
   mkdir logs models
   ```

2. Create or mount `sharpai.json` when you need custom settings. The image includes a default for quick smoke tests.

3. Run the container:
   ```bash
   docker run -d --name sharpai -p 8000:8000 jchristn77/sharpai:v4.0.1
   ```

4. Download a model using the API (GGUF format required):
   ```bash
   curl -H "Authorization: Bearer $SHARPAI_BEARER_TOKEN" http://localhost:8000/api/pull \
     -d '{"model":"QuantFactory/Qwen2.5-3B-GGUF"}'
   ```

5. Generate text:
   ```bash
   curl -H "Authorization: Bearer $SHARPAI_BEARER_TOKEN" http://localhost:8000/api/generate \
     -d '{
       "model": "QuantFactory/Qwen2.5-3B-GGUF",
       "prompt": "Why is the sky blue?",
       "stream": false
     }'
   ```

### Docker Compose

For production deployments, use Docker Compose. The repository ships ready-to-use files under `docker/`:
`compose.yaml` (SharpAI plus the Prometheus/Loki/Tempo/Grafana observability stack), `compose-cpu.yaml`,
and `compose-cuda.yaml`. A minimal standalone service looks like this:

```yaml
services:
  sharpai:
    image: jchristn77/sharpai:v4.0.1
    ports:
      - "8000:8000"
    volumes:
      - ./sharpai.json:/app/sharpai.json
      - type: bind
        source: ./sharpai.db
        target: /app/sharpai.db
        bind:
          create_host_path: false
      - ./logs:/app/logs
      - ./models:/app/models
    environment:
      DOTNET_GC_SERVER: "1"
      SHARPAI_FORCE_BACKEND: "auto"
      SHARPAI_CPU_VARIANT: "auto"
      SHARPAI_REQUIRE_BACKEND: "false"
      SHARPAI_ENABLE_NATIVE_LOGGING: "false"
      SHARPAI_NUM_THREADS: "0"
      SHARPAI_BATCH_THREADS: "0"
      SHARPAI_GPU_LAYERS: "auto"
      SHARPAI_MAIN_GPU: "0"
      SHARPAI_CONTEXT_SIZE: "0"
      SHARPAI_BATCH_SIZE: "0"
      SHARPAI_UBATCH_SIZE: "0"
      SHARPAI_USE_MMAP: "true"
      SHARPAI_USE_MLOCK: "false"
      SHARPAI_FLASH_ATTENTION: "false"
    healthcheck:
      test: ["CMD-SHELL", "curl --fail http://localhost:8000/ready || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 5
      start_period: 30s
    restart: unless-stopped
```

Then run:
```bash
docker compose up -d
```

### GPU Support in Docker

To enable GPU acceleration in Docker:

#### NVIDIA GPUs
Install the [NVIDIA Container Toolkit](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/install-guide.html) and modify your run command:

```bash
docker run --gpus all \
  -p 8000:8000 \
  -v ./sharpai.json:/app/sharpai.json \
  -v ./sharpai.db:/app/sharpai.db \
  -v ./logs:/app/logs \
  -v ./models:/app/models \
  jchristn77/sharpai:v4.0.1
```

For Docker Compose, add:
```yaml
services:
  sharpai:
    # ... other configuration ...
    environment:
      SHARPAI_FORCE_BACKEND: "cuda"
      SHARPAI_REQUIRE_BACKEND: "true"
      NVIDIA_VISIBLE_DEVICES: "all"
      NVIDIA_DRIVER_CAPABILITIES: "compute,utility"
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: all
              capabilities: [gpu]
```

### Troubleshooting

- **Container exits immediately**: Check that `sharpai.json` exists and is valid JSON
- **Models not persisting**: Ensure the `./models/` directory has proper write permissions
- **Cannot connect to API**: Verify port 8000 is not already in use and firewall rules allow access
- **Out of memory errors**: Large models may require significant RAM. Consider using quantized models or adjusting Docker memory limits
## 📚 Version History

Please see the [CHANGELOG.md](CHANGELOG.md) file for detailed version history and release notes.

Have a bug, feature request, or idea? Please [file an issue](https://github.com/jchristn/SharpAI/issues) on our GitHub repository. We welcome community input on our roadmap!

## 📄 License

This project is licensed under the MIT License.

## 🙏 Acknowledgments

- Built on [LlamaSharp](https://github.com/SciSharp/LLamaSharp) for GGUF model inference
- Model hosting by [HuggingFace](https://huggingface.co/)
- Inspired by (and forever grateful to) [Ollama](https://ollama.ai/) for API compatibility
- Special thanks to the community of developers that helped build, test, and refine SharpAI

## Operator interface plans and diagnostics

See [the TUI implementation plan](TUI_PLAN.md) for the proposed C#/TUIKit client and complete dashboard parity checklist. The TUI is planned, not yet shipped. Current dashboard diagnostics are documented in [request and inference history](docs/HISTORY_TELEMETRY.md), [JSON logging](docs/LOGGING.md), and [Access administration](docs/DASHBOARD_ADMINISTRATION.md).

### Scoped prompt and model-configuration libraries

The dashboard manages system-wide defaults, tenant defaults and private user items on the **System prompts**
and **Model configurations** pages. Playground selects these saved items using two dropdowns and adjacent
edit actions. Users can duplicate shared defaults into their personal library; administrators may publish
to their authorized shared scopes. See [Scoped libraries](docs/SCOPED_LIBRARIES.md) for ownership rules,
API operations and migration behavior. Legacy model presets remain available independently.

### Dashboard and API authentication

Sign in with `admin@sharpai.local` / `password`, tenant `default`, on an initial installation.
Dashboard access requires a validated session. All API calls require `Authorization: Bearer <token>`;
only health/readiness probes and login are anonymous. The default model/inference bearer token is
provisioned at `docker/auth/default-bearer-token` for Compose installations. Manage additional tokens
and permissions under **Access**. See [authentication setup](DEPLOYMENT_GUIDE.md#api-bearer-credentials).

Credential administrators can reveal, hide, copy and regenerate API bearer tokens under **Access → Credentials**.
See [token management API](DEPLOYMENT_GUIDE.md#reveal-and-regenerate-api-bearer-tokens) for the endpoints and migration behavior.

API curl examples use `SHARPAI_BEARER_TOKEN`. From the repository root, set it with
`export SHARPAI_BEARER_TOKEN="$(cat docker/auth/default-bearer-token)"` before making model or inference requests.
