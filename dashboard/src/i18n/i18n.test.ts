import { describe, expect, it } from 'vitest';
import en from './en.json';
import ar from './ar.json';
import i18n, { setActiveLocale } from './config';
import { localeMeta } from './locales';

type Catalog = { [key: string]: string | Catalog };

function flatten(node: Catalog, prefix = ''): string[] {
  const keys: string[] = [];
  for (const [key, value] of Object.entries(node)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (typeof value === 'string') keys.push(path);
    else keys.push(...flatten(value, path));
  }
  return keys;
}

describe('i18n catalogs', () => {
  it('has no orphaned keys in a translation catalog (every ar key exists in the en source)', () => {
    const enKeys = new Set(flatten(en as Catalog));
    const orphans = flatten(ar as Catalog).filter((key) => !enKeys.has(key));
    expect(orphans).toEqual([]);
  });

  it('exposes the required top-level namespaces in the source catalog', () => {
    for (const ns of ['app', 'nav', 'topbar', 'home', 'models', 'requestHistory', 'settings', 'login']) {
      expect(Object.prototype.hasOwnProperty.call(en, ns)).toBe(true);
    }
  });
});

describe('locale switching', () => {
  it('applies text direction to the document when the locale changes', () => {
    setActiveLocale('ar');
    expect(localeMeta('ar').dir).toBe('rtl');
    expect(document.documentElement.dir).toBe('rtl');
    expect(document.documentElement.lang).toBe('ar');

    setActiveLocale('en');
    expect(document.documentElement.dir).toBe('ltr');
    expect(document.documentElement.lang).toBe('en');
    expect(i18n.language).toBe('en');
  });
});
