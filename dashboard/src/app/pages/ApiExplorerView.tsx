import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ConfirmModal, CopyButton, JsonViewer, StatusBadge, toneForHttpStatus } from '../../components/ui';
import type { BadgeTone } from '../../components/ui';
import { ApiError } from '../../api/ApiClient';
import { useApi } from '../ApiProvider';

interface Operation {
  method: string;
  path: string;
  tag: string;
  summary: string;
  description: string;
}

const METHODS = ['get', 'post', 'put', 'delete', 'patch'];

function toneForMethod(method: string): BadgeTone {
  switch (method) {
    case 'GET':
      return 'info';
    case 'DELETE':
      return 'danger';
    case 'POST':
    case 'PUT':
    case 'PATCH':
      return 'warning';
    default:
      return 'neutral';
  }
}

function parseOperations(spec: unknown): Operation[] {
  const record = (spec ?? {}) as Record<string, unknown>;
  const paths = (record.paths ?? {}) as Record<string, unknown>;
  const operations: Operation[] = [];
  for (const [path, pathItem] of Object.entries(paths)) {
    const item = (pathItem ?? {}) as Record<string, unknown>;
    for (const method of METHODS) {
      const op = item[method] as Record<string, unknown> | undefined;
      if (!op) continue;
      const tags = Array.isArray(op.tags) ? (op.tags as string[]) : [];
      operations.push({
        method: method.toUpperCase(),
        path,
        tag: tags[0] ?? 'Other',
        summary: typeof op.summary === 'string' ? op.summary : '',
        description: typeof op.description === 'string' ? op.description : '',
      });
    }
  }
  return operations;
}

