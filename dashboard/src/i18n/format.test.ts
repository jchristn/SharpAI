import { describe, expect, it } from 'vitest';
import { formatBytes, formatDurationMs, formatList, formatNumber, formatPercent } from './format';
import { isSupportedLocale, localeMeta } from './locales';

describe('locale-aware formatters', () => {
  it('formats numbers and percentages for a locale', () => {
    expect(formatNumber(1234567, 'en-US')).toBe('1,234,567');
    expect(formatPercent(0.5, 'en-US')).toBe('50%');
  });

  it('formats byte sizes and durations', () => {
    expect(formatBytes(1536, 'en-US')).toBe('1.5 KB');
    expect(formatDurationMs(500, 'en-US')).toBe('500 ms');
    expect(formatDurationMs(1500, 'en-US')).toBe('1.5 s');
  });

  it('joins lists with Intl.ListFormat', () => {
    expect(formatList(['a', 'b'], 'en-US')).toBe('a and b');
  });
});

describe('locale registry', () => {
  it('reports text direction and support', () => {
    expect(localeMeta('ar').dir).toBe('rtl');
    expect(localeMeta('en').dir).toBe('ltr');
    expect(isSupportedLocale('en')).toBe(true);
    expect(isSupportedLocale('zz')).toBe(false);
  });
});
