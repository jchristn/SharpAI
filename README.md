<div align="center">
  <img src="assets/logo.png" width="256" height="256" alt="SharpAI logo">
</div>

# SharpAI

**Local AI inference. Your models. Your infrastructure. Familiar APIs.**

## Overview

Run GGUF models on systems you control and bring AI into your applications through
Ollama-compatible and OpenAI-compatible APIs. SharpAI combines a local inference engine,
a server, a web dashboard, a terminal dashboard, and client SDKs in one open-source project.

Deploy on a workstation, a private server, or infrastructure you manage. Choose the models,
control access, and see how requests perform. Inference runs on your hardware without requiring
a hosted AI service or paying a provider for each token.

[Docker and web quick start](#docker-and-web-quick-start) ·
[TUI quick start](#tui-quick-start) ·
[Model server quick start](#model-server-quick-start) ·
[REST API reference](REST_API.md) ·
[Deployment guide](DEPLOYMENT_GUIDE.md) ·
[Changelog](CHANGELOG.md)

## What is SharpAI

SharpAI is a .NET-based platform for running local language and embedding models in GGUF format.
Use it in two ways:

- **Deploy an inference server.** Give applications a shared HTTP service with familiar chat,
  completion, embedding, and model-management APIs. Operate it through the web dashboard or TUI.
- **Embed inference in a .NET application.** Use the SharpAI library directly when your application
  should own the model lifecycle and run inference in its own process.

The server handles inference; the dashboards and SDKs connect to it. A TUI running on your laptop
can manage a remote SharpAI server without downloading its models to your laptop.

SharpAI uses [LLamaSharp](https://github.com/SciSharp/LLamaSharp) for inference. Model capabilities,
quality, speed, and memory requirements depend on the GGUF model and hardware you choose.
API compatibility covers the implemented Ollama and OpenAI operations; see the
[SDK guides](#features) for supported calls.

## Use Cases

- **Private assistants and internal tools.** Run chat, summarization, drafting, and text-processing
  workflows inside infrastructure your organization manages.
- **Semantic search and retrieval.** Generate embeddings with a compatible model for document search,
  similarity matching, or the retrieval step of a RAG application.
- **Local development and experimentation.** Compare models, prompts, and sampling settings in the
  playground before connecting an application.
- **Shared inference for teams.** Offer a centrally managed model service with tenant boundaries,
  users, credentials, roles, and permissions.
- **Disconnected deployments.** Run inference without an external model provider once the models
  and runtime dependencies are available locally.
- **AI inside .NET products.** Embed the inference library in desktop applications, services, or
  internal tooling instead of operating a separate inference server.

## Benefits

- **Control where inference happens.** Choose the machines, network boundaries, model storage,
  and operational policies that fit your deployment. Configure history and telemetry to match
  your data-handling requirements.
- **Build on familiar integrations.** Use supported Ollama and OpenAI API patterns, or the C#,
  JavaScript/TypeScript, and Python SDKs, to connect existing applications.
- **Choose your models.** Work with supported GGUF models and quantizations that fit your workload
  and memory budget. Pull models from Hugging Face and manage them from one interface.
- **Plan around hardware costs.** Serve inference on infrastructure you provision, without a required
  hosted inference subscription or provider token charges. Hardware and operating costs remain yours.
- **Understand performance.** Inspect request and inference history, token usage, timings, and activity
  charts instead of treating inference as an opaque service.
- **Work from a browser or terminal.** Use the web dashboard for visual administration and the TUI
  for keyboard-driven work on local or remote servers.
- **Adapt the software.** The MIT license lets you inspect, modify, and integrate SharpAI into
  commercial or internal products.

## Features

| Area | Capabilities |
| --- | --- |
| Inference | Chat, text completion, embeddings, streaming responses, cancellation, and configurable generation parameters |
| Model management | GGUF downloads, capability detection, model inspection, resident-model visibility, unload, and deletion |
| Hardware | CPU inference, NVIDIA CUDA on supported Windows/Linux deployments, and Metal on native Apple Silicon macOS |
| Web dashboard | Overview, models, playground, API explorer, histories, observability, access administration, and settings |
| Terminal dashboard | Remote HTTP connections, keyboard navigation, Markdown chat, history charts, themes, and localization |
| Reusable configuration | Scoped system-prompt and model-configuration libraries for shared defaults and personal experimentation |
| Access control | Authentication, tenants, users, bearer credentials, roles, permissions, and audit history |
| Operations | Request/inference history, token and timing details, JSON logs, and OpenTelemetry integration |
| Storage | SQLite by default; PostgreSQL, MySQL, and SQL Server support |
| Integration | Ollama-compatible and OpenAI-compatible APIs, built-in OpenAPI/Swagger documentation, and three client SDKs |

Docker runs Linux containers. For Metal acceleration on Apple Silicon, run SharpAI natively on
macOS; Docker on macOS does not expose Metal to the container. NVIDIA container deployments require
GPU access configured on the host. See the [deployment guide](DEPLOYMENT_GUIDE.md) for requirements.

Explore the component and operational guides:

- [C# SDK](sdk/csharp/README.md), [JavaScript/TypeScript SDK](sdk/js/README.md), and [Python SDK](sdk/python/README.md)
- [Web dashboard](dashboard/README.md) and [terminal dashboard](docs/TUI.md)
- [Model download sources](docs/MODEL_PULL_SOURCES.md) and [inference operations](docs/OPERATIONS.md)
- [Prompt and configuration libraries](docs/SCOPED_LIBRARIES.md)
- [Access administration](docs/DASHBOARD_ADMINISTRATION.md) and [request/inference history](docs/HISTORY_TELEMETRY.md)
- [Observability stack](docker/telemetry/README.md) and [Postman resources](postman/README.md)

## Authentication and Multitenancy

The managed server uses the access controls below. [Standalone serving](docs/SERVE.md) is an
explicitly unauthenticated, single-model alternative for local inference.


SharpAI authenticates both people and applications with bearer tokens. Web and TUI users sign in
with a tenant, email, and password to receive an expiring, revocable session. Applications use API
credentials created for a user, with their own permissions, scope, active status, and optional expiry.
Send either token as `Authorization: Bearer <token>`. Only health/readiness probes and the login
operation are anonymous; API documentation and metrics also require authentication.

A **platform administrator** manages tenants and can administer across them. A **tenant administrator**
manages users and access within their own tenant. Roles and permissions determine which resources
other users and credentials can read, change, or execute. A credential cannot exceed its owner's
permissions, and changing a tenant ID in a request does not grant access to another tenant.

Tenants separate identities, grants, private/shared libraries, and tenant-scoped history. Models,
resident weights, inference capacity, and server settings belong to the shared server. Deploy separate
SharpAI servers when you need independent model storage or hardware isolation.

System prompts and model configurations can be shared system-wide, published as tenant defaults,
or kept private to a user. Private items remain private even from administrators; duplicate a shared
item to customize your own copy.

For a fresh installation, use the initial sign-in below and change its password before sharing access.
Compose also creates a limited model/inference token in `docker/auth/default-bearer-token`;
create separate credentials for your applications rather than sharing an administrator session.

See the [REST authentication contract](REST_API.md#authentication-and-multitenancy),
[access administration guide](docs/DASHBOARD_ADMINISTRATION.md), and
[Postman collection](postman/SharpAI.postman_collection.json) for login, credential creation, role assignments,
revocation, token reveal/rotation, and tenant-scoped requests.

## Docker and web quick start

Install Git and Docker with Docker Compose, then clone the repository:

```sh
git clone https://github.com/jchristn/SharpAI.git
cd SharpAI
```

The checked-in deployment uses server and dashboard images tagged `v4.0.1`.
On macOS or Linux, start the bundled stack from the repository root:

```sh
cd docker
./update.sh
```

On Windows PowerShell:

```powershell
cd docker
if (!(Test-Path sharpai.db)) { New-Item -ItemType File sharpai.db | Out-Null }
docker compose pull
docker compose up -d
```

The bundled Compose stack starts the server, web dashboard, and observability services.
The dashboard waits for the server to become ready.

| Service | Address |
| --- | --- |
| Web dashboard | [http://localhost:3000](http://localhost:3000) |
| Inference server | [http://localhost:8000](http://localhost:8000) |
| Grafana | [http://localhost:9400](http://localhost:9400) |

Open the web dashboard and sign in. A fresh installation seeds these credentials:

| Field | Value |
| --- | --- |
| Tenant | `default` |
| Email | `admin@sharpai.local` |
| Password | `password` |

Existing installations retain their configured accounts. Change the initial password before
sharing the deployment.

To run your first inference:

1. Open **Models** and pull a supported GGUF model from Hugging Face. Choose one that fits your
   available memory. Configure a Hugging Face token in **Settings** if the repository requires access.
2. Open **Playground**. The first inference-capable model is selected automatically when available.
3. Send a message. Try a saved system prompt or model configuration to adjust its behavior.
4. Open the history pages to inspect requests, inference timings, and token usage.

For application access, create a bearer credential under **Credentials** with the permissions the
application needs, then follow your [SDK's connection instructions](#features).
Compose also provisions a limited model/inference credential at `docker/auth/default-bearer-token`.

The supplied Compose files persist models in a named Docker volume and configuration, database,
authentication assets, and logs through mounts. See the [deployment guide](DEPLOYMENT_GUIDE.md)
for persistence, networking, native installation, and GPU setup. For a smaller CPU deployment or
an NVIDIA deployment, see `docker/compose-cpu.yaml` and `docker/compose-cuda.yaml`.

## Model server quick start

`SharpAI.Serve` is the single-model server project. It runs on macOS, Windows, and Linux and serves
an existing GGUF through both Ollama-compatible and OpenAI-compatible APIs on the same port.
It requires no registry, database, authentication, or dashboard.

Install the .NET 8 or .NET 10 SDK, then install the tool and start a model:

```sh
# Once available on your configured NuGet feed:
dotnet tool install --global SharpAI.Serve
sharpai serve /models/assistant.gguf --served-model-name assistant --port 8000
```

Keep `$HOME/.dotnet/tools` (macOS/Linux) or `%USERPROFILE%\.dotnet\tools` (Windows) on PATH.
The command runs from any directory; replace the model path with your own GGUF file.
It binds to `127.0.0.1` by default and accepts requests without a bearer token. The model loads
before the listener starts. Use `sharpai /?` for options and Ctrl+C to stop.

To run directly from an unreleased checkout, use this command from the repository root:

```sh
dotnet run --project src/SharpAI.Serve -f net8.0 -- serve /models/assistant.gguf --served-model-name assistant --port 8000
```

Use `-f net10.0` with the .NET 10 SDK. For a NuGet tool installation from this checkout,
see the [local-feed instructions](docs/SERVE.md#install-update-and-remove).

Application text logs consistently use `timestamp Severity [Subsystem] message`.
Startup displays the SharpAI banner; timestamped REST logs include status, duration, and available
TTFT/token counts. Use `--log-format json --log-detail detailed` for compact structured records
with phase timings, throughput, stream counters, and request IDs. `--log-detail minimal` emits
completion summaries; `--log-level info` hides request logs, and `--quiet` suppresses application console logs.

With the .NET tool directory on PATH, `sharpai` runs from any working directory. Use
`dotnet tool update --global SharpAI.Serve` to update and
`dotnet tool uninstall --global SharpAI.Serve` to remove it. From an unreleased checkout,
pack/install from a local feed or run the project directly. See the [standalone guide](docs/SERVE.md)
for prerequisites, all CLI options, native backend support, HTTP examples, source installation,
and validation. The web dashboard and TUI administration client use the managed server.

## TUI quick start

Use the terminal dashboard on macOS, Windows, or Linux to connect to the same server.
An interactive terminal and a running SharpAI server are required. Models remain on the server.

With the .NET 8 SDK installed, run this command from the repository root, in a shell or PowerShell:

```sh
dotnet run --project src/SharpAI.Tui -f net8.0 -- --server http://127.0.0.1:8000
```

Replace the URL to connect to a remote deployment. With the .NET 10 SDK, use `-f net10.0`.
Use a terminal at least 80 columns by 24 rows; navigation becomes a drawer below 100 columns.

1. Press **Ctrl+P** and choose **Sign in**, then enter your tenant, email, and password.
   **Use bearer token** is also available, subject to that credential's permissions.
2. Press **F2** to navigate to **Models** or **Playground**.
3. Type a message and press **Enter** to send. **Ctrl+J** inserts a newline.
4. Type `/?` for chat commands, including `/clear`, `/compact`, `/model`, and `/stop`.
   `/compact` summarizes earlier context with the selected model while preserving the visible transcript.

Press **F1** for help, **F5** to refresh, and **Ctrl+Q** to quit. Preferences live in
`~/.sharpai/tui.json`; interactively entered passwords and bearer tokens stay in memory.

You can also build a self-contained archive that runs without an installed .NET runtime:

```sh
# macOS Apple Silicon; use osx-x64 for Intel
./build-tui.sh --rid osx-arm64
# Linux; use linux-arm64 for ARM64
./build-tui.sh --rid linux-x64
```

On Windows:

```powershell
.\build-tui.bat --rid win-x64
```

Use `win-arm64` for Windows ARM64. Extract the generated archive under `installers/`, then launch
`./sharpai-tui --server http://127.0.0.1:8000` on macOS/Linux or
`.\sharpai-tui.exe --server http://127.0.0.1:8000` in PowerShell.

See the [terminal guide](docs/TUI.md) for token-file profiles, themes, localization, packaging,
and validation, or follow the [remote-server walkthrough](examples/tui/README.md).
The [implementation plan](TUI_PLAN.md) tracks remaining parity and release gates.

## Issues and Enhancements

Report bugs and propose improvements through [GitHub Issues](https://github.com/jchristn/SharpAI/issues).
For a bug, include your SharpAI version, operating system, deployment method, model name, steps to
reproduce, and relevant logs with credentials and sensitive content removed.

For an enhancement, describe the task you want to accomplish and how the change would help.
See the [changelog](CHANGELOG.md) for release history.

## License (MIT)

SharpAI is available under the [MIT License](LICENSE.md), including for commercial use.
Model weights and third-party dependencies have their own licenses; review the terms for the models
you deploy.
