<div align="center">
  <img src="https://github.com/jchristn/sharpai/blob/main/assets/logo.png" width="256" height="256">
</div>

# @sharpai/sdk

**A TypeScript SDK for interacting with SharpAI server instances - providing Ollama and OpenAI compatible API wrappers for local AI inference.**

<p align="center">
  <img src="https://img.shields.io/badge/TypeScript-007ACC?style=for-the-badge&logo=typescript&logoColor=white" />
  <img src="https://img.shields.io/badge/Node.js-339933?style=for-the-badge&logo=nodedotjs&logoColor=white" />
  <img src="https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge" />
</p>

<p align="center">
  <strong>A TypeScript SDK for SharpAI - Local AI inference with Ollama and OpenAI compatible APIs</strong>
</p>

<p align="center">
  Embeddings | Completions | Chat | Model Management | Streaming Support
</p>

**IMPORTANT** - @sharpai/sdk assumes you have deployed the SharpAI REST server. If you are integrating a SharpAI library directly into your code, use of this SDK is not necessary.

---

## Features

- **Ollama API Compatibility** - Full support for Ollama API endpoints and models
- **OpenAI API Compatibility** - Complete OpenAI API compatibility for seamless integration
- **Model Management** - Download, list, and delete models with streaming progress updates
- **Multiple Inference Types**:
  - Text embeddings generation
  - Text completions (streaming and non-streaming)
  - Chat completions (streaming and non-streaming)
- **Streaming Support** - Real-time token streaming for completions and chat
- **Full TypeScript Support** - Complete type definitions for all APIs
- **Error Handling** - Graceful error handling with null returns (no exceptions thrown)
- **Configurable Logging** - Built-in request/response logging capabilities

## Installation

```bash
npm install @sharpai/sdk
```

Or with yarn:

```bash
yarn add @sharpai/sdk
```

## Quick Start

### Basic Usage

```typescript
import { SharpAISdk } from '@sharpai/sdk';

// Initialize the SDK
const sdk = new SharpAISdk('http://localhost:8000');

// List available models
const models = await sdk.ollama.listLocalModels();
console.log(`Found ${models?.length ?? 0} models`);

// Generate a completion
const result = await sdk.ollama.generateCompletion({
  model: 'llama2',
  prompt: 'The meaning of life is',
  options: {
    temperature: 0.7,
    num_predict: 100,
  },
});

console.log(`Completion: ${result?.response}`);
```

### With Logging

```typescript
const sdk = new SharpAISdk('http://localhost:8000');
sdk.logRequests = true;
sdk.logResponses = true;
sdk.logger = (level, message) => console.log(`[${level}] ${message}`);
```

## API Reference

### SharpAISdk Class

The main SDK class that provides access to all functionality.

#### Constructor

```typescript
const sdk = new SharpAISdk(endpoint: string);
```

- `endpoint`: SharpAI server endpoint URL

#### Properties

- `endpoint`: Server endpoint URL (readonly)
- `timeoutMs`: Request timeout in milliseconds (default: 300000 / 5 minutes)
- `logRequests`: Enable request logging
- `logResponses`: Enable response logging
- `logger`: Custom logger callback `(level: string, message: string) => void`

#### Main API Groups

- `ollama`: Ollama API methods
- `openAI`: OpenAI API methods

## Ollama API Methods

### Model Management

```typescript
// List local models
const models = await sdk.ollama.listLocalModels();

// Pull a model with streaming progress
const pullRequest = { model: 'llama2' };

for await (const progress of sdk.ollama.pullModel(pullRequest)) {
  console.log(`Status: ${progress.status}`);
  if (progress.status === 'success') break;
}

// Delete a model
await sdk.ollama.deleteModel({ model: 'llama2' });
```

### Text Completions

```typescript
// Non-streaming completion
const result = await sdk.ollama.generateCompletion({
  model: 'llama2',
  prompt: 'The future of AI is',
  options: {
    temperature: 0.7,
    num_predict: 100,
  },
});

console.log(`Completion: ${result?.response}`);

// Streaming completion
for await (const chunk of sdk.ollama.generateCompletionStream({
  model: 'llama2',
  prompt: 'Write a poem about',
  options: { temperature: 0.8, num_predict: 200 },
})) {
  process.stdout.write(chunk.response ?? '');
}
```

### Chat Completions

```typescript
// Non-streaming chat
const chatResult = await sdk.ollama.generateChatCompletion({
  model: 'llama2',
  messages: [{ role: 'user', content: 'Hello, how are you?' }],
  options: { temperature: 0.7, num_predict: 100 },
});

console.log(`Assistant: ${chatResult?.response}`);

// Streaming chat
for await (const chunk of sdk.ollama.generateChatCompletionStream({
  model: 'llama2',
  messages: [{ role: 'user', content: 'Tell me a joke' }],
  options: { temperature: 0.7, num_predict: 150 },
})) {
  process.stdout.write(chunk.message?.content ?? '');
}
```

