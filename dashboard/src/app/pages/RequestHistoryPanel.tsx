import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  ActivityBucket,
  ActivityChart,
  Column,
  DataTable,
  DEFAULT_PAGE_SIZE,
  FilterBar,
  FilterField,
  JsonViewer,
  Modal,
  StatusBadge,
  toneForHttpStatus,
} from '../../components/ui';
import { ApiError } from '../../api/ApiClient';
import { useApi } from '../ApiProvider';
import { DEFAULT_RANGE_KEY, TIME_RANGES, rangeByKey } from './requestHistoryRanges';

type Row = Record<string, unknown>;

interface Page {
  objects: Row[];
  total: number;
}

function pick(row: Row, ...keys: string[]): unknown {
  for (const key of keys) {
    if (row[key] !== undefined && row[key] !== null) return row[key];
  }
  return undefined;
}

function readPage(value: unknown): Page {
  const record = (value ?? {}) as Record<string, unknown>;
  const objects = (record.Objects ?? record.objects ?? []) as Row[];
  const total = Number(record.TotalRecords ?? record.totalRecords ?? objects.length) || 0;
  return { objects, total };
}

function asText(value: unknown): string {
  if (value === undefined || value === null) return '';
  return String(value);
}

interface Summary {
  total: number;
  success: number;
  failure: number;
  avgMs: number;
  buckets: ActivityBucket[];
}

function readSummary(value: unknown): Summary {
  const record = (value ?? {}) as Record<string, unknown>;
  const rawBuckets = (record.Buckets ?? record.buckets ?? []) as Row[];
  const buckets: ActivityBucket[] = rawBuckets.map((b) => ({
    start: asText(pick(b, 'BucketStartUtc', 'bucketStartUtc')),
    end: asText(pick(b, 'BucketEndUtc', 'bucketEndUtc')),
    success: Number(pick(b, 'SuccessCount', 'successCount') ?? 0),
    failure: Number(pick(b, 'FailureCount', 'failureCount') ?? 0),
  }));
  return {
    total: Number(record.TotalCount ?? record.totalCount ?? 0),
    success: Number(record.TotalSuccess ?? record.totalSuccess ?? 0),
    failure: Number(record.TotalFailure ?? record.totalFailure ?? 0),
    avgMs: Number(record.AverageDurationMs ?? record.averageDurationMs ?? 0),
    buckets,
  };
}

export interface RequestHistoryPanelProps {
  /** i18n key for the panel title. */
  titleKey: string;
  /** Optional server category filter (e.g. "inference" for inference/embeddings endpoints). */
  category?: string;
  /**
   * Optional endpoint sub-filter options rendered as a select (value = pathContains substring).
   * Used by the inference panel to narrow to chat / completions / embeddings.
   */
  endpointOptions?: { label: string; value: string }[];
}

/**
 * Reusable request-history panel: time-range presets (last hour/day/week/month with the product-standard
 * bucket sizes), KPI strip, click-to-drill activity chart, backend filters, paginated table, and a details
 * modal. Rendered once for all API requests and once (with category="inference") for inference/embeddings.
 */
