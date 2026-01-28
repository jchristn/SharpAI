# Changelog

## Current Version

v1.0.0

- Initial release of SharpAI SDK
- C# SDK for interacting with SharpAI server
- Support for both Ollama and OpenAI compatible API endpoints
- Model management capabilities
  - Pull models with streaming progress updates
  - List local models
  - Delete models
- AI Operations (Ollama API)
  - Text completion (non-streaming and streaming)
  - Chat completion (non-streaming and streaming)
  - Embeddings generation (single and multiple texts)
- AI Operations (OpenAI API)
  - Text completion (non-streaming and streaming)
  - Chat completion (non-streaming and streaming)
  - Embeddings generation (single and multiple texts)
- HTTP client functionality
  - GET, POST, DELETE request support
  - Streaming response handling
  - Chunked transfer encoding support
  - Configurable timeouts and logging
- JSON serialization
  - Built-in JSON serialization with customizable options
  - Support for enum string conversion
  - Case-insensitive property matching
- Logging and debugging
  - Request/response logging
  - Custom logger support
  - Debug mode for detailed output
- Dependencies
  - SharpAI (1.0.14) - Core SharpAI functionality
  - RestWrapper (3.1.8) - HTTP client wrapper
  - System.Text.Json (9.0.9) - JSON serialization
- Platform support
  - .NET 8.0 minimum requirement
  - Cross-platform compatibility

## Previous Versions

Notes from previous versions will be placed here.
