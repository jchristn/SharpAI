import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ActivityBucket, ActivityChart, StatusBadge, toneForHttpStatus } from '../../components/ui';
import { useApi } from '../ApiProvider';

type Row = Record<string, unknown>;

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

function pick(row: Row, ...keys: string[]): unknown {
  for (const key of keys) {
    if (row[key] !== undefined && row[key] !== null) return row[key];
  }
  return undefined;
}

function recentFailures(value: unknown): Row[] {
  const record = (value ?? {}) as Record<string, unknown>;
  const objects = (record.Objects ?? record.objects ?? []) as Row[];
  return objects.filter((r) => Number(pick(r, 'StatusCode', 'statusCode') ?? 0) >= 400).slice(0, 5);
}

/** Overview view — KPIs, request-activity chart, and a recent-failures panel with manual refresh. */
export function HomeView(): JSX.Element {
  const { t } = useTranslation();
  const api = useApi();
  const [onDisk, setOnDisk] = useState<number | null>(null);
  const [resident, setResident] = useState<number | null>(null);
  const [summary, setSummary] = useState<HomeSummary | null>(null);
  const [failures, setFailures] = useState<Row[]>([]);

  const load = useCallback(async (): Promise<void> => {
    const [tags, ps, sum, history] = await Promise.all([
      api.listModels().catch(() => null),
      api.runningModels().catch(() => null),
      api.requestHistorySummary().catch(() => null),
      api.requestHistory({ pageSize: 100 }).catch(() => null),
    ]);
    setOnDisk(tags === null ? null : countModels(tags));
    setResident(ps === null ? null : countModels(ps));
    setSummary(sum === null ? null : readSummary(sum));
    setFailures(history === null ? [] : recentFailures(history));
  }, [api]);

  useEffect(() => {
    void load();
  }, [load]);

  const show = (value: number | null): string => (value === null ? '—' : String(value));

  return (
    <div>
      <div className="sa-page__toolbar">
        <h1 className="sa-page__title">{t('home.title')}</h1>
        <button type="button" className="sa-btn" onClick={() => void load()}>
          {t('home.refresh')}
        </button>
      </div>

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
          <div className="sa-kpi__value">{summary ? summary.total : '—'}</div>
        </div>
      </div>

      {summary ? (
        <>
          <div className="sa-kpi__label" style={{ marginBottom: 'var(--sa-space-2)' }}>{t('home.requestActivity')}</div>
          <ActivityChart buckets={summary.buckets} />
        </>
      ) : null}

      <div className="sa-kpi__label" style={{ margin: 'var(--sa-space-3) 0 var(--sa-space-2)' }}>{t('home.recentFailures')}</div>
      {failures.length === 0 ? (
        <div style={{ color: 'var(--sa-text-faint)', fontSize: 'var(--sa-text-sm)' }}>{t('home.noFailures')}</div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--sa-space-2)' }}>
          {failures.map((row, index) => (
            <div key={String(pick(row, 'Id', 'id') ?? index)} style={{ display: 'flex', alignItems: 'center', gap: 'var(--sa-space-2)', fontSize: 'var(--sa-text-sm)' }}>
              <StatusBadge tone={toneForHttpStatus(Number(pick(row, 'StatusCode', 'statusCode') ?? 0))}>
                {String(pick(row, 'StatusCode', 'statusCode') ?? '—')}
              </StatusBadge>
              <span style={{ fontFamily: 'var(--sa-font-mono)', color: 'var(--sa-text-muted)' }}>
                {String(pick(row, 'Method', 'method') ?? '')} {String(pick(row, 'Path', 'path') ?? '')}
              </span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
