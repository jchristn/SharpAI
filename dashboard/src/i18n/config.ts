import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import en from './en.json';
import { canonicalLocale, DEFAULT_LOCALE, localeMeta } from './locales';
export const STORAGE_KEY = 'sharpai.locale';
type Catalog = { [key: string]: string | Catalog };
const loaders = import.meta.glob(['./*.json', '!./en.json']);
export function pseudo(node: Catalog): Catalog {
  return Object.fromEntries(Object.entries(node).map(([key,value]) => [key, typeof value === 'string'
    ? `⟦${value.split(/(\{\{[^}]+\}\})/).map(part => part.startsWith('{{') ? part : part.replace(/[aeiou]/g, char => ({a:'á',e:'é',i:'í',o:'ó',u:'ú'})[char] ?? char)).join('')}${'·'.repeat(Math.ceil(value.length * .4))}⟧` : pseudo(value)]));
}
export function detectLocale(): string {
  try { const stored = window.localStorage.getItem(STORAGE_KEY); if (stored) return canonicalLocale(stored); } catch { /* unavailable storage */ }
  for (const code of typeof navigator === 'undefined' ? [] : navigator.languages) {
    const canonical = canonicalLocale(code);
    if (canonical !== DEFAULT_LOCALE || code.startsWith('en')) return canonical;
  }
  return DEFAULT_LOCALE;
}
function applyDocument(code: string): void {
  if (typeof document !== 'undefined') { document.documentElement.lang = code; document.documentElement.dir = localeMeta(code).dir; }
}
void i18n.use(initReactI18next).init({ resources: { en: { translation: en }, 'en-XA': { translation: pseudo(en) }, 'ar-XB': { translation: pseudo(en) } }, lng: 'en', fallbackLng: DEFAULT_LOCALE, interpolation: { escapeValue: false } });
let selection = 0;
export async function setActiveLocale(input: string): Promise<void> {
  const code = canonicalLocale(input); const request = ++selection;
  if (!i18n.hasResourceBundle(code,'translation')) {
    const module = await loaders[`./${code}.json`]() as {default: Catalog};
    i18n.addResourceBundle(code,'translation',module.default);
  }
  if (request !== selection) return;
  await i18n.changeLanguage(code);
  try { window.localStorage.setItem(STORAGE_KEY,code); } catch { /* unavailable storage */ }
  applyDocument(code);
}
export const localeReady = setActiveLocale(detectLocale());
export default i18n;
