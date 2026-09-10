import type { ReactNode } from 'react';
import { Modal } from './Modal';
import './ui.css';

export interface ConfirmModalProps {
  open: boolean;
  title: ReactNode;
  message: ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  danger?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/** A confirmation dialog for destructive or bulk actions. Never uses the native confirm(). */
export function ConfirmModal({
  open,
  title,
  message,
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  danger = false,
  onConfirm,
  onCancel,
}: ConfirmModalProps): JSX.Element {
  return (
    <Modal
      open={open}
      title={title}
      onClose={onCancel}
      footer={
        <>
          <button type="button" className="sa-btn" onClick={onCancel}>
            {cancelLabel}
          </button>
          <button
            type="button"
            className={`sa-btn ${danger ? 'sa-btn--danger' : 'sa-btn--primary'}`}
            onClick={onConfirm}
          >
            {confirmLabel}
          </button>
        </>
      }
    >
      {message}
    </Modal>
  );
}