export function RequestHistoryPanel({ titleKey, category, endpointOptions }: RequestHistoryPanelProps): JSX.Element {
  const { t } = useTranslation();
  const api = useApi();
  const [page, setPage] = useState<Page>({ objects: [], total: 0 });
  const [pageNumber, setPageNumber] = useState<number>(1);
  const [pageSize, setPageSize] = useState<number>(DEFAULT_PAGE_SIZE);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [detail, setDetail] = useState<Row | null>(null);
  const [rangeKey, setRangeKey] = useState<string>(DEFAULT_RANGE_KEY);
  const [refreshNonce, setRefreshNonce] = useState<number>(0);
  const [drill, setDrill] = useState<{ fromUtc: string; toUtc: string } | null>(null);
  const [summary, setSummary] = useState<Summary | null>(null);

  const emptyFilters = useMemo(
    () => ({ method: '', statusCode: '', pathContains: '', endpoint: '' }),
    [],
  );
  const [filters, setFilters] = useState<Record<string, string>>(emptyFilters);
  const [applied, setApplied] = useState<Record<string, string>>(emptyFilters);

  // Compute the [from, to] window and bucket size from the selected preset. refreshNonce re-anchors "now".
  const rangeWindow = useMemo(() => {
    const preset = rangeByKey(rangeKey);
    const now = Date.now();
    void refreshNonce;
    return {
      fromUtc: new Date(now - preset.rangeMs).toISOString(),
      toUtc: new Date(now).toISOString(),
      bucketMinutes: preset.bucketMinutes,
    };
  }, [rangeKey, refreshNonce]);

  const filterFields: FilterField[] = [
    { key: 'method', label: t('requestHistory.filterMethod'), type: 'select', options: ['GET', 'POST', 'PUT', 'DELETE'].map((m) => ({ label: m, value: m })) },
    { key: 'statusCode', label: t('requestHistory.filterStatus'), type: 'text', placeholder: t('requestHistory.filterStatusPlaceholder') },
  ];
  if (endpointOptions && endpointOptions.length > 0) {
    filterFields.push({ key: 'endpoint', label: t('requestHistory.filterEndpoint'), type: 'select', options: endpointOptions });
  } else {
    filterFields.push({ key: 'pathContains', label: t('requestHistory.filterPath'), type: 'text', placeholder: t('requestHistory.filterPathPlaceholder') });
  }

  const load = useCallback(async (): Promise<void> => {
    setLoading(true);
    setError(null);
    try {
      const base: Record<string, string | number> = {};
      if (category) base.category = category;
      if (applied.method) base.method = applied.method;
      if (applied.statusCode) base.statusCode = applied.statusCode;
      // pathContains comes from either the free-text field or the endpoint select.
      const pathContains = applied.endpoint || applied.pathContains;
      if (pathContains) base.pathContains = pathContains;

      const summaryQuery = { ...base, fromUtc: rangeWindow.fromUtc, toUtc: rangeWindow.toUtc, bucketMinutes: rangeWindow.bucketMinutes };
      const tableQuery = {
        ...base,
        fromUtc: drill ? drill.fromUtc : rangeWindow.fromUtc,
        toUtc: drill ? drill.toUtc : rangeWindow.toUtc,
        pageNumber,
        pageSize,
      };

      const [result, summaryResult] = await Promise.all([
        api.requestHistory(tableQuery),
        api.requestHistorySummary(summaryQuery),
      ]);
      setPage(readPage(result));
      setSummary(readSummary(summaryResult));
    } catch (err) {
      setError(err instanceof ApiError ? `${err.status}: ${err.message}` : t('requestHistory.loadError'));
      setPage({ objects: [], total: 0 });
    } finally {
      setLoading(false);
    }
  }, [api, category, pageNumber, pageSize, applied, rangeWindow, drill, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const columns: Column<Row>[] = [
    { key: 'method', header: t('requestHistory.colMethod'), render: (row) => asText(pick(row, 'Method', 'method')) },
    { key: 'path', header: t('requestHistory.colPath'), render: (row) => asText(pick(row, 'Path', 'path')) },
    {
      key: 'status',
      header: t('requestHistory.colStatus'),
      render: (row) => {
        const code = Number(pick(row, 'StatusCode', 'statusCode') ?? 0);
        return (
          <StatusBadge tone={toneForHttpStatus(code)} dot>
            {code || '—'}
          </StatusBadge>
        );
      },
    },
    { key: 'duration', header: t('requestHistory.colDuration'), render: (row) => asText(pick(row, 'DurationMs', 'durationMs')) },
    { key: 'ip', header: t('requestHistory.colSourceIp'), render: (row) => asText(pick(row, 'SourceIp', 'sourceIp')) },
    { key: 'created', header: t('requestHistory.colCreated'), render: (row) => asText(pick(row, 'CreatedUtc', 'createdUtc')) },
    {
      key: 'actions',
      header: '',
      render: (row) => (
        <button type="button" className="sa-btn sa-btn--ghost" onClick={() => setDetail(row)}>
          {t('requestHistory.details')}
        </button>
      ),
    },
  ];

  return (
    <div>
      <div className="sa-page__toolbar">
        <h1 className="sa-page__title">{t(titleKey)}</h1>
        <div className="sa-rangebar" role="group" aria-label={t('requestHistory.rangeLabel')}>
          {TIME_RANGES.map((r) => (
            <button
              key={r.key}
              type="button"
              className={`sa-btn sa-btn--sm${rangeKey === r.key ? ' sa-btn--active' : ' sa-btn--ghost'}`}
              aria-pressed={rangeKey === r.key}
              onClick={() => {
                setRangeKey(r.key);
                setDrill(null);
                setPageNumber(1);
              }}
            >
              {t(r.labelKey)}
            </button>
          ))}
          <button type="button" className="sa-btn sa-btn--sm" onClick={() => setRefreshNonce((n) => n + 1)} disabled={loading}>
            {t('common.refresh')}
          </button>
        </div>
      </div>

      {summary ? (
        <>
          <div className="sa-kpis">
            <div className="sa-kpi">
              <div className="sa-kpi__label">{t('requestHistory.requests')}</div>
              <div className="sa-kpi__value">{summary.total}</div>
            </div>
            <div className="sa-kpi">
              <div className="sa-kpi__label">{t('requestHistory.successRate')}</div>
              <div className="sa-kpi__value">{summary.total ? Math.round((summary.success / summary.total) * 100) : 0}%</div>
            </div>
            <div className="sa-kpi">
              <div className="sa-kpi__label">{t('requestHistory.avgDuration')}</div>
              <div className="sa-kpi__value">{Math.round(summary.avgMs)} ms</div>
            </div>
          </div>
          <ActivityChart
            buckets={summary.buckets}
            onBucketClick={(bucket) => {
              setDrill({ fromUtc: bucket.start, toUtc: bucket.end });
              setPageNumber(1);
            }}
          />
        </>
      ) : null}

      {drill ? (
        <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--sa-space-2)', marginBottom: 'var(--sa-space-2)', fontSize: 'var(--sa-text-sm)' }}>
          <span className="sa-filterbar__label">
            {t('requestHistory.range')}: {drill.fromUtc} → {drill.toUtc}
          </span>
          <button type="button" className="sa-btn sa-btn--ghost" onClick={() => { setDrill(null); setPageNumber(1); }}>
            {t('requestHistory.clearRange')}
          </button>
        </div>
      ) : null}

      <FilterBar
        fields={filterFields}
        values={filters}
        onChange={(key, value) => setFilters((prev) => ({ ...prev, [key]: value }))}
        onApply={() => {
          setPageNumber(1);
          setApplied(filters);
        }}
        onClear={() => {
          setFilters(emptyFilters);
          setApplied(emptyFilters);
          setPageNumber(1);
        }}
      />

      <DataTable<Row>
        columns={columns}
        rows={page.objects}
        rowKey={(row) => asText(pick(row, 'Id', 'id', 'guid')) || Math.random().toString(36)}
        loading={loading}
        error={error}
        emptyMessage={t('requestHistory.empty')}
        pagination={{
          pageNumber,
          pageSize,
          totalRecords: page.total,
          onPageChange: setPageNumber,
          onPageSizeChange: (size) => {
            setPageSize(size);
            setPageNumber(1);
          },
        }}
      />

      <Modal open={detail !== null} title={t('requestHistory.detailsTitle')} onClose={() => setDetail(null)}>
        {detail ? <JsonViewer value={detail} /> : null}
      </Modal>
    </div>
  );
}
