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
| `--log-format` | `text` | `text` or compact `json`, one complete object per line |
| `--log-detail` | `standard` | Request detail: `off`, `minimal`, `standard`, `detailed` |
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

Choose format, request detail, and severity independently:

```sh
sharpai serve /models/assistant.gguf --log-format json --log-detail detailed
sharpai serve /models/assistant.gguf --log-format text --log-detail minimal
sharpai serve /models/assistant.gguf --log-format json --log-detail detailed --quiet --log-file /logs/sharpai.jsonl
```

| Request detail | Events and fields |
| --- | --- |
| `off` | No request access events; runtime/startup logging still follows `--log-level` |
| `minimal` | Completion only: request ID, method, path, source IP, declared request bytes, status, HTTP duration |
| `standard` | Start and completion, plus requested model/streaming, available token counts, engine TTFT, and cancellation |
| `detailed` | Standard plus engine phases, rates, timing provenance, stream counters, response length, runtime configuration, and trace identity |

All request events use Debug severity. `--log-level info` hides them while retaining startup and
informational engine events. Detail controls request events; it does not remove fields from engine
runtime events. `--quiet` disables application console logs; an explicit `--log-file` still receives
messages at the selected level. File output appends UTF-8 records. No log file is created unless requested.

Text output uses UTC timestamps with six fractional digits and retains the readable summary above.
Detailed text adds named `key=value` statistics. JSON output is **NDJSON**: every application log is a
complete compact object on one physical line, with the banner/newlines escaped inside strings and
no non-JSON header prefix. Startup and runtime messages use the same envelope. Structured engine
messages remain structured fields, rather than requiring another JSON parse of the message string.
CLI help/version are ordinary text; CLI/startup failures in JSON mode are JSON error objects on stderr.
Native backend diagnostics, such as Metal probing, can still write separate non-JSON stderr output.
Keep stderr separate from stdout when collecting JSON logs. This does not configure remote UDP syslog.

Example **standard** JSON completion (illustrative values):

```json
{"schema_version":1,"timestamp":"2026-10-22T22:40:01.818745Z","severity":"Debug","service":"sharpai","version":"4.0.1","event":"request.completed","message":"completed POST /api/chat from 127.0.0.1: 200 (31826ms, TTFT 6188ms, 82 input tok, 261 output tok)","request_id":"02104413-75cf-4537-8bed-33d73265c741","method":"POST","path":"/api/chat","source_ip":"127.0.0.1","request_content_length":336,"status":200,"duration_ms":31826.0,"model":"assistant","stream":true,"statistics_available":true,"ttft_ms":6188.0,"prompt_tokens":82,"completion_tokens":261,"total_tokens":343,"cancelled":false}
```

The `request_id` matches the `X-SharpAI-Request` response header and correlates start/completion
records under concurrent load. Started records have request metadata; status and inference values
are added at completion. Paths exclude query values. Request records contain no headers, prompts,
or generated content. Embeddings and rejected/probe requests have null unavailable inference fields;
they do not report fabricated token rates. An attached accumulator can contain zeros after an early
failure. `engine_timing_available` indicates whether engine total time was measured.

The detailed completion fields are:

| Fields | Units and interpretation |
| --- | --- |
| `duration_ms` | Monotonic HTTP wall time from pre-routing through response completion, including streaming |
| `ttft_ms` | Engine time to first generated token, including reasoning before filtering; not client-visible/network TTFT |
| `prompt_tokens`, `completion_tokens`, `total_tokens` | Measured engine token counts before output filtering; completion includes reasoning |
| `engine_total_ms` | Aggregate engine call duration; excludes additional HTTP/serialization time |
| `queue_ms`, `load_ms` | Engine admission wait and request-time model load; preloaded weights normally have zero request load time |
| `prompt_eval_ms`, `generation_ms` | Prompt processing and generation phases; zero can mean unavailable/not executed in the engine accumulator |
| `prompt_tokens_per_second` | `prompt_tokens * 1000 / prompt_eval_ms`; null without a positive measured denominator |
| `generation_tokens_per_second` | `completion_tokens * 1000 / generation_ms`; phase throughput, not end-to-end HTTP throughput |
| `statistics_available`, `engine_timing_available` | Whether an accumulator is attached and whether engine total timing is present |
| `timing_source` | `native` or `backend_step_wall_clock`; null before measured engine timing; wall-clock fallback phases are approximate |
| `cancelled` | Engine cancellation flag, or null when no engine accumulator exists |
| `outcome` | `ok`, `client_error`, `server_error`, or `cancelled`, from HTTP status and the engine cancellation flag |
| `request_content_length` | Declared HTTP body bytes; not a measurement of a chunked request body |
| `response_content_length` | Response content length for nonstreaming responses; null after streamed writes |
| `stream_chunks` | Successfully completed transport writes, including terminal/empty writes |
| `stream_payload_bytes` | NDJSON bytes for Ollama; UTF-8 SSE `data` bytes for OpenAI, including `[DONE]` but excluding SSE framing and HTTP chunk headers |
| `first_stream_write_ms` | HTTP elapsed time after the first nonempty stream write completes; not a client receipt timestamp |
| `stream_duration_ms` | Elapsed time between first and last nonempty stream writes; null without streamed data |
| `model`, `stream` | Requested model alias and streaming selection, including protocol defaults; null when no inference request was parsed |
| `backend`, `context_size`, `parallel` | Selected native backend, effective context tokens, and configured concurrent generation slots |
| `trace_id`, `span_id` | Engine activity identity when tracing is active; null when no listener supplies it |

A Metal tensor probe can fall back to the embedded library and still reach `Ready` successfully.

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
python3 scripts/test-serve-logging.py --model /models/test.gguf
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
