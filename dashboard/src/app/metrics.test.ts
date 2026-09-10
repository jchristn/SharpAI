import { describe, expect, it } from 'vitest';
import { averageLatencyMs, parseSample, ratePerSecond, sumMetric } from './metrics';

const SAMPLE = `# HELP sharpai_inference_requests_total Inference requests processed.
# TYPE sharpai_inference_requests_total counter
sharpai_inference_requests_total{operation="chat",model="a"} 40
sharpai_inference_requests_total{operation="embed"} 2
# TYPE sharpai_inference_tokens_generated_total counter
sharpai_inference_tokens_generated_total 1000
# TYPE sharpai_models_resident gauge
sharpai_models_resident 3
# TYPE sharpai_inference_latency_seconds histogram
sharpai_inference_latency_seconds_sum 21
sharpai_inference_latency_seconds_count 42
sharpai_inference_latency_seconds_bucket{le="0.5"} 30
`;

describe('metrics parser', () => {
  it('sums a counter family across label sets', () => {
    expect(sumMetric(SAMPLE, 'sharpai_inference_requests_total')).toBe(42);
  });

  it('does not match a longer metric name by prefix', () => {
    // _count must not be captured when asking for the histogram base name.
    expect(sumMetric(SAMPLE, 'sharpai_inference_latency_seconds')).toBe(0);
  });

  it('parses the sample of interest and computes average latency', () => {
    const sample = parseSample(SAMPLE);
    expect(sample.requests).toBe(42);
    expect(sample.tokens).toBe(1000);
    expect(sample.resident).toBe(3);
    expect(averageLatencyMs(sample)).toBe(500); // 21s / 42 = 0.5s = 500ms
  });

  it('computes a per-second rate and guards against counter resets', () => {
    expect(ratePerSecond(100, 150, 5)).toBe(10);
    expect(ratePerSecond(150, 100, 5)).toBe(0); // reset -> 0, not negative
    expect(ratePerSecond(0, 10, 0)).toBe(0); // no interval -> 0
  });
});
