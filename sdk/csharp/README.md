<div align="center">
  <img src="https://github.com/jchristn/sharpai/blob/main/assets/logo.png" width="256" height="256">
</div>

# SharpAI.Sdk

**A C# SDK for interacting with SharpAI server instances - providing Ollama and OpenAI compatible API wrappers for local AI inference.**

<p align="center">
  <img src="https://img.shields.io/badge/.NET-5C2D91?style=for-the-badge&logo=.net&logoColor=white" />
  <img src="https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white" />
  <img src="https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge" />
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/SharpAI.Sdk/">
    <img src="https://img.shields.io/nuget/v/SharpAI.Sdk.svg?style=flat" alt="NuGet Version">
  </a>
  &nbsp;
  <a href="https://www.nuget.org/packages/SharpAI.Sdk">
    <img src="https://img.shields.io/nuget/dt/SharpAI.Sdk.svg" alt="NuGet Downloads">
  </a>
</p>

<p align="center">
  <strong>A .NET SDK for SharpAI - Local AI inference with Ollama and OpenAI compatible APIs</strong>
</p>

<p align="center">
  Embeddings • Completions • Chat • Model Management • Streaming Support
</p>

**IMPORTANT** - SharpAI.Sdk assumes you have deployed the SharpAI REST server. If you are integrating a SharpAI library directly into your code, use of this SDK is not necessary.

---

## 🚀 Features

- **Ollama API Compatibility** - Full support for Ollama API endpoints and models
- **OpenAI API Compatibility** - Complete OpenAI API compatibility for seamless integration
- **Model Management** - Download, list, and delete models with streaming progress updates
- **Multiple Inference Types**:
  - Text embeddings generation
  - Text completions (streaming and non-streaming)
  - Chat completions (streaming and non-streaming)
- **Streaming Support** - Real-time token streaming for completions and chat
- **Async/Await Support** - Full async/await support for all operations
- **Error Handling** - Graceful error handling with detailed logging
- **Configurable Logging** - Built-in request/response logging capabilities

## 📦 Installation

Install SharpAI.Sdk via NuGet:

```bash
dotnet add package SharpAI.Sdk
```

Or via Package Manager Console:

```powershell
Install-Package SharpAI.Sdk
```

## 🚀 Quick Start

### Basic Usage

```csharp
using SharpAI.Sdk;

// Initialize the SDK
SharpAISdk sdk = new SharpAISdk("http://localhost:8000");

// List available models
List<OllamaLocalModel>? models = await sdk.Ollama.ListLocalModels();
Console.WriteLine($"Found {models?.Count ?? 0} models");

// Generate a completion
OllamaGenerateCompletionRequest request = new OllamaGenerateCompletionRequest
{
    Model = "llama2",
    Prompt = "The meaning of life is",
    Options = new OllamaCompletionOptions
    {
        Temperature = 0.7f,
        NumPredict = 100
    }
};

OllamaGenerateCompletionResult? result = await sdk.Ollama.GenerateCompletion(request);
Console.WriteLine($"Completion: {result?.Response}");
```

### With Logging

```csharp
SharpAISdk sdk = new SharpAISdk("http://localhost:8000");
sdk.LogRequests = true;
sdk.LogResponses = true;
sdk.Logger = (level, message) => Console.WriteLine($"[{level}] {message}");
```

## 📖 API Reference

### SharpAISdk Class

The main SDK class that provides access to all functionality.

#### Constructor

```csharp
public SharpAISdk(string endpoint)
```

- `endpoint`: SharpAI server endpoint URL

#### Properties

- `Endpoint`: Server endpoint URL
- `TimeoutMs`: Request timeout in milliseconds (default: 300000)
- `LogRequests`: Enable request logging
- `LogResponses`: Enable response logging
- `Logger`: Custom logger delegate

#### Main API Groups

- `Ollama`: Ollama API methods
- `OpenAI`: OpenAI API methods

