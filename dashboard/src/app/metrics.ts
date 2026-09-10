// Minimal parser for the Prometheus text exposition format. Enough to pull a few named series out of the
// server's /metrics endpoint for the in-app observability summary — not a general-purpose implementation.

/** Sum the values of a metric family (all label sets) by exact metric name. */
export function sumMetric(text: string, name: string): number {
  let total = 0;
  for (const line of text.split('\n')) {
    if (line.length === 0 || line.charCodeAt(0) === 35 /* # */) continue;
    // A sample line is `name` or `name{labels}` followed by whitespace and a value.
    if (!line.startsWith(name)) continue;
    const after = line.charAt(name.length);
    if (after !== '' && after !== ' ' && after !== '{' && after !== '\t') continue;
    const value = Number(line.trim().split(/\s+/).pop());
    if (Number.isFinite(value)) total += value;
  }
  return total;
}

export interface MetricsSample {
  requests: number;
  tokens: number;
  resident: number;
  latencySum: number;
  latencyCount: number;
}

/** Extract the SharpAI metrics of interest from a /metrics exposition. */
export function parseSample(text: string): MetricsSample {
  return {
    requests: sumMetric(text, 'sharpai_inference_requests_total'),
    tokens: sumMetric(text, 'sharpai_inference_tokens_generated_total'),
    resident: sumMetric(text, 'sharpai_models_resident'),
    latencySum: sumMetric(text, 'sharpai_inference_latency_seconds_sum'),
    latencyCount: sumMetric(text, 'sharpai_inference_latency_seconds_count'),
  };
}

/** Average latency in milliseconds across all recorded inferences, or null when none. */
export function averageLatencyMs(sample: MetricsSample): number | null {
  if (sample.latencyCount <= 0) return null;
  return (sample.latencySum / sample.latencyCount) * 1000;
}

/** Per-second rate between two counter samples taken `seconds` apart. */
export function ratePerSecond(previous: number, current: number, seconds: number): number {
  if (seconds <= 0 || current < previous) return 0;
  return (current - previous) / seconds;
}
