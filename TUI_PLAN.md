# SharpAI terminal dashboard implementation plan

Status: proposed implementation, not shipped. Baseline: the v4.0.1 web dashboard and its checked-in operation catalog. The executable will be `sharpai-tui`, written in C#, with a NuGet dependency on TUIKit. It will operate the existing server over HTTP, so running a terminal client does not load model weights, open the server database, or bypass authorization.

## Accepted execution scope and current evidence

The implementation follows the user's decisions: execute all work that can run without a human; target macOS, Windows and Linux; use memory sessions or existing protected assets under `~/.sharpai` without requiring a vault; omit unsupported Arabic/RTL; and carry backend contract fixes through web, SDKs, Postman, documentation and validation. Existing release versions are unchanged.

The executable implementation, public HTTP client, reusable Postman simulations, shared test descriptors, local publisher and CI matrix are present. The model-pull URL contract was corrected across server, web and all SDK documentation and tested with a real pinned GGUF download. See [the operator guide](docs/TUI.md), [validation evidence](docs/tui/README.md) and [the remote-profile example](examples/tui/README.md).

The original acceptance criteria below remain the release standard. Workflow manifest entries remain `implemented` pending the complete parity review; implementation and passing route tests do not automatically check off full workflow acceptance. Local evidence identifies the exercised native platforms, while Windows and remote runner jobs require execution after the branch is made available to CI. External publication, persistent signing identities, package-manager submissions and terminal/translation acceptance remain separate gates.

## Reference review and design decisions

The review covered the source implementations below, rather than screenshots alone. Paths are relative to the sibling repositories under `~/Code`. The patterns are useful for different parts of SharpAI; their product-specific background agents and approval policies do not become SharpAI features.

| Reference source | Observed pattern | SharpAI decision |
| --- | --- | --- |
| `Armada/src/Armada.Tui/ArmadaTuiApp.cs`, `Shell/`, `Screens/`, `Services/TuiApplicationDispatcher.cs`, `parity.json`; `Armada/src/Test.Shared/Infrastructure/TuiTestHost.cs` | Routed shell, injected client and terminal backend, preference/session bootstrap, explicit dashboard parity manifest, headless workflow tests | Primary architecture: route registry, typed screen controllers, one application dispatcher, testable terminal backend, checked parity manifest |
| `S3Drive/src/S3Drive.Tui/TuiController.cs`, `DriveListView.cs`, `HintBar.cs`, `DriveFormModal.cs` | Independently focused list/activity panes, clickable contextual shortcut hints, bounded activity tail, asynchronous operations | Focus-specific action hints; bounded background activity and model-download progress; selection remains attached to record ID |
| `Armor/src/Armor.Tui/Program.cs`, `TuiController.cs` | Menu/form workflows, startup logging without screen corruption, crash reporting after terminal restoration, lifecycle-owned telemetry | Initialize logging before the screen, disable console sink while active, cancel and dispose on quit, restore terminal before reporting failures |
| `Mux/src/Mux.Cli/App/MuxTuiApp.cs`, `TerminalClipboard.cs`; `Mux/src/Test.Shared/Suites/TuiShellSuite.cs` | Continuous transcript, composer dock, telemetry sidebar, injected backend, narrow-terminal collapse, explicit streaming and queued work state | Dock composer below a scrollable transcript; collapsible parameters and turn telemetry; preserve manual scroll position. SharpAI keeps one active generation and does not silently queue prompts |
| `Arguebot/src/ArgueBot.Tui/ConversationApp.cs`, `UiDispatcher.cs`, `WorkspaceViewport.cs`, `MessageHistoryWorkflow.cs`, `AttemptDetails.cs` | Server client separated from widgets, revision guards, retained drafts, history and attempt inspection, adaptive viewports | Cancel obsolete loads and reject stale revisions after tenant/model/session changes. Structured request and inference inspectors share server records |
| `TUIKit/src/TUIKit/Hosting/`, `Widgets/`, `Testing/`; `README.md` | Focus scopes, forms, tables, tabs, scroll/collapsible containers, Markdown/transcript, command registry, modal stack, headless backend | Use actual library widgets and the host interaction contract; no parallel custom input loop |

