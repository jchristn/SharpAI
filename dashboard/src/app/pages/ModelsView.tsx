import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ActionMenu, Column, ConfirmModal, DataTable, Modal } from '../../components/ui';
import { ApiError } from '../../api/ApiClient';
import { useApi } from '../ApiProvider';

type Model = Record<string, unknown>;

function models(value: unknown): Model[] {
  const record = (value ?? {}) as Record<string, unknown>;
  const list = record.models ?? record.Models;
  return Array.isArray(list) ? (list as Model[]) : [];
}

function field(row: Model, ...keys: string[]): string {
  for (const key of keys) {
    if (row[key] !== undefined && row[key] !== null) return String(row[key]);
  }
  return '';
}

/** Model management (T7): local models, running models, delete/unload with confirmation, and streaming pull. */
export function ModelsView(): JSX.Element {
  const { t } = useTranslation();
  const api = useApi();
  const [onDisk, setOnDisk] = useState<Model[]>([]);
  const [running, setRunning] = useState<Model[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [confirm, setConfirm] = useState<{ kind: 'delete' | 'unload'; name: string } | null>(null);

  const [pullOpen, setPullOpen] = useState<boolean>(false);
  const [pullName, setPullName] = useState<string>('');
  const [pullLog, setPullLog] = useState<string[]>([]);
  const [pulling, setPulling] = useState<boolean>(false);

  const load = useCallback(async (): Promise<void> => {
    setLoading(true);
    setError(null);
    try {
      const [tags, ps] = await Promise.all([api.listModels(), api.runningModels()]);
      setOnDisk(models(tags));
      setRunning(models(ps));
    } catch (err) {
      setError(err instanceof ApiError ? `${err.status}: ${err.message}` : t('models.loadError'));
    } finally {
      setLoading(false);
    }
  }, [api]);

  useEffect(() => {
    void load();
  }, [load]);

  const runConfirm = async (): Promise<void> => {
    if (!confirm) return;
    try {
      if (confirm.kind === 'delete') await api.deleteModel(confirm.name);
      else await api.unloadModel(confirm.name);
    } finally {
      setConfirm(null);
      void load();
    }
  };

  const runPull = async (): Promise<void> => {
    if (!pullName.trim()) return;
    setPulling(true);
    setPullLog([]);
    try {
      await api.pullModel(pullName.trim(), (line) => setPullLog((prev) => [...prev, line]));
    } catch (err) {
      setPullLog((prev) => [...prev, err instanceof ApiError ? `Error ${err.status}: ${err.message}` : t('models.pullFailed')]);
    } finally {
      setPulling(false);
      void load();
    }
  };

  const diskColumns: Column<Model>[] = [
    { key: 'name', header: t('models.colName'), render: (row) => field(row, 'name', 'model', 'Name') },
    { key: 'size', header: t('models.colSize'), render: (row) => field(row, 'size', 'Size') },
    {
      key: 'actions',
      header: '',
      render: (row) => (
        <ActionMenu
          items={[
            { label: t('models.delete'), danger: true, onClick: () => setConfirm({ kind: 'delete', name: field(row, 'name', 'model', 'Name') }) },
          ]}
        />
      ),
    },
  ];

  const runningColumns: Column<Model>[] = [
    { key: 'name', header: t('models.colName'), render: (row) => field(row, 'name', 'model', 'Name') },
    { key: 'vram', header: t('models.colVram'), render: (row) => field(row, 'size_vram', 'sizeVram') },
    { key: 'expires', header: t('models.colExpires'), render: (row) => field(row, 'expires_at', 'expiresAt') },
    {
      key: 'actions',
      header: '',
      render: (row) => (
        <ActionMenu
          items={[
            { label: t('models.unload'), onClick: () => setConfirm({ kind: 'unload', name: field(row, 'name', 'model', 'Name') }) },
          ]}
        />
      ),
    },
  ];

  return (
    <div>
      <div className="sa-page__toolbar">
        <h1 className="sa-page__title">{t('models.title')}</h1>
        <div style={{ display: 'flex', gap: 'var(--sa-space-2)' }}>
          <button type="button" className="sa-btn" onClick={() => void load()} disabled={loading}>
            {t('common.refresh')}
          </button>
          <button type="button" className="sa-btn sa-btn--primary" onClick={() => setPullOpen(true)}>
            {t('models.pull')}
          </button>
        </div>
      </div>

      <h2 style={{ fontSize: 'var(--sa-text-lg)' }}>{t('models.running')}</h2>
      <DataTable<Model>
        columns={runningColumns}
        rows={running}
        rowKey={(row) => field(row, 'name', 'model', 'Name')}
        loading={loading}
        error={error}
        emptyMessage={t('models.emptyRunning')}
      />

      <h2 style={{ fontSize: 'var(--sa-text-lg)', marginTop: 'var(--sa-space-5)' }}>{t('models.onDisk')}</h2>
      <DataTable<Model>
        columns={diskColumns}
        rows={onDisk}
        rowKey={(row) => field(row, 'name', 'model', 'Name')}
        loading={loading}
        error={error}
        emptyMessage={t('models.emptyDisk')}
      />

      <ConfirmModal
        open={confirm !== null}
        title={confirm?.kind === 'delete' ? t('models.deleteTitle') : t('models.unloadTitle')}
        message={confirm ? (confirm.kind === 'delete' ? t('models.deleteConfirm', { name: confirm.name }) : t('models.unloadConfirm', { name: confirm.name })) : ''}
        confirmLabel={confirm?.kind === 'delete' ? t('models.delete') : t('models.unload')}
        danger={confirm?.kind === 'delete'}
        onConfirm={() => void runConfirm()}
        onCancel={() => setConfirm(null)}
      />

      <Modal
        open={pullOpen}
        title={t('models.pullTitle')}
        onClose={() => setPullOpen(false)}
        footer={
          <>
            <button type="button" className="sa-btn" onClick={() => setPullOpen(false)}>
              {t('models.close')}
            </button>
            <button type="button" className="sa-btn sa-btn--primary" onClick={() => void runPull()} disabled={pulling}>
              {pulling ? t('models.pulling') : t('models.pull')}
            </button>
          </>
        }
      >
        <input
          type="text"
          className="sa-json"
          style={{ width: '100%', padding: 'var(--sa-space-2)' }}
          placeholder={t('models.pullPlaceholder')}
          value={pullName}
          onChange={(event) => setPullName(event.target.value)}
        />
        {pullLog.length > 0 ? (
          <pre className="sa-json" style={{ marginTop: 'var(--sa-space-3)', maxHeight: '40vh' }}>
            {pullLog.join('\n')}
          </pre>
        ) : null}
      </Modal>
    </div>
  );
}
