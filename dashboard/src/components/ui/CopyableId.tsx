import { CopyButton } from './CopyButton';
import './ui.css';

export interface CopyableIdProps {
  value: string;
  /** Number of leading characters to show before truncating with an ellipsis. 0 shows the full value. */
  truncateTo?: number;
}

/** Renders a monospace identifier with a copy affordance, optionally truncated for dense tables. */
export function CopyableId({ value, truncateTo = 0 }: CopyableIdProps): JSX.Element {
  const display = truncateTo > 0 && value.length > truncateTo ? `${value.slice(0, truncateTo)}…` : value;
  return (
    <span className="sa-copy">
      <span className="sa-copy__id" title={value}>
        {display}
      </span>
      <CopyButton value={value} label="Copy id" />
    </span>
  );
}
