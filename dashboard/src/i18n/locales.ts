export type TextDirection = 'ltr' | 'rtl';
export interface LocaleMeta { code: string; english: string; native: string; dir: TextDirection; fallback: string; }
export const DEFAULT_LOCALE = 'en';
export const LOCALES: LocaleMeta[] = [
  {code:'en',english:'English',native:'English'},
  {code:'es',english:'Spanish',native:'Español'},
  {code:'pt',english:'Portuguese',native:'Português'},
  {code:'fr',english:'French',native:'Français'},
  {code:'it',english:'Italian',native:'Italiano'},
  {code:'de',english:'German',native:'Deutsch'},
  {code:'zh',english:'Chinese (Simplified)',native:'简体中文'},
  {code:'ar',english:'Arabic',native:'العربية'},
  {code:'ru',english:'Russian',native:'Русский'},
  {code:'ms',english:'Malay',native:'Bahasa Melayu'},
  {code:'hi',english:'Hindi',native:'हिन्दी'},
  {code:'ja',english:'Japanese',native:'日本語'},
  {code:'en-XA',english:'Pseudo (expansion)',native:'[Expanded]'},
  {code:'ar-XB',english:'Pseudo (RTL)',native:'[RTL]'},
].map(({code,english,native}) => ({code,english,native,dir:code === 'ar' || code === 'ar-XB' ? 'rtl' : 'ltr',fallback:'en'}));
export function canonicalLocale(code: string): string {
  const normalized = code.replaceAll('_','-');
  return LOCALES.find(locale => locale.code.toLowerCase() === normalized.toLowerCase())?.code
    ?? LOCALES.find(locale => locale.code === normalized.split('-')[0].toLowerCase())?.code ?? DEFAULT_LOCALE;
}
export function localeMeta(code: string): LocaleMeta { return LOCALES.find(locale => locale.code === canonicalLocale(code)) ?? LOCALES[0]; }
export function isSupportedLocale(code: string): boolean { return LOCALES.some(locale => locale.code === code); }
