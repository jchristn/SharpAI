# System prompts and model configurations

The dashboard has two managed libraries:

| Page | Content | Playground use |
| --- | --- | --- |
| `/system-prompts` | Named instructions and an optional description | Inserted as the system message before the conversation |
| `/model-configurations` | All generation parameters, stop sequences, streaming, JSON output and thinking visibility | Applied to subsequent chat requests; excludes the system prompt and model selection |

The home page links to both libraries and counts the items visible to the current caller.
Playground groups each selector by visibility. The edit icon opens the selected item; for a read-only
shared default it opens a duplicate editor. Existing conversations retain previously generated replies.
There is no separate editable generation-parameters panel in Playground.

## Visibility and ownership

| Scope | Who can read | Who can publish, edit or delete |
| --- | --- | --- |
| System | All authenticated users, and anonymous callers when authentication is disabled | Platform administrators |
| Tenant | Members of that tenant; platform administrators may select a tenant workspace | That tenant's administrators or platform administrators |
| User | Only that user, including appropriately authorized credentials owned by that user | That user, subject to explicit RBAC denials and credential grants |

Platform and tenant administrators do not enumerate or inspect other users' private items. Human users
have intrinsic library self-service rights: read available defaults, duplicate them, and manage personal
items. Explicit RBAC denies for `SystemPrompt` or `ModelConfiguration` override those rights. Integration
credentials require explicit permissions and remain subject to their owner's permission ceiling.

Ownership filters are applied in SQL before counts and pagination. Names may repeat; IDs are unique.
Scope and owner are immutable for an existing ID. Use **Duplicate** to publish a copy into a different
scope. A duplicate defaults to personal visibility when the caller has a user identity. Administrators
may choose an authorized shared scope instead. The original stays unchanged.

With authentication disabled, anonymous callers retain the existing open-server platform rights, but
have no private user library. Supplying credentials to the new library endpoints resolves the actual
principal, even in open mode. Sign in to create personal items. Invalid supplied credentials are rejected.

## REST contract

The same operations apply to `/v1.0/system-prompts` and `/v1.0/model-configurations`:

| Method | Path suffix | Behavior |
| --- | --- | --- |
| GET | none | Paginated visible items; `pageNumber`, `pageSize` (1–1000), exact `name`, optional `tenantId` workspace |
| POST | none | Create an authorized scoped item |
| GET | `/{id}` | Read a visible item; invisible/private/foreign IDs return 404 |
| PUT | `/{id}` | Update mutable content and metadata without changing ownership |
| POST | `/{id}/duplicate` | Create a copy of a visible source; omitted payload inherits the original, a supplied Content or Configuration replaces that complete payload in the new copy |
| DELETE | `/{id}` | Delete an authorized item |

`GET /v1.0/library-context` returns `IsAdmin`, `IsTenantAdmin`, `TenantId`, `UserTenantId` and
`UserId` when available. Only a platform administrator can select another tenant using `tenantId`.
Null properties may be omitted. List responses use the existing `EnumerationResult` shape with
`Objects`, `TotalRecords` and `EndOfResults`. Items include `CanEdit` and `CanDelete` for UI controls;
these flags are recalculated server-side and never authorize a write.

Requests use PascalCase field names. For example, a signed-in user's prompt:

```json
{
  "Name": "Concise answers",
  "Description": "A brief answer before supporting detail.",
  "Scope": "User",
  "Content": "Answer directly and concisely. State assumptions when needed."
}
```

A model configuration:

```json
{
  "Name": "Interactive chat",
  "Scope": "User",
  "Configuration": {
    "Temperature": 0.6,
    "MaxTokens": 256,
    "TopP": 0.9,
    "TopK": 40,
    "MinP": 0.05,
    "RepeatPenalty": 1.1,
    "RepeatLastN": 64,
    "PresencePenalty": 0,
    "FrequencyPenalty": 0,
    "Seed": null,
    "Stop": [],
    "StreamResponse": true,
    "JsonOutput": false,
    "DisplayThinking": false
  }
}
```

Omitted or null numeric parameters use engine defaults. All numeric bounds match the structured editor.
`StreamResponse` defaults true; JSON output and thinking visibility default false. Descriptions are not
sent to the model. User ownership is derived from the caller; submitted IDs and timestamps are ignored.
Configurations reject unknown properties, including a system prompt.

Validation failures return 400, missing/invalid identity returns 401, and insufficient publication rights,
explicit RBAC denials, or attempted owner impersonation return 403. Mutations and authorization denials
are audited without copying prompt text into the audit message. Library request and response bodies are withheld from shared request history and debug body logs,
and library query strings are omitted from retained URLs. This prevents shared history access from
bypassing private-item visibility. Inference history retains its existing capture behavior for executed requests.

## Persistence and compatibility

Migration 7 adds `library_items` and a visibility index for SQLite, MySQL, PostgreSQL and SQL Server. It
seeds one system-wide helpful-assistant prompt and one balanced model configuration. Existing data and
legacy model presets are retained. Deleting a user removes that user’s private library items; deleting
a tenant removes its tenant and user items. Shared system items survive creator/account deletion. The old `/api/create` model-preset workflow is independent of these
new libraries. Existing Ollama and OpenAI inference payloads remain unchanged; Playground expands the
selected records into those payloads before submitting a request.

Tests cover scope visibility, cross-account and cross-tenant requests, owner impersonation, elevated
publication attempts, duplicates, RBAC denials, credential restrictions, pagination, numeric validation,
restart persistence and signed-in identity with open inference enabled.
