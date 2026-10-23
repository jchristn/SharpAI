# Standalone model serving

`SharpAI.Serve` installs the `sharpai` command. It serves one GGUF file on macOS, Windows,
or Linux through Ollama-compatible and OpenAI-compatible HTTP APIs. It loads the model
before opening its listener. Inference executes locally using SharpAI's shared engine and
HTTP handlers, including chat templates, tool calls, streaming, token usage, and context admission.
Compatibility covers the endpoints below; this is not a vLLM implementation or its complete API.

There is no authentication, dashboard, registry, database, settings file, remote download,
or management API. The model stays in its original location. The managed SharpAI server
remains the choice for authenticated, multitenant operation and the web/TUI administration clients.
Standalone serving defaults to loopback. Choose the bind address deliberately when exposing this
unauthenticated service to other machines.

## Install, update, and remove

Install the [.NET SDK](https://dotnet.microsoft.com/download) 8 or later. The package includes
.NET 8 and .NET 10 implementations and permits major runtime roll-forward. NuGet tool packages
are framework-dependent; this is not a self-contained executable.

Once the package has been published to your configured feed:

```sh
dotnet tool install --global SharpAI.Serve
sharpai --help
dotnet tool update --global SharpAI.Serve
dotnet tool uninstall --global SharpAI.Serve
```

Keep `$HOME/.dotnet/tools` on PATH on macOS/Linux, or `%USERPROFILE%\.dotnet\tools` on Windows.
The installed command runs from any directory. Model, template, library, and log paths may be
absolute or relative to the directory from which you invoke it. Spaces in paths require quoting.
Installation does not change your working directory or copy your models.

For an unreleased checkout, build and install from a local package feed:

```sh
dotnet pack src/SharpAI.Serve -c Release -o artifacts/serve
# Use the absolute path of artifacts/serve as <feed>.
dotnet tool install --global SharpAI.Serve --add-source <feed> --version 4.0.1
```

An isolated installation can use `--tool-path <directory>` instead of `--global` for **all three**
install/update/uninstall commands. Put that directory on PATH. Updating a running server requires
stopping it first and starting it again after the update. Building a package does not publish it. The repository release workflow packs and publishes the
tool on release tags/manual dispatch when `NUGET_API_KEY` is configured.
See Microsoft's [tool installation guidance](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools).

## Run a model

```sh
sharpai serve /models/assistant.gguf --served-model-name assistant --port 8000
# Windows example:
sharpai serve "C:\Models\assistant.gguf" --served-model-name assistant --port 8000
```

Use `sharpai serve --help` or `sharpai /?` or `sharpai usage` for the complete command reference. For source runs:

```sh
dotnet run --project src/SharpAI.Serve -f net8.0 -- serve /models/assistant.gguf
```

```sh
curl http://127.0.0.1:8000/ready
curl http://127.0.0.1:8000/v1/chat/completions \
  -H 'Content-Type: application/json' \
  -d '{"model":"assistant","messages":[{"role":"user","content":"Hello"}],"max_tokens":128}'
curl http://127.0.0.1:8000/api/chat \
  -H 'Content-Type: application/json' \
  -d '{"model":"assistant","messages":[{"role":"user","content":"Hello"}],"stream":false}'
```

OpenAI-compatible clients should use `http://127.0.0.1:8000/v1` as their base URL. If a client
requires an API key value, any placeholder works; this server does not authenticate it. Existing
SharpAI SDK inference calls use the server origin without a bearer. SDK management methods and
TUI/web administration require the managed server and cannot operate against this reduced host.

## Options

| Option | Default | Meaning |
| --- | --- | --- |
| `--host` | `127.0.0.1` | IP address or `localhost`; `0.0.0.0` exposes IPv4 interfaces |
| `--port` | `8000` | Listen port, 1–65535 |
| `--served-model-name` | Filename without extension | Exact model name required in requests |
| `--api` | `both` | `ollama`, `openai`, or `both` |
| `--backend` | `auto` | `auto`, `cpu`, `cuda`, or `metal`; explicit choices must load |
| `--context-size` | Trained model context, or 4096 | Positive context window in tokens |
| `--max-tokens` | `2048` | Default generated-token budget, including reasoning |
| `--threads` | Automatic | Positive generation thread count |
| `--batch-threads` | Automatic | Positive prompt-processing thread count |
| `--gpu-layers` | Automatic | Nonnegative layer count or `all` |
| `--main-gpu` | `0` | Nonnegative CUDA device index |
| `--parallel` | `1` | Concurrent generation slots, 1–1024; busy requests fail with 429 |
| `--temperature` | `0.6` | Sampling temperature, 0–2 |
| `--seed` | Unspecified | Signed 32-bit sampling seed |
| `--chat-template` | Embedded template/family fallback | Existing nonempty template file, at most 1 MiB; validated before listening |
| `--native-library` | Automatic native resolution | Existing backend library; requires an explicit `--backend` |
| `--log-file` | None | Optional log file; creates its parent directory |
| `--log-level` | `debug` | `debug`, `info`, `warning`, or `error` |
| `--quiet` | Off | Suppress application console logs; startup errors and native-library output may remain |
| `-h`, `--help`, `/?`, `usage` | — | Print usage without loading a model |
| `--version` | — | Print tool assembly version |

Use `--option=value` or `--option value`. Repeated and unknown options fail. `--` ends option
parsing for filenames beginning with `-`. Generation requests override the CLI defaults using
OpenAI `max_tokens`/`temperature`/`seed` or Ollama `options.num_predict`/`temperature`/`seed`.
The context window still limits prompt plus generated tokens. A small-context model may need a
lower output budget. This host does not read the managed server's `sharpai.json` or initialize
`~/.sharpai`; CLI runtime settings override inherited equivalents in the process environment.

CPU native libraries and macOS Metal support ship with the portable tool. CUDA binaries are
excluded from the NuGet package to keep it within feed size limits. For CUDA, obtain the matching
LLamaSharp 0.30.0 CUDA12 native library and its companion dependencies, then supply
`--backend cuda --native-library /absolute/path/to/libllama.so` (Windows: `llama.dll`).
Keep companion native libraries beside it and satisfy the platform's CUDA runtime requirements.
Explicit unavailable backends fail startup. Auto selection can fall back to CPU. Native support
also depends on OS, architecture, drivers, and model format; CI exercises CPU on macOS, Windows,
and Linux. GPU execution requires appropriate hardware.

## Startup and request logging

Startup emits the SharpAI ASCII banner, tool version, loading message, and ready endpoint through
SyslogLogging. Console logging is enabled by default, including when stdout is redirected.
The default level is `debug`, so REST requests emit processing and completion records:

```text
2026-10-22 22:39:31.928144 Debug processing POST /api/chat from 127.0.0.1 (336 bytes)
2026-10-22 22:40:01.818745 Debug completed POST /api/chat from 127.0.0.1: 200 (31826ms, TTFT 6188ms, 82 input tok, 261 output tok)
```

Timestamps are UTC with six fractional digits. Duration covers HTTP request handling through the
completed response, including streaming. TTFT is the engine's measured first-token duration;
token counts include reasoning before output filtering. Timings and counts appear when engine
statistics are available; probes, rejected requests, and embedding calls omit unavailable metrics.
Request logs include method, path, source address, declared body length, and response status.
They omit query values, headers, prompts, and generated content. Runtime engine messages also use
the configured logger. This does not configure a remote UDP syslog destination.

`--log-level info` hides debug request logs while retaining the startup banner and informational
messages. `--quiet` disables application console logs; an explicit `--log-file` still receives
messages at the selected level. No log file is created unless requested. Native backend output,
such as Metal capability probing, may still appear outside the application logger. A Metal tensor
probe can fall back to the embedded library and still reach `Ready` successfully.

## HTTP contract

All inference POST bodies require `Content-Type: application/json` and an exact `model` alias.
The shared [REST reference](../REST_API.md) describes inference request/response fields,
tool calls, stop behavior, SSE/NDJSON framing, and usage. Its managed-server authentication,
permissions, model lifecycle, and management endpoints do **not** apply to this host.

| Method | Route | Availability and behavior |
| --- | --- | --- |
| GET, HEAD | `/health` | All modes; process healthy after listener starts |
| GET | `/ready` | All modes; loaded alias and `context_size` |
| OPTIONS | Any path | CORS preflight |
| GET | `/api/tags` | Ollama; one configured model |
| GET | `/api/ps` | Ollama; the resident configured model |
| GET | `/api/version` | Ollama; SharpAI version |
| POST | `/api/show` | Ollama; model details, context, capability flags |
| POST | `/api/generate` | Ollama text generation; `prompt` required |
| POST | `/api/chat` | Ollama chat; nonempty `messages` required |
| POST | `/api/embed` | Ollama embeddings; string or nonempty string-array `input` |
| GET | `/v1/models` | OpenAI; one model in `data` |
| POST | `/v1/completions` | OpenAI text generation |
| POST | `/v1/chat/completions` | OpenAI chat; nonempty `messages` required |
| POST | `/v1/embeddings` | OpenAI embeddings; string or nonempty string-array `input` |

Ollama generation defaults to streaming NDJSON; OpenAI defaults to a single JSON response.
Set `stream` explicitly to choose behavior. OpenAI streams finish with `data: [DONE]`; optional
`stream_options.include_usage` supplies final usage. Embedding-only models reject generation;
generation-only models reject embeddings. `/api/show` returns SharpAI capability flags as
`{"embeddings":true,"completions":false}` rather than Ollama's capability-name array.

Invalid JSON/request/context is 400, unknown model or disabled/absent route is 404, incompatible
model capability is 403, saturated generation capacity is 429, and unexpected inference failures
are 500. OpenAI error bodies use an `error` object; Ollama errors use an `error` string. Clients
must also handle a connection ending during a stream. There are no model pull/import/unload,
settings, authentication, tenant, metrics, dashboard, or administrative routes.

Ctrl+C/SIGTERM cancels active work, stops the listener, and disposes the model. Exit status is 0
for help/version/normal shutdown, 2 for invalid CLI arguments, and 1 for startup/runtime failure
(including bad GGUF, unavailable backend, or occupied port). Native initialization may finish
before a pending shutdown is observed.

## Validation and package lifecycle

```sh
dotnet run --project src/Test.Serve -f net8.0
dotnet run --project src/Test.Serve -f net10.0
python3 scripts/test-serve.py --model /models/test.gguf --embedding-model /models/embeddings.gguf
python3 scripts/test-serve.py --framework net10.0 --model /models/test.gguf
python3 scripts/test-serve-package.py --model /models/test.gguf
```

The live harness uses real native models, no authentication or mocks, and checks both protocol
families, terminal stream records, budgets, errors, capability gates, shutdown, and filesystem
side effects from an unrelated directory. The package harness packs two versions, installs,
executes through PATH from another directory, updates, runs real inference, uninstalls, and checks
that the command shim is removed. It uses an isolated tool directory and never alters your global
tools or publishes a package. The shared deterministic suite also runs through xUnit/NUnit.
Import [the standalone Postman collection](../postman/SharpAI.Serve.postman_collection.json)
for manual HTTP exercises; its default `fixture` model alias should match `--served-model-name`.
