# SharpAI REST API

Complete reference for the SharpAI v4.0.1 server's registered HTTP operations. The server hosts
GGUF inference and administration on one origin, normally `http://localhost:8000`.
Ollama-compatible operations use `/api/`, OpenAI-compatible operations use `/v1/`, and
SharpAI management operations generally use `/v1.0/` (with settings and diagnostics under `/api/`).

Import [SharpAI.postman_collection.json](postman/SharpAI.postman_collection.json) for runnable requests,
examples, and assertions organized into **Ollama APIs**, **OpenAI APIs**, and **Management APIs**.
The checked [OpenAPI snapshot](docs/api/openapi.json) and the authenticated live `/openapi.json`
provide a tooling-oriented inventory. This guide adds field-level details and implementation limits
that generic OpenAPI object schemas do not express.

## Contents

- [Authentication and multitenancy](#authentication-and-multitenancy)
- [Requests, pagination and errors](#requests-pagination-and-errors)
- [Streaming and cancellation](#streaming-and-cancellation)
- [Compatibility limits](#compatibility-limits)
- [Endpoint index](#endpoint-index)
- [Ollama APIs](#ollama-apis)
- [OpenAI APIs](#openai-apis)
- [Management APIs](#management-apis)
- [Field reference](#field-reference)
- [Maintaining this reference](#maintaining-this-reference)

## Authentication and multitenancy

Send `Authorization: Bearer <token>` on protected requests. The server always requires bearer
authentication, including `/`, model lists, metrics, OpenAPI, and Swagger. Only exact `/health`,
`/ready`, and `POST /v1.0/token` are anonymous application paths. CORS OPTIONS preflight is handled
by middleware. `HEAD /health` is anonymous; `HEAD /` and favicon requests are protected.
Legacy access-key headers, `x-api-key`, and HTTP Basic authentication are not accepted for API access.
Changing legacy `Auth.Enabled` does not make the current server's HTTP boundary anonymous.

Two bearer types serve different callers:

| Type | How obtained | Use |
| --- | --- | --- |
| User session | Login with email/password headers | Interactive dashboard/TUI users; revocable and expiring |
| API credential | Create a credential for a tenant user | Application integrations; explicit role/scope assignments, expiry and active status |

Login accepts headers, not a JSON password body:

```sh
export SHARPAI_URL=http://localhost:8000
curl -X POST "${SHARPAI_URL}/v1.0/token" \
  -H "x-email: ${SHARPAI_EMAIL}" \
  -H "x-password: ${SHARPAI_PASSWORD}" \
  -H "x-tenant-guid: ${SHARPAI_TENANT_GUID:-default}"
```

The response contains `token`, `sessionId`, `tenantId`, `userId`, and `expiresUtc`.
Store the token privately as `SHARPAI_BEARER_TOKEN` for the examples below. The default tenant is
used when `x-tenant-guid` is omitted. Fresh installations seed tenant `default`, email
`admin@sharpai.local`, password `password`; existing accounts retain their passwords.
Use HTTPS when credentials travel beyond a trusted local connection.

`GET /v1.0/token` reads the current user session; `DELETE /v1.0/token` revokes it. A credential bearer
can authenticate ordinary permitted requests but does not become a user session. Password reset
revokes the user's sessions. Disabled/deleted users, inactive tenants, expired/revoked sessions,
and inactive/expired/deleted credentials cannot continue authenticating.

A platform administrator manages tenants and may explicitly select other tenant scopes. Other
principals are bound to their authenticated tenant: changing a path's `tenantGuid` or a filter does
not grant cross-tenant access. Tenant administrators manage their own tenant and cannot become
platform administrators by granting roles. Built-in/protected records have additional mutation checks.

RBAC uses resource types (`Tenant`, `User`, `Credential`, `Session`, `Role`, `Permission`, `Assignment`,
`Audit`, `Admin`, `Model`, `Inference`, `Settings`, `RequestHistory`, `SystemPrompt`,
`ModelConfiguration`, or wildcard `All`) and operations (`Create`, `Read`, `Update`, `Delete`,
`Execute`, `Admin`, shorthand `Write`, or wildcard `All`). `Write` expands to create/update/delete.
Assignments select a role by `RoleGuid` or built-in `RoleName`, with `ResourceScope` equal to `Tenant`
or `Resource`. Resource scope requires a `ResourceGuid`; `InheritsToChildren` controls propagation.
Explicit denies win for ordinary principals; administrators have the server's privileged bypass rules.
A credential needs its own grants and remains subject to the owning user's effective permission ceiling.
Creating a credential alone does not grant inference rights.

Tenant isolation applies to tenant-owned identities, grants, libraries, audits, and history. The model
registry, resident weights, inference capacity, legacy presets, and server settings are shared
server resources; tenants do not receive separate inference processes, model stores, or hardware quotas.
Use separate server deployments when those boundaries are required.

Scoped libraries add ownership rules: System items are shared, Tenant items are tenant defaults,
and User items are private even from administrators. Only authorized administrators publish shared
items; users duplicate shared items to edit a personal copy. See [scoped libraries](docs/SCOPED_LIBRARIES.md).

Compose provisions a limited model/inference bootstrap credential at `docker/auth/default-bearer-token`.
It cannot administer settings or accounts. Ordinary credential reads never expose a usable token;
create, authorized reveal and rotation return secret-bearing responses marked `Cache-Control: no-store`.
Reveal/rotation are audited. A legacy hash-only credential returns 409 on reveal and must be rotated;
rotation invalidates its previous token immediately. See [access administration](docs/DASHBOARD_ADMINISTRATION.md).

## Requests, pagination and errors

- Send JSON bodies with `Content-Type: application/json`. Inference DTOs use their documented
  lowercase/snake_case JSON names; most management DTOs use PascalCase. Use the names shown in examples.
- Path values must be individually URL-encoded. IDs are opaque strings unless a route explicitly
  requires a GUID. A model name and a model registry GUID are different identifiers.
- General list routes support `pageNumber` (1-based, default 1), `pageSize` (default 100, clamped
  1–1000), `order` (`CreatedAscending` or `CreatedDescending`), `createdAfter`, and `createdBefore`.
  Each route lists its applicable filters. Model-list compatibility routes return all registered models.
- Management pages use `EnumerationResult<T>` with `Objects`, `TotalRecords`, `RecordsRemaining`,
  `EndOfResults`, `MaxResults`, `Skip`, `IterationsRequired`, `Success`, and timestamp metadata.
  Advance `pageNumber` until `EndOfResults` is true. Some assignment/mapping routes return plain arrays;
  those are called out individually. History has its own filters and default page size of 25.
- UTC timestamps use ISO 8601; OpenAI `created` values use Unix seconds. Ollama duration values
  are nanoseconds unless their field states another unit. Missing metrics are unavailable, not zero.
- Successful mutations generally return 200, not 201 or 204. Model deletion has no JSON result.
- Responses carry `X-SharpAI-Request` for correlation. Treat tokens, settings, prompt bodies and history
  exports as private data. History capture depends on configuration; it is not an unconditional audit
  of all requests. Library bodies are withheld from shared history to protect personal items.

| HTTP status | Meaning |
| --- | --- |
| 200 | Completed JSON result, content, or streaming response (inspect terminal records) |
| 400 | Missing/invalid input, invalid scope/configuration, or context-length preflight rejection |
| 401 | Missing/invalid/expired bearer or failed login; protected requests advertise `WWW-Authenticate: Bearer` |
| 403 | Insufficient permission, cross-tenant access, protected mutation, or incompatible model capability |
| 404 | Missing model/resource, or an item outside the caller's visible ownership scope |
| 409 | Duplicate name or retained credential token unavailable |
| 429 | Inference engine busy or model memory/admission budget exceeded |
| 500 | Unhandled processing/native inference failure |
| 503 | Readiness dependencies are not ready |

Error envelopes vary by layer. The pre-routing bearer challenge returns:

```json
{"error":"Bearer authentication is required."}
```

Ollama inference errors normally use `{"error":"message"}`. OpenAI inference errors normally use:

```json
{"error":{"message":"message","type":"context_length_exceeded","code":"context_length_exceeded"}}
```

The inference translator uses types such as `invalid_request_error`, `authentication_error`,
`permission_error`, `not_found_error`, `conflict_error`, `server_busy`, `rate_limit_exceeded`, and
`internal_error`. Explicit handler validation can use its own OpenAI error type/parameter values.
Management exceptions use Watson's error envelope; clients must inspect the HTTP status and retain
unexpected JSON/text errors rather than assume every error has the OpenAI shape.
A representative management validation response is:

```json
{"Error":"BadRequest","StatusCode":400,"Description":"The request was invalid or malformed.","Message":"A name is required."}
```


Retry bounded, idempotent reads when appropriate. Never automatically repeat a generation, model pull,
credential issuance/rotation, or management mutation after an ambiguous transport failure: first inspect
the outcome. The server does not provide a general idempotency-key contract.

## Streaming and cancellation

Ollama chat/completion use newline-delimited JSON (`application/x-ndjson`) when streaming is enabled.
Ollama defaults to streaming when `stream` is omitted. Accumulate each content delta and continue reading
through the final `done: true` record; terminal records can contain output as well as timings and usage.
A `done_reason` of `length` means the output-token budget was exhausted. Do not treat HTTP chunk boundaries
as JSON record boundaries or assume one chunk is one UTF-8 character.

Illustrative NDJSON records (timings omitted):

```json
{"model":"example","message":{"role":"assistant","content":"Hello"},"done":false}
{"model":"example","message":{"role":"assistant","content":" there."},"done":true,"done_reason":"stop"}
```

OpenAI chat/completion default to nonstreaming. `stream: true` uses server-sent events
(`text/event-stream`): parse each `data:` record, consume terminal `finish_reason` chunks, and stop at
`data: [DONE]`. Chat content is in `choices[].delta`; completion text is in `choices[].text`.
With `stream_options: {"include_usage": true}`, a separate usage-only chunk with empty `choices`
arrives before `[DONE]`. Finish reasons include `stop`, `length`, and `tool_calls` when appropriate.
Usage and timing belong to the complete call, not an individual delta.

```text
data: {"choices":[{"index":0,"delta":{"content":"Hello"},"finish_reason":null}]}

data: {"choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

data: [DONE]
```

The pull handler always emits NDJSON progress, even with `stream: false`. Require an explicit
`status: "success"`; an HTTP 200 can precede a later `{error: ...}` record. Progress `percent` is
fractional (1 is complete), with `downloaded` bytes and optional `file`.

Cancel an HTTP operation by closing/cancelling the client request. There is no standalone REST
`/stop` or chat-session endpoint. Client commands such as `/clear` and `/compact` are dashboard/TUI
features; compaction calls ordinary nonstreaming chat. A stream that closes without its protocol's
terminal marker is incomplete even if its HTTP status was 200. Once headers have been sent, a failure
cannot reliably replace the response with a new HTTP status or error body.

## Compatibility limits

This is the implemented SharpAI surface, not every endpoint or feature in upstream Ollama or OpenAI.
There are no Responses API, image/audio generation, hosted assistants, fine-tuning, or file-upload routes.
GGUF text/embedding capability is model-dependent. Tool definitions and tool-call messages are supported
for chat; the caller executes tools and supplies their results. Tool-mode output may be buffered before
structured calls are emitted. JSON output uses supported grammar constraints and model capability.

Some compatibility DTO fields are accepted without corresponding behavior:

- OpenAI generation uses one completion per input prompt; `n` does not request multiple candidates.
  `best_of`, `echo`, `suffix`, log-probability controls, `logit_bias`, and the end-user `user` tag do not
  implement the full upstream behavior. `parallel_tool_calls` does not create a tool execution service.
- Embedding inputs accept text or text arrays, not token-ID arrays. Output is floating-point vectors
  at the model's dimensions; do not rely on `encoding_format` or `dimensions` to transform them.
- Ollama request-level `keep_alive`, raw/template/context compatibility fields, and hardware tuning
  options do not replace the server's model lifecycle, chat-template resolver, or runtime configuration.
  Chat rendering uses model metadata and configured presets. Configure residency and hardware on the server.
- The generation engine honors the implemented temperature, token budget, top-p, top-k, min-p,
  repeat/presence/frequency penalty, repeat window, seed, and stop-sequence paths. Other compatibility
  option fields in DTO tables are not a guarantee of an active sampler or per-request runtime override.

For model downloads, source validation, immutable revisions, shard handling and verification, see
[model pull sources](docs/MODEL_PULL_SOURCES.md). For capacity, context and timing details, see
[inference operations](docs/OPERATIONS.md).

### Settings replacement and static assets

GET settings responses can be submitted unchanged to PUT, including named values such as
`Rest.AccessControl.Mode: "DefaultPermit"`. Settings use the same serializer as configuration files;
defined numeric access-control modes remain accepted, and invalid access-control modes return 400. PUT is a full
replacement, so preserve the fetched object and change only the intended values.

`GET /favicon.ico` serves the bundled ICO asset from the application output directory, independently
of the server's working directory, with content type `image/x-icon`.