/** API Explorer (T6): browse the live OpenAPI document grouped by tag, with a details panel and copyable curl. */
export function ApiExplorerView(): JSX.Element {
  const { t } = useTranslation();
  const api = useApi();
  const [operations, setOperations] = useState<Operation[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<Operation | null>(null);
  const [pathValue, setPathValue] = useState<string>('');
  const [body, setBody] = useState<string>('');
  const [running, setRunning] = useState<boolean>(false);
  const [result, setResult] = useState<{ status: number; body: unknown } | null>(null);
  const [confirmOpen, setConfirmOpen] = useState<boolean>(false);
  const baseUrl = (typeof window !== 'undefined' ? window.location.origin : '');

  useEffect(() => {
    setPathValue(selected?.path ?? '');
    setBody('');
    setResult(null);
  }, [selected]);

  const runRequest = async (): Promise<void> => {
    if (!selected) return;
    setRunning(true);
    setResult(null);
    try {
      const response = await api.execute(selected.method, pathValue, selected.method === 'GET' ? undefined : body || undefined);
      setResult({ status: response.status, body: response.body });
    } catch {
      setResult({ status: 0, body: t('apiExplorer.loadError') });
    } finally {
      setRunning(false);
    }
  };

  const onSend = (): void => {
    if (!selected) return;
    if (selected.method === 'DELETE') setConfirmOpen(true);
    else void runRequest();
  };

  useEffect(() => {
    let cancelled = false;
    api
      .openapi()
      .then((spec) => {
        if (!cancelled) setOperations(parseOperations(spec));
      })
      .catch((err: unknown) => {
        if (!cancelled) setError(err instanceof ApiError ? `${err.status}: ${err.message}` : t('apiExplorer.loadError'));
      });
    return () => {
      cancelled = true;
    };
  }, [api]);

  const groups = useMemo(() => {
    const byTag = new Map<string, Operation[]>();
    for (const op of operations) {
      const list = byTag.get(op.tag) ?? [];
      list.push(op);
      byTag.set(op.tag, list);
    }
    return Array.from(byTag.entries()).sort((a, b) => a[0].localeCompare(b[0]));
  }, [operations]);

  const curl = selected ? `curl -X ${selected.method} "${baseUrl}${pathValue}"` : '';

  return (
    <div>
      <h1 className="sa-page__title">{t('apiExplorer.title')}</h1>
      {error ? <StatusBadge tone="danger">{error}</StatusBadge> : null}

      <div style={{ display: 'grid', gridTemplateColumns: 'minmax(280px, 360px) 1fr', gap: 'var(--sa-space-4)' }}>
        <div style={{ maxHeight: '70vh', overflowY: 'auto' }}>
          {groups.map(([tag, ops]) => (
            <div key={tag} style={{ marginBottom: 'var(--sa-space-4)' }}>
              <div className="sa-sidebar__group-label">{tag}</div>
              {ops.map((op) => (
                <button
                  key={`${op.method} ${op.path}`}
                  type="button"
                  className={`sa-nav-item ${selected === op ? 'sa-nav-item--active' : ''}`}
                  onClick={() => setSelected(op)}
                  style={{ display: 'flex', alignItems: 'center', gap: 'var(--sa-space-2)' }}
                >
                  <StatusBadge tone={toneForMethod(op.method)}>{op.method}</StatusBadge>
                  <span style={{ fontFamily: 'var(--sa-font-mono)', fontSize: 'var(--sa-text-xs)' }}>{op.path}</span>
                </button>
              ))}
            </div>
          ))}
        </div>

        <div>
          {selected ? (
            <div className="sa-kpi">
              <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--sa-space-2)' }}>
                <StatusBadge tone={toneForMethod(selected.method)}>{selected.method}</StatusBadge>
                <span style={{ fontFamily: 'var(--sa-font-mono)' }}>{selected.path}</span>
              </div>
              {selected.summary ? <p style={{ fontWeight: 600 }}>{selected.summary}</p> : null}
              {selected.description ? <p style={{ color: 'var(--sa-text-muted)' }}>{selected.description}</p> : null}

              <label className="sa-filterbar__field" style={{ marginTop: 'var(--sa-space-3)' }}>
                <span className="sa-filterbar__label">{t('apiExplorer.path')}</span>
                <input type="text" value={pathValue} onChange={(e) => setPathValue(e.target.value)} />
              </label>

              {selected.method !== 'GET' ? (
                <label className="sa-filterbar__field" style={{ marginTop: 'var(--sa-space-2)' }}>
                  <span className="sa-filterbar__label">{t('apiExplorer.requestBody')}</span>
                  <textarea
                    className="sa-json"
                    style={{ minHeight: '16vh', resize: 'vertical' }}
                    value={body}
                    onChange={(e) => setBody(e.target.value)}
                  />
                </label>
              ) : null}

              <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--sa-space-2)', marginTop: 'var(--sa-space-3)' }}>
                <button type="button" className="sa-btn sa-btn--primary" onClick={onSend} disabled={running}>
                  {running ? t('apiExplorer.sending') : t('apiExplorer.send')}
                </button>
                <code className="sa-json" style={{ flex: 1 }}>{curl}</code>
                <CopyButton value={curl} label={t('apiExplorer.copyCurl')} />
              </div>

              {result ? (
                <div style={{ marginTop: 'var(--sa-space-3)' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--sa-space-2)' }}>
                    <span className="sa-filterbar__label">{t('apiExplorer.response')}</span>
                    <StatusBadge tone={toneForHttpStatus(result.status)}>{result.status || '—'}</StatusBadge>
                  </div>
                  <JsonViewer value={result.body} />
                </div>
              ) : null}
            </div>
          ) : (
            <div style={{ color: 'var(--sa-text-muted)' }}>{t('apiExplorer.select')}</div>
          )}
        </div>
      </div>

      <ConfirmModal
        open={confirmOpen}
        title={t('apiExplorer.confirmTitle')}
        message={t('apiExplorer.confirmMessage')}
        confirmLabel={t('apiExplorer.send')}
        danger
        onConfirm={() => {
          setConfirmOpen(false);
          void runRequest();
        }}
        onCancel={() => setConfirmOpen(false)}
      />
    </div>
  );
}
