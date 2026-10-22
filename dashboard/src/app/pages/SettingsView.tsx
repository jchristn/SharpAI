import { Icon } from '../Icon';
import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { SettingsFields } from './SettingsFields';
import { StatusBadge } from '../../components/ui';
import { ApiError } from '../../api/ApiClient';
import { useApi } from '../ApiProvider';

type SaveState = { kind: 'idle' } | { kind: 'saved' } | { kind: 'error'; message: string };

interface ServerInfo {
  version: string;
  backend: string;
  nativeInit: string;
  database: string;
  authEnabled: boolean;
  telemetryEnabled: boolean;
}

function record(value: unknown): Record<string, unknown> {
  return (value ?? {}) as Record<string, unknown>;
}

function buildInfo(settings: unknown, health: unknown): ServerInfo {
  const s = record(settings);
  const h = record(health);
  const db = record(s.Database);
  const auth = record(s.Auth);
  const telemetry = record(s.Telemetry);
  return {
    version: String(h.version ?? s.SoftwareVersion ?? '—'),
    backend: String(h.backend ?? '—'),
    nativeInit: String(h.native_initialized ?? '—'),
    database: String(db.Type ?? '—'),
    authEnabled: auth.Enabled === true,
    telemetryEnabled: telemetry.Enable === true,
  };
}

/** Settings / Server Info view (T8): a structured server-info panel plus editable, typed configuration sections. */
export function SettingsView(): JSX.Element {
  const { t } = useTranslation();
  const api = useApi();
  const [settings, setSettings] = useState<Record<string, unknown> | null>(null);
  const [baseline, setBaseline] = useState('');
  const [saving, setSaving] = useState(false);
  const dirty = settings !== null && JSON.stringify(settings) !== baseline;
  const [info, setInfo] = useState<ServerInfo | null>(null);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [save, setSave] = useState<SaveState>({ kind: 'idle' });

  const load = useCallback(async (): Promise<void> => {
    setLoading(true);
    setError(null);
    try {
      const settings = await api.getSettings();
      if (!settings || typeof settings !== 'object' || Array.isArray(settings)) throw new ApiError(502, t('settings.invalidResponse'), null);
      const editable = { ...record(settings), HuggingFace: { ApiKey: null, ...record(record(settings).HuggingFace) }, Runtime: { ForceBackend: null, CpuBackendPath: null, GpuBackendPath: null, MetalBackendPath: null, ...record(record(settings).Runtime) }, Database: { Username: null, Password: null, ...record(record(settings).Database) } };
      setSettings(editable);
      setBaseline(JSON.stringify(editable));
      setSave({ kind: 'idle' });
      const health = await api.health().catch(() => null);
      setInfo(buildInfo(settings, health));
    } catch (err) {
      setError(err instanceof ApiError ? `${err.status}: ${err.message}` : t('settings.loadError'));
    } finally {
      setLoading(false);
    }
  }, [api, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const onSave = async (): Promise<void> => {
    setSave({ kind: 'idle' });
    if (!settings || saving) return;
    setSaving(true);
    try {
      await api.updateSettings(settings);
      setBaseline(JSON.stringify(settings));
      setSave({ kind: 'saved' });
    } catch (err) {
      setSave({ kind: 'error', message: err instanceof ApiError ? `${err.status}: ${err.message}` : t('settings.saveError') });
    } finally {
      setSaving(false);
    }
  };

  const flag = (on: boolean): string => (on ? t('settings.enabled') : t('settings.disabled'));

  return (
    <form onSubmit={(event) => { event.preventDefault(); void onSave(); }}>
      <div className="sa-page__toolbar">
        <div><h1 className="sa-page__title">{t('settings.title')}</h1><p className="sa-heading-description">{t('text.configureYourServerInferenceRuntimeAndConnected')}</p></div>
        <div style={{ display: 'flex', gap: 'var(--sa-space-2)', alignItems: 'center' }}>
          {save.kind === 'saved' ? <StatusBadge tone="success">{t('settings.saved')}</StatusBadge> : null}
          {save.kind === 'error' ? <StatusBadge tone="danger">{save.message}</StatusBadge> : null}
          <button type="button" className="sa-btn--icon sa-btn" onClick={() => void load()} disabled={loading || saving} aria-label={t('settings.reload')}><Icon name="refresh"/></button>
          <button type="submit" className="sa-btn sa-btn--primary" disabled={loading || saving || !dirty}>
            {saving ? t('settings.saving') : t('settings.save')}
          </button>
        </div>
      </div>

      {error ? <StatusBadge tone="danger">{error}</StatusBadge> : null}

      {info ? (
        <>
          <div className="sa-kpi__label" style={{ marginBottom: 'var(--sa-space-2)' }}>{t('settings.serverInfo')}</div>
          <div className="sa-kpis">
            <div className="sa-kpi"><div className="sa-kpi__label">{t('settings.version')}</div><div className="sa-kpi__value">{info.version}</div></div>
            <div className="sa-kpi"><div className="sa-kpi__label">{t('settings.backend')}</div><div className="sa-kpi__value">{info.backend}</div></div>
            <div className="sa-kpi"><div className="sa-kpi__label">{t('settings.nativeInit')}</div><div className="sa-kpi__value">{info.nativeInit}</div></div>
            <div className="sa-kpi"><div className="sa-kpi__label">{t('settings.database')}</div><div className="sa-kpi__value">{info.database}</div></div>
            <div className="sa-kpi">
              <div className="sa-kpi__label">{t('settings.auth')}</div>
              <div style={{ marginTop: 'var(--sa-space-2)' }}>
                <StatusBadge tone={info.authEnabled ? 'success' : 'neutral'}>{flag(info.authEnabled)}</StatusBadge>
              </div>
            </div>
            <div className="sa-kpi">
              <div className="sa-kpi__label">{t('settings.telemetry')}</div>
              <div style={{ marginTop: 'var(--sa-space-2)' }}>
                <StatusBadge tone={info.telemetryEnabled ? 'success' : 'neutral'}>{flag(info.telemetryEnabled)}</StatusBadge>
              </div>
            </div>
          </div>
        </>
      ) : null}

      <div className="sa-settings-note">{t('text.changesAreSavedToTheServerConfiguration')}</div>
      {loading ? <div className="sa-empty" role="status">{t('text.loadingConfiguration')}</div> : settings ? <fieldset disabled={saving} className="sa-settings-fields"><SettingsFields value={settings} onChange={(next) => { setSettings(next); setSave({ kind: 'idle' }); }} /></fieldset> : null}
      <div role="status" aria-live="polite" className="sa-page-description">{dirty ? t('settings.dirty') : settings ? t('settings.current') : ''}</div>
    </form>
  );
}
