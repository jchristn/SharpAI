# SharpAI documentation

These guides describe the current source checkout. Server and dashboard Docker assets use `v4.0.1`;
SDK/package versions have their own release history. Instructions that exercise a server require bearer
authentication except for health/readiness and login. Start with the [project README](../README.md)
for Docker, web, and terminal setup.

| Guide | Purpose |
| --- | --- |
| [REST API](../REST_API.md) | All registered operations, fields, permissions, examples, streaming, and compatibility limits |
| [Deployment](../DEPLOYMENT_GUIDE.md) | Native/Docker installation, configuration, persistence, hardware, and credentials |
| [SDK status](SDK_STATUS.md) | Actual C#, JavaScript/TypeScript, and Python coverage, tests, console tools, and known gaps |
| [Terminal dashboard](TUI.md) | Connection profiles, keyboard controls, chat commands, charts, themes, and packaging |
| [Dashboard administration](DASHBOARD_ADMINISTRATION.md) | Tenants, users, credentials, roles, permissions, assignments, sessions, and audit |
| [Scoped libraries](SCOPED_LIBRARIES.md) | Shared and personal system prompts and model configurations |
| [Model pull sources](MODEL_PULL_SOURCES.md) | Hugging Face source formats, immutable revisions, verification, and progress |
| [Inference operations](OPERATIONS.md) | Usage, context/memory admission, diagnostics, benchmark receipts, and verification |
| [History telemetry](HISTORY_TELEMETRY.md) | Request, generation, and embedding history, timing semantics, redaction, and retention |
| [Observability](OBSERVABILITY.md) | Metrics, traces, logs, dashboards, authentication, and collector configuration |
| [JSON logging](LOGGING.md) | Structured event bodies, trace correlation, and long-record reconstruction |
| [Benchmarks](BENCHMARKS.md) | Reproducible throughput/latency measurements and limitations of historical figures |
| [Postman resources](../postman/README.md) | The consolidated collection, simulations, mocks, and reproducible live validation |
| [Terminal evidence](tui/README.md) | Recorded terminal runs, baseline/package artifacts, and outstanding acceptance gates |

## Contracts and recorded evidence

`api/openapi.json` is the checked server-generated contract snapshot. The root REST reference and
Postman collection are generated together from that inventory, reviewed operation semantics, and DTO
sources. `api/reference-introduction.md` is a generator fragment whose links resolve from the generated
root `REST_API.md`; it is not a separate operator guide.

[API validation](api/validation.json) records the .NET 8/10 native REST/Postman runs, including actual
remote model pulling, embeddings, streaming, HTTP metrics, settings replacement, and favicon bytes.
[Terminal evidence](tui/README.md) explains the older source snapshots, result JSON, frames, and package
checksums. Recorded artifacts describe a particular run; they do not assert that today's checkout or
all operating systems have been retested. Historical hashes and frames are retained unchanged.

Check generated contracts with:

```sh
python3 scripts/generate-rest-reference.py --check
python3 scripts/check-tui.py
```

These structural checks do not execute inference or establish SDK parity. Each live harness documents
its prerequisites, target, cleanup, and limits.

- [Standalone serving](SERVE.md): `sharpai serve`, NuGet tool lifecycle, CLI options, and unauthenticated single-model inference.
