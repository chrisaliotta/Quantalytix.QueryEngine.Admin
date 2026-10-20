import type { ColumnDef } from '@tanstack/react-table';
import { ClipboardPaste, Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { EntityTable } from '@/shared/EntityTable';
import type { LookupItem } from '@/shared/types';
import { usePublishedVersionItems } from '@/shared/useLatestVersionItems';
import { useBridgeMappings, useReplaceBridgeMappings } from './api';
import { BridgeMappingSheet } from './BridgeMappingSheet';
import { BridgeMappingsPasteDialog } from './BridgeMappingsPasteDialog';
import { toMappingRows, withoutMapping, type BridgeMappingRow } from './mappingList';
import type { Bridge, BridgeMapping, BridgeVersion } from './types';
import { decimalText, sourceWeightSums, type WeightSum } from './weights';

/** What the mapping sheet was opened for: an existing mapping, or a new one. */
interface MappingSheetRequest {
  mapping: BridgeMappingRow | null;
}

export interface BridgeMappingsProps {
  bridge: Bridge;
  version: BridgeVersion;
  isEditable: boolean;
}

/**
 * A bridge version's mappings in a table, each member labelled from the published version of its dimension, with each
 * source member's weight sum in allocation mode `partition`. On a draft or the published version they are added, edited
 * and deleted one by one, or replaced by a bulk paste; edits to the published version reach the next request.
 */
export function BridgeMappings({ bridge, version, isEditable }: BridgeMappingsProps) {
  const mappings = useBridgeMappings(version.bridgeVersionId);
  const sourceMembers = usePublishedVersionItems('dimension-versions', bridge.sourceDimensionId, 'members');
  const targetMembers = usePublishedVersionItems('dimension-versions', bridge.targetDimensionId, 'members');
  const replace = useReplaceBridgeMappings(version.bridgeVersionId);
  const [sheet, setSheet] = useState<MappingSheetRequest | null>(null);
  const [pasting, setPasting] = useState(false);
  const [deleting, setDeleting] = useState<BridgeMappingRow | null>(null);
  const rows = loadedRows(mappings.data, sourceMembers, targetMembers);
  const isPartition = version.allocationMode === 'partition';
  const sums = sourceWeightSums(mappings.data ?? []);

  const columns: ColumnDef<BridgeMappingRow>[] = [
    {
      accessorKey: 'sourceMemberKey',
      header: 'Source member',
      cell: ({ row }) => <MemberCell memberKey={row.original.sourceMemberKey} label={row.original.sourceLabel} />,
    },
    {
      accessorKey: 'targetMemberKey',
      header: 'Target member',
      cell: ({ row }) => <MemberCell memberKey={row.original.targetMemberKey} label={row.original.targetLabel} />,
    },
    { accessorKey: 'factor', header: 'Factor', cell: ({ row }) => decimalText(row.original.factor) },
    {
      accessorKey: 'allocationWeight',
      header: 'Allocation weight',
      cell: ({ row }) => decimalText(row.original.allocationWeight),
    },
    ...(isPartition ? [weightSumColumn(sums)] : []),
    {
      id: 'actions',
      header: '',
      cell: ({ row }) =>
        isEditable && (
          <Button
            variant="ghost"
            size="icon-xs"
            aria-label={`Delete ${row.original.sourceMemberKey} → ${row.original.targetMemberKey}`}
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
    <div data-slot="bridge-mappings" className="space-y-3">
      {isPartition && rows && <PartitionSummary rows={rows} sums={sums} />}
      <EntityTable
        columns={columns}
        data={rows}
        isLoading={mappings.isPending || sourceMembers === null || targetMembers === null}
        error={mappings.error}
        searchPlaceholder="Search mappings..."
        emptyMessage="No mappings yet."
        onRowClick={mapping => setSheet({ mapping })}
        toolbar={
          isEditable && (
            <div className="ml-auto flex gap-2">
              <Button variant="outline" disabled={rows === undefined} onClick={() => setPasting(true)}>
                <ClipboardPaste />
                Bulk paste
              </Button>
              <Button onClick={() => setSheet({ mapping: null })}>
                <Plus />
                New mapping
              </Button>
            </div>
          )
        }
      />
      <ApiErrorAlert error={replace.error} />
      {sheet && (
        <BridgeMappingSheet
          version={version}
          mappings={mappings.data ?? []}
          mapping={sheet.mapping}
          sourceMembers={sourceMembers}
          targetMembers={targetMembers}
          readOnly={!isEditable}
          onClose={() => setSheet(null)}
        />
      )}
      {pasting && (
        <BridgeMappingsPasteDialog versionId={version.bridgeVersionId} mappings={rows ?? []} onClose={() => setPasting(false)} />
      )}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={open => !open && setDeleting(null)}
        title={`Delete ${deleting?.sourceMemberKey ?? ''} → ${deleting?.targetMemberKey ?? ''}?`}
        description="The source member no longer maps to this target in this version."
        confirmLabel="Delete mapping"
        destructive
        onConfirm={() => deleting && replace.mutate(withoutMapping(mappings.data ?? [], deleting))}
      />
    </div>
  );
}

/** The table rows, once the mappings and both member lists have loaded. */
function loadedRows(mappings: BridgeMapping[] | undefined, sourceMembers: LookupItem[] | null, targetMembers: LookupItem[] | null) {
  if (!mappings || !sourceMembers || !targetMembers) {
    return undefined;
  }
  return toMappingRows(mappings, sourceMembers, targetMembers);
}

/** The column that shows each row's source member weight sum, marked when it is not 1. */
function weightSumColumn(sums: Map<string, WeightSum>): ColumnDef<BridgeMappingRow> {
  return {
    id: 'sourceWeightSum',
    header: 'Source weight sum',
    cell: ({ row }) => <WeightSumText sum={sums.get(row.original.sourceMemberKey)} />,
  };
}

function WeightSumText({ sum }: { sum: WeightSum | undefined }) {
  return <span className={cn(sum && !sum.isOne && 'font-medium text-destructive')}>{sum?.text}</span>;
}

/** In allocation mode `partition`: whether every source member's weights sum to 1, or which sources do not. */
function PartitionSummary({ rows, sums }: { rows: BridgeMappingRow[]; sums: Map<string, WeightSum> }) {
  if (rows.length === 0) {
    return null;
  }
  const unbalanced = [...sums].filter(([, sum]) => !sum.isOne).map(([sourceMemberKey, sum]) => `${sourceMemberKey} (${sum.text})`);
  if (unbalanced.length === 0) {
    return <p className="text-sm text-muted-foreground">Every source member's weights sum to 1.</p>;
  }
  return (
    <p className="text-sm text-destructive">
      Partition needs each source member's weights to sum to 1. These do not: {unbalanced.join(', ')}.
    </p>
  );
}

/** A member's key with its label beside it. */
function MemberCell({ memberKey, label }: { memberKey: string; label: string }) {
  return (
    <span className="flex items-baseline gap-2">
      <span className="font-mono">{memberKey}</span>
      <span className="text-muted-foreground">{label}</span>
    </span>
  );
}