## 🔧 Ollama API Methods

### Model Management

```csharp
// List local models
List<OllamaLocalModel>? models = await sdk.Ollama.ListLocalModels();

// Pull a model with streaming progress
OllamaPullModelRequest pullRequest = new OllamaPullModelRequest
{
    Model = "llama2"
};

await foreach (SharpAIPullModelResponse progress in sdk.Ollama.PullModel(pullRequest))
{
    Console.WriteLine($"Status: {progress.Status}");
    if (progress.IsComplete()) break;
}

// Delete a model
OllamaDeleteModelRequest deleteRequest = new OllamaDeleteModelRequest
{
    Model = "llama2"
};
await sdk.Ollama.DeleteModel(deleteRequest);
```

### Text Completions

```csharp
// Non-streaming completion
OllamaGenerateCompletionRequest request = new OllamaGenerateCompletionRequest
{
    Model = "llama2",
    Prompt = "The future of AI is",
    Options = new OllamaCompletionOptions
    {
        Temperature = 0.7f,
        NumPredict = 100
    }
};

OllamaGenerateCompletionResult? result = await sdk.Ollama.GenerateCompletion(request);
Console.WriteLine($"Completion: {result?.Response}");

// Streaming completion
await foreach (OllamaStreamingCompletionResult chunk in sdk.Ollama.GenerateCompletionStream(request))
{
    Console.Write(chunk.Response);
}
```

### Chat Completions

```csharp
// Non-streaming chat
List<OllamaChatMessage> messages = new List<OllamaChatMessage>
{
    new OllamaChatMessage { Role = "user", Content = "Hello, how are you?" }
};

OllamaGenerateChatCompletionRequest chatRequest = new OllamaGenerateChatCompletionRequest
{
    Model = "llama2",
    Messages = messages,
    Options = new OllamaCompletionOptions
    {
        Temperature = 0.7f,
        NumPredict = 100
    }
};

OllamaGenerateCompletionResult? chatResult = await sdk.Ollama.GenerateChatCompletion(chatRequest);
Console.WriteLine($"Assistant: {chatResult?.Response}");

// Streaming chat
await foreach (OllamaGenerateChatCompletionChunk chunk in sdk.Ollama.GenerateChatCompletionStream(chatRequest))
{
    Console.Write(chunk.Message?.Content);
}
```

### Embeddings

```csharp
// Single text embedding
OllamaGenerateEmbeddingsRequest embeddingRequest = new OllamaGenerateEmbeddingsRequest
{
    Model = "llama2",
    Input = "This is a test sentence"
};

OllamaGenerateEmbeddingsResult? embeddingResult = await sdk.Ollama.GenerateEmbeddings(embeddingRequest);
Console.WriteLine($"Embedding dimensions: {embeddingResult?.Embedding?.Length}");

// Multiple text embeddings
OllamaGenerateEmbeddingsRequest multipleRequest = new OllamaGenerateEmbeddingsRequest
{
    Model = "llama2"
};
multipleRequest.SetInputs(new[] { "First text", "Second text", "Third text" });

OllamaGenerateEmbeddingsResult? multipleResult = await sdk.Ollama.GenerateMultipleEmbeddings(multipleRequest);
Console.WriteLine($"Generated {multipleResult?.Embeddings?.Count} embeddings");
```

## 🤖 OpenAI API Methods

### Text Completions

```csharp
// Non-streaming completion
OpenAIGenerateCompletionRequest request = new OpenAIGenerateCompletionRequest
{
    Model = "llama2",
    Prompt = "The future of AI is",
    MaxTokens = 100,
    Temperature = 0.7f
};

OpenAIGenerateCompletionResult? result = await sdk.OpenAI.GenerateCompletionAsync(request);
Console.WriteLine($"Completion: {result?.Choices?[0]?.Text}");

// Streaming completion
await foreach (OpenAIStreamingCompletionResult chunk in sdk.OpenAI.GenerateCompletionStreamAsync(request))
{
    Console.Write(chunk?.Choices?[0]?.Text);
}
```

