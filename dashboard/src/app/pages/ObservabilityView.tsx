import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { StatusBadge } from '../../components/ui';
import { formatDurationMs, formatNumber } from '../../i18n/format';
import { useApi } from '../ApiProvider';
import { averageLatencyMs, parseSample, ratePerSecond } from '../metrics';
import type { MetricsSample } from '../metrics';

type Probe = 'ok' | 'down' | 'checking';

interface LiveMetrics {
  requestRate: number;
  tokenRate: number;
  resident: number;
  avgLatencyMs: number | null;
}

const POLL_MS = 5000;

/** Observability panel (T9): live health/readiness, in-app metric summaries polled from /metrics, and links. */
export function ObservabilityView(): JSX.Element {
  const { t, i18n } = useTranslation();
  const api = useApi();
  const [health, setHealth] = useState<Probe>('checking');
  const [ready, setReady] = useState<Probe>('checking');
  const [metrics, setMetrics] = useState<LiveMetrics | null>(null);
  const [metricsError, setMetricsError] = useState<boolean>(false);
  const previous = useRef<{ sample: MetricsSample; at: number } | null>(null);
  const baseUrl = typeof window !== 'undefined' ? window.location.origin : '';

  useEffect(() => {
    let cancelled = false;
    api.health().then(() => !cancelled && setHealth('ok')).catch(() => !cancelled && setHealth('down'));
    api.ready().then(() => !cancelled && setReady('ok')).catch(() => !cancelled && setReady('down'));
    return () => {
      cancelled = true;
    };
  }, [api]);

  useEffect(() => {
    let cancelled = false;

    const poll = async (): Promise<void> => {
      try {
        const text = await api.metrics();
        if (cancelled) return;
        const sample = parseSample(text);
        const now = Date.now();
        const prev = previous.current;
        const seconds = prev ? (now - prev.at) / 1000 : 0;
        setMetrics({
          requestRate: prev ? ratePerSecond(prev.sample.requests, sample.requests, seconds) : 0,
          tokenRate: prev ? ratePerSecond(prev.sample.tokens, sample.tokens, seconds) : 0,
          resident: sample.resident,
          avgLatencyMs: averageLatencyMs(sample),
        });
        previous.current = { sample, at: now };
        setMetricsError(false);
      } catch {
        if (!cancelled) setMetricsError(true);
      }
    };

    void poll();
    const timer = window.setInterval(() => void poll(), POLL_MS);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [api]);

  const badge = (probe: Probe): JSX.Element => {
    if (probe === 'checking') return <StatusBadge tone="neutral" dot>{t('observability.checking')}</StatusBadge>;
    if (probe === 'ok') return <StatusBadge tone="success" dot>{t('observability.up')}</StatusBadge>;
    return <StatusBadge tone="danger" dot>{t('observability.down')}</StatusBadge>;
  };

  const num = (value: number): string => formatNumber(Math.round(value * 100) / 100, i18n.language);

  return (
    <div>
      <h1 className="sa-page__title">{t('observability.title')}</h1>

      <div className="sa-kpis">
        <div className="sa-kpi">
          <div className="sa-kpi__label">{t('observability.health')}</div>
          <div style={{ marginTop: 'var(--sa-space-2)' }}>{badge(health)}</div>
        </div>
        <div className="sa-kpi">
          <div className="sa-kpi__label">{t('observability.readiness')}</div>
          <div style={{ marginTop: 'var(--sa-space-2)' }}>{badge(ready)}</div>
        </div>
      </div>

      <div className="sa-kpi__label" style={{ margin: 'var(--sa-space-2) 0' }}>{t('observability.liveMetrics')}</div>
      {metricsError ? (
        <StatusBadge tone="warning">{t('observability.metricsUnavailable')}</StatusBadge>
      ) : (
        <div className="sa-kpis">
          <div className="sa-kpi">
            <div className="sa-kpi__label">{t('observability.requestRate')}</div>
            <div className="sa-kpi__value">{metrics ? num(metrics.requestRate) : '—'}</div>
          </div>
          <div className="sa-kpi">
            <div className="sa-kpi__label">{t('observability.tokensPerSec')}</div>
            <div className="sa-kpi__value">{metrics ? num(metrics.tokenRate) : '—'}</div>
          </div>
          <div className="sa-kpi">
            <div className="sa-kpi__label">{t('observability.residentModels')}</div>
            <div className="sa-kpi__value">{metrics ? formatNumber(metrics.resident, i18n.language) : '—'}</div>
          </div>
          <div className="sa-kpi">
            <div className="sa-kpi__label">{t('observability.avgLatency')}</div>
            <div className="sa-kpi__value">
              {metrics && metrics.avgLatencyMs !== null ? formatDurationMs(metrics.avgLatencyMs, i18n.language) : '—'}
            </div>
          </div>
        </div>
      )}

      <div className="sa-kpi" style={{ marginTop: 'var(--sa-space-4)' }}>
        <p style={{ fontWeight: 600 }}>{t('observability.endpoints')}</p>
        <p style={{ color: 'var(--sa-text-muted)' }}>{t('observability.prometheusText', { url: `${baseUrl}/metrics` })}</p>
        <p>
          <a className="sa-btn sa-btn--primary" href="http://localhost:9400" target="_blank" rel="noreferrer">
            {t('observability.openGrafana')}
          </a>
        </p>
      </div>
    </div>
  );
}
