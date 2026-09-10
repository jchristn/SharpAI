<div align="center">
  <img src="https://github.com/jchristn/sharpai/blob/main/assets/logo.png?raw=true" width="200" height="200">
</div>

# SharpAI

SharpAI runs GGUF language models locally and exposes them over the same REST APIs you already use with
Ollama and OpenAI. Point an existing Ollama or OpenAI client at the container and it works — no cloud
account, no per-token billing, and no data leaving the host. The image is built on
[LlamaSharp](https://github.com/SciSharp/LLamaSharp) and ships with a management dashboard, request-level
observability, and optional multi-tenant authentication.

## When to use it

Reach for this image when you want local inference behind a stable API: an air-gapped or on-prem
deployment, a development server shared by a team, a drop-in replacement for a hosted OpenAI endpoint in
CI, or an embeddings service for a retrieval pipeline. Because it speaks both the Ollama (`/api/*`) and
OpenAI (`/v1/*`) dialects, most existing SDKs and tools connect without code changes.

## What is inside

The container is a single .NET server. On startup it loads its configuration from `sharpai.json`,
initializes its database (SQLite by default; MySQL, PostgreSQL, and SQL Server are also supported),
and begins serving:

- **Ollama-compatible API** — `/api/tags`, `/api/pull`, `/api/show`, `/api/ps`, `/api/generate`,
  `/api/chat`, `/api/embed`, `/api/delete`, `/api/unload`.
- **OpenAI-compatible API** — `/v1/models`, `/v1/chat/completions`, `/v1/completions`, `/v1/embeddings`.
- **Operations** — request-history capture, health/readiness probes, an OpenAPI document and Swagger UI,
  and a Prometheus `/metrics` endpoint.
- **Optional AAA** — authentication, RBAC authorization, and an audit trail, all **off by default** so
  the server behaves like an open local Ollama until you turn them on.

Chat requests are rendered with each model's own embedded chat template when the GGUF carries one, so
prompts match what the model was trained on rather than a hand-maintained format table.

## Getting started

```bash
docker run -d \
  --name sharpai \
  -p 8000:8000 \
  -v sharpai-models:/app/models \
  -v sharpai-data:/app \
  jchristn/sharpai:v5.0.0
```

The server listens on port 8000. Pull a model and chat with it using any Ollama client, or `curl`:

```bash
# Pull a GGUF model (requires a HuggingFace token in sharpai.json for gated/hosted models)
curl -X POST http://localhost:8000/api/pull -d '{"name":"<model>"}'

# Chat (OpenAI-compatible)
curl http://localhost:8000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{"model":"<model>","messages":[{"role":"user","content":"Hello!"}]}'
```

Configuration lives in `sharpai.json` (auto-created on first run) — REST host/port, the models directory,
the database block, the HuggingFace API key, concurrency and model-lifecycle limits, telemetry, request
history, and the authentication block. Mount a volume over `/app` to persist it along with the database.

## GPU acceleration

CPU inference works out of the box. For NVIDIA GPUs, use the CUDA image variant and pass `--gpus all`;
the server auto-detects the backend and reports VRAM usage through `/api/ps`. See the CPU and CUDA
Compose files in the repository for a complete setup.

## Observability

The repository's `docker/compose.yaml` brings up SharpAI alongside Prometheus, Loki, Tempo, and Grafana
with dashboards provisioned, so you can watch request rates, latencies, model load events, and logs while
you operate the server. The server exports OTLP metrics/traces/logs and a Prometheus exposition at
`/metrics`.

## Links

- Source, full README, and Compose files: https://github.com/jchristn/SharpAI
- Deployment guide: https://github.com/jchristn/SharpAI/blob/main/DEPLOYMENT-GUIDE.md
- Issues: https://github.com/jchristn/SharpAI/issues

Licensed under the MIT license.