### Chat Completions

```csharp
// Non-streaming chat
List<OpenAIChatMessage> messages = new List<OpenAIChatMessage>
{
    new OpenAIChatMessage { Role = "user", Content = "Hello, how are you?" }
};

OpenAIGenerateChatCompletionRequest chatRequest = new OpenAIGenerateChatCompletionRequest
{
    Model = "llama2",
    Messages = messages,
    MaxTokens = 100,
    Temperature = 0.7f
};

OpenAIGenerateChatCompletionResult? result = await sdk.OpenAI.GenerateChatCompletionAsync(chatRequest);
Console.WriteLine($"Assistant: {result?.Choices?[0]?.Message?.Content}");

// Streaming chat
await foreach (OpenAIStreamingCompletionResult chunk in sdk.OpenAI.GenerateChatCompletionStreamAsync(chatRequest))
{
    Console.Write(chunk?.Choices?[0]?.Text);
}
```

### Embeddings

```csharp
// Single text embedding
OpenAIGenerateEmbeddingsRequest embeddingRequest = new OpenAIGenerateEmbeddingsRequest
{
    Model = "llama2",
    Input = "This is a test sentence"
};

OpenAIGenerateEmbeddingsResult? embeddingResult = await sdk.OpenAI.GenerateEmbeddingsAsync(embeddingRequest);
Console.WriteLine($"Embedding dimensions: {embeddingResult?.Data?[0]?.Embedding?.Length}");

// Multiple text embeddings
OpenAIGenerateEmbeddingsRequest multipleRequest = new OpenAIGenerateEmbeddingsRequest
{
    Model = "llama2"
};
multipleRequest.SetInputs(new[] { "First text", "Second text", "Third text" });

OpenAIGenerateEmbeddingsResult? multipleResult = await sdk.OpenAI.GenerateMultipleEmbeddingsAsync(multipleRequest);
Console.WriteLine($"Generated {multipleResult?.Data?.Count} embeddings");
```

## 🗄️ Model Management

SharpAI.Sdk provides comprehensive model management capabilities:

### Pulling Models

```csharp
OllamaPullModelRequest pullRequest = new OllamaPullModelRequest
{
    Model = "TheBloke/Llama-2-7B-Chat-GGUF"
};

Console.WriteLine("Downloading model with progress updates...");
await foreach (SharpAIPullModelResponse progress in sdk.Ollama.PullModel(pullRequest))
{
    if (!string.IsNullOrEmpty(progress.Status))
    {
        Console.Write($"\rStatus: {progress.Status}");
        
        if (progress.Downloaded.HasValue && progress.Percent.HasValue)
        {
            double? percentage = progress.GetProgressPercentage();
            string progressStr = progress.GetFormattedProgress();
            Console.Write($" - {progressStr}");
        }
    }
    
    if (progress.IsComplete())
    {
        Console.WriteLine($"\nDownload completed: {progress.Status}");
        break;
    }
    
    if (progress.HasError())
    {
        Console.WriteLine($"\nError: {progress.Error}");
        break;
    }
}
```

### Listing Models

```csharp
List<OllamaLocalModel>? models = await sdk.Ollama.ListLocalModels();
if (models != null && models.Count > 0)
{
    Console.WriteLine($"Found {models.Count} models:");
    foreach (OllamaLocalModel model in models)
    {
        Console.WriteLine($"  - {model.Name} (Size: {model.Size} bytes)");
        if (model.Details != null)
        {
            Console.WriteLine($"    Format: {model.Details.Format}");
            Console.WriteLine($"    Family: {model.Details.Family}");
            Console.WriteLine($"    Parameter Size: {model.Details.ParameterSize}");
        }
    }
}
```

## 🌊 Streaming Support

Both Ollama and OpenAI APIs support streaming for real-time token generation:

