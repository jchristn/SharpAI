import { Icon } from '../Icon';
import { useTranslation } from 'react-i18next';
/** Structured configuration controls. Values keep their original JSON types and unknown fields. */
function fieldLabel(key: string): string {
  return key.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/([A-Z])([A-Z][a-z])/g, '$1 $2').replace(/Utc$/, '(UTC)');
}
const descriptions = new Set(['Runtime','Storage','Auth','Telemetry','RequestHistory','Rest','Database','Logging']);
const dictionaries = new Set(['QuantizationPriority', 'DefaultHeaders']);
const readOnly = new Set(['CreatedUtc', 'SchemaVersion', 'SoftwareVersion', 'Prefix', 'CacheCount']);
const secret = /password|apikey|signingkey|secret/i;

export function SettingsFields({ value, onChange, path = '' }: {
  value: Record<string, unknown>;
  onChange: (value: Record<string, unknown>) => void;
  path?: string;
}): JSX.Element {
  const { t } = useTranslation();
  const label = (key: string): string => t(`fields.${key}`, { defaultValue: fieldLabel(key) });
  if (!value || typeof value !== 'object' || Array.isArray(value)) return <p role="alert">{t('settings.invalidResponse')}</p>;
  return <>{Object.entries(value).map(([key, item]) => {
    const location = path ? `${path}.${key}` : key;
    const change = (next: unknown): void => onChange({ ...value, [key]: next });
    if (item !== null && typeof item === 'object' && !Array.isArray(item) && dictionaries.has(key)) {
      const entries = Object.entries(item as Record<string, unknown>);
      return <fieldset className="sa-settings-section" key={key}><legend>{label(key)}</legend>
        {entries.map(([name, entry], index) => <div className="sa-dictionary-row" key={index}>
          <label className="sa-filterbar__field"><span>{t('text.name')}</span><input aria-label={`${label(key)} name ${index + 1}`} value={name} onChange={(event) => change(Object.fromEntries(entries.map(([k,v], i) => [i === index ? event.target.value : k,v])))} /></label>
          <label className="sa-filterbar__field"><span>{t('text.value')}</span><input aria-label={`${label(key)} value ${index + 1}`} type={typeof entry === 'number' ? 'number' : 'text'} value={String(entry)} onChange={(event) => change({ ...item, [name]: typeof entry === 'number' ? Number(event.target.value) : event.target.value })} /></label>
          <button type="button" className="sa-btn sa-btn--ghost sa-btn--icon" data-help="delete" aria-label={t('settings.removeOverride', {name})} onClick={() => change(Object.fromEntries(entries.filter(([k]) => k !== name)))}><Icon name="trash"/></button>
        </div>)}
        <button type="button" data-help="create" className="sa-btn" onClick={() => { let name = t('settings.newOverride'); while (name in item) name += ' '; change({ ...item, [name]: key === 'QuantizationPriority' ? 0 : '' }); }}>{t('text.addOverride')}</button>
      </fieldset>;
    }
    if (item !== null && typeof item === 'object' && !Array.isArray(item)) {
      return <fieldset className="sa-settings-section" key={key}><legend>{label(key)}</legend>
        {descriptions.has(key) && <p className="sa-page-description" style={{ margin: '0 0 20px' }}>{t(`settingsDescriptions.${key}`)}</p>}
        <SettingsFields value={item as Record<string, unknown>} onChange={change} path={location} />
        {Object.keys(item).length === 0 && <small className="sa-page-description">{t('text.noOverridesConfiguredServerDefaultsApply')}</small>}
      </fieldset>;
    }
    if (Array.isArray(item)) {
      return <fieldset className="sa-settings-section" key={key}><legend>{label(key)}</legend>
        {item.map((entry: unknown, index: number) => <div key={index} className="sa-array-row">
          {entry !== null && typeof entry === 'object' ? <SettingsFields value={entry as Record<string, unknown>} path={`${location}.${index}`} onChange={(next) => change(item.map((old, i) => i === index ? next : old))} /> :
            <label className="sa-filterbar__field"><span>{label(key)} {index + 1}</span><input type={secret.test(key) ? 'password' : 'text'} value={String(entry)} onChange={(event) => change(item.map((old, i) => i === index ? event.target.value : old))} /></label>}
          <button type="button" className="sa-btn sa-btn--ghost sa-btn--icon" data-help="delete" aria-label={t('settings.removeItem', {name: label(key), index:index+1})} onClick={() => change(item.filter((_, i) => i !== index))}><Icon name="trash"/></button>
        </div>)}
        <button type="button" data-help="create" className="sa-btn" onClick={() => change([...item, key === 'Servers' ? { Hostname: '127.0.0.1', Port: 514, RandomizePorts: false, MinimumPort: 65000, MaximumPort: 65535 } : typeof item[0] === 'object' ? Object.fromEntries(Object.entries(item[0] as Record<string, unknown>).map(([k,v]) => [k, typeof v === 'boolean' ? false : typeof v === 'number' ? 0 : ''])) : ''])}>{t('settings.addItem', {name:label(key)})}</button>
      </fieldset>;
    }
    return <div className="sa-setting-field" key={key}><label className="sa-filterbar__field">
      <span>{label(key)}</span>
      {typeof item === 'boolean' ? <select value={String(item)} onChange={(event) => change(event.target.value === 'true')}><option value="true">{t('text.enabled')}</option><option value="false">{t('text.disabled')}</option></select> :
      key === 'ForceBackend' ? <select value={String(item ?? '')} onChange={(event) => change(event.target.value || null)}><option value="">{t('text.automatic')}</option><option value="cpu">{t('text.cpu')}</option><option value="cuda">{t('text.cuda')}</option><option value="metal">{t('text.metal')}</option></select> :
      <input type={typeof item === 'number' ? 'number' : secret.test(key) ? 'password' : 'text'} min={typeof item === 'number' ? 0 : undefined} step={typeof item === 'number' ? 1 : undefined} required={typeof item === 'number'} disabled={readOnly.has(key)} autoComplete="off" value={String(item ?? '')} placeholder={item === null ? t('settings.defaults') : undefined} onChange={(event) => change(typeof item === 'number' ? Number(event.target.value) : item === null && !event.target.value ? null : event.target.value)} />}
      {readOnly.has(key) && <small className="sa-page-description">{t('text.managedByTheServer')}</small>}
    </label></div>;
  })}</>;
}
