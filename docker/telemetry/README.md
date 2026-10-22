# SharpAI observability stack

This directory holds the configuration for the Prometheus + Loki + Grafana + Tempo stack
that ships with the v4.0.1 server/dashboard in `docker/compose.yaml`. It gives an operator a live view of how a SharpAI
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

The bundled Compose configuration enables telemetry. Both core meters (`SharpAI.Inference`
and `SharpAI.Models`) and inference/load activities are subscribed by the server. Engine
metrics reach Prometheus through the collector; Watson HTTP metrics use the app's `/metrics`.
The separate Radiant Prometheus listener is disabled in Compose because it is unnecessary here.

The **SharpAI — Engine operations** dashboard shows TTFT, queue wait, prompt/generation
rates, occupied/configured slots, model-load timings and cancellations. Phase metrics include
`timing_source`; `backend_step_wall_clock` is approximate. Model-load p95 covers observations
since process start; request panels use rates over the selected interval.

Engine completion and model-load logs include JSON `trace_id`/`span_id` fields without request
text. Loki derived fields link to Tempo, and Tempo maps `service.name` to Loki's `service_name`
for trace-to-log navigation. Trace IDs remain log content rather than indexed stream labels.

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
      sharpai-engine.json        # engine phase metrics, slots, cancellations, correlated logs
      sharpai-logs.json          # all logs + warning/error filter
```

None of the backends use named volumes, so `docker compose down --volumes` discards their state
cleanly — the reset flow in `docker/factory/` needs no special handling for them.

## Pointing at an external collector

To send telemetry to your own OpenTelemetry Collector or a vendor backend instead of the bundled
stack, set `SHARPAI_TELEMETRY_OTLP_ENDPOINT` on the `sharpai` service and drop the observability
services from the compose file. The server does not care what is on the other end of the OTLP
endpoint.

## Verify the complete path

Build the current checkout and run an isolated stack against real local weights:

```sh
dotnet build src/SharpAI.sln
python3 scripts/test-observability.py --coverage --model /path/model.gguf --output /tmp/sharpai-observability
```

This starts the real collector, Prometheus, Loki, Tempo and Grafana on isolated ports, runs
live server contracts, executes every engine metric panel through Grafana, checks Loki/Tempo
correlation, and saves direct server coverage. It uses the pinned local `dotnet-coverage` tool
and static instrumentation of temporary assembly copies, including on macOS ARM64.
Add `--expected-backend metal` or `cuda` to require native log evidence of GPU offload.
The harness removes its containers and preserves evidence in the output directory.

## First start and updates

From `docker/`, run `./update.sh`. It creates a missing `sharpai.db` file before Compose mounts it.
For manual Compose startup, create the file first with `touch sharpai.db`. Existing databases are
preserved; a directory at that path is rejected. The UI waits for the server's `/ready` health check,
so inspect `docker logs sharpai` if `sharpai-ui` stays in `Created`.
