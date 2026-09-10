// Locale registry (BCP 47). Each entry carries the English name, the native autonym shown in the selector,
// text direction, and a fallback. Adding a locale here + a catalog is all that's needed — no logic changes.

export type TextDirection = 'ltr' | 'rtl';

export interface LocaleMeta {
  code: string;
  english: string;
  native: string;
  dir: TextDirection;
  fallback: string;
}

export const DEFAULT_LOCALE = 'en';

export const LOCALES: LocaleMeta[] = [
  { code: 'en', english: 'English', native: 'English', dir: 'ltr', fallback: 'en' },
  { code: 'ar', english: 'Arabic', native: 'العربية', dir: 'rtl', fallback: 'en' },
  { code: 'en-XA', english: 'Pseudo (expansion/RTL testing)', native: '[Ṕśéúdó]', dir: 'ltr', fallback: 'en' },
];

export function localeMeta(code: string): LocaleMeta {
  return LOCALES.find((locale) => locale.code === code) ?? LOCALES[0];
}

export function isSupportedLocale(code: string): boolean {
  return LOCALES.some((locale) => locale.code === code);
}
