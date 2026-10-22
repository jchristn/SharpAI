# Inference operations and verification

SharpAI's library and server now share one model-pull pipeline. It pins repository listings and downloads
to the repository commit, requires an upstream SHA-256 and positive expected size for every file, and
registers a model only after verifying and publishing the complete set. An atomically published
registration journal recovers process death between file publication and registry insertion on the
next pull of that model; committed registrations are retained and unregistered publications removed. A missing digest fails explicitly;
a Git file object ID is never treated as a repository revision or an LFS content hash.

Downloads use adjacent `.partial` files and identity/ETag sidecars. Resuming requires a matching immutable
URL, expected hash, size, and strong ETag. Range responses must agree with the requested offset, complete
length, and ETag. A server that ignores Range restarts the transfer. Resumed prefixes are rehashed;
interrupted transfers can resume, while integrity failures never replace a published model.

Split GGUF sets retain their native filenames inside a GUID directory; existing single-file GUID storage
continues to work. Pulls, local imports, listing, inspection, loading, unloading, deletion, and memory
estimates resolve the same entry file. Incomplete, duplicate, inconsistent, or mismatched shard sets fail
preflight. Split digests hash the newline-separated, ordered uppercase per-shard SHA-256 digests;
benchmark receipts retain each actual file hash separately. Local imports have no upstream digest to
compare: they validate GGUF structure, compute provenance hashes, and publish before registration.

## Usage and timings

Generation and chat calls accept an optional `InferenceStatistics` accumulator on their grammar overloads.
Original public constructor and method signatures are retained for compiled callers.
The accumulator belongs to one request; separate concurrent calls must use separate accumulators.
Prompt counts use the exact rendered prompt, including special tokens, templates, and tool instructions.
Completion counts come from sampled non-EOG engine tokens before trimming, thinking filtering, or tool
parsing. They are not text-fragment counts or retokenized output counts.

Both streaming and non-streaming Ollama terminal responses contain duration and token fields. Durations
are nanoseconds. OpenAI non-streaming responses contain usage; streams emit an aggregate usage-only
chunk with empty `choices` immediately before `[DONE]` only when `stream_options.include_usage` is true.
Prompt-array completion usage aggregates all prompts while preserving stream choice indices.

Native phase counters are used when available. The pinned backend can disable those counters. In that
case, SharpAI measures time inside backend iterator steps, excluding consumer/network waits. These phase
timings are approximate: prompt time includes inference-context creation and first sampling. Telemetry
labels them `timing_source=backend_step_wall_clock`, and receipts expose
`PhaseTimingsApproximate=true`. TTFT is time to the first sampled non-EOG token and includes slot wait.
Cold-load duration is reported separately and included in the API accumulator's total duration.

New core instruments cover load duration, queue wait, TTFT, prompt/generation durations and token rates,
cancellations, and occupied/configured slots. Existing Prometheus and OTLP subscriptions export them.
No prompt, response, session identifier, or request identifier is attached to these metrics.
Inference spans now carry measured phase times, token counts, timing source and outcome; model loads
have separate timed spans. Completion/load log records contain matching trace/span IDs and flow through
the existing filelog-to-Loki pipeline. The provisioned **SharpAI — Engine operations** Grafana dashboard
covers every new metric and links logs to Tempo. See [stack verification](../docker/telemetry/README.md).