[TUIKit 1.12.0](https://www.nuget.org/packages/TUIKit/1.12.0) was verified on NuGet on 2026-10-21 and matches the local source package version. Pin that package in the first implementation change and restore from NuGet. Local source is an API reference, not a project reference or vendored fork. If implementation starts later, verify availability and review API changes before changing that pin.

## Product and transport boundaries

Create `src/SharpAI.Tui/SharpAI.Tui.csproj`, an executable targeting `net8.0;net10.0`, nullable enabled and implicit usings disabled. Reuse `sdk/csharp` where its methods support the current server contract. First reconcile the SDK against `dashboard/src/api/ApiClient.ts`, `dashboard/src/api/operations.json`, and the server route registrars. Add missing typed SDK calls there, rather than building a second ad hoc HTTP client inside widgets. The TUI must not reference `SharpAI.Server`, its database driver, or native inference libraries.

The normalized client must support cancellation, timeouts separated from long-running inference, JSON error envelopes, NDJSON pull/chat, OpenAI SSE, bounded response inspection, locale headers, and per-connection session state. Retry only idempotent reads with bounded backoff. Never automatically repeat a mutation, pull, credential issuance, or generation after an ambiguous network failure. Report the ambiguity and let the operator inspect history before retrying.

Proposed implementation layout:

```text
src/SharpAI.Tui/
  Program.cs
  SharpAITuiApp.cs
  Shell/                 # root layout, route/command registry, status and hints
  Screens/               # overview, models, playground, histories, explorer, observability, access, settings
  Services/              # UI dispatch, connection/session, locale, preferences, clipboard, external links
  Widgets/               # shared table controller, structured details, typed forms, progress and help
  Localization/          # shared catalog adapter, plural/interpolation and explicit-culture formatting
  parity.json            # every dashboard workflow -> screen, command and test ID
src/Test.Shared/Tui/     # Touchstone suites, fixtures, terminal recording and fake server transport
src/Test.Tui/            # isolated end-to-end terminal workflow runner
src/Test.Benchmark/     # black-box TUI startup/render/stream/large-table scenarios
```

Each screen controller receives a client, dispatcher, session scope, localizer, clock and cancellation token. Workers fetch off the UI thread; completed state is applied through TUIKit `Post(Action)`. Each route and tenant scope has a revision number, and continuations must compare it before updating widgets. Dispose the route token source and polling timers when leaving a screen. Keep all collections bounded and paginate at the server.

## Complete dashboard parity inventory

Every row below becomes entries in `parity.json`, including empty, loading, forbidden, validation and server-error variants. A row is complete only when the named workflows work through the public server API and have an executable test. Extra server operations reachable through API Explorer remain reachable in the terminal explorer even when they have no dedicated web page.

| Surface | Terminal workflows and data | Existing public contract | Acceptance evidence |
| --- | --- | --- | --- |
| Connection and shell | Server URL/profile, explicit HTTP/TLS behavior, health/readiness, current identity and admin badges, login/logout, default-tenant sign-in, language/theme, help, repository/docs/service links, version | `/health`, `/ready`, `POST /v1.0/token`, `GET /v1.0/token`, `DELETE /v1.0/token`, `/v1.0/auth/context` | Resume/expired sessions, auth-off mode, unreachable server, no token leakage, keyboard-only navigation |
| Overview | All web KPI values and four quick actions: models, playground, observability, explorer; manual refresh | Same dashboard summary/model/health calls | Values and actions agree with web fixtures; no fabricated activity |
| Models | Registered and resident model tables with names, sizes, modified/expiry times; structured pull progress from repository name or full URL; delete-local-model confirmation; unload-resident-model confirmation; refresh both lists | `/api/tags`, `/api/ps`, `/api/pull`, `/api/delete`, `/api/unload`; explorer also exposes show/import/inspect | Real NDJSON progress, malformed URL, failed/aborted download, unreadable model, protected data and failure paths |
| Playground chat | Actual model picker; conversation, Markdown and code; system prompt; temperature, maximum output tokens, top-p, top-k, min-p, repeat penalty/window, presence/frequency penalties, seed; per-line stop strings; JSON output; thinking visibility; streaming toggle; one-line initial composer; cancel; new conversation; copy text/code; warning and rotating wait messages | `/api/tags`, `/api/chat` streaming and non-streaming | Multi-turn request payloads, whitespace preservation, split UTF-8 frames, errors after headers, cancellation, manual transcript scroll, all parameter bounds |
| Playground telemetry | Per-turn TTFT, total/stream time, input/output/total tokens, generation speed; expand details only when response data exists; unavailable values distinct from zero | Final inference envelope; persisted history detail for additional engine phases | Same units as web; no invented cache usage; stopped and failed responses visibly distinguished |
| Playground embeddings | Model/input selection, cancellation, dimension count and readable vector preview, copy full result | `/api/embed` | Single/batched/unsupported model, no fabricated vector values |
| Request History | UTC timestamp first, method/path/status/duration/source; filter method/status/path; presets hour/day/week/month with exact web bucket widths; chart-to-table interval drilldown; pagination/page size; refresh preserves filters; row activation opens wide structured inspector | `/v1.0/api/request-history`, `/summary`, `/{id}` | Encoded query parity, newest-first ordering, details fetched separately, timestamp and filter consistency |
| Request inspector | Request ID, complete URL, principal/tenant/user, start/end/status/duration, trace/span IDs; request and response headers/body, original bytes, truncation and unavailable flags; redacted fields stay redacted; clipboard | `GET /v1.0/api/request-history/{id}` | Large text/JSON/NDJSON/SSE, missing body, old records, 404/403; no whole-record JSON-only presentation |
| Inference History | All request-history controls with inference category and endpoint selector, duration meters and usage/phase breakdown; nullable cached count, queue/load/prompt/generation/engine/TTFT/stream phases, cancellation and approximation flags | Same history endpoints, `category=inference` | Persisted fields match engine measurements and survive server restart; unavailable data explained |
| API Explorer | Operation picker from checked catalog above request editor; method/path placeholders/query/body; all catalog operations, validation, destructive confirmations, execute, response status/body and streaming inspector, exact command/response copy | `operations.json`, `/openapi.json`, selected API route | Catalog/route coverage gate; valid JSON, path encoding, authorization, 4xx/5xx, NDJSON/SSE, cancel long response |
| Observability | Health/ready plus current web request/token-rate/resident/latency summaries; detect missing metrics; configurable Grafana SharpAI-folder, Prometheus, Loki and Tempo links; exact URL copy | `/health`, `/ready`, `/metrics`; configured engine metrics endpoint such as dashboard `/engine-metrics` | Refresh/delta rates and missing/stale series; external service failure is separate from SharpAI health |
| Access: tenants | Scope selector, enumerate all accessible tenants, create/edit/activate/deactivate/delete, protected restrictions | `/v1.0/tenants`, `/{tenant}` | Platform-only mutation enforced server-side; isolated test tenants |
| Access: users | Paged users; separate ID/name/email/type/status/date; create/edit; platform/tenant admin checks; reset password with confirmation; delete; effective permission inspection | Tenant user routes, password and permission inspection operations in catalog | Cannot elevate a tenant administrator or mutate protected global admins; own password reset invalidates session |
| Access: credentials | Paged separate IDs, name, owner, public key, expiry/status; create/edit/revoke; one-time secret copy; effective rights and owner ceiling | Tenant credential routes and effective-permission route | Secret only in creation modal; never preferences/logs/history; dismiss clears memory |
| Access: roles | Built-ins/custom rows; create/rename/delete custom roles; permission mappings add/remove; inspect inactive and built-in mappings | Tenant roles and role-permission mapping routes | Built-in/protected mutations disabled; cross-tenant mappings rejected |
| Access: permissions | Typed permit/deny forms; operations/resources; create/edit where backend permits/delete custom rules | Tenant permission routes | Shorthand Read/Write/All semantics, deny precedence, invalid operations |
| Access: assignments | User grants and credential scope tabs; principal picker; role; tenant/resource scope, exact resource ID and inheritance; create/remove; inspect effective permission rules | User-role and credential-scope assignment routes | IDs required for resource scope; tenant boundaries and credential owner ceiling |
| Access: sessions | Selected tenant sessions, separate session/user IDs, creation/expiry, revoke confirmation | Tenant session routes | Revoked and inactive-owner sessions rejected on next API call |
| Access: audit | Paged security events with event/principal/path/outcome/time and detail inspection | `/v1.0/api/audit` | Scope/permission changes never reveal another tenant’s events |
| Settings / server info | Single scrollable typed form, all returned sections including HuggingFace, Auth, Rest, Runtime, Database, Storage, Telemetry, RequestHistory, Logging, Debug; nested objects, arrays, header/priority dictionaries; add/remove row icons; password masks; read-only managed values; dirty/reload/save and restart notes | `/api/settings` GET/PUT, `/health` | Unknown fields preserved, secret redaction policy respected, type-correct round trip, null overrides, failed save retains draft |
| Cross-cutting table/form behavior | Distinct nonwrapping copyable IDs; filter drafts; paging, loading/error/empty; localized useful help for labels/fields/headers/actions; column visibility, pinned essentials, persisted per-table preferences | Existing list filter capabilities | No hidden records are claimed to be filtered; unsupported server sort/filter labeled local-to-page |

The terminal observability client must not assume that `/metrics` contains engine metrics: it currently exposes Watson HTTP measurements, while the web proxy obtains engine metrics from the collector. Store the engine endpoint in the connection profile, and implement separate parsers and unavailable states. The same distinction applies to links: remote profiles derive hostnames from the selected server or explicit profile values, never the operator's own localhost.

## Interaction and terminal layout

Use a compact shell: one header row for server, health, identity and selected tenant; a navigation region; the active screen; and a context-aware hint row. Start at 120x40 with an 18-column navigation rail. At 100 columns collapse it to an overlay menu; at 80x24 show one primary pane at a time. Below the declared minimum, show a useful resize message while preserving state. Tables scroll horizontally inside their pane; identifiers never wrap.

Use TUIKit `DataTable<T>`/`DataTableLoader<T>` for paged results, `Form` and masked `TextField` for edits, `TabStrip` for Access, `CollapsibleSection` for parameters, `MarkdownView` or `StreamingTranscript` for conversation, `ProgressBar` for pulls, `DefinitionList` for metadata, and `ActivityTimeline`/bar charts for phase measurements. Verify concrete constructors against the pinned package when coding. Full-size parameter expansion participates in page scrolling rather than creating a second tiny scroll area.

Bind F1 to contextual help, Ctrl+P to the command palette, Tab/Shift+Tab to focus traversal, Enter to the selected action/row, Esc to close the top modal or cancel a draft, and Ctrl+Q to quit with a dirty/in-flight confirmation. Use Ctrl+J for a composer newline in terminals that cannot distinguish Shift+Enter. Single-letter shortcuts are inactive while editing. Every pointer action must have a keyboard path; focused controls show the same operational help as hover tooltips. Use a single command registry to generate hints, menus, help and command search. No shortcut may silently broaden tenant scope.

Render model text and server errors as untrusted text. Strip terminal escape/control sequences and disallow unsafe automatic URL schemes. OSC 8 links are allowlisted. Clipboard uses local platform integration or explicit OSC 52 over SSH, with a visible success/failure indicator and a selectable-text fallback. Opening an external browser is optional; copying a URL works in headless and SSH sessions.

## Authentication, configuration and privacy

Connection profiles contain server URL, non-secret preferences and selected locale/theme. Session tokens belong in an OS credential vault: Keychain on macOS, Credential Manager on Windows, Secret Service on Linux. If the vault is unavailable, use memory-only sessions unless the user explicitly opts into a documented secure fallback. Never put passwords, bearer tokens or Hugging Face keys in arguments, profile JSON, crash reports, telemetry attributes or clipboard history automatically.

Revalidate `/v1.0/auth/context` after login, reconnect and permission failures. Tenant administrators are bound to their authenticated tenant; platform administrators explicitly select a target. Backend decisions remain authoritative. Clear scoped caches, pending detail requests, form drafts and one-time secrets on principal/tenant changes. Record mutations through existing server APIs so audit behavior is identical to the web UI. An auth-disabled server remains visibly labeled and does not prompt for a fabricated login.

## Requirements traceability

The normative source is `~/Code/Agents/requirements`. Compliance is a release gate, not a claim that a written plan has already implemented the requirements.

| Requirement document | Concrete implementation obligation and gate |
| --- | --- |
| `CODE_STYLE.md` | Namespace first, system usings inside namespace, explicit types, `_PascalCase` private fields, one class/enum per file, nullable and guards, XML docs on public APIs, cancellation on async methods, disposal, no console writes from libraries, no em dashes. Build both TFMs with warnings treated as errors |
| `AUTHENTICATION.md` | Reuse tenant-bound identities, session/credential schemes, immutable built-ins, deny precedence, owner ceiling, exact resource scope and audited admin bypasses. Negative cross-tenant and privilege-escalation tests are mandatory |
| `BACKEND_ARCHITECTURE.md` | Keep UI, SDK transport and server/domain separate; no database/native-engine access from TUI. Reuse public normalized clients and provider-neutral contracts |
| `FRONTEND_ARCHITECTURE.md`, `DASHBOARD_STYLE_AND_USABILITY.md` | Maintain route inventory, normalized resource access, compact typed forms, IDs in separate columns, table preferences, coherent shell, useful help, structured history, all workflow states and keyboard usability. Adapt screen widths to terminal cell geometry |
| `I18N.md` | Share stable web catalog keys via embedded/generated resources and a real localization adapter supporting interpolation/plurals. Implement en/es/pt/fr/it/de/zh/ar/ru/ms/hi/ja plus expansion and RTL pseudo-locales. Explicit `CultureInfo` formatting, autonyms, pre-auth selector, locale headers and persistence. Preserve machine enum/path values; test CJK width/combining marks/RTL |
| `TELEMETRY_REQUIREMENTS.md` | One lifecycle-owned Radiant host when enabled, ActivitySource/Meter for client requests, screens and failures; W3C propagation; correlate request/history/trace IDs. No secrets/prompts/high-cardinality metric labels. Emit JSON logs to files/OTLP without corrupting the screen. Verify Prometheus/Loki/Tempo/Grafana delivery in the isolated stack |
| `BACKEND_TEST_ARCHITECTURE.md` | Shared Touchstone descriptors in Test.Shared, exposed through automated CLI and xUnit/NUnit adapters. Headless terminal tests reuse the same cases; do not duplicate assertions in separate runners |
| `SIMULATED_USER_TESTING.md` | Complete operator tasks against an isolated running server, record keyboard/pointer steps, observable completion, cleanup, screenshots/frame captures and failures. Test synthetic transport failures separately from live model inference |
| `BENCHMARKING.md` | Black-box startup, table paging/scroll, stream throughput, resize and idle CPU measurements. Seed inputs, isolate ports/data, capture commit/config/package/OS/model provenance, report incomplete runs as failures, commit baselines and compare gates |
| `INSTALLERS.md` | Add publisher manifest and `SharpAI.Publisher`, CI runtime matrix, self-contained downloads/installers plus Windows winget/choco, macOS brew and Linux apt/rpm channels as applicable. Include checksums, unsigned notice, free macOS ad-hoc signing and signed Linux repository metadata. Installer smoke tests precede publication |
| `REPOSITORY_REQUIREMENTS.md` | Update README, CHANGELOG, deployment and Docker Hub docs, build-all forwarding, CI checks and examples. The terminal client has no new public HTTP listener; if a future service is introduced, apply the required healthchecks and dependency order |
| `EXAMPLE_APPLICATIONS.md` | Add a runnable remote-profile walkthrough covering download, chat, inspect history and a scoped read-only account; keep it isolated and document cleanup |
| `VERSIONING.md` | Keep existing release versions until explicitly authorized. Package/app version and image tag are separate; never bump as a side effect of adding TUI |
| `WRITING_DOCUMENTS.md` | Review plan, help, release notes and guides for plain operator language and remove em dashes |

Terminal RTL support requires an explicit test spike: TUIKit handles cell widths, but that alone does not prove correct bidi shaping. Verify Arabic mixed with IDs, dates and paths on Windows Terminal, macOS Terminal and a Linux terminal. If host shaping is inadequate, implement tested bidi presentation at the rendering boundary while preserving logical text for editing/copy; do not declare Arabic supported from translated strings alone.

## Sequenced implementation backlog

Each task below is small enough to become an issue or reviewable change. Dependencies are explicit. No wave is accepted with placeholder buttons, fake data or untested parity claims.

| ID | Dependency | Deliverable | Completion gate |
| --- | --- | --- | --- |
| T01 | None | Snapshot web route/operation/form inventory into `parity.json`, with workflow IDs and fixtures | Every inventory row above maps to commands and planned tests; drift check rejects new unmapped web routes |
| T02 | T01 | Reconcile C# SDK, add typed history telemetry DTOs, current admin/settings calls, NDJSON/SSE readers, cancellation and structured errors | SDK contract tests including split frames, malformed JSON, 401/403/429/5xx, cancellation and no automatic mutation retry |
| T03 | T01 | Scaffold executable/project, pinned NuGet, injected terminal, screen/command registries, dispatcher, disposal | Both framework builds warning-free; headless smoke and terminal restore after injected failure |
| T04 | T02,T03 | Connection profiles, vault sessions, auth-off/login/resume/logout, scope/permission context | Cross-tenant/session-switch races and expired/revoked sessions pass; no plaintext secret persistence |
| T05 | T03 | Shared localized forms/tables/details/help, clipboard/external links, theme and responsive shell | Every control reachable by keyboard; 80x24/100x30/120x40/160x50 frames, Unicode/ASCII, long-label and Arabic spike pass |
| T06 | T02,T04,T05 | Overview and Models, pull progress/cancel/error handling, delete/unload confirmations | Isolated server workflows and a real downloaded-model smoke; selection preserved across refresh |
| T07 | T06 | Playground chat/embeddings, all parameters, streaming on/off, cancel, copy, warning/quips, per-turn telemetry | Native model live tests plus deterministic transport tests; no transcript jump while scrolled; no stale turn after model/session switch |
| T08 | T02,T05 | Request and inference history with exact presets, filters, drilldown, paging, timestamp/meter columns and full structured details | Metadata-only list plus full-detail fetch verified; persisted timings and bodies survive restart and remain redacted |
| T09 | T02,T05 | Operation-catalog API Explorer including all management/inference operations, streaming and destructive confirmation | Catalog coverage, parameter encoding and typed errors pass; no silent JSON-only stream buffering |
| T10 | T04,T05 | All eight Access sections and every mutation/inspection workflow | Full admin/tenant-admin/read-only/credential-owner matrix, protected role restrictions, one-time secret and password/session revocation pass |
| T11 | T02,T05 | Single scrollable complete Settings form and server info; Observability signals and service links | Unknown settings preserved, Hugging Face editable, dirty/error/restart flows; configured remote endpoints and engine-metrics absence tested |
| T12 | T06-T11 | Full parity audit, isolated user simulations, live end-to-end run, benchmark fixtures and baseline gates | Every `parity.json` entry names a passing executable test; evidence bundle records commit/config/framework/model and cleanup |
| T13 | T12 | Publisher/runtime/package-manager artifacts, docs, examples and CI release jobs | Clean-install/upgrade/uninstall tests on supported OS/architectures, checksums/notices, terminal restore and preferences/session policy verified |

The first usable increment ends at T07, but it is not dashboard parity and must not be labeled complete. Full functionality requires T08 through T13, including operational packaging and the negative security cases. If a current server contract cannot support a requested presentation, add the smallest authorized backend capability and its tests before checking off that workflow.

## Validation and release evidence

Use an isolated SQLite server for workflow tests, unique ports and disposable tenants/models. Add provider migration/contract coverage for PostgreSQL, MySQL and SQL Server when persisted telemetry fields change. Exercise one real model through Ollama chat and OpenAI chat, streaming and non-streaming, plus unsupported embeddings and model-not-found responses. Transport fixtures are useful for timing and failure determinism but are not evidence of native model compatibility.

Headless tests record input sequences and rendered cells, not just controller state. Assert focus, scroll anchoring, tooltip/help text, copy success/failure, form validation, modal dismissal, cancellation and resize during work. Run every route with populated, empty, loading, forbidden and failed data, all locales, and representative narrow/wide terminal sizes. Store fixtures and logs with secrets removed. Benchmark comparison must include wall time, allocation/working set, idle CPU and rendered bytes/frames with tolerances based on repeated baseline runs.

CI must build both TFMs, run shared suites through the existing test adapters, check locale and parity drift, launch isolated server/TUI simulations, compare benchmarks, and smoke packaged binaries on the runtime matrix. The release evidence names the exact commits, TUIKit/SDK/runtime versions, OS/architecture, terminal mode, model/config hashes, commands, results and any unsupported capability. A checked-off plan item without that evidence reopens automatically during the parity audit.

## Scoped libraries added to web parity

The web dashboard now manages System prompts (`/system-prompts`) and Model configurations
(`/model-configurations`). TUI implementation must include list/pagination, structured inspection,
create/edit/delete and Duplicate for both resources. Add their entry points to the home screen.

Use `/v1.0/library-context`, `/v1.0/system-prompts` and `/v1.0/model-configurations`. Enforce the same
System/Tenant/User publication choices in the interface; the server remains authoritative. Private
items are visible only to their owner, including when the caller is an administrator. Duplicate defaults
to the current user's private scope, and offers authorized shared scopes for administrators. A visibility
change requires duplication, not an in-place owner change.

Replace T07's inline prompt/parameter ownership with the two saved-library selectors and adjacent edit
actions. Read-only shared selections open a copy editor. The model configuration editor contains all
sampling fields, stop sequences, streaming, JSON format and thinking visibility, but no system prompt.
Test that selection and editing preserve all fields, private records disappear on identity changes,
and duplicated items leave the source unchanged. See [the contract](docs/SCOPED_LIBRARIES.md).
