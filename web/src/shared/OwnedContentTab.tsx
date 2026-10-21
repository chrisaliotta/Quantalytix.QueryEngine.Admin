import type { ColumnDef } from '@tanstack/react-table';
import { Plus, Trash2 } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from './ApiErrorAlert';
import { ConfirmDialog } from './ConfirmDialog';
import { EntityTable } from './EntityTable';
import { useDeleteOwnedContent, useOwnedContent, type ContentItem, type ContentKind } from './ownedContent';

export interface OwnedContentTabProps<K extends ContentKind> {
  kind: K;
  /** The calculation model or report the rows belong to, as the kind's routes name it. */
  ownerId: number;
  /** The table's columns; the tab adds the delete action. */
  columns: ColumnDef<ContentItem<K>>[];
  /** What a row is called, such as `variable`. */
  noun: string;
  /** A row's ID, which its item routes use, and its key. */
  identify: (item: ContentItem<K>) => { id: number; key: string };
  /** What deleting a row also deletes, or what keeps it. */
  deleteDescription: string;
  /** The sheet that creates a row (`item` null) or edits one. */
  renderSheet: (item: ContentItem<K> | null, onClose: () => void) => ReactNode;
}

/** What the tab's sheet was opened for: an existing row, or a new one. */
interface SheetRequest<K extends ContentKind> {
  item: ContentItem<K> | null;
}

/** One kind of a calculation model's or report's content in a table, whose rows a sheet adds and edits. */
export function OwnedContentTab<K extends ContentKind>({
  kind,
  ownerId,
  columns,
  noun,
  identify,
  deleteDescription,
  renderSheet,
}: OwnedContentTabProps<K>) {
  const rows = useOwnedContent(kind, ownerId);
  const deleteRow = useDeleteOwnedContent(kind, ownerId);
  const [sheet, setSheet] = useState<SheetRequest<K> | null>(null);
  const [deleting, setDeleting] = useState<ContentItem<K> | null>(null);
  const deletingKey = deleting === null ? '' : identify(deleting).key;

  const withActions: ColumnDef<ContentItem<K>>[] = [
    ...columns,
    {
      id: 'actions',
      header: '',
      cell: ({ row }) => (
        <Button
          variant="ghost"
          size="icon-xs"
          aria-label={`Delete ${identify(row.original).key}`}
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
    <div data-slot="owned-content-tab" className="space-y-3">
      <EntityTable
        columns={withActions}
        data={rows.data}
        isLoading={rows.isPending}
        error={rows.error}
        searchPlaceholder={`Search ${noun}s...`}
        emptyMessage={`No ${noun}s yet.`}
        onRowClick={item => setSheet({ item })}
        toolbar={
          <Button size="sm" className="ml-auto" onClick={() => setSheet({ item: null })}>
            <Plus />
            New {noun}
          </Button>
        }
      />
      <ApiErrorAlert error={deleteRow.error} />
      {sheet && renderSheet(sheet.item, () => setSheet(null))}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={open => !open && setDeleting(null)}
        title={`Delete ${noun} ${deletingKey}?`}
        description={deleteDescription}
        confirmLabel={`Delete ${noun}`}
        destructive
        onConfirm={() => deleting && deleteRow.mutate(identify(deleting).id)}
      />
    </div>
  );
}
