# SharpAI — Improvement Plan (v5.0.0, enterprise track)

This document is the working plan to take SharpAI from a competent LlamaSharp wrapper to a
viable, correct, **enterprise-ready** alternative to Ollama and OpenAI-compatible model runners —
one that is at least as reliable as Ollama and observable enough to operate a fleet in production.
It is scoped end-to-end: core inference, server, model lifecycle, security, telemetry, dashboard,
SDKs, tests, docs, Docker, and CI.

It is written to be *annotated in place*. A developer picks up a task, flips its checkbox to
in-progress, records a note, and marks it complete when the acceptance criteria are met.

**Reliability is the north star.** Every workstream carries a testing obligation (see W10). The bar
is not "it runs" — it is "it behaves correctly under concurrency, memory pressure, malformed input,
and backend failure, and we have automated proof." Aim for ~100% meaningful coverage of the core
library and server, exercised through the Touchstone suites.

---

## §0 — Versioning & release train

**Decision (2026-08-14): normalize the entire repository to a single `5.0.0` version.** The server,
dashboard, and Docker image were on `4.0.1`; they move to `5.0.0`. The core library (`1.0.17`) and
SDKs are pulled onto the same unified `5.0.0` so the enterprise release has one coherent version
story. This is a major, breaking release — chat-template behavior changes, the config schema
changes, the route surface changes, auth and telemetry are added, and vision is removed.

