import { useState } from 'react';
import './ui.css';

export interface CopyButtonProps {
  value: string;
  label?: string;
}

async function copyToClipboard(value: string): Promise<boolean> {
  try {
    if (navigator.clipboard && navigator.clipboard.writeText) {
      await navigator.clipboard.writeText(value);
      return true;
    }
  } catch {
    // fall through to false
  }
  return false;
}

/** A small button that copies a value to the clipboard and briefly confirms. */
export function CopyButton({ value, label = 'Copy' }: CopyButtonProps): JSX.Element {
  const [copied, setCopied] = useState<boolean>(false);

  const onCopy = async (): Promise<void> => {
    const ok = await copyToClipboard(value);
    if (ok) {
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1200);
    }
  };

  return (
    <button
      type="button"
      className="sa-btn sa-btn--ghost"
      onClick={() => void onCopy()}
      aria-label={label}
    >
      {copied ? 'Copied' : label}
    </button>
  );
}
