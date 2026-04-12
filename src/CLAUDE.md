# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SharpAI is a .NET library for local AI inference with an optional Ollama-compatible API server. It provides direct model embedding capabilities and server deployment without cloud dependencies.

## Architecture

The solution consists of:

- **SharpAI** - Core library (.NET 8) containing the main AI functionality
- **SharpAI.Server** - Console application that provides an Ollama-compatible REST API server
- **Test.*** projects - Various test applications demonstrating different components

### Core Components

- **AIDriver** - Main entry point providing access to Chat, Completion, Embeddings, Models, and Vision APIs
- **Engines** - Model execution engines (LlamaSharpEngine for local inference)
- **Services** - ModelFileService for file management, ModelEngineService for engine coordination
- **Models** - Data models including Ollama-compatible request/response objects
- **Hosting** - Infrastructure for running models and managing resources

## Development Commands

### Build
```bash
dotnet build SharpAI.sln
```

### Run Tests
Individual test projects can be run with:
```bash
dotnet run --project Test.SharpAIDriver
dotnet run --project Test.LlamaSharpProvider
dotnet run --project Test.HuggingFace
dotnet run --project Test.PromptBuilder
```

### Run Server

**Development (from source):**
```bash
dotnet run --project SharpAI.Server
```

**Production (from build output):**
```bash
# Navigate to build output
cd SharpAI.Server/bin/Debug/net8.0  # or Release

# Use platform-specific startup script
./start-windows.bat  # Windows
./start-linux.sh     # Linux
./start-mac.sh       # macOS
```

The startup scripts automatically handle platform-specific library configuration.

### Docker Build
Use the provided batch script:
```bash
build-docker.bat <tag>
```

## Key Dependencies

- **LLamaSharp** - Local model inference engine with CPU, CUDA12, and Metal backends
- **Watson.ORM.Sqlite** - Database layer for model metadata
- **SwiftStack** - REST API framework for the server
- **RestWrapper** - HTTP client utilities
- **SyslogLogging** - Logging infrastructure

## Configuration

- Server settings are stored in `./sharpai.json` (auto-created on first run)
- Models are stored in `./models/` directory by default
- Database file: `./sharpai.db`
- **HuggingFace API Key required** for model downloads (get from: https://huggingface.co/settings/tokens)

### Required Configuration

Add your HuggingFace API key to `sharpai.json` before pulling models:
```json
{
  "HuggingFace": {
    "ApiKey": "hf_YOUR_API_KEY_HERE"
  }
}
```

### Runtime Backend Configuration

The server automatically detects CPU/GPU capabilities:
- **Windows/Linux**: Auto-detects NVIDIA GPU for CUDA acceleration
- **macOS Apple Silicon**: Auto-detects Metal GPU acceleration
- **macOS Intel**: Uses CPU
- Can be overridden with `"Runtime": {"ForceBackend": "cpu"}`, `"cuda"`, or `"metal"`

## API Compatibility

SharpAI.Server provides Ollama-compatible endpoints:
- `/api/pull` - Download models
- `/api/delete` - Remove models
- `/api/tags` - List local models
- `/api/embed` - Generate embeddings
- `/api/generate` - Text completion
- `/api/chat` - Chat completions

## Model Support

- Supports GGUF format models via LlamaSharp
- HuggingFace integration for model downloads
- Multi-modal support with vision projector files (mmproj GGUF)
- Local inference without cloud dependencies

## Coding Standards (STRICTLY ENFORCED)

### File Structure and Organization
- Namespace declaration at the top, using statements INSIDE the namespace block
- Microsoft/system library usings first (alphabetical), then other usings (alphabetical)
- One class or one enum per file - no nesting multiple classes/enums
- Enable nullable reference types: `<Nullable>enable</Nullable>` in project files

### Naming Conventions
- Private class member variables: underscore + PascalCase (e.g., `_FooBar`, not `_fooBar`)
- No `var` keyword - use explicit types
- Meaningful names with context

### Documentation
- All public members, constructors, and public methods MUST have XML documentation
- NO documentation on private members or methods
- Document default/min/max values where appropriate
- Document exceptions using `/// <exception>` tags
- Document thread safety guarantees
- Document nullability in XML comments

### Property Implementation
- All public members should have explicit getters/setters with backing variables
- Include range or null validation when value requires it

### Async/Await Patterns
- Use `.ConfigureAwait(false)` where appropriate
- Every async method should accept `CancellationToken` (unless class has token member)
- Check cancellation at appropriate places
- For methods returning `IEnumerable`, create async variant with `CancellationToken`

### Error Handling
- Use specific exception types, not generic `Exception`
- Include meaningful error messages with context
- Consider custom exception types for domain-specific errors
- Use exception filters: `catch (SqlException ex) when (ex.Number == 2601)`
- Validate input parameters with guard clauses at method start
- Use `ArgumentNullException.ThrowIfNull()` for .NET 6+ or manual null checks
- Consider Result pattern or Option/Maybe types for methods that can fail

### Resource Management
- Implement `IDisposable`/`IAsyncDisposable` for unmanaged resources
- Use `using` statements or declarations for `IDisposable` objects
- Follow full Dispose pattern with `protected virtual void Dispose(bool disposing)`
- Always call `base.Dispose()` in derived classes

### Concurrency and Threading
- Document thread safety guarantees
- Use `Interlocked` operations for simple atomic operations
- Prefer `ReaderWriterLockSlim` over `lock` for read-heavy scenarios

### LINQ and Collections
- Prefer LINQ methods over manual loops when readability isn't compromised
- Use `.Any()` instead of `.Count() > 0` for existence checks
- Be aware of multiple enumeration - consider `.ToList()` when needed
- Use `.FirstOrDefault()` with null checks rather than `.First()`

### Configuration and Flexibility
- Avoid hard-coded constants for configurable values
- Use public members with backing private members set to reasonable defaults
- Document what different values mean and their effects

### Prohibited Patterns
- Do not use tuples unless absolutely necessary
- Do not make assumptions about opaque class members/methods - ask for implementation
- Proactively eliminate null exception possibilities