import type { ColumnDef } from '@tanstack/react-table';
import { Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { EntityTable } from '@/shared/EntityTable';
import { useDeleteLevel, useLevels } from './api';
import { LevelSheet } from './LevelSheet';
import type { DimensionLevel } from './types';

const baseColumns: ColumnDef<DimensionLevel>[] = [
  { accessorKey: 'ordinal', header: 'Ordinal' },
  { accessorKey: 'levelKey', header: 'Key' },
  { accessorKey: 'displayName', header: 'Name' },
  { accessorKey: 'periodicity', header: 'Periodicity', cell: ({ row }) => row.original.periodicity ?? '—' },
  { accessorKey: 'isLeafLevel', header: 'Leaf', cell: ({ row }) => (row.original.isLeafLevel ? 'Yes' : '') },
];

/** What the level sheet was opened for: an existing level, or a new one. */
interface LevelSheetRequest {
  level: DimensionLevel | null;
}

/** The version's levels in a table; a draft or the published version adds, edits and deletes them in a sheet. */
export function LevelsTab({ versionId, isEditable }: { versionId: number; isEditable: boolean }) {
  const levels = useLevels(versionId);
  const deleteLevel = useDeleteLevel(versionId);
  const [sheet, setSheet] = useState<LevelSheetRequest | null>(null);
  const [deleting, setDeleting] = useState<DimensionLevel | null>(null);
  const rows = levels.data ?? [];
  const nextOrdinal = Math.max(0, ...rows.map(level => level.ordinal)) + 1;

  const columns: ColumnDef<DimensionLevel>[] = [
    ...baseColumns,
    {
      id: 'actions',
      header: '',
      cell: ({ row }) =>
        isEditable && (
          <Button
            variant="ghost"
            size="icon-xs"
            aria-label={`Delete ${row.original.levelKey}`}
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
    <div data-slot="levels-tab" className="space-y-3">
      <EntityTable
        columns={columns}
        data={levels.data}
        isLoading={levels.isPending}
        error={levels.error}
        emptyMessage="No levels yet."
        onRowClick={level => setSheet({ level })}
        toolbar={
          isEditable && (
            <Button size="sm" className="ml-auto" onClick={() => setSheet({ level: null })}>
              <Plus />
              New level
            </Button>
          )
        }
      />
      <ApiErrorAlert error={deleteLevel.error} />
      {sheet && (
        <LevelSheet
          versionId={versionId}
          level={sheet.level}
          nextOrdinal={nextOrdinal}
          readOnly={!isEditable}
          onClose={() => setSheet(null)}
        />
      )}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={open => !open && setDeleting(null)}
        title={`Delete level ${deleting?.levelKey ?? ''}?`}
        description="Levels that members use cannot be deleted."
        confirmLabel="Delete level"
        destructive
        onConfirm={() => deleting && deleteLevel.mutate(deleting.levelId)}
      />
    </div>
  );
}
