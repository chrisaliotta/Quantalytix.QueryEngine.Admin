import type { ColumnDef } from '@tanstack/react-table';
import { Plus, Trash2 } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from './ApiErrorAlert';
import { ConfirmDialog } from './ConfirmDialog';
import { EntityTable } from './EntityTable';
import { useDeleteVersionContent, useVersionContent, type ContentItem, type ContentKind } from './versionContent';

export interface VersionContentTabProps<K extends ContentKind> {
  kind: K;
  /** The model or report version the rows belong to, as the kind's routes name it. */
  versionId: number;
  isEditable: boolean;
  /** The table's columns; the tab adds the delete action. */
  columns: ColumnDef<ContentItem<K>>[];
  /** What a row is called, such as `variable`. */
  noun: string;
  /** A row's ID, which its item routes use, and its key. */
  identify: (item: ContentItem<K>) => { id: number; key: string };
  /** What deleting a row also deletes, or what keeps it. */
  deleteDescription: string;
  /** The sheet that creates a row (`item` null), edits a row of an editable version, or shows a row of any other version. */
  renderSheet: (item: ContentItem<K> | null, onClose: () => void) => ReactNode;
}

/** What the tab's sheet was opened for: an existing row, or a new one. */
interface SheetRequest<K extends ContentKind> {
  item: ContentItem<K> | null;
}

/**
 * One kind of a model or report version's content in a table; a draft or the published version adds, edits and deletes rows
 * in a sheet.
 */
export function VersionContentTab<K extends ContentKind>({
  kind,
  versionId,
  isEditable,
  columns,
  noun,
  identify,
  deleteDescription,
  renderSheet,
}: VersionContentTabProps<K>) {
  const rows = useVersionContent(kind, versionId);
  const deleteRow = useDeleteVersionContent(kind, versionId);
  const [sheet, setSheet] = useState<SheetRequest<K> | null>(null);
  const [deleting, setDeleting] = useState<ContentItem<K> | null>(null);
  const deletingKey = deleting === null ? '' : identify(deleting).key;

  const withActions: ColumnDef<ContentItem<K>>[] = [
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
    <div data-slot="version-content-tab" className="space-y-3">
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
