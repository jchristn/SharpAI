import { Icon } from '../Icon';
import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ActionMenu, Column, ConfirmModal, DataTable, Modal } from '../../components/ui';
import { formatBytes, formatDateTime } from '../../i18n/format';
import { normalizeRepository } from '../../api/repository';
import { ApiError } from '../../api/ApiClient';
import { useApi } from '../ApiProvider';

const PULL_EXAMPLE_REPOSITORY = 'Qwen/Qwen3-8B-GGUF';
const PULL_EXAMPLE_URL = `https://huggingface.co/${PULL_EXAMPLE_REPOSITORY}/tree/main`;

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
  const { t, i18n } = useTranslation();
  const api = useApi();
  const [onDisk, setOnDisk] = useState<Model[]>([]);
  const [running, setRunning] = useState<Model[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [confirm, setConfirm] = useState<{ kind: 'delete' | 'unload'; name: string } | null>(null);

  const [pullOpen, setPullOpen] = useState<boolean>(false);
  const [pullName, setPullName] = useState<string>('');
  const [progress, setProgress] = useState<{status?: string; downloaded?: number; percent?: number; file?: string}>({});
  const [pullError, setPullError] = useState<string | null>(null);
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
  }, [api, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const runConfirm = async (): Promise<void> => {
    if (!confirm) return;
    try {
      if (confirm.kind === 'delete') await api.deleteModel(confirm.name);
      else await api.unloadModel(confirm.name);
    } catch (err) {
      setError(err instanceof ApiError ? `${err.status}: ${err.message}` : t('models.pullFailed'));
      setConfirm(null);
      return;
    }
    setConfirm(null);
    void load();
  };

  const runPull = async (): Promise<void> => {
    if (!pullName.trim()) return;
    let repository: string;
    try { repository = normalizeRepository(pullName); } catch (err) { setPullError(t(err instanceof Error ? err.message : 'models.invalidRepository')); return; }
    setPulling(true);
    setPullError(null); setProgress({status:'pulling manifest'});
    try {
      let succeeded=false;
      await api.pullModel(repository, (line) => {const update=JSON.parse(line);succeeded ||= update.status==='success';setProgress(update);});
      if(!succeeded) throw new Error(t('models.pullFailed'));
      setProgress(prev => ({...prev,status:'success',percent:1}));
    } catch (err) {
      setPullError(err instanceof ApiError ? `${err.status}: ${err.message}` : t('models.pullFailed'));
    } finally {
      setPulling(false);
      void load();
    }
  };

  const diskColumns: Column<Model>[] = [
    { key: 'name', width:'42%', header: t('models.colName'), render: (row) => field(row, 'name', 'model', 'Name') },
    { key: 'size', width:'18%', header: t('models.colSize'), render: (row) => formatBytes(Number(field(row, 'size', 'Size')), i18n.language) },
    {key:'modified',width:'30%',header:t('models.colModified'),render:row=>field(row,'modified_at','ModifiedUtc')?formatDateTime(field(row,'modified_at','ModifiedUtc'),i18n.language):'—'},
    {
      key: 'actions', width:'10%',
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
    { key: 'name', width:'42%', header: t('models.colName'), render: (row) => field(row, 'name', 'model', 'Name') },
    { key: 'vram', width:'18%', header: t('models.colVram'), render: (row) => formatBytes(Number(field(row, 'size_vram', 'sizeVram')), i18n.language) },
    { key: 'expires', width:'30%', header: t('models.colExpires'), render: (row) => formatDateTime(field(row, 'expires_at', 'expiresAt'), i18n.language) },
    {
      key: 'actions', width:'10%',
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
    <div className="sa-models-page">
      <div className="sa-page__toolbar">
        <div><h1 className="sa-page__title">{t('models.title')}</h1><p className="sa-heading-description">{t('text.manageDownloadedModelsAndTheModelsCurrently')}</p></div>
        <div style={{ display: 'flex', gap: 'var(--sa-space-2)' }}>
          <button type="button" className="sa-btn--icon sa-btn" onClick={() => void load()} disabled={loading} aria-label={t('common.refresh')}><Icon name="refresh"/></button>
          <button type="button" className="sa-btn--icon sa-btn sa-btn--primary" onClick={() => setPullOpen(true)} aria-label={t('models.pull')}><Icon name="download"/></button>
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
            <button type="button" className="sa-btn sa-btn--primary" aria-label={t(pulling ? 'models.pulling' : 'models.pull')} onClick={() => void runPull()} disabled={pulling || !pullName.trim()}>
              <Icon name="download"/>
            </button>
          </>
        }
      >
        <p id="sa-pull-help" className="sa-page-description" style={{margin:'0 0 16px',overflowWrap:'anywhere'}}>
          {t('models.pullHelp')}<br />
          <code dir="ltr">{PULL_EXAMPLE_REPOSITORY}</code><br />
          <code dir="ltr">{PULL_EXAMPLE_URL}</code>
        </p>
        <input
          aria-label={t('models.pullPlaceholder')}
          aria-describedby="sa-pull-help"
          type="text"
          className="sa-input"
          style={{ width: '100%', padding: 'var(--sa-space-2)' }}
          placeholder={t('models.pullPlaceholder')}
          disabled={pulling}
          value={pullName}
          onChange={(event) => setPullName(event.target.value)}
        />
        {(pulling || progress.status) && <section className="sa-pull-progress" aria-live="polite">
          <strong>{t(progress.status === 'success' ? 'pull.complete' : progress.status === 'pulling manifest' ? 'pull.preparing' : 'pull.downloading')}</strong>
          <progress aria-label={t('pull.progress')} max={1} value={progress.percent} />
          <div className="sa-pull-stats"><span>{progress.percent !== undefined ? new Intl.NumberFormat(i18n.language,{style:'percent',maximumFractionDigits:1}).format(progress.percent) : t('pull.waiting')}</span><span>{formatBytes(progress.downloaded ?? 0,i18n.language)}</span></div>
          {progress.file && <small dir="ltr">{progress.file}</small>}
          <p>{t(progress.status === 'success' ? 'pull.ready' : 'pull.keepOpen')}</p>
        </section>}
        {pullError && <p role="alert" className="sa-error">{pullError}</p>}
      </Modal>
    </div>
  );
}
