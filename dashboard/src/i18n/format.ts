// Shared, locale-aware formatting helpers. Every formatter takes an explicit locale (never relies on the
// ambient default) so display formatting is deterministic and testable, per the i18n formatting policy.

export function formatNumber(value: number, locale: string): string {
  return new Intl.NumberFormat(locale).format(value);
}

export function formatPercent(fraction: number, locale: string): string {
  return new Intl.NumberFormat(locale, { style: 'percent', maximumFractionDigits: 0 }).format(fraction);
}

export function formatDateTime(value: string | number | Date, locale: string): string {
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return String(value);
  return new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(date);
}

export function formatDurationMs(ms: number, locale: string): string {
  if (ms < 1000) return `${formatNumber(Math.round(ms), locale)} ms`;
  return `${formatNumber(Math.round(ms) / 1000, locale)} s`;
}

export function formatBytes(bytes: number, locale: string): string {
  if (bytes <= 0) return `${formatNumber(0, locale)} B`;
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const exponent = Math.min(units.length - 1, Math.floor(Math.log(bytes) / Math.log(1024)));
  const value = bytes / Math.pow(1024, exponent);
  return `${new Intl.NumberFormat(locale, { maximumFractionDigits: 1 }).format(value)} ${units[exponent]}`;
}

export function formatList(items: string[], locale: string): string {
  return new Intl.ListFormat(locale, { style: 'long', type: 'conjunction' }).format(items);
}
