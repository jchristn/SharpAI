import { useTranslation } from 'react-i18next';
import { RequestHistoryPanel } from './RequestHistoryPanel';

/**
 * Inference & embeddings request history — the same charts/table/modals as the API history, restricted
 * server-side to the inference endpoints (Ollama + OpenAI chat/generate/embeddings), with an endpoint
 * sub-filter. Uses the product-standard time-range bucket sizes.
 */
export function InferenceHistoryView(): JSX.Element {
  const { t } = useTranslation();

  // Endpoint sub-filters (value is a pathContains substring within the inference set).
  const endpointOptions = [
    { label: t('inferenceHistory.endpointChat'), value: 'chat' },
    { label: t('inferenceHistory.endpointEmbeddings'), value: 'embed' },
    { label: t('inferenceHistory.endpointGenerate'), value: '/generate' },
  ];

  return (
    <RequestHistoryPanel
      titleKey="inferenceHistory.title"
      category="inference"
      endpointOptions={endpointOptions}
    />
  );
}
