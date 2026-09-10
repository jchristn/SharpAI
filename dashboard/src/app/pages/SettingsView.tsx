import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
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

/** Settings / Server Info view (T8): a structured server-info panel plus a raw settings JSON editor. */
export function SettingsView(): JSX.Element {
  const { t } = useTranslation();
  const api = useApi();
  const [text, setText] = useState<string>('');
  const [info, setInfo] = useState<ServerInfo | null>(null);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [save, setSave] = useState<SaveState>({ kind: 'idle' });

  const load = useCallback(async (): Promise<void> => {
    setLoading(true);
    setError(null);
    try {
      const settings = await api.getSettings();
      setText(JSON.stringify(settings, null, 2));
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
    let parsed: unknown;
    try {
      parsed = JSON.parse(text);
    } catch {
      setSave({ kind: 'error', message: t('settings.invalidJson') });
      return;
    }
    try {
      await api.updateSettings(parsed);
      setSave({ kind: 'saved' });
    } catch (err) {
      setSave({ kind: 'error', message: err instanceof ApiError ? `${err.status}: ${err.message}` : t('settings.saveError') });
    }
  };

  const flag = (on: boolean): string => (on ? t('settings.enabled') : t('settings.disabled'));

  return (
    <div>
      <div className="sa-page__toolbar">
        <h1 className="sa-page__title">{t('settings.title')}</h1>
        <div style={{ display: 'flex', gap: 'var(--sa-space-2)', alignItems: 'center' }}>
          {save.kind === 'saved' ? <StatusBadge tone="success">{t('settings.saved')}</StatusBadge> : null}
          {save.kind === 'error' ? <StatusBadge tone="danger">{save.message}</StatusBadge> : null}
          <button type="button" className="sa-btn" onClick={() => void load()} disabled={loading}>
            {t('settings.reload')}
          </button>
          <button type="button" className="sa-btn sa-btn--primary" onClick={() => void onSave()} disabled={loading}>
            {t('settings.save')}
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

      <div className="sa-kpi__label" style={{ margin: 'var(--sa-space-3) 0 var(--sa-space-2)' }}>{t('settings.rawConfig')}</div>
      <textarea
        className="sa-json"
        style={{ width: '100%', minHeight: '45vh', resize: 'vertical' }}
        value={text}
        spellCheck={false}
        onChange={(event) => setText(event.target.value)}
        aria-label={t('settings.rawConfig')}
      />
    </div>
  );
}
