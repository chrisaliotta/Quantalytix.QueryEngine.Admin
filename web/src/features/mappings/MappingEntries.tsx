import type { ColumnDef } from '@tanstack/react-table';
import { ClipboardPaste, Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { EntityTable } from '@/shared/EntityTable';
import type { LookupItem } from '@/shared/types';
import { useLookup } from '@/shared/useLookup';
import { useDeleteEntry, useMappingEntries } from './api';
import { EntriesPasteDialog } from './EntriesPasteDialog';
import { EntrySheet } from './EntrySheet';
import type { MappingEntry } from './types';

/** An entry with its member's key and label, which the table shows and searches. */
interface EntryRow extends MappingEntry {
  memberKey: string;
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
  /** The dimension version the mapping version pins; the entries map to its members. */
  dimensionVersionId: number | null;
  isDraft: boolean;
}

/**
 * A mapping version's entries in a table. On a draft they are added, edited and deleted one by one, or replaced by a
 * bulk paste.
 */
export function MappingEntries({ versionId, dimensionVersionId, isDraft }: MappingEntriesProps) {
  const entries = useMappingEntries(versionId);
  const members = useLookup('members', dimensionVersionId);
  const deleteEntry = useDeleteEntry(versionId);
  const [sheet, setSheet] = useState<EntrySheetRequest | null>(null);
  const [pasting, setPasting] = useState(false);
  const [deleting, setDeleting] = useState<MappingEntry | null>(null);
  const rows = entries.data && members.data ? toRows(entries.data, members.data) : undefined;

  const columns: ColumnDef<EntryRow>[] = [
    ...baseColumns,
    {
      id: 'actions',
      header: '',
      cell: ({ row }) =>
        isDraft && (
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
        isLoading={entries.isPending || members.isPending}
        error={entries.error ?? members.error}
        searchPlaceholder="Search entries..."
        emptyMessage="No entries yet."
        onRowClick={entry => setSheet({ entry })}
        toolbar={
          isDraft && (
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
          dimensionVersionId={dimensionVersionId}
          entry={sheet.entry}
          readOnly={!isDraft}
          onClose={() => setSheet(null)}
        />
      )}
      {pasting && (
        <EntriesPasteDialog
          versionId={versionId}
          entries={entries.data ?? []}
          members={members.data ?? []}
          onClose={() => setPasting(false)}
        />
      )}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={open => !open && setDeleting(null)}
        title={`Delete the entry ${deleting?.sourceKeyValue ?? ''}?`}
        description="The source value no longer maps to a member in this draft."
        confirmLabel="Delete entry"
        destructive
        onConfirm={() => deleting && deleteEntry.mutate(deleting.mappingEntryId)}
      />
    </div>
  );
}

/** The entries with their members' keys and labels; a member outside the pinned version shows its ID. */
function toRows(entries: MappingEntry[], members: LookupItem[]): EntryRow[] {
  const byId = new Map(members.map(member => [member.id, member]));
  return entries.map(entry => {
    const member = byId.get(entry.memberId);
    if (!member) {
      return { ...entry, memberKey: String(entry.memberId), memberLabel: 'Not a member of the pinned version' };
    }
    return { ...entry, memberKey: member.key, memberLabel: member.label };
  });
}
