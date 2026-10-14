import type { ColumnDef, SortDirection } from '@tanstack/react-table';
import { ArrowDown, ArrowUp, ArrowUpDown, Search } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { DataTable, type DataTableRenderers } from '@registry/components/DataTable';
import { Loading } from '@registry/components/Loading';
import { Input } from '@/components/ui/input';
import { TableCell, TableHead, TableRow } from '@/components/ui/table';
import { cn } from '@/lib/utils';
import { ApiErrorAlert } from './ApiErrorAlert';

export interface EntityTableProps<TRow extends object> {
  /**
   * TanStack column definitions; columns with an accessor sort by clicking their header.
   * `any` is TanStack's recommended value type so `createColumnHelper` columns fit.
   */
  columns: ColumnDef<TRow, any>[];
  /** Rows; `undefined` while the query loads. */
  data: TRow[] | undefined;
  isLoading: boolean;
  error?: Error | null;
  /** Shows a search box that keeps rows with any text or number value containing the term. */
  searchPlaceholder?: string;
  /** Makes rows clickable; stop propagation in cell buttons that do something else. */
  onRowClick?: (row: TRow) => void;
  emptyMessage?: string;
  /** Extra controls next to the search box, e.g. filters. */
  toolbar?: ReactNode;
}

/** Searchable, sortable entity list built on the registry DataTable. */
export function EntityTable<TRow extends object>({
  columns,
  data,
  isLoading,
  error = null,
  searchPlaceholder,
  onRowClick,
  emptyMessage = 'No records yet.',
  toolbar,
}: EntityTableProps<TRow>) {
  const [search, setSearch] = useState('');
  const rows = filterRows(data ?? [], search);

  const renderers: DataTableRenderers<TRow> = {
    renderHeaderCell: ({ header, defaultProps, content }) => {
      const { key, ...props } = defaultProps;
      if (header.isPlaceholder || !header.column.getCanSort()) {
        return (
          <TableHead key={key} {...props}>
            {content}
          </TableHead>
        );
      }
      return (
        <TableHead key={key} {...props}>
          <button
            type="button"
            className="inline-flex items-center gap-1 hover:text-foreground"
            onClick={header.column.getToggleSortingHandler()}
          >
            {content}
            <SortIcon direction={header.column.getIsSorted()} />
          </button>
        </TableHead>
      );
    },
    renderRow: ({ row, defaultProps, cells }) => {
      const { key, ...props } = defaultProps;
      return (
        <TableRow
          key={key}
          {...props}
          className={cn(onRowClick && 'cursor-pointer')}
          onClick={onRowClick && (() => onRowClick(row.original))}
        >
          {cells}
        </TableRow>
      );
    },
    renderEmptyState: ({ colSpan }) => (
      <TableRow>
        <TableCell colSpan={colSpan} className="h-24 text-center text-muted-foreground">
          <EmptyState isLoading={isLoading} search={search} emptyMessage={emptyMessage} />
        </TableCell>
      </TableRow>
    ),
  };

  return (
    <div data-slot="entity-table" className="space-y-3">
      {(searchPlaceholder || toolbar) && (
        <div data-slot="entity-table-toolbar" className="flex flex-wrap items-center gap-2">
          {searchPlaceholder && (
            <div className="relative w-full max-w-xs">
              <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                className="pl-8"
                placeholder={searchPlaceholder}
                value={search}
                onChange={event => setSearch(event.target.value)}
              />
            </div>
          )}
          {toolbar}
        </div>
      )}
      <ApiErrorAlert error={error} />
      <DataTable columns={columns} data={rows} renderers={renderers} className="h-auto border" />
    </div>
  );
}

function SortIcon({ direction }: { direction: false | SortDirection }) {
  if (direction === 'asc') {
    return <ArrowUp className="size-3.5" />;
  }
  if (direction === 'desc') {
    return <ArrowDown className="size-3.5" />;
  }
  return <ArrowUpDown className="size-3.5 opacity-40" />;
}

function EmptyState(props: { isLoading: boolean; search: string; emptyMessage: string }) {
  if (props.isLoading) {
    return <Loading size="sm" />;
  }
  if (props.search.trim()) {
    return <>No records match "{props.search.trim()}".</>;
  }
  return <>{props.emptyMessage}</>;
}

function filterRows<TRow extends object>(rows: TRow[], search: string): TRow[] {
  const term = search.trim().toLowerCase();
  if (!term) {
    return rows;
  }
  return rows.filter(row =>
    Object.values(row).some(
      value =>
        (typeof value === 'string' || typeof value === 'number') &&
        String(value).toLowerCase().includes(term)
    )
  );
}
