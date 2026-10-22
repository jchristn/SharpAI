# SharpAI Python SDK

A small, **zero-dependency** Python client for a [SharpAI](https://github.com/jchristn/SharpAI) server.
It covers the Ollama-compatible API, the OpenAI-compatible API, request history, authentication, and the
account/RBAC management surface. It defaults to the loopback address `http://127.0.0.1:8000`.

## Install

```bash
pip install sharpai
```

Or from source:

```bash
pip install ./sdk/python
```

Requires Python 3.9+. No third-party dependencies (standard library only).

## Quickstart

```python
from sharpai import SharpAIClient

client = SharpAIClient()  # http://127.0.0.1:8000 by default

# Chat (non-streaming)
reply = client.chat("my-model", [{"role": "user", "content": "Hello!"}])
print(reply["message"]["content"])

# Embeddings
vectors = client.embeddings("my-model", "some text")

# Pull a model, streaming progress
for event in client.pull_model("my-model"):
    print(event)
```

## Authentication

Authentication is mandatory. Use an API bearer credential or sign in to obtain a bearer session:

```python
# Interactive login -> stores a bearer session token on the client
client = SharpAIClient()
client.login("admin@sharpai.local", "password", tenant_guid="default")

# Or supply credentials up front
client = SharpAIClient(token="eyJ...")                       # bearer session token
```

## Endpoint coverage

| Area | Methods |
|------|---------|
| Health | `health`, `ready` |
| Settings | `get_settings`, `update_settings` |
| Models (Ollama) | `list_models`, `running_models`, `show_model`, `delete_model`, `unload_model`, `pull_model` (streaming) |
| Inference (Ollama) | `chat`, `generate`, `embeddings` |
| OpenAI-compatible | `openai_models`, `openai_chat`, `openai_completions`, `openai_embeddings` |
| Request history | `request_history`, `request_history_summary`, `request_history_entry` |
| Auth | `login`, `session`, `logout`, `audit` |
| Management (RBAC) | `list_tenants`, `create_tenant`, `list_users`, `create_user`, `delete_user`, `list_credentials`, `create_credential`, `list_roles`, `create_assignment`, `user_permissions`, `credential_permissions` |

Any non-2xx response raises `sharpai.SharpAIError` (with `.status`, `.message`, `.body`).

## Tests

```bash
cd sdk/python
python -m unittest discover -s tests -v
```

The test harness mocks HTTP, so no running server is required.

## Bearer token reveal and regeneration

These methods require Credential Admin permission within the addressed tenant. Reveal returns `token` and
`tokenType`; ordinary credential reads omit token values. Legacy hash-only credentials return HTTP 409 until
regenerated. Regeneration immediately invalidates the previous token and preserves grants, active status and expiry.
The SDK uses its normal non-success response behavior; do not log returned token objects.

```python
token = client.reveal_credential_token("default", "crd_...")
replacement = client.regenerate_credential_token("default", "crd_...")
```

See [API contract and encrypted storage](../../DEPLOYMENT_GUIDE.md#reveal-and-regenerate-api-bearer-tokens) for authentication, caching, auditing, backup and legacy-token details.
