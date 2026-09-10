import { CopyButton } from './CopyButton';
import './ui.css';

export interface JsonViewerProps {
  value: unknown;
}

function stringify(value: unknown): string {
  try {
    return JSON.stringify(value, null, 2);
  } catch {
    return String(value);
  }
}

/** Pretty-prints a JSON value in a scrollable monospace block with a copy affordance. */
export function JsonViewer({ value }: JsonViewerProps): JSX.Element {
  const text = stringify(value);
  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
        <CopyButton value={text} label="Copy JSON" />
      </div>
      <pre className="sa-json">{text}</pre>
    </div>
  );
}
