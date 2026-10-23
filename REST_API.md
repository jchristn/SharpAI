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

## Endpoint index

The reference covers **97 registered operations**. Metrics availability depends on server configuration.

| Method | Path | Purpose |
| --- | --- | --- |
| GET | [`/`](#get-) | Server homepage |
| HEAD | [`/`](#head-) | Server liveness check |
| POST | [`/api/chat`](#post-api-chat) | Generate chat completion |
| DELETE | [`/api/delete`](#delete-api-delete) | Delete a model |
| GET | [`/api/diagnostics/memory`](#get-api-diagnostics-memory) | Process memory and model estimates |
| GET | [`/api/diagnostics/runtime`](#get-api-diagnostics-runtime) | Runtime capabilities and configuration |
| POST | [`/api/embed`](#post-api-embed) | Generate embeddings |
| POST | [`/api/generate`](#post-api-generate) | Generate text completion |
| POST | [`/api/import`](#post-api-import) | Import a local GGUF model |
| POST | [`/api/inspect`](#post-api-inspect) | Inspect a registered model by name without loading weights |
| GET | [`/api/models/{id}/inspect`](#get-api-models-id-inspect) | Inspect GGUF metadata and memory without loading weights |
| GET | [`/api/ps`](#get-api-ps) | List running (loaded) models |
| POST | [`/api/pull`](#post-api-pull) | Pull a model |
| GET | [`/api/settings`](#get-api-settings) | Get current server settings |
| PUT | [`/api/settings`](#put-api-settings) | Update server settings |
| POST | [`/api/show`](#post-api-show) | Show model information |
| GET | [`/api/tags`](#get-api-tags) | List local models |
| POST | [`/api/unload`](#post-api-unload) | Unload a model from memory |
| GET | [`/api/version`](#get-api-version) | Server version |
| GET | [`/favicon.ico`](#get-favicon-ico) | Serve favicon |
| HEAD | [`/favicon.ico`](#head-favicon-ico) | Favicon HEAD probe |
| GET | [`/health`](#get-health) | Health check |
| HEAD | [`/health`](#head-health) | Health HEAD probe |
| GET | [`/metrics`](#get-metrics) | Read HTTP server metrics |
| GET | [`/openapi.json`](#get-openapi-json) | GET /openapi.json |
| GET | [`/ready`](#get-ready) | Readiness check |
| GET | [`/swagger`](#get-swagger) | GET /swagger |
| GET | [`/v1.0/api/audit`](#get-v1-0-api-audit) | List security audit events |
| GET | [`/v1.0/api/request-history`](#get-v1-0-api-request-history) | List captured requests |
| DELETE | [`/v1.0/api/request-history`](#delete-v1-0-api-request-history) | Bulk delete captured requests |
| GET | [`/v1.0/api/request-history/summary`](#get-v1-0-api-request-history-summary) | Summarize captured requests |
| GET | [`/v1.0/api/request-history/{id}`](#get-v1-0-api-request-history-id) | Read a captured request |
| DELETE | [`/v1.0/api/request-history/{id}`](#delete-v1-0-api-request-history-id) | Delete a captured request |
| GET | [`/v1.0/auth/context`](#get-v1-0-auth-context) | Read dashboard authorization context |
| GET | [`/v1.0/library-context`](#get-v1-0-library-context) | Read library scope and publication rights |
| GET | [`/v1.0/model-configurations`](#get-v1-0-model-configurations) | List visible model-configurations |
| POST | [`/v1.0/model-configurations`](#post-v1-0-model-configurations) | Create a ModelConfiguration |
| GET | [`/v1.0/model-configurations/{id}`](#get-v1-0-model-configurations-id) | Read a visible ModelConfiguration |
| PUT | [`/v1.0/model-configurations/{id}`](#put-v1-0-model-configurations-id) | Update a ModelConfiguration |
| DELETE | [`/v1.0/model-configurations/{id}`](#delete-v1-0-model-configurations-id) | Delete a ModelConfiguration |
| POST | [`/v1.0/model-configurations/{id}/duplicate`](#post-v1-0-model-configurations-id-duplicate) | Duplicate a visible ModelConfiguration |
| GET | [`/v1.0/models/presets`](#get-v1-0-models-presets) | List model presets |
| POST | [`/v1.0/models/presets`](#post-v1-0-models-presets) | Create a model preset |
| GET | [`/v1.0/models/presets/{name}`](#get-v1-0-models-presets-name) | Read a model preset |
| DELETE | [`/v1.0/models/presets/{name}`](#delete-v1-0-models-presets-name) | Delete a model preset |
| GET | [`/v1.0/system-prompts`](#get-v1-0-system-prompts) | List visible system-prompts |
| POST | [`/v1.0/system-prompts`](#post-v1-0-system-prompts) | Create a SystemPrompt |
| GET | [`/v1.0/system-prompts/{id}`](#get-v1-0-system-prompts-id) | Read a visible SystemPrompt |
| PUT | [`/v1.0/system-prompts/{id}`](#put-v1-0-system-prompts-id) | Update a SystemPrompt |
| DELETE | [`/v1.0/system-prompts/{id}`](#delete-v1-0-system-prompts-id) | Delete a SystemPrompt |
| POST | [`/v1.0/system-prompts/{id}/duplicate`](#post-v1-0-system-prompts-id-duplicate) | Duplicate a visible SystemPrompt |
| GET | [`/v1.0/tenants`](#get-v1-0-tenants) | List tenants |
| POST | [`/v1.0/tenants`](#post-v1-0-tenants) | Create a tenant |
| GET | [`/v1.0/tenants/{tenantGuid}`](#get-v1-0-tenants-tenantguid) | Read a tenant |
| PUT | [`/v1.0/tenants/{tenantGuid}`](#put-v1-0-tenants-tenantguid) | Update a tenant |
| DELETE | [`/v1.0/tenants/{tenantGuid}`](#delete-v1-0-tenants-tenantguid) | Delete a tenant |
| POST | [`/v1.0/tenants/{tenantGuid}/assignments`](#post-v1-0-tenants-tenantguid-assignments) | Assign a role to a user |
| DELETE | [`/v1.0/tenants/{tenantGuid}/assignments/{assignmentGuid}`](#delete-v1-0-tenants-tenantguid-assignments-assignmentguid) | Revoke a role assignment |
| GET | [`/v1.0/tenants/{tenantGuid}/credentials`](#get-v1-0-tenants-tenantguid-credentials) | List credentials |
| POST | [`/v1.0/tenants/{tenantGuid}/credentials`](#post-v1-0-tenants-tenantguid-credentials) | Create a credential |
| GET | [`/v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}`](#get-v1-0-tenants-tenantguid-credentials-credentialguid) | Read a credential |
| PUT | [`/v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}`](#put-v1-0-tenants-tenantguid-credentials-credentialguid) | Update credential metadata, expiration, or active status |
| DELETE | [`/v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}`](#delete-v1-0-tenants-tenantguid-credentials-credentialguid) | Delete a credential |
| GET | [`/v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}/assignments`](#get-v1-0-tenants-tenantguid-credentials-credentialguid-assignments) | List a credential's scope assignments |
| POST | [`/v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}/assignments`](#post-v1-0-tenants-tenantguid-credentials-credentialguid-assignments) | Grant a role-scoped credential assignment |
| DELETE | [`/v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}/assignments/{assignmentGuid}`](#delete-v1-0-tenants-tenantguid-credentials-credentialguid-assignments-assignmentguid) | Revoke a credential scope assignment |
| GET | [`/v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}/permissions`](#get-v1-0-tenants-tenantguid-credentials-credentialguid-permissions) | Inspect a credential's effective permissions |
| GET | [`/v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}/token`](#get-v1-0-tenants-tenantguid-credentials-credentialguid-token) | Reveal a credential bearer token |
| POST | [`/v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}/token/rotate`](#post-v1-0-tenants-tenantguid-credentials-credentialguid-token-rotate) | Regenerate a credential bearer token |
| GET | [`/v1.0/tenants/{tenantGuid}/permissions`](#get-v1-0-tenants-tenantguid-permissions) | List permissions |
| POST | [`/v1.0/tenants/{tenantGuid}/permissions`](#post-v1-0-tenants-tenantguid-permissions) | Create a permission |
| PUT | [`/v1.0/tenants/{tenantGuid}/permissions/{permissionGuid}`](#put-v1-0-tenants-tenantguid-permissions-permissionguid) | Update a custom permission |
| DELETE | [`/v1.0/tenants/{tenantGuid}/permissions/{permissionGuid}`](#delete-v1-0-tenants-tenantguid-permissions-permissionguid) | Delete a custom permission |
| GET | [`/v1.0/tenants/{tenantGuid}/roles`](#get-v1-0-tenants-tenantguid-roles) | List roles |
| POST | [`/v1.0/tenants/{tenantGuid}/roles`](#post-v1-0-tenants-tenantguid-roles) | Create a custom role |
| PUT | [`/v1.0/tenants/{tenantGuid}/roles/{roleGuid}`](#put-v1-0-tenants-tenantguid-roles-roleguid) | Update a custom role |
| DELETE | [`/v1.0/tenants/{tenantGuid}/roles/{roleGuid}`](#delete-v1-0-tenants-tenantguid-roles-roleguid) | Delete a custom role |
| GET | [`/v1.0/tenants/{tenantGuid}/roles/{roleGuid}/permissions`](#get-v1-0-tenants-tenantguid-roles-roleguid-permissions) | List a role's mapped permissions |
| POST | [`/v1.0/tenants/{tenantGuid}/roles/{roleGuid}/permissions/{permissionGuid}`](#post-v1-0-tenants-tenantguid-roles-roleguid-permissions-permissionguid) | Map a permission to a role |
| DELETE | [`/v1.0/tenants/{tenantGuid}/roles/{roleGuid}/permissions/{permissionGuid}`](#delete-v1-0-tenants-tenantguid-roles-roleguid-permissions-permissionguid) | Remove a custom role's permission mapping |
| GET | [`/v1.0/tenants/{tenantGuid}/sessions`](#get-v1-0-tenants-tenantguid-sessions) | List tenant sessions without token material |
| DELETE | [`/v1.0/tenants/{tenantGuid}/sessions/{sessionGuid}`](#delete-v1-0-tenants-tenantguid-sessions-sessionguid) | Revoke a tenant session |
| GET | [`/v1.0/tenants/{tenantGuid}/users`](#get-v1-0-tenants-tenantguid-users) | List users |
| POST | [`/v1.0/tenants/{tenantGuid}/users`](#post-v1-0-tenants-tenantguid-users) | Create a user |
| GET | [`/v1.0/tenants/{tenantGuid}/users/{userGuid}`](#get-v1-0-tenants-tenantguid-users-userguid) | Read a user |
| PUT | [`/v1.0/tenants/{tenantGuid}/users/{userGuid}`](#put-v1-0-tenants-tenantguid-users-userguid) | Update a user |
| DELETE | [`/v1.0/tenants/{tenantGuid}/users/{userGuid}`](#delete-v1-0-tenants-tenantguid-users-userguid) | Delete a user |
| GET | [`/v1.0/tenants/{tenantGuid}/users/{userGuid}/assignments`](#get-v1-0-tenants-tenantguid-users-userguid-assignments) | List a user's role assignments |
| PUT | [`/v1.0/tenants/{tenantGuid}/users/{userGuid}/password`](#put-v1-0-tenants-tenantguid-users-userguid-password) | Reset a user's password and revoke sessions |
| GET | [`/v1.0/tenants/{tenantGuid}/users/{userGuid}/permissions`](#get-v1-0-tenants-tenantguid-users-userguid-permissions) | Inspect a user's effective permissions |
| GET | [`/v1.0/token`](#get-v1-0-token) | Read the current session |
| POST | [`/v1.0/token`](#post-v1-0-token) | Log in and create a session token |
| DELETE | [`/v1.0/token`](#delete-v1-0-token) | Revoke the current session |
| POST | [`/v1/chat/completions`](#post-v1-chat-completions) | Generate chat completion (OpenAI-compatible) |
| POST | [`/v1/completions`](#post-v1-completions) | Generate text completion (OpenAI-compatible) |
| POST | [`/v1/embeddings`](#post-v1-embeddings) | Generate embeddings (OpenAI-compatible) |
| GET | [`/v1/models`](#get-v1-models) | List models (OpenAI-compatible) |

## Ollama APIs

<a id="post-api-chat"></a>

### POST /api/chat

Generate chat completion

**Authorization:** Inference Execute.

**Required inputs:** model and messages (conversation array). Each message supplies role/content or the applicable tool-call/result fields.

**Success response:** OllamaGenerateChatCompletionResult; see the response DTO and streaming contract below.

**Field reference:** [`OllamaGenerateChatCompletionRequest`](#ollamageneratechatcompletionrequest), [`OllamaGenerateChatCompletionResult`](#ollamageneratechatcompletionresult) in REST_API.md.

model and messages are required. Send the full conversation on each chat call; server sessions authenticate users and do not store chat context. Output tokens include hidden reasoning. API default budget is 128 when not supplied (or preset default for chat); UI Balanced configuration explicitly supplies 2048. Context preflight may reject prompt plus output budget with 400. Capacity/admission failures return 429. Unsupported completion model returns 403; missing model returns 404. Ollama defaults to streaming when stream is omitted; stream=false returns a single JSON object.

**Server description:** Generates a chat completion from a sequence of messages. Supports streaming via chunked transfer.

**Declared responses:** 200, 429. Protected operations can also return 401; authorization checks can return 403.

**Example JSON body:**

```json
{
  "model": "{{model}}",
  "messages": [
    {
      "role": "user",
      "content": "Hello"
    }
  ],
  "stream": false,
  "options": {
    "num_predict": 2048
  }
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X POST "${SHARPAI_URL}/api/chat" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="delete-api-delete"></a>

### DELETE /api/delete

Delete a model

**Authorization:** Model Write.

**Required inputs:** name.

**Success response:** 200 with no JSON result on deletion; 404 with an error if not registered.

**Field reference:** [`OllamaDeleteModelRequest`](#ollamadeletemodelrequest) in REST_API.md.

show requires model; delete and unload use name. Deletion removes the registered model and managed files. Unload preserves downloaded files; an empty object unloads all resident models.

**Server description:** Deletes a locally stored model by name.

**Declared responses:** 200, 404. Protected operations can also return 401; authorization checks can return 403.

**Example JSON body:**

```json
{
  "name": "{{model}}"
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X DELETE "${SHARPAI_URL}/api/delete" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="post-api-embed"></a>

### POST /api/embed

Generate embeddings

**Authorization:** Inference Execute.

**Required inputs:** model and input (text string or nonempty text array).

**Success response:** OllamaGenerateEmbeddingsResult: model, embeddings (one float array per input), and available usage/timing fields.

**Field reference:** [`OllamaGenerateEmbeddingsRequest`](#ollamagenerateembeddingsrequest), [`OllamaGenerateEmbeddingsResult`](#ollamagenerateembeddingsresult) in REST_API.md.

model and input are required; input accepts a string or string array, not token ID arrays. Select a model with embedding support; incompatible models return 403. No streaming. Output dimensions follow the model.

**Server description:** Generates vector embeddings for a single input or array of inputs.

**Declared responses:** 200. Protected operations can also return 401; authorization checks can return 403.

**Example JSON body:**

```json
{
  "model": "{{embeddingModel}}",
  "input": [
    "Hello world",
    "A second input"
  ]
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X POST "${SHARPAI_URL}/api/embed" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="post-api-generate"></a>

### POST /api/generate

Generate text completion

**Authorization:** Inference Execute.

**Required inputs:** model and prompt; OpenAI prompt accepts a text string or nonempty string array.

**Success response:** OllamaGenerateCompletionResult; see the response DTO and streaming contract below.

**Field reference:** [`OllamaGenerateCompletionRequest`](#ollamageneratecompletionrequest), [`OllamaGenerateCompletionResult`](#ollamageneratecompletionresult) in REST_API.md.

model and prompt are required. Send the full conversation on each chat call; server sessions authenticate users and do not store chat context. Output tokens include hidden reasoning. API default budget is 128 when not supplied (or preset default for chat); UI Balanced configuration explicitly supplies 2048. Context preflight may reject prompt plus output budget with 400. Capacity/admission failures return 429. Unsupported completion model returns 403; missing model returns 404. Ollama defaults to streaming when stream is omitted; stream=false returns a single JSON object.

**Server description:** Generates a text completion for the given prompt. Supports streaming via chunked transfer.

**Declared responses:** 200, 429. Protected operations can also return 401; authorization checks can return 403.

**Example JSON body:**

```json
{
  "model": "{{model}}",
  "prompt": "Explain local inference in one sentence.",
  "stream": false,
  "options": {
    "num_predict": 2048
  }
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X POST "${SHARPAI_URL}/api/generate" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="post-api-import"></a>

### POST /api/import

Import a local GGUF model

**Authorization:** Model Write.

**Required inputs:** path; name is optional.

**Success response:** Imported model metadata: name, model, modified_at, size, digest, details (parent_model, format, family, families, parameter_size, quantization_level), and capabilities (embeddings/completions booleans).

**Field reference:** [`ImportModelRequest`](#importmodelrequest), [`OllamaLocalModel`](#ollamalocalmodel) in REST_API.md.

path is a GGUF file readable on the server filesystem, not a file upload or client-side path. name is optional, defaults to filename stem. Copies into managed storage, detects capabilities and registers the model; no Hugging Face token needed. 400 invalid path/file; 404 missing file; 409 duplicate name; 500 import failure.

**Server description:** Registers a GGUF model that already exists on the server's local filesystem ; no download and no HuggingFace token. Supply the local file 'path' and an optional 'name' (defaults to the file name). The file is copied into the models directory and its capabilities are detected from GGUF metadata. Returns the registered model details.

**Declared responses:** 200, 400, 404, 409. Protected operations can also return 401; authorization checks can return 403.

**Example JSON body:**

```json
{
  "path": "{{modelPath}}",
  "name": "{{importName}}"
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X POST "${SHARPAI_URL}/api/import" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="get-api-ps"></a>

### GET /api/ps

List running (loaded) models

**Authorization:** Model Read.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** OllamaListRunningModelsResult containing currently loaded model entries.

**Field reference:** [`OllamaListRunningModelsResult`](#ollamalistrunningmodelsresult), [`OllamaRunningModel`](#ollamarunningmodel) in REST_API.md.

size_vram reports full model size on a selected CUDA/Metal backend and zero on CPU; it is not measured GPU allocation. expires_at reflects configured keep-alive eviction or is null when disabled.

**Server description:** Returns the list of models that are currently loaded in memory, matching the Ollama '/api/ps' (ollama ps) endpoint. The size_vram field reports the full model size when the CUDA or Metal backend is active and 0 when the CPU backend is active. When keep-alive eviction is enabled (SHARPAI_KEEP_ALIVE_SECONDS), expires_at reports the eviction time; otherwise it is null.

**Declared responses:** 200. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/api/ps" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="post-api-pull"></a>

### POST /api/pull

Pull a model

**Authorization:** Model Write.

**Required inputs:** model, or legacy name, identifying a supported Hugging Face source.

**Success response:** NDJSON progress records, ending with {status: "success", downloaded, percent: 1}; errors after headers appear as {error: string}.

**Field reference:** [`OllamaPullModelRequest`](#ollamapullmodelrequest) in REST_API.md.

model (or legacy name) is a Hugging Face organization/repository or validated HTTPS repository/tree/blob/resolve GGUF URL. The current handler always streams progress, including when stream=false is sent. insecure/username/password fields are not used for registry authentication; configure the server Hugging Face token. Revisions resolve to pinned commits; exact GGUF file selection is honored. Read every record and require explicit success. Never automatically retry an ambiguous pull.

**Server description:** Downloads a verified GGUF model from a Hugging Face organization/name, HTTPS repository URL, tree revision URL, or resolve/blob GGUF URL. Resolves revisions to immutable commits and honors exact file selection. Streams NDJSON progress with downloaded bytes and percent; success is explicit. Failures after headers are error records. Never automatically retry a pull.

**Declared responses:** 200, 400. Protected operations can also return 401; authorization checks can return 403.

**Example JSON body:**

```json
{
  "model": "{{modelSource}}",
  "stream": true
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X POST "${SHARPAI_URL}/api/pull" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="post-api-show"></a>

### POST /api/show

Show model information

**Authorization:** Model Read.

**Required inputs:** model.

**Success response:** OllamaShowModelInfoResult with model metadata, parameters and capabilities.

**Field reference:** [`OllamaShowModelInfoRequest`](#ollamashowmodelinforequest), [`OllamaShowModelInfoResult`](#ollamashowmodelinforesult) in REST_API.md.

show requires model; delete and unload use name. Deletion removes the registered model and managed files. Unload preserves downloaded files; an empty object unloads all resident models.

**Server description:** Returns metadata and capabilities for a local model, matching Ollama's /api/show.

**Declared responses:** 200, 404. Protected operations can also return 401; authorization checks can return 403.

**Example JSON body:**

```json
{
  "model": "{{model}}"
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X POST "${SHARPAI_URL}/api/show" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="get-api-tags"></a>

### GET /api/tags

List local models

**Authorization:** Model Read.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** {models: [{name, model, modified_at, size, digest, details: {parent_model, format, family, families, parameter_size, quantization_level}, capabilities: {embeddings: boolean, completions: boolean}}]}; returns all registered models, empty array when none. Pagination/filter query parameters are not applied by this route.

**Field reference:** [`OllamaLocalModel`](#ollamalocalmodel) in REST_API.md.

**Server description:** Returns the list of locally available models.

**Declared responses:** 200. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/api/tags" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="post-api-unload"></a>

### POST /api/unload

Unload a model from memory

**Authorization:** Model Write.

**Required inputs:** No required model field; name selects one model, an empty object unloads all.

**Success response:** {status, model, message}; unloads resident weights but keeps files and registry. Without a model, unloads all resident models.

**Field reference:** [`OllamaUnloadModelRequest`](#ollamaunloadmodelrequest) in REST_API.md.

show requires model; delete and unload use name. Deletion removes the registered model and managed files. Unload preserves downloaded files; an empty object unloads all resident models.

**Server description:** Unloads a model from memory, freeing GPU/CPU resources. If no model name is provided, all loaded models are unloaded. This is useful when switching between models on memory-constrained systems.

**Declared responses:** 200, 404. Protected operations can also return 401; authorization checks can return 403.

**Example JSON body:**

```json
{
  "name": "{{model}}"
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X POST "${SHARPAI_URL}/api/unload" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="get-api-version"></a>

### GET /api/version

Server version

**Authorization:** Authenticated bearer; no additional route-level RBAC check.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** {version: string}.

**Server description:** Returns the SharpAI server version, matching Ollama's /api/version.

**Declared responses:** 200. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/api/version" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

## OpenAI APIs

<a id="post-v1-chat-completions"></a>

### POST /v1/chat/completions

Generate chat completion (OpenAI-compatible)

**Authorization:** Inference Execute.

**Required inputs:** model and messages (conversation array). Each message supplies role/content or the applicable tool-call/result fields.

**Success response:** OpenAIGenerateChatCompletionResult; see the response DTO and streaming contract below.

**Field reference:** [`OpenAIGenerateChatCompletionRequest`](#openaigeneratechatcompletionrequest), [`OpenAIGenerateChatCompletionResult`](#openaigeneratechatcompletionresult) in REST_API.md.

model and messages are required. Send the full conversation on each chat call; server sessions authenticate users and do not store chat context. Output tokens include hidden reasoning. API default budget is 128 when not supplied (or preset default for chat); UI Balanced configuration explicitly supplies 2048. Context preflight may reject prompt plus output budget with 400. Capacity/admission failures return 429. Unsupported completion model returns 403; missing model returns 404. OpenAI defaults to nonstreaming; stream=true uses SSE.

**Server description:** OpenAI-compatible chat completion endpoint. Supports streaming via server-sent events.

**Declared responses:** 200, 429. Protected operations can also return 401; authorization checks can return 403.

**Example JSON body:**

```json
{
  "model": "{{model}}",
  "messages": [
    {
      "role": "user",
      "content": "Hello"
    }
  ],
  "stream": false,
  "max_tokens": 2048
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X POST "${SHARPAI_URL}/v1/chat/completions" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="post-v1-completions"></a>

### POST /v1/completions

Generate text completion (OpenAI-compatible)

**Authorization:** Inference Execute.

**Required inputs:** model and prompt; OpenAI prompt accepts a text string or nonempty string array.

**Success response:** OpenAIGenerateCompletionResult; see the response DTO and streaming contract below.

**Field reference:** [`OpenAIGenerateCompletionRequest`](#openaigeneratecompletionrequest), [`OpenAIGenerateCompletionResult`](#openaigeneratecompletionresult) in REST_API.md.

model and prompt are required. Send the full conversation on each chat call; server sessions authenticate users and do not store chat context. Output tokens include hidden reasoning. API default budget is 128 when not supplied (or preset default for chat); UI Balanced configuration explicitly supplies 2048. Context preflight may reject prompt plus output budget with 400. Capacity/admission failures return 429. Unsupported completion model returns 403; missing model returns 404. OpenAI defaults to nonstreaming; stream=true uses SSE.

**Server description:** OpenAI-compatible text completion endpoint. Supports streaming via server-sent events.

**Declared responses:** 200, 429. Protected operations can also return 401; authorization checks can return 403.

**Example JSON body:**

```json
{
  "model": "{{model}}",
  "prompt": "Explain local inference in one sentence.",
  "stream": false,
  "max_tokens": 2048
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X POST "${SHARPAI_URL}/v1/completions" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="post-v1-embeddings"></a>

### POST /v1/embeddings

Generate embeddings (OpenAI-compatible)

**Authorization:** Inference Execute.

**Required inputs:** model and input (text string or nonempty text array).

**Success response:** OpenAIGenerateEmbeddingsResult: object=list, data entries (object=embedding, embedding float array, index), model, usage.

**Field reference:** [`OpenAIGenerateEmbeddingsRequest`](#openaigenerateembeddingsrequest), [`OpenAIGenerateEmbeddingsResult`](#openaigenerateembeddingsresult) in REST_API.md.

model and input are required; input accepts a string or string array, not token ID arrays. Select a model with embedding support; incompatible models return 403. No streaming. Output dimensions follow the model.

**Server description:** OpenAI-compatible embeddings endpoint.

**Declared responses:** 200. Protected operations can also return 401; authorization checks can return 403.

**Example JSON body:**

```json
{
  "model": "{{embeddingModel}}",
  "input": [
    "Hello world",
    "A second input"
  ]
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X POST "${SHARPAI_URL}/v1/embeddings" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="get-v1-models"></a>

### GET /v1/models

List models (OpenAI-compatible)

**Authorization:** Model Read.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Query parameters (optional):**
- `include_context`: Add trained_context_length and effective_context_length without loading weights.

**Success response:** {object: "list", data: [{id, object: "model", created: Unix seconds, owned_by: "sharpai"}]}; returns all registered models.

**Server description:** OpenAI-compatible model list; returns locally available models. Optional include_context=true adds trained_context_length and effective_context_length without loading weights.

**Declared responses:** 200. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1/models" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

## Management APIs

<a id="get-"></a>

### GET /

Server homepage

**Authorization:** Authenticated bearer; no additional route-level RBAC check.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** HTML operational homepage.

**Server description:** Returns the default HTML homepage indicating the node is operational.

**Declared responses:** 200. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="head-"></a>

### HEAD /

Server liveness check

**Authorization:** Authenticated bearer; no additional route-level RBAC check.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** No body; 200 when reachable and authenticated.

**Server description:** HEAD probe that returns 200 OK when the server is reachable.

**Declared responses:** 200. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -I "${SHARPAI_URL}/" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-api-diagnostics-memory"></a>

### GET /api/diagnostics/memory

Process memory and model estimates

**Authorization:** Admin Admin.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** working_set_bytes, peak_working_set_bytes, managed_heap_bytes, gc_available_bytes, model_budget_bytes, models inspection array, per_model_observed_available, per_model_observed_bytes, attribution_note.

**Declared responses:** 200. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/api/diagnostics/memory" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-api-diagnostics-runtime"></a>

### GET /api/diagnostics/runtime

Runtime capabilities and configuration

**Authorization:** Admin Admin.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** backend_selected, gpu_offload_verified, gpu_offload_note, framework, os, architecture, processors, apis, generation_slots, context_override, llamasharp_version.

**Declared responses:** 200. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/api/diagnostics/runtime" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="post-api-inspect"></a>

### POST /api/inspect

Inspect a registered model by name without loading weights

**Authorization:** Model Read.

**Required inputs:** model.

**Success response:** {model, sha256, inspection: ModelInspection}.

**Field reference:** [`ModelInspection`](#modelinspection) in REST_API.md.

Reads GGUF metadata and estimates without loading weights. GET id must be a model registry GUID, not its name. Missing model: 404; invalid GUID or missing model: 400. Estimates do not establish observed GPU offload or per-model allocations.

**Declared responses:** 200, 400, 404. Protected operations can also return 401; authorization checks can return 403.

**Example JSON body:**

```json
{
  "model": "{{model}}"
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X POST "${SHARPAI_URL}/api/inspect" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="get-api-models-id-inspect"></a>

### GET /api/models/{id}/inspect

Inspect GGUF metadata and memory without loading weights

**Authorization:** Model Read.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** ModelInspection object.

**Field reference:** [`ModelInspection`](#modelinspection) in REST_API.md.

Reads GGUF metadata and estimates without loading weights. GET id must be a model registry GUID, not its name. Missing model: 404; invalid GUID or missing model: 400. Estimates do not establish observed GPU offload or per-model allocations.

**Declared responses:** 200, 400, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `id`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/api/models/${MODEL_GUID}/inspect" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-api-settings"></a>

### GET /api/settings

Get current server settings

**Authorization:** Settings Read.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** Settings object (PascalCase), including configured storage, database, authentication, runtime, telemetry, logging and request-history settings.

**Field reference:** [`Settings`](#settings) in REST_API.md.

**Server description:** Returns the current in-memory server settings loaded from sharpai.json.

**Declared responses:** 200. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/api/settings" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="put-api-settings"></a>

### PUT /api/settings

Update server settings

**Authorization:** Admin Admin.

**Required inputs:** A complete settings object; preserve fields fetched by GET. Named enum values returned by GET can be sent unchanged.

**Success response:** Settings object (PascalCase), including configured storage, database, authentication, runtime, telemetry, logging and request-history settings.

**Field reference:** [`Settings`](#settings) in REST_API.md.

Full replacement, not PATCH. First GET settings and copy the complete JSON into settingsJson. CreatedUtc and SoftwareVersion are preserved. The file is rewritten; listener/database changes require restart. Do not submit a partial object: omitted values take DTO defaults. Settings may contain secrets; keep exports private. GET responses can be submitted unchanged to PUT; named or defined numeric Rest.AccessControl.Mode values are accepted and invalid modes return 400.

**Server description:** Replaces the in-memory server settings and rewrites sharpai.json on disk. CreatedUtc and SoftwareVersion are preserved from the current settings. Some settings (REST hostname, port, SSL, Database) require a server restart to take effect.

**Declared responses:** 200, 400. Protected operations can also return 401; authorization checks can return 403.

**Body variable:** Complete JSON fetched from GET settings.

```text
{{settingsJson}}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X PUT "${SHARPAI_URL}/api/settings" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="get-favicon-ico"></a>

### GET /favicon.ico

Serve favicon

**Authorization:** Authenticated bearer; no additional route-level RBAC check.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** ICO favicon bytes from the bundled assets/favicon.ico under the application directory; independent of the server working directory.

**Server description:** Returns the bundled SharpAI ICO favicon, independently of the working directory.

**Declared responses:** 200. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/favicon.ico" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="head-favicon-ico"></a>

### HEAD /favicon.ico

Favicon HEAD probe

**Authorization:** Authenticated bearer; no additional route-level RBAC check.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** No body; 200 when reachable and authenticated.

**Declared responses:** 200. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -I "${SHARPAI_URL}/favicon.ico" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-health"></a>

### GET /health

Health check

**Authorization:** Anonymous infrastructure probe.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** Health object: status, version, backend, native_initialized, utc.

**Server description:** Lightweight liveness check for container and process monitoring.

**Declared responses:** 200. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/health"
```

<a id="head-health"></a>

### HEAD /health

Health HEAD probe

**Authorization:** Anonymous infrastructure probe.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** No body.

**Declared responses:** 200. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -I "${SHARPAI_URL}/health"
```

<a id="get-metrics"></a>

### GET /metrics

Read HTTP server metrics

**Authorization:** Authenticated bearer; no additional route-level RBAC check.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** Prometheus text exposition of Watson HTTP server instruments; registered only when the HTTP metrics collector is enabled.

**Server description:** Authenticated Prometheus exposition for Watson HTTP server instruments.

**Declared responses:** 200, 401. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/metrics" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-openapi-json"></a>

### GET /openapi.json

GET /openapi.json

**Authorization:** Authenticated bearer; no additional route-level RBAC check.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** OpenAPI 3 document describing this server.

**Declared responses:** 200. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/openapi.json" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-ready"></a>

### GET /ready

Readiness check

**Authorization:** Anonymous infrastructure probe.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** Readiness object: status, version, backend, native_initialized, database_initialized, models_directory, models_directory_ready, logs_directory, logs_directory_ready, telemetry_enabled, telemetry_ready, utc. Returns 503 while dependencies are not ready.

**Server description:** Readiness check that verifies startup initialization and writable runtime directories.

**Declared responses:** 200, 503. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/ready"
```

<a id="get-swagger"></a>

### GET /swagger

GET /swagger

**Authorization:** Authenticated bearer; no additional route-level RBAC check.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** Swagger UI HTML.

**Declared responses:** 200. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/swagger" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-v1-0-api-audit"></a>

### GET /v1.0/api/audit

List security audit events

**Authorization:** Audit Read.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Query parameters (optional):**
- `pageNumber`: 1-based page; values below 1 clamp to 1.
- `pageSize`: 1 to 1000. Default 100 for general enumeration, 25 for request history.
- `order`: CreatedAscending or CreatedDescending; default CreatedDescending.
- `createdAfter`: Exclusive creation-time lower bound, UTC.
- `createdBefore`: Exclusive creation-time upper bound, UTC.
- `tenantGuid`: Platform administrators may scope to a tenant; others are constrained to their own tenant.

**Success response:** EnumerationResult<AuditLogEntry>.

**Field reference:** [`AuditLogEntry`](#auditlogentry) in REST_API.md.

**Server description:** Paginated list of security audit events (authentication failures and privileged operations). Requires administrator or tenant-administrator privileges. Global administrators may scope with the tenantGuid query parameter; tenant administrators are constrained to their tenant.

**Declared responses:** 200, 403. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/api/audit" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-v1-0-api-request-history"></a>

### GET /v1.0/api/request-history

List captured requests

**Authorization:** RequestHistory Read.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Query parameters (optional):**
- `pageNumber`: 1-based page; values below 1 clamp to 1.
- `pageSize`: 1 to 1000; default 25.
- `method`: Exact HTTP method.
- `statusCode`: Exact HTTP status.
- `pathContains`: Substring of request path.
- `category`: generation, embeddings, inference (both), or all.
- `fromUtc`: Inclusive UTC lower bound.
- `toUtc`: Exclusive UTC upper bound.
- `tenantId`: Platform administrators may select a tenant; others are bound to their authenticated tenant.
- `userId`: Filter by authenticated user ID.

**Success response:** EnumerationResult<RequestHistoryEntry>; list entries omit bodies.

**Field reference:** [`RequestHistoryEntry`](#requesthistoryentry), [`RequestHistorySummary`](#requesthistorysummary), [`RequestHistoryBucket`](#requesthistorybucket) in REST_API.md.

Non-platform principals are bound to their tenant. Inference history and embedding history use category filters on these same endpoints; they are not separate routes. Bulk DELETE removes all matching rows, not just a page; an unfiltered delete clears the caller-visible history scope.

**Server description:** Paginated list of captured requests (bodies omitted). Filters: method, statusCode, pathContains, category (generation, embeddings, or legacy combined inference), fromUtc, toUtc, tenantId, userId, pageNumber, pageSize.

**Declared responses:** 200. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/api/request-history" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="delete-v1-0-api-request-history"></a>

### DELETE /v1.0/api/request-history

Bulk delete captured requests

**Authorization:** RequestHistory Delete.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Query parameters (optional):**
- `pageNumber`: 1-based page; values below 1 clamp to 1.
- `pageSize`: 1 to 1000; default 25.
- `method`: Exact HTTP method.
- `statusCode`: Exact HTTP status.
- `pathContains`: Substring of request path.
- `category`: generation, embeddings, inference (both), or all.
- `fromUtc`: Inclusive UTC lower bound.
- `toUtc`: Exclusive UTC upper bound.
- `tenantId`: Platform administrators may select a tenant; others are bound to their authenticated tenant.
- `userId`: Filter by authenticated user ID.

**Success response:** {deletedCount: integer}.

**Field reference:** [`RequestHistoryEntry`](#requesthistoryentry), [`RequestHistorySummary`](#requesthistorysummary), [`RequestHistoryBucket`](#requesthistorybucket) in REST_API.md.

Non-platform principals are bound to their tenant. Inference history and embedding history use category filters on these same endpoints; they are not separate routes. Bulk DELETE removes all matching rows, not just a page; an unfiltered delete clears the caller-visible history scope.

**Server description:** Deletes all captured requests matching the supplied filter. Returns the number of rows deleted.

**Declared responses:** 200. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X DELETE "${SHARPAI_URL}/v1.0/api/request-history" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-v1-0-api-request-history-summary"></a>

### GET /v1.0/api/request-history/summary

Summarize captured requests

**Authorization:** RequestHistory Read.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Query parameters (optional):**
- `pageNumber`: 1-based page; values below 1 clamp to 1.
- `pageSize`: 1 to 1000; default 25.
- `method`: Exact HTTP method.
- `statusCode`: Exact HTTP status.
- `pathContains`: Substring of request path.
- `category`: generation, embeddings, inference (both), or all.
- `fromUtc`: Inclusive UTC lower bound.
- `toUtc`: Exclusive UTC upper bound.
- `tenantId`: Platform administrators may select a tenant; others are bound to their authenticated tenant.
- `userId`: Filter by authenticated user ID.
- `bucketMinutes`: Bucket width 1 to 1440 minutes, default 15; includes empty intervals.

**Success response:** RequestHistorySummary with time buckets, counts and average durations.

**Field reference:** [`RequestHistoryEntry`](#requesthistoryentry), [`RequestHistorySummary`](#requesthistorysummary), [`RequestHistoryBucket`](#requesthistorybucket) in REST_API.md.

Non-platform principals are bound to their tenant. Inference history and embedding history use category filters on these same endpoints; they are not separate routes. Bulk DELETE removes all matching rows, not just a page; an unfiltered delete clears the caller-visible history scope.

**Server description:** Time-bucketed counts and average durations for chart rendering. Emits a bucket for every interval including empty ones. Query: fromUtc, toUtc, bucketMinutes, plus the list filters.

**Declared responses:** 200. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/api/request-history/summary" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-v1-0-api-request-history-id"></a>

### GET /v1.0/api/request-history/{id}

Read a captured request

**Authorization:** RequestHistory Read.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** RequestHistoryEntry including captured/redacted headers and bodies; 404 if absent.

**Field reference:** [`RequestHistoryEntry`](#requesthistoryentry), [`RequestHistorySummary`](#requesthistorysummary), [`RequestHistoryBucket`](#requesthistorybucket) in REST_API.md.

Non-platform principals are bound to their tenant. Inference history and embedding history use category filters on these same endpoints; they are not separate routes. Bulk DELETE removes all matching rows, not just a page; an unfiltered delete clears the caller-visible history scope.

**Server description:** Returns a single captured request including headers and bodies.

**Declared responses:** 200, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `id`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/api/request-history/${HISTORY_ID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="delete-v1-0-api-request-history-id"></a>

### DELETE /v1.0/api/request-history/{id}

Delete a captured request

**Authorization:** RequestHistory Delete.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** {deleted: boolean}.

**Field reference:** [`RequestHistoryEntry`](#requesthistoryentry), [`RequestHistorySummary`](#requesthistorysummary), [`RequestHistoryBucket`](#requesthistorybucket) in REST_API.md.

Non-platform principals are bound to their tenant. Inference history and embedding history use category filters on these same endpoints; they are not separate routes. Bulk DELETE removes all matching rows, not just a page; an unfiltered delete clears the caller-visible history scope.

**Server description:** Deletes a single captured request by identifier.

**Declared responses:** 200. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `id`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X DELETE "${SHARPAI_URL}/v1.0/api/request-history/${HISTORY_ID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-v1-0-auth-context"></a>

### GET /v1.0/auth/context

Read dashboard authorization context

**Authorization:** Authenticated bearer; no additional route-level RBAC check.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** {authEnabled, isAdmin, isTenantAdmin, tenantId, tenantName, userId, capabilities}; capabilities contains read/write flags for Tenant, User, Credential, Role, Permission, Assignment, Session, Audit and Admin.

Capability hints do not replace server-side checks. userId reflects PrincipalGuid; for API credentials that is the credential principal identifier.

**Declared responses:** 200, 400, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/auth/context" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-v1-0-library-context"></a>

### GET /v1.0/library-context

Read library scope and publication rights

**Authorization:** Authenticated bearer; reports the caller's own library context.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Query parameters (optional):**
- `tenantId`: Selected tenant; only platform admins may select another tenant. Does not expose another user's private items.

**Success response:** {IsAdmin, IsTenantAdmin, TenantId, UserId, UserTenantId}.

**Field reference:** [`LibraryItem`](#libraryitem), [`GenerationConfiguration`](#generationconfiguration) in REST_API.md.

System items are shared, Tenant items are tenant defaults, User items are private to their owner (including from administrators). User ownership is derived from the principal. Only platform admins publish System items; tenant admins publish Tenant items in their own tenant. Duplicate to change visibility/ownership. CanEdit/CanDelete are response hints. IDs/timestamps are server assigned.

**Declared responses:** 200, 400, 401, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/library-context" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-v1-0-model-configurations"></a>

### GET /v1.0/model-configurations

List visible model-configurations

**Authorization:** Authenticated principal; library operation permission plus ownership/publication rules. Human users have self-service rights unless explicitly denied. Credential owner ceilings remain enforced.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Query parameters (optional):**
- `pageNumber`: 1-based page; values below 1 clamp to 1.
- `pageSize`: 1 to 1000. Default 100 for general enumeration, 25 for request history.
- `order`: CreatedAscending or CreatedDescending; default CreatedDescending.
- `createdAfter`: Exclusive creation-time lower bound, UTC.
- `createdBefore`: Exclusive creation-time upper bound, UTC.
- `name`: Exact name; maximum 256 characters.
- `tenantId`: Selected tenant; only platform admins may select another tenant. Does not expose another user's private items.

**Success response:** EnumerationResult<LibraryItem>; visibility filtering occurs before counts and pagination.

**Field reference:** [`LibraryItem`](#libraryitem), [`GenerationConfiguration`](#generationconfiguration) in REST_API.md.

System items are shared, Tenant items are tenant defaults, User items are private to their owner (including from administrators). User ownership is derived from the principal. Only platform admins publish System items; tenant admins publish Tenant items in their own tenant. Duplicate to change visibility/ownership. CanEdit/CanDelete are response hints. IDs/timestamps are server assigned.

**Server description:** System defaults, selected tenant defaults and only the caller's private items. Query: tenantId (platform admins only for other tenants), name, pageNumber, pageSize. Ownership filtering precedes pagination.

**Declared responses:** 200, 400, 401, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/model-configurations" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="post-v1-0-model-configurations"></a>

### POST /v1.0/model-configurations

Create a ModelConfiguration

**Authorization:** Authenticated principal; library operation permission plus ownership/publication rules. Human users have self-service rights unless explicitly denied. Credential owner ceilings remain enforced.

**Required inputs:** Name and Configuration; Scope defaults to User. Description is optional.

**Query parameters (optional):**
- `tenantId`: Selected tenant; only platform admins may select another tenant. Does not expose another user's private items.

**Success response:** LibraryItem.

**Field reference:** [`LibraryItem`](#libraryitem), [`GenerationConfiguration`](#generationconfiguration) in REST_API.md.

System items are shared, Tenant items are tenant defaults, User items are private to their owner (including from administrators). User ownership is derived from the principal. Only platform admins publish System items; tenant admins publish Tenant items in their own tenant. Duplicate to change visibility/ownership. CanEdit/CanDelete are response hints. IDs/timestamps are server assigned. Configuration is required; Content must be absent. Stop permits at most 64 non-null entries of at most 2048 characters each.

**Declared responses:** 200, 400, 401, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Example JSON body:**

```json
{
  "Name": "My saved item",
  "Description": "Reusable defaults",
  "Scope": "User",
  "Configuration": {
    "Temperature": 0.6,
    "MaxTokens": 2048,
    "TopP": 0.9,
    "Stop": [],
    "StreamResponse": true,
    "JsonOutput": false,
    "DisplayThinking": false
  }
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X POST "${SHARPAI_URL}/v1.0/model-configurations" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="get-v1-0-model-configurations-id"></a>

### GET /v1.0/model-configurations/{id}

Read a visible ModelConfiguration

**Authorization:** Authenticated principal; library operation permission plus ownership/publication rules. Human users have self-service rights unless explicitly denied. Credential owner ceilings remain enforced.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Query parameters (optional):**
- `tenantId`: Selected tenant; only platform admins may select another tenant. Does not expose another user's private items.

**Success response:** LibraryItem.

**Field reference:** [`LibraryItem`](#libraryitem), [`GenerationConfiguration`](#generationconfiguration) in REST_API.md.

System items are shared, Tenant items are tenant defaults, User items are private to their owner (including from administrators). User ownership is derived from the principal. Only platform admins publish System items; tenant admins publish Tenant items in their own tenant. Duplicate to change visibility/ownership. CanEdit/CanDelete are response hints. IDs/timestamps are server assigned.

**Declared responses:** 200, 400, 401, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `id`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/model-configurations/${MODEL_CONFIGURATION_ID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="put-v1-0-model-configurations-id"></a>

### PUT /v1.0/model-configurations/{id}

Update a ModelConfiguration

**Authorization:** Authenticated principal; library operation permission plus ownership/publication rules. Human users have self-service rights unless explicitly denied. Credential owner ceilings remain enforced.

**Required inputs:** Name and Configuration; Scope defaults to User. Description is optional.

**Query parameters (optional):**
- `tenantId`: Selected tenant; only platform admins may select another tenant. Does not expose another user's private items.

**Success response:** LibraryItem.

**Field reference:** [`LibraryItem`](#libraryitem), [`GenerationConfiguration`](#generationconfiguration) in REST_API.md.

System items are shared, Tenant items are tenant defaults, User items are private to their owner (including from administrators). User ownership is derived from the principal. Only platform admins publish System items; tenant admins publish Tenant items in their own tenant. Duplicate to change visibility/ownership. CanEdit/CanDelete are response hints. IDs/timestamps are server assigned. Configuration is required; Content must be absent. Stop permits at most 64 non-null entries of at most 2048 characters each.

**Declared responses:** 200, 400, 401, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `id`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Example JSON body:**

```json
{
  "Name": "My saved item",
  "Description": "Reusable defaults",
  "Scope": "User",
  "Configuration": {
    "Temperature": 0.6,
    "MaxTokens": 2048,
    "TopP": 0.9,
    "Stop": [],
    "StreamResponse": true,
    "JsonOutput": false,
    "DisplayThinking": false
  }
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X PUT "${SHARPAI_URL}/v1.0/model-configurations/${MODEL_CONFIGURATION_ID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="delete-v1-0-model-configurations-id"></a>

### DELETE /v1.0/model-configurations/{id}

Delete a ModelConfiguration

**Authorization:** Authenticated principal; library operation permission plus ownership/publication rules. Human users have self-service rights unless explicitly denied. Credential owner ceilings remain enforced.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Query parameters (optional):**
- `tenantId`: Selected tenant; only platform admins may select another tenant. Does not expose another user's private items.

**Success response:** {deleted: true}.

**Field reference:** [`LibraryItem`](#libraryitem), [`GenerationConfiguration`](#generationconfiguration) in REST_API.md.

System items are shared, Tenant items are tenant defaults, User items are private to their owner (including from administrators). User ownership is derived from the principal. Only platform admins publish System items; tenant admins publish Tenant items in their own tenant. Duplicate to change visibility/ownership. CanEdit/CanDelete are response hints. IDs/timestamps are server assigned.

**Declared responses:** 200, 400, 401, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `id`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X DELETE "${SHARPAI_URL}/v1.0/model-configurations/${MODEL_CONFIGURATION_ID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="post-v1-0-model-configurations-id-duplicate"></a>

### POST /v1.0/model-configurations/{id}/duplicate

Duplicate a visible ModelConfiguration

**Authorization:** Authenticated principal; library operation permission plus ownership/publication rules. Human users have self-service rights unless explicitly denied. Credential owner ceilings remain enforced.

**Required inputs:** Name; omitted Content or Configuration inherits the source payload. Scope defaults to User.

**Query parameters (optional):**
- `tenantId`: Selected tenant; only platform admins may select another tenant. Does not expose another user's private items.

**Success response:** LibraryItem.

**Field reference:** [`LibraryItem`](#libraryitem), [`GenerationConfiguration`](#generationconfiguration) in REST_API.md.

System items are shared, Tenant items are tenant defaults, User items are private to their owner (including from administrators). User ownership is derived from the principal. Only platform admins publish System items; tenant admins publish Tenant items in their own tenant. Duplicate to change visibility/ownership. CanEdit/CanDelete are response hints. IDs/timestamps are server assigned.

**Server description:** Copies payload without changing the source. Scope defaults to User. Only platform administrators may publish System items, tenant administrators may publish Tenant items in their own tenant. User ownership is derived from the authenticated principal.

**Declared responses:** 200, 400, 401, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `id`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Example JSON body:**

```json
{
  "Name": "My copy",
  "Scope": "User"
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X POST "${SHARPAI_URL}/v1.0/model-configurations/${MODEL_CONFIGURATION_ID}/duplicate" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="get-v1-0-models-presets"></a>

### GET /v1.0/models/presets

List model presets

**Authorization:** Model Read.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** Preset list.

**Field reference:** [`CreatePresetRequest`](#createpresetrequest), [`ModelPreset`](#modelpreset) in REST_API.md.

Legacy server-wide presets are separate from scoped prompt/configuration libraries. Duplicate preset names return 409; base model must exist. Use the preset name as model for inference.

**Server description:** Paginated list of Modelfile-equivalent presets (system prompt, sampling defaults, stop, template override).

**Declared responses:** 200. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/models/presets" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="post-v1-0-models-presets"></a>

### POST /v1.0/models/presets

Create a model preset

**Authorization:** Model Write.

**Required inputs:** name and model; other preset fields are optional.

**Success response:** Saved ModelPreset.

**Field reference:** [`CreatePresetRequest`](#createpresetrequest), [`ModelPreset`](#modelpreset) in REST_API.md.

Legacy server-wide presets are separate from scoped prompt/configuration libraries. Duplicate preset names return 409; base model must exist. Use the preset name as model for inference.

**Server description:** Creates a Modelfile-equivalent preset. A request that names the preset as its model runs the base model with these defaults applied unless overridden.

**Declared responses:** 200, 400, 404, 409. Protected operations can also return 401; authorization checks can return 403.

**Example JSON body:**

```json
{
  "name": "{{presetName}}",
  "model": "{{model}}",
  "system": "Answer clearly.",
  "temperature": 0.6,
  "max_tokens": 2048,
  "top_p": 0.9,
  "stop": []
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X POST "${SHARPAI_URL}/v1.0/models/presets" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="get-v1-0-models-presets-name"></a>

### GET /v1.0/models/presets/{name}

Read a model preset

**Authorization:** Model Read.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** ModelPreset; 404 if not found.

**Field reference:** [`CreatePresetRequest`](#createpresetrequest), [`ModelPreset`](#modelpreset) in REST_API.md.

Legacy server-wide presets are separate from scoped prompt/configuration libraries. Duplicate preset names return 409; base model must exist. Use the preset name as model for inference.

**Server description:** Returns a single preset by name.

**Declared responses:** 200, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `name`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/models/presets/${PRESET_NAME}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="delete-v1-0-models-presets-name"></a>

### DELETE /v1.0/models/presets/{name}

Delete a model preset

**Authorization:** Model Delete.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** {deleted: true, name}.

**Field reference:** [`CreatePresetRequest`](#createpresetrequest), [`ModelPreset`](#modelpreset) in REST_API.md.

Legacy server-wide presets are separate from scoped prompt/configuration libraries. Duplicate preset names return 409; base model must exist. Use the preset name as model for inference.

**Server description:** Deletes a preset by name.

**Declared responses:** 200, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `name`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X DELETE "${SHARPAI_URL}/v1.0/models/presets/${PRESET_NAME}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-v1-0-system-prompts"></a>

### GET /v1.0/system-prompts

List visible system-prompts

**Authorization:** Authenticated principal; library operation permission plus ownership/publication rules. Human users have self-service rights unless explicitly denied. Credential owner ceilings remain enforced.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Query parameters (optional):**
- `pageNumber`: 1-based page; values below 1 clamp to 1.
- `pageSize`: 1 to 1000. Default 100 for general enumeration, 25 for request history.
- `order`: CreatedAscending or CreatedDescending; default CreatedDescending.
- `createdAfter`: Exclusive creation-time lower bound, UTC.
- `createdBefore`: Exclusive creation-time upper bound, UTC.
- `name`: Exact name; maximum 256 characters.
- `tenantId`: Selected tenant; only platform admins may select another tenant. Does not expose another user's private items.

**Success response:** EnumerationResult<LibraryItem>; visibility filtering occurs before counts and pagination.

**Field reference:** [`LibraryItem`](#libraryitem), [`GenerationConfiguration`](#generationconfiguration) in REST_API.md.

System items are shared, Tenant items are tenant defaults, User items are private to their owner (including from administrators). User ownership is derived from the principal. Only platform admins publish System items; tenant admins publish Tenant items in their own tenant. Duplicate to change visibility/ownership. CanEdit/CanDelete are response hints. IDs/timestamps are server assigned.

**Server description:** System defaults, selected tenant defaults and only the caller's private items. Query: tenantId (platform admins only for other tenants), name, pageNumber, pageSize. Ownership filtering precedes pagination.

**Declared responses:** 200, 400, 401, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/system-prompts" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="post-v1-0-system-prompts"></a>

### POST /v1.0/system-prompts

Create a SystemPrompt

**Authorization:** Authenticated principal; library operation permission plus ownership/publication rules. Human users have self-service rights unless explicitly denied. Credential owner ceilings remain enforced.

**Required inputs:** Name and Content; Scope defaults to User. Description is optional.

**Query parameters (optional):**
- `tenantId`: Selected tenant; only platform admins may select another tenant. Does not expose another user's private items.

**Success response:** LibraryItem.

**Field reference:** [`LibraryItem`](#libraryitem), [`GenerationConfiguration`](#generationconfiguration) in REST_API.md.

System items are shared, Tenant items are tenant defaults, User items are private to their owner (including from administrators). User ownership is derived from the principal. Only platform admins publish System items; tenant admins publish Tenant items in their own tenant. Duplicate to change visibility/ownership. CanEdit/CanDelete are response hints. IDs/timestamps are server assigned. Content is required, at most 1048576 characters. Configuration must be absent.

**Declared responses:** 200, 400, 401, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Example JSON body:**

```json
{
  "Name": "My saved item",
  "Description": "Reusable defaults",
  "Scope": "User",
  "Content": "Answer clearly and accurately."
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X POST "${SHARPAI_URL}/v1.0/system-prompts" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="get-v1-0-system-prompts-id"></a>

### GET /v1.0/system-prompts/{id}

Read a visible SystemPrompt

**Authorization:** Authenticated principal; library operation permission plus ownership/publication rules. Human users have self-service rights unless explicitly denied. Credential owner ceilings remain enforced.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Query parameters (optional):**
- `tenantId`: Selected tenant; only platform admins may select another tenant. Does not expose another user's private items.

**Success response:** LibraryItem.

**Field reference:** [`LibraryItem`](#libraryitem), [`GenerationConfiguration`](#generationconfiguration) in REST_API.md.

System items are shared, Tenant items are tenant defaults, User items are private to their owner (including from administrators). User ownership is derived from the principal. Only platform admins publish System items; tenant admins publish Tenant items in their own tenant. Duplicate to change visibility/ownership. CanEdit/CanDelete are response hints. IDs/timestamps are server assigned.

**Declared responses:** 200, 400, 401, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `id`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/system-prompts/${SYSTEM_PROMPT_ID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="put-v1-0-system-prompts-id"></a>

### PUT /v1.0/system-prompts/{id}

Update a SystemPrompt

**Authorization:** Authenticated principal; library operation permission plus ownership/publication rules. Human users have self-service rights unless explicitly denied. Credential owner ceilings remain enforced.

**Required inputs:** Name and Content; Scope defaults to User. Description is optional.

**Query parameters (optional):**
- `tenantId`: Selected tenant; only platform admins may select another tenant. Does not expose another user's private items.

**Success response:** LibraryItem.

**Field reference:** [`LibraryItem`](#libraryitem), [`GenerationConfiguration`](#generationconfiguration) in REST_API.md.

System items are shared, Tenant items are tenant defaults, User items are private to their owner (including from administrators). User ownership is derived from the principal. Only platform admins publish System items; tenant admins publish Tenant items in their own tenant. Duplicate to change visibility/ownership. CanEdit/CanDelete are response hints. IDs/timestamps are server assigned. Content is required, at most 1048576 characters. Configuration must be absent.

**Declared responses:** 200, 400, 401, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `id`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Example JSON body:**

```json
{
  "Name": "My saved item",
  "Description": "Reusable defaults",
  "Scope": "User",
  "Content": "Answer clearly and accurately."
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X PUT "${SHARPAI_URL}/v1.0/system-prompts/${SYSTEM_PROMPT_ID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="delete-v1-0-system-prompts-id"></a>

### DELETE /v1.0/system-prompts/{id}

Delete a SystemPrompt

**Authorization:** Authenticated principal; library operation permission plus ownership/publication rules. Human users have self-service rights unless explicitly denied. Credential owner ceilings remain enforced.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Query parameters (optional):**
- `tenantId`: Selected tenant; only platform admins may select another tenant. Does not expose another user's private items.

**Success response:** {deleted: true}.

**Field reference:** [`LibraryItem`](#libraryitem), [`GenerationConfiguration`](#generationconfiguration) in REST_API.md.

System items are shared, Tenant items are tenant defaults, User items are private to their owner (including from administrators). User ownership is derived from the principal. Only platform admins publish System items; tenant admins publish Tenant items in their own tenant. Duplicate to change visibility/ownership. CanEdit/CanDelete are response hints. IDs/timestamps are server assigned.

**Declared responses:** 200, 400, 401, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `id`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X DELETE "${SHARPAI_URL}/v1.0/system-prompts/${SYSTEM_PROMPT_ID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="post-v1-0-system-prompts-id-duplicate"></a>

### POST /v1.0/system-prompts/{id}/duplicate

Duplicate a visible SystemPrompt

**Authorization:** Authenticated principal; library operation permission plus ownership/publication rules. Human users have self-service rights unless explicitly denied. Credential owner ceilings remain enforced.

**Required inputs:** Name; omitted Content or Configuration inherits the source payload. Scope defaults to User.

**Query parameters (optional):**
- `tenantId`: Selected tenant; only platform admins may select another tenant. Does not expose another user's private items.

**Success response:** LibraryItem.

**Field reference:** [`LibraryItem`](#libraryitem), [`GenerationConfiguration`](#generationconfiguration) in REST_API.md.

System items are shared, Tenant items are tenant defaults, User items are private to their owner (including from administrators). User ownership is derived from the principal. Only platform admins publish System items; tenant admins publish Tenant items in their own tenant. Duplicate to change visibility/ownership. CanEdit/CanDelete are response hints. IDs/timestamps are server assigned.

**Server description:** Copies payload without changing the source. Scope defaults to User. Only platform administrators may publish System items, tenant administrators may publish Tenant items in their own tenant. User ownership is derived from the authenticated principal.

**Declared responses:** 200, 400, 401, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `id`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Example JSON body:**

```json
{
  "Name": "My copy",
  "Scope": "User"
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X POST "${SHARPAI_URL}/v1.0/system-prompts/${SYSTEM_PROMPT_ID}/duplicate" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="get-v1-0-tenants"></a>

### GET /v1.0/tenants

List tenants

**Authorization:** Platform administrator; create/delete additionally require Admin Admin.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Query parameters (optional):**
- `pageNumber`: 1-based page; values below 1 clamp to 1.
- `pageSize`: 1 to 1000. Default 100 for general enumeration, 25 for request history.
- `order`: CreatedAscending or CreatedDescending; default CreatedDescending.
- `createdAfter`: Exclusive creation-time lower bound, UTC.
- `createdBefore`: Exclusive creation-time upper bound, UTC.

**Success response:** EnumerationResult<Tenant>.

**Field reference:** [`Tenant`](#tenant) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization.

**Server description:** Paginated list of tenants. Requires platform-administrator (Admin) privileges.

**Declared responses:** 200, 403. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/tenants" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="post-v1-0-tenants"></a>

### POST /v1.0/tenants

Create a tenant

**Authorization:** Platform administrator; create/delete additionally require Admin Admin.

**Required inputs:** Name; Active is optional for updates. Credential ExpiresUtc changes expiry; ClearExpiration=true removes it.

**Success response:** EnumerationResult<Tenant>.

**Field reference:** [`Tenant`](#tenant) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization.

**Server description:** Creates a tenant. Requires platform-administrator (Admin) privileges.

**Declared responses:** 200, 400, 403. Protected operations can also return 401; authorization checks can return 403.

**Example JSON body:**

```json
{
  "Name": "Example tenant"
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X POST "${SHARPAI_URL}/v1.0/tenants" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="get-v1-0-tenants-tenantguid"></a>

### GET /v1.0/tenants/{tenantGuid}

Read a tenant

**Authorization:** Tenant Read within the URL tenant; other tenants require platform administrator.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** Tenant.

**Field reference:** [`Tenant`](#tenant) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization.

**Server description:** Returns a single tenant. Callers are constrained to their own tenant unless they are platform administrators.

**Declared responses:** 200, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="put-v1-0-tenants-tenantguid"></a>

### PUT /v1.0/tenants/{tenantGuid}

Update a tenant

**Authorization:** Platform administrator; create/delete additionally require Admin Admin.

**Required inputs:** Name; Active is optional for updates. Credential ExpiresUtc changes expiry; ClearExpiration=true removes it.

**Success response:** Tenant.

**Field reference:** [`Tenant`](#tenant) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization.

**Declared responses:** 200, 400, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Example JSON body:**

```json
{
  "Name": "Example tenant",
  "Active": true
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X PUT "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="delete-v1-0-tenants-tenantguid"></a>

### DELETE /v1.0/tenants/{tenantGuid}

Delete a tenant

**Authorization:** Platform administrator; create/delete additionally require Admin Admin.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** {deleted: true} with the applicable tenantId/userId/credentialId/roleId/assignmentId, or {revoked: true} for sessions.

**Field reference:** [`Tenant`](#tenant) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization.

**Server description:** Deletes a tenant. Protected tenants cannot be deleted. Requires platform-administrator (Admin) privileges.

**Declared responses:** 200, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X DELETE "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="post-v1-0-tenants-tenantguid-assignments"></a>

### POST /v1.0/tenants/{tenantGuid}/assignments

Assign a role to a user

**Authorization:** Assignment Admin within the URL tenant; other tenants require platform administrator.

**Required inputs:** UserGuid and RoleGuid or built-in RoleName; ResourceGuid is required when ResourceScope is Resource.

**Success response:** EnumerationResult<UserRoleAssignment>.

**Field reference:** [`UserRoleAssignment`](#userroleassignment), [`CreateAssignmentRequest`](#createassignmentrequest) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. Supply RoleGuid or a built-in RoleName; role must be active and available in this tenant. Resource scope requires ResourceGuid. ResourceScope is Tenant or Resource; owner ceilings still apply.

**Server description:** Grants a role to a user at tenant or resource scope. The role may be referenced by GUID or name.

**Declared responses:** 200, 400, 403. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Example JSON body:**

```json
{
  "UserGuid": "{{userGuid}}",
  "RoleGuid": "{{roleGuid}}",
  "ResourceScope": "Tenant",
  "ResourceGuid": null,
  "InheritsToChildren": true
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X POST "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/assignments" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="delete-v1-0-tenants-tenantguid-assignments-assignmentguid"></a>

### DELETE /v1.0/tenants/{tenantGuid}/assignments/{assignmentGuid}

Revoke a role assignment

**Authorization:** Assignment Admin within the URL tenant; other tenants require platform administrator.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** {deleted: true} with the applicable tenantId/userId/credentialId/roleId/assignmentId, or {revoked: true} for sessions.

**Field reference:** [`UserRoleAssignment`](#userroleassignment), [`CreateAssignmentRequest`](#createassignmentrequest) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. Supply RoleGuid or a built-in RoleName; role must be active and available in this tenant. Resource scope requires ResourceGuid. ResourceScope is Tenant or Resource; owner ceilings still apply.

**Server description:** Removes a role assignment from a user.

**Declared responses:** 200, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`, `assignmentGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X DELETE "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/assignments/${ASSIGNMENT_GUID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-v1-0-tenants-tenantguid-credentials"></a>

### GET /v1.0/tenants/{tenantGuid}/credentials

List credentials

**Authorization:** Credential Read within the URL tenant; other tenants require platform administrator.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Query parameters (optional):**
- `pageNumber`: 1-based page; values below 1 clamp to 1.
- `pageSize`: 1 to 1000. Default 100 for general enumeration, 25 for request history.
- `order`: CreatedAscending or CreatedDescending; default CreatedDescending.
- `createdAfter`: Exclusive creation-time lower bound, UTC.
- `createdBefore`: Exclusive creation-time upper bound, UTC.

**Success response:** EnumerationResult<Credential>.

**Field reference:** [`Credential`](#credential) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. Ordinary reads omit usable token material. A credential belongs to a user in this tenant and is constrained by the owner's effective permissions.

**Server description:** Paginated, tenant-scoped list of credentials. Secret hashes are redacted.

**Declared responses:** 200, 403. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/credentials" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="post-v1-0-tenants-tenantguid-credentials"></a>

### POST /v1.0/tenants/{tenantGuid}/credentials

Create a credential

**Authorization:** Credential Admin within the URL tenant; other tenants require platform administrator.

**Required inputs:** UserGuid; Name and ExpiresUtc are optional.

**Success response:** {guid, userId, tenantId, name, token, tokenType: "Bearer", expiresUtc}; Cache-Control: no-store.

**Field reference:** [`Credential`](#credential), [`CreateCredentialRequest`](#createcredentialrequest) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. Ordinary reads omit usable token material. A credential belongs to a user in this tenant and is constrained by the owner's effective permissions.

**Server description:** Creates a revocable bearer credential and returns token ONCE. Send Authorization: Bearer token. Its digest is stored in the database; an encrypted copy supports authorized reveal.

**Declared responses:** 200, 400, 403. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Example JSON body:**

```json
{
  "UserGuid": "{{userGuid}}",
  "Name": "Example application",
  "ExpiresUtc": null
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X POST "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/credentials" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="get-v1-0-tenants-tenantguid-credentials-credentialguid"></a>

### GET /v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}

Read a credential

**Authorization:** Credential Read within the URL tenant; other tenants require platform administrator.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** Credential.

**Field reference:** [`Credential`](#credential) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. Ordinary reads omit usable token material. A credential belongs to a user in this tenant and is constrained by the owner's effective permissions.

**Server description:** Returns a single credential (secret hash redacted).

**Declared responses:** 200, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`, `credentialGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/credentials/${CREDENTIAL_GUID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="put-v1-0-tenants-tenantguid-credentials-credentialguid"></a>

### PUT /v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}

Update credential metadata, expiration, or active status

**Authorization:** Credential Admin within the URL tenant; other tenants require platform administrator.

**Required inputs:** Name; Active is optional for updates. Credential ExpiresUtc changes expiry; ClearExpiration=true removes it.

**Success response:** Credential.

**Field reference:** [`Credential`](#credential) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. Ordinary reads omit usable token material. A credential belongs to a user in this tenant and is constrained by the owner's effective permissions.

**Declared responses:** 200, 400, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`, `credentialGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Example JSON body:**

```json
{
  "Name": "Example application",
  "Active": true,
  "ExpiresUtc": null,
  "ClearExpiration": false
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X PUT "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/credentials/${CREDENTIAL_GUID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="delete-v1-0-tenants-tenantguid-credentials-credentialguid"></a>

### DELETE /v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}

Delete a credential

**Authorization:** Credential Admin within the URL tenant; other tenants require platform administrator.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** {deleted: true} with the applicable tenantId/userId/credentialId/roleId/assignmentId, or {revoked: true} for sessions.

**Field reference:** [`Credential`](#credential) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. Ordinary reads omit usable token material. A credential belongs to a user in this tenant and is constrained by the owner's effective permissions.

**Server description:** Deletes a credential.

**Declared responses:** 200, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`, `credentialGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X DELETE "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/credentials/${CREDENTIAL_GUID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-v1-0-tenants-tenantguid-credentials-credentialguid-assignments"></a>

### GET /v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}/assignments

List a credential's scope assignments

**Authorization:** Assignment Read.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** CredentialScopeAssignment[] (plain array).

**Field reference:** [`CredentialScopeAssignment`](#credentialscopeassignment) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. Ordinary reads omit usable token material. A credential belongs to a user in this tenant and is constrained by the owner's effective permissions. Assignments scope credential access and cannot exceed the owning user's rights.

**Declared responses:** 200, 400, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`, `credentialGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/credentials/${CREDENTIAL_GUID}/assignments" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="post-v1-0-tenants-tenantguid-credentials-credentialguid-assignments"></a>

### POST /v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}/assignments

Grant a role-scoped credential assignment

**Authorization:** Assignment Admin.

**Required inputs:** RoleGuid or built-in RoleName; ResourceGuid is required when ResourceScope is Resource.

**Success response:** CredentialScopeAssignment.

**Field reference:** [`CredentialScopeAssignment`](#credentialscopeassignment) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. Ordinary reads omit usable token material. A credential belongs to a user in this tenant and is constrained by the owner's effective permissions. Assignments scope credential access and cannot exceed the owning user's rights.

**Declared responses:** 200, 400, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`, `credentialGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Example JSON body:**

```json
{
  "RoleGuid": "{{roleGuid}}",
  "ResourceScope": "Tenant",
  "ResourceGuid": null,
  "InheritsToChildren": true
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X POST "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/credentials/${CREDENTIAL_GUID}/assignments" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="delete-v1-0-tenants-tenantguid-credentials-credentialguid-assignments-assignmentguid"></a>

### DELETE /v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}/assignments/{assignmentGuid}

Revoke a credential scope assignment

**Authorization:** Assignment Admin.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** {deleted: true}.

**Field reference:** [`CredentialScopeAssignment`](#credentialscopeassignment) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. Ordinary reads omit usable token material. A credential belongs to a user in this tenant and is constrained by the owner's effective permissions. Assignments scope credential access and cannot exceed the owning user's rights.

**Declared responses:** 200, 400, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`, `credentialGuid`, `assignmentGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X DELETE "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/credentials/${CREDENTIAL_GUID}/assignments/${ASSIGNMENT_GUID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-v1-0-tenants-tenantguid-credentials-credentialguid-permissions"></a>

### GET /v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}/permissions

Inspect a credential's effective permissions

**Authorization:** Same principal inspecting itself, otherwise Admin Admin; tenant boundary always enforced.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** {principalType, principalId, tenantId, count, permissions: [{resourceType, operation, effect, scope, resourceGuid, inheritsToChildren}]}.

**Field reference:** [`EffectivePermission`](#effectivepermission) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. Ordinary reads omit usable token material. A credential belongs to a user in this tenant and is constrained by the owner's effective permissions.

**Server description:** Returns the computed effective permission set for a credential within a tenant. Requires Admin on the Admin resource, or the credential's owner reading its own permissions.

**Declared responses:** 200, 403. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`, `credentialGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/credentials/${CREDENTIAL_GUID}/permissions" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-v1-0-tenants-tenantguid-credentials-credentialguid-token"></a>

### GET /v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}/token

Reveal a credential bearer token

**Authorization:** Credential Admin for this tenant and target credential.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** {token, tokenType: "Bearer"}; Cache-Control: no-store.

**Field reference:** [`Credential`](#credential) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. Ordinary reads omit usable token material. A credential belongs to a user in this tenant and is constrained by the owner's effective permissions. Reveal decrypts a retained token and is audited; legacy hash-only credentials return 409. Rotate immediately invalidates the old token, preserves assignments/status/expiry and is audited. Protected credentials cannot rotate. Rotation does not activate an expired or inactive credential.

**Server description:** Requires Credential Admin permission within the target tenant. Returns the encrypted-at-rest token after decryption, with Cache-Control: no-store. Ordinary credential reads and lists never return tokens. Reveal is audited and the response token is redacted from request history. Legacy hash-only credentials return 409 and must be regenerated.

**Declared responses:** 200, 401, 403, 404, 409. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`, `credentialGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/credentials/${CREDENTIAL_GUID}/token" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="post-v1-0-tenants-tenantguid-credentials-credentialguid-token-rotate"></a>

### POST /v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}/token/rotate

Regenerate a credential bearer token

**Authorization:** Credential Admin for this tenant and target credential.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** {token, tokenType: "Bearer"}; Cache-Control: no-store.

**Field reference:** [`Credential`](#credential), [`CreateCredentialRequest`](#createcredentialrequest) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. Ordinary reads omit usable token material. A credential belongs to a user in this tenant and is constrained by the owner's effective permissions. Reveal decrypts a retained token and is audited; legacy hash-only credentials return 409. Rotate immediately invalidates the old token, preserves assignments/status/expiry and is audited. Protected credentials cannot rotate. Rotation does not activate an expired or inactive credential.

**Server description:** Requires Credential Admin permission. Immediately invalidates the previous token, preserves the credential's scopes, active flag and expiry, and retains the replacement encrypted at rest. Reveal later through GET .../token. Update integrations using the old token. Protected credentials return 403.

**Declared responses:** 200, 401, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`, `credentialGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X POST "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/credentials/${CREDENTIAL_GUID}/token/rotate" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-v1-0-tenants-tenantguid-permissions"></a>

### GET /v1.0/tenants/{tenantGuid}/permissions

List permissions

**Authorization:** Permission Read within the URL tenant; other tenants require platform administrator.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Query parameters (optional):**
- `pageNumber`: 1-based page; values below 1 clamp to 1.
- `pageSize`: 1 to 1000. Default 100 for general enumeration, 25 for request history.
- `order`: CreatedAscending or CreatedDescending; default CreatedDescending.
- `createdAfter`: Exclusive creation-time lower bound, UTC.
- `createdBefore`: Exclusive creation-time upper bound, UTC.

**Success response:** EnumerationResult<Permission>.

**Field reference:** [`Permission`](#permission) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. ResourceTypes and OperationTypes must be nonempty. Effect is Permit or Deny; operations must be supported enum values. Explicit deny takes precedence for ordinary principals.

**Server description:** Paginated list of the tenant's permissions plus globally-visible built-in permissions.

**Declared responses:** 200, 403. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/permissions" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="post-v1-0-tenants-tenantguid-permissions"></a>

### POST /v1.0/tenants/{tenantGuid}/permissions

Create a permission

**Authorization:** Permission Admin within the URL tenant; other tenants require platform administrator.

**Required inputs:** Nonempty ResourceTypes and OperationTypes arrays; Effect is Permit (default) or Deny. Name and Active are optional; PUT replaces the permission fields.

**Success response:** EnumerationResult<Permission>.

**Field reference:** [`Permission`](#permission) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. ResourceTypes and OperationTypes must be nonempty. Effect is Permit or Deny; operations must be supported enum values. Explicit deny takes precedence for ordinary principals.

**Server description:** Creates a tenant-scoped permission (Permit or Deny) over resource types and operations.

**Declared responses:** 200, 400, 403. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Example JSON body:**

```json
{
  "Name": "Read models",
  "ResourceTypes": [
    "Model"
  ],
  "OperationTypes": [
    "Read"
  ],
  "Effect": "Permit",
  "Active": true
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X POST "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/permissions" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="put-v1-0-tenants-tenantguid-permissions-permissionguid"></a>

### PUT /v1.0/tenants/{tenantGuid}/permissions/{permissionGuid}

Update a custom permission

**Authorization:** Permission Admin within the URL tenant; other tenants require platform administrator.

**Required inputs:** Nonempty ResourceTypes and OperationTypes arrays; Effect is Permit (default) or Deny. Name and Active are optional; PUT replaces the permission fields.

**Success response:** Permission.

**Field reference:** [`Permission`](#permission) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. ResourceTypes and OperationTypes must be nonempty. Effect is Permit or Deny; operations must be supported enum values. Explicit deny takes precedence for ordinary principals.

**Declared responses:** 200, 400, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`, `permissionGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Example JSON body:**

```json
{
  "Name": "Read models",
  "ResourceTypes": [
    "Model"
  ],
  "OperationTypes": [
    "Read"
  ],
  "Effect": "Permit",
  "Active": true
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X PUT "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/permissions/${PERMISSION_GUID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="delete-v1-0-tenants-tenantguid-permissions-permissionguid"></a>

### DELETE /v1.0/tenants/{tenantGuid}/permissions/{permissionGuid}

Delete a custom permission

**Authorization:** Permission Admin within the URL tenant; other tenants require platform administrator.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** {deleted: true} with the applicable tenantId/userId/credentialId/roleId/assignmentId, or {revoked: true} for sessions.

**Field reference:** [`Permission`](#permission) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. ResourceTypes and OperationTypes must be nonempty. Effect is Permit or Deny; operations must be supported enum values. Explicit deny takes precedence for ordinary principals.

**Declared responses:** 200, 400, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`, `permissionGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X DELETE "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/permissions/${PERMISSION_GUID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-v1-0-tenants-tenantguid-roles"></a>

### GET /v1.0/tenants/{tenantGuid}/roles

List roles

**Authorization:** Role Read within the URL tenant; other tenants require platform administrator.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Query parameters (optional):**
- `pageNumber`: 1-based page; values below 1 clamp to 1.
- `pageSize`: 1 to 1000. Default 100 for general enumeration, 25 for request history.
- `order`: CreatedAscending or CreatedDescending; default CreatedDescending.
- `createdAfter`: Exclusive creation-time lower bound, UTC.
- `createdBefore`: Exclusive creation-time upper bound, UTC.

**Success response:** EnumerationResult<UserRole>.

**Field reference:** [`UserRole`](#userrole) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization.

**Server description:** Paginated list of the tenant's custom roles plus the globally-visible built-in roles.

**Declared responses:** 200, 403. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/roles" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="post-v1-0-tenants-tenantguid-roles"></a>

### POST /v1.0/tenants/{tenantGuid}/roles

Create a custom role

**Authorization:** Role Admin within the URL tenant; other tenants require platform administrator.

**Required inputs:** Name; Active is optional for updates. Credential ExpiresUtc changes expiry; ClearExpiration=true removes it.

**Success response:** EnumerationResult<UserRole>.

**Field reference:** [`UserRole`](#userrole) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization.

**Server description:** Creates a tenant-scoped custom role. Built-in roles are immutable; clone by creating a custom role and mapping permissions.

**Declared responses:** 200, 400, 403. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Example JSON body:**

```json
{
  "Name": "Example role"
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X POST "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/roles" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="put-v1-0-tenants-tenantguid-roles-roleguid"></a>

### PUT /v1.0/tenants/{tenantGuid}/roles/{roleGuid}

Update a custom role

**Authorization:** Role Admin within the URL tenant; other tenants require platform administrator.

**Required inputs:** Name; Active is optional for updates. Credential ExpiresUtc changes expiry; ClearExpiration=true removes it.

**Success response:** UserRole.

**Field reference:** [`UserRole`](#userrole) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization.

**Declared responses:** 200, 400, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`, `roleGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Example JSON body:**

```json
{
  "Name": "Example role",
  "Active": true
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X PUT "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/roles/${ROLE_GUID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="delete-v1-0-tenants-tenantguid-roles-roleguid"></a>

### DELETE /v1.0/tenants/{tenantGuid}/roles/{roleGuid}

Delete a custom role

**Authorization:** Role Admin within the URL tenant; other tenants require platform administrator.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** {deleted: true} with the applicable tenantId/userId/credentialId/roleId/assignmentId, or {revoked: true} for sessions.

**Field reference:** [`UserRole`](#userrole) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization.

**Server description:** Deletes a tenant-scoped custom role. Built-in and protected roles cannot be deleted.

**Declared responses:** 200, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`, `roleGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X DELETE "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/roles/${ROLE_GUID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-v1-0-tenants-tenantguid-roles-roleguid-permissions"></a>

### GET /v1.0/tenants/{tenantGuid}/roles/{roleGuid}/permissions

List a role's mapped permissions

**Authorization:** Role Read within the URL tenant; other tenants require platform administrator.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** Permission[] (plain array, not a page).

**Field reference:** [`UserRole`](#userrole), [`Permission`](#permission), [`RolePermissionMap`](#rolepermissionmap) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. Built-in roles are visible but immutable. Mapped permissions must be active and either global or in this tenant.

**Declared responses:** 200, 400, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`, `roleGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/roles/${ROLE_GUID}/permissions" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="post-v1-0-tenants-tenantguid-roles-roleguid-permissions-permissionguid"></a>

### POST /v1.0/tenants/{tenantGuid}/roles/{roleGuid}/permissions/{permissionGuid}

Map a permission to a role

**Authorization:** Role Admin within the URL tenant; other tenants require platform administrator.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** RolePermissionMap.

**Field reference:** [`UserRole`](#userrole), [`Permission`](#permission), [`RolePermissionMap`](#rolepermissionmap) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. Built-in roles are visible but immutable. Mapped permissions must be active and either global or in this tenant.

**Server description:** Associates a permission with a custom role. Built-in roles are immutable.

**Declared responses:** 200, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`, `roleGuid`, `permissionGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X POST "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/roles/${ROLE_GUID}/permissions/${PERMISSION_GUID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="delete-v1-0-tenants-tenantguid-roles-roleguid-permissions-permissionguid"></a>

### DELETE /v1.0/tenants/{tenantGuid}/roles/{roleGuid}/permissions/{permissionGuid}

Remove a custom role's permission mapping

**Authorization:** Role Admin within the URL tenant; other tenants require platform administrator.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** {deleted: true}.

**Field reference:** [`UserRole`](#userrole), [`Permission`](#permission), [`RolePermissionMap`](#rolepermissionmap) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. Built-in roles are visible but immutable. Mapped permissions must be active and either global or in this tenant.

**Declared responses:** 200, 400, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`, `roleGuid`, `permissionGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X DELETE "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/roles/${ROLE_GUID}/permissions/${PERMISSION_GUID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-v1-0-tenants-tenantguid-sessions"></a>

### GET /v1.0/tenants/{tenantGuid}/sessions

List tenant sessions without token material

**Authorization:** Session Read within the URL tenant; other tenants require platform administrator.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Query parameters (optional):**
- `pageNumber`: 1-based page; values below 1 clamp to 1.
- `pageSize`: 1 to 1000. Default 100 for general enumeration, 25 for request history.
- `order`: CreatedAscending or CreatedDescending; default CreatedDescending.
- `createdAfter`: Exclusive creation-time lower bound, UTC.
- `createdBefore`: Exclusive creation-time upper bound, UTC.

**Success response:** EnumerationResult<AuthSession>.

**Field reference:** [`AuthSession`](#authsession) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization.

**Declared responses:** 200, 400, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/sessions" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="delete-v1-0-tenants-tenantguid-sessions-sessionguid"></a>

### DELETE /v1.0/tenants/{tenantGuid}/sessions/{sessionGuid}

Revoke a tenant session

**Authorization:** Session Admin within the URL tenant; other tenants require platform administrator.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** {deleted: true} with the applicable tenantId/userId/credentialId/roleId/assignmentId, or {revoked: true} for sessions.

**Field reference:** [`AuthSession`](#authsession) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization.

**Declared responses:** 200, 400, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`, `sessionGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X DELETE "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/sessions/${SESSION_GUID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-v1-0-tenants-tenantguid-users"></a>

### GET /v1.0/tenants/{tenantGuid}/users

List users

**Authorization:** User Read within the URL tenant; other tenants require platform administrator.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Query parameters (optional):**
- `pageNumber`: 1-based page; values below 1 clamp to 1.
- `pageSize`: 1 to 1000. Default 100 for general enumeration, 25 for request history.
- `order`: CreatedAscending or CreatedDescending; default CreatedDescending.
- `createdAfter`: Exclusive creation-time lower bound, UTC.
- `createdBefore`: Exclusive creation-time upper bound, UTC.

**Success response:** EnumerationResult<User>.

**Field reference:** [`User`](#user) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. Password hashes are redacted. Platform-admin user changes require a platform administrator. Deleting a user also removes owned credentials and role assignments.

**Server description:** Paginated, tenant-scoped list of users. Password hashes are redacted.

**Declared responses:** 200, 403. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/users" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="post-v1-0-tenants-tenantguid-users"></a>

### POST /v1.0/tenants/{tenantGuid}/users

Create a user

**Authorization:** User Admin within the URL tenant; other tenants require platform administrator.

**Required inputs:** Email and Password; other profile/admin fields are optional.

**Success response:** EnumerationResult<User>.

**Field reference:** [`User`](#user), [`CreateUserRequest`](#createuserrequest) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. Password hashes are redacted. Platform-admin user changes require a platform administrator. Deleting a user also removes owned credentials and role assignments.

**Server description:** Creates a tenant user. The plaintext password is hashed server-side and never returned.

**Declared responses:** 200, 400, 403. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Example JSON body:**

```json
{
  "Email": "{{newUserEmail}}",
  "Password": "{{newUserPassword}}",
  "FirstName": "Example",
  "LastName": "User",
  "IsAdmin": false,
  "IsTenantAdmin": false
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X POST "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/users" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="get-v1-0-tenants-tenantguid-users-userguid"></a>

### GET /v1.0/tenants/{tenantGuid}/users/{userGuid}

Read a user

**Authorization:** User Read within the URL tenant; other tenants require platform administrator.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** User.

**Field reference:** [`User`](#user) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. Password hashes are redacted. Platform-admin user changes require a platform administrator. Deleting a user also removes owned credentials and role assignments.

**Server description:** Returns a single user (password hash redacted).

**Declared responses:** 200, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`, `userGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/users/${USER_GUID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="put-v1-0-tenants-tenantguid-users-userguid"></a>

### PUT /v1.0/tenants/{tenantGuid}/users/{userGuid}

Update a user

**Authorization:** User Admin within the URL tenant; other tenants require platform administrator.

**Required inputs:** Email; optional FirstName, LastName, IsAdmin, IsTenantAdmin, Active.

**Success response:** User.

**Field reference:** [`User`](#user) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. Password hashes are redacted. Platform-admin user changes require a platform administrator. Deleting a user also removes owned credentials and role assignments.

**Declared responses:** 200, 400, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`, `userGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Example JSON body:**

```json
{
  "Email": "{{newUserEmail}}",
  "FirstName": "Example",
  "LastName": "User",
  "Active": true,
  "IsAdmin": false,
  "IsTenantAdmin": false
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X PUT "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/users/${USER_GUID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="delete-v1-0-tenants-tenantguid-users-userguid"></a>

### DELETE /v1.0/tenants/{tenantGuid}/users/{userGuid}

Delete a user

**Authorization:** User Admin within the URL tenant; other tenants require platform administrator.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** {deleted: true} with the applicable tenantId/userId/credentialId/roleId/assignmentId, or {revoked: true} for sessions.

**Field reference:** [`User`](#user) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. Password hashes are redacted. Platform-admin user changes require a platform administrator. Deleting a user also removes owned credentials and role assignments.

**Server description:** Deletes a user and cascades to their credentials and role assignments. Protected users cannot be deleted.

**Declared responses:** 200, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`, `userGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X DELETE "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/users/${USER_GUID}" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-v1-0-tenants-tenantguid-users-userguid-assignments"></a>

### GET /v1.0/tenants/{tenantGuid}/users/{userGuid}/assignments

List a user's role assignments

**Authorization:** Assignment Read.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Query parameters (optional):**
- `pageNumber`: 1-based page; values below 1 clamp to 1.
- `pageSize`: 1 to 1000. Default 100 for general enumeration, 25 for request history.
- `order`: CreatedAscending or CreatedDescending; default CreatedDescending.
- `createdAfter`: Exclusive creation-time lower bound, UTC.
- `createdBefore`: Exclusive creation-time upper bound, UTC.

**Success response:** EnumerationResult<UserRoleAssignment>; RoleName is populated when RoleGuid is present.

**Field reference:** [`UserRoleAssignment`](#userroleassignment) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. Password hashes are redacted. Platform-admin user changes require a platform administrator. Deleting a user also removes owned credentials and role assignments.

**Server description:** Paginated list of a user's role assignments within the tenant.

**Declared responses:** 200, 403. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`, `userGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/users/${USER_GUID}/assignments" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="put-v1-0-tenants-tenantguid-users-userguid-password"></a>

### PUT /v1.0/tenants/{tenantGuid}/users/{userGuid}/password

Reset a user's password and revoke sessions

**Authorization:** User Admin within the URL tenant; other tenants require platform administrator.

**Required inputs:** Password; must be nonblank.

**Success response:** {updated: true}.

**Field reference:** [`User`](#user) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. Password hashes are redacted. Platform-admin user changes require a platform administrator. Deleting a user also removes owned credentials and role assignments. Resets the password and revokes all user sessions, including later pages. Resetting the protected administrator password requires a platform administrator.

**Declared responses:** 200, 400, 403, 404. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`, `userGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Example JSON body:**

```json
{
  "Password": "{{newUserPassword}}"
}
```

**Request:** Save the JSON body above as `request.json` and replace collection placeholders with your values.

```sh
curl -X PUT "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/users/${USER_GUID}/password" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}" \
  -H "Content-Type: application/json" --data-binary @request.json
```

<a id="get-v1-0-tenants-tenantguid-users-userguid-permissions"></a>

### GET /v1.0/tenants/{tenantGuid}/users/{userGuid}/permissions

Inspect a user's effective permissions

**Authorization:** Same principal inspecting itself, otherwise Admin Admin; tenant boundary always enforced.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** {principalType, principalId, tenantId, count, permissions: [{resourceType, operation, effect, scope, resourceGuid, inheritsToChildren}]}.

**Field reference:** [`EffectivePermission`](#effectivepermission) in REST_API.md.

Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization. Password hashes are redacted. Platform-admin user changes require a platform administrator. Deleting a user also removes owned credentials and role assignments.

**Server description:** Returns the computed effective permission set for a user within a tenant, as (resourceType, operation, effect, scope, resourceGuid) grants. Requires Admin on the Admin resource, or the principal reading their own permissions.

**Declared responses:** 200, 403. Protected operations can also return 401; authorization checks can return 403.

**Path parameters (required):** `tenantGuid`, `userGuid`. URL-encode each value. Use the owning resource's identifier; model inspection uses a GUID, while preset name uses a registered preset name.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/tenants/${TENANT_GUID}/users/${USER_GUID}/permissions" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="get-v1-0-token"></a>

### GET /v1.0/token

Read the current session

**Authorization:** A valid user session bearer token; API credentials do not identify a session.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** {sessionId, tenantId, userId, createdUtc, expiresUtc}.

No JSON body. Login credentials are HTTP headers. DELETE immediately revokes the current session. Invalid login/session returns 401; missing login headers return 400.

**Server description:** Returns details of the session referenced by the supplied bearer token (Authorization: Bearer).

**Declared responses:** 200, 401. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X GET "${SHARPAI_URL}/v1.0/token" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

<a id="post-v1-0-token"></a>

### POST /v1.0/token

Log in and create a session token

**Authorization:** Anonymous login with x-email, x-password and optional x-tenant-guid (default tenant when omitted).

**Required inputs:** x-email and x-password headers. Optional x-tenant-guid selects the login tenant; no JSON body.

**Success response:** {token, sessionId, tenantId, userId, expiresUtc}.

No JSON body. Login credentials are HTTP headers. DELETE immediately revokes the current session. Invalid login/session returns 401; missing login headers return 400.

**Server description:** Validates an email/password login and, on success, returns an opaque bearer token that references a revocable server-side session. Supply credentials in the x-email, x-password, and optional x-tenant-guid headers (the 'default' tenant is used when omitted). This endpoint is anonymous so it is reachable without a prior credential.

**Declared responses:** 200, 400, 401. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X POST "${SHARPAI_URL}/v1.0/token" \
  -H "x-email: ${SHARPAI_EMAIL}" \
  -H "x-password: ${SHARPAI_PASSWORD}" \
  -H "x-tenant-guid: ${SHARPAI_TENANT_GUID:-default}"
```

<a id="delete-v1-0-token"></a>

### DELETE /v1.0/token

Revoke the current session

**Authorization:** A valid user session bearer token; API credentials do not identify a session.

**Required inputs:** No JSON body. Supply all named path parameters and authentication when required.

**Success response:** {revoked: true, sessionId}.

No JSON body. Login credentials are HTTP headers. DELETE immediately revokes the current session. Invalid login/session returns 401; missing login headers return 400.

**Server description:** Revokes (logs out) the session referenced by the supplied bearer token. The token is immediately invalid.

**Declared responses:** 200, 401. Protected operations can also return 401; authorization checks can return 403.

**Request:**

```sh
curl -X DELETE "${SHARPAI_URL}/v1.0/token" \
  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"
```

## Field reference

These tables list JSON field names, CLR value types, and validation/default documentation from the checked DTO sources. Nullable values may be omitted or null unless an operation requires them. JSON arrays correspond to `List<T>`/array types; enums normally serialize as their named values (defined numeric values are also accepted for settings enums). Operation-specific requirements above take precedence over generic DTO descriptions. Compatibility DTOs can contain accepted but unused fields: consult the compatibility limits before relying on them. Response-only fields (IDs, timestamps, protection flags, computed capabilities) are assigned by the server, not writable inputs.

### DatabaseSettings

Source: [src/SharpAI/Database/DatabaseSettings.cs](src/SharpAI/Database/DatabaseSettings.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Type` | `DatabaseTypeEnum` | Database provider type. Default DatabaseTypeEnum.Sqlite. |
| `Filename` | `string` | SQLite database file path. Used only when Type is Sqlite. Default "./sharpai.db". |
| `Hostname` | `string` | Server hostname or IP for server databases. Prefer 127.0.0.1 over localhost for loopback. |
| `Port` | `int` | Server port. 0 uses the provider default (MySQL 3306, PostgreSQL 5432, SQL Server 1433). |
| `DatabaseName` | `string` | Database (catalog) name for server databases. |
| `Username` | `string` | Username for server databases. |
| `Password` | `string` | Password for server databases. |

### EnumerationResult

Source: [src/SharpAI/Models/EnumerationResult.cs](src/SharpAI/Models/EnumerationResult.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Success` | `bool` | Indicates if the statistics operation was successful. |
| `Timestamp` | `Timestamp` | Start and end timestamps. |
| `MaxResults` | `int` | Maximum number of results to retrieve. |
| `Skip` | `int` | Skip. |
| `IterationsRequired` | `int` | Iterations required. |
| `ContinuationToken` | `Guid?` | Continuation token. |
| `EndOfResults` | `bool` | Boolean indicating end of results. |
| `TotalRecords` | `long` | Total number of records. |
| `RecordsRemaining` | `long` | Number of candidate records remaining in the enumeration. |
| `Objects` | `List<T>` | Objects. |

### GenerationConfiguration

Source: [src/SharpAI/Models/GenerationConfiguration.cs](src/SharpAI/Models/GenerationConfiguration.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Temperature` | `double?` | Temperature. Optional; allowed range 0 through 2. |
| `MaxTokens` | `int?` | MaxTokens. Optional; allowed range 1 through 32768. |
| `TopP` | `double?` | TopP. Optional; allowed range 0 through 1. |
| `TopK` | `int?` | TopK. Optional; allowed range 0 through 100. |
| `MinP` | `double?` | MinP. Optional; allowed range 0 through 1. |
| `RepeatPenalty` | `double?` | RepeatPenalty. Optional; allowed range 0 through 2. |
| `RepeatLastN` | `int?` | RepeatLastN. Optional; allowed range 0 through 32768. |
| `PresencePenalty` | `double?` | PresencePenalty. Optional; allowed range -2 through 2. |
| `FrequencyPenalty` | `double?` | FrequencyPenalty. Optional; allowed range -2 through 2. |
| `Seed` | `int?` | Seed. Optional; allowed range 0 through 2147483647. |
| `Stop` | `List<string>` | Literal stop sequences. Defaults to empty. |
| `StreamResponse` | `bool` | Whether replies stream incrementally. Default true. |
| `JsonOutput` | `bool` | Request a JSON response. Default false. |
| `DisplayThinking` | `bool` | Show model thinking when supported. Default false. |

### LibraryItem

Source: [src/SharpAI/Models/LibraryItem.cs](src/SharpAI/Models/LibraryItem.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Id` | `string` | Immutable identifier. |
| `Kind` | `string` | SystemPrompt or ModelConfiguration, assigned by the endpoint. |
| `Name` | `string` | Display name, at most 256 characters. |
| `Description` | `string` | Optional operator description, at most 4096 characters. |
| `Scope` | `string` | System, Tenant, or User. Default User. |
| `TenantId` | `string` | Owning tenant for Tenant and User items; null for System items. |
| `UserId` | `string` | Owning user for User items; otherwise null. Always derived from the authenticated user. |
| `Content` | `string` | System prompt text. Only present for SystemPrompt items. |
| `Configuration` | `GenerationConfiguration` | Generation parameters. Only present for ModelConfiguration items. |
| `CreatedUtc` | `DateTime` | Creation timestamp. |
| `UpdatedUtc` | `DateTime` | Modification timestamp. |
| `CanEdit` | `bool` | Whether the current caller can edit this record. Computed by REST, never trusted on writes. |
| `CanDelete` | `bool` | Whether the current caller may delete this record. Computed by REST. |

### ModelFile

Source: [src/SharpAI/Models/ModelFile.cs](src/SharpAI/Models/ModelFile.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `GUID` | `Guid` | GUID (primary identifier). |
| `Name` | `string` | Name. |
| `ParentModel` | `string` | Parent model name. |
| `Format` | `string` | Model format. |
| `Family` | `string` | Model family. |
| `ContentLength` | `long` | Content length of the file. |
| `ParameterCount` | `long` | Parameter count. |
| `MD5Hash` | `string` | MD5. |
| `SHA1Hash` | `string` | SHA1. |
| `SHA256Hash` | `string` | SHA256. |
| `SourceUrl` | `string` | Source URL. |
| `ParameterSize` | `string` | Parameter size. |
| `Quantization` | `string` | Quantization. |
| `Embeddings` | `bool` | Boolean indicating if the model can be used for embeddings. |
| `Completions` | `bool` | Boolean indicating if the model can be used for completions. |
| `ModelCreationUtc` | `DateTime?` | Timestamp from the hosting provider, generally a last modified timestamp, in UTC time. |
| `CreatedUtc` | `DateTime` | Timestamp from creation, in UTC time. |

### ModelPreset

Source: [src/SharpAI/Models/ModelPreset.cs](src/SharpAI/Models/ModelPreset.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `GUID` | `Guid` | GUID (primary identifier). |
| `Name` | `string` | Preset name. Unique; used as the model value in inference requests to select this preset. |
| `ModelName` | `string` | Name of the base model (in the registry) that this preset runs against. Required. |
| `SystemPrompt` | `string` | Default system prompt injected ahead of the conversation, or null for none. |
| `Temperature` | `float?` | Default sampling temperature applied when the request does not specify one, or null. |
| `MaxTokens` | `int?` | Default maximum tokens to generate when the request does not specify one, or null. |
| `TopP` | `float?` | Default top-p (nucleus) sampling value applied when the request does not specify one, or null. |
| `TemplateOverride` | `string` | Optional raw chat-template override (GGUF/Jinja-style) stored with the preset, or null to use the model's own template. |
| `Stop` | `List<string>` | Default stop sequences merged with any supplied by the request. Never null. |
| `CreatedUtc` | `DateTime` | Timestamp from creation, in UTC time. |
| `LastUpdateUtc` | `DateTime` | Timestamp from last update, in UTC time. |

### OllamaChatCompletionMetrics

Source: [src/SharpAI/Models/Ollama/OllamaChatCompletionMetrics.cs](src/SharpAI/Models/Ollama/OllamaChatCompletionMetrics.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `PromptTokens` | `int` | Number of tokens in the prompt. |
| `CompletionTokens` | `int` | Number of tokens generated. |
| `PromptTokensPerSecond` | `double` | Prompt evaluation rate (tokens/second). |
| `GenerationTokensPerSecond` | `double` | Generation rate (tokens/second). |
| `TotalDurationMs` | `double` | Total response time in milliseconds. |
| `LoadDurationMs` | `double` | Model load time in milliseconds. |
| `PromptEvalDurationMs` | `double` | Prompt evaluation time in milliseconds. |
| `GenerationDurationMs` | `double` | Generation time in milliseconds. |

### OllamaChatMessage

Source: [src/SharpAI/Models/Ollama/OllamaChatMessage.cs](src/SharpAI/Models/Ollama/OllamaChatMessage.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `role` | `string` | Role of the message sender (required). Valid values: "system", "user", "assistant", "tool" |
| `content` | `string` | Content of the message (required). |
| `images` | `List<string>` | Base64-encoded images for multimodal models (optional). Only valid for user messages. |
| `tool_calls` | `List<OllamaToolCall>` | Tool calls made by the assistant (optional). Only valid for assistant messages. |

### OllamaCompletionContext

Source: [src/SharpAI/Models/Ollama/OllamaCompletionContext.cs](src/SharpAI/Models/Ollama/OllamaCompletionContext.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Data` | `List<int>` | The context data. |
| `Model` | `string` | The model this context is for. |
| `LastUpdated` | `DateTime` | When this context was last updated. |

### OllamaCompletionMetrics

Source: [src/SharpAI/Models/Ollama/OllamaCompletionMetrics.cs](src/SharpAI/Models/Ollama/OllamaCompletionMetrics.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `PromptTokens` | `int` | Number of tokens in the prompt. |
| `CompletionTokens` | `int` | Number of tokens generated. |
| `ContextSize` | `int` | Size of the context maintained. |
| `PromptTokensPerSecond` | `double` | Prompt evaluation rate (tokens/second). |
| `GenerationTokensPerSecond` | `double` | Generation rate (tokens/second). |
| `TotalDurationMs` | `double` | Total response time in milliseconds. |
| `LoadDurationMs` | `double` | Model load time in milliseconds. |
| `PromptEvalDurationMs` | `double` | Prompt evaluation time in milliseconds. |
| `GenerationDurationMs` | `double` | Generation time in milliseconds. |

### OllamaCompletionOptions

Source: [src/SharpAI/Models/Ollama/OllamaCompletionOptions.cs](src/SharpAI/Models/Ollama/OllamaCompletionOptions.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `seed` | `int?` | Random seed for generation (optional). |
| `num_predict` | `int?` | Number of tokens to generate (optional, default: infinite). -1 = infinite, -2 = fill context |
| `num_gpu` | `int?` | Number of layers to offload to GPU (optional). 0 = no GPU, -1 = all layers |
| `temperature` | `float?` | Temperature for sampling (optional, default: 0.8). Range: 0.0 to 2.0 |
| `top_k` | `int?` | Top-k sampling parameter (optional, default: 40). Range: 0 to 100 |
| `top_p` | `float?` | Top-p sampling parameter (optional, default: 0.9). Range: 0.0 to 1.0 |
| `min_p` | `float?` | Min-p sampling parameter (optional, default: 0.0). Range: 0.0 to 1.0 |
| `tfs_z` | `float?` | Tail free sampling parameter (optional, default: 1.0). Range: 0.0 to 1.0 |
| `typical_p` | `float?` | Typical sampling parameter (optional, default: 1.0). Range: 0.0 to 1.0 |
| `repeat_penalty` | `float?` | Repeat penalty (optional, default: 1.1). Range: 0.0 to 2.0 |
| `repeat_last_n` | `int?` | Last n tokens to consider for repeat penalty (optional, default: 64). Range: 0 to context size |
| `presence_penalty` | `float?` | Presence penalty (optional, default: 0.0). Range: -2.0 to 2.0 |
| `frequency_penalty` | `float?` | Frequency penalty (optional, default: 0.0). Range: -2.0 to 2.0 |
| `mirostat` | `int?` | Mirostat sampling mode (0/1/2) (optional, default: 0). 0 = disabled, 1 = Mirostat v1, 2 = Mirostat v2 |
| `mirostat_tau` | `float?` | Mirostat target entropy tau (optional, default: 5.0). Range: 0.0 to 10.0 |
| `mirostat_eta` | `float?` | Mirostat learning rate eta (optional, default: 0.1). Range: 0.0 to 1.0 |
| `penalize_newline` | `bool?` | Penalize newline tokens (optional, default: true). |
| `stop` | `List<string>` | Stop sequences for generation (optional). |
| `display_thinking` | `bool?` | When true, include model thinking/reasoning tokens (e.g. &lt;think&gt;...&lt;/think&gt;) in the response. When false (default), thinking tokens are stripped from the output. |
| `numa` | `bool?` | Enable NUMA support (optional, default: false). |
| `num_ctx` | `int?` | Context size (optional, default: 2048). Range: 128 to 1048576 |
| `num_batch` | `int?` | Batch size for prompt evaluation (optional, default: 512). Range: 1 to context size |
| `num_thread` | `int?` | Number of threads to use (optional). Range: 1 to 128 |
| `num_keep` | `int?` | Number of tokens to keep from initial prompt (optional, default: 4). Range: 0 to context size |
| `use_mlock` | `bool?` | Use memory locking (optional, default: false). |
| `use_mmap` | `bool?` | Use memory mapping (optional, default: true). |
| `vocab_only` | `bool?` | Vocabulary only mode (optional, default: false). |
| `low_vram` | `bool?` | Low VRAM mode (optional, default: false). |
| `f16_kv` | `bool?` | F16 key-value storage (optional, default: true). |
| `main_gpu` | `int?` | Main GPU index (optional). Range: 0 to number of GPUs - 1 |
| `logits_all` | `bool?` | Logits all mode (optional). |

### OllamaDeleteModelRequest

Source: [src/SharpAI/Models/Ollama/OllamaDeleteModelRequest.cs](src/SharpAI/Models/Ollama/OllamaDeleteModelRequest.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `name` | `string` | Registered model name to delete; required. |

### OllamaEmbeddingsOptions

Source: [src/SharpAI/Models/Ollama/OllamaEmbeddingsOptions.cs](src/SharpAI/Models/Ollama/OllamaEmbeddingsOptions.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `num_thread` | `int?` | Number of threads to use (optional). Range: 1 to 128 |
| `num_ctx` | `int?` | Context size (optional). Range: 128 to 1048576 |
| `num_gpu` | `int?` | Number of layers to offload to GPU (optional). 0 = no GPU, -1 = all layers |
| `main_gpu` | `int?` | Main GPU index (optional). Range: 0 to number of GPUs - 1 |
| `low_vram` | `bool?` | Low VRAM mode (optional, default: false). |
| `f16_kv` | `bool?` | F16 key-value storage (optional, default: true). |
| `numa` | `bool?` | Enable NUMA support (optional, default: false). |
| `use_mmap` | `bool?` | Use memory mapping (optional, default: true). |
| `use_mlock` | `bool?` | Use memory locking (optional, default: false). |

### OllamaGenerateChatCompletionChunk

Source: [src/SharpAI/Models/Ollama/OllamaGenerateChatCompletionChunk.cs](src/SharpAI/Models/Ollama/OllamaGenerateChatCompletionChunk.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `model` | `string` | The model that generated the response. |
| `created_at` | `DateTime?` | The timestamp of when the response was created. |
| `message` | `OllamaChatMessage` | The partial message generated by the model. |
| `done` | `bool` | Whether the response is complete. False for intermediate chunks, true for the final chunk. |
| `done_reason` | `string` | Reason why the response is done. Examples: "stop", "length", "function_call" Only present in the final chunk (when done=true). |
| `total_duration` | `long?` | Total duration in nanoseconds. Only present in the final chunk (when done=true). |
| `load_duration` | `long?` | Model load duration in nanoseconds. Only present in the final chunk (when done=true). |
| `prompt_eval_count` | `int?` | Number of tokens in the prompt. Only present in the final chunk (when done=true). |
| `prompt_eval_duration` | `long?` | Prompt evaluation duration in nanoseconds. Only present in the final chunk (when done=true). |
| `eval_count` | `int?` | Number of tokens generated in the response. Only present in the final chunk (when done=true). |
| `eval_duration` | `long?` | Response generation duration in nanoseconds. Only present in the final chunk (when done=true). |

### OllamaGenerateChatCompletionRequest

Source: [src/SharpAI/Models/Ollama/OllamaGenerateChatCompletionRequest.cs](src/SharpAI/Models/Ollama/OllamaGenerateChatCompletionRequest.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `model` | `string` | Model name to use for chat completion (required). |
| `messages` | `List<OllamaChatMessage>` | Messages for the chat (required). |
| `options` | `OllamaCompletionOptions` | Additional model parameters (optional). Never null; a default instance is substituted when the client omits the field or sends null. |
| `format` | `string` | Format to return the response in. Currently only "json" is supported (optional). |
| `template` | `string` | The full prompt template (overrides what is defined in the Modelfile) (optional). |
| `stream` | `bool?` | Enable streaming of generated text (optional, defaults to true). |
| `keep_alive` | `string` | How long to keep the model loaded in memory (optional). Examples: "5m", "10m", "1h", "never" |
| `tools` | `List<OllamaTool>` | Tools/functions available for the model to use (optional). |

### OllamaGenerateChatCompletionResult

Source: [src/SharpAI/Models/Ollama/OllamaGenerateChatCompletionResult.cs](src/SharpAI/Models/Ollama/OllamaGenerateChatCompletionResult.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `model` | `string` | The model that generated the response. |
| `created_at` | `DateTime?` | The timestamp of when the response was created. |
| `message` | `OllamaChatMessage` | The message generated by the model. |
| `done` | `bool` | Whether the response is complete. |
| `done_reason` | `string` | Reason why the response is done. Examples: "stop", "length", "function_call" |
| `total_duration` | `long?` | Total duration in nanoseconds. |
| `load_duration` | `long?` | Model load duration in nanoseconds. |
| `prompt_eval_count` | `int?` | Number of tokens in the prompt. |
| `prompt_eval_duration` | `long?` | Prompt evaluation duration in nanoseconds. |
| `eval_count` | `int?` | Number of tokens generated in the response. |
| `eval_duration` | `long?` | Response generation duration in nanoseconds. |

### OllamaGenerateChatCompletionStreamingResult

Source: [src/SharpAI/Models/Ollama/OllamaGenerateChatCompletionStreamingResult.cs](src/SharpAI/Models/Ollama/OllamaGenerateChatCompletionStreamingResult.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Chunks` | `IAsyncEnumerable<OllamaGenerateChatCompletionChunk>` | The async enumerable stream of chat completion chunks. Yields OllamaGenerateChatCompletionChunk objects as they arrive from the server. |

### OllamaGenerateCompletionChunk

Source: [src/SharpAI/Models/Ollama/OllamaGenerateCompletionChunk.cs](src/SharpAI/Models/Ollama/OllamaGenerateCompletionChunk.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `model` | `string` | The model that generated the response. |
| `created_at` | `DateTime?` | The timestamp of when the response was created. |
| `response` | `string` | The partial generated text response. |
| `done` | `bool` | Whether the response is complete. False for intermediate chunks, true for the final chunk. |
| `context` | `List<int>` | Context for maintaining conversation state. Only present in the final chunk (when done=true). |
| `total_duration` | `long?` | Total duration in nanoseconds. Only present in the final chunk (when done=true). |
| `load_duration` | `long?` | Model load duration in nanoseconds. Only present in the final chunk (when done=true). |
| `prompt_eval_count` | `int?` | Number of tokens in the prompt. Only present in the final chunk (when done=true). |
| `prompt_eval_duration` | `long?` | Prompt evaluation duration in nanoseconds. Only present in the final chunk (when done=true). |
| `eval_count` | `int?` | Number of tokens generated in the response. Only present in the final chunk (when done=true). |
| `eval_duration` | `long?` | Response generation duration in nanoseconds. Only present in the final chunk (when done=true). |

### OllamaGenerateCompletionRequest

Source: [src/SharpAI/Models/Ollama/OllamaGenerateCompletionRequest.cs](src/SharpAI/Models/Ollama/OllamaGenerateCompletionRequest.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `model` | `string` | Model name to use for generation (required). |
| `prompt` | `string` | The prompt to generate a response for (required). |
| `options` | `OllamaCompletionOptions` | Additional model parameters (optional). Never null; a default instance is substituted when the client omits the field or sends null. |
| `system` | `string` | System message to use (overrides what is defined in the Modelfile) (optional). |
| `template` | `string` | The full prompt or prompt template (overrides what is defined in the Modelfile) (optional). |
| `context` | `List<int>` | The context from a previous request, used to keep a conversation going (optional). |
| `stream` | `bool?` | Enable streaming of generated text (optional, defaults to true). |
| `raw` | `bool?` | If false, the response will not include the prompt (optional). |
| `format` | `string` | Format to return the response in. Currently only "json" is supported (optional). |
| `images` | `List<string>` | Base64-encoded images for multimodal models (optional). |
| `keep_alive` | `string` | How long to keep the model loaded in memory (optional). Examples: "5m", "10m", "1h", "never" |

### OllamaGenerateCompletionResult

Source: [src/SharpAI/Models/Ollama/OllamaGenerateCompletionResult.cs](src/SharpAI/Models/Ollama/OllamaGenerateCompletionResult.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `model` | `string` | The model that generated the response. |
| `created_at` | `DateTime?` | The timestamp of when the response was created. |
| `response` | `string` | The generated text response. |
| `context` | `List<int>` | Context for maintaining conversation state. Can be passed in the next request to continue the conversation. |
| `total_duration` | `long?` | Total duration in nanoseconds. |
| `load_duration` | `long?` | Model load duration in nanoseconds. |
| `prompt_eval_count` | `int?` | Number of tokens in the prompt. |
| `prompt_eval_duration` | `long?` | Prompt evaluation duration in nanoseconds. |
| `eval_count` | `int?` | Number of tokens generated in the response. |
| `eval_duration` | `long?` | Response generation duration in nanoseconds. |

### OllamaGenerateCompletionStreamingResult

Source: [src/SharpAI/Models/Ollama/OllamaGenerateCompletionStreamingResult.cs](src/SharpAI/Models/Ollama/OllamaGenerateCompletionStreamingResult.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Chunks` | `IAsyncEnumerable<OllamaGenerateCompletionChunk>` | The async enumerable stream of completion chunks. Yields OllamaGenerateCompletionChunk objects as they arrive from the server. |

### OllamaGenerateEmbeddingsRequest

Source: [src/SharpAI/Models/Ollama/OllamaGenerateEmbeddingsRequest.cs](src/SharpAI/Models/Ollama/OllamaGenerateEmbeddingsRequest.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `model` | `string` | Model name to use for generating embeddings (required). |
| `input` | `object` | Input text(s) to generate embeddings for (required). Can be a single string or an array of strings. Use SetInput() or SetInputs() to set values, and GetInputs() to retrieve as array. |
| `options` | `OllamaEmbeddingsOptions` | Options for generating embeddings (optional). |
| `keep_alive` | `string` | How long to keep the model loaded in memory (optional). Examples: "5m", "10m", "1h", "never" |
| `truncate` | `bool?` | Truncate inputs that exceed the model's context window (optional). |

### OllamaGenerateEmbeddingsResult

Source: [src/SharpAI/Models/Ollama/OllamaGenerateEmbeddingsResult.cs](src/SharpAI/Models/Ollama/OllamaGenerateEmbeddingsResult.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `model` | `string` | The model that generated the embeddings. |
| `embeddings` | `object` | Generated embedding(s). Can be a single array of floats or an array of arrays of floats. Use GetEmbedding() for single result or GetEmbeddings() for multiple results. |
| `total_duration` | `long?` | Total duration in nanoseconds. |
| `load_duration` | `long?` | Model load duration in nanoseconds. |
| `prompt_eval_count` | `int?` | Prompt evaluation count. |

### OllamaListLocalModelsResult

Source: [src/SharpAI/Models/Ollama/OllamaListLocalModelsResult.cs](src/SharpAI/Models/Ollama/OllamaListLocalModelsResult.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `models` | `List<OllamaLocalModel>` | List of models available locally. |

### OllamaListRunningModelsResult

Source: [src/SharpAI/Models/Ollama/OllamaListRunningModelsResult.cs](src/SharpAI/Models/Ollama/OllamaListRunningModelsResult.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `models` | `List<OllamaRunningModel>` | List of models currently loaded in memory. |

### OllamaLocalModel

Source: [src/SharpAI/Models/Ollama/OllamaLocalModel.cs](src/SharpAI/Models/Ollama/OllamaLocalModel.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `name` | `string` | Model name including tag (e.g., "llama2:latest"). |
| `digest` | `string` | Model digest/hash. |
| `size` | `long?` | Model size in bytes. |
| `modified_at` | `DateTime?` | When the model was last modified. |
| `details` | `OllamaModelDetails` | Model details including format, family, parameter size, and quantization. |

### OllamaModelDetails

Source: [src/SharpAI/Models/Ollama/OllamaModelDetails.cs](src/SharpAI/Models/Ollama/OllamaModelDetails.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `parent_model` | `string` | Parent model this was created from. |
| `format` | `string` | Model format (e.g., "gguf"). |
| `family` | `string` | Model family (e.g., "llama", "mistral"). |
| `families` | `List<string>` | Model families/architectures. |
| `parameter_size` | `string` | Parameter size (e.g., "7B", "13B", "70B"). |
| `quantization_level` | `string` | Quantization level (e.g., "Q4_0", "Q4_K_M", "Q8_0"). |

### OllamaModelStatistics

Source: [src/SharpAI/Models/Ollama/OllamaModelStatistics.cs](src/SharpAI/Models/Ollama/OllamaModelStatistics.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `TotalModels` | `int` | Total number of models. |
| `TotalSizeBytes` | `long` | Total size of all models in bytes. |
| `TotalSizeFormatted` | `string` | Total size formatted as string. |
| `AverageSizeBytes` | `long` | Average model size in bytes. |
| `AverageSizeFormatted` | `string` | Average model size formatted as string. |
| `LargestModel` | `OllamaLocalModel` | Largest model. |
| `SmallestModel` | `OllamaLocalModel` | Smallest model. |
| `NewestModel` | `OllamaLocalModel` | Most recently modified model. |
| `OldestModel` | `OllamaLocalModel` | Oldest modified model. |
| `ModelsByFamily` | `Dictionary<string, int>` | Count of models by family. |
| `ModelsByQuantization` | `Dictionary<string, int>` | Count of models by quantization level. |
| `ModelsByParameterSize` | `Dictionary<string, int>` | Count of models by parameter size. |

### OllamaPullModelRequest

Source: [src/SharpAI/Models/Ollama/OllamaPullModelRequest.cs](src/SharpAI/Models/Ollama/OllamaPullModelRequest.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `model` | `string` | Model name to pull (required). |
| `name` | `string` | Model name to pull (required). |
| `insecure` | `bool?` | Allow insecure connections to the registry (optional). |
| `stream` | `bool?` | Enable streaming of pull progress (optional). |
| `username` | `string` | Username for registry authentication (optional). |
| `password` | `string` | Password for registry authentication (optional). |

### OllamaPullModelResultMessage

Source: [src/SharpAI/Models/Ollama/OllamaPullModelResultMessage.cs](src/SharpAI/Models/Ollama/OllamaPullModelResultMessage.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `status` | `string` | Current status of the pull operation. Examples: "pulling manifest", "downloading", "verifying sha256 digest", "writing manifest", "removing any unused layers", "success" |
| `digest` | `string` | Digest of the layer being downloaded. Format: "sha256:hash" |
| `total` | `long?` | Total size of the current layer in bytes. |
| `completed` | `long?` | Number of bytes completed for the current layer. |
| `error` | `string` | Error message if the pull operation failed. |

### OllamaPullProgressTracker

Source: [src/SharpAI/Models/Ollama/OllamaPullProgressTracker.cs](src/SharpAI/Models/Ollama/OllamaPullProgressTracker.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `TotalBytes` | `long` | Total bytes to download across all layers. |
| `CompletedBytes` | `long` | Total bytes completed across all layers. |
| `CurrentStatus` | `string` | Current status message. |
| `CurrentLayer` | `string` | Current layer being downloaded. |
| `LayersCompleted` | `int` | Number of layers completed. |

### OllamaRunningModel

Source: [src/SharpAI/Models/Ollama/OllamaRunningModel.cs](src/SharpAI/Models/Ollama/OllamaRunningModel.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `name` | `string` | Model name including tag. |
| `digest` | `string` | Model digest/hash. |
| `size` | `long?` | Total memory size in bytes (RAM + VRAM). |
| `size_vram` | `long?` | VRAM usage in bytes. |
| `expires_at` | `DateTime?` | When the model expires from memory. ISO 8601 format string. |
| `details` | `OllamaModelDetails` | Model details including format, family, parameter size, and quantization. Reusing the existing OllamaModelDetails class. |

### OllamaRunningModelStatistics

Source: [src/SharpAI/Models/Ollama/OllamaRunningModelStatistics.cs](src/SharpAI/Models/Ollama/OllamaRunningModelStatistics.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `TotalRunningModels` | `int` | Total number of running models. |
| `TotalMemoryBytes` | `long` | Total memory usage in bytes. |
| `TotalMemoryFormatted` | `string` | Total memory usage formatted. |
| `TotalVRAMBytes` | `long` | Total VRAM usage in bytes. |
| `TotalVRAMFormatted` | `string` | Total VRAM usage formatted. |
| `TotalRAMBytes` | `long` | Total RAM usage in bytes. |
| `TotalRAMFormatted` | `string` | Total RAM usage formatted. |
| `AverageMemoryBytes` | `long` | Average memory per model in bytes. |
| `AverageMemoryFormatted` | `string` | Average memory per model formatted. |
| `LargestModel` | `OllamaRunningModel` | Model using the most memory. |
| `SmallestModel` | `OllamaRunningModel` | Model using the least memory. |
| `NextToExpire` | `OllamaRunningModel` | Model expiring soonest. |
| `ModelsUsingVRAM` | `int` | Number of models with VRAM allocation. |
| `AverageVRAMPercentage` | `double` | Average VRAM percentage across models. |
| `ModelsByFamily` | `Dictionary<string, int>` | Count of running models by family. |

### OllamaShowModelInfoRequest

Source: [src/SharpAI/Models/Ollama/OllamaShowModelInfoRequest.cs](src/SharpAI/Models/Ollama/OllamaShowModelInfoRequest.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `model` | `string` | Name of the model to show (required). |
| `verbose` | `bool?` | Include verbose information about the model (optional). |

### OllamaShowModelInfoResult

Source: [src/SharpAI/Models/Ollama/OllamaShowModelInfoResult.cs](src/SharpAI/Models/Ollama/OllamaShowModelInfoResult.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `license` | `string` | Model license information. |
| `modelfile` | `string` | Model file content (when verbose is true). |
| `parameters` | `string` | Model parameters configuration. |
| `template` | `string` | Template used for prompts. |
| `system` | `string` | System message/prompt. |
| `details` | `OllamaModelDetails` | Model details including format, family, families, parameter size, and quantization level. |
| `messages` | `List<OllamaChatMessage>` | Messages/examples used in the model. |
| `model_info` | `Dictionary<string, object>` | Model information including general metadata. |
| `modified_at` | `DateTime?` | Modified timestamp. |

### OllamaStreamingChatCompletionResult

Source: [src/SharpAI/Models/Ollama/OllamaStreamingChatCompletionResult.cs](src/SharpAI/Models/Ollama/OllamaStreamingChatCompletionResult.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `model` | `string` | The model that generated the response. |
| `created_at` | `DateTime?` | The timestamp of when the response was created. |
| `message` | `OllamaChatMessage` | The partial message generated by the model. |
| `done` | `bool` | Whether this is the final chunk in the stream. |

### OllamaStreamingCompletionResult

Source: [src/SharpAI/Models/Ollama/OllamaStreamingCompletionResult.cs](src/SharpAI/Models/Ollama/OllamaStreamingCompletionResult.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `model` | `string` | The model that generated the response. |
| `created_at` | `DateTime?` | The timestamp of when the response was created. |
| `response` | `string` | The partial response text. |
| `done` | `bool` | Whether this is the final chunk in the stream. |

### OllamaTool

Source: [src/SharpAI/Models/Ollama/OllamaTool.cs](src/SharpAI/Models/Ollama/OllamaTool.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `type` | `string` | Type of tool (required). Currently only "function" is supported. |
| `function` | `OllamaToolFunction` | Function definition (required when type is "function"). |

### OllamaToolCall

Source: [src/SharpAI/Models/Ollama/OllamaToolCall.cs](src/SharpAI/Models/Ollama/OllamaToolCall.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `function` | `OllamaToolCallFunction` | Function that was called (required). |

### OllamaToolCallFunction

Source: [src/SharpAI/Models/Ollama/OllamaToolCallFunction.cs](src/SharpAI/Models/Ollama/OllamaToolCallFunction.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `name` | `string` | Name of the function that was called (required). |
| `arguments` | `string` | Arguments passed to the function as a JSON string (required). |

### OllamaToolFunction

Source: [src/SharpAI/Models/Ollama/OllamaToolFunction.cs](src/SharpAI/Models/Ollama/OllamaToolFunction.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `name` | `string` | Name of the function (required). |
| `description` | `string` | Description of what the function does (required). |
| `parameters` | `OllamaToolParameters` | Parameters the function accepts, described as a JSON Schema object (required). |

### OllamaToolParameters

Source: [src/SharpAI/Models/Ollama/OllamaToolParameters.cs](src/SharpAI/Models/Ollama/OllamaToolParameters.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `type` | `string` | Type of the parameters object (required). Should typically be "object". |
| `properties` | `Dictionary<string, object>` | Properties of the parameters object (required). Each property is a JSON Schema definition. |
| `required` | `List<string>` | List of required property names (optional). |

### OllamaUnloadModelRequest

Source: [src/SharpAI/Models/Ollama/OllamaUnloadModelRequest.cs](src/SharpAI/Models/Ollama/OllamaUnloadModelRequest.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `name` | `string` | Name of the model to unload (required). Following Ollama convention, this uses 'name' in JSON but 'Model' in code. |

### OpenAIChatChoice

Source: [src/SharpAI/Models/OpenAI/OpenAIChatChoice.cs](src/SharpAI/Models/OpenAI/OpenAIChatChoice.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `index` | `int` | Index of this choice. |
| `message` | `OpenAIChatMessage` | Chat message generated by the model. |
| `delta` | `OpenAIChatMessage` | Delta message for streaming responses. |
| `logprobs` | `OpenAIChatLogprobs` | Log probabilities information. |
| `finish_reason` | `string` | Reason the generation stopped. Possible values: "stop", "length", "tool_calls", "content_filter", "function_call" |

### OpenAIChatCompletionChunk

Source: [src/SharpAI/Models/OpenAI/OpenAIChatCompletionChunk.cs](src/SharpAI/Models/OpenAI/OpenAIChatCompletionChunk.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `id` | `string` | Unique identifier for the chat completion. |
| `object` | `string` | Object type (always "chat.completion.chunk"). |
| `created` | `long?` | Unix timestamp when the completion was created. |
| `model` | `string` | Model used for the completion. |
| `system_fingerprint` | `string` | System fingerprint. |
| `choices` | `List<OpenAIChatChoice>` | List of chat completion choices with delta updates. |
| `usage` | `OpenAIUsage` | Usage statistics for the completion. Only present in the final chunk when stream_options.include_usage is true. |

### OpenAIChatLogprobContent

Source: [src/SharpAI/Models/OpenAI/OpenAIChatLogprobContent.cs](src/SharpAI/Models/OpenAI/OpenAIChatLogprobContent.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `token` | `string` | The token. |
| `logprob` | `float` | Log probability of this token. |
| `bytes` | `List<int>` | UTF-8 byte representation of the token. |
| `top_logprobs` | `List<OpenAITopLogprob>` | Top alternative tokens and their log probabilities. |

### OpenAIChatLogprobs

Source: [src/SharpAI/Models/OpenAI/OpenAIChatLogprobs.cs](src/SharpAI/Models/OpenAI/OpenAIChatLogprobs.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `content` | `List<OpenAIChatLogprobContent>` | Log probability information for each content token. |

### OpenAIChatMessage

Source: [src/SharpAI/Models/OpenAI/OpenAIChatMessage.cs](src/SharpAI/Models/OpenAI/OpenAIChatMessage.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `role` | `string` | Role of the message sender (required). Valid values: "system", "user", "assistant", "tool" |
| `content` | `object` | Content of the message. Can be a string or an array of content parts for multimodal input. |
| `name` | `string` | Name of the author of this message. |
| `tool_calls` | `List<OpenAIToolCall>` | Tool calls made by the assistant. |
| `tool_call_id` | `string` | Tool call ID this message is responding to. |

### OpenAICompletionChoice

Source: [src/SharpAI/Models/OpenAI/OpenAICompletionChoice.cs](src/SharpAI/Models/OpenAI/OpenAICompletionChoice.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `text` | `string` | Generated text. |
| `index` | `int` | Index of this choice. |
| `logprobs` | `OpenAILogprobs` | Log probabilities information. |
| `finish_reason` | `string` | Reason the generation stopped. Possible values: "stop", "length" |

### OpenAICompletionChunk

Source: [src/SharpAI/Models/OpenAI/OpenAICompletionChunk.cs](src/SharpAI/Models/OpenAI/OpenAICompletionChunk.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `id` | `string` | Unique identifier for the completion. |
| `object` | `string` | Object type (always "text_completion"). |
| `created` | `long?` | Unix timestamp when the completion was created. |
| `model` | `string` | Model used for the completion. |
| `system_fingerprint` | `string` | System fingerprint. |
| `choices` | `List<OpenAICompletionChoice>` | List of completion choices. |

### OpenAICompletionTokensDetails

Source: [src/SharpAI/Models/OpenAI/OpenAICompletionTokensDetails.cs](src/SharpAI/Models/OpenAI/OpenAICompletionTokensDetails.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `reasoning_tokens` | `int?` | Number of reasoning tokens generated. |
| `audio_tokens` | `int?` | Number of audio tokens generated. |
| `accepted_prediction_tokens` | `int?` | Number of tokens that were accepted. |
| `rejected_prediction_tokens` | `int?` | Number of tokens that were rejected. |

### OpenAIEmbedding

Source: [src/SharpAI/Models/OpenAI/OpenAIEmbedding.cs](src/SharpAI/Models/OpenAI/OpenAIEmbedding.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `object` | `string` | Object type (always "embedding"). |
| `index` | `int` | Index of this embedding in the input array. |
| `embedding` | `float[]` | The embedding vector. |

### OpenAIEmbeddingUsage

Source: [src/SharpAI/Models/OpenAI/OpenAIEmbeddingUsage.cs](src/SharpAI/Models/OpenAI/OpenAIEmbeddingUsage.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `prompt_tokens` | `int` | Number of tokens in the input. |
| `total_tokens` | `int` | Total number of tokens used. |

### OpenAIError

Source: [src/SharpAI/Models/OpenAI/OpenAIError.cs](src/SharpAI/Models/OpenAI/OpenAIError.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `error` | `OpenAIErrorDetails` | Error details. |

### OpenAIErrorDetails

Source: [src/SharpAI/Models/OpenAI/OpenAIErrorDetails.cs](src/SharpAI/Models/OpenAI/OpenAIErrorDetails.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `message` | `string` | Message. |
| `type` | `string` | Type. |
| `param` | `object` | Parameters. |
| `code` | `string` | Code. |

### OpenAIGenerateChatCompletionRequest

Source: [src/SharpAI/Models/OpenAI/OpenAIGenerateChatCompletionRequest.cs](src/SharpAI/Models/OpenAI/OpenAIGenerateChatCompletionRequest.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `stream_options` | `OpenAIStreamOptions` | Optional streaming usage controls. Null preserves the default stream contract. |
| `model` | `string` | ID of the model to use (required). |
| `messages` | `List<OpenAIChatMessage>` | Messages for the chat conversation (required). |
| `response_format` | `OpenAIResponseFormat` | Response format specification. |
| `max_tokens` | `int?` | The maximum number of tokens to generate. |
| `temperature` | `float?` | Temperature for sampling (0-2). |
| `top_p` | `float?` | Nucleus sampling parameter (0-1). |
| `n` | `int?` | Number of chat completions to generate for each input message. |
| `stream` | `bool?` | Whether to stream partial progress. |
| `stop` | `object` | Up to 4 sequences where the API will stop generating further tokens. |
| `presence_penalty` | `float?` | Presence penalty (-2.0 to 2.0). |
| `frequency_penalty` | `float?` | Frequency penalty (-2.0 to 2.0). |
| `logit_bias` | `Dictionary<string, float>` | Modify the likelihood of specified tokens appearing in the completion. |
| `logprobs` | `bool?` | Include the log probabilities on the logprobs most likely tokens. |
| `top_logprobs` | `int?` | Number of most likely tokens to return at each token position. |
| `user` | `string` | A unique identifier representing your end-user. |
| `tools` | `List<OpenAITool>` | Functions/tools the model may call. |
| `tool_choice` | `object` | Controls which (if any) tool is called by the model. |
| `parallel_tool_calls` | `bool?` | Whether to enable parallel function calling. |
| `seed` | `int?` | Random seed for deterministic generation. |

### OpenAIGenerateChatCompletionResult

Source: [src/SharpAI/Models/OpenAI/OpenAIGenerateChatCompletionResult.cs](src/SharpAI/Models/OpenAI/OpenAIGenerateChatCompletionResult.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `id` | `string` | Unique identifier for the chat completion. |
| `object` | `string` | Object type (always "chat.completion"). |
| `created` | `long?` | Unix timestamp when the completion was created. |
| `model` | `string` | Model used for the completion. |
| `system_fingerprint` | `string` | System fingerprint. |
| `choices` | `List<OpenAIChatChoice>` | List of chat completion choices. |
| `usage` | `OpenAIUsage` | Usage statistics for the completion. |

### OpenAIGenerateChatCompletionStreamingResult

Source: [src/SharpAI/Models/OpenAI/OpenAIGenerateChatCompletionStreamingResult.cs](src/SharpAI/Models/OpenAI/OpenAIGenerateChatCompletionStreamingResult.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Chunks` | `IAsyncEnumerable<OpenAIChatCompletionChunk>` | The async enumerable stream of chat completion chunks. Yields OpenAIChatCompletionChunk objects as they arrive from the server. |

### OpenAIGenerateCompletionRequest

Source: [src/SharpAI/Models/OpenAI/OpenAIGenerateCompletionRequest.cs](src/SharpAI/Models/OpenAI/OpenAIGenerateCompletionRequest.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `stream_options` | `OpenAIStreamOptions` | Optional streaming usage controls. Null preserves the default stream contract. |
| `model` | `string` | ID of the model to use (required). |
| `prompt` | `object` | The prompt(s) to generate completions for (required). Can be a string or an array of strings. |
| `suffix` | `string` | The suffix that comes after a completion of inserted text. |
| `max_tokens` | `int?` | The maximum number of tokens to generate. |
| `temperature` | `float?` | Temperature for sampling (0-2). |
| `top_p` | `float?` | Nucleus sampling parameter (0-1). |
| `n` | `int?` | Number of completions to generate for each prompt. |
| `stream` | `bool?` | Whether to stream partial progress. |
| `logprobs` | `int?` | Include the log probabilities on the logprobs most likely tokens. |
| `echo` | `bool?` | Echo back the prompt in addition to the completion. |
| `stop` | `object` | Up to 4 sequences where the API will stop generating further tokens. |
| `presence_penalty` | `float?` | Presence penalty (-2.0 to 2.0). |
| `frequency_penalty` | `float?` | Frequency penalty (-2.0 to 2.0). |
| `best_of` | `int?` | Generates best_of completions server-side and returns the best. |
| `logit_bias` | `Dictionary<string, float>` | Modify the likelihood of specified tokens appearing in the completion. |
| `user` | `string` | A unique identifier representing your end-user. |
| `seed` | `int?` | Random seed for deterministic generation. |

### OpenAIGenerateCompletionResult

Source: [src/SharpAI/Models/OpenAI/OpenAIGenerateCompletionResult.cs](src/SharpAI/Models/OpenAI/OpenAIGenerateCompletionResult.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `id` | `string` | Unique identifier for the completion. |
| `object` | `string` | Object type (always "text_completion"). |
| `created` | `long?` | Unix timestamp when the completion was created. |
| `model` | `string` | Model used for the completion. |
| `system_fingerprint` | `string` | System fingerprint. |
| `choices` | `List<OpenAICompletionChoice>` | List of completion choices. |
| `usage` | `OpenAIUsage` | Usage statistics for the completion. |

### OpenAIGenerateCompletionStreamingResult

Source: [src/SharpAI/Models/OpenAI/OpenAIGenerateCompletionStreamingResult.cs](src/SharpAI/Models/OpenAI/OpenAIGenerateCompletionStreamingResult.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Chunks` | `IAsyncEnumerable<OpenAICompletionChunk>` | The async enumerable stream of completion chunks. Yields OpenAICompletionChunk objects as they arrive from the server. |

### OpenAIGenerateEmbeddingsRequest

Source: [src/SharpAI/Models/OpenAI/OpenAIGenerateEmbeddingsRequest.cs](src/SharpAI/Models/OpenAI/OpenAIGenerateEmbeddingsRequest.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `model` | `string` | ID of the model to use (required). |
| `input` | `object` | Input text to embed (required). Can be a string or an array of strings. |
| `encoding_format` | `string` | The format to return the embeddings in. Can be either "float" or "base64". |
| `dimensions` | `int?` | The number of dimensions the resulting output embeddings should have. Only supported in some models. |
| `user` | `string` | A unique identifier representing your end-user. |

### OpenAIGenerateEmbeddingsResult

Source: [src/SharpAI/Models/OpenAI/OpenAIGenerateEmbeddingsResult.cs](src/SharpAI/Models/OpenAI/OpenAIGenerateEmbeddingsResult.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `object` | `string` | Object type (always "list"). |
| `data` | `List<OpenAIEmbedding>` | List of embedding objects. |
| `model` | `string` | Model used to generate embeddings. |
| `usage` | `OpenAIEmbeddingUsage` | Usage statistics. |

### OpenAILogprobs

Source: [src/SharpAI/Models/OpenAI/OpenAILogprobs.cs](src/SharpAI/Models/OpenAI/OpenAILogprobs.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `tokens` | `List<string>` | List of token strings. |
| `token_logprobs` | `List<float?>` | Log probabilities for each token. |
| `top_logprobs` | `List<Dictionary<string, float>>` | Top log probabilities for each token position. |
| `text_offset` | `List<int>` | Text offset for each token. |

### OpenAIPromptTokensDetails

Source: [src/SharpAI/Models/OpenAI/OpenAIPromptTokensDetails.cs](src/SharpAI/Models/OpenAI/OpenAIPromptTokensDetails.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `cached_tokens` | `int?` | Number of tokens from cached content. |
| `audio_tokens` | `int?` | Number of tokens from audio input. |

### OpenAIResponseFormat

Source: [src/SharpAI/Models/OpenAI/OpenAIResponseFormat.cs](src/SharpAI/Models/OpenAI/OpenAIResponseFormat.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `type` | `string` | Type of response format. Valid values: "text", "json_object", "json_schema" |
| `json_schema` | `object` | JSON schema for structured output (when type is "json_schema"). |

### OpenAIStreamOptions

Source: [src/SharpAI/Models/OpenAI/OpenAIStreamOptions.cs](src/SharpAI/Models/OpenAI/OpenAIStreamOptions.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `include_usage` | `bool` | Emit an aggregate usage chunk with empty choices immediately before DONE. Default false. |

### OpenAIStreamingChatCompletionResult

Source: [src/SharpAI/Models/OpenAI/OpenAIStreamingChatCompletionResult.cs](src/SharpAI/Models/OpenAI/OpenAIStreamingChatCompletionResult.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `id` | `string` | Unique identifier for the chat completion. |
| `object` | `string` | Object type (always "chat.completion.chunk"). |
| `created` | `long?` | Unix timestamp when the completion was created. |
| `model` | `string` | Model used for the completion. |
| `system_fingerprint` | `string` | System fingerprint. |
| `choices` | `List<OpenAIChatChoice>` | List of chat completion choices with deltas. |
| `usage` | `OpenAIUsage` | Usage statistics (only in final chunk). |

### OpenAIStreamingCompletionResult

Source: [src/SharpAI/Models/OpenAI/OpenAIStreamingCompletionResult.cs](src/SharpAI/Models/OpenAI/OpenAIStreamingCompletionResult.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `id` | `string` | Unique identifier for the completion. |
| `object` | `string` | Object type (always "text_completion"). |
| `created` | `long?` | Unix timestamp when the completion was created. |
| `model` | `string` | Model used for the completion. |
| `choices` | `List<OpenAICompletionChoice>` | List of completion choices. |

### OpenAITool

Source: [src/SharpAI/Models/OpenAI/OpenAITool.cs](src/SharpAI/Models/OpenAI/OpenAITool.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `type` | `string` | Type of tool (currently only "function" is supported). |
| `function` | `OpenAIToolFunction` | Function definition. |

### OpenAIToolCall

Source: [src/SharpAI/Models/OpenAI/OpenAIToolCall.cs](src/SharpAI/Models/OpenAI/OpenAIToolCall.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `id` | `string` | Unique identifier for the tool call. |
| `type` | `string` | Type of tool call (currently only "function"). |
| `function` | `OpenAIToolCallFunction` | Function call details. |

### OpenAIToolCallFunction

Source: [src/SharpAI/Models/OpenAI/OpenAIToolCallFunction.cs](src/SharpAI/Models/OpenAI/OpenAIToolCallFunction.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `name` | `string` | Name of the function to call. |
| `arguments` | `string` | Arguments to pass to the function as a JSON string. |

### OpenAIToolFunction

Source: [src/SharpAI/Models/OpenAI/OpenAIToolFunction.cs](src/SharpAI/Models/OpenAI/OpenAIToolFunction.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `name` | `string` | Name of the function. |
| `description` | `string` | Description of what the function does. |
| `parameters` | `object` | Parameters the function accepts, described as a JSON Schema object. |
| `strict` | `bool?` | Whether to enable strict schema adherence. |

### OpenAITopLogprob

Source: [src/SharpAI/Models/OpenAI/OpenAITopLogprob.cs](src/SharpAI/Models/OpenAI/OpenAITopLogprob.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `token` | `string` | The token. |
| `logprob` | `float` | Log probability of this token. |
| `bytes` | `List<int>` | UTF-8 byte representation of the token. |

### OpenAIUsage

Source: [src/SharpAI/Models/OpenAI/OpenAIUsage.cs](src/SharpAI/Models/OpenAI/OpenAIUsage.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `prompt_tokens` | `int` | Number of tokens in the prompt. |
| `completion_tokens` | `int` | Number of tokens in the completion. |
| `total_tokens` | `int` | Total number of tokens used. |
| `prompt_tokens_details` | `OpenAIPromptTokensDetails` | Detailed breakdown of prompt tokens (for chat completions with images). |
| `completion_tokens_details` | `OpenAICompletionTokensDetails` | Detailed breakdown of completion tokens. |

### RequestHistoryBucket

Source: [src/SharpAI/Models/RequestHistoryBucket.cs](src/SharpAI/Models/RequestHistoryBucket.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `BucketStartUtc` | `DateTime` | UTC start of the bucket (inclusive). |
| `BucketEndUtc` | `DateTime` | UTC end of the bucket (exclusive). |
| `SuccessCount` | `int` | Number of successful (status &lt; 400) requests in the bucket. |
| `FailureCount` | `int` | Number of failed (status &gt;= 400) requests in the bucket. |
| `AverageDurationMs` | `double` | Average request duration in milliseconds within the bucket. |

### RequestHistoryEntry

Source: [src/SharpAI/Models/RequestHistoryEntry.cs](src/SharpAI/Models/RequestHistoryEntry.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Id` | `string` | Entry identifier (prefix "req_"). Never null or empty. |
| `TenantId` | `string` | Tenant identifier, or null for unauthenticated requests. |
| `UserId` | `string` | User identifier, or null for unauthenticated requests. |
| `PrincipalName` | `string` | Principal display name, if resolved. |
| `Method` | `string` | HTTP method (GET, POST, etc.). |
| `Path` | `string` | Route template that matched, or the raw path when no template. |
| `Url` | `string` | Full request URL including query string. |
| `StatusCode` | `int` | Response HTTP status code. |
| `DurationMs` | `double` | Request duration in milliseconds. |
| `SourceIp` | `string` | Client source IP as observed by the server. |
| `RequestHeaders` | `Dictionary<string, string>` | Request headers with secrets redacted. Never null. |
| `RequestBody` | `string` | Request body (may be truncated). |
| `RequestBodyBytes` | `long` | Original request body length in bytes, before truncation. |
| `RequestBodyTruncated` | `bool` | Whether the request body was truncated. |
| `ResponseHeaders` | `Dictionary<string, string>` | Response headers. Never null. |
| `ResponseBody` | `string` | Response body (may be truncated). |
| `ResponseBodyBytes` | `long` | Original response body length in bytes, before truncation. |
| `ResponseBodyTruncated` | `bool` | Whether the response body was truncated. |
| `CreatedUtc` | `DateTime` | UTC time the request began. |
| `CompletedUtc` | `DateTime?` | UTC time the response was sent, if known. |
| `ModelName` | `string` | Captured ModelName. Null means unavailable, including older records. Timing values use milliseconds. |
| `IsStreaming` | `bool?` | Captured IsStreaming. Null means unavailable, including older records. Timing values use milliseconds. |
| `InputTokens` | `int?` | Captured InputTokens. Null means unavailable, including older records. Timing values use milliseconds. |
| `OutputTokens` | `int?` | Captured OutputTokens. Null means unavailable, including older records. Timing values use milliseconds. |
| `CachedTokens` | `int?` | Captured CachedTokens. Null means unavailable, including older records. Timing values use milliseconds. |
| `TtftMs` | `double?` | Captured TtftMs. Null means unavailable, including older records. Timing values use milliseconds. |
| `StreamDurationMs` | `double?` | Captured StreamDurationMs. Null means unavailable, including older records. Timing values use milliseconds. |
| `QueueMs` | `double?` | Captured QueueMs. Null means unavailable, including older records. Timing values use milliseconds. |
| `LoadMs` | `double?` | Captured LoadMs. Null means unavailable, including older records. Timing values use milliseconds. |
| `PromptMs` | `double?` | Captured PromptMs. Null means unavailable, including older records. Timing values use milliseconds. |
| `GenerationMs` | `double?` | Captured GenerationMs. Null means unavailable, including older records. Timing values use milliseconds. |
| `EngineTotalMs` | `double?` | Captured EngineTotalMs. Null means unavailable, including older records. Timing values use milliseconds. |
| `Cancelled` | `bool?` | Captured Cancelled. Null means unavailable, including older records. Timing values use milliseconds. |
| `PhaseTimingsApproximate` | `bool?` | Captured PhaseTimingsApproximate. Null means unavailable, including older records. Timing values use milliseconds. |
| `TraceId` | `string` | Captured TraceId. Null means unavailable, including older records. Timing values use milliseconds. |
| `SpanId` | `string` | Captured SpanId. Null means unavailable, including older records. Timing values use milliseconds. |

### RequestHistorySummary

Source: [src/SharpAI/Models/RequestHistorySummary.cs](src/SharpAI/Models/RequestHistorySummary.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `TotalCount` | `long` | Total number of requests in the range. |
| `TotalSuccess` | `long` | Total successful (status &lt; 400) requests. |
| `TotalFailure` | `long` | Total failed (status &gt;= 400) requests. |
| `AverageDurationMs` | `double` | Average request duration in milliseconds across the range. |
| `Buckets` | `List<RequestHistoryBucket>` | Buckets covering the range, including empty ones. Never null. |

### ModelInspection

Source: [src/SharpAI/Operations/ModelInspection.cs](src/SharpAI/Operations/ModelInspection.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Architecture` | `string` | Architecture; zero or null means unavailable. Memory values use bytes. |
| `WeightsBytes` | `long` | WeightsBytes; zero or null means unavailable. Memory values use bytes. |
| `KvBytesPerSlot` | `long` | KvBytesPerSlot; zero or null means unavailable. Memory values use bytes. |
| `BuffersBytes` | `long` | BuffersBytes; zero or null means unavailable. Memory values use bytes. |
| `HeadroomBytes` | `long` | HeadroomBytes; zero or null means unavailable. Memory values use bytes. |
| `TotalBytes` | `long` | TotalBytes; zero or null means unavailable. Memory values use bytes. |
| `HostBytes` | `long` | HostBytes; zero or null means unavailable. Memory values use bytes. |
| `DeviceBytes` | `long` | DeviceBytes; zero or null means unavailable. Memory values use bytes. |
| `TrainedContext` | `int` | TrainedContext; zero or null means unavailable. Memory values use bytes. |
| `EffectiveContext` | `int` | EffectiveContext; zero or null means unavailable. Memory values use bytes. |
| `Slots` | `int` | Slots; zero or null means unavailable. Memory values use bytes. |
| `Layers` | `int` | Layers; zero or null means unavailable. Memory values use bytes. |
| `Heads` | `int` | Heads; zero or null means unavailable. Memory values use bytes. |
| `KvHeads` | `int` | KvHeads; zero or null means unavailable. Memory values use bytes. |
| `ParameterCount` | `long` | ParameterCount; zero or null means unavailable. Memory values use bytes. |
| `QuantizationType` | `uint` | QuantizationType; zero or null means unavailable. Memory values use bytes. |
| `Supported` | `bool` | Supported; zero or null means unavailable. Memory values use bytes. |
| `EstimateAvailable` | `bool` | EstimateAvailable; zero or null means unavailable. Memory values use bytes. |
| `Assumptions` | `string` | Assumptions; zero or null means unavailable. Memory values use bytes. |
| `Files` | `List<string>` | Files; zero or null means unavailable. Memory values use bytes. |

### AuditLogEntry

Source: [src/SharpAI/Security/AuditLogEntry.cs](src/SharpAI/Security/AuditLogEntry.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Guid` | `string` | Audit entry identifier (prefix "aud_"). |
| `TenantGuid` | `string` | Tenant identifier, if resolved. |
| `EventType` | `string` | Event type (for example "AuthenticationFailure", "AuthorizationDenied", "SessionCreated"). |
| `PrincipalType` | `PrincipalTypeEnum` | Principal type. |
| `PrincipalGuid` | `string` | Principal identifier, if resolved. |
| `Method` | `string` | HTTP method. |
| `Path` | `string` | Request path. |
| `IpAddress` | `string` | Client IP address. |
| `AuthResult` | `bool` | Whether authentication succeeded. |
| `AuthzResult` | `bool` | Whether authorization succeeded. |
| `DenialReason` | `string` | Reason for a denial, if any. |
| `StatusCode` | `int` | Response status code. |
| `CreatedUtc` | `DateTime` | Event time in UTC. |

### AuthSession

Source: [src/SharpAI/Security/AuthSession.cs](src/SharpAI/Security/AuthSession.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Guid` | `string` | Session identifier (prefix "ses_"). This is the value carried inside the encrypted bearer token. |
| `UserGuid` | `string` | Principal (user) identifier. |
| `TenantGuid` | `string` | Tenant identifier. |
| `PrincipalType` | `PrincipalTypeEnum` | Principal type. |
| `Active` | `bool` | Whether the session is active (not revoked). |
| `IsProtected` | `bool` | Whether the session is protected. |
| `CreatedUtc` | `DateTime` | Creation time in UTC. |
| `ExpiresUtc` | `DateTime` | Expiry time in UTC. |
| `LastUsedUtc` | `DateTime?` | Last used time in UTC, if ever. |
| `RevokedUtc` | `DateTime?` | Revocation time in UTC, if revoked. |
| `RevocationReason` | `string` | Revocation reason, if revoked. |

### Credential

Source: [src/SharpAI/Security/Credential.cs](src/SharpAI/Security/Credential.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Guid` | `string` | Credential identifier (prefix "crd_"). |
| `UserGuid` | `string` | Owning user identifier. |
| `TenantGuid` | `string` | Owning tenant identifier. |
| `Name` | `string` | Human-readable name. |
| `AccessKey` | `string` | Access key (prefix "access_"). Safe to store and display. |
| `SecretSha256` | `string` | SHA-256 hex digest of the secret key. Never the plaintext. |
| `Active` | `bool` | Whether the credential is active. |
| `IsProtected` | `bool` | Whether the credential is protected from deletion/modification via tenant APIs. |
| `CreatedUtc` | `DateTime` | Creation time in UTC. |
| `LastUpdateUtc` | `DateTime` | Last update time in UTC. |
| `LastUsedUtc` | `DateTime?` | Last used time in UTC, if ever. |
| `ExpiresUtc` | `DateTime?` | Expiry time in UTC, if the credential expires. |

### CredentialScopeAssignment

Source: [src/SharpAI/Security/CredentialScopeAssignment.cs](src/SharpAI/Security/CredentialScopeAssignment.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Guid` | `string` | Assignment identifier (prefix "asn_"). |
| `TenantGuid` | `string` | Tenant identifier. |
| `CredentialGuid` | `string` | Credential identifier. |
| `RoleGuid` | `string` | Role identifier, or null for a role-less direct grant. |
| `RoleName` | `string` | Role name fallback, or null. |
| `ResourceScope` | `ResourceScopeEnum` | Resource scope of the grant. |
| `ResourceGuid` | `string` | Target resource identifier when resource-scoped. |
| `InheritsToChildren` | `bool` | Whether a tenant-scoped grant flows to child resources inside the tenant. |
| `Permissions` | `List<string>` | Direct operation grants (OperationTypeEnum names) for a role-less assignment. Never null. |
| `ResourceTypes` | `List<string>` | Direct resource-type grants for a role-less assignment. Never null. |
| `Active` | `bool` | Whether the assignment is active. |
| `IsProtected` | `bool` | Whether the assignment is protected. |
| `CreatedUtc` | `DateTime` | Creation time in UTC. |
| `LastUpdateUtc` | `DateTime` | Last update time in UTC. |

### EffectivePermission

Source: [src/SharpAI/Security/EffectivePermission.cs](src/SharpAI/Security/EffectivePermission.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `ResourceType` | `string` | Resource type this grant applies to (or ResourceTypes.All). |
| `Operation` | `OperationTypeEnum` | Operation this grant covers. |
| `Effect` | `PermissionEffectEnum` | Whether this grant permits or denies. |
| `ResourceScope` | `ResourceScopeEnum` | Scope of the grant. |
| `ResourceGuid` | `string` | Target resource identifier when resource-scoped, otherwise null. |
| `InheritsToChildren` | `bool` | Whether a tenant-scoped grant flows to child resources. |

### Permission

Source: [src/SharpAI/Security/Permission.cs](src/SharpAI/Security/Permission.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Guid` | `string` | Permission identifier (prefix "perm_"). |
| `TenantGuid` | `string` | Owning tenant identifier, or null for a globally-visible built-in permission. |
| `Name` | `string` | Human-readable name (for example "Manage Users"). |
| `ResourceTypes` | `List<string>` | Resource types this permission applies to. Use ResourceTypes.All for a wildcard. Never null. |
| `OperationTypes` | `List<string>` | Operation types this permission covers, as OperationTypeEnum names. Use "All" for a wildcard. Never null. |
| `Effect` | `PermissionEffectEnum` | Whether this permission permits or explicitly denies. |
| `Active` | `bool` | Whether the permission is active. |
| `IsProtected` | `bool` | Whether the permission is protected from deletion/mutation through tenant APIs. |
| `CreatedUtc` | `DateTime` | Creation time in UTC. |
| `LastUpdateUtc` | `DateTime` | Last update time in UTC. |

### RolePermissionMap

Source: [src/SharpAI/Security/RolePermissionMap.cs](src/SharpAI/Security/RolePermissionMap.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Guid` | `string` | Mapping identifier (prefix "asn_"). |
| `TenantGuid` | `string` | Owning tenant identifier, or null for a built-in mapping. |
| `RoleGuid` | `string` | Role identifier. |
| `PermissionGuid` | `string` | Permission identifier. |
| `Active` | `bool` | Whether the mapping is active. |
| `IsProtected` | `bool` | Whether the mapping is protected. |
| `CreatedUtc` | `DateTime` | Creation time in UTC. |
| `LastUpdateUtc` | `DateTime` | Last update time in UTC. |

### Tenant

Source: [src/SharpAI/Security/Tenant.cs](src/SharpAI/Security/Tenant.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Guid` | `string` | Tenant identifier (prefix "ten_", or "default" for the seeded default tenant). |
| `Name` | `string` | Tenant name. |
| `Active` | `bool` | Whether the tenant is active. |
| `IsProtected` | `bool` | Whether the tenant is protected from deletion/modification via tenant APIs. |
| `CreatedUtc` | `DateTime` | Creation time in UTC. |
| `LastUpdateUtc` | `DateTime` | Last update time in UTC. |

### User

Source: [src/SharpAI/Security/User.cs](src/SharpAI/Security/User.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Guid` | `string` | User identifier (prefix "usr_"). |
| `TenantGuid` | `string` | Owning tenant identifier. |
| `FirstName` | `string` | First name. |
| `LastName` | `string` | Last name. |
| `Email` | `string` | Email address (unique within a tenant). |
| `PasswordSha256` | `string` | SHA-256 hex digest of the user's password. Never the plaintext. |
| `IsAdmin` | `bool` | Whether the user is a global administrator. |
| `IsTenantAdmin` | `bool` | Whether the user is a tenant administrator. |
| `Active` | `bool` | Whether the user is active. |
| `IsProtected` | `bool` | Whether the user is protected from deletion/modification via tenant APIs. |
| `CreatedUtc` | `DateTime` | Creation time in UTC. |
| `LastUpdateUtc` | `DateTime` | Last update time in UTC. |

### UserRole

Source: [src/SharpAI/Security/UserRole.cs](src/SharpAI/Security/UserRole.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Guid` | `string` | Role identifier (prefix "role_"). |
| `TenantGuid` | `string` | Owning tenant identifier, or null for a globally-visible built-in role. |
| `Name` | `string` | Human-readable role name (for example "TenantAdmin", "Viewer"). |
| `IsBuiltIn` | `bool` | Whether the role is seeded and maintained by the platform. |
| `Active` | `bool` | Whether the role is active. |
| `IsProtected` | `bool` | Whether the role is protected from deletion/mutation through tenant APIs. |
| `CreatedUtc` | `DateTime` | Creation time in UTC. |
| `LastUpdateUtc` | `DateTime` | Last update time in UTC. |

### UserRoleAssignment

Source: [src/SharpAI/Security/UserRoleAssignment.cs](src/SharpAI/Security/UserRoleAssignment.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Guid` | `string` | Assignment identifier (prefix "asn_"). |
| `TenantGuid` | `string` | Tenant identifier. |
| `UserGuid` | `string` | User identifier. |
| `RoleGuid` | `string` | Role identifier, or null when the assignment references a role by name. |
| `RoleName` | `string` | Role name, used as a fallback when RoleGuid is null or unresolved. |
| `ResourceScope` | `ResourceScopeEnum` | Resource scope of the grant. |
| `ResourceGuid` | `string` | Target resource identifier when ResourceScope is ResourceScopeEnum.Resource. |
| `InheritsToChildren` | `bool` | Whether a tenant-scoped grant flows to child resources inside the tenant. |
| `Active` | `bool` | Whether the assignment is active. |
| `IsProtected` | `bool` | Whether the assignment is protected. |
| `CreatedUtc` | `DateTime` | Creation time in UTC. |
| `LastUpdateUtc` | `DateTime` | Last update time in UTC. |

### UpdateRequest

Source: [src/SharpAI.Server/API/REST/Routes/AdministrationRoutes.cs](src/SharpAI.Server/API/REST/Routes/AdministrationRoutes.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Name` | `string` | Display name. |
| `Email` | `string` | Email address. |
| `FirstName` | `string` | First name. |
| `LastName` | `string` | Last name. |
| `Password` | `string` | New password, used only by the password reset route. |
| `IsAdmin` | `bool?` | Platform administrator flag. |
| `IsTenantAdmin` | `bool?` | Tenant administrator flag. |
| `Active` | `bool?` | Whether the record is active. |
| `ExpiresUtc` | `DateTime?` | Credential expiration. |
| `ClearExpiration` | `bool` | Explicitly clear a credential expiration. |

### CreateAssignmentRequest

Source: [src/SharpAI.Server/Classes/Requests/CreateAssignmentRequest.cs](src/SharpAI.Server/Classes/Requests/CreateAssignmentRequest.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `UserGuid` | `string` | User identifier the role is assigned to. |
| `RoleGuid` | `string` | Role identifier, or null when referencing a role by name. |
| `RoleName` | `string` | Role name, used when RoleGuid is null (for example a built-in role name). |
| `ResourceScope` | `ResourceScopeEnum` | Resource scope of the grant. |
| `ResourceGuid` | `string` | Target resource identifier when resource-scoped. |
| `InheritsToChildren` | `bool` | Whether a tenant-scoped grant flows to child resources. |

### CreateCredentialRequest

Source: [src/SharpAI.Server/Classes/Requests/CreateCredentialRequest.cs](src/SharpAI.Server/Classes/Requests/CreateCredentialRequest.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `UserGuid` | `string` | Owning user identifier. |
| `Name` | `string` | Human-readable name. |
| `ExpiresUtc` | `DateTime?` | Optional expiry time in UTC. |

### CreatePresetRequest

Source: [src/SharpAI.Server/Classes/Requests/CreatePresetRequest.cs](src/SharpAI.Server/Classes/Requests/CreatePresetRequest.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `name` | `string` | Preset name (used as the model value in inference requests). Required. |
| `model` | `string` | Base model name in the registry that the preset runs against. Required. |
| `system` | `string` | Default system prompt, or null. |
| `temperature` | `float?` | Default sampling temperature, or null. |
| `max_tokens` | `int?` | Default maximum tokens to generate, or null. |
| `top_p` | `float?` | Default top-p value, or null. |
| `template` | `string` | Optional raw chat-template override, or null. |
| `stop` | `List<string>` | Default stop sequences, or null. |

### CreateUserRequest

Source: [src/SharpAI.Server/Classes/Requests/CreateUserRequest.cs](src/SharpAI.Server/Classes/Requests/CreateUserRequest.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Email` | `string` | Email address (unique within the tenant). |
| `FirstName` | `string` | First name. |
| `LastName` | `string` | Last name. |
| `Password` | `string` | Plaintext password. Hashed server-side; never stored or echoed. |
| `IsAdmin` | `bool` | Whether the user is a global administrator. |
| `IsTenantAdmin` | `bool` | Whether the user is a tenant administrator. |

### ImportModelRequest

Source: [src/SharpAI.Server/Classes/Requests/ImportModelRequest.cs](src/SharpAI.Server/Classes/Requests/ImportModelRequest.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `path` | `string` | Absolute or relative path to a local .gguf file readable by the server process. Required. |
| `name` | `string` | Registry name to assign to the imported model. When null or empty, the file name (without its extension) is used. |

### AuthSettings

Source: [src/SharpAI.Server/Classes/Settings/AuthSettings.cs](src/SharpAI.Server/Classes/Settings/AuthSettings.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Enabled` | `bool` | Authentication is mandatory. Legacy false values are ignored. |
| `AdminApiKeys` | `List<string>` | Legacy administrator API keys, ignored by the server. Never null. |
| `TokenSigningKey` | `string` | Key material used to encrypt session bearer tokens (AES-256, SHA-256-derived key). When null or empty a random per-boot key is generated, which invalidates existing tokens on restart; set a stable value to keep sessions valid across restarts. Never null after loading. |
| `SessionTtlMinutes` | `int` | Lifetime of a login session in minutes. Default 60; clamped to the range 1..43200 (30 days). |

### DebugSettings

Source: [src/SharpAI.Server/Classes/Settings/DebugSettings.cs](src/SharpAI.Server/Classes/Settings/DebugSettings.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `RequestBody` | `bool` | True to enable request body logging. |

### HuggingFaceSettings

Source: [src/SharpAI.Server/Classes/Settings/HuggingFaceSettings.cs](src/SharpAI.Server/Classes/Settings/HuggingFaceSettings.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `ApiKey` | `string` | Optional API key. Null or empty permits public repository access. |

### LoggingSettings

Source: [src/SharpAI.Server/Classes/Settings/LoggingSettings.cs](src/SharpAI.Server/Classes/Settings/LoggingSettings.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Servers` | `List<SyslogServer>` | List of syslog servers. |
| `LogDirectory` | `string` | Log directory. |
| `LogFilename` | `string` | Log filename. |
| `ConsoleLogging` | `bool` | Enable or disable console logging. |
| `EnableColors` | `bool` | Enable colors in logging. |
| `MinimumSeverity` | `int` | Minimum severity. |

### RequestHistorySettings

Source: [src/SharpAI.Server/Classes/Settings/RequestHistorySettings.cs](src/SharpAI.Server/Classes/Settings/RequestHistorySettings.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Enabled` | `bool` | Whether request capture is enabled. Default true. |
| `MaxRequestBodyBytes` | `int` | Maximum request body bytes captured before truncation. Default 65536, range 0-1048576. |
| `MaxResponseBodyBytes` | `int` | Maximum response body bytes captured before truncation. Default 65536, range 0-1048576. |
| `RetentionDays` | `int` | Retention in days before a captured row is eligible for pruning. Default 30, range 1-3650. |

### RuntimeSettings

Source: [src/SharpAI.Server/Classes/Settings/RuntimeSettings.cs](src/SharpAI.Server/Classes/Settings/RuntimeSettings.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `ForceBackend` | `string` | Force a specific backend. Valid values: "cpu", "cuda", "metal", or null for auto-detection. |
| `CpuBackendPath` | `string` | Path to the CPU backend native library. Default: "./runtimes/cpu/libllama.so" (Linux), "./runtimes/cpu/llama.dll" (Windows), "./runtimes/cpu/libllama.dylib" (macOS). Supports environment variable expansion. |
| `GpuBackendPath` | `string` | Path to the GPU backend native library. Default: "./runtimes/cuda/libllama.so" (Linux), "./runtimes/cuda/llama.dll" (Windows). Supports environment variable expansion. Note: On macOS Apple Silicon, use MetalBackendPath instead. |
| `MetalBackendPath` | `string` | Path to the Metal backend native library. Default: "./runtimes/osx-arm64/native/libllama.dylib" (macOS Apple Silicon). Supports environment variable expansion. Note: Only available on macOS Apple Silicon (ARM64). |
| `EnableNativeLogging` | `bool` | Enable or disable LlamaSharp native library logging to console. When disabled, llama.cpp log messages will not be written to console. Default: false (disabled). |

### Settings

Source: [src/SharpAI.Server/Classes/Settings/Settings.cs](src/SharpAI.Server/Classes/Settings/Settings.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `CreatedUtc` | `DateTime` | Timestamp from creation, in UTC time. |
| `SchemaVersion` | `string` | Configuration schema version, used to migrate settings across major releases. Defaults to the current schema version when absent from a loaded file. |
| `SoftwareVersion` | `string` | Software version. |
| `Logging` | `LoggingSettings` | Logging settings. |
| `Storage` | `StorageSettings` | Storage settings. |
| `Database` | `DatabaseSettings` | Database settings. |
| `HuggingFace` | `HuggingFaceSettings` | HuggingFace settings. |
| `Rest` | `WebserverSettings` | REST settings. |
| `Debug` | `DebugSettings` | Debug settings. |
| `QuantizationPriority` | `Dictionary<string, int>` | Quantization priority.  If null or empty, Ollama quantization priority will be used. |
| `Runtime` | `RuntimeSettings` | Runtime settings for native library backend configuration. |
| `Telemetry` | `TelemetrySettings` | Telemetry settings for OpenTelemetry metrics, traces, and logs. |
| `RequestHistory` | `RequestHistorySettings` | Request-history capture settings. |
| `Auth` | `AuthSettings` | Authentication configuration; bearer authentication is mandatory in the server. Legacy false is ignored. |

### StorageSettings

Source: [src/SharpAI.Server/Classes/Settings/StorageSettings.cs](src/SharpAI.Server/Classes/Settings/StorageSettings.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `TempDirectory` | `string` | Temporary storage directory. |
| `ModelsDirectory` | `string` | Models directory. |

### SyslogServer

Source: [src/SharpAI.Server/Classes/Settings/SyslogServer.cs](src/SharpAI.Server/Classes/Settings/SyslogServer.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Hostname` | `string` | Hostname. |
| `Port` | `int` | Port. |
| `RandomizePorts` | `bool` | Boolean to indicate whether or not randomized port numbers should be used. If false, the value in 'Port' will be used. |
| `MinimumPort` | `int` | Minimum port. |
| `MaximumPort` | `int` | Maximum port. |

### TelemetrySettings

Source: [src/SharpAI.Server/Classes/Settings/TelemetrySettings.cs](src/SharpAI.Server/Classes/Settings/TelemetrySettings.cs).

| JSON field | Type | Meaning / validation |
| --- | --- | --- |
| `Enable` | `bool` | Whether telemetry is enabled. Default true. When false, no meters or activity sources are subscribed and no exporter is started. |
| `ServiceName` | `string` | Logical service name attached to all telemetry. Default "sharpai". May not be null or empty. |
| `OtlpEndpoint` | `string` | OTLP collector endpoint. Default "http://localhost:4317". May not be null or empty. |
| `OtlpProtocol` | `string` | OTLP protocol: "grpc" (default) or "httpprotobuf". Any other value falls back to "grpc". |
| `PrometheusEnable` | `bool` | Whether to serve an in-process Prometheus scrape endpoint (separate HttpListener). Default false; in the bundled Docker stack, metrics reach Prometheus through the OTLP collector instead. |
| `PrometheusHostname` | `string` | Hostname for the in-process Prometheus endpoint. Default "localhost". |
| `PrometheusPort` | `int` | Port for the in-process Prometheus endpoint. Default 9464. Clamped to the range 0-65535. |
| `PrometheusPath` | `string` | Path for the in-process Prometheus endpoint. Default "/metrics". |
| `EnableMetrics` | `bool` | Whether metrics are exported. Default true. |
| `EnableTraces` | `bool` | Whether traces are exported. Default true. |
| `EnableLogs` | `bool` | Whether logs are exported over OTLP. Default false; the bundled stack ships logs to Loki by tailing the server's log files, so OTLP log export is off unless explicitly enabled. |

## Maintaining this reference

`docs/api/openapi.json` is the checked snapshot of the server-generated route contract. The generator adds reviewed route-family semantics and DTO field tables from source. After route changes, export the authenticated `/openapi.json` from a server built from the same checkout, update the snapshot, review request/response details in `scripts/generate-rest-reference.py`, and regenerate both artifacts:

```sh
python3 scripts/generate-rest-reference.py
python3 scripts/generate-rest-reference.py --check
python3 scripts/generate-rest-reference.py --check --live-openapi /path/to/fresh-openapi.json
```

The checker compares server literal registrations, dynamically registered library routes, the dashboard operation catalog, collection coverage, and generated output. It does not turn a passing documentation check into evidence that every endpoint has been executed. See [Postman usage and validation](postman/README.md).
