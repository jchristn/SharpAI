import { useTranslation } from 'react-i18next';
import { formatNumber } from '../../i18n/format';
import './ui.css';

/** Mandated page-size options; default 25 (DASHBOARD_STYLE_AND_USABILITY). */
export const PAGE_SIZES: number[] = [10, 25, 50, 100, 250, 500, 1000];
export const DEFAULT_PAGE_SIZE = 25;

export interface PaginationProps {
  pageNumber: number;
  pageSize: number;
  totalRecords: number;
  onPageChange: (pageNumber: number) => void;
  onPageSizeChange: (pageSize: number) => void;
}

/** Above-the-table pagination with page-size selection and a records summary. */
export function Pagination({
  pageNumber,
  pageSize,
  totalRecords,
  onPageChange,
  onPageSizeChange,
}: PaginationProps): JSX.Element {
  const { t, i18n } = useTranslation();
  const totalPages = Math.max(1, Math.ceil(totalRecords / pageSize));
  const clampedPage = Math.min(Math.max(1, pageNumber), totalPages);
  const first = totalRecords === 0 ? 0 : (clampedPage - 1) * pageSize + 1;
  const last = Math.min(clampedPage * pageSize, totalRecords);

  return (
    <div className="sa-pagination">
      <span>
        {t('pagination.range',{first:formatNumber(first,i18n.language),last:formatNumber(last,i18n.language),total:formatNumber(totalRecords,i18n.language)})}
      </span>
      <div className="sa-pagination__controls">
        <label>
          {t('text.rows')}{' '}
          <select
            value={pageSize}
            onChange={(event) => onPageSizeChange(Number(event.target.value))}
            aria-label={t('text.rowsPerPage')}
          >
            {PAGE_SIZES.map((size) => (
              <option key={size} value={size}>
                {size}
              </option>
            ))}
          </select>
        </label>
        <button
          type="button"
          className="sa-btn"
          disabled={clampedPage <= 1}
          onClick={() => onPageChange(clampedPage - 1)}
          aria-label={t('text.previousPage')}
        >
          {t('text.prev')}</button>
        <span>
          {clampedPage} / {totalPages}
        </span>
        <button
          type="button"
          className="sa-btn"
          disabled={clampedPage >= totalPages}
          onClick={() => onPageChange(clampedPage + 1)}
          aria-label={t('text.nextPage')}
        >
          {t('text.next')}</button>
      </div>
    </div>
  );
}
