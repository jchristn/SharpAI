import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { Icon } from '../Icon';
import type { ActivityBucket } from '../../components/ui';
import { formatNumber } from '../../i18n/format';
import { useApi } from '../ApiProvider';



function countModels(value: unknown): number {
  const record = (value ?? {}) as Record<string, unknown>;
  const models = record.models ?? record.Models;
  return Array.isArray(models) ? models.length : 0;
}

interface HomeSummary {
  total: number;
  buckets: ActivityBucket[];
}

function readSummary(value: unknown): HomeSummary {
  const record = (value ?? {}) as Record<string, unknown>;
  const raw = (record.Buckets ?? record.buckets ?? []) as Record<string, unknown>[];
  const buckets: ActivityBucket[] = raw.map((b) => ({
    start: String(b.BucketStartUtc ?? b.bucketStartUtc ?? ''),
    end: String(b.BucketEndUtc ?? b.bucketEndUtc ?? ''),
    success: Number(b.SuccessCount ?? b.successCount ?? 0),
    failure: Number(b.FailureCount ?? b.failureCount ?? 0),
  }));
  return { total: Number(record.TotalCount ?? record.totalCount ?? 0), buckets };
}

/** Overview view — KPIs, request-activity chart, and a recent-failures panel with manual refresh. */
export function HomeView(): JSX.Element {
  const { t, i18n } = useTranslation();
  const api = useApi();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(false);
  const [onDisk, setOnDisk] = useState<number | null>(null);
  const [resident, setResident] = useState<number | null>(null);
  const [summary, setSummary] = useState<HomeSummary | null>(null);


  const load = useCallback(async (): Promise<void> => {
    setLoading(true);
    const [tags, ps, sum] = await Promise.all([
      api.listModels().catch(() => null),
      api.runningModels().catch(() => null),
      api.requestHistorySummary().catch(() => null),
    ]);
    setError([tags, ps, sum].some(value => value === null));
    setLoading(false);
    setOnDisk(tags === null ? null : countModels(tags));
    setResident(ps === null ? null : countModels(ps));
    setSummary(sum === null ? null : readSummary(sum));
  }, [api]);

  useEffect(() => {
    void load();
  }, [load]);

  const show = (value: number | null): string => (value === null ? '—' : formatNumber(value, i18n.language));

  return (
    <div>
      <div className="sa-page__toolbar">
        <div><h1 className="sa-page__title">{t('home.title')}</h1><p className="sa-heading-description">{t('text.yourModelsRequestActivityAndServerHealth')}</p></div>
        <button type="button" className="sa-btn--icon sa-btn" disabled={loading} onClick={() => void load()} aria-label={t('home.refresh')}><Icon name="refresh"/></button>
      </div>

      {error && <div className="sa-settings-note" role="alert">{t('text.someServerDataCouldNotBeLoaded')}</div>}
      <div className="sa-kpis">
        <div className="sa-kpi">
          <div className="sa-kpi__label">{t('home.modelsOnDisk')}</div>
          <div className="sa-kpi__value">{show(onDisk)}</div>
        </div>
        <div className="sa-kpi">
          <div className="sa-kpi__label">{t('home.residentModels')}</div>
          <div className="sa-kpi__value">{show(resident)}</div>
        </div>
        <div className="sa-kpi">
          <div className="sa-kpi__label">{t('home.requests')}</div>
          <div className="sa-kpi__value">{summary ? formatNumber(summary.total, i18n.language) : '—'}</div>
        </div>
      </div>

      <div className="sa-quick-actions">{[
        ['/models','quick.models','quick.modelsHelp'],['/playground','quick.test','quick.testHelp'],['/observability','quick.observe','quick.observeHelp'],['/api-explorer','quick.api','quick.apiHelp'],
      ].map(([path,label,help])=><Link className="sa-quick-card" to={path} key={path}><Icon name={path}/><strong>{t(label)}</strong><p>{t(help)}</p></Link>)}</div>
    </div>
  );
}
