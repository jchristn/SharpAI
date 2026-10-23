# SharpAI Postman resources

## Complete REST collection

Import [SharpAI.postman_collection.json](SharpAI.postman_collection.json) into Postman. It covers
all 97 registered server operations in three top-level folders:

- **Ollama APIs:** model discovery/lifecycle and chat, completion, and embedding inference.
- **OpenAI APIs:** model listing and compatible chat, completion, and embedding inference.
- **Management APIs:** authentication, tenants, users, credentials, roles, permissions, assignments,
  sessions, audit, scoped libraries, history, diagnostics, settings, probes, documentation, and metrics.
  Existing boundary and terminal assertions are retained under **Regression scenarios**.

See [REST_API.md](../REST_API.md) for required inputs, response fields, permissions, filters,
stream framing, cancellation, and compatibility limits. Four extra inference requests verify NDJSON/SSE streaming, including terminal records and optional usage.
Saved examples are illustrative wire shapes,
not captured production responses. Error examples preserve the bearer challenge shape.

Create a private Postman environment and set these values as needed:

| Variable | Purpose |
| --- | --- |
| `protocol`, `hostname`, `port` | Server origin; defaults to `http://localhost:8000` |
| `bearerToken` | Session or API credential used by ordinary requests |
| `loginEmail`, `loginPassword`, `tenantGuid` | Login headers; login saves the returned bearer and identity to the active environment |
| `adminBearerToken` | Administrative bearer for the retained credential regression scenarios |
| `model`, `embeddingModel` | Existing model names with the required capability |
| `maxTokens` | Explicit generation budget; default 2048, includes hidden reasoning |
| `modelSource`, `modelPath`, `importName` | Remote pull source or server-local import path/name |
| `userGuid`, `credentialGuid`, `roleGuid`, `permissionGuid`, `assignmentGuid`, `sessionGuid` | Existing target resource identifiers |
| `modelGuid`, `historyId`, `systemPromptId`, `modelConfigurationId`, `presetName` | Inspection, history, library, or preset targets |
| `newUserEmail`, `newUserPassword` | User creation/update/reset inputs |
| `settingsJson` | Complete settings JSON fetched first with GET; PUT replaces the whole object and accepts the returned enum names unchanged |

Collection values for secrets and deployment-specific IDs are blank. Keep environment exports and CLI
reports containing tokens private. The collection includes mutations, logout, and secret reveal/rotation operations. It is a request catalog,
not an ordered provisioning script: select requests in the order appropriate to your target records.
Query parameters are present but disabled until you enable them. Use IDs from responses rather than
reusing a user assignment ID as a credential assignment ID.

Example read-only local CLI run with a private environment file:

```sh
postman collection run postman/SharpAI.postman_collection.json \
  -e /path/to/private.postman_environment.json \
  -i d0975fc0-1563-547a-8dc4-a4a16f2e44ad --no-report-events
```

The ID selects **GET /api/tags**; stable request IDs avoid ambiguity between route slashes and folder paths.
Run from the repository root. The guide and collection are generated together:

```sh
python3 scripts/generate-rest-reference.py
python3 scripts/generate-rest-reference.py --check
python3 scripts/test-rest-reference.py --model /path/to/local-completion.gguf --output artifacts/rest-reference
```

The validation script starts a disposable native SQLite server with HTTP metrics enabled, compares its live OpenAPI inventory,
creates tenant/user/role/credential/library fixtures, imports the supplied real GGUF, and runs selected
saved requests through the Postman CLI. It always stops the server and removes the temporary database,
model copies, and secret environment file. It does not publish a workspace or alter your normal server.
It pulls a pinned public MiniLM GGUF through the saved collection, verifies real embeddings through both
API families, and checks authenticated HTTP metrics. The favicon is tested from an empty working directory
using the actual bundled ICO. Raw GET-to-PUT settings round trips and invalid enums are also exercised.

Local .NET 8/10 results and source hashes are recorded in [validation evidence](../docs/api/validation.json).
CI repeats the native collection checks on both frameworks.

Generation inputs are the checked OpenAPI snapshot, reviewed operation semantics in the generator,
DTO source fields, and [maintained regression scripts](regression-scenarios.json). Edit those inputs,
then regenerate; avoid hand-editing generated collection requests.

## Local download simulation

`mocks/huggingface` implements synthetic repository metadata, pinned tree listings, GGUF download bytes,
and HTTP Range/ETag failure cases. It has no real upstream dependency or credentials. Unmodeled paths
return 501. It is intentionally not a general Hugging Face emulator.

```sh
postman simulation run postman/simulations/downloads.sim.yaml --port 4907 --output ndjson
```

In another terminal:

```sh
SHARPAI_TEST_HF_URL=http://127.0.0.1:4907/hf dotnet test src/Test.Xunit -f net8.0
```

Use `scripts/test-operations.sh` to manage the simulation and an isolated real SharpAI server together,
including cleanup and coverage. Details and limitations are in [operations](../docs/OPERATIONS.md).

## Live authentication and token management

The repository collection `SharpAI.postman_collection.json` uses HTTP bearer authentication. Keep
`bearerToken` in a private environment for normal API access. **Authentication boundary** provides read-only
401/200 assertions for model access and metrics.

**Credential token management** uses `adminBearerToken`, `tenantGuid`, and `credentialGuid` from the environment.
Reveal requires Credential Admin permission, returns a no-store response and is audited. Regeneration immediately
invalidates the old token and preserves its grants/status/expiry; run that request only when replacement is intended.
The replacement is stored in the private environment variable `revealedBearerToken`; do not export it into Git.
Hash-only legacy tokens return 409 from reveal and require regeneration. Ordinary credential reads remain redacted.
The live OpenAPI document is authoritative for routes, permission requirements and response codes. Full details
are in [the deployment guide](../DEPLOYMENT_GUIDE.md#reveal-and-regenerate-api-bearer-tokens).

## Terminal transport contracts

`postman/simulations/tui.sim.yaml` runs the checked SharpAI fixtures on one local origin.
`python3 scripts/test-tui.py --model /path/to/model.gguf` drives the real C# client and terminal host
through it, alongside a separate native server. Fixtures under `/fixture` are synthetic transport cases,
not server endpoints or inference compatibility evidence. The collection includes a read-only
Management APIs > Regression scenarios > Terminal dashboard contracts folder and a verified pull assertion using `modelSource`. No workspace
push or public deployment is required for local verification.

### Playground command regression fixtures

TUI and web composer commands are client actions defined in
`dashboard/src/api/chatCommands.json`; they are not new server endpoints. `/compact`
uses the existing nonstreaming `/api/chat` contract and only commits a completed, nonempty,
uncancelled summary with a non-length finish reason. The TUI simulation records fixture
chat requests at `/fixture/chat-requests` and covers summary success, HTTP failure,
truncation, cancellation, Markdown responses, and literal slash input. The fixture routes
are test-only and are not server API operations.
