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
var sdk = new SharpAISdk("http://localhost:8000");

// List available models
var models = await sdk.Ollama.ListLocalModels();
Console.WriteLine($"Found {models?.Count ?? 0} models");

// Generate a completion
var request = new OllamaGenerateCompletionRequest
{
    Model = "llama2",
    Prompt = "The meaning of life is",
    Options = new OllamaCompletionOptions
    {
        Temperature = 0.7f,
        NumPredict = 100
    }
};

var result = await sdk.Ollama.GenerateCompletion(request);
Console.WriteLine($"Completion: {result?.Response}");
```

### With Logging

```csharp
var sdk = new SharpAISdk("http://localhost:8000");
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
var models = await sdk.Ollama.ListLocalModels();

// Pull a model with streaming progress
var pullRequest = new OllamaPullModelRequest
{
    Model = "llama2"
};

await foreach (var progress in sdk.Ollama.PullModel(pullRequest))
{
    Console.WriteLine($"Status: {progress.Status}");
    if (progress.IsComplete()) break;
}

// Delete a model
var deleteRequest = new OllamaDeleteModelRequest
{
    Model = "llama2"
};
await sdk.Ollama.DeleteModel(deleteRequest);
```

### Text Completions

```csharp
// Non-streaming completion
var request = new OllamaGenerateCompletionRequest
{
    Model = "llama2",
    Prompt = "The future of AI is",
    Options = new OllamaCompletionOptions
    {
        Temperature = 0.7f,
        NumPredict = 100
    }
};

var result = await sdk.Ollama.GenerateCompletion(request);
Console.WriteLine($"Completion: {result?.Response}");

// Streaming completion
await foreach (var chunk in sdk.Ollama.GenerateCompletionStream(request))
{
    Console.Write(chunk.Response);
}
```

### Chat Completions

```csharp
// Non-streaming chat
var messages = new List<OllamaChatMessage>
{
    new OllamaChatMessage { Role = "user", Content = "Hello, how are you?" }
};

var chatRequest = new OllamaGenerateChatCompletionRequest
{
    Model = "llama2",
    Messages = messages,
    Options = new OllamaCompletionOptions
    {
        Temperature = 0.7f,
        NumPredict = 100
    }
};

var chatResult = await sdk.Ollama.GenerateChatCompletion(chatRequest);
Console.WriteLine($"Assistant: {chatResult?.Response}");

// Streaming chat
await foreach (var chunk in sdk.Ollama.GenerateChatCompletionStream(chatRequest))
{
    Console.Write(chunk.Message?.Content);
}
```

### Embeddings

```csharp
// Single text embedding
var embeddingRequest = new OllamaGenerateEmbeddingsRequest
{
    Model = "llama2",
    Input = "This is a test sentence"
};

var embeddingResult = await sdk.Ollama.GenerateEmbeddings(embeddingRequest);
Console.WriteLine($"Embedding dimensions: {embeddingResult?.Embedding?.Length}");

// Multiple text embeddings
var multipleRequest = new OllamaGenerateEmbeddingsRequest
{
    Model = "llama2"
};
multipleRequest.SetInputs(new[] { "First text", "Second text", "Third text" });

var multipleResult = await sdk.Ollama.GenerateMultipleEmbeddings(multipleRequest);
Console.WriteLine($"Generated {multipleResult?.Embeddings?.Count} embeddings");
```

## 🤖 OpenAI API Methods

### Text Completions

```csharp
// Non-streaming completion
var request = new OpenAIGenerateCompletionRequest
{
    Model = "llama2",
    Prompt = "The future of AI is",
    MaxTokens = 100,
    Temperature = 0.7f
};

var result = await sdk.OpenAI.GenerateCompletionAsync(request);
Console.WriteLine($"Completion: {result?.Choices?[0]?.Text}");

// Streaming completion
await foreach (var chunk in sdk.OpenAI.GenerateCompletionStreamAsync(request))
{
    Console.Write(chunk?.Choices?[0]?.Text);
}
```

### Chat Completions

```csharp
// Non-streaming chat
var messages = new List<OpenAIChatMessage>
{
    new OpenAIChatMessage { Role = "user", Content = "Hello, how are you?" }
};

