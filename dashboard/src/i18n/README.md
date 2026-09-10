# Dashboard internationalization

The dashboard is fully localizable. Every operator-facing string flows through i18next
(`react-i18next`); nothing user-visible is hard-coded in a component.

## Layout

- `config.ts` — i18next initialization, deterministic locale detection, persistence, central
  `lang`/`dir` application, and `setActiveLocale(code)`. Imported once from `main.tsx` so the locale is
  resolved before first paint.
- `locales.ts` — the locale registry: `code`, English name, native autonym, text direction, fallback.
- `en.json`, `ar.json` — translation catalogs. `en` is the source of truth.
- `format.ts` — shared, explicit-locale `Intl` formatters (`formatNumber/Percent/DateTime/DurationMs/Bytes/List`).
- `../app/LanguageSelector.tsx` — the language selector shown in the shell (native autonyms).

## Adding a locale

1. Add an entry to `LOCALES` in `locales.ts` (code, English name, native autonym, `dir`, `fallback`).
2. Add a `<code>.json` catalog (copy `en.json`; translate values, keep keys identical).
3. Register the catalog in `config.ts` `resources`.

No application logic changes are required. Untranslated keys fall back to English.

## Locale resolution order

1. Explicit user selection (persisted to `localStorage` as `sharpai.locale`).
2. Browser preference (`navigator.language`, then its base language).
3. Default fallback (`en`).

A generated **pseudo-locale** (`en-XA`) accents and pads every source string to surface truncation and
expansion issues; **Arabic** (`ar`) exercises RTL. `setActiveLocale` stamps
`document.documentElement.lang`/`dir` so RTL flips the whole shell.

## Keys and conventions

- Keys are stable identifiers (`models.deleteConfirm`), never English sentences.
- Use interpolation (`t('models.deleteConfirm', { name })`) rather than string concatenation.
- Localize accessibility strings (`aria-label`, `title`) alongside visible text.
- Route display formatting through `format.ts` with an explicit locale — never bare `toLocaleString()`.

## Server-authored text

Outbound API requests carry `Accept-Language` from the active locale (see `ApiClient`). The preferred
strategy is that the server returns **stable codes/enums** and the client localizes their display labels;
server-returned localized text is acceptable when driven by `Accept-Language`. Never persist rendered
strings — persist semantic payloads and localize at render time.

## Tests / CI gate

`i18n.test.ts` asserts there are no orphaned keys (every non-source catalog key exists in `en`) and that
locale switching applies the correct `dir`/`lang`. `format.test.ts` covers the formatters and RTL
direction. Extend the orphaned-key check into a CI step as catalogs grow.
