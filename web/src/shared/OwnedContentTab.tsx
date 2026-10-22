import type { ColumnDef } from '@tanstack/react-table';
import type { ReactNode } from 'react';
import { ContentTab } from './ContentTab';
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

/** One kind of a calculation model's or report's content in a table, whose rows a sheet adds and edits, routed by row ID. */
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

  return (
    <ContentTab
      rows={rows}
      columns={columns}
      noun={noun}
      keyOf={item => identify(item).key}
      deleteDescription={deleteDescription}
      deleteRow={{ mutate: item => deleteRow.mutate(identify(item).id), error: deleteRow.error }}
      renderSheet={renderSheet}
    />
  );
}
