import { useEffect } from 'react';
import { createPortal } from 'react-dom';
import type { ReactNode } from 'react';
import './ui.css';

export interface ModalProps {
  open: boolean;
  title: ReactNode;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
}

/**
 * A portaled modal dialog. Replaces the browser's native alert/confirm/prompt. Closes on Escape and on
 * overlay click; the panel scrolls internally so long content never breaks the page layout.
 */
export function Modal({ open, title, onClose, children, footer }: ModalProps): JSX.Element | null {
  useEffect(() => {
    if (!open) return;
    const onKey = (event: KeyboardEvent): void => {
      if (event.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open, onClose]);

  if (!open) return null;

  return createPortal(
    <div className="sa-modal__overlay" onClick={onClose} role="presentation">
      <div
        className="sa-modal"
        role="dialog"
        aria-modal="true"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="sa-modal__header">
          <span>{title}</span>
          <button type="button" className="sa-modal__close" aria-label="Close" onClick={onClose}>
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
