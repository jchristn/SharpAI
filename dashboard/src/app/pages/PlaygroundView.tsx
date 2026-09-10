import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ApiError } from '../../api/ApiClient';
import { useApi } from '../ApiProvider';

type Mode = 'chat' | 'embeddings';

function streamDelta(event: unknown): string {
  const record = (event ?? {}) as Record<string, unknown>;
  const message = record.message as Record<string, unknown> | undefined;
  if (message && typeof message.content === 'string') return message.content;
  if (typeof record.response === 'string') return record.response;
  return '';
}

function extractVector(value: unknown): number[] | null {
  const record = (value ?? {}) as Record<string, unknown>;
  const embedding = record.embedding ?? record.embeddings;
  if (Array.isArray(embedding)) {
    return Array.isArray(embedding[0]) ? (embedding[0] as number[]) : (embedding as number[]);
  }
  return null;
}

/** Inference playground (T7): streaming chat and an embeddings tab. */
export function PlaygroundView(): JSX.Element {
  const { t } = useTranslation();
  const api = useApi();
  const [mode, setMode] = useState<Mode>('chat');
  const [model, setModel] = useState<string>('');
  const [prompt, setPrompt] = useState<string>('');
  const [answer, setAnswer] = useState<string>('');
  const [vector, setVector] = useState<number[] | null>(null);
  const [busy, setBusy] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const send = async (): Promise<void> => {
    if (!model.trim() || !prompt.trim()) return;
    setBusy(true);
    setError(null);
    setAnswer('');
    setVector(null);
    try {
      if (mode === 'chat') {
        await api.chatStream({ model: model.trim(), messages: [{ role: 'user', content: prompt }] }, (event) => {
          setAnswer((prev) => prev + streamDelta(event));
        });
      } else {
        const result = await api.embeddings({ model: model.trim(), input: prompt });
        setVector(extractVector(result));
      }
    } catch (err) {
      setError(err instanceof ApiError ? `${err.status}: ${err.message}` : t('playground.failed'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <div className="sa-page__toolbar">
        <h1 className="sa-page__title">{t('playground.title')}</h1>
        <div style={{ display: 'flex', gap: 'var(--sa-space-2)' }}>
          <button
            type="button"
            className={`sa-btn ${mode === 'chat' ? 'sa-btn--primary' : ''}`}
            onClick={() => setMode('chat')}
          >
            {t('playground.chat')}
          </button>
          <button
            type="button"
            className={`sa-btn ${mode === 'embeddings' ? 'sa-btn--primary' : ''}`}
            onClick={() => setMode('embeddings')}
          >
            {t('playground.embeddings')}
          </button>
        </div>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--sa-space-3)', maxWidth: 820 }}>
        <label className="sa-filterbar__field">
          <span className="sa-filterbar__label">{t('playground.model')}</span>
          <input type="text" value={model} placeholder={t('playground.modelPlaceholder')} onChange={(e) => setModel(e.target.value)} />
        </label>

        <label className="sa-filterbar__field">
          <span className="sa-filterbar__label">{mode === 'chat' ? t('playground.prompt') : t('playground.input')}</span>
          <textarea
            className="sa-json"
            style={{ minHeight: '18vh', resize: 'vertical' }}
            value={prompt}
            onChange={(e) => setPrompt(e.target.value)}
          />
        </label>

        <div>
          <button type="button" className="sa-btn sa-btn--primary" onClick={() => void send()} disabled={busy}>
            {busy ? t('playground.generating') : mode === 'chat' ? t('playground.send') : t('playground.embed')}
          </button>
        </div>

        {error ? <div style={{ color: 'var(--sa-danger)' }}>{error}</div> : null}

        {mode === 'chat' && answer ? (
          <div>
            <div className="sa-filterbar__label">{t('playground.response')}</div>
            <pre className="sa-json" style={{ whiteSpace: 'pre-wrap' }}>{answer}</pre>
          </div>
        ) : null}

        {mode === 'embeddings' && vector ? (
          <div>
            <div className="sa-filterbar__label">
              {t('playground.dimensions')}: {vector.length} — {t('playground.preview')}
            </div>
            <pre className="sa-json" style={{ whiteSpace: 'pre-wrap' }}>
              [{vector.slice(0, 12).map((n) => n.toFixed(4)).join(', ')}
              {vector.length > 12 ? ', …' : ''}]
            </pre>
          </div>
        ) : null}
      </div>
    </div>
  );
}
