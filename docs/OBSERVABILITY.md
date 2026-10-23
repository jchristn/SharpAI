# Observability runbook

SharpAI uses the .NET metrics/activity APIs, Radiant, and Watson 7.3.2. The bundled stack collects
engine metrics, traces, and JSON logs in Prometheus, Tempo, and Loki and displays them in Grafana.
See [the stack guide](../docker/telemetry/README.md) for service wiring and persistence.

## Signal paths

| Signal | Source and destination |
| --- | --- |
| Engine metrics | `SharpAI.Inference` and `SharpAI.Models` meters through Radiant/OTLP to the collector's Prometheus exporter on port 8889 |
| HTTP metrics | Watson instruments exposed by the authenticated server `/metrics` route on the API port |
| Traces | Inference/model-load and Watson activity sources through OTLP to Tempo |
| Application logs | JSON rolling files tailed by the collector's filelog receiver into Loki; OTLP logging is optional |

Prometheus scrapes both `otel-collector:8889` and `sharpai:8000/metrics` in the bundled configuration.
The server's `/metrics` reports HTTP measurements; it is not the collector's engine-metrics endpoint.
Missing engine series are unavailable, not zero. An idle process may not have emitted every series yet.

## Configuration

Merge this object into `sharpai.json`; use a collector hostname reachable from the server:

```json
{
  "Telemetry": {
    "Enable": true,
    "ServiceName": "sharpai",
    "OtlpEndpoint": "http://otel-collector:4317",
    "OtlpProtocol": "grpc",
    "PrometheusEnable": false,
    "PrometheusPort": 9464,
    "PrometheusPath": "/metrics",
    "EnableMetrics": true,
    "EnableTraces": true,
    "EnableLogs": false
  }
}
```

The server accepts `grpc` or `httpprotobuf` for `OtlpProtocol`; other values fall back to gRPC.
The OTLP endpoint must be nonempty. Use port 4318 with `httpprotobuf` when your collector exposes
OTLP/HTTP. Keep `ServiceName` stable for consistent `service.name` grouping.

`Telemetry.Enable` controls the Radiant host. `Rest.Telemetry.Enable` controls Watson HTTP measurement;
the server enables it when the engine telemetry host is configured on. An explicitly enabled
`Rest.Telemetry.Enable` can provide HTTP metrics while the engine host is off, as in the
[REST validation harness](../scripts/test-rest-reference.py).

`Telemetry.PrometheusEnable` optionally starts a separate Radiant Prometheus listener at
`PrometheusHostname:PrometheusPort/PrometheusPath`. It does not move the authenticated API `/metrics`
route or replace OTLP configuration. It is off in the bundled stack. The server disables Watson's
built-in pre-routing scrape handler and serves HTTP metrics through its authenticated route instead.

For an external monitoring system, configure its collector as `OtlpEndpoint` and run only the server/web
services or the CPU/CUDA Compose variant. Keep OTLP configured even if you additionally enable the
Radiant scrape listener; the current host enables its OTLP exporter when started. Network access to a
separate collector/exporter is configured independently of SharpAI API bearer authentication.

## Engine metrics

The names below follow the bundled collector's Prometheus suffixing. Histograms also expose
`_bucket`, `_sum`, and `_count`; inspect the actual scrape's HELP/TYPE records when using another exporter.

