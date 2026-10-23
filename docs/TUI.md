# SharpAI terminal dashboard

Build and run the HTTP-only client against an existing server:

```sh
dotnet run --project src/SharpAI.Tui -f net8.0 -- --server http://127.0.0.1:8000
```

Run from the repository root with the matching .NET SDK installed. For an already built
self-contained archive, invoke its `sharpai-tui` executable instead; no installed .NET runtime is needed.
The server must already be running; see [Docker/web setup](../README.md#getting-started-docker-and-web).

| Flag | Meaning |
| --- | --- |
| `--server http(s)://host:port` | Server origin; overrides the saved profile for this run |
| `--locale en` | Startup locale; overrides the saved profile for this run |
| `--profile-directory path` | Directory containing `tui.json` and the client log directory |
| `--help`, `--version` | Print usage/version without starting the interactive terminal |

There are no password/token command-line arguments. Supported production locales are en, es, pt, fr,
it, de, zh, ru, ms, hi, and ja; `qps-ploc` is an expansion-test locale.

The client targets .NET 8 and .NET 10 and pins TUIKit 1.12.0 from NuGet. It has no reference to the
server, database drivers, or native inference engine. Model weights and database files stay on the
selected server. An interactive terminal is required; `--help` and `--version` also work when redirected.

Press F2 for navigation, Ctrl+P for commands, and F1 for operational help. Tab and Shift+Tab traverse
controls. Enter activates rows and sends a composed chat; Ctrl+J adds a composer newline. Ctrl+X cancels
active work. Ctrl+Q confirms before discarding a form or cancelling a generation. F5 refreshes.
Ctrl+Y copies the selected result using the local clipboard; if unavailable, the client opens selectable
text. Credential tokens use a masked modal with an explicit Copy action and Ctrl+R reveal.

Use Commands > Sign in for an interactive session, or Use bearer token for an API credential. The
current server requires authentication. Initial isolated installations seed `admin@sharpai.local`
with password `password` in tenant `default`; existing accounts retain their passwords. The server's
bootstrap API credential has limited model/inference rights and cannot administer settings or accounts.
Sessions can be invalidated by a server restart and must be validated again.

Preferences live in `~/.sharpai/tui.json`. Passwords and entered bearer values stay in memory. A profile
can reference an existing `TokenFile`; the client reads it without copying its contents into preferences.
On Unix that file must be owner-only. The client does not create token files or use a credential vault.
Choose a language before signing in through Commands > Language, or pass `--locale`. Profile, language,
and theme changes apply after restarting the client. Arabic is omitted because bidi shaping has not
been established for the required terminal hosts. Shared catalog translations fall back to English.

Navigation follows the web groups Overview, Models, Operations, and System, with section spacing
after Overview, Playground, and Observability. Access and history tabs appear as individual terminal
navigation items.

The shell collapses navigation below 100 columns and preserves state when the window is smaller than
80x24. Identifiers have separate nonwrapping table columns. Use Commands > Choose table columns to
save a per-screen selection. Tables paginate through the public API; the client does not claim to
filter or sort records that the server has not returned.

Playground uses saved System Prompt and Model Configuration libraries. Read-only shared items open
an independent duplicate editor. A visibility change requires Duplicate. User items remain private even
from administrators. Model configuration forms include sampling fields, per-row stop strings, streaming,
JSON output and thinking visibility; system prompts remain separate. See [scoped libraries](SCOPED_LIBRARIES.md).

Model pulls accept repository IDs and validated Hugging Face URLs. See [pull sources](MODEL_PULL_SOURCES.md).
Inspect request and inference history using UTC-first tables, server filters, time presets and interval
drilldown. Bodies and measured phases are fetched separately on row activation. Unavailable measurements
are not reported as zero.

Configure `EngineMetrics` separately from `/metrics`: the latter reports Watson HTTP measurements.
Engine summaries require the collector's engine series. Remote service links derive their host from
the selected server unless overridden through `ServiceLinks`. Copying links works without a browser.
Telemetry is opt-in through `TelemetryEnabled` and `OtlpEndpoint`; the client starts one Radiant host and
writes JSON lifecycle logs under `~/.sharpai/logs`. Logs exclude credentials, prompts, bodies, URLs and
principal IDs. Prometheus export does not add a listener to the terminal client.

## Validation

```sh
python3 scripts/test-tui.py --model /absolute/path/to/model.gguf --output artifacts/tui
python3 scripts/check-tui.py
```

The harness creates disposable ports, a SQLite server and a Postman simulation, runs both frameworks,
records terminal cells, checks native Ollama/OpenAI inference and persisted history after restart, runs
benchmarks, and cleans up services and data. `--pull-url` additionally exercises a real verified remote
pull. Native inference and synthetic transport evidence are recorded separately. The checked files under
`tui/` are historical run artifacts; [their inventory](tui/README.md) describes their age and scope.
Current server/collection evidence is in [api/validation.json](api/validation.json).

The parity manifest records every web route, API operation and planned workflow. Catalog/focus coverage
is not proof of complete workflow parity. `scripts/check-tui.py --release --results <results.json>` fails
unless every workflow is explicitly complete and its executable evidence passes. See
[TUI_PLAN.md](../TUI_PLAN.md) and the evidence in [tui](tui/) for remaining release gates.

## Packaging

```sh
./build-tui.sh --rid osx-arm64
# Windows: build-tui.bat --rid win-x64
```

`publisher.json` owns the six-RID matrix. `SharpAI.Publisher` generates self-contained archives, checksums,
and channel artifacts under ignored `installers/`. Windows binaries are unsigned; macOS uses free
ad-hoc signing without notarization. Uninstall removes only the installed executable; preferences and
existing token files remain under the operator's control. Package-manager metadata uses real archive
checksums. Chocolatey packages embed the matching runtime payload for offline installation.

Build a native channel after its archive, for example:

```sh
dotnet run --project src/SharpAI.Publisher -- --channel dmg --rid osx-arm64
dotnet run --project src/SharpAI.Publisher -- --channel deb --rid linux-x64
python3 scripts/build-tui-repository.py --packages installers --output installers/repositories --key-id YOUR_EXISTING_GPG_FINGERPRINT
```

Native channel tools (`hdiutil`, `iscc`, `choco`, `fpm`) must be installed on the build host. Repository
metadata additionally uses `apt-ftparchive`, `createrepo_c` and GPG. The repository helper signs with an
existing identity and performs no upload. CI uses an explicitly disposable key for signature tests.

Publication, persistent repository signing keys and external moderation are separate from
local builds. The CI workflow builds and tests before producing package artifacts; it does not publish
a release or push a Postman workspace.

## History charts

API Request History, Inference History and Embeddings History each show a stacked activity chart with
successful and failed request counts, UTC time/count axes, total requests, success rate and average
duration. Inference History uses the same generation-only filter as the web dashboard; embeddings have
their own view. Hour/day/week/month presets retain the shared 1/15/120/1440-minute server buckets.

Tab to the chart and use Left/Right (or Home/End) to inspect a bar. Press Enter or click a bar to filter
the request table to its UTC interval; **Clear range** returns to the full preset window. Hovering shows
the interval and exact counts. **Activity chart** opens a larger view. On narrow terminals, neighboring
buckets merge into wider intervals so counts remain visible without dropping requests; drilldown uses
the entire displayed interval. Success/failure have separate glyphs as well as colors, with S/F in ASCII
mode. Empty, loading and unavailable summaries are distinct and never reuse stale chart bars.

### Chat completion visibility

The conversation follows incoming text while positioned at the bottom. Scrolling up pauses
following; return to the end to resume. Final-frame content is retained even when the HTTP
stream ends without a trailing newline. A stream without a completion marker is reported
as failed while retaining its partial text. `Token limit reached` means the server ended
generation at its token budget; increase `MaxTokens` in the selected model configuration
for longer answers. The web Playground also reports this limit and follows layout changes
when its Markdown renderer loads.

Playground automatically chooses the first model advertising completion support, falling
back to the first listed model when no completion-capable model is identified. You can
select another model at any time. The web Playground uses the same selection rule.
The Balanced starter now reserves 2,048 tokens, including hidden thinking; server upgrades
update only the untouched original starter. Restart the server to apply that upgrade.

### Midnight Violet

The default `dark` theme uses the logo-derived Midnight Violet palette: ink `#11101A`,
panel `#191624`, text `#F4F0FA`, muted `#B3A6C4`, border `#65557E`, lavender focus
`#BDA9F3`, selected surface `#35254F`, and pink errors `#EF85B9`. Success (`#8ACDB3`)
and warning (`#E4C18A`) are reserved for semantic states. History charts use lavender
success bars and pink failure bars, with separate glyphs and labels. Navigation, forms,
Markdown, focused borders, dialogs, notifications, and keyboard hints share these roles.
ASCII mode preserves the palette; explicit light and high-contrast profiles remain available.

### Chat commands and rendering

Both Playground interfaces recognize the same composer commands. Type `/?`, `/help`,
`/commands`, or `/` for help. Commands are handled by the client; use `//` to send a
literal leading slash to the model.

| Command | Action |
| --- | --- |
| `/clear` (`/new`, `/reset`) | Clear the visible conversation, summary, retained model context, and turn details. |
| `/compact` | Ask the selected model to summarize earlier turns; keep the latest exchange when there is more than one. |
| `/model [name]` (`/models`) | List available models, or select a named model while retaining the conversation. |
| `/status` | Show the selected model, output budget, and retained context state. |
| `/stop` (`/cancel`) | Cancel generation or compaction. |

Compaction uses a nonstreaming `/api/chat` request with a summary instruction and a
1,024-token output budget. It preserves the visible transcript. Only a nonempty, completed,
uncancelled summary that did not hit its token limit replaces earlier model context.
Failures and cancellation leave the original context intact. Repeated compaction includes
the previous summary. The selected system prompt still accompanies subsequent chat requests.
Compaction is a model inference and appears in server request/inference history. Use `/stop`
before starting another command while generation is active.

TUI chat output is rendered by TUIKit `MarkdownView`, including headings, emphasis, links,
lists, tables, and fenced code. Framed content has at least one blank column on each side;
scrollbars, navigation drawers, and credential dialogs keep the same horizontal separation.


## Related contracts and client coverage

Use the [REST reference](../REST_API.md) for all server operations and
[Postman resources](../postman/README.md) for the collection and transport simulations. The TUI uses
`SharpAI.Client`; its transport capabilities do not establish full parity for the public C#, JavaScript,
or Python SDKs. See [SDK status](SDK_STATUS.md).
