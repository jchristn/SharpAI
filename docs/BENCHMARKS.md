# SharpAI performance benchmarks

This document describes how SharpAI's generation throughput and latency are measured, and records a
reference run. The benchmark is reproducible from the repository — it is not a marketing number, and
the harness is committed alongside the tests.

## Running the benchmark

The benchmark lives in the Touchstone test harness and runs against a real GGUF model. Provide a model
(see [model-gated tests](../src/CLAUDE.md)) via the `SHARPAI_TEST_MODEL_GGUF` environment variable or a
`test-models/` directory, then:

```bash
cd src
dotnet run --project Test.Automated -c Release -f net8.0 -- \
  --benchmark --iterations 8 --concurrency 4 --max-tokens 64
```

Flags: `--iterations N` (generations per phase), `--concurrency N` (parallel decode slots for the
concurrent phase), `--max-tokens N` (tokens generated per request). The harness loads the model, runs a
warm-up generation (excluded from timing), then measures two phases:

- **Sequential** — `iterations` generations one at a time. Reports average per-request latency and
  aggregate generated tokens/second.
- **Concurrent** — the same `iterations` generations submitted at once with `concurrency` decode slots.
  Reports wall-clock time, aggregate tokens/second, and the wall-clock speedup over the sequential phase.

## Reference run

Model: `Qwen2.5-1.5B-Instruct` (Q4_K_M, ~1.1 GB). Backend: **CPU** (LLamaSharp CPU backend). Commodity
laptop. `--iterations 6 --concurrency 3 --max-tokens 48`.

| Phase | Wall time | Avg latency | Throughput |
|---|---|---|---|
| Sequential (1 at a time) | 25.6 s | ~4.26 s/req | ~11.3 tok/s |
| Concurrent (3 slots) | 21.3 s | — | ~13.5 tok/s |

Concurrent wall-clock speedup: **~1.20×**.

### Reading the result

On a **CPU** backend, generation is compute-bound and all decode slots share the same cores, so parallel
decode yields a modest speedup (here ~1.2×) rather than a linear one — the cores are already saturated by
a single stream. The concurrency win is larger when requests spend time waiting (e.g. staggered arrivals)
and on GPU backends where a single stream does not saturate the device. The value of multiple slots on CPU
is primarily **latency fairness under load** (no request is fully blocked behind another) rather than raw
throughput.

## Concurrency safety

Each concurrent generation runs on its **own** `StatelessExecutor` (hence its own context / KV-cache) when
more than one slot is configured (`SHARPAI_MAX_CONCURRENT_GENERATIONS > 1`); the single shared executor is
used only in the default serialized (one-slot) configuration. This isolation is what makes multi-slot
generation safe — an earlier implementation shared one executor across slots and raced on its internal
context. A regression test (`ModelLifecycle/ParallelSlots`) fires three concurrent generations with three
slots and requires all to complete without error.

True continuous batching (llama.cpp `BatchedExecutor`, sharing one context across streams with far lower
per-stream memory) remains a future optimization.
