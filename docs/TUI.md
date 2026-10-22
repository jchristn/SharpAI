# SharpAI terminal dashboard

Build and run the HTTP-only client against an existing server:

```sh
dotnet run --project src/SharpAI.Tui -f net8.0 -- --server http://127.0.0.1:8000
```

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
pull. Native inference and synthetic transport evidence are recorded separately.

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