### Ollama Streaming

```csharp
// Streaming text completion
OllamaGenerateCompletionRequest request = new OllamaGenerateCompletionRequest
{
    Model = "llama2",
    Prompt = "Write a story about",
    Options = new OllamaCompletionOptions
    {
        Temperature = 0.8f,
        NumPredict = 200
    }
};

Console.WriteLine("Streaming completion:");
await foreach (OllamaStreamingCompletionResult chunk in sdk.Ollama.GenerateCompletionStream(request))
{
    Console.Write(chunk.Response);
}

// Streaming chat completion
OllamaGenerateChatCompletionRequest chatRequest = new OllamaGenerateChatCompletionRequest
{
    Model = "llama2",
    Messages = new List<OllamaChatMessage>
    {
        new OllamaChatMessage { Role = "user", Content = "Tell me a joke" }
    },
    Options = new OllamaCompletionOptions
    {
        Temperature = 0.7f,
        NumPredict = 150
    }
};

Console.WriteLine("Streaming chat:");
await foreach (OllamaGenerateChatCompletionChunk chunk in sdk.Ollama.GenerateChatCompletionStream(chatRequest))
{
    Console.Write(chunk.Message?.Content);
}
```

### OpenAI Streaming

```csharp
// Streaming text completion
OpenAIGenerateCompletionRequest request = new OpenAIGenerateCompletionRequest
{
    Model = "llama2",
    Prompt = "Write a story about",
    MaxTokens = 200,
    Temperature = 0.8f
};

Console.WriteLine("Streaming completion:");
await foreach (OpenAIStreamingCompletionResult chunk in sdk.OpenAI.GenerateCompletionStreamAsync(request))
{
    Console.Write(chunk?.Choices?[0]?.Text);
}

// Streaming chat completion
OpenAIGenerateChatCompletionRequest chatRequest = new OpenAIGenerateChatCompletionRequest
{
    Model = "llama2",
    Messages = new List<OpenAIChatMessage>
    {
        new OpenAIChatMessage { Role = "user", Content = "Tell me a joke" }
    },
    MaxTokens = 150,
    Temperature = 0.7f
};

Console.WriteLine("Streaming chat:");
await foreach (OpenAIStreamingCompletionResult chunk in sdk.OpenAI.GenerateChatCompletionStreamAsync(chatRequest))
{
    Console.Write(chunk?.Choices?[0]?.Text);
}
```

## ⚠️ Error Handling

The SDK handles errors gracefully and returns null for failed operations:

```csharp
try
{
    OllamaGenerateCompletionResult? result = await sdk.Ollama.GenerateCompletion(request);
    
    if (result == null)
    {
        Console.WriteLine("Failed to generate completion or no result received");
    }
    else
    {
        Console.WriteLine($"Success: {result.Response}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```

## ⚙️ Configuration

### Timeout Configuration

```csharp
SharpAISdk sdk = new SharpAISdk("http://localhost:8000");
sdk.TimeoutMs = 120000; // 2 minutes
```

### Logging Configuration

```csharp
SharpAISdk sdk = new SharpAISdk("http://localhost:8000");
sdk.LogRequests = true;
sdk.LogResponses = true;
sdk.Logger = (level, message) => 
{
    // Custom logging implementation
    File.AppendAllText("sdk.log", $"[{DateTime.UtcNow}] [{level}] {message}\n");
};
```

## 📊 Version History

Please see the [CHANGELOG.md](CHANGELOG.md) file for detailed version history and release notes.

## 📄 License

This project is licensed under the MIT License.

## Authentication & admin surface (v5)

The server requires bearer authentication. Pass a bearer token to the constructor, or
sign in to obtain a bearer session token (stored on the client automatically):

```csharp
using SharpAI.Sdk;

// API bearer credential (shown once on creation) or interactive session token:
SharpAISdk sdk = new SharpAISdk("http://127.0.0.1:8000", token: "eyJ...");        // bearer token

// Or interactive login -> stores the token on the client
await sdk.Admin.LoginAsync("admin@sharpai.local", "password", "default");
```

