import { useTranslation } from 'react-i18next';
import { useEffect, useRef, useState } from 'react';
import { copyToClipboard } from './clipboard';
import { Icon } from '../../app/Icon';
import './ui.css';

export interface CopyButtonProps { value: string; label?: string }

export function CopyButton({ value, label }: CopyButtonProps): JSX.Element {
  const { t } = useTranslation();
  const [state, setState] = useState<'idle'|'copied'|'error'>('idle');
  const timer = useRef<number | undefined>(undefined);
  useEffect(() => () => window.clearTimeout(timer.current), []);
  const description = state === 'copied' ? t('common.copied') : state === 'error' ? t('common.copyFailed') : label ?? t('common.copy');
  return <button type="button" className={`sa-btn sa-btn--ghost sa-btn--icon sa-copy-button ${state === 'copied' ? 'sa-copy-button--copied' : ''}`} title={description} aria-label={description} onClick={() => {
    void copyToClipboard(value).then(ok => {
      setState(ok ? 'copied' : 'error');
      window.clearTimeout(timer.current);
      timer.current = window.setTimeout(() => setState('idle'), 1800);
    });
  }}><Icon name={state === 'copied' ? 'check' : 'copy'}/><span className="sa-sr-only" role="status">{state === 'idle' ? '' : description}</span></button>;
}
