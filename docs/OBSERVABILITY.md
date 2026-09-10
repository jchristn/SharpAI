# Observability runbook

SharpAI emits metrics, traces, and logs through the standard .NET telemetry APIs, hosted in-process by
[Radiant](https://www.nuget.org/packages/Radiant) and by Watson 7.1's built-in webserver meter. This
document explains what the signals mean, how to read the bundled Grafana dashboards, and how to send the
data to your own monitoring system instead of the shipped stack. For the topology of the local
Prometheus + Loki + Tempo + Grafana stack, see [`docker/telemetry/README.md`](../docker/telemetry/README.md).

## Turning it on

Telemetry is configured under the `Telemetry` block in `sharpai.json`:

```json
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
```

With `Enable` true, the server pushes metrics and traces over OTLP to `OtlpEndpoint`. Independently, the
Watson webserver exposes a Prometheus exposition at `/metrics` on the API port for direct scraping.

## Metrics catalog

Metric names use dots in the .NET/OTLP world and underscores in Prometheus; counters gain a `_total`
suffix and histograms expand into `_bucket`/`_sum`/`_count` series. Both forms are listed below.

| .NET / OTLP name | Prometheus name | Type | Meaning |
|---|---|---|---|
| `sharpai.inference.requests` | `sharpai_inference_requests_total` | counter | Inference requests processed (chat, completion, embeddings). Tagged by operation and model. |
| `sharpai.inference.tokens_generated` | `sharpai_inference_tokens_generated_total` | counter | Tokens produced during generation. Divide the rate by the request rate for tokens/sec. |
| `sharpai.inference.latency` | `sharpai_inference_latency_seconds` | histogram (seconds) | End-to-end inference latency. Use `_bucket` for quantiles, `_sum`/`_count` for the average. |
| `sharpai.models.resident` | `sharpai_models_resident` | gauge | Number of models currently loaded in memory. |

The Watson meter adds HTTP-level series (request counts and durations by route and status) at `/metrics`,
so you get server-wide traffic and error rates alongside the inference-specific metrics above.

### Useful queries

```promql
# Requests per second
rate(sharpai_inference_requests_total[5m])

# Tokens per second
rate(sharpai_inference_tokens_generated_total[5m])

# p95 inference latency (seconds)
histogram_quantile(0.95, sum(rate(sharpai_inference_latency_seconds_bucket[5m])) by (le))

# Resident models right now
sharpai_models_resident
```

## Traces

Inference operations open an `Activity` on the `SharpAI.Inference` source, tagged with
`sharpai.operation` and `sharpai.model`. Traces are exported over OTLP to Tempo; open a request in Grafana
Explore (Tempo datasource) to see the span and its tags. Because the request-history pipeline records the
same requests, you can correlate a slow span with its captured request in the dashboard.

## Reading the Grafana dashboards

The stack provisions two dashboards (Grafana at `http://localhost:9400`, anonymous admin):

- **SharpAI Overview** — request rate, inference latency, tokens/sec, and resident models over time. This
  is the day-to-day operations view: watch latency and the resident-model count as load changes, and use
  the request-rate panel to confirm traffic is reaching the server.
- **SharpAI Logs** — a live tail of the server logs from Loki, filterable by level. Use it to pull the
  error lines behind a latency spike or a failed pull.

The dashboard's own **Observability** page links straight to Grafana and to the raw `/metrics` endpoint.

## Pointing at an external collector

You do not need the bundled stack. To ship to your own observability platform:

1. Set `Telemetry.OtlpEndpoint` (and `OtlpProtocol`, `grpc` or `http/protobuf`) to your OTLP collector or
   a vendor endpoint that accepts OTLP. Set `EnableLogs` true if you also want logs over OTLP.
2. Do not start the `docker/compose.yaml` telemetry services — run only the SharpAI container (or use
   `docker/compose-cpu.yaml` / `docker/compose-cuda.yaml`, which omit the stack).
3. Alternatively, for a pull-based setup, leave OTLP unset and enable `PrometheusEnable`; point your own
   Prometheus at the server's `/metrics` (API port) and `PrometheusPort` exposition.

Keep `ServiceName` stable so the `service.name` resource attribute groups this deployment consistently in
your backend.