| Artifact | Was | Now (target `5.0.0`) |
|---|---|---|
| `SharpAI` core library (NuGet) | 1.0.17 | **5.0.0** |
| `SharpAI.Server` + Docker image tag | 4.0.1 | **5.0.0** |
| Dashboard | 4.0.1 | **5.0.0** |
| `SharpAI.Sdk` (C#) | 1.0.1 | **5.0.0** |
| `@sharpai/sdk` (JS) | 1.0.0 | **5.0.0** |
| `sharpai` (Python) | — | **5.0.0** (first release) |

**Rollout.** `5.0.0` is not one big-bang merge. Ship pre-releases as workstreams land:
`5.0.0-alpha.N` (P0 engine + observability), `5.0.0-beta.N` (backend refactor, API completeness,
auth, request history), `5.0.0-rc.N` (dashboard, i18n, SDKs, docs), then `5.0.0`. Tag each
(`v5.0.0-alpha.1`) so `run.bat <tag>` and the Docker flow keep working.

**Config schema versioning.** Add a `SchemaVersion` field to `sharpai.json` and migrate-on-load, so
the `4.x → 5.0` settings changes (auth block, telemetry block, database block, concurrency/lifecycle
settings) upgrade cleanly instead of failing to deserialize.

---

## How to use this document

Each task carries a checkbox and, where useful, sub-checklists, acceptance criteria, the surfaces it
touches, and the requirement document it satisfies. Update the checkbox and the **Owner/Notes** line
as you go.

**Status legend:** `[ ]` not started · `[~]` in progress · `[x]` complete · `[!]` blocked ·
`[-]` deliberately skipped (link the decision in §18).

**Surfaces:** `core` · `server` · `dashboard` · `sdk` · `tests` · `docs` · `docker` · `ci` ·
`telemetry`.

**Requirement references** (under `C:\code\agents\requirements\`): `REPOSITORY_REQUIREMENTS` ·
`CODE_STYLE` · `BACKEND_ARCHITECTURE` · `BACKEND_TEST_ARCHITECTURE` · `FRONTEND_ARCHITECTURE` ·
`DASHBOARD_STYLE_AND_USABILITY` · `I18N` · `AUTHENTICATION` · `WRITING_DOCUMENTS` ·
`EXAMPLE_APPLICATIONS`.

---

## Progress dashboard

| # | Workstream | Priority | Status | Done / Total |
|---|------------|----------|--------|--------------|
| W0 | Version normalization to 5.0.0 | P0 | `[x]` | 1 / 1 |
| W1 | Inference correctness (chat templates, stop, clamps) | P0 | `[x]` | 6 / 6 |
| W2 | Concurrency & throughput | P0 | `[x]` | 5 / 5 |
| W3 | Model lifecycle & memory management | P0 | `[x]` | 5 / 5 |
| W4 | API completeness (tools, models, JSON mode; vision removed) | P0/P1 | `[x]` | 7 / 7 |
| W5 | Model sourcing & onboarding friction | P1 | `[~]` | 3 / 5 |
| W6 | GPU breadth (Vulkan/ROCm, VRAM-fit) | P2 | `[ ]` | 0 / 4 |
| W7 | Backend architecture conformance (Watson 7.1, 4-DB, DTOs) | P1 | `[~]` | 1 / 7 |
| W8 | Request capture & history | P1 | `[x]` | 5 / 5 |
| W9 | Auth / authz / accounting (built, **off by default**) | P1 | `[~]` | 8 / 9 |
| W10 | Test architecture (Touchstone) & ~100% coverage | P0 | `[~]` | 3 / 9 |
| W11 | Dashboard rebuild to standard | P1 | `[x]` | 10 / 10 |
| W12 | Internationalization | P1 | `[x]` | 6 / 6 |
| W13 | SDKs (C#, JS, Python) parity | P1 | `[~]` | 5 / 6 |
| W14 | Docs & repository housekeeping | P1 | `[~]` | 6 / 8 |
| W15 | CI/CD | P1 | `[~]` | 4 / 5 |
| W16 | Operations hardening | P2 | `[x]` | 3 / 3 |
| W17 | Observability & telemetry (Radiant, Watson 7.1, Prom/Loki/Grafana/Tempo) | P1 | `[~]` | 7 / 8 |

**Priority meaning.** P0 separates a *runner* from a *wrapper* and underpins reliability. P1 is
required for standards compliance, enterprise operability, and a credible full product. P2 is reach.

Recommended sequencing: **W0** (done first, mechanical) → **W1–W3** (the technical case) →
**W7/W10** (the refactor + test spine everything else builds on) → **W17** (observability, largely
independent, high enterprise value) → **W8/W9/W4/W5** → **W11/W12/W13** → **W14/W15/W16**.

---

## §1 — W0: Version normalization

- [x] **W0.T1 — Set every artifact to `5.0.0`.** _(done 2026-08-14)_
  - [x] `src/SharpAI/SharpAI.csproj` `<Version>` → `5.0.0`; `PackageReleaseNotes` refreshed.
  - [x] `src/SharpAI.Server/SharpAI.Server.csproj` → `<Version>5.0.0</Version>` added.
  - [x] `sdk/csharp/src/SharpAI.Sdk/SharpAI.Sdk.csproj` → `5.0.0`.
  - [x] `dashboard/package.json` → `5.0.0`; `sdk/js/package.json` → `5.0.0`.
  - [x] `docker/compose-cpu.yaml`, `docker/compose-cuda.yaml`, new `docker/compose.yaml` image tags → `v5.0.0`.
  - [x] `docker/factory/sharpai.json`, `docker/sharpai.json`, `src/SharpAI.Server/sharpai.docker.json` `SoftwareVersion` → `5.0.0`; `SchemaVersion` added.
  - [x] `build-*.bat` example tags and README `v4.0.1` references → `v5.0.0`.
  - **Acceptance:** no active `4.0.1`/`1.0.17` version reference remains (only a dashboard README
    historical migration note and a `remark-gfm ^4.0.0` dep, both intentional). Build verification of
    the solution on `net8.0`/`net10.0` runs alongside W7 (the server still targets Watson 7.0.11).
  - **Surfaces:** all · **Owner/Notes:** _completed this pass._

---

## §2 — W1: Inference correctness

The single highest-leverage area. The engine currently picks a chat template from a hand-maintained
architecture-to-format table (`ChatFormatHelper`) instead of the model's own embedded template. That
produces subtly wrong prompts for any model whose real template deviates, and guarantees a permanent
catch-up treadmill. Everything here is `core` + `tests` unless noted.

- [x] **W1.T1 — Apply the GGUF-embedded chat template as the primary path.** _(done 2026-08-14)_
  `LlamaSharpEngine` implements `IChatTemplateSource` using LlamaSharp 0.27's `LLamaTemplate` (built from
  the model weights, `strict:true`), probing/caching whether the model carries an embedded template and
  rendering messages through it. `ChatTemplateResolver` prefers the embedded template and falls back to
  `ChatFormatHelper`/`ChatPromptBuilder` on absence, empty render, or error. Wired into **both** server
  chat handlers (`/api/chat`, `/v1/chat/completions`); the public `PromptBuilder`/`ChatFormat` API is
  preserved as the documented fallback.
  - **Remaining for full acceptance:** the byte-match golden tests vs llama.cpp need real GGUFs
    (tracked in W1.T6). The resolver decision logic is covered by 6 descriptors (fake source, no model).
  - **Owner/Notes:** _resolver + engine verified; goldens pending a model fixture._
- [x] **W1.T2 — Derive stop/termination from the model.** _(done 2026-08-14)_ When the embedded template
  is used, the resolver returns **no** injected anti-prompts, so generation ends on the model's native
  EOS/EOT tokens (as in llama.cpp/Ollama) instead of the hard-coded `{ "user:", ... }` list. The family
  `GetDefaultStopSequences` table remains only for the fallback path. **Owner/Notes:** _—_
- [x] **W1.T3 — Fix the silent `MaxTokens` clamp.** _(done 2026-08-14)_ Replaced
  `Math.Max(maxTokens, 100)` in all four engine generation methods with `EffectiveMaxTokens`, which
  honors any positive request exactly (including `max_tokens: 8`) and only applies the configurable
  `DefaultMaxTokens` (default 512, min 1) when the caller passes a non-positive value. **Owner/Notes:** _—_

- [x] **W1.T4 — Correct, configurable thinking-tag filtering.** _(done 2026-08-14)_ `ThinkingFilter`
  markers are now configurable (ctor + a static overload taking custom open/close tags), so it serves
  reasoning models beyond `<think>`; streaming still never emits a partial tag, and an unclosed block is
  no longer leaked on `Flush`. Covered by 4 new descriptors. **Owner/Notes:** _server per-request keep/strip
  already exists via `DisplayThinking`; a server-level default-tag setting is optional follow-up._
- [x] **W1.T5 — Embedding chunk-and-average made opt-in.** _(done 2026-08-14)_ `EnableEmbeddingChunking`
  (default true) on the engine; when false, an over-length embedding input throws a clear error instead
  of silently chunking-and-averaging (which changes the vector's meaning). **Owner/Notes:** _—_

- [x] **W1.T6 — Golden-output regression suite for prompt rendering.** _(done 2026-08-21)_ The
  fixture-gated `ModelInferenceSuite` now renders the model's embedded chat template for a fixed
  conversation and **byte-matches it against a committed golden keyed by architecture** (`src/goldens/
  chat-<arch>.txt`; newlines normalized). Goldens are self-populating with `SHARPAI_WRITE_GOLDENS=1` and
  the case skips cleanly for an architecture with no committed golden or when run outside the source tree.
  Captured + verified against **Qwen2.5-1.5B** (`chat-qwen2.txt`) — 185/185 automated cases green with the
  model present. A LlamaSharp bump that changed the rendered bytes would now fail this case.
  **Ref:** BACKEND_TEST_ARCHITECTURE · **Owner/Notes:** _qwen2 golden committed; add more families as
  their GGUFs are supplied._

---

## §3 — W2: Concurrency & throughput

Today one model serves one request at a time (`_GenerationSemaphore(1,1)`), and
`ModelEngineService.GetByModelFile` calls `InitializeAsync(...).Wait()` **while holding the global
`_EnginesLock`**, so loading any model blocks every request to every other model.

- [x] **W2.T1 — Remove the global lock from the model-load path.** _(done 2026-08-14)_ `ModelEngineService`
  now stores engines in a `ConcurrentDictionary<string, Lazy<Task<LlamaSharpEngine>>>` with per-model
  gating. Loading model B no longer holds any lock that model A's requests need, and concurrent callers
  for the same model share a single initialization. Failed/disposed entries are evicted and retried.
  - **Remaining for full acceptance:** the measured "B-load doesn't stall A" benchmark needs models
    (W2.T5). **Owner/Notes:** _global `_EnginesLock` + `.Wait()`-under-lock eliminated._
- [x] **W2.T2 — Purge sync-over-async from the acquisition path.** _(done 2026-08-14)_ Added
  `GetByModelFileAsync` (fully async, `Task.WaitAsync(token)` so the caller's wait is cancellable
  without cancelling the shared load). The sync `GetByModelFile` remains as a thin boundary shim for
  existing callers; migrating the handlers to the async overload is a follow-up. **Owner/Notes:** _—_
- [x] **W2.T3 — Configurable generation concurrency.** _(done 2026-08-14; concurrency-safety fixed 2026-08-21)_
  `MaxConcurrentGenerations` (default 1, env `SHARPAI_MAX_CONCURRENT_GENERATIONS`) sizes the per-model
  generation semaphore, so operators can opt into parallel decode slots. **Critical fix (2026-08-21):** with
  more than one slot, concurrent generations shared a single `StatelessExecutor` and raced on its internal
  context, throwing `ObjectDisposedException` — i.e. `MaxConcurrentGenerations > 1` crashed under load. Each
  concurrent generation now runs on its own executor/context (the shared executor is used only in the
  serialized one-slot fast path); validated by the W2.T5 benchmark and locked by `ModelLifecycle/ParallelSlots`.
  llama.cpp continuous batching (BatchedExecutor) remains a deeper future optimization. **Owner/Notes:** _—_
- [x] **W2.T4 — Request admission & backpressure.** _(done 2026-08-14; critical bug fixed 2026-08-21)_
  `GenerationQueueTimeoutMs` (default 0 = wait forever, env `SHARPAI_GENERATION_QUEUE_TIMEOUT_MS`) bounds
  the slot wait; on timeout the engine throws `EngineBusyException` (maps to a busy response).
  `CancellationToken` already frees the wait on client disconnect. **Critical fix (2026-08-21):**
  `LlamaSharpEngine.AcquireGenerationSlotAsync` recursed into **itself** on the `timeout <= 0` (default)
  path instead of awaiting the semaphore — an unbounded recursion that **StackOverflow-crashed the process
  on the very first generation under default settings**. Now awaits `_GenerationSemaphore.WaitAsync(token)`
  directly. Caught by running the model-gated `ModelInferenceSuite` against a real GGUF (Qwen2.5-1.5B) and
  confirmed fixed end-to-end (server `/api/chat` returns a completion). **Owner/Notes:** _server 503 mapping
  tracked under W4.T7._
- [x] **W2.T5 — Concurrency benchmark harness.** _(done 2026-08-21)_ A committed benchmark (`ConcurrencyBenchmark`,
  run via `Test.Automated --benchmark --iterations N --concurrency N --max-tokens N`) measures sequential vs
  N-concurrent generation (avg latency, tokens/sec, wall speedup) and is documented in
  [docs/BENCHMARKS.md](docs/BENCHMARKS.md) with a reference CPU run (Qwen2.5-1.5B: ~11.3 tok/s sequential →
  ~13.5 tok/s at 3 slots, ~1.2× wall speedup — honest CPU-bound numbers). **This benchmark caught a second
  critical bug** (see W2.T3): concurrent decode on the shared `StatelessExecutor` threw
  `ObjectDisposedException`, so `MaxConcurrentGenerations > 1` crashed under load. Fixed by giving each
  concurrent generation its own executor/context; locked by `ModelLifecycle/ParallelSlots`. **Surfaces:** tests,
  docs, core · **Owner/Notes:** _—_

---

## §4 — W3: Model lifecycle & memory management

No cap, no idle eviction, no memory-aware admission, and every model loads its weights twice (a second
`_EmbeddingModel` on every `InitializeAsync`).

- [x] **W3.T1 — Lazy embedder.** _(done 2026-08-14)_ `InitializeAsync` no longer eagerly loads a second
  copy of the weights for embeddings; `EnsureEmbedderAsync` builds the embedding model/context on first
  embedding request (once, gated by a semaphore, with failure memoized and resources cleaned up on
  error). A generation-only model now allocates a single weight set. **Owner/Notes:** _—_
- [x] **W3.T2 — Keep-alive / idle eviction.** _(done 2026-08-14)_ `KeepAliveSeconds` (default 0 = never,
  env `SHARPAI_KEEP_ALIVE_SECONDS`) drives a background sweep in `ModelEngineService` that disposes
  models idle beyond the timeout; last-access time is tracked per model. **Owner/Notes:** _—_
- [x] **W3.T3 — Max-resident cap + LRU eviction.** _(done 2026-08-14)_ `MaxResidentModels` (default 0 =
  unlimited, env `SHARPAI_MAX_RESIDENT_MODELS`); admission evicts the least-recently-used loaded model
  when the cap would be exceeded. **Owner/Notes:** _—_
- [x] **W3.T4 — Memory-budget admission.** _(done 2026-08-14)_ `ModelMemoryBudgetBytes` (env
  `SHARPAI_MODEL_MEMORY_BUDGET_MB`, 0 = unlimited): admission evicts LRU models to fit a new model's
  file size within the budget and throws `ModelAdmissionException` if it cannot — a structured error
  instead of an OOM/native crash. **Note:** this is a configurable file-size budget (deterministic,
  cross-platform); true VRAM auto-detection remains with the GPU work (W6). **Owner/Notes:** _—_
- [x] **W3.T5 — Accurate `/api/ps` accounting.** _(done 2026-08-14)_ `/api/ps` already reported
  name/digest/size/size_vram/family; it now also reports a real `expires_at` computed from the model's
  last access + keep-alive (via `ModelEngineService.GetExpiryUtc`), and the stale "always null" OpenAPI
  note was corrected. **Surfaces:** core, server · **Owner/Notes:** _—_

---

## §5 — W4: API completeness

The `OllamaTool`/`OpenAITool` model classes exist but the handlers reference them zero times.

- [x] **W4.T1 — Tool / function calling.** `[P0]` _(done 2026-08-21)_ The model-independent core
  (`ToolCallParser`/`ToolPromptBuilder`/`ToolResponseMapper`, + new `ToolRequestMapper` mapping the OpenAI/Ollama
  tool shapes to `ToolDefinition`) is now **wired end-to-end in both chat handlers**: tools are converted,
  the `<tool_call>` system instruction is injected (merged into any caller system prompt), the model output is
  parsed, and calls are emitted as OpenAI `tool_calls` + `finish_reason:"tool_calls"` / Ollama
  `message.tool_calls` + `done_reason:"tool_calls"`. `tool_choice:"none"` disables; prior assistant tool-call
  turns are replayed into the prompt; streaming buffers then emits a single tool_calls chunk. **Parser hardened
  from a real finding:** Qwen2.5-1.5B emits `<tool_call>{...}` **without** the closing tag, so the parser now
  uses balanced-brace extraction (handles unclosed tags + nested braces) — 2 new deterministic cases.
  **Validated live** against Qwen2.5-1.5B: OpenAI non-stream, OpenAI stream, Ollama non-stream all return the
  correct `get_weather({city})` call; no-tool control still returns normal chat. Locked by a fixture-gated
  engine test (`ModelInference/ToolCalling`). **Owner/Notes:** _SDK round-trip parity is a small follow-up._
- [x] **W4.T2 — `GET /v1/models`.** `[P0]` _(done 2026-08-14)_ Returns local models in OpenAI list shape
  (`object: "list"`, `data[].{id,object,created,owned_by}`). The `/v1/models/{id}` retrieve variant is a
  small follow-up (route path-param support to confirm). **Owner/Notes:** _—_
- [x] **W4.T3 — `POST /api/show` and `GET /api/version`.** `[P1]` _(done 2026-08-14)_ `/api/version`
  returns the server version; `/api/show` returns GGUF-derived metadata + capabilities (family,
  parameter size, quantization, embeddings/completions, size, digest) for a named model, with 400/404
  handling. **Owner/Notes:** _—_
- [x] **W4.T4 — JSON mode / structured outputs.** `[P1]` _(done 2026-08-21)_ New `SharpAI.Grammars.JsonGrammar`
  supplies a canonical JSON GBNF grammar and maps OpenAI `response_format` (`json_object`/`json_schema`) and
  Ollama `format` onto it. The engine's four generation methods gained grammar-accepting overloads that set
  `DefaultSamplingPipeline.Grammar` (LlamaSharp 0.27), so decoding is constrained to valid JSON; the existing
  overrides delegate with no grammar (unconstrained). Wired into both chat handlers (grammar suppressed when
  tools are active). **Validated live:** OpenAI `json_object` and Ollama `format:"json"` return valid JSON, and
  a torture prompt (asking for a poem under `json_object`) **still** yields syntactically valid JSON — proving
  the grammar constrains sampling. Locked by `JsonGrammarSuite` (7 deterministic cases) + a fixture-gated
  `ModelInference/JsonMode` valid-JSON assertion. **Note:** `json_schema`/Ollama schema objects currently fall
  back to the general JSON grammar (valid JSON, not yet schema-exact); full schema→GBNF is a future
  enhancement. **Owner/Notes:** _—_
- [-] **W4.T5 — `logprobs`.** `[P2]` _(deferred by decision 2026-08-14)_ The current behavior returns
  `null` logprobs, which is OpenAI-compliant when logprobs are not requested, so the contract is not
  misleading. Emitting real token logprobs requires sampler-level changes and is deferred until there is
  demand; the fields are retained (not dropped) so the response shape stays OpenAI-shaped.
  **Owner/Notes:** _revisit with a model fixture if a consumer needs it._
- [x] **W4.T6 — Remove vision / llava entirely.** `[P1]` (decision **D3: remove**) _(done 2026-08-14)_
  - [x] Removed the `Dlls\win-x64\llava_shared.dll` `<None Include>` item from `SharpAI.csproj`,
        deleted the binary, and untracked it from git (the `Dlls/` dir is gone).
  - [x] Removed the `vision` doc mention from `LlamaSharpEngine`; core vision code (`VisionDriver`,
        `LLavaWeights`) was already removed in a prior release per CHANGELOG.
  - [x] Removed vision/mmproj claims from `src/CLAUDE.md`; dropped `llava` from `SharpAI.csproj`
        package tags (added `openai gguf telemetry`). README had no vision claims.
  - **Acceptance met:** no active `llava`/vision reference remains in source, project files, or docs
    (only the 5.0.0 release-note line documenting the removal). Core library builds clean.
  - **Surfaces:** core, docs · **Owner/Notes:** _completed this pass._
- [x] **W4.T7 — Error-shape parity.** _(done 2026-08-19)_ The `RunInference` wrapper now covers **all six
  inference routes** (Ollama + OpenAI chat/generate/embeddings) and, on any pre-stream error, writes the
  OpenAI/Ollama **error envelope** `{"error":{"message":...,"type":...}}` with the right status + type:
  429 `server_busy` (EngineBusy/ModelAdmission), 400 `invalid_request_error`, 404 `not_found_error`,
  500 `internal_error` — instead of Watson's default error shape, so OpenAI/Ollama SDKs never throw on parse.
  Mid-stream errors (after the response starts) are safely swallowed. Build-verified; a runtime assertion
  lands with the live-server contract suite (W10.T4). **Surfaces:** server · **Owner/Notes:** _—_

---

## §6 — W5: Model sourcing & onboarding friction

- [x] **W5.T1 — Import a local GGUF by path** (no download, no token). _(done 2026-08-21)_ New
  `POST /api/import` (Ollama - Models tag) accepts a local filesystem `path` + optional `name` (defaults to
  the file name), validates the file exists and carries the **GGUF magic header**, copies it into the models
  directory under its registry GUID, detects capabilities via `GgufMetadataReader` (falling back to a full
  engine load), computes MD5/SHA1/SHA256, and registers the `ModelFile` (family/size/digest/capabilities).
  Typed `ImportModelRequest` DTO + OpenAPI (200/400/404/409). RBAC-gated `Model:Write`. Verified end-to-end
  against a running server: import → `/api/tags` shows it (family `qwen2`) → `/api/chat` generates; duplicate
  name → 409, missing file → 404, non-GGUF → 400. **Owner/Notes:** _server route inline for now; SDK parity
  is a follow-up._
- [x] **W5.T2 — Optional HF token for public repos** (token only for gated/private). _(done 2026-08-21)_
  `HuggingFaceClient` no longer throws when constructed without a token; the `Authorization: Bearer` header is
  sent only when a token is present, so public repositories pull anonymously and a token is required only for
  gated/private repos. The server startup no longer requires an HF key. Locked by
  `Reliability/HuggingFace_Token_Optional`. **Owner/Notes:** _—_
- [ ] **W5.T3 — Ollama-registry pull path (evaluate).** Record feasibility/licensing in §18. **Owner/Notes:** _—_
- [x] **W5.T4 — Modelfile-equivalent presets** (system prompt, params, template override, stop). _(done 2026-08-23)_
  New `ModelPreset` + `IModelPresetMethods`/`ModelPresetMethods` (portable SQL) added as **migration v5 across all
  four providers**, exposed via `DatabaseDriverBase.Presets`. CRUD routes under `/v1.0/models/presets`
  (list/create/get/delete, RBAC `Model:*`). A preset names a base model and carries a default system prompt,
  temperature, max-tokens, stop, and a stored template override. Both chat handlers resolve a request whose
  `model` names a preset to the base model and apply the defaults — system prompt injected only when the request
  has no system message (explicit request system wins), temperature/max-tokens/stop fall back to the preset.
  **Validated end-to-end:** a "pirate" preset over `qwen-base` made both `/api/chat` and `/v1/chat/completions`
  answer in-persona ("Ahoy matey!"); an explicit request system prompt correctly overrode it; list/get/delete
  verified. SQLite contract suite `ModelPresetSuite` (6 cases) is green. **Note:** `TopP` and the raw
  `templateOverride` are persisted but not yet applied to the sampler/prompt (the engine methods don't expose
  top-p or a custom template yet) — a follow-up. **Owner/Notes:** _—_
- [ ] **W5.T5 — Sharded GGUF + explicit overridable quant selection.** **Owner/Notes:** _—_

---

## §7 — W6: GPU breadth

- [ ] **W6.T1 — Vulkan backend** (AMD/Intel/NVIDIA) into `NativeLibraryBootstrapper` + `SHARPAI_FORCE_BACKEND`. **Owner/Notes:** _—_
- [ ] **W6.T2 — ROCm backend** for AMD on Linux (evaluate LlamaSharp support). **Owner/Notes:** _—_
- [ ] **W6.T3 — VRAM-fit partial offload** (compute layer count from VRAM + model size). **Owner/Notes:** _—_
- [ ] **W6.T4 — Multi-GPU tensor split** beyond a single `MainGpu`. **Owner/Notes:** _—_

---

## §8 — W7: Backend architecture conformance

`BACKEND_ARCHITECTURE` is normative and the current server violates several load-bearing rules. This
is the refactor spine that W8/W9/W17 build on. Also bump **Watson 7.0.11 → 7.1.0** here (needed for
W17 telemetry).

- [~] **W7.T1 — Watson 7.1.0 upgrade + drop the `server.Get/Post<T>` convenience surface.** _(started 2026-08-14)_
  **Upgraded `Watson` 7.0.11 → 7.1.0**; the existing route API still compiles on 7.1.0, so the build
  stays green and Watson's native telemetry is now available (see W17.T3). Remaining: migrate handlers
  to `server.Routes.{Pre,Post}Authentication.{Static,Parameter}.Add(...)` with typed DTOs from
  `ctx.Request.DataAsString`, per the registrar pattern (W7.T2). **Surfaces:** server, tests · **Owner/Notes:** _—_
- [~] **W7.T2 — Per-feature route registrar classes.** _(advanced 2026-08-24)_ The registrar seam under
  `src/SharpAI.Server/API/REST/Routes/` now carries: a shared **`RouteContext`** (webserver, version, a
  **settings accessor + replacer**, serializer, database/model/telemetry services, and the auth gate — via a
  `ConfigureControlPlane` step); a **`RouteHelpers`** static (the `Describe` convention, writable-dir probe,
  plus the relocated `RouteParam`/`ParseEnumQuery`/`HeaderValue`/`ExtractBearerToken`); a reusable
  **`AuthorizationGate`** (the extracted `Authorize`/`AuthorizeInspection` + audited RBAC-denial logic); and
  **three extracted registrars** — `GeneralRoutes`, **`SettingsRoutes`** (`GET/PUT /api/settings`), and
  **`RequestHistoryRoutes`** (the five `/v1.0/api/request-history` routes). `Program.cs`'s own remaining
  routes now **delegate** their `Authorize`/helpers to the same gate/`RouteHelpers` (single implementation),
  and the inline Settings + RequestHistory regions were deleted. **Verified live:** the `Test.Server` contract
  harness is **10/10** (default) and **4/4** (auth: 401 challenge, anonymous-open, admin-key bypass, login→token
  + RBAC 403) after the extraction. _(further advanced 2026-08-24)_ **`OllamaInferenceRoutes`** (`/api/embed`,
  `/api/generate`, `/api/chat`) and **`OpenAIInferenceRoutes`** (`/v1/models`, `/v1/embeddings`,
  `/v1/completions`, `/v1/chat/completions`) are now extracted too, with the shared inference error-envelope
  wrapper moved into an **`InferenceErrors`** helper. Five registrars total now; the inline inference regions
  were deleted from `Program.cs`. Re-verified live: contract suite **10/10**, and real generation through the
  extracted routes (`/api/chat` → "OK", `/v1/chat/completions`, `/v1/models`, `/api/embed` 404 envelope).
  Remaining: `OllamaModelRoutes` (pull/import/presets/delete/unload/tags/ps/version/show), auth/management, and
  `MetricsRoutes`. **Owner/Notes:** _general/settings/request-history/ollama-inference/openai-inference + auth
  gate done; model/auth/management groups follow._
- [~] **W7.T3 — Thin `Program.cs` + instance `SharpAIServer` host** owning composition. _(advanced 2026-08-24)_
  `RouteContext` + `AuthorizationGate` are the composition seams the instance host will own; the Settings and
  RequestHistory inline regions (~90 lines) were removed from `Program.cs` in favor of registrar `Register`
  calls, and the shared helpers now live outside the static host. Remaining: move composition into an instance
  `SharpAIServer` and migrate the remaining route groups. **Owner/Notes:** _—_
- [x] **W7.T4 — Provider-neutral database layer — all four providers (decision D2: required).**
  _(done 2026-08-14)_ **WatsonORM removed entirely**; replaced with a hand-rolled interface/implementation
  layer under `src/SharpAI/Database/`: `DatabaseTypeEnum`, `DatabaseSettings`, `IModelRegistryMethods`,
  `SchemaMigration`, `DatabaseDriverBase` (connection-agnostic execution + versioned/tracked migration
  runner + serialized access), `DatabaseDriverFactory`, and provider drivers for **`Sqlite`, `Mysql`,
  `Postgresql`, `SqlServer`** on raw ADO.NET (Microsoft.Data.Sqlite, MySqlConnector, Npgsql,
  Microsoft.Data.SqlClient) with dialect-specific DDL/paging. Handwritten portable SQL executes CRUD;
  a `schema_migrations` table tracks applied versions idempotently. `AIDriver`, `ModelDriver`,
  `ModelFileService`, `ModelFile`, and the server were cut over; `ModelFile` no longer carries ORM
  attributes.
  - **Verified:** the SQLite contract suite (migrate-idempotent, add/get, duplicate-name, exists/all,
    update round-trip, get-many, enumerate paging, delete) is green across console/xUnit/NUnit. The
    MySQL/PostgreSQL/SQL Server drivers honor the same `IModelRegistryMethods` contract; their runtime
    matrix (W10.T5) runs under Docker DB profiles.
  - **Remaining:** the per-provider `Queries/` split is currently one shared portable-SQL implementation
    with per-driver DDL/paging (a documented simplification since the entity is portable); registry
    first-boot seeding is N/A.
  - **Surfaces:** core/server, tests, docker · **Owner/Notes:** _—_
- [~] **W7.T5 — Prefixed identifiers via a central `IdGenerator`.** _(started 2026-08-14)_ Added
  `SharpAI.Helpers.IdGenerator` with `req_`/`mdl_` prefixes; request-history entries use `req_` ids.
  Remaining: adopt K-sortable `PrettyId` (vs GUID-backed) and apply `mdl_` to the model registry.
  **Owner/Notes:** _—_
- [ ] **W7.T6 — Typed DTOs everywhere; no JSON DOM for fixed contracts** (`Requests/`, `Responses/`).
  _Down-payment (2026-08-14):_ model listing is now typed end-to-end via `EnumerationQuery` →
  `EnumerationResult<ModelFile>`; there are no unbounded `All()`/get-all APIs (the Ollama/OpenAI list
  contracts page through the enumeration). Remaining: audit the rest of the handlers for JSON-DOM use on
  fixed shapes. **Owner/Notes:** _—_
- [~] **W7.T7 — Settings hardening.** _(started 2026-08-14)_ `SchemaVersion` is now a first-class
  `Settings` property (default `5.0.0`, no longer dropped on round-trip); `TelemetrySettings` clamps its
  numerics. Remaining: broader validate-on-load, migrate-on-load across the `4.x→5.0` shape, env-var
  secret overrides audit, and startup config logging without secrets. **Owner/Notes:** _—_

---

## §9 — W8: Request capture & history

Mandatory per `BACKEND_ARCHITECTURE`; backs the dashboard Home/Request-History/drill-down. Absent today.

- [x] **W8.T1 — Capture in `PostRouting`.** _(done 2026-08-14)_ `RequestHistoryCaptureService` builds a
  `RequestHistoryEntry` synchronously from the Watson context, redacts secret headers
  (`authorization`/`proxy-authorization`/`cookie`/`set-cookie` and any `*api-key*`/`*token*`), truncates
  the request body to the configured limit, and writes on a background task so it never blocks the
  response. Wired into `PostRouting`, guarded by `RequestHistory.Enabled`. _Note: the current routing
  layer does not retain response bodies, so response metadata + headers are captured but not the body._
- [x] **W8.T2 — `RequestHistorySettings`.** _(done 2026-08-14)_ Enabled (default true), MaxRequest/
  ResponseBodyBytes (clamped 0-1MB, default 65536), RetentionDays (clamped 1-3650, default 30); added to
  `Settings` and the default `sharpai.json`.
- [x] **W8.T3 — `IRequestHistoryMethods` + four-provider implementation.** _(done 2026-08-14)_ Create,
  Read (with bodies), Enumerate (paged, bodies omitted), Summarize (in-memory time buckets, every bucket
  emitted), Delete, DeleteMany, Prune — handwritten SQL over the shared driver; structured columns with
  headers as JSON. `request_history` table added as migration v2 to all four providers. SQLite contract
  suite (create/read, enumerate-omits-bodies, summarize, delete/prune) is green.
- [x] **W8.T4 — Routes** `/v1.0/api/request-history` (list, `/summary`, `/{id}`, delete `/{id}`, bulk
  delete) with OpenAPI metadata under a "Request History" tag. _(done 2026-08-14)_
- [x] **W8.T5 — Hourly prune job** honoring `RetentionDays` (first run after 5 min), disposed on
  shutdown. _(done 2026-08-14)_

- **Surfaces (all):** server, tests, dashboard (W11) · **Ref:** BACKEND_ARCHITECTURE

---

## §10 — W9: Authentication / authorization / accounting

**Decision D1: err toward Ollama** — auth ships **disabled by default** (`Auth.Enabled = false`) so the
out-of-box experience is an open local server like Ollama; the full AAA stack is opt-in for enterprise
and pairs with the four-database requirement. When disabled, requests run as an implicit single system
principal; when enabled, everything below is enforced exactly as `AUTHENTICATION` requires.

- [x] **W9.T1 — Auth mode & default (off by default, Ollama parity).** _(done 2026-08-14)_ `AuthSettings.Enabled`
  defaults `false`. Off: `AuthEvaluator` installs the implicit **system principal** (fully authorized) and
  never challenges — behavior identical to today (Ollama parity). On: a valid admin API key (`x-api-key`,
  constant-time compared) yields an administrator; other requests to non-anonymous paths are challenged
  (401). `/`, `/health`, `/ready`, `/favicon.ico`, `/openapi.json`, `/swagger` stay anonymous in both
  modes. Verified by 6 evaluator descriptors. **Owner/Notes:** _full user/credential/session/RBAC layered on next._
- [x] **W9.T2 — Schema + data-access.** _(done 2026-08-14)_ `Tenant`, `User`, `Credential`, `AuthSession`,
  and `AuditLogEntry` models (guid/active/isprotected/created/lastupdate + tenant columns), with
  `ITenantMethods`/`IUserMethods`/`ICredentialMethods`/`IAuthSessionMethods`/`IAuditMethods` and
  handwritten-SQL implementations. Tables added as **migration v3 across all four providers**;
  `DatabaseDriverBase` exposes `Tenants`/`Users`/`Credentials`/`Sessions`/`Audit`. SQLite contract tests
  (tenant+user+password-verify, credential-by-access-key, session+token round-trip+revoke, audit
  enumerate) are green. Full roles/permissions/assignments tables are deferred to the RBAC engine (T5).
  **Owner/Notes:** _—_
- [x] **W9.T3 — Request-context resolution + typed `RequestContext`.** _(done 2026-08-14; hook corrected
  2026-08-19)_ `RequestContext` (principal type/guid, tenant, IsAuthenticated/IsAdmin/IsTenantAdmin, scheme)
  is built by `AuthenticationService.AttachContext` and attached to `ctx.Metadata`. **Correction:** this was
  originally registered on Watson's `Routes.AuthenticateRequest`, but that hook (and `AuthenticateApiRequest`)
  only fires for routes flagged `requiresAuthentication: true` — which none of the server's routes set — so
  it never ran and every protected route 403'd even with valid credentials. Resolution now runs in the
  `PreRouting` hook (fires for every request); the per-route `Authorize()` helper throws the 401 challenge
  and enforces RBAC. Verified live by the auth-enabled contract suite (see W10.T4). **Owner/Notes:** _—_
- [~] **W9.T4 — Auth schemes.** _(done 2026-08-14; one scheme deferred)_ `AuthenticationEngine` (core,
  framework-free) layers three credentialed schemes on the admin-`x-api-key`/anonymous decision: **header
  login** (x-email/x-password → session), **bearer session token** (Authorization: Bearer / x-token), and
  **access-key + secret-key** (x-access-key/x-secret-key), tried in order, one principal per request. Wired
  into the server via `AuthenticationService`; secrets compared as SHA-256 digests, constant-time. Deferred:
  AWS-style signed request (skew + nonce + canonicalization + constant-time HMAC) — enum value reserved.
  12 engine contract tests green. **Owner/Notes:** _signed-request scheme deferred._
- [x] **W9.T5 — RBAC.** _(done 2026-08-14)_ Full RBAC data layer (`userroles`, `permissions`,
  `rolepermissionmaps`, `userroleassignments`, `credentialscopeassignments`) added as **migration v4 across
  all four providers**, with interface/implementation data-access wired into `DatabaseDriverBase`. The pure,
  framework-free `RbacEngine` evaluates the `(tenant, principal, resourceType, operation, resourceGuid?)`
  tuple with **explicit-deny-wins**, tenant vs resource scope, `InheritsToChildren`, `Write`→Create/Update/
  Delete expansion, `All` wildcards, the `IsAdmin`/`IsTenantAdmin` bypass rules, and the **credential
  owner-ceiling**. Six immutable built-in roles (TenantAdmin/SecurityAdmin/Auditor/Editor/Viewer/
  TenantMember, null tenant + protected) are seeded idempotently at startup (`RbacSeeder`), resolvable by
  GUID or name. 10 evaluator contract tests are green. Remaining follow-on: per-route operation-scope
  mapping to enforce these gates on the data plane (tracked with W9.T8). **Owner/Notes:** _—_
- [x] **W9.T6 — Session tokens.** _(done 2026-08-14)_ `AuthSession` (server-side, revocable, with expiry)
  + `SessionTokenService` (AES-256-CBC, **fresh random IV per token**). Server endpoints landed:
  `POST /v1.0/token` (login → token), `GET /v1.0/token` (session details), `DELETE /v1.0/token` (revoke),
  all with OpenAPI metadata and reachable anonymously so login works without a prior credential. Token TTL
  is configurable (`Auth.SessionTtlMinutes`); the AES key material is `Auth.TokenSigningKey` (random
  per-boot when unset, logged as a warning). **Owner/Notes:** _—_
- [x] **W9.T7 — Audit stream.** _(done 2026-08-14)_ `AuditLogEntry` + `IAuditMethods` across the
  four-provider layer. `AuthenticationService` now records an audit entry on every authentication denial
  (event type, method, path, source IP, 401), and `GET /v1.0/api/audit` exposes a paginated, tenant-scoped
  audit feed — global admins may scope by `tenantGuid`; tenant admins are constrained to their own tenant;
  non-admins get 403. **Owner/Notes:** _—_
- [x] **W9.T8 — Enforcement + effective-permissions inspection.** _(done 2026-08-14)_ RBAC is now enforced
  on the data plane: a central `Authorize(req, resourceType, operation, resourceGuid)` gate maps every
  control-plane and inference route to its `(ResourceType, Operation)` cost and calls `RbacEngine.Authorize`,
  auditing denials and returning **403** with a reason. Admin-class routes (settings write, request-history,
  audit) require `Admin`/resource grants; inference maps to `Inference:Execute`, model management to
  `Model:Write`/`Delete`, reads to `Read`. When auth is disabled every request runs as the system principal
  so the gate is a no-op (Ollama parity preserved). Inspection endpoints added:
  `GET /v1.0/tenants/{tenantGuid}/users/{userGuid}/permissions` and `.../credentials/{credentialGuid}/permissions`
  (Admin, or the principal reading its own). **Owner/Notes:** _—_
- [x] **W9.T9 — Account & RBAC management API.** _(done 2026-08-15)_ Tenant-scoped, RBAC-gated CRUD for the
  AAA data plane under `/v1.0/tenants/...`: tenants (platform-admin), users, credentials, roles, permissions,
  role→permission mapping, and user role assignments. Each route enforces tenant isolation + its permission
  gate; secrets/password hashes are redacted (credential secret returned once at creation); user delete
  cascades to credentials + assignments; built-in/protected records are immutable. Typed DTOs + OpenAPI.
  Data layer locked by a new SQLite contract test. **Owner/Notes:** _—_
- **Surfaces (all):** core/server, dashboard, sdk, tests, docs · **Ref:** AUTHENTICATION

---

## §11 — W10: Test architecture & ~100% coverage

`BACKEND_TEST_ARCHITECTURE` mandates **Touchstone** (source at `C:\code\touchstone`; packages
`Touchstone.Core`, `Touchstone.Cli`, `Touchstone.XunitAdapter`, `Touchstone.NunitAdapter`) with
`Test.Shared` / `Test.Automated` / `Test.Xunit` / `Test.Nunit`. The goal is Ollama-or-better
reliability, so this workstream is **P0** and the coverage target is ~100% of meaningful paths in the
core library and server. All harnesses bind and target `127.0.0.1`, never `localhost`.

- [x] **W10.T1 — Stand up the Touchstone projects.** _(done 2026-08-14)_
  Created `src/Test.Shared` (Touchstone.Core 0.1.12 only, zero console output — `SharpAISuites.All`
  aggregates `ChatFormatSuite`, `ChatPromptBuilderSuite`, `TextGenerationSuite`, `ThinkingFilterSuite`,
  **80 descriptors** covering the full deterministic prompt/format/thinking surface), `src/Test.Automated`
  (Touchstone.Cli console runner with `--results` JSON), `src/Test.Xunit` (Fact + Theory + coverlet),
  `src/Test.Nunit` (TestCaseSource + coverlet). All target `net8.0;net10.0` and are in `SharpAI.sln`.
  **Verified green:** console 80/80, xUnit 81/81, NUnit 80/80. Coverage gate wiring lands with W10.T9/W15.
  **Owner/Notes:** _harness proven end-to-end on all three runners._
- [~] **W10.T2 — Retire/relocate the ad-hoc console test apps.** _(started 2026-08-14)_ Retired the
  redundant `SharpAI.Tests` xUnit project (its `PromptSupportTests` are fully covered by the new
  suites) — removed from the solution and deleted. Remaining: fold `Test.HuggingFace`,
  `Test.LlamaSharpProvider`, `Test.PromptBuilder`, `Test.SharpAIDriver` console apps into shared
  descriptors (or a `test/` location) as their behaviors gain suite coverage. **Owner/Notes:** _—_
- [~] **W10.T3 — Core inference suites** against a tiny committed/downloaded GGUF. _(advanced 2026-08-21)_
  The model fixture (`ModelFixture`) + `ModelInferenceSuite` are in place and now **exercised against a real
  GGUF** (Qwen2.5-1.5B, downloaded to `src/test-models/`, gitignored so CI stays model-less): init, embedded
  chat-template rendering, small-`max_tokens` generation, concurrent generation, embeddings, **and the W1.T6
  template golden** — 185/185 automated cases green with the model present; all skip cleanly without it. This
  run **caught the critical generation-slot StackOverflow** (see W2.T4). Remaining: stop handling and
  thinking-filter assertions on real output, and lifecycle assertions (evict/keep-alive/LRU, admission
  refusal). **Owner/Notes:** _real-model smoke + golden done; lifecycle assertions pending._
- [~] **W10.T4 — Server contract suites.** _(live smoke verified 2026-08-19)_ Ran the **real server**
  end-to-end (net8.0, CPU backend, temp SQLite, auth off) and verified the runtime contracts by HTTP:
  `/health`, `/ready` (incl. telemetry readiness), `/api/version`, **`/v1/models`** (`{"object":"list","data":[]}`),
  `/api/tags`, `/openapi.json`, `/v1.0/api/request-history` + `/v1.0/tenants` (EnumerationResult envelope;
  request-history capture + auth seeding confirmed working), and **error-shape parity (W4.T7)** — a
  missing-model chat returns HTTP 404 with the OpenAI envelope `{"error":{"message","type":"invalid_request_error","code":"model_not_found"}}`
  and Ollama's `{"error":"..."}` string. **Now automated:** a new **`Test.Server`** console harness
  (`ServerContractSuite`, Touchstone) runs these HTTP contract assertions against a running server — **10/10
  green locally** (the 10th asserts `/metrics` → 404 when telemetry is disabled; see W10.T7) — and a
  **`server-contract.yml`** CI workflow builds the server, starts it with a
  throwaway config, runs the harness, and tears it down. An **auth-enabled leg** (`ServerAuthContractSuite`,
  run with `--auth` against a second server whose `AdminApiKeys` = `["test-admin-key"]`) verifies the 401
  challenge on protected routes, anonymous routes staying open, admin-key bypass, the email/password login →
  bearer flow, and RBAC **403** denial for an unprivileged principal — **4/4 green locally**, and now a
  second CI leg. Remaining: streaming (SSE/NDJSON) assertions, tool-calling/JSON-mode (model-gated), and the
  full per-route matrix.
  _(The auth leg **caught a critical latent bug**: auth was wired to a Watson hook that never fired for the
  server's routes, so `Auth.Enabled = true` 403'd every request regardless of credentials — see W9.T3 and
  the CHANGELOG. The auth-off smoke also caught + fixed a bad `SchemaVersion` example in DEPLOYMENT-GUIDE —
  it must be a string.)_
  **Owner/Notes:** _—_
- [x] **W10.T5 — Provider-matrix DB suites (required — decision D2).** _(done 2026-08-24)_ The SQLite
  contract runs in-process (`DatabaseSuite`/`ModelPresetSuite`). The **model-registry + preset +
  request-history contract now runs against real servers** for all four providers via a provider-agnostic
  runner (`DbMatrixRunner`) driven by a new `Test.Automated --dbmatrix` mode (provider/host/port/db/user/pass
  from `SHARPAI_DBTEST_*`). Verified against **PostgreSQL 16 (13/13), MySQL 8.4 (13/13), and SQL Server 2022
  (13/13)** using ephemeral Docker containers — each ran migrations v1–v5 and exercised CRUD/enumerate/update/
  delete for models, presets, and request history. Reproducible via `scripts/db-matrix.sh` (spins the three
  containers, runs the matrix, tears down). **Owner/Notes:** _4-DB claim now proven against live servers, not
  just SQLite; wiring the script as a CI job is the remaining step._
- [x] **W10.T6 — Auth/authz suites.** _(done 2026-08-16)_ Deny-wins, tenant/resource scope,
  `InheritsToChildren`, credential owner-ceiling, admin/tenant-admin bypass, **cross-tenant isolation**,
  unresolvable-role, and the **auth-disabled open-server** path (system principal) are covered by
  `AuthEvaluatorSuite`, `AuthenticationEngineSuite` (login/bearer/access-key/revoke/expiry, 12 cases),
  `RbacEngineSuite` (14 cases), and `ReliabilitySuite`. Session revocation + expiry are asserted end to end.
  Signed-request replay/skew is out of scope until the signed-request scheme (W9.T4) lands. **Owner/Notes:** _—_
- [~] **W10.T7 — Telemetry suites.** _(live contract verified 2026-08-19)_ A new **`ServerTelemetryContractSuite`**
  (`Test.Server`, run with `--telemetry` against a telemetry-enabled server) verifies at runtime that the
  Watson-native **`/metrics`** endpoint is served, emits **valid Prometheus exposition (0.0.4)** with the
  expected HTTP-server series (`http_server_request_duration_seconds`, `watson_route_matches_total`), and
  that traffic is actually recorded — the per-route request **counter increments** after additional
  `/health` requests. **3/3 green locally** and a third **`server-contract.yml`** CI leg. The
  **telemetry-disabled no-op** (`/metrics` → 404 when `Telemetry.Enable = false`) is asserted by the default
  suite (W10.T4). Remaining: assert the custom `sharpai.*` inference/model series (model-gated, and they
  export via Radiant/OTLP rather than the Watson Prometheus endpoint) and span production. **Owner/Notes:** _—_
- [~] **W10.T8 — Fault-injection & reliability suites.** _(started 2026-08-16)_ `ReliabilitySuite` covers
  the deterministic core: numeric clamping (page bounds/offset), token crypto against malformed/foreign-key
  input (fresh-IV distinctness, non-base64, truncated, wrong key → null), constant-time password verify,
  key-generation entropy, and anonymous/protected path classification. Remaining (needs the live in-process
  server harness from T4): oversized inputs, cancelled requests, backend-load failure, disk-full on pull,
  concurrent pull+delete, OOM admission. **Owner/Notes:** _—_
- [ ] **W10.T9 — Coverage gate in CI** (fail under threshold; publish the report artifact). Track the
  number honestly; document any intentionally-uncovered native-interop lines. **Owner/Notes:** _—_
- _Coverage expansion (2026-08-19):_ added deterministic suites for request-history query parsing +
  numeric clamping (`RequestHistoryQuerySuite`) and a SQLite request-history **filter** contract case
  (method/status/path) and a **model-registry** filter contract case (family/quantization/exact-name), plus
  the tool-calling prompt/response-mapping suite (W4.T1). Total across the three runners is now ~179
  deterministic cases (was ~130).
- **Surfaces (all):** tests, ci · **Ref:** BACKEND_TEST_ARCHITECTURE

---

## §12 — W11: Dashboard rebuild to standard

`FRONTEND_ARCHITECTURE` + `DASHBOARD_STYLE_AND_USABILITY` are prescriptive and the current dashboard
does not match the mandated stack or feature set. Treat as a rebuild.

- [x] **W11.T1 — Authenticated shell.** _(done 2026-08-17)_ Built the rebuilt `AppShell`
  (`dashboard/src/app/`): grouped sidebar navigation, a topbar carrying the server URL, version, a **live
  health indicator** (polls `/health` via the ApiClient), a repo link, and a **theme toggle** wired to the
  CSS-variable tokens; plus an `ApiProvider` (React context over the hand-rolled `ApiClient`) and a
  `useTheme` hook. A `NewApp` root composes it end-to-end. Verified by `npm run build`. Remaining: identity/
  role + logout (needs the auth session store), language selector (W12), and moving nav to React Router 7.
  **Entry-point cutover complete (2026-08-16):** `main.tsx` now boots the rebuilt app and the entire legacy
  tree (App.tsx, pages, Ant-Design components, hoc, redux, scss) was deleted; axios, antd, redux, router,
  and sass were removed from `package.json` (**695 packages pruned, 0 vulnerabilities**). The dashboard is
  now a single-entry SPA — production bundle **170 kB JS (54 kB gzip) / 40 modules**, down from
  ~2.17 MB / 3,940 modules. **Auth is wired end to end (2026-08-16):** an `AuthProvider` owns the session
  and the token-bound ApiClient, a `LoginModal` exchanges email/password for a bearer token, the session is
  persisted across refreshes, and the topbar shows the signed-in identity with **Sign out** (revokes the
  session); the dashboard still works anonymously when the server runs open. Remaining: React 18→19 + adopt
  React Router 7 for real routing, and the language-selector control (W12). **Owner/Notes:** _—_
- [x] **W11.T2 — Stack alignment.** _(done 2026-08-17)_ Laid the framework-agnostic
  foundation the rebuild builds on, verified by `npm run build` (full `tsc` + vite bundle, green): a
  hand-rolled fetch **`ApiClient`** (`dashboard/src/api/`, **no axios**) with typed
  EnumerationQuery/EnumerationResult envelopes, bearer/api-key/access-key auth, `ApiError` normalization,
  and coverage of health/settings/models/inference/request-history/token/audit/management/effective-
  permissions; **CSS-variable theming** (`src/theme/tokens.css`, light + dark via `prefers-color-scheme`
  + `[data-theme]` override) and a `themeController` (persist/toggle/stamp). Remaining: migrate the app
  from React 18→19 and adopt React Router 7. **axios, Ant Design, Redux, React-Router, and sass are all
  removed** and the legacy tree is deleted (cutover done, see T1). The broken jest harness was replaced with
  a working **Vitest** runner. The stack now matches the standard: **React 19**, **Vite 8**, **React Router 7**
  (deep-linkable routes for all seven views + browser history; nginx SPA fallback already in place), the
  hand-rolled **no-axios `ApiClient`**, and CSS-variable theming. 11 unit tests green (ApiClient, auth,
  StatusBadge, theme); a one-line `JSX.Element` compatibility shim covers React 19's removal of the global
  JSX namespace. **Owner/Notes:** _—_
- [x] **W11.T3 — Shared components.** _(done 2026-08-16)_ Delivered an
  Ant-Design-free component library under `dashboard/src/components/ui/`, tokens-only styling, verified by
  `npm run build`: **DataTable** (explicit loading/error/empty states, delegated sortable headers,
  own-container horizontal scroll, above-table **Pagination** with page sizes `[10,25,50,100,250,500,1000]`
  default 25), **Modal**/**ConfirmModal** (portaled, Escape/overlay close, no native `alert/confirm/prompt`),
  **JsonViewer**, **CopyButton**/**CopyableId**, **StatusBadge** (color-not-sole-signal), and
  **FilterBar** (backend-driven text/select filters). Remaining: portaled ActionMenu, then finish wiring the
  set into all pages under visual+a11y QA (T10). **Owner/Notes:** _—_
- [x] **W11.T4 — Home/Overview.** _(done 2026-08-17)_ `HomeView` shows domain KPIs (models on disk,
  resident models, request volume), the hand-rolled SVG **activity chart**, a **manual Refresh** control, and
  a **recent-failures panel** (recent request-history entries with status ≥ 400, shown with status pills).
  Tokens/sec live on the Observability page (T9). **Owner/Notes:** _—_
- [x] **W11.T5 — Request History view** (consumes W8). _(done 2026-08-17)_ `RequestHistoryView` on the
  ApiClient + `DataTable`: a **KPI strip** (requests, success rate, avg duration) and a hand-rolled SVG
  **activity chart** (exact success/failure bucket counts) whose bars **click to filter** the table by that
  time range; a backend-backed `FilterBar` (method/status/path); paginated table with a status pill;
  row-action "Details" opening a modal that renders the full entry as **copyable JSON**; loading/error/empty
  states. Both list and summary reflect the active filters. **Owner/Notes:** _—_
- [x] **W11.T6 — API Explorer.** _(done 2026-08-17)_ `ApiExplorerView` browses the live `/openapi.json`
  grouped by tag, and now **executes requests in-app**: an editable path (for `{param}` substitution), a JSON
  request-body editor for non-GET methods, a Send button that calls the endpoint with **inherited auth**, the
  response rendered with a **status pill + JsonViewer**, a copyable `curl`, and a **confirm dialog on DELETE**.
  Deferred polish: fully spec-generated per-parameter forms, additional snippet languages, and per-origin
  request history. **Owner/Notes:** _—_
- [x] **W11.T7 — Model management + inference playgrounds.** _(done 2026-08-17)_ `ModelsView` lists on-disk
  (`/api/tags`) and running (`/api/ps`) models with delete/unload behind `ConfirmModal` and a streaming pull
  modal. `PlaygroundView` now **streams chat tokens live** (NDJSON via the ApiClient's new `chatStream`) and
  has an **embeddings tab** (dimensions + vector preview) alongside chat. Remaining polish (deferred):
  import-from-file and richer per-model metadata. **Owner/Notes:** _—_
- [x] **W11.T8 — Settings / Server Info.** _(done 2026-08-17)_ `SettingsView` now shows a **structured
  server-info panel** (version, backend, native-init, DB provider, auth mode, telemetry — from `/health` +
  `/api/settings`) above the raw settings JSON editor (edit + validate + PUT with save/error status).
  **Owner/Notes:** _—_
- [x] **W11.T9 — Observability panel.** _(done 2026-08-17)_ `ObservabilityView` shows live health/readiness
  probes and **in-app metric summaries** — it polls `/metrics` every 5s, parses the Prometheus exposition
  (`sumMetric`/`parseSample`), and renders **request rate, tokens/sec, resident models, and average latency**
  (rates computed from consecutive counter samples; values locale-formatted via the W12 `Intl` helpers), with
  graceful "metrics unavailable" handling. Links to `/metrics` and Grafana (`:9400`). Parser covered by 4
  unit tests. **Owner/Notes:** _—_
- [x] **W11.T10 — Visual + accessibility QA.** _(done 2026-08-19)_ Playwright harness (`playwright.config.ts`
  + `e2e/dashboard.spec.ts`) runs across **4 viewport/theme projects** (1280/768/390 px, light + dark) and
  checks semantic landmarks (header/main), sidebar routing, the theme toggle, **no horizontal overflow**, and
  **axe accessibility** (no critical violations). **Ran locally: 18 passed, 2 skipped** (nav is correctly
  skipped where the sidebar collapses). The run **caught a real mobile horizontal-overflow bug** (topbar not
  wrapping at 390px), which was fixed. CI job `e2e.yml` installs Chromium and runs the suite. **Owner/Notes:** _—_

---

## §13 — W12: Internationalization

`I18N` is architectural, not optional; CI must block missing keys. Reference implementation: Hydra.

- [x] **W12.T1 — i18n foundation.** _(done 2026-08-17)_ `dashboard/src/i18n/` with `config.ts` (i18next +
  react-i18next, init before first paint via `main.tsx`), `locales.ts` (registry: code/English/native
  autonym/dir/fallback for **en**, **ar (RTL)**, and a generated **pseudo-locale**), `en.json`/`ar.json`
  catalogs, `format.ts`, and a shared `LanguageSelector` in the shell. Deterministic detection (persisted →
  browser → default) is implemented directly rather than via the language-detector plugin. **Owner/Notes:** _—_
- [x] **W12.T2 — Externalize every operator string.** _(done 2026-08-17)_ Every operator-facing string
  across the shell and all seven views (Overview, Models, Playground, Request History, API Explorer,
  Observability, Settings) plus the login dialog now flows through `t()` — titles, buttons, table headers,
  filters, empty/error/loading states, confirm dialogs (with `{{name}}` interpolation), and `aria-label`s.
  Catalogs: `en` (complete) + `ar` (RTL); untranslated keys fall back to English. **Owner/Notes:** _—_
- [x] **W12.T3 — Explicit-locale formatters.** _(done 2026-08-17)_ `format.ts` provides
  `formatNumber/Percent/DateTime/DurationMs/Bytes/List` (via `Intl.*`, each taking an explicit locale),
  unit-tested. Remaining polish: route the chart tooltips + view timestamps through them. **Owner/Notes:** _—_
- [x] **W12.T4 — `lang`/`dir` sync + persistence.** _(done 2026-08-17)_ Locale changes centrally stamp
  `document.documentElement.lang`/`dir` (RTL for `ar`), persist to `localStorage`, and apply without a
  reload; the selector lives in the always-present shell so it works before and after sign-in and survives
  deep links. **Owner/Notes:** _—_
- [x] **W12.T5 — Server-text strategy.** _(done 2026-08-17)_ The `ApiClient` sends **`Accept-Language`** from
  the active locale on every request (and the streaming pull). Documented strategy (i18n README): server
  returns stable codes/enums that the client localizes; server-localized text is acceptable when driven by
  `Accept-Language`; rendered strings are never persisted. **Owner/Notes:** _—_
- [x] **W12.T6 — i18n CI + QA.** _(done 2026-08-17)_ Generated pseudo-locale (expansion + accents), a
  **missing/orphaned-key gate** (`i18n.test.ts` asserts every non-source catalog key exists in `en`), an
  **RTL smoke test** (locale switch stamps `dir=rtl`/`lang=ar`), formatter tests incl. RTL direction, and a
  maintenance doc (`src/i18n/README.md`). The key-gate test runs under `npm test`; wiring it as a blocking
  CI step lands with W15. **Owner/Notes:** _—_

---

## §14 — W13: SDKs (C#, JS, Python)

Reach parity, cover the new endpoints, use `127.0.0.1` loopback, and each ship a thorough test harness
+ README. Reference: SharpAI is itself cited as the SDK-harness example.

_New-endpoint parity (2026-08-23):_ all three SDKs gained `importModel` (`POST /api/import`) and preset CRUD
(`list/create/get/delete` over `/v1.0/models/presets`) — C# (`OllamaMethods`, builds green), JS
(`OllamaMethods`, build + a new mocked-fetch `ollama.test.ts`, 7/7), Python (`client.py`, unittest 6/6).
Tool-calling and JSON-mode need no new SDK surface (they flow through the existing chat request/response shapes).

- [x] **W13.T1 — C# SDK.** _(done 2026-08-17)_ `SharpAI.Sdk` now carries **auth** (bearer token / admin API
  key / access-key+secret via constructor params, injected into every request) plus a generic `SendAsync`,
  and a new **`Admin`** group (`IAdminMethods`/`AdminMethods`) covering settings, request history,
  `LoginAsync`/`SessionAsync`/`LogoutAsync`/`AuditAsync` (login stores the token), and the account/RBAC
  management surface (tenants/users/credentials/roles/assignments/effective-permissions) with typed request
  DTOs. Builds clean on **net8.0 + net10.0** (only the pre-existing NU1903 transitive advisory remains);
  README updated. The existing tool-calling/`/v1/models`/`/api/show` coverage in the Ollama/OpenAI groups is
  retained. **Owner/Notes:** _—_
- [x] **W13.T2 — JS/TS SDK.** _(done 2026-08-17)_ Extended `@sharpai/sdk`: the base client now carries
  **auth** (bearer token / admin API key / access-key+secret, injected into every request) and a generic
  `sendAsync`, plus a new **`admin`** group (`IAdminMethods`/`AdminMethods`) covering settings, request
  history, `login`/`session`/`logout`/`audit` (login stores the token), and the account/RBAC management
  surface (tenants/users/credentials/roles/assignments/effective-permissions). Typecheck + build
  (CJS/ESM/DTS) clean; a mocked-fetch **Vitest harness (4 tests, green)**; README updated. **Owner/Notes:** _—_
- [x] **W13.T3 — Python SDK.** _(done 2026-08-17)_ New zero-dependency (stdlib-only) `sharpai` package
  under `sdk/python/` (`pyproject.toml`, `src/sharpai/{client,errors,__init__}.py`) covering health/settings,
  Ollama models + inference (incl. **streaming `pull_model`**), the OpenAI-compatible endpoints, request
  history, auth (login/session/logout/audit, with token storage), and the account/RBAC management surface;
  optional bearer/api-key/access-key auth; `SharpAIError` on non-2xx. Ships a mocked-HTTP **unittest harness
  (6 tests, green)** and a README with quickstart + endpoint coverage — this also lands the Python portions
  of T4/T5. **Owner/Notes:** _—_
- [ ] **W13.T4 — Per-SDK test harness** against a live `127.0.0.1` server (streaming + error shapes). **Owner/Notes:** _—_
- [x] **W13.T5 — Per-SDK README.** _(done 2026-08-17)_ All three SDK READMEs cover install, quickstart,
  auth, and endpoint coverage (Python new; C# and JS extended with an auth + admin-surface section).
  **Owner/Notes:** _—_
- [x] **W13.T6 — Version + publish pipeline.** _(done 2026-08-17)_ `.github/workflows/release.yml`
  publishes all three SDKs on a `v*` tag — **NuGet** (`dotnet pack`/`push`), **npm** (`npm publish --access
  public`), **PyPI** (`python -m build` + `twine`) — each gated on its checks (C# build; JS `npm test`+build;
  Python `unittest`) and auto-skipping publish when the secret is absent. C# pack + Python build + JS
  test/build validated locally. This is also W15.T5. **Owner/Notes:** _—_

---

## §15 — W14: Docs & repository housekeeping

Follow `WRITING_DOCUMENTS` for prose: real paragraphs, no stock lead-ins, no generic recap.

- [x] **W14.T1 — `DOCKERHUB_README.md`.** _(done 2026-08-17)_ Created with use cases, what's-inside
  architecture, getting-started (run + curl), GPU, and observability; logo referenced by explicit raw
  GitHub URL. **Ref:** REPOSITORY_REQUIREMENTS · **Owner/Notes:** _—_
- [~] **W14.T2 — README accuracy pass.** _(started 2026-08-17)_ Added a "What's new in 5.0" section
  (embedded chat templates, rebuilt dashboard, AAA off-by-default, 4-DB, observability, request history,
  SDK parity); fixed the `yourusername` issues link → `jchristn/SharpAI`; corrected the Docker Compose
  section to reference the shipped `docker/compose*.yaml` files instead of "create your own". Remaining:
  a deeper pass on the tool-calling / model-import / GPU-matrix / HF-token-optionality prose. **Owner/Notes:** _—_
- [x] **W14.T3 — CHANGELOG discipline.** _(done 2026-08-17)_ `CHANGELOG.md` carries a
  `## Unreleased — v5.0.0 (in progress)` section with the 5.0.0 version story and Added/Changed/Removed
  entries appended per slice as workstreams land. **Owner/Notes:** _—_
- [x] **W14.T4 — Update `CLAUDE.md` + `DEPLOYMENT-GUIDE.md`.** _(done 2026-08-19)_ Fixed the stale
  `src/CLAUDE.md` dependency notes (SwiftStack → Watson 7.1; Watson.ORM.Sqlite → the hand-written
  provider-neutral ADO.NET 4-DB layer; added Radiant telemetry; multi-target net8.0/net10.0). Refreshed
  `DEPLOYMENT-GUIDE.md` for v5: the complete-config example now includes the **Database/Auth/Telemetry/
  RequestHistory** blocks, with a "Version 5.0 configuration blocks" subsection explaining each (4-DB,
  auth-off-by-default, OTLP+Prometheus, request-history retention) and a **Dashboard & observability**
  section pointing at `docs/OBSERVABILITY.md` and Grafana. **Owner/Notes:** _—_
- [x] **W14.T5 — Repo layout compliance.** _(done 2026-08-19)_ Audited: all source lives under `src/`
  (core, server, and the Test.* projects), `dashboard/`, and `sdk/{csharp,js,python}`; docs under `docs/`,
  Docker assets under `docker/`, the archived plan under `archive/`. **No stray source** (`.cs`/`.csproj`/
  `.sln`/`.ts`/`.py`) outside those roots. (Minor: a stale `.gitignore` line references a non-existent
  `src/Docker/models/` — harmless.) **Owner/Notes:** _—_
- [ ] **W14.T6 — Docker asset conventions.** `.yaml` with build contexts; per-provider local compose
  profiles (`mysql`/`postgres`/`sqlserver`, SQLite default); `.dockerignore` coverage; stop committing
  runtime artifacts (`docker/logs/`, `docker/models/`, mutated `docker/sharpai.db`) — add to
  `.gitignore`. **Owner/Notes:** _—_
- [x] **W14.T7 — NuGet packaging check.** _(done 2026-08-17)_ Audited both packable projects: `SharpAI` and
  `SharpAI.Sdk` each set `IncludeSymbols`/`SymbolPackageFormat=snupkg`, `PackageReadmeFile`, `PackageIcon`,
  and `PackageLicenseFile`, and pack README/logo/LICENSE via `<None Pack=...>`; both at `5.0.0`; `SharpAI.Sdk`
  packs cleanly (verified). _Follow-up for release: `SharpAI.Sdk` still references the published `SharpAI`
  `1.0.17` (kept so it restores today) — bump to `5.0.0` once the core package is published._ **Owner/Notes:** _—_
- [x] **W14.T8 — Observability runbook.** _(done 2026-08-17)_ `docs/OBSERVABILITY.md` documents the
  `Telemetry` config, the full metrics catalog (`sharpai.inference.requests`/`tokens_generated`/`latency`,
  `sharpai.models.resident` + the Watson HTTP meter) with both OTLP and Prometheus name forms and example
  PromQL, traces (`SharpAI.Inference` activity source + tags), how to read the two provisioned Grafana
  dashboards, and how to point at an external OTLP collector or scrape `/metrics` directly. _(Also fixed a
  wrong Grafana link in the dashboard Observability page: `:3300` → `:9400`.)_ **Ref:** WRITING_DOCUMENTS
  · **Owner/Notes:** _—_

---

## §16 — W15: CI/CD

No `.github/workflows` exists today. CI is where the reliability and i18n guarantees become real.

- [x] **W15.T1 — Build + test workflow.** _(done 2026-08-17)_ `.github/workflows/backend.yml`: restore +
  Release build of `src/SharpAI.sln` on **net8.0 + net10.0**, runs `Test.Automated` (Touchstone CLI, uploads
  the JSON artifact — non-zero exit gates), then `Test.Xunit` + `Test.Nunit` with `XPlat Code Coverage`
  uploaded as an artifact. Every command was validated locally. Remaining: a hard fail-under-coverage
  threshold once a measured baseline exists. **Owner/Notes:** _—_
- [x] **W15.T2 — Dashboard workflow.** _(done 2026-08-17)_ `.github/workflows/dashboard.yml`: `npm ci`,
  `npm run lint` (eslint flat config repaired for the React 19 + TS stack — 0 errors), `npm run build`
  (tsc + vite), and `npm test` (Vitest — includes the **i18n orphaned-key gate** + pseudo-locale/RTL checks).
  Remaining: Playwright visual QA (W11.T10) and tightening lint to `--max-warnings 0`. **Owner/Notes:** _—_
- [~] **W15.T3 — Docker image build.** _(started 2026-08-17)_ `.github/workflows/docker.yml` builds the
  dashboard image and **validates all three compose files** (`compose.yaml`, `compose-cpu`, `compose-cuda`
  — verified locally with `docker compose config`). Remaining: build/push the CPU + CUDA **server** images
  (native LLamaSharp, larger runners + registry secrets) and the DB service profiles for the W10.T5 matrix.
  **Owner/Notes:** _—_
- [x] **W15.T4 — Observability smoke.** _(done 2026-08-17)_ `.github/workflows/observability-smoke.yml`
  brings up the telemetry half of `docker/compose.yaml` (`--no-deps`: collector, Prometheus, Loki, Tempo,
  Grafana — the server image isn't needed) and asserts Prometheus is ready with its scrape **config +
  targets** loaded and Grafana provisions its **prometheus/loki/tempo datasources**. **Validated locally
  end to end** (all assertions passed against the live stack). **Owner/Notes:** _—_
- [x] **W15.T5 — Release publishing.** _(done 2026-08-17)_ Delivered as `release.yml` (see W13.T6): tag-
  triggered NuGet/npm/PyPI publish, each gated on the SDK's checks and skipped when its secret is unset.
  **Owner/Notes:** _—_

---

## §17 — W16: Operations hardening

- [x] **W16.T1 — Graceful shutdown / draining.** _(done 2026-08-17)_ A **SIGTERM** handler
  (`PosixSignalRegistration`) plus the existing SIGINT hook route through one `RequestShutdown`;
  `GracefulShutdownAsync` drains in-flight requests for a short window, stops the server, disposes the prune
  timer / engines / database, and disposes the telemetry host **last so its OTLP exporters flush**. `docker
  stop` now shuts down cleanly instead of being killed. **Owner/Notes:** _—_
- [x] **W16.T2 — Structured startup logging.** _(done 2026-08-17)_ `LogStartupSummary` emits one line after
  the server starts: version, resolved backend, native-init state, DB provider, models directory, telemetry
  endpoint + enabled state, and auth mode — with **no secrets** (DB password, HF key, token key, and admin
  keys are never logged). **Owner/Notes:** _—_
- [x] **W16.T3 — Readiness fidelity.** _(done 2026-08-17)_ `/ready` already gated on native/backend init,
  DB initialization (migrations applied), and writable models/logs dirs — now also on **telemetry-host
  readiness** (satisfied when telemetry is disabled or the host is active), returning 503 + a per-check body
  when not ready. `/health` remains a cheap liveness probe (status/version/backend/native-init). **Owner/Notes:** _—_

---

## §18 — W17: Observability & telemetry

Enterprise operators need to see how the stack behaves. This workstream instruments the app and ships a
turnkey Prometheus + Loki + Grafana + Tempo stack in Docker, modeled on `C:\code\xeno` (layout +
factory reset) and `C:\code\less3\less3-2.1` (Watson-native `/metrics` scrape). Telemetry is **opt-in**
via config and a clean no-op when disabled.

**Design.** The app emits through the .NET BCL (`Meter`/`ActivitySource`/`ILogger`). **Radiant**
(`Radiant` 0.1.2, source `C:\code\radiant`) hosts the OpenTelemetry pipeline in-process and pushes OTLP
to a collector; **Watson 7.1.0** emits its own `"Watson"` HTTP-server meter and spans. The collector
fans out to Prometheus (metrics), Tempo (traces), and Loki (logs, via `filelog` tailing the existing
`sharpai.log.*` files). Grafana reads all three via provisioned datasources + file-provisioned
dashboards.

- [x] **W17.T1 — Instrument the core library with the BCL (no Radiant reference in `SharpAI`).**
  _(done 2026-08-14)_ Added `SharpAI.Telemetry.SharpAITelemetry`: meters `SharpAI.Inference` /
  `SharpAI.Models` and activity source `SharpAI.Inference`, with counter `sharpai.inference.requests`,
  counter `sharpai.inference.tokens_generated`, histogram `sharpai.inference.latency` (seconds), and an
  observable gauge `sharpai.models.resident`. All four `LlamaSharpEngine` generation methods record
  latency/requests/tokens (tagged `operation`/`model`/`outcome`, low-cardinality); `ModelEngineService`
  feeds the resident-model gauge. Pure BCL, no host dependency; guarded by a no-op smoke suite.
  **Surfaces:** core, tests · **Owner/Notes:** _metric names match the Grafana Overview panels._
- [x] **W17.T2 — Radiant host in the server composition root.** _(done 2026-08-14)_ Added `Radiant`
  `0.1.2` to `SharpAI.Server`; `TelemetryHost` starts `RadiantHost` from the new `Settings.Telemetry`
  section, subscribes to the `SharpAI.*` meters/source and Watson's `"Watson"` source, configures OTLP,
  and disposes on shutdown. Startup failures are swallowed (logged, server continues). Wired into
  `Program.cs` (`InitializeTelemetry`) and disposed after `_Server.Dispose()`. Build verified.
  **Owner/Notes:** _off cleanly when `Telemetry.Enable=false`._
- [x] **W17.T3 — Watson 7.1 native telemetry.** _(done 2026-08-14)_ With Watson at 7.1.0, the server
  enables `WebserverSettings.Telemetry` and serves the in-process Prometheus `/metrics` endpoint on the
  existing listener whenever `Settings.Telemetry.Enable` is true; `prometheus.yaml`'s `sharpai` job
  scrapes it. Watson spans export over OTLP through Radiant (which already subscribes to the `Watson`
  source), while Watson metrics come from `/metrics` to avoid double-counting, per the less3 pattern.
  **Owner/Notes:** _—_
- [x] **W17.T4 — `TelemetrySettings`.** _(done 2026-08-14)_ `TelemetrySettings` (Enable, ServiceName,
  Otlp endpoint/protocol, Prometheus enable/host/port/path, Metrics/Traces/Logs toggles) with clamps and
  `SHARPAI_TELEMETRY_*` env overrides; added to `Settings`; a `Telemetry` block is present in the docker
  `sharpai.json` defaults (OTLP → `otel-collector:4317`). **Owner/Notes:** _—_
- [x] **W17.T5 — Docker observability stack** under `docker/telemetry/` and a new `docker/compose.yaml`.
  _(done 2026-08-14)_ Authored `compose.yaml` (app + dashboard + `otel-collector` 0.109.0,
  `prometheus` v2.55.1, `loki` 3.2.1, `tempo` 2.6.1, `grafana` 11.3.0), `otel-collector-config.yaml`
  (`filelog` tails `/app/logs/*.log*` → Loki; `otlp` → Prometheus + Tempo; `memory_limiter`),
  `prometheus.yaml` (collector + app `/metrics` jobs), `loki-config.yaml`, `tempo.yaml`. No named
  volumes on backends (reset-friendly). Telemetry env vars added to the `sharpai` service.
  **Owner/Notes:** _stack config complete; app-side emission lands in W17.T1–T4/T8._
- [x] **W17.T6 — Grafana provisioning + dashboards.** _(done 2026-08-14)_ Datasources with fixed UIDs
  `prometheus`/`loki`/`tempo` + trace↔log correlation; file dashboard provider; **Overview** dashboard
  (request rate/latency p50/p95/p99, error rate, tokens/sec, resident models, per-model inference
  latency, queue depth/active requests) and **Logs** dashboard (all + warning/error filter on
  `{service_name="sharpai"}`), `schemaVersion: 39`, `"id": null`. Plus a `docker/telemetry/README.md`.
  **Owner/Notes:** _Runtime/Process + Inference-detail dashboards can be added once metrics flow._
- [x] **W17.T7 — Factory defaults + reset (config hygiene).** _(done 2026-08-14)_ Telemetry configs are
  static checked-in files (not runtime state), so `reset.sh`/`reset.bat` need no change; verified the
  no-named-volume design means `docker compose down --volumes` clears backend state. Added
  `docker/logs/`, `docker/models/`, `docker/sharpai.db` to `.gitignore` and untracked the runtime DB
  (seed remains at `docker/factory/sharpai.db`). Remaining: the `Telemetry` block in the default
  `sharpai.json` lands with the W17.T4 settings code. **Owner/Notes:** _config-side complete._
- [~] **W17.T8 — Emit spans + metrics from handlers/services and validate end-to-end.** _(advanced 2026-08-21)_
  Core inference records latency/requests/tokens metrics; **all six inference handlers** (Ollama + OpenAI
  chat/completion/embeddings) now open a `SharpAI.Inference` **request span** (`inference.<op>` tagged with
  `sharpai.operation`/`sharpai.model`) via `SharpAITelemetry.StartInference`, wrapping the whole request. Span
  production is verified deterministically with an `ActivityListener` (`Telemetry/Span_Produced_WithListener`).
  Remaining: the live end-to-end check (pull a model, run inference, confirm request rate/latency/tokens in
  Grafana and traces in Tempo/logs in Loki) needs the running Docker stack — an environment-gated integration
  pass. **Surfaces:** server, telemetry, tests, docs, docker · **Owner/Notes:** _handler spans done + unit-verified;
  Grafana/Tempo confirmation is the docker-stack integration leg._

---

## §19 — Decisions

Resolved by the product owner; recorded here for traceability.

- [x] **D1 — Multi-tenancy & AAA.** Err toward Ollama: auth **built but disabled by default**; full
  AAA is opt-in for enterprise. (2026-08-13)
- [x] **D2 — Four DB providers.** All four required (`Sqlite`/`Mysql`/`Postgresql`/`SqlServer`);
  critical for enterprise; no SQLite-only shortcut. (2026-08-13)
- [x] **D3 — Vision/multimodal.** Remove llava entirely for now. (2026-08-13)
- [x] **D6 — Version.** Normalize the whole repo to a unified `5.0.0`. (2026-08-14)
- [x] **D7 — Telemetry stack.** Radiant + Watson 7.1 in-app; Prometheus + Loki + Grafana + Tempo in
  Docker, modeled on xeno (layout/factory) and less3 (Watson `/metrics` scrape). (2026-08-14)
- [x] **D8 — Testing.** Touchstone (`C:\code\touchstone`) with Test.Shared/Automated/Xunit/Nunit;
  target ~100% meaningful coverage; reliability ≥ Ollama. (2026-08-14)
- [ ] **D4 — Ollama-registry pulls (gates W5.T3).** In or out, given licensing/ToS? **Decision:** _—_
- [ ] **D5 — Positioning.** Confirm the headline is ".NET-native local inference (embed or serve),
  OpenAI- and Ollama-compatible, enterprise-observable" rather than a head-to-head "Ollama
  replacement," and align README + DOCKERHUB_README. **Decision:** _—_

---

## §20 — Definition of done (per shipped slice)

A task is not done until, as applicable to its surfaces:

- code compiles clean on `net8.0` and `net10.0` with the `CODE_STYLE` rules honored (no `var`, no
  tuples, usings inside namespace, XML docs on public members, `ConfigureAwait(false)`, one type per
  file, null-checked setters, clamped numerics);
- Touchstone suites cover the new behavior (including a failure/edge path) and run green in CI on
  `127.0.0.1`, keeping the coverage gate satisfied;
- telemetry is emitted for new server behavior and visible in Grafana where relevant;
- dashboard changes pass the mandatory responsive + light/dark + accessibility visual QA and the
  Docker dashboard container is rebuilt and restarted;
- user-facing strings are localized and the i18n key checks pass;
- affected SDKs and their harnesses are updated;
- README / DOCKERHUB_README / CHANGELOG / CLAUDE.md reflect the change and remain accurate;
- the relevant checkbox is flipped to `[x]` with an Owner/Notes entry.
