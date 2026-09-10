import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import en from './en.json';
import ar from './ar.json';
import { DEFAULT_LOCALE, isSupportedLocale, localeMeta } from './locales';

const STORAGE_KEY = 'sharpai.locale';

type Catalog = { [key: string]: string | Catalog };

// Generate a pseudo-locale from the source catalog for expansion/RTL testing: accent letters and pad the
// string so layout issues surface early, without maintaining a separate hand-authored catalog.
function pseudo(node: Catalog): Catalog {
  const out: Catalog = {};
  for (const [key, value] of Object.entries(node)) {
    out[key] = typeof value === 'string' ? accent(value) : pseudo(value);
  }
  return out;
}

function accent(text: string): string {
  const map: Record<string, string> = { a: 'á', e: 'é', i: 'í', o: 'ó', u: 'ú', A: 'Á', E: 'É', O: 'Ó' };
  const body = text.replace(/[aeiouAEO]/g, (c) => map[c] ?? c);
  return `⟦${body}··⟧`;
}

/** Deterministic locale resolution: persisted selection, then browser preference, then default fallback. */
function detectLocale(): string {
  try {
    const stored = window.localStorage.getItem(STORAGE_KEY);
    if (stored && isSupportedLocale(stored)) return stored;
  } catch {
    // ignore storage errors
  }
  if (typeof navigator !== 'undefined' && navigator.language) {
    if (isSupportedLocale(navigator.language)) return navigator.language;
    const base = navigator.language.split('-')[0];
    if (isSupportedLocale(base)) return base;
  }
  return DEFAULT_LOCALE;
}

function applyDocument(code: string): void {
  const meta = localeMeta(code);
  if (typeof document !== 'undefined') {
    document.documentElement.lang = code;
    document.documentElement.dir = meta.dir;
  }
}

const initialLocale = detectLocale();

void i18n.use(initReactI18next).init({
  resources: {
    en: { translation: en as Catalog },
    ar: { translation: ar as Catalog },
    'en-XA': { translation: pseudo(en as Catalog) },
  },
  lng: initialLocale,
  fallbackLng: DEFAULT_LOCALE,
  interpolation: { escapeValue: false },
});

applyDocument(initialLocale);

/** Switch the active locale: change i18next, persist, and update document lang/dir centrally. */
export function setActiveLocale(code: string): void {
  if (!isSupportedLocale(code)) return;
  void i18n.changeLanguage(code);
  try {
    window.localStorage.setItem(STORAGE_KEY, code);
  } catch {
    // ignore storage errors
  }
  applyDocument(code);
}

export default i18n;
