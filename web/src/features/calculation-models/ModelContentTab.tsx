import type { ColumnDef } from '@tanstack/react-table';
import { Plus, Trash2 } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { EntityTable } from '@/shared/EntityTable';
import { useDeleteModelContent, useModelContent } from './api';
import type { ModelContentItem, ModelContentKind } from './types';

export interface ModelContentTabProps<K extends ModelContentKind> {
  kind: K;
  versionId: number;
  isEditable: boolean;
  /** The table's columns; the tab adds the delete action. */
  columns: ColumnDef<ModelContentItem<K>>[];
  /** What a row is called, such as `variable`. */
  noun: string;
  /** A row's ID, which its item routes use, and its key. */
  identify: (item: ModelContentItem<K>) => { id: number; key: string };
  /** What deleting a row also deletes, or what keeps it. */
  deleteDescription: string;
  /** The sheet that creates a row (`item` null), edits a row of an editable version, or shows a row of any other version. */
  renderSheet: (item: ModelContentItem<K> | null, onClose: () => void) => ReactNode;
}

/** What the tab's sheet was opened for: an existing row, or a new one. */
interface SheetRequest<K extends ModelContentKind> {
  item: ModelContentItem<K> | null;
}

/** One kind of a model version's content in a table; a draft or the published version adds, edits and deletes rows in a sheet. */
export function ModelContentTab<K extends ModelContentKind>({
  kind,
  versionId,
  isEditable,
  columns,
  noun,
  identify,
  deleteDescription,
  renderSheet,
}: ModelContentTabProps<K>) {
  const rows = useModelContent(kind, versionId);
  const deleteRow = useDeleteModelContent(kind, versionId);
  const [sheet, setSheet] = useState<SheetRequest<K> | null>(null);
  const [deleting, setDeleting] = useState<ModelContentItem<K> | null>(null);
  const deletingKey = deleting === null ? '' : identify(deleting).key;

  const withActions: ColumnDef<ModelContentItem<K>>[] = [
    ...columns,
    {
      id: 'actions',
      header: '',
      cell: ({ row }) =>
        isEditable && (
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
    <div data-slot="model-content-tab" className="space-y-3">
      <EntityTable
        columns={withActions}
        data={rows.data}
        isLoading={rows.isPending}
        error={rows.error}
        searchPlaceholder={`Search ${noun}s...`}
        emptyMessage={`No ${noun}s yet.`}
        onRowClick={item => setSheet({ item })}
        toolbar={
          isEditable && (
            <Button size="sm" className="ml-auto" onClick={() => setSheet({ item: null })}>
              <Plus />
              New {noun}
            </Button>
          )
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