All requests then carry the appropriate auth headers automatically. The `Admin` group covers the v5
control plane:

- **Settings** — `GetSettingsAsync`, `UpdateSettingsAsync`
- **Request history** — `RequestHistoryAsync`, `RequestHistorySummaryAsync`, `RequestHistoryEntryAsync`
- **Auth** — `LoginAsync`, `SessionAsync`, `LogoutAsync`, `AuditAsync`
- **Account / RBAC** — `ListTenantsAsync`, `CreateTenantAsync`, `ListUsersAsync`, `CreateUserAsync`,
  `DeleteUserAsync`, `ListCredentialsAsync`, `CreateCredentialAsync`, `ListRolesAsync`,
  `CreateAssignmentAsync`, `UserPermissionsAsync`, `CredentialPermissionsAsync`

```csharp
JsonElement? page = await sdk.Admin.RequestHistoryAsync(new Dictionary<string, string> { ["pageSize"] = "25" });
CreatedCredential? cred = await sdk.Admin.CreateCredentialAsync("ten_...", "usr_...", "CI key"); // Token can be revealed by authorized administrators
```

Read methods return the parsed `JsonElement` (or null on a non-success status).

## Bearer token reveal and regeneration

These methods require Credential Admin permission within the addressed tenant. Reveal returns `token` and
`tokenType`; ordinary credential reads omit token values. Legacy hash-only credentials return HTTP 409 until
regenerated. Regeneration immediately invalidates the previous token and preserves grants, active status and expiry.
The SDK uses its normal non-success response behavior; do not log returned token objects.

```csharp
JsonElement? token = await sdk.Admin.RevealCredentialTokenAsync("default", "crd_...");
JsonElement? replacement = await sdk.Admin.RegenerateCredentialTokenAsync("default", "crd_...");
```

See [API contract and encrypted storage](../../DEPLOYMENT_GUIDE.md#reveal-and-regenerate-api-bearer-tokens) for authentication, caching, auditing, backup and legacy-token details.

Run SDK HTTP transport and secret-log regression tests with `dotnet test tests/SharpAI.Sdk.Tests -c Release` from this directory.

## Verified model pull sources

Pull methods accept an organization/name repository ID or a Hugging Face HTTPS repository, tree revision,
or resolve/blob GGUF URL. Pass the complete URL unchanged to preserve the requested revision and exact
file selection. The server verifies the complete model before its final NDJSON `status=success` record.
HTTP 200 alone does not confirm a successful pull. Never automatically retry an ambiguous download.
See [the shared source contract](../../docs/MODEL_PULL_SOURCES.md).

`SharpAI.Client` is the lightweight HTTP-only transport used by the terminal dashboard. It targets .NET 8/10,
provides typed library/history/pull models, bounded inspection, cancellation, NDJSON/SSE decoding, and
structured HTTP errors. It has no inference or database dependency. `SharpAISdk.CreateServerClient()`
creates an independently owned session using the existing SDK authentication snapshot.

Playground commands such as `/?`, `/clear`, and `/compact` are handled by the TUI and web
clients. Sending these strings through an SDK sends ordinary prompt text. To reproduce
compaction in an integration, use the existing nonstreaming chat API with a summary
instruction, verify completion and finish reason, then replace earlier context only after
success. See [the shared command behavior](../../docs/TUI.md#chat-commands-and-rendering).

## REST contract reference

See the [complete REST API reference](../../REST_API.md) for authentication, tenant boundaries,
permissions, request/response fields, and streaming behavior. Import the
[Postman collection](../../postman/SharpAI.postman_collection.json) for runnable examples across
Ollama, OpenAI, and management APIs. Settings use a complete GET-edit-PUT replacement; named enum
values returned by GET can be submitted unchanged. Listener/database changes require a server restart.
