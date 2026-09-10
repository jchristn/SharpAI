import { useTranslation } from 'react-i18next';
import { LOCALES } from '../i18n/locales';
import { setActiveLocale } from '../i18n/config';

/** Keyboard-accessible language selector. Shows each language as its native autonym. */
export function LanguageSelector(): JSX.Element {
  const { t, i18n } = useTranslation();
  return (
    <select
      className="sa-btn sa-btn--ghost"
      value={i18n.language}
      onChange={(event) => setActiveLocale(event.target.value)}
      aria-label={t('topbar.language')}
    >
      {LOCALES.map((locale) => (
        <option key={locale.code} value={locale.code}>
          {locale.native}
        </option>
      ))}
    </select>
  );
}
