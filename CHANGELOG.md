# Changelog

## Current Version

v4.0.0

### Fixed

- **OpenAI streaming chat completions now use the standard response format** — The `/v1/chat/completions` streaming endpoint previously returned chunks using the text completion format (`choices[0].text`) instead of the chat completion format (`choices[0].delta.content`). This caused OpenAI-compatible clients (e.g. Mux) to silently drop streamed tokens. Streaming chunks now use `chat.completion.chunk` object type with `delta` payloads per the OpenAI specification.
- **Corrected Content-Type for streaming chat completions** — Changed from `application/x-ndjson` to `text/event-stream` (SSE) to match the OpenAI streaming protocol
- **Corrected object type for non-streaming chat completions** — Changed from `text_completion` to `chat.completion`
- **Dashboard streaming parser updated** to read from `delta.content` with fallback to `text` for backwards compatibility

### Added

- **Apple Metal GPU acceleration** for macOS Apple Silicon (M1/M2/M3/M4)
  - Auto-detected on Apple Silicon Macs when `libggml-metal.dylib` is present
  - Configurable via `ForceBackend: "metal"` or `SHARPAI_FORCE_BACKEND=metal`
  - New `MetalBackendPath` setting for custom Metal library location
  - VRAM reporting in `/api/ps` and dashboard now works for Metal backend
  - Only available for bare-metal macOS installs, not Docker containers
- **Pure-C# GGUF metadata reader** (`GgufMetadataReader`) for lightweight capability detection
  - Reads model architecture and capabilities from GGUF file header without loading weights
  - Falls back to full engine initialization only if header read fails
  - Enables successful model registration even when llama.cpp doesn't support the architecture
- **Thinking token filter** for models like Qwen3 that emit `<think>...</think>` blocks
  - Strips thinking tokens from responses by default (both streaming and non-streaming)
  - New `display_thinking` option to show thinking tokens when desired
  - "Display Thinking" toggle in dashboard chat settings
- **Format-aware default stop sequences** derived from the model's chat template
  - ChatML models get `<|im_end|>`, Llama3 gets `<|eot_id|>`, etc.
  - Prevents completions from running indefinitely when client doesn't send stop sequences
- New `PreLoadMacOSDependencies()` in `NativeLibraryBootstrapper` for reliable macOS library loading
- Metal GPU readiness reporting in `start-mac.sh` and `diagnose-mac.sh`
- "Metal (Apple GPU)" option in dashboard Configuration page Force Backend dropdown
- Metal Backend Path configuration field in dashboard
- METAL.md implementation plan for Apple Metal GPU support

### Changed

- **Upgraded LLamaSharp from v0.25.0 to v0.26.0** with newer llama.cpp and broader architecture support
- `ForceBackend` now accepts `"metal"` in addition to `"cpu"` and `"cuda"`
- `GetOptimalGpuLayers()` now returns all-layer offload for both CUDA and Metal backends
- Updated platform support table: macOS Apple Silicon now shows GPU ✅ (Metal)
- Model pull error handling: orphaned files cleaned up on failure, full exceptions logged
- General exception catch in pull handler prevents silent 500 errors
- Embedding-only architecture list consolidated into `GgufMetadataReader.EmbeddingOnlyArchitectures` (single source of truth)
- Updated `ChatFormatHelper` model family mappings for Qwen3/3.5, Llama 3.3/4, Gemma 3, DeepSeek2, Phi-4, SmolLM
- Dashboard logo left-justified in sidebar to match nav item alignment
- Replaced `react-toggle-dark-mode` with inline SVG toggle (eliminates react-spring dependency tree warnings)

### Removed

- Vision/multimodal code (`VisionDriver`, `LLavaWeights` usage) — removed due to LLamaSharp 0.26.0 API change (LLaVA replaced by MTMD)
- Noisy "no models currently loaded" debug log on every `/api/ps` poll

---

## v4.0.0

### Server

- **Migrated from SwiftStack 0.3.3 to Watson 7.0.11** as the underlying HTTP framework, with FastAPI-style typed route handlers (`server.Get/Post/Put/Delete`) and `ApiRequest` / `WebserverException` types replacing their SwiftStack equivalents
- **Built-in OpenAPI / Swagger documentation** for every REST endpoint
  - `GET /openapi.json` — full OpenAPI 3.0 document
  - `GET /swagger` — interactive Swagger UI
  - Every Ollama, OpenAI, general, and settings route is tagged with summary, description, request body schema, and response schemas
- **New settings API**
  - `GET /api/settings` — returns the current in-memory `Settings` object
  - `PUT /api/settings` — overwrites in-memory settings and rewrites `sharpai.json` on disk; `CreatedUtc` and `SoftwareVersion` are preserved server-side
