import type { ColumnDef } from '@tanstack/react-table';
import { Plus, Trash2 } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from './ApiErrorAlert';
import { ConfirmDialog } from './ConfirmDialog';
import { EntityTable } from './EntityTable';

export interface ContentTabProps<TRow extends object> {
  /** The owner's rows: `undefined` while they load. */
  rows: { data: TRow[] | undefined; isPending: boolean; error: Error | null };
  /** The table's columns; the tab adds the delete action. */
  columns: ColumnDef<TRow>[];
  /** What a row is called, such as `variable`. */
  noun: string;
  /** The key a row is named by in the delete action. */
  keyOf: (row: TRow) => string;
  /** What deleting a row also deletes, or what keeps it. */
  deleteDescription: string;
  /** Deletes a row, and the error of the last delete. */
  deleteRow: { mutate: (row: TRow) => void; error: Error | null };
  /** The sheet that creates a row (`row` null) or edits one. */
  renderSheet: (row: TRow | null, onClose: () => void) => ReactNode;
}

/** An owner's rows of one kind in a table, which a sheet adds and edits and a confirmed action deletes. */
export function ContentTab<TRow extends object>({ rows, columns, noun, keyOf, deleteDescription, deleteRow, renderSheet }: ContentTabProps<TRow>) {
  const [sheet, setSheet] = useState<{ row: TRow | null } | null>(null);
  const [deleting, setDeleting] = useState<TRow | null>(null);
  const deletingKey = deleting === null ? '' : keyOf(deleting);

  const withActions: ColumnDef<TRow>[] = [
    ...columns,
    {
      id: 'actions',
      header: '',
      cell: ({ row }) => (
        <Button
          variant="ghost"
          size="icon-xs"
          aria-label={`Delete ${keyOf(row.original)}`}
          onClick={event => {
            event.stopPropagation();
            setDeleting(row.original);
          }}
        >
          <Trash2 />
        </Button>
      ),
    },
  ];

  return (
    <div data-slot="content-tab" className="space-y-3">
      <EntityTable
        columns={withActions}
        data={rows.data}
        isLoading={rows.isPending}
        error={rows.error}
        searchPlaceholder={`Search ${noun}s...`}
        emptyMessage={`No ${noun}s yet.`}
        onRowClick={row => setSheet({ row })}
        toolbar={
          <Button size="sm" className="ml-auto" onClick={() => setSheet({ row: null })}>
            <Plus />
            New {noun}
          </Button>
        }
      />
      <ApiErrorAlert error={deleteRow.error} />
      {sheet && renderSheet(sheet.row, () => setSheet(null))}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={open => !open && setDeleting(null)}
        title={`Delete ${noun} ${deletingKey}?`}
        description={deleteDescription}
        confirmLabel={`Delete ${noun}`}
        destructive
        onConfirm={() => deleting && deleteRow.mutate(deleting)}
      />
    </div>
  );
}
