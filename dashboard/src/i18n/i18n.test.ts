import { describe, expect, it } from 'vitest';
import i18n, { pseudo, setActiveLocale, STORAGE_KEY } from './config';
import en from './en.json';
import { canonicalLocale, LOCALES } from './locales';
type Catalog = { [key:string]: string | Catalog };
function flatten(node: Catalog, prefix=''): Record<string,string> {
  const result:Record<string,string>={};
  for(const [key,value] of Object.entries(node)){
    if(typeof value==='string')result[prefix+key]=value;
    else Object.assign(result,flatten(value,prefix+key+'.'));
  }
  return result;
}
const catalogs = import.meta.glob('./*.json', { eager: true, import: 'default' }) as Record<string,Catalog>;
const source = flatten(en);
describe('complete catalogs', () => {
  for (const locale of LOCALES.filter(locale => !['en-XA','ar-XB'].includes(locale.code))) {
    it(`${locale.code} includes every source key and preserves interpolation`, () => {
      const translated = flatten(catalogs[`./${locale.code}.json`]);
      expect(Object.keys(translated).sort()).toEqual(Object.keys(source).sort());
      for (const [key,value] of Object.entries(source)) {
        expect(translated[key],key).toBeTruthy();
        expect(translated[key],key).not.toContain('\uFFFD');
        expect(translated[key].match(/\{\{[^}]+\}\}/g)?.sort() ?? [],key).toEqual(value.match(/\{\{[^}]+\}\}/g)?.sort() ?? []);
      }
    });
  }
  it('preserves placeholders under expansion', () => expect(pseudo({message:'Save {{name}}'}).message).toContain('{{name}}'));
  it('canonicalizes regional preferences', () => { expect(canonicalLocale('pt-BR')).toBe('pt'); expect(canonicalLocale('zh-Hans')).toBe('zh'); expect(canonicalLocale('ja-JP')).toBe('ja'); });
  it('persists languages and applies RTL direction', async () => {
    await setActiveLocale('ar'); expect(document.documentElement.dir).toBe('rtl'); expect(localStorage.getItem(STORAGE_KEY)).toBe('ar');
    await setActiveLocale('ja'); expect(document.documentElement.lang).toBe('ja'); expect(i18n.language).toBe('ja');
    await setActiveLocale('en'); expect(document.documentElement.dir).toBe('ltr');
  });
});