- **New capability detection** from GGUF file metadata
  - `LlamaSharpEngine` now exposes a `general.architecture` property and authoritative `SupportsEmbeddings` / `SupportsGeneration` checks derived from `general.architecture` and `general.pooling_type` GGUF keys instead of assuming every model supports both
  - A known-architecture whitelist (`bert`, `nomic-bert`, `nomic-bert-moe`, `jina-bert-v2`, `jina-bert-v3`, `t5encoder`, `gte`, `bge`, `gritlm`) marks embedding-only models; everything else is completion-capable
  - `ModelFile.Embeddings` and `ModelFile.Completions` are now set at pull time from the GGUF metadata, and `ModelFile.Family` is populated from `general.architecture` so the correct chat template is used for chat completions
  - A background startup task (`RedetectModelCapabilitiesAsync`) re-inspects every existing model on server start, corrects stale capability flags and family values in the database, and logs a detailed per-model summary with counts of inspected / updated / unchanged / skipped / failed
- **Capabilities exposed in `/api/tags`**: `ModelFile.ToOllamaModelDetails()` now includes a `capabilities: { embeddings, completions }` object alongside the Ollama-compatible fields
- **Stop sequences honored** — the Ollama and OpenAI completion / chat completion handlers now pass `gcr.Options.Stop` (or the normalized OpenAI `Stop` object) through to `LlamaSharpEngine.InferenceParams.AntiPrompts`; previously they were hard-coded to `null` and ignored
- **Model pull progress** now includes both `downloaded`, `completed`, `total`, and `percent` fields so clients can compute a progress bar regardless of which field name they expect
- **CORS preflight handler** registered at `_Server.Routes.Preflight` that responds to every OPTIONS request with `Access-Control-Allow-Origin: *`, `Access-Control-Allow-Methods`, `Access-Control-Allow-Headers`, and a 24-hour `Access-Control-Max-Age` so dashboard POST/PUT/DELETE calls from a different origin succeed
- **PostRouting debug logging** logs `METHOD URL STATUS (Nms)` for every served request
- Server console banner and `OpenApi.Info.Version` now read from `_Version = "4.0.0"`

### Dashboard

- **Migrated from Next.js 14 to Vite 5 + React Router 6**
  - Removed `next`, `@ant-design/nextjs-registry`, `eslint-config-next`, and the `next/jest` preset
  - Added `vite`, `@vitejs/plugin-react`, `vite-tsconfig-paths`, `react-router-dom@6`, and plain `@typescript-eslint` + `eslint-plugin-react-*` for linting
  - Deleted `src/app/` (Next.js App Router) and replaced it with `src/main.tsx` + `src/App.tsx` declaring routes via `<BrowserRouter>` / `<Routes>` / `<Route>` with nested dashboard layout via `<Outlet />`
  - Root `index.html` now lives in the project root with explicit `<head>` tags and a `<script type="module" src="/src/main.tsx">` entry point
  - All `"use client"` directives stripped (23 files)
  - `next/link` → `react-router-dom` `Link` (with `to=` instead of `href=`)
  - `usePathname()` → `useLocation().pathname`
  - `useRouter().push()` → `useNavigate()`
  - `next/font/google` Inter import → Google Fonts `<link>` in `index.html`
  - `AntdRegistry` SSR flicker wrapper removed — pure CSR has no SSR to hydrate
  - Dockerfile updated to multi-stage `vite build` → `vite preview` runtime
  - Dev server cold start: ~500ms (previously several seconds in Next.js)
- **New Configuration page** at `/dashboard/configuration`
  - Structured form with collapsible sections for Logging, Storage, Database, HuggingFace, REST server, Runtime, Debug, and Quantization Priority
  - Dynamic syslog server list via `Form.List`
  - HuggingFace API key rendered as a password input
  - Database, REST, and SSL sections flagged with a "Changes require server restart" warning
  - Save / Reset buttons with change tracking
  - Force Backend select exposes Auto / CPU / CUDA options
- **Running Models section** on the Models page
  - New polling query (`useGetRunningModelsQuery`, 5-second interval) against `/api/ps`
  - Table shows Name, Family, Quantization, Size, and VRAM columns
  - VRAM shows `—` with a tooltip explaining "zero when running on CPU" for CPU-only installs
- **Model pull UX overhauled**
  - Pull lifecycle moved into a dedicated `PullProgressProvider` that owns its own abort controllers and lives inside the Redux store, so navigation is never blocked
  - Dedicated "Pulling Models" table below the main models list with Model, Status, Progress bar, Size (downloaded / total), and Cancel columns
  - Cancel button aborts the underlying axios request immediately
  - Progress bar reads the `downloaded` / `completed` / `total` / `percent` fields from each NDJSON chunk, so it animates correctly while the stream is in flight
  - On completion, the local models list auto-refreshes via RTK Query cache invalidation
  - Pull Model modal shows "Cancel Pull" + "Close" buttons while a pull is in flight, and the input autofocuses after the modal's open animation settles
- **Robust NDJSON streaming parser** (`parseNdJson`) replaces the previous `replaceAll("}{", "},{")` hack. Parses each line independently so a single bad chunk no longer poisons the entire buffer and truncates displayed output.
- **Model-type filtering** on every inference page
  - Embeddings page only shows embedding-capable models
  - Completions and Chat Completion pages only show completion-capable models
  - Driven by the server-provided `capabilities` object
  - Auto-selects the only available model when exactly one qualifies
