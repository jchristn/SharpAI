import { RequestHistoryPanel } from './RequestHistoryPanel';

/** API request history — all captured HTTP requests, with charts, table, and detail modals. */
export function RequestHistoryView(): JSX.Element {
  return <RequestHistoryPanel titleKey="requestHistory.title" />;
}
