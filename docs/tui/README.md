# Terminal validation evidence

The terminal implementation is available from source and through the local publisher. This directory
retains evidence from specific earlier runs; it is not a regenerated snapshot of the current UI.
[summary.json](summary.json) records an earlier 93/93 passing cases per framework, tied to source
commit `9c815098d89bd7eb1db0c8c3b63c883cc8bdfa9c` plus working-tree changes. Those counts are not the
current test inventory. Later chart evidence records 98 cases per framework. Frames under `frames/`
predate the Midnight Violet, padding, and chat-command updates and must not be used as current design
examples. Benchmark baselines and package hashes describe the original measured/built artifacts. See [the operator guide](../TUI.md) and [the remote walkthrough](../../examples/tui/README.md).

The evidence distinguishes public API/native inference checks from synthetic transport fixtures. The GGUF fixture is a real 19,077,344-byte model pinned to SHA-256 `66967fbece6dbe97886593fdbb73589584927e29119ec31f08090732d1861739`. Native Ollama and OpenAI streaming/nonstreaming tests run against an isolated SQLite server with two CPU threads and a 512-token context; no synthetic stream establishes model compatibility. The run also downloads that pinned file through the real server, generates through the original URL registered as its model name, and reopens the database to verify measured history survives restart.

`baselines/osx-arm64-net8.json` and its repeat include twelve samples with two warmups, 10,000-row table navigation, 2,001 HTTP stream frames, resize/render traffic, process startup, allocations and working set. Idle CPU runs in a separate warmed process to exclude GC/JIT from the preceding allocation tests. The gate compares medians at 2.5 times baseline with a 10 ms time allowance, and checks rendered bytes, working set and idle CPU (100 ms allowance over two seconds). Each CI operating system establishes and checks its own repeated baseline. A baseline from another runtime identifier is rejected.

In the recorded packaging run, six runtime archives cross-published locally. Both macOS binaries ran locally (x64 through the host's translation support), and Linux ARM64 and x64 ran in isolated containers. Linux deb/rpm install, upgrade from a disposable older-version package, replacement and removal and signed apt/rpm repository metadata are exercised locally, including an apt install from the signed local repository. The repository signatures use a disposable test key, never a release identity. A native macOS PTY run exits with its original terminal settings, and a mounted DMG supports installation, replacement and removal.

The checked CI workflow defines [GitHub-supported runner labels](https://docs.github.com/en/actions/reference/runners/github-hosted-runners) and adds native Windows x64/ARM64 runners, Inno and Chocolatey installation/replacement/removal, both macOS architectures and Linux architectures. Those remote jobs have not been run from this local checkout. Publishing the branch and running that workflow, reviewing the actual terminal experience on Windows/Linux hardware, reviewing translations, supplying a persistent repository signing identity, and submitting package-manager metadata remain release gates. Windows signing and Apple notarization are optional external identities; the artifacts disclose unsigned/ad-hoc status.

The strict parity gate intentionally remains closed: route coverage is not an assertion that every combination of Access mutations, principal capabilities and platform terminal behavior has been exercised through the UI. `parity.json` retains `implemented` rather than `complete` statuses. The 270-assertion administration API suite covers protected records, privilege escalation, deny rules, owner ceilings, tenant isolation and revocation; terminal operator tests separately exercise real forms and confirmations. This distinction prevents catalog/frame checks from being mistaken for complete workflow proof.

Reproduce with `python3 scripts/test-tui.py --model /path/to/weights.gguf --output artifacts/tui`. Add `--benchmark-baseline docs/tui/baselines/osx-arm64-net8.json` on a matching machine. Run `python3 scripts/test-tui-telemetry.py --api http://isolated-server:port --output artifacts/tui/telemetry` for an isolated real Collector/Prometheus/Loki/Tempo/Grafana check. All scripts clean up their servers and disposable state.


## Evidence inventory

| Files | Meaning and current use |
| --- | --- |
| `summary.json`, `net8.0-results.json`, `net10.0-results.json` | Earlier 93-case run; retain its original counts |
| `provenance.json`, `source-snapshot.json` | Source/config/model identity for that earlier run; old paths, including the root Postman collection, are historical identities |
| `frames/*.txt` | Older terminal cell captures, preceding the current theme/padding/chat-command work |
| `history-charts/*` | Later chart captures and 98-case validation with 13-row inline/8-row compact charts |
| `baselines/*.json` | Measured macOS ARM64 performance baseline/repeat; compare only matching runtime identifiers |
| `live-pull*.json`, `restart.json` | Recorded real pull/inference and durable-history checks |
| `packages.json` | Checksums for previously built archives/installers, not today's generated binaries |
| `telemetry.json` | Recorded isolated client metrics/logs/trace stack check |

Current server/collection validation is recorded separately in [API evidence](../api/validation.json).
It includes remote native embeddings and HTTP metrics, but does not claim current full terminal workflow
parity or SDK parity. The release parity gate remains closed until every manifest workflow has complete
executable/platform acceptance. See [SDK status](../SDK_STATUS.md) for consumer tooling gaps.

To obtain evidence for the current checkout, run the TUI harness into a new output directory and retain
its provenance and actual results. Do not overwrite historical checksums, source hashes, or baseline
captures to imply a new run. A source snapshot can mention a file path that has since moved; that is
expected historical metadata, not a broken active documentation link.