| .NET instrument | Prometheus base name | Type / unit | Meaning |
| --- | --- | --- | --- |
| `sharpai.inference.requests` | `sharpai_inference_requests_total` | Counter / calls | Measured generation calls, tagged by operation/model/outcome |
| `sharpai.inference.tokens_generated` | `sharpai_inference_tokens_generated_total` | Counter / tokens | Sampled output tokens before text filtering |
| `sharpai.inference.latency` | `sharpai_inference_latency_seconds` | Histogram / seconds | Measured engine generation lifetime |
| `sharpai.models.resident` | `sharpai_models_resident` | Gauge / models | Resident model count |
| `sharpai.models.load_duration` | `sharpai_models_load_duration_seconds` | Histogram / seconds | Model load duration |
| `sharpai.inference.queue_wait` | `sharpai_inference_queue_wait_seconds` | Histogram / seconds | Generation-slot wait |
| `sharpai.inference.time_to_first_token` | `sharpai_inference_time_to_first_token_seconds` | Histogram / seconds | Engine TTFT, including slot wait |
| `sharpai.inference.prompt_eval_duration` | `sharpai_inference_prompt_eval_duration_seconds` | Histogram / seconds | Prompt evaluation phase |
| `sharpai.inference.generation_duration` | `sharpai_inference_generation_duration_seconds` | Histogram / seconds | Generation phase |
| `sharpai.inference.prompt_tokens_per_second` | `sharpai_inference_prompt_tokens_per_second` | Histogram / tokens per second | Per-call prompt rate |
| `sharpai.inference.generation_tokens_per_second` | `sharpai_inference_generation_tokens_per_second` | Histogram / tokens per second | Per-call generation rate |
| `sharpai.inference.cancellations` | `sharpai_inference_cancellations_total` | Counter / calls | Cancelled measured generations |
| `sharpai.inference.busy_slots` | `sharpai_inference_busy_slots` | Gauge / slots | Occupied generation slots |
| `sharpai.inference.total_slots` | `sharpai_inference_total_slots` | Gauge / slots | Configured model slots |

The measured generation path emits engine phase instruments; they are not a count of every HTTP
request or a guarantee of embedding phase instrumentation. Use HTTP metrics/history for all API traffic,
and the embedding history filter for embedding requests. Phase values can be approximate backend-step
wall-clock measurements. They overlap and should not be added as a waterfall. See
[history timing semantics](HISTORY_TELEMETRY.md) and [inference operations](OPERATIONS.md).

Useful PromQL examples:

```promql
# Generation calls per second
sum(rate(sharpai_inference_requests_total[5m]))

# Aggregate generated tokens per second
sum(rate(sharpai_inference_tokens_generated_total[5m]))

# p95 measured generation lifetime
histogram_quantile(0.95, sum by (le) (rate(sharpai_inference_latency_seconds_bucket[5m])))

# Resident models
sharpai_models_resident
```

Dividing token rate by call rate yields tokens per call, not tokens per second. Use the aggregate token
counter rate above for throughput, or the generation-rate histogram sum/count for mean per-call speed.

## Traces, logs, and dashboards

Inference spans use activity source `SharpAI.Inference` and include operation/model, measured tokens,
phase durations, outcome, timing source, and trace/span IDs. Model loads have separate spans. History
captures trace identity when available, so the history inspector and structured completion logs can
help correlate a slow call with Tempo. See [JSON logging](LOGGING.md) for event fields and chunk assembly.

Grafana runs at `http://localhost:9400` in the supplied stack and provisions three dashboards in the
**SharpAI** folder:

- **SharpAI ; Overview:** HTTP traffic/errors, latency, resident models, and token throughput.
- **SharpAI ; Engine operations:** TTFT, queue/load phases, generation rates, slots, cancellations,
  and correlated operation logs.
- **SharpAI ; Logs:** application log exploration through Loki.

The web Observability page links to the configured services and uses its engine-metrics proxy. The TUI
uses its `EngineMetrics` profile URL separately from the API `/metrics` route. Configure a collector
endpoint reachable from the client when connecting remotely; do not substitute HTTP metrics for engine
measurements. Missing series are displayed as unavailable.

## Authentication and validation

The SharpAI API `/metrics`, `/openapi.json`, and the dashboard `/engine-metrics` proxy require a bearer.
Compose Prometheus reads `docker/auth/default-bearer-token` through a read-only mount. Replace that
configured credential if it is revoked. Health and readiness remain anonymous. Grafana, Loki, Tempo,
Prometheus, and collector endpoints have their own deployment access configuration.

[API evidence](api/validation.json) records successful authenticated HTTP metrics with nonzero request
measurements on .NET 8 and .NET 10. The [TUI telemetry bundle](tui/telemetry.json) records a historical
isolated client/Collector/Prometheus/Loki/Tempo/Grafana run; it is not a fresh full-stack run for this checkout.
The older engine-stack scripts `test-observability.py` and `test-hardware-memory.py` still assume open
server access and require authentication updates before they can reproduce their older results against
the current server. See [verification status](OPERATIONS.md#current-verification-paths).
