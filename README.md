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

## 📁 Monorepo Structure

SharpAI is organized as a monorepo containing the core library, server, dashboard, and client SDKs:

```
SharpAI/
├── src/                    # Core .NET library and server
│   ├── SharpAI/           # Core library (NuGet: SharpAI)
│   ├── SharpAI.Server/    # REST API server (Watson 7 + OpenAPI/Swagger)
│   └── Test.*/            # Test projects
├── dashboard/              # Vite + React + Ant Design web interface
├── sdk/
│   ├── csharp/            # C# SDK (NuGet: SharpAI.Sdk)
│   ├── python/            # Python SDK (coming soon)
│   └── js/                # TypeScript/JavaScript SDK (npm: @sharpai/sdk)
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
var ai = new AIDriver(
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

var messages = new List<ChatMessage>
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
- `ChatML` - OpenAI ChatML format (GPT models, models fine-tuned with ChatML) including Qwen
- `Llama2` - Llama 2 instruction format (Llama-2-Chat models)
- `Llama3` - Llama 3 format (Llama-3-Instruct models)
- `Alpaca` - Alpaca instruction format (Alpaca, Vicuna, WizardLM, and many Llama-based fine-tunes)
- `Mistral` - Mistral instruction format (Mistral-Instruct, Mixtral-Instruct models)
- `HumanAssistant` - Human/Assistant format (Anthropic Claude-style training, some chat models)
- `Zephyr` - Zephyr model format (Zephyr beta/alpha models)
- `Phi` - Microsoft Phi format (Phi-2, Phi-3 models)
- `DeepSeek` - DeepSeek format (DeepSeek-Coder, DeepSeek-LLM models)

If you are unsure which your model supports, choose `Simple`.

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
var context = new Dictionary<string, string>
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
var examples = new List<(string input, string output)>
{
    ("2+2", "4"),
    ("5*3", "15")
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

For detailed installation instructions, troubleshooting, and production deployment, see **[DEPLOYMENT-GUIDE.md](DEPLOYMENT-GUIDE.md)**.

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

The `NativeLibraryBootstrapper` automatically detects your platform and GPU at startup, selecting the appropriate backend (CPU, CUDA, or Metal). See the [Requirements](#-requirements) section for detailed GPU requirements.

## 🐳 Running in Docker

SharpAI.Server is available as a Docker image, providing an easy way to deploy the Ollama-compatible API server without local installation.

### Quick Start

#### Using Docker Run

For Windows:
```batch
run.bat v4.0.0
```

For Linux/macOS:
```bash
./run.sh v4.0.0
```

#### Using Docker Compose

For Windows:
```batch
compose-up.bat
```

For Linux/macOS:
```bash
./compose-up.sh
```

### Prerequisites

Before running the Docker container, ensure you have:

1. **Configuration file**: Create a `sharpai.json` configuration file in your working directory
2. **Directory structure**: The container expects the following directories to exist:
   - `./logs/` - For application logs
   - `./models/` - For storing downloaded GGUF models

### Docker Image

The official Docker image is available at: [`jchristn77/sharpai`](https://hub.docker.com/r/jchristn77/sharpai).  Refer to the `docker` directory for assets useful for running in Docker and Docker Compose.

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

### Example Usage

1. Create the required directory structure:
   ```bash
   mkdir logs models
   ```

2. Create your `sharpai.json` configuration file

3. Run the container:
   ```bash
   # Windows
   run.bat v4.0.0
   
   # Linux/macOS
   ./run.sh v4.0.0
   ```

4. Download a model using the API (GGUF format required):
   ```bash
   curl http://localhost:8000/api/pull \
     -d '{"model":"QuantFactory/Qwen2.5-3B-GGUF"}'
   ```

5. Generate text:
   ```bash
   curl http://localhost:8000/api/generate \
     -d '{
       "model": "QuantFactory/Qwen2.5-3B-GGUF",
       "prompt": "Why is the sky blue?",
       "stream": false
     }'
   ```

### Docker Compose

For production deployments, you can use Docker Compose. Create a `compose.yaml` file:

```yaml
services:
  sharpai:
    image: jchristn77/sharpai:v4.0.0
    ports:
      - "8000:8000"
    volumes:
      - ./sharpai.json:/app/sharpai.json
      - ./sharpai.db:/app/sharpai.db
      - ./logs:/app/logs
      - ./models:/app/models
    environment:
      - TERM=xterm-256color
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
  jchristn77/sharpai:v4.0.0
```

For Docker Compose, add:
```yaml
services:
  sharpai:
    # ... other configuration ...
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

Have a bug, feature request, or idea? Please [file an issue](https://github.com/yourusername/sharpai/issues) on our GitHub repository. We welcome community input on our roadmap!

## 📄 License

This project is licensed under the MIT License.

## 🙏 Acknowledgments

- Built on [LlamaSharp](https://github.com/SciSharp/LLamaSharp) for GGUF model inference
- Model hosting by [HuggingFace](https://huggingface.co/)
- Inspired by (and forever grateful to) [Ollama](https://ollama.ai/) for API compatibility
- Special thanks to the community of developers that helped build, test, and refine SharpAI