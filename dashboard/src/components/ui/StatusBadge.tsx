import type { ReactNode } from 'react';
import './ui.css';

export type BadgeTone = 'neutral' | 'success' | 'warning' | 'danger' | 'info';

export interface StatusBadgeProps {
  tone?: BadgeTone;
  dot?: boolean;
  children: ReactNode;
}

/**
 * A compact status pill. Color is never the sole signal — a text label always accompanies the tone, and
 * an optional leading dot aids scanning.
 */
export function StatusBadge({ tone = 'neutral', dot = false, children }: StatusBadgeProps): JSX.Element {
  return (
    <span className={`sa-badge sa-badge--${tone}`}>
      {dot ? <span className="sa-badge__dot" aria-hidden="true" /> : null}
      {children}
    </span>
  );
}

/** Map a numeric HTTP status code to a badge tone. */
export function toneForHttpStatus(status: number): BadgeTone {
  if (status >= 500) return 'danger';
  if (status >= 400) return 'warning';
  if (status >= 200 && status < 300) return 'success';
  return 'neutral';
}
