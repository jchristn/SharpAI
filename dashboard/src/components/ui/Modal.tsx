import { useTranslation } from 'react-i18next';
import { useEffect, useId, useRef } from 'react';
import { createPortal } from 'react-dom';
import type { ReactNode } from 'react';
import './ui.css';

export interface ModalProps {
  open: boolean;
  title: ReactNode;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
  wide?: boolean;
  className?: string;
}

/**
 * A portaled modal dialog. Replaces the browser's native alert/confirm/prompt. Closes on Escape and on
 * overlay click; the panel scrolls internally so long content never breaks the page layout.
 */
export function Modal({ open, title, onClose, children, footer, wide = false, className = '' }: ModalProps): JSX.Element | null {
  const { t } = useTranslation();
  const titleId = useId();
  const dialog = useRef<HTMLDivElement>(null);
  const closeRef = useRef(onClose);
  useEffect(() => { closeRef.current = onClose; }, [onClose]);
  useEffect(() => {
    if (!open) return;
    const previous = document.activeElement as HTMLElement | null;
    const focusable = (): HTMLElement[] => Array.from(dialog.current?.querySelectorAll<HTMLElement>('button:not(:disabled), a[href], input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex="0"]') ?? []);
    focusable()[0]?.focus();
    const onKey = (event: KeyboardEvent): void => {
      if (event.key === 'Escape') closeRef.current();
      if (event.key === 'Tab') {
        const elements = focusable();
        const first = elements[0]; const last = elements[elements.length - 1];
        if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
        else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
      }
    };
    window.addEventListener('keydown', onKey);
    return () => { window.removeEventListener('keydown', onKey); previous?.focus(); };
  }, [open]);

  if (!open) return null;

  return createPortal(
    <div className="sa-modal__overlay" onClick={onClose} role="presentation">
      <div
        ref={dialog}
        aria-labelledby={titleId}
        className={`sa-modal ${wide ? 'sa-modal--wide' : ''} ${className}`}
        role="dialog"
        aria-modal="true"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="sa-modal__header">
          <span id={titleId}>{title}</span>
          <button type="button" className="sa-modal__close" aria-label={t('text.close')} onClick={onClose}>
            ×
          </button>
        </div>
        <div className="sa-modal__body">{children}</div>
        {footer ? <div className="sa-modal__footer">{footer}</div> : null}
      </div>
    </div>,
    document.body,
  );
}