LlamaSharp 0.30 creates and disposes an inference context for each `StatelessExecutor` call. No bounded
cross-request prefix cache is claimed or implemented. See the pinned
[executor source](https://github.com/SciSharp/LLamaSharp/blob/v0.30.0/LLama/LLamaStatelessExecutor.cs).

## Preflight, context, and memory

Inspect a local file without loading weights:

```sh
dotnet run --project src/SharpAI.Server -f net8.0 -- --inspect /path/model.gguf
```

Inspect a registered model with `POST /api/inspect`, body `{"model":"repository/model"}`, or
`GET /api/models/{guid}/inspect`. The response describes architecture, quantization code, parameter count,
trained/effective context, layers and heads, complete files, and memory assumptions. Parameter counts can
be derived from tensor descriptors without reading tensor weights. Malformed GGUF versions, lengths,
arrays, tensor shapes, and truncated metadata fail explicitly. GGUF versions 2 and 3 are supported.

`/api/show` includes trained/effective context in `model_info`. `/v1/models?include_context=true` opts
into those fields; the default OpenAI model-list shape is unchanged. Rendered prompt tokens plus the
requested output budget must fit the effective context. Overflow returns HTTP 400 before streaming starts,
with prompt count, output reservation, and limit; no silent truncation or automatic summarization occurs.

Standard-attention estimates include shared weights, both K and V, a retained base context, every
configured generation slot, workspace buffers, and headroom. Models declaring embedding support also
reserve a second weights copy and embedding context. Unsupported cache layouts or incomplete metadata
have no invented estimate: when a budget is enabled, admission refuses them explicitly.

| Setting | Default | Meaning |
|---|---:|---|
| `SHARPAI_MODEL_MEMORY_BUDGET_MB` | 0 | Combined conservative model reservations; 0 disables this limit |
| `SHARPAI_HOST_MEMORY_BUDGET_MB` | 0 | Host-pool reservation limit |
| `SHARPAI_DEVICE_MEMORY_BUDGET_MB` | 0 | Separate device-pool reservation limit |
| `SHARPAI_MEMORY_BUFFERS_MB` | 256 | Workspace allowance per model |
| `SHARPAI_MEMORY_HEADROOM_PERCENT` | 20 | Additional allowance, valid range 0–1000 |

CUDA host/device estimates are overlapping upper bounds and must not be summed. Metal uses one unified
host pool. Estimates assume F16 caches and full attention; they are admission bounds, not allocation
measurements or guarantees that a native load will succeed. Pending loads reserve capacity atomically.
Cancelled waiters do not cancel shared loads; failed loads release reservations. Active generations are
ineligible for LRU/idle eviction.

`GET /api/diagnostics/runtime` reports selected backend, runtime, APIs, and configured controls. Selection
alone does not establish actual GPU offload; the report says so explicitly. Device inventory, observed
GPU allocations, and exact tensor placement are not supplied by this implementation.
`GET /api/diagnostics/memory` reports process working set/peak, managed heap, and model estimates;
per-model observed allocation is explicitly unavailable. Both diagnostics routes require administrator
access when authentication is enabled. With authentication disabled, the existing open-server policy applies.

## Benchmark receipts

```sh
SHARPAI_TEST_MODEL_GGUF=/path/model.gguf \
  dotnet run --project src/Test.Automated -c Release -f net8.0 -- \
  --benchmark --iterations 100 --concurrency 3 --max-tokens 64 --receipt receipt.json
```

Receipts contain schema version, library versions, selected backend, effective generation settings,
hardware/runtime details, per-file hashes, workload identity, warm state, concurrency, per-request token
counts/timings, failures, and latency/TTFT percentiles. Native build ID is explicitly unavailable rather
than inferred from the wrapper version. Failed measured requests remain in receipts and cause a nonzero
benchmark exit. Percentiles use nearest rank; p95 requires 20 samples, p99 requires 100. Smaller tails
are null. Model-load failure or cancellation aborts the run before a completed receipt exists.

Prefix reuse, continuous batching, quantization, additional API dialects, vision, and parallel chunked
downloads remain deferred. A small-model benchmark is not evidence for implementing those features.

## Tests and measured coverage

The reproducible integration harness requires .NET 8/10, Python 3, curl, Postman CLI 1.70 or newer,
and a real local GGUF file:

```sh
npm install -g postman-cli@1.75.0
SHARPAI_TEST_MODEL_GGUF=/path/small-model.gguf scripts/test-operations.sh /tmp/sharpai-evidence
python3 -m unittest discover -s scripts -p 'test_coverage_summary.py'
```

It creates an isolated server and database, runs the committed Postman Hugging Face simulation over real
HTTP, tests both frameworks, executes native operational tests separately from model instruction-following
tests, checks live API contracts, and restarts with authentication enabled to test administrator-only access.
The simulated upstream models only the tree/resolve/Range paths used here; it is a synthetic local fixture,
not a published upstream-owned mock. No workspace or cloud publishing is involved.

The coverage gate requires 100% lines and at least 90% branches in the new operations module.
The `Inference operations` workflow runs this harness with a pinned, hash-verified ~19 MB story model.
Ordinary test runs explicitly skip simulation, native, or live-server cases when their environment is absent.
The dedicated harness supplies those environments so those checks actually execute.

Local validation on 2026-10-21:

| Check | Result |
|---|---:|
| xUnit deterministic + simulated HTTP + live API cases, each framework | 376 passed |
| Native operational + reliability cases, each framework | 24 passed |
| Existing live server contract suite | 10 passed |
| Authentication/RBAC contract suite, including new diagnostics assertions | 4 passed |
| Coverage-summary + real-shard fixture utility tests | 6 passed |
| Python SDK unit + live consumer contract cases | 9 passed |
| C# SDK live consumer contracts, each framework | 7 passed |
| NUnit deterministic cases, each framework | 199 passed |
| New `SharpAI.Operations` production code | 100% lines, at least 92.97% branches |
| Whole SharpAI core, union of the four instrumented runs | 48.32% lines, at least 36.15% branches |
| Original commit `b6eae77`, deterministic suite | 200 passed; 34.50% core lines |

The core coverage summary excludes test assemblies, the separately running server, SDKs, and dashboard.
Direct server coverage is saved separately by the observability harness, so its denominator is not mixed
with the core summary. The measured Metal observability run covered 49.33% of server lines in its
separate instrumented report (29 live HTTP contract cases passed). Branch counts use the best observed count per source line, a conservative lower
bound rather than an assumed union of opaque branch identities. The whole repository is not near 100%.
CI configuration is committed; a hosted CI run has not been observed here.

## Additional reliability and hardware checks

The operations harness now generates two real GGUF shards from the hash-verified small model, serves
them through the existing Postman simulation, then downloads/verifies, inspects, loads, generates,
unloads and deletes them through production code. This exercises native multi-shard loading, including
rejection of missing shards; it does not establish compatibility with every large-model architecture.
The fixture splitter preserves tensor bytes and is test tooling, not a runtime conversion dependency.

Recovery tests kill a separate OS process during a download, after shard verification, immediately before
registry insertion and immediately after insertion. They restart against the same files/SQLite database
and assert integrity, complete shard sets, stable committed model identity and removal of orphaned
publications. This covers abrupt process death, not power-loss durability or multiple processes sharing
one model directory concurrently.

Native reliability tests repeatedly load/unload engines, occupy every slot, force queue timeouts and
cancelled waiters, dispose unfinished streams, verify active-model eviction protection, and generate
after reload. Set `SHARPAI_TEST_SOAK_CYCLES` (default 8) for longer runs and
`SHARPAI_TEST_RETAINED_MEMORY_LIMIT_MB` (default 64) to tune the post-warmup leak allowance.
Live Python SDK consumer checks verify Ollama timings, OpenAI usage and typed context-overflow errors.
Live HTTP tests disconnect streaming clients across both API dialects and require a subsequent request
to finish within its timeout.

```sh
python3 scripts/test-hardware-memory.py --model /path/model.gguf --backend metal --output /tmp/sharpai-metal
SHARPAI_TEST_OBSERVABILITY=1 SHARPAI_TEST_MODEL_GGUF=/path/model.gguf scripts/test-operations.sh /tmp/sharpai-evidence
```

The memory matrix runs contexts 128/256 with 1/3 slots and concurrent generation, samples whole-process
working set and compares its increase with the model host estimate plus an explicit allocator allowance.
GPU modes require a native `offloaded N/M layers to GPU` message, so selected-backend metadata alone
cannot pass. CUDA also checks process allocations via `nvidia-smi` against the device estimate.
The harness limits each small-fixture inference context to two CPU threads to avoid oversubscription.
CPU and Metal matrices passed locally; CUDA requires a CUDA runner. These are small-model checks and
process-level observations, not exact per-model attribution or a general proof of admission bounds.

The isolated observability harness runs the live HTTP contract suite, checks every engine instrument in
Prometheus (including cancellation series), executes the provisioned metric panel queries through Grafana,
finds correlated operation records in Loki, retrieves measured inference spans from Tempo, and validates
provisioned log/trace navigation. It also statically instruments temporary server/core assemblies and
requires exercised server lines in its separate Cobertura report. Both CPU and Metal export paths have
been exercised locally; Metal logs confirmed native offload. Hosted CI remains unobserved.

## Remaining plan work

- Runtime diagnostics still lack device inventory/memory and verified tensor placement.
- Memory diagnostics lack reliable observed per-model allocation attribution.
- Prefix reuse and continuous batching remain conditional on representative workload measurements.
- Quantization, Anthropic Messages, vision projectors and parallel downloads remain deferred until demand.
- Broader architectures, larger contexts/models, CUDA, SDK/database coverage and long-duration hardware soak
  runs need additional environments and fixtures; the new harnesses support continued validation.

## NuGet compatibility update (2026-10-21)

All direct dependencies with available stable updates were updated across the main and C# SDK solutions.
LLamaSharp and both native backends use one shared `LlamaSharpVersion` property, now 0.30.0.
The deprecated mmap/mlock booleans are mapped to the new `LLamaLoadMode`, including all four combinations.
Metadata preflight reads the trained/effective context without constructing native `ModelParams`.

LLamaSharp 0.30 ships .NET 10 and .NET Standard bindings. SharpAI supplies lazy native-library resolution
for .NET 8 embedded callers. `NativeBackendInitializer` now lives in the library and runs before
`GetOptimalGpuLayers` chooses offload settings. The server delegates its runtime settings to that same
initializer. Standalone Apple Silicon consumers select Metal automatically; CPU forcing, custom paths,
and GPU-layer overrides remain available. .NET 10 retains LLamaSharp's own native loader. Native generation, real shards,
cancellation, queue/reload behavior and all mmap/mlock combinations are exercised on both frameworks.

The C# SDK now reads OpenAI SSE through RestWrapper's event reader, while retaining Ollama NDJSON
chunk handling. The operations harness builds the SDK and runs seven live consumer contracts on each
framework against its isolated local server; it does not download additional models. SqlClient's
transitive PKCS dependency is explicitly pinned to the patched 10.0.13 in both library and SDK.


### Standalone Metal regression probe

Build the solution, then run the library-only worker on native Apple Silicon with a local GGUF file:

```bash
SHARPAI_ENABLE_NATIVE_LOGGING=true SHARPAI_CONTEXT_SIZE=128 \
  dotnet src/Test.Automated/bin/Debug/net8.0/Test.Automated.dll \
  --native-backend-probe metal 999 /path/to/model.gguf
```

Repeat with `net10.0`. The worker asserts the selected backend and GPU-layer request, performs real
inference, and rejects a loaded `SharpAI.Server` assembly. Native logs should show layers offloaded to
the GPU. `999` is the engine's request for all layers, clamped by llama.cpp to the model's layer count.
To verify CPU forcing, set `SHARPAI_FORCE_BACKEND=cpu` and use `--native-backend-probe cpu 0`.
To verify layer overrides, use `SHARPAI_GPU_LAYERS=0` with `--native-backend-probe metal 0`, or a
positive layer count with the matching expected value. Run each scenario in a new process because
native backend initialization is process-wide.

The complete regression matrix checks automatic selection, forced CPU, zero/limited/all-layer
overrides, explicit host configuration, missing-Metal CPU fallback, and required-backend failures:

```bash
python3 scripts/test-metal-library.py --model /path/to/model.gguf --output /tmp/sharpai-metal-evidence
```

Each successful model case validates native offload counts as well as generated output. Both .NET
frameworks run by default; use `--framework net8.0` or `--framework net10.0` to select one.
