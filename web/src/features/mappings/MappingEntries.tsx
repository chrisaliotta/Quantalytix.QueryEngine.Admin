import type { ColumnDef } from '@tanstack/react-table';
import { ClipboardPaste, Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { EntityTable } from '@/shared/EntityTable';
import type { LookupItem } from '@/shared/types';
import { usePublishedDimensionItems } from '@/shared/useLatestVersionItems';
import { useDeleteEntry, useMappingEntries } from './api';
import { EntriesPasteDialog } from './EntriesPasteDialog';
import { EntrySheet } from './EntrySheet';
import type { MappingEntry } from './types';

/** An entry with its member's label, which the table shows and searches. */
interface EntryRow extends MappingEntry {
  memberLabel: string;
}

const baseColumns: ColumnDef<EntryRow>[] = [
  {
    accessorKey: 'sourceKeyValue',
    header: 'Source key',
    cell: ({ row }) => <code className="whitespace-pre">{row.original.sourceKeyValue}</code>,
  },
  { accessorKey: 'memberKey', header: 'Member key' },
  { accessorKey: 'memberLabel', header: 'Member label' },
];

/** What the entry sheet was opened for: an existing entry, or a new one. */
interface EntrySheetRequest {
  entry: MappingEntry | null;
}

export interface MappingEntriesProps {
  versionId: number;
  /** The set's dimension; the entries' member keys resolve in its published version. */
  dimensionId: number;
  isEditable: boolean;
}

/**
 * A mapping version's entries in a table, each with the label of its member in the published version of the set's
 * dimension. On a draft or the published version they are added, edited and deleted one by one, or replaced by a bulk
 * paste; edits to the published version reach the next request.
 */
export function MappingEntries({ versionId, dimensionId, isEditable }: MappingEntriesProps) {
  const entries = useMappingEntries(versionId);
  const members = usePublishedDimensionItems(dimensionId, 'members');
  const deleteEntry = useDeleteEntry(versionId);
  const [sheet, setSheet] = useState<EntrySheetRequest | null>(null);
  const [pasting, setPasting] = useState(false);
  const [deleting, setDeleting] = useState<MappingEntry | null>(null);
  const rows = entries.data && members ? toRows(entries.data, members) : undefined;

  const columns: ColumnDef<EntryRow>[] = [
    ...baseColumns,
    {
      id: 'actions',
      header: '',
      cell: ({ row }) =>
        isEditable && (
          <Button
            variant="ghost"
            size="icon-xs"
            aria-label={`Delete ${row.original.sourceKeyValue}`}
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
    <div data-slot="mapping-entries" className="space-y-3">
      <EntityTable
        columns={columns}
        data={rows}
        isLoading={entries.isPending || members === null}
        error={entries.error}
        searchPlaceholder="Search entries..."
        emptyMessage="No entries yet."
        onRowClick={entry => setSheet({ entry })}
        toolbar={
          isEditable && (
            <div className="ml-auto flex gap-2">
              <Button variant="outline" disabled={rows === undefined} onClick={() => setPasting(true)}>
                <ClipboardPaste />
                Bulk paste
              </Button>
              <Button onClick={() => setSheet({ entry: null })}>
                <Plus />
                New entry
              </Button>
            </div>
          )
        }
      />
      <ApiErrorAlert error={deleteEntry.error} />
      {sheet && (
        <EntrySheet
          versionId={versionId}
          members={members}
          entry={sheet.entry}
          readOnly={!isEditable}
          onClose={() => setSheet(null)}
        />
      )}
      {pasting && (
        <EntriesPasteDialog versionId={versionId} entries={entries.data ?? []} onClose={() => setPasting(false)} />
      )}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={open => !open && setDeleting(null)}
        title={`Delete the entry ${deleting?.sourceKeyValue ?? ''}?`}
        description="The source value no longer maps to a member in this version."
        confirmLabel="Delete entry"
        destructive
        onConfirm={() => deleting && deleteEntry.mutate(deleting.dimensionMappingId)}
      />
    </div>
  );
}

/** The entries with their members' labels; a key the published version lacks is marked. */
function toRows(entries: MappingEntry[], members: LookupItem[]): EntryRow[] {
  const labels = new Map(members.map(member => [member.key, member.label]));
  return entries.map(entry => ({ ...entry, memberLabel: labels.get(entry.memberKey) ?? 'Not a member of the published version' }));
}
