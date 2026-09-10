import type { ReactNode } from 'react';
import { Pagination } from './Pagination';
import './ui.css';

export type SortDirection = 'asc' | 'desc';

export interface Column<T> {
  key: string;
  header: ReactNode;
  render?: (row: T) => ReactNode;
  /** Accessor used when no render is provided. */
  accessor?: (row: T) => ReactNode;
  sortable?: boolean;
  width?: string;
}

export interface DataTableProps<T> {
  columns: Column<T>[];
  rows: T[];
  rowKey: (row: T) => string;
  loading?: boolean;
  error?: string | null;
  emptyMessage?: string;
  sortColumn?: string;
  sortDirection?: SortDirection;
  onSort?: (columnKey: string, direction: SortDirection) => void;
  pagination?: {
    pageNumber: number;
    pageSize: number;
    totalRecords: number;
    onPageChange: (pageNumber: number) => void;
    onPageSizeChange: (pageSize: number) => void;
  };
}

/**
 * A generic data table with explicit loading/error/empty states, sortable headers (delegated to the
 * caller so sorting can be backend-driven), and above-the-table pagination. Wide content scrolls inside
 * its own container so the page body never scrolls horizontally.
 */
export function DataTable<T>(props: DataTableProps<T>): JSX.Element {
  const { columns, rows, rowKey, loading, error, emptyMessage, sortColumn, sortDirection, onSort, pagination } = props;

  const headerClick = (column: Column<T>): void => {
    if (!column.sortable || !onSort) return;
    const nextDirection: SortDirection = sortColumn === column.key && sortDirection === 'asc' ? 'desc' : 'asc';
    onSort(column.key, nextDirection);
  };

  const sortIndicator = (column: Column<T>): string => {
    if (sortColumn !== column.key) return '';
    return sortDirection === 'asc' ? ' ▲' : ' ▼';
  };

  return (
    <div>
      {pagination ? <Pagination {...pagination} /> : null}
      <div className="sa-table__wrap">
        <table className="sa-table">
          <thead>
            <tr>
              {columns.map((column) => (
                <th
                  key={column.key}
                  className={column.sortable ? 'sa-sortable' : undefined}
                  style={column.width ? { width: column.width } : undefined}
                  onClick={() => headerClick(column)}
                  aria-sort={
                    sortColumn === column.key ? (sortDirection === 'asc' ? 'ascending' : 'descending') : undefined
                  }
                >
                  {column.header}
                  {sortIndicator(column)}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td className="sa-table__state" colSpan={columns.length}>
                  Loading…
                </td>
              </tr>
            ) : error ? (
              <tr>
                <td className="sa-table__state" colSpan={columns.length}>
                  {error}
                </td>
              </tr>
            ) : rows.length === 0 ? (
              <tr>
                <td className="sa-table__state" colSpan={columns.length}>
                  {emptyMessage ?? 'No records.'}
                </td>
              </tr>
            ) : (
              rows.map((row) => (
                <tr key={rowKey(row)}>
                  {columns.map((column) => (
                    <td key={column.key}>
                      {column.render ? column.render(row) : column.accessor ? column.accessor(row) : null}
                    </td>
                  ))}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
