# SDK status and verification

This page describes the checked-in SDKs, not the versions currently published to package registries.
The complete server contract is in [REST_API.md](../REST_API.md). SDK coverage is partial: do not infer
complete endpoint support from the server/Postman inventory or from a passing SDK unit test suite.

| SDK | README | Automated tests | Live-server console tool |
| --- | --- | --- | --- |
| C# `SharpAI.Sdk` | [C# guide](../sdk/csharp/README.md) | HTTP transport/secret-log unit tests plus an older live consumer harness | `Test.SharpAISdk` interactive app exists; its authentication flow needs updating |
| JavaScript/TypeScript `@sharpai/sdk` | [JS/TS guide](../sdk/js/README.md) | Vitest HTTP-mocked unit tests | No dedicated console app or live-server harness |
| Python `sharpai` | [Python guide](../sdk/python/README.md) | Standard-library unit tests and a small live operations suite | No dedicated console app; live tests need current bearer setup |

The C# directory also contains `SharpAI.Client`, the HTTP transport used by the TUI. It supports
additional workflows, including scoped libraries, and exposes generic HTTP access. It is separate from
`SharpAI.Sdk`; its capabilities do not automatically exist in the public SDK or the other languages.

## Supported areas and gaps

The SDKs expose model operations, Ollama/OpenAI inference, settings, history reads, login/session
operations, and selected administration methods. Credential token reveal/rotation and verified model
pull sources are implemented in all three. C# and JavaScript expose streaming inference methods.

The public SDKs lack complete named-method coverage for scoped libraries, administration updates and
deletes, permission and role management, credential assignments, tenant sessions, diagnostics, and
history deletion. Consult each implementation rather than assuming every REST operation has a wrapper.

Python `chat`/`generate` accept a `stream` argument, but currently read the entire response through the
ordinary request path. OpenAI calls use the same buffered path. They do not expose proper NDJSON/SSE
inference iterators; only model pull has a progress iterator.

Examples in older SDK README sections can omit authentication or overstate compatibility. Supply a
bearer token or use the SDK's login method before protected calls. Legacy API/access-key constructor
parameters do not authenticate against the current server. The C# interactive app and existing C#/Python
live harnesses do not yet provide a complete current-authentication workflow out of the box.

## Running the existing unit tests

From the repository root:

```sh
dotnet test sdk/csharp/tests/SharpAI.Sdk.Tests -c Release
npm ci --prefix sdk/js
npm test --prefix sdk/js
python3 -m unittest discover -s sdk/python/tests -p 'test_client.py'
```

The JavaScript and Python unit suites most recently passed 9 and 7 cases respectively in the
[API evidence bundle](api/validation.json). These are mocked/unit results, not proof of live SDK parity.

The existing C# console projects are runnable from source:

```sh
dotnet run --project sdk/csharp/src/Test.SharpAISdk -f net8.0
dotnet run --project sdk/csharp/src/Test.Automated -f net8.0 -- --contracts http://127.0.0.1:8000
```

The first is an interactive inference demo. The second expects an `ops-story` model and currently
constructs a client without a bearer token. Both require authentication updates before they can serve
as ready-to-use live exercises against the current server. Python's `test_operations_live.py` similarly
expects `SHARPAI_TEST_SERVER_URL` and `ops-story`, but constructs an unauthenticated client. A skipped
live test is not a passing live contract check.

For a working current server validation path, use the [Postman harness](../postman/README.md):

```sh
python3 scripts/test-rest-reference.py --model /path/to/local-completion.gguf --output artifacts/rest-reference
```

It starts a disposable authenticated server, verifies the actual HTTP contracts, and cleans up its data.
It exercises the server and collection, not the three SDK implementations.
