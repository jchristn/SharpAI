# Request and inference history

The v4.0.1 dashboard fetches a metadata-only page from `/v1.0/api/request-history` and loads a complete record from `/v1.0/api/request-history/{id}` when a row is opened. Request and inference pages put UTC start time first. Inference rows include duration meters relative to the longest request on the current page. The large inspector separates identity, timings, token counts, headers and payloads rather than showing the record as one JSON document.

Schema migration 6 adds nullable typed columns for model, streaming state, input/output/cached tokens, TTFT, stream duration, queue/load/prompt/generation/engine durations, cancellation, timing approximation and trace/span IDs. The migration is defined for SQLite, PostgreSQL, MySQL and SQL Server. Existing rows retain null for unavailable measurements. The current engine does not report cached tokens, so that field remains null rather than a fabricated zero. Body-bearing detail responses remain distinct from lightweight list responses.

Engine trace/span IDs are copied into the measurement accumulator before the activity is disposed, so post-routing persistence does not depend on `Activity.Current`.

Engine phases come from the same request-owned `InferenceStatistics` used in response envelopes, Prometheus metrics, Tempo spans and engine completion logs. TTFT is measured inside the engine before output filtering, starting before generation-slot acquisition. It does not include earlier model loading or HTTP overhead. Stream duration is the remaining HTTP duration after that engine TTFT, so it includes transport and earlier unmeasured overhead. Phases may overlap and must not be summed as a waterfall. `PhaseTimingsApproximate` identifies backend-step wall-clock estimates instead of native counters. Requests that fail before engine acquisition, and embeddings without an engine phase accumulator, still record HTTP duration but leave engine counters unavailable.

Capture retains successfully emitted Ollama chunks and OpenAI SSE data with a bounded per-request buffer. Non-streaming responses use Watson's retained response data. `RequestHistory.MaxRequestBodyBytes` and `MaxResponseBodyBytes` bound retained payloads; zero disables retention. Original byte counts and truncation are displayed. Retention follows `RetentionDays`. Capture redacts passwords, bearer/session tokens, signing keys, API keys and credential secrets in JSON, and sensitive headers, before retaining content. Malformed security/settings bodies are withheld. Model prompts and answers can remain in history, so access to history is privileged and should be granted deliberately.

Non-platform principals can only enumerate, summarize, read or delete history within their authenticated tenant. Explicit foreign tenant filters and foreign record IDs are rejected. Records with no tenant, including older records and historical system operations, remain platform-administrator-only. The current server requires bearer authentication; legacy auth-disabled settings do not open these endpoints.

## Views and filters

The web Request History workspace has API History, Inference History, and Embeddings History tabs.
The TUI exposes them as separate navigation items. All three use the same list and summary endpoints:
`category=generation` selects chat/completions, `category=embeddings` selects embeddings, and omitted
category selects all captured requests. Legacy `category=inference` includes both generation and
embeddings. Chart time presets use 1/15/120/1440-minute buckets for hour/day/week/month windows.

Both clients show successful/failed activity and fetch details separately from metadata pages. See
[REST history contracts](../REST_API.md#management-apis) for the full filters and delete semantics.

## Validation

`HistoryTelemetryTests` verifies typed field persistence across reopening the database, metadata-only lists, unavailable cached counts, parseable JSON logging and lossless long-message reconstruction. `scripts/test-administration.py` runs an isolated authenticated server and checks redaction on full details, including token responses and password headers. Browser tests exercise structured payloads, duration meters, full-detail fetching, locale help and streaming/non-streaming Playground requests. Live model checks cover Ollama and OpenAI chat with streaming enabled and disabled; live validation must inspect the resulting durable history as well as the response.
