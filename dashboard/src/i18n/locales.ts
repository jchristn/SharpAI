export type TextDirection = 'ltr' | 'rtl';
export interface LocaleMeta { code: string; english: string; native: string; dir: TextDirection; fallback: string; }
export const DEFAULT_LOCALE = 'en';
export const LOCALES: LocaleMeta[] = [
  ['en','English','English'], ['es','Spanish','Español'], ['pt','Portuguese','Português'],
  ['fr','French','Français'], ['it','Italian','Italiano'], ['de','German','Deutsch'],
  ['zh','Chinese (Simplified)','简体中文'], ['ar','Arabic','العربية'], ['ru','Russian','Русский'],
  ['ms','Malay','Bahasa Melayu'], ['hi','Hindi','हिन्दी'], ['ja','Japanese','日本語'],
  ['en-XA','Pseudo (expansion)','[Expanded]'], ['ar-XB','Pseudo (RTL)','[RTL]'],
].map(([code,english,native]) => ({code,english,native,dir:code === 'ar' || code === 'ar-XB' ? 'rtl' : 'ltr',fallback:'en'}));
export function canonicalLocale(code: string): string {
  const normalized = code.replaceAll('_','-');
  return LOCALES.find(locale => locale.code.toLowerCase() === normalized.toLowerCase())?.code
    ?? LOCALES.find(locale => locale.code === normalized.split('-')[0].toLowerCase())?.code ?? DEFAULT_LOCALE;
}
export function localeMeta(code: string): LocaleMeta { return LOCALES.find(locale => locale.code === canonicalLocale(code)) ?? LOCALES[0]; }
export function isSupportedLocale(code: string): boolean { return LOCALES.some(locale => locale.code === code); }
