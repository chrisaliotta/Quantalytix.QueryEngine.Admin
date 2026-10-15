import type { ColumnDef } from '@tanstack/react-table';
import { Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from './ApiErrorAlert';
import { EntitySelect } from './EntitySelect';
import { EntityTable } from './EntityTable';
import type { LookupItem } from './types';
import { useLookup } from './useLookup';

/** The version of one dataset or dimension that a release or an assumption set pins. */
export interface PinnedVersion {
  identityId: number;
  versionId: number;
}

/** A pin being edited; an identity that was just added has no version yet. */
interface PinRow {
  identityId: number;
  versionId: number | null;
}

export interface PinnedVersionsTableProps {
  /** Lookup kind of the pinned identities. */
  identityLookup: 'datasets' | 'dimensions';
  /** Lookup kind of their versions, which are listed per identity. */
  versionLookup: 'dataset-versions' | 'dimension-versions';
  /** What an identity is called, e.g. `Dataset`, for the column header and the add picker. */
  identityLabel: string;
  /** The saved pins; remount the table (`key`) when they are refetched so editing starts from them. */
  pins: PinnedVersion[];
  /** Shows the pins without editing controls, e.g. once the release is no longer a draft. */
  readOnly: boolean;
  /** Saves the complete list of pins. */
  onSave: (pins: PinnedVersion[]) => void;
  isSaving: boolean;
  saveError: Error | null;
}

/**
 * Editable list of pinned versions, one row per dataset or dimension, each with a picker of its
 * published versions. Rows are added and removed locally; "Save" sends the whole list.
 */
export function PinnedVersionsTable({
  identityLookup,
  versionLookup,
  identityLabel,
  pins,
  readOnly,
  onSave,
  isSaving,
  saveError,
}: PinnedVersionsTableProps) {
  const identities = useLookup(identityLookup);
  const [rows, setRows] = useState<PinRow[]>(pins);
  const identitiesById = new Map(identities.data?.map(item => [item.id, item]));
  const isChanged = !hasSamePins(rows, pins);
  const noun = identityLabel.toLowerCase();

  function setVersion(identityId: number, versionId: number | null) {
    setRows(current => current.map(row => (row.identityId === identityId ? { ...row, versionId } : row)));
  }

  function addIdentity(identityId: number | null) {
    if (identityId !== null) {
      setRows(current => [...current, { identityId, versionId: null }]);
    }
  }

  function removeIdentity(identityId: number) {
    setRows(current => current.filter(row => row.identityId !== identityId));
  }

  const columns: ColumnDef<PinRow>[] = [
    {
      id: 'identity',
      header: identityLabel,
      accessorFn: row => identitiesById.get(row.identityId)?.key ?? String(row.identityId),
      cell: ({ row }) => <IdentityName item={identitiesById.get(row.original.identityId)} id={row.original.identityId} />,
    },
    {
      id: 'version',
      header: 'Pinned version',
      cell: ({ row }) => (
        <EntitySelect
          lookupKind={versionLookup}
          parentId={row.original.identityId}
          value={row.original.versionId}
          onChange={versionId => setVersion(row.original.identityId, versionId)}
          filter={item => item.status === 'published' || item.id === row.original.versionId}
          disabled={readOnly}
          placeholder="Select a published version"
        />
      ),
    },
  ];
  if (!readOnly) {
    columns.push({
      id: 'remove',
      header: '',
      cell: ({ row }) => (
        <Button
          variant="ghost"
          size="icon"
          aria-label={`Remove ${noun}`}
          onClick={() => removeIdentity(row.original.identityId)}
        >
          <Trash2 />
        </Button>
      ),
    });
  }

  return (
    <div data-slot="pinned-versions-table" className="space-y-3">
      <EntityTable
        columns={columns}
        data={rows}
        isLoading={false}
        emptyMessage={`No ${noun} is pinned yet.`}
        toolbar={
          !readOnly && (
            <>
              <div className="w-72">
                <EntitySelect
                  lookupKind={identityLookup}
                  value={null}
                  onChange={addIdentity}
                  filter={item => !rows.some(row => row.identityId === item.id)}
                  placeholder={`Add ${noun}...`}
                />
              </div>
              <div className="ml-auto flex gap-2">
                <Button variant="outline" disabled={!isChanged || isSaving} onClick={() => setRows(pins)}>
                  Discard changes
                </Button>
                <Button
                  disabled={!isChanged || !rows.every(isPinned) || isSaving}
                  onClick={() => onSave(rows.filter(isPinned))}
                >
                  Save
                </Button>
              </div>
            </>
          )
        }
      />
      <ApiErrorAlert error={saveError} />
    </div>
  );
}

function IdentityName({ item, id }: { item: LookupItem | undefined; id: number }) {
  if (!item) {
    return <span className="text-muted-foreground">{id}</span>;
  }
  return (
    <span className="flex flex-col">
      <span className="font-medium">{item.key}</span>
      <span className="text-xs text-muted-foreground">{item.label}</span>
    </span>
  );
}

function isPinned(row: PinRow): row is PinnedVersion {
  return row.versionId !== null;
}

/** Whether the edited rows pin exactly the saved versions, in any order. */
function hasSamePins(rows: PinRow[], pins: PinnedVersion[]) {
  return (
    rows.length === pins.length &&
    pins.every(pin => rows.some(row => row.identityId === pin.identityId && row.versionId === pin.versionId))
  );
}