### Embeddings

```typescript
// Single text embedding
const embeddingResult = await sdk.ollama.generateEmbeddings({
  model: 'llama2',
  input: 'This is a test sentence',
});

console.log(`Embedding dimensions: ${embeddingResult?.embeddings?.length}`);

// Multiple text embeddings
const multipleResult = await sdk.ollama.generateEmbeddings({
  model: 'llama2',
  input: ['First text', 'Second text', 'Third text'],
});
```

## OpenAI API Methods

### Text Completions

```typescript
// Non-streaming completion
const result = await sdk.openAI.generateCompletionAsync({
  model: 'llama2',
  prompt: 'The future of AI is',
  max_tokens: 100,
  temperature: 0.7,
});

console.log(`Completion: ${result?.choices?.[0]?.text}`);

// Streaming completion
for await (const chunk of sdk.openAI.generateCompletionStreamAsync({
  model: 'llama2',
  prompt: 'Write a story about',
  max_tokens: 200,
  temperature: 0.8,
})) {
  process.stdout.write(chunk?.choices?.[0]?.text ?? '');
}
```

### Chat Completions

```typescript
// Non-streaming chat
const result = await sdk.openAI.generateChatCompletionAsync({
  model: 'llama2',
  messages: [{ role: 'user', content: 'Hello, how are you?' }],
  max_tokens: 100,
  temperature: 0.7,
});

console.log(`Assistant: ${result?.choices?.[0]?.message?.content}`);

// Streaming chat
for await (const chunk of sdk.openAI.generateChatCompletionStreamAsync({
  model: 'llama2',
  messages: [{ role: 'user', content: 'Tell me a joke' }],
  max_tokens: 150,
  temperature: 0.7,
})) {
  process.stdout.write(chunk?.choices?.[0]?.text ?? '');
}
```

### Embeddings

```typescript
// Single text embedding
const result = await sdk.openAI.generateEmbeddingsAsync({
  model: 'llama2',
  input: 'This is a test sentence',
});

console.log(`Embedding dimensions: ${result?.data?.[0]?.embedding?.length}`);

// Multiple text embeddings
const multipleResult = await sdk.openAI.generateEmbeddingsAsync({
  model: 'llama2',
  input: ['First text', 'Second text', 'Third text'],
});
```

## Helper Functions

The SDK provides helper functions for working with response objects:

```typescript
import {
  OllamaGenerateCompletionResultHelpers,
  OllamaGenerateEmbeddingsResultHelpers,
  OpenAIGenerateCompletionResultHelpers,
  OpenAIGenerateChatCompletionResultHelpers,
  SharpAIPullModelResponseHelpers,
} from '@sharpai/sdk';

// Get tokens per second from completion result
const result = await sdk.ollama.generateCompletion(request);
if (result) {
  const tokensPerSec = OllamaGenerateCompletionResultHelpers.getGenerationTokensPerSecond(result);
  console.log(`Generation speed: ${tokensPerSec?.toFixed(2)} tokens/sec`);
}

// Get formatted progress during model pull
for await (const progress of sdk.ollama.pullModel(request)) {
  const formatted = SharpAIPullModelResponseHelpers.getFormattedProgress(progress);
  console.log(`Progress: ${formatted}`);
}
```

## Error Handling

The SDK handles errors gracefully and returns null for failed operations:

```typescript
const result = await sdk.ollama.generateCompletion(request);

if (result === null) {
  console.log('Failed to generate completion or no result received');
} else {
  console.log(`Success: ${result.response}`);
}
```

## Cancellation

All methods support cancellation via AbortSignal:

```typescript
const controller = new AbortController();

// Cancel after 5 seconds
setTimeout(() => controller.abort(), 5000);

const result = await sdk.ollama.generateCompletion(request, controller.signal);
```

## Configuration

### Timeout Configuration

```typescript
const sdk = new SharpAISdk('http://localhost:8000');
sdk.timeoutMs = 120000; // 2 minutes
```

### Logging Configuration

```typescript
const sdk = new SharpAISdk('http://localhost:8000');
sdk.logRequests = true;
sdk.logResponses = true;
sdk.logger = (level, message) => {
  // Custom logging implementation
  console.log(`[${new Date().toISOString()}] [${level}] ${message}`);
};
```

## TypeScript Support

The SDK is written in TypeScript and provides complete type definitions for all APIs:

```typescript
import type {
  OllamaGenerateCompletionRequest,
  OllamaGenerateCompletionResult,
  OllamaChatMessage,
  OpenAIGenerateChatCompletionRequest,
  OpenAIGenerateChatCompletionResult,
} from '@sharpai/sdk';
```

## Requirements

- Node.js 18.0.0 or higher
- A running SharpAI server instance

## License

This project is licensed under the MIT License.
