<img src="../assets/logo.png" height="48" alt="SharpAI">

# SharpAI Dashboard (v4.0.1)

A React and TypeScript workspace for the SharpAI server, built with Vite. The dashboard uses shared, accessible controls and supports light and dark themes, English, Spanish, Portuguese, French, Italian, German, Simplified Chinese, Arabic, Russian, Malay, Hindi, and Japanese, and desktop, tablet, and phone layouts.

## Pages

- **Overview:** model counts, request activity, and recent failures.
- **Models:** running and downloaded models, streaming pull progress, confirmed deletion and unloading.
- **Playground:** streaming chat with a stop button, installed-model suggestions, and embedding previews.
- **Request and inference history:** time ranges, filters, charts, pagination, and request details.
- **API Explorer:** browse the live OpenAPI document and execute requests with the current session.
- **Observability:** health, readiness, live metrics, and a Grafana link.
- **Settings:** grouped configuration forms with typed controls, masked credentials, lists, and key/value overrides. Saves preserve unmodified fields. Some changes require a server restart.

The logo and favicon in `public/images/` match the canonical assets in `../assets/`.

## Development

Use a Node.js version supported by Vite 8 (Node 22.12+ or a newer supported release).

```sh
npm ci
npm run dev
```

The application calls its own origin. The production nginx configuration proxies API requests to the SharpAI server. For a custom embedding or development harness, pass `baseUrl` to `NewApp`.

## Build and validation

```sh
npm run build
npm test
npm run lint
npx playwright install chromium
npm run e2e
```

Playwright starts a separate preview server on port 3017 so it does not reuse the Docker dashboard on port 3000. Tests cover all routes in desktop light/dark, tablet, and mobile layouts, accessibility, navigation, login, settings preservation and error handling, chat, embeddings, model actions, model pulls, and API execution. Workflow fixtures validate browser behavior without changing the live server configuration or downloading models.

Build the dashboard Docker image from the repository root using `./build-dashboard.sh v4.0.1`.

## Localization

English is the source and fallback catalog. Locale files are UTF-8 JSON in `src/i18n/`, lazy-loaded before the selected language is rendered. The registry owns autonyms and direction. Regional variants map to the supported base language; `zh` uses Simplified Chinese and `pt` uses general Portuguese. `en-XA` expands text and `ar-XB` exercises RTL layout. The language selector is available in the shell and the sign-in dialog and persists across reloads and authentication changes.

Use semantic keys and i18next interpolation (`{{name}}`); never concatenate translated fragments. To add a locale, add its registry entry and a complete catalog with identical keys and interpolation placeholders. Run `npm test` for missing/orphaned keys, encoding, placeholders, locale switching, and the hard-coded JSX/accessibility-string gate. Browser tests exercise every supported locale and pseudo-locale. Both gates run on every push and pull request.

The new catalogs were machine translated and checked for structural correctness; native-speaker terminology review remains advisable. Keep SharpAI, Hugging Face, Grafana, Prometheus, Loki, Tempo, API paths, IDs, enum wire values, and raw diagnostic payloads unchanged. Server-authored errors and downloadable request/response examples are diagnostic content: requests carry `Accept-Language`, but untranslated upstream errors remain verbatim so operators can troubleshoot them. UI labels and operation descriptions are localized independently.

`npm run e2e` exercises browser behavior with fixtures. Separately run `python3 ../scripts/test-dashboard-deployment.py` against the Docker UI for real nginx/API routing, JSON response shapes, SPA route boundaries, and pull-method handling. It does not download models or change settings.

Pulls accept a full Hugging Face repository ID (for example `Qwen/Qwen3-8B-GGUF`) or its repository-page URL. Short names without an owner are rejected with guidance instead of guessing a repository. Public repositories need no API token; private or gated repositories require a valid token.

The Compose deployment defaults to a 2,048-token context so a model with a large trained context does not allocate its full KV cache in a small Docker VM. Override `SHARPAI_CONTEXT_SIZE` when starting Compose if your available memory supports a larger context. This deployment setting does not change the library's model-default context behavior.

The Observability view reads `/engine-metrics`, proxied to the collector's Prometheus exporter, while `/metrics` remains the server's HTTP instrumentation endpoint. Without a collector the engine cards report unavailable instead of presenting false zeroes. For the full stack, add `--engine-metrics` to the deployment smoke command.

### Chat and model transfers

Playground selects installed models from a dropdown and retains conversation turns until a new conversation,
model change, or navigation. It renders Markdown without enabling raw HTML and shows per-turn visible-output
TTFT, total request time, streaming time, input/output/total tokens, and engine generation tokens per second.
Times are measured by the browser; token counts come from the terminal Ollama event and remain unavailable
when a stream is interrupted. Hidden reasoning tokens are included in the engine output count; the server does
not currently report a separate reasoning-token count. Sampling controls are request-scoped and blank values
use server defaults. Context/memory settings remain in Settings. Pull progress includes transfer bytes,
percentage when known, the current file, and a clear completion or error state; the HTTP stream remains NDJSON
for Ollama clients. Closing the dialog leaves the server-side transfer running.

For initial administrator credentials and enabling sign-in, see
[Dashboard sign-in credentials](../DEPLOYMENT_GUIDE.md#dashboard-sign-in-credentials).

The server exposes Ollama `/api/chat`, `/api/generate`, `/api/embed`, and model management operations,
and OpenAI-compatible `/v1/models`, `/v1/chat/completions`, `/v1/completions`, and `/v1/embeddings`.
This is inference compatibility, not the full OpenAI hosted-service API (for example, `/v1/responses`
is not implemented). To verify both chat protocols against an installed completion model, run:

```sh
python3 scripts/test-playground-api.py --model Qwen/Qwen3-8B-GGUF
```

Run this from the repository root. It checks actual streaming and non-streaming replies, terminal markers,
and input/output token usage through the UI's nginx proxy. Set `SHARPAI_API_KEY` when authentication requires it.

### Administration

The sidebar's Administration group includes Tenants, Users, Credentials, Roles, Permissions, Assignments,
Sessions, and Security audit. Use the Selected tenant dropdown to scope administration. Platform tenant
management is restricted to platform administrators; other principals see only permitted surfaces in their
own tenant. Forms are purpose-built, password fields are write-only, and credential secrets are displayed
once. See [the operator guide](../docs/DASHBOARD_ADMINISTRATION.md) for setup, role grants, and test commands.