- **Completion defaults** bumped: `num_predict: 150 → 1024`, `num_ctx: 1024 → 4096`, stop list expanded to include `[INST]`, `[/INST]`, `<|im_end|>`, `<|endoftext|>`, `<|eot_id|>`, `</s>`, `user:`, `User:`, `USER:`
- **Chat improvements**
  - Animated "typing" indicator with three pulsing dots and a "Thinking…" label while waiting for the first streamed token
  - Focus automatically returns to the input after generation completes
  - "AI can make mistakes. Fact check all answers." disclaimer below every chat input
  - Response Details modal is now 1050px wide (up from 600), taller, with the Status Information and Headers sections laid out as proper 2-column grids; Headers are shown in a fixed-width font
- **New theme** based on a mid-purple (`#9333ea`) drawn from the logo gradient (replacing the previous Ant Design blue). WCAG AA compliant on both light and dark backgrounds.
- **Dark mode** uses near-black backgrounds (`#050505` base, `#101010` cards) for reduced eye strain
- **Sidebar restructure**
  - Dark (`#050505`) sidebar with the purple-tinted selected-item highlight
  - Collapse control and version string pinned to the bottom via `margin-top: auto`
  - Version display shows the dashboard's own `package.json` version, with a tooltip that reveals both the dashboard and server versions
- **Header controls**
  - GitHub icon, light/dark toggle, and Logout link, in that order, on the right
  - All three now share a fixed-height flex row with `line-height: 1` so their visual midlines align against the "Logout" text regardless of whether they're an `<a>`, a bare SVG, or an icon+text link
- **Page titles + subtitles** on every navigable page
  - `PageContainer` gained a `pageSubtitle` prop rendered beneath the main title in secondary text
  - Every navbar destination (Models, Embeddings, Completions, Chat Completion, Configuration) now has a one-paragraph description explaining what the page is for
- **Tooltips on every column, label, value, and input**
  - New centralized `src/constants/tooltips.ts` containing ~90 explanations across all pages
  - New `TooltipHeader` component renders a column label + `?` icon with hover tooltip
  - Every `Form.Item` in Configuration, Embeddings, the Pull Model modal, the Landing page, and the ChatSettings sidebar has a `tooltip=` prop
  - Every table column across the dashboard (models, running models, pulling models) has a `TooltipHeader`
  - Every standalone button (Pull models, Available Models, Clear Chat, Settings toggle, Save, Reset, Cancel Pull, etc.) is wrapped in `SharpTooltip`
  - The VRAM `—` placeholder in the Running Models table has its own tooltip so hovering the blank value explains "zero when running on CPU"

### Build, packaging, dependencies

- Dashboard `package.json` bumped to `4.0.0`
- Server `_Version` bumped to `4.0.0`; `docker/sharpai.json` and both `bin/Debug/net*/sharpai.json` files updated
- `SharpAI.csproj` bumped to `1.0.15`
- Vite `css.preprocessorOptions.scss.api = "modern-compiler"` with `silenceDeprecations: ["legacy-js-api", "import", "global-builtin"]` — silences the Sass legacy-API deprecation warnings triggered by Ant Design's internal SCSS without touching Ant Design itself

## Previous Versions

### v1.0.0

- Initial release
- Core AI inference engine based on LlamaSharp
- Support for GGUF model format exclusively
- Model management with automatic download from HuggingFace
  - Automatic GGUF file discovery and selection
  - Intelligent quantization selection based on Ollama preferences
  - SQLite-based model registry with metadata tracking
  - Model file hashing (MD5, SHA1, SHA256)
- Embedding generation capabilities
  - Single text embedding generation
  - Batch embedding generation for multiple texts
  - Automatic dimension detection
- Text completion support
  - Non-streaming completions with customizable parameters
  - Streaming completions with async enumerable support
  - Temperature and max token controls
- Chat completion functionality
  - Non-streaming chat responses
  - Streaming chat responses
  - Support for conversation history in prompts
- Comprehensive prompt formatting system
  - 10 different chat formats (Simple, ChatML, Llama2, Llama3, Alpaca, Mistral, HumanAssistant, Zephyr, Phi, DeepSeek)
  - 10 text generation formats (Raw, Completion, Instruction, QuestionAnswer, CreativeWriting, CodeGeneration, Academic, ListGeneration, TemplateFilling, Dialogue)
  - Few-shot learning support with examples
  - Context-aware prompt building
- GPU acceleration support via LlamaSharp
  - Automatic CUDA detection and optimization
  - Support for NVIDIA (CUDA), AMD (ROCm/Vulkan), Apple Silicon (Metal), Intel (SYCL/Vulkan)
  - Automatic GPU layer allocation
- Platform support
  - Tested on Windows 11, macOS Sequoia, Ubuntu 24.04
  - Minimum .NET 8.0 requirement
- SharpAI.Server project included
  - Ollama-compatible REST API endpoints
  - OpenAI-compatible REST API endpoints
- Dependencies
  - LlamaSharp for model inference
  - SyslogLogging for flexible logging
  - Watson.ORM.Sqlite for model registry
  - SwiftStack for the application platform
  - RestWrapper for HuggingFace API integration
