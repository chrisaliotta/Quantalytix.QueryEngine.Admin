import type { ColumnDef } from '@tanstack/react-table';
import { ClipboardPaste, Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { EntityTable } from '@/shared/EntityTable';
import type { LookupItem } from '@/shared/types';
import { useLookup } from '@/shared/useLookup';
import { useBridgeMappings, useReplaceBridgeMappings } from './api';
import { BridgeMappingSheet } from './BridgeMappingSheet';
import { BridgeMappingsPasteDialog } from './BridgeMappingsPasteDialog';
import { toMappingRows, withoutMapping, type BridgeMappingRow } from './mappingList';
import type { BridgeMapping, BridgeVersion } from './types';
import { decimalText, sourceWeightSums, type WeightSum } from './weights';

/** What the mapping sheet was opened for: an existing mapping, or a new one. */
interface MappingSheetRequest {
  mapping: BridgeMappingRow | null;
}

export interface BridgeMappingsProps {
  version: BridgeVersion;
  isDraft: boolean;
}

/**
 * A bridge version's mappings in a table, with each source member's weight sum in allocation mode `partition`. On a
 * draft they are added, edited and deleted one by one, or replaced by a bulk paste.
 */
export function BridgeMappings({ version, isDraft }: BridgeMappingsProps) {
  const mappings = useBridgeMappings(version.bridgeVersionId);
  const sourceMembers = useLookup('members', version.sourceDimensionVersionId);
  const targetMembers = useLookup('members', version.targetDimensionVersionId);
  const replace = useReplaceBridgeMappings(version.bridgeVersionId);
  const [sheet, setSheet] = useState<MappingSheetRequest | null>(null);
  const [pasting, setPasting] = useState(false);
  const [deleting, setDeleting] = useState<BridgeMappingRow | null>(null);
  const rows = loadedRows(mappings.data, sourceMembers.data, targetMembers.data);
  const isPartition = version.allocationMode === 'partition';
  const sums = sourceWeightSums(mappings.data ?? []);

  const columns: ColumnDef<BridgeMappingRow>[] = [
    {
      accessorKey: 'sourceKey',
      header: 'Source member',
      cell: ({ row }) => <MemberCell memberKey={row.original.sourceKey} label={row.original.sourceLabel} />,
    },
    {
      accessorKey: 'targetKey',
      header: 'Target member',
      cell: ({ row }) => <MemberCell memberKey={row.original.targetKey} label={row.original.targetLabel} />,
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
        isDraft && (
          <Button
            variant="ghost"
            size="icon-xs"
            aria-label={`Delete ${row.original.sourceKey} → ${row.original.targetKey}`}
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
        isLoading={mappings.isPending || sourceMembers.isPending || targetMembers.isPending}
        error={mappings.error ?? sourceMembers.error ?? targetMembers.error}
        searchPlaceholder="Search mappings..."
        emptyMessage="No mappings yet."
        onRowClick={mapping => setSheet({ mapping })}
        toolbar={
          isDraft && (
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
          readOnly={!isDraft}
          onClose={() => setSheet(null)}
        />
      )}
      {pasting && (
        <BridgeMappingsPasteDialog
          versionId={version.bridgeVersionId}
          mappings={rows ?? []}
          sourceMembers={sourceMembers.data ?? []}
          targetMembers={targetMembers.data ?? []}
          onClose={() => setPasting(false)}
        />
      )}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={open => !open && setDeleting(null)}
        title={`Delete ${deleting?.sourceKey ?? ''} → ${deleting?.targetKey ?? ''}?`}
        description="The source member no longer maps to this target in this draft."
        confirmLabel="Delete mapping"
        destructive
        onConfirm={() => deleting && replace.mutate(withoutMapping(mappings.data ?? [], deleting))}
      />
    </div>
  );
}

/** The table rows, once the mappings and both member lists have loaded. */
function loadedRows(mappings?: BridgeMapping[], sourceMembers?: LookupItem[], targetMembers?: LookupItem[]) {
  if (!mappings || !sourceMembers || !targetMembers) {
    return undefined;
  }
  return toMappingRows(mappings, sourceMembers, targetMembers);
}

/** The column that shows each row's source member weight sum, marked when it is not 1. */
function weightSumColumn(sums: Map<number, WeightSum>): ColumnDef<BridgeMappingRow> {
  return {
    id: 'sourceWeightSum',
    header: 'Source weight sum',
    cell: ({ row }) => <WeightSumText sum={sums.get(row.original.sourceMemberId)} />,
  };
}

function WeightSumText({ sum }: { sum: WeightSum | undefined }) {
  return <span className={cn(sum && !sum.isOne && 'font-medium text-destructive')}>{sum?.text}</span>;
}

/** In allocation mode `partition`: whether every source member's weights sum to 1, or which sources do not. */
function PartitionSummary({ rows, sums }: { rows: BridgeMappingRow[]; sums: Map<number, WeightSum> }) {
  if (rows.length === 0) {
    return null;
  }
  const sourceKeys = new Map(rows.map(row => [row.sourceMemberId, row.sourceKey]));
  const unbalanced = [...sums]
    .filter(([, sum]) => !sum.isOne)
    .map(([sourceMemberId, sum]) => `${sourceKeys.get(sourceMemberId)} (${sum.text})`);
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