var chatRequest = new OpenAIGenerateChatCompletionRequest
{
    Model = "llama2",
    Messages = messages,
    MaxTokens = 100,
    Temperature = 0.7f
};

var result = await sdk.OpenAI.GenerateChatCompletionAsync(chatRequest);
Console.WriteLine($"Assistant: {result?.Choices?[0]?.Message?.Content}");

// Streaming chat
await foreach (var chunk in sdk.OpenAI.GenerateChatCompletionStreamAsync(chatRequest))
{
    Console.Write(chunk?.Choices?[0]?.Text);
}
```

### Embeddings

```csharp
// Single text embedding
var embeddingRequest = new OpenAIGenerateEmbeddingsRequest
{
    Model = "llama2",
    Input = "This is a test sentence"
};

var embeddingResult = await sdk.OpenAI.GenerateEmbeddingsAsync(embeddingRequest);
Console.WriteLine($"Embedding dimensions: {embeddingResult?.Data?[0]?.Embedding?.Length}");

// Multiple text embeddings
var multipleRequest = new OpenAIGenerateEmbeddingsRequest
{
    Model = "llama2"
};
multipleRequest.SetInputs(new[] { "First text", "Second text", "Third text" });

var multipleResult = await sdk.OpenAI.GenerateMultipleEmbeddingsAsync(multipleRequest);
Console.WriteLine($"Generated {multipleResult?.Data?.Count} embeddings");
```

## 🗄️ Model Management

SharpAI.Sdk provides comprehensive model management capabilities:

### Pulling Models

```csharp
var pullRequest = new OllamaPullModelRequest
{
    Model = "TheBloke/Llama-2-7B-Chat-GGUF"
};

Console.WriteLine("Downloading model with progress updates...");
await foreach (var progress in sdk.Ollama.PullModel(pullRequest))
{
    if (!string.IsNullOrEmpty(progress.Status))
    {
        Console.Write($"\rStatus: {progress.Status}");
        
        if (progress.Downloaded.HasValue && progress.Percent.HasValue)
        {
            var percentage = progress.GetProgressPercentage();
            var progressStr = progress.GetFormattedProgress();
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
var models = await sdk.Ollama.ListLocalModels();
if (models != null && models.Count > 0)
{
    Console.WriteLine($"Found {models.Count} models:");
    foreach (var model in models)
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
var request = new OllamaGenerateCompletionRequest
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
await foreach (var chunk in sdk.Ollama.GenerateCompletionStream(request))
{
    Console.Write(chunk.Response);
}

// Streaming chat completion
var chatRequest = new OllamaGenerateChatCompletionRequest
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
await foreach (var chunk in sdk.Ollama.GenerateChatCompletionStream(chatRequest))
{
    Console.Write(chunk.Message?.Content);
}
```

### OpenAI Streaming

```csharp
// Streaming text completion
var request = new OpenAIGenerateCompletionRequest
{
    Model = "llama2",
    Prompt = "Write a story about",
    MaxTokens = 200,
    Temperature = 0.8f
};

Console.WriteLine("Streaming completion:");
await foreach (var chunk in sdk.OpenAI.GenerateCompletionStreamAsync(request))
{
    Console.Write(chunk?.Choices?[0]?.Text);
}

// Streaming chat completion
var chatRequest = new OpenAIGenerateChatCompletionRequest
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
await foreach (var chunk in sdk.OpenAI.GenerateChatCompletionStreamAsync(chatRequest))
{
    Console.Write(chunk?.Choices?[0]?.Text);
}
```

## ⚠️ Error Handling

The SDK handles errors gracefully and returns null for failed operations:

```csharp
try
{
    var result = await sdk.Ollama.GenerateCompletion(request);
    
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
var sdk = new SharpAISdk("http://localhost:8000");
sdk.TimeoutMs = 120000; // 2 minutes
```

### Logging Configuration

```csharp
var sdk = new SharpAISdk("http://localhost:8000");
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
