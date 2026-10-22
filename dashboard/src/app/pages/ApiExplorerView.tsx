import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ConfirmModal, CopyButton, JsonViewer, StatusBadge, toneForHttpStatus } from '../../components/ui';
import operations from '../../api/operations.json';
import { useApi } from '../ApiProvider';

/** A request workbench backed by the checked-in server operation catalog. */
export function ApiExplorerView(): JSX.Element {
  const { t } = useTranslation();
  const api = useApi();
  const [index, setIndex] = useState(operations.findIndex(op => op.path === '/health'));
  const selected = operations[index];
  const [path, setPath] = useState('/health');
  const [body, setBody] = useState('');
  const [parameters, setParameters] = useState<Record<string,string>>({});
  const [error, setError] = useState<string | null>(null);
  const [running, setRunning] = useState(false);
  const [result, setResult] = useState<{status:number;body:unknown} | null>(null);
  const [confirm, setConfirm] = useState(false);
  const resolvedPath = path.replace(/\{([^}]+)\}/g, (_, key: string) => encodeURIComponent(parameters[key] ?? ''));
  const parameterNames = Array.from(path.matchAll(/\{([^}]+)\}/g), match => match[1]);
  const quote = (text: string): string => "'" + text.replaceAll("'", "'\"'\"'") + "'";
  const curl = `curl -X ${selected.method} ${quote(window.location.origin + resolvedPath)} -H "Authorization: Bearer $SHARPAI_BEARER_TOKEN"${body ? ` -H 'Content-Type: application/json' --data-raw ${quote(body)}` : ''}`;

  const run = async (): Promise<void> => {
    setRunning(true); setError(null); setResult(null);
    try { setResult(await api.execute(selected.method, resolvedPath, selected.method === 'GET' ? undefined : body || undefined)); }
    catch { setError(t('apiExplorer.requestError')); }
    finally { setRunning(false); }
  };
  const send = (): void => {
    if (!resolvedPath.startsWith('/') || parameterNames.some(key => !parameters[key]?.trim())) { setError(t('apiExplorer.invalidPath')); return; }
    if (body) { try { JSON.parse(body); } catch { setError(t('apiExplorer.invalidBody')); return; } }
    if (selected.method === 'DELETE') setConfirm(true); else void run();
  };
  return <div>
    <h1 className="sa-page__title">{t('apiExplorer.title')}</h1>
    <p className="sa-heading-description">{t('apiExplorer.description')}</p>
    <label className="sa-filterbar__field sa-operation-picker"><span>{t('apiExplorer.operation')}</span>
      <select value={index} disabled={running} onChange={event => { const next = Number(event.target.value); setIndex(next); setPath(operations[next].path); setParameters({}); setBody(operations[next].example ? JSON.stringify(operations[next].example, null, 2) : ''); setResult(null); setError(null); }}>
        {Array.from(new Set(operations.map(op => op.tag))).map(tag => <optgroup key={tag} label={t(`apiGroups.${tag}`)}>{operations.map((op,i) => op.tag === tag && <option key={i} value={i}>{op.method} {op.path}</option>)}</optgroup>)}
      </select>
    </label>
    <form className="sa-kpi" onSubmit={event => { event.preventDefault(); send(); }}>
      <div style={{ display:'flex',gap:10,alignItems:'center',flexWrap:'wrap' }}><StatusBadge tone="info">{selected.method}</StatusBadge><code>{selected.path}</code></div>
      <p>{t(`apiOperations.${selected.method} ${selected.path}`)}</p>
      <label className="sa-filterbar__field"><span>{t('apiExplorer.path')}</span><input value={path} onChange={event => setPath(event.target.value)} required disabled={running} /></label>
      {parameterNames.map(key => <label key={key} className="sa-filterbar__field" style={{ marginTop:12 }}><span>{t('apiExplorer.pathParameter', {name:key})}</span><input required value={parameters[key] ?? ''} onChange={event => setParameters({...parameters,[key]:event.target.value})} disabled={running} /></label>)}
      {selected.method !== 'GET' && <label className="sa-filterbar__field" style={{ marginTop:16 }}><span>{t('apiExplorer.requestBody')}</span><textarea className="sa-json" rows={8} value={body} onChange={event => setBody(event.target.value)} disabled={running} /></label>}
      <div className="sa-request-actions"><button className="sa-btn sa-btn--primary" disabled={running}>{running ? t('apiExplorer.sending') : t('apiExplorer.send')}</button><code className="sa-json">{curl}</code><CopyButton value={curl} label={t('apiExplorer.copyCurl')} /></div>
      {error && <p role="alert" style={{color:'var(--sa-danger)'}}>{error}</p>}
      {result && <section style={{marginTop:24}} aria-label={t('apiExplorer.response')}><StatusBadge tone={toneForHttpStatus(result.status)}>{result.status}</StatusBadge><JsonViewer value={result.body} /></section>}
    </form>
    <ConfirmModal open={confirm} title={t('apiExplorer.confirmTitle')} message={t('apiExplorer.confirmMessage')} danger confirmLabel={t('apiExplorer.send')} onConfirm={() => {setConfirm(false);void run();}} onCancel={() => setConfirm(false)} />
  </div>;
}
