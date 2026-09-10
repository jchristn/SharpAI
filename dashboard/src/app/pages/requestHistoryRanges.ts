/**
 * Canonical dashboard time-range presets and their chart bucket sizes.
 *
 * The bucket sizes match the rest of the product family (pneuma / assistanthub / conductor / wilson):
 *   last hour  -> 60s   buckets (1 minute)
 *   last day   -> 900s  buckets (15 minutes)
 *   last week  -> 7200s buckets (2 hours)
 *   last month -> 86400s buckets (24 hours / 1 day)
 *
 * The server summary endpoint buckets by whole minutes (bucketMinutes), so the second values are
 * expressed here as minutes: 1, 15, 120, 1440.
 */
export interface TimeRangePreset {
  key: string;
  labelKey: string;
  rangeMs: number;
  bucketMinutes: number;
}

export const TIME_RANGES: TimeRangePreset[] = [
  { key: 'lastHour', labelKey: 'requestHistory.rangeHour', rangeMs: 60 * 60 * 1000, bucketMinutes: 1 },
  { key: 'lastDay', labelKey: 'requestHistory.rangeDay', rangeMs: 24 * 60 * 60 * 1000, bucketMinutes: 15 },
  { key: 'lastWeek', labelKey: 'requestHistory.rangeWeek', rangeMs: 7 * 24 * 60 * 60 * 1000, bucketMinutes: 120 },
  { key: 'lastMonth', labelKey: 'requestHistory.rangeMonth', rangeMs: 30 * 24 * 60 * 60 * 1000, bucketMinutes: 1440 },
];

/** Default preset, matching the product family (last 24 hours). */
export const DEFAULT_RANGE_KEY = 'lastDay';

export function rangeByKey(key: string): TimeRangePreset {
  return TIME_RANGES.find((r) => r.key === key) ?? TIME_RANGES[1];
}
