# SharpAI observability stack

This directory holds the configuration for the Prometheus + Loki + Grafana + Tempo stack
that ships with `docker/compose.yaml`. It gives an operator a live view of how a SharpAI
deployment is behaving — request rate and latency, inference throughput, resident models,
and logs — without wiring up an external monitoring system first.

## What runs

| Service | Image | Host port | Role |
|---|---|---|---|
| otel-collector | `otel/opentelemetry-collector-contrib:0.109.0` | 4317/4318, 8889 | Receives OTLP metrics/traces from SharpAI; tails log files into Loki; exposes metrics for Prometheus. |
| prometheus | `prom/prometheus:v2.55.1` | 9090 | Scrapes the collector and the SharpAI `/metrics` endpoint. |
| loki | `grafana/loki:3.2.1` | 3100 | Log aggregation. |
| tempo | `grafana/tempo:2.6.1` | 3200 | Trace storage. |
| grafana | `grafana/grafana:11.3.0` | 9400 | Dashboards (anonymous admin, dark theme). |

## How data flows

The SharpAI server emits metrics and traces through the .NET telemetry APIs, hosted in-process
by [Radiant](https://www.nuget.org/packages/Radiant) and by Watson 7.2's built-in `"Watson"`
meter. Those are pushed over OTLP to the collector, which forwards metrics to Prometheus and
traces to Tempo. Logs take a different path: the server writes rolling files to `docker/logs/`,
the collector's `filelog` receiver tails them, and ships the lines to Loki. Grafana reads all
three through provisioned datasources with fixed UIDs (`prometheus`, `loki`, `tempo`) so the
bundled dashboards resolve without manual setup.

Logs work as soon as the server is writing files. Metrics and traces light up once the in-app
telemetry is enabled (plan workstream **W17**); until then the `sharpai` Prometheus target reads
as *down* and the metric panels show *No data*, which is expected.

## Layout

```
telemetry/
  otel-collector-config.yaml     # receivers, processors, exporters, pipelines
  prometheus.yaml                # scrape config (collector + app /metrics)
  loki-config.yaml               # single-binary filesystem Loki
  tempo.yaml                     # local trace storage
  grafana/
    provisioning/
      datasources/datasources.yaml   # Prometheus, Loki, Tempo (fixed UIDs)
      dashboards/dashboards.yaml      # file provider
    dashboards/
      sharpai-overview.json      # request rate/latency, tokens/sec, resident models, queue
      sharpai-logs.json          # all logs + warning/error filter
```

None of the backends use named volumes, so `docker compose down --volumes` discards their state
cleanly — the reset flow in `docker/factory/` needs no special handling for them.

## Pointing at an external collector

To send telemetry to your own OpenTelemetry Collector or a vendor backend instead of the bundled
stack, set `SHARPAI_TELEMETRY_OTLP_ENDPOINT` on the `sharpai` service and drop the observability
services from the compose file. The server does not care what is on the other end of the OTLP
endpoint.
