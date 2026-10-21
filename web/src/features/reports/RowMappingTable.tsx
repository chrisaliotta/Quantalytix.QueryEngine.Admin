import type { ColumnDef } from '@tanstack/react-table';
import { FlipVertical2, ListChecks, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { EntityTable } from '@/shared/EntityTable';
import { useLookup } from '@/shared/useLookup';
import { useReplaceRowMappings } from './designApi';
import { RowMappingSheet } from './RowMappingSheet';
import type { DesignRow, DesignRowMapping, ReportDesign } from './types';

export interface RowMappingTableProps {
  versionId: number;
  design: ReportDesign;
  /** The row selected in the tree; null before one is. */
  row: DesignRow | null;
  isEditable: boolean;
}

/** How a factor shows: + and − for the usual signs, the factor itself otherwise. */
function signText(factor: number) {
  if (factor === 1) {
    return '+';
  }
  return factor === -1 ? '−' : String(factor);
}

/**
 * The source items mapped to the selected row: each item of a source's bound dimension whose values the row adds up, with its
 * sign. A data row of a draft or the published version picks its items in a sheet, flips an item's sign and removes items;
 * the codes mapping sets map to the row show read-only, because the Mapping sets pages edit them.
 */
export function RowMappingTable({ versionId, design, row, isEditable }: RowMappingTableProps) {
  const replace = useReplaceRowMappings(versionId);
  const dimensions = useLookup('dimensions');
  const mappingSets = useLookup('mapping-sets');
  const [picking, setPicking] = useState(false);
  const mappingSetKeys = new Map(mappingSets.data?.map(item => [item.id, item.key]));

  if (row === null) {
    return <p className="py-6 text-center text-sm text-muted-foreground">Select a row to see the source items mapped to it.</p>;
  }
  if (row.kind !== 'data') {
    return (
      <div data-slot="row-mapping-table" className="space-y-3">
        <p className="py-6 text-center text-sm text-muted-foreground">
          {row.kind === 'group' ? 'A group totals the rows under it' : 'Sections and formula rows show no source values'}; only
          data rows take source items.
        </p>
        <MappedCodes row={row} mappingSetKeys={mappingSetKeys} />
      </div>
    );
  }

  const dataRow = row;
  const sources = new Map(design.sources.map(source => [source.bridgeId, source]));
  const dimensionKeys = new Map(dimensions.data?.map(item => [item.id, item.key]));

  function save(mappings: DesignRowMapping[]) {
    replace.mutate({ rowKey: dataRow.key, mappings });
  }

  const columns: ColumnDef<DesignRowMapping>[] = [
    { id: 'source', header: 'Source', accessorFn: mapping => sources.get(mapping.bridgeId)!.label },
    {
      id: 'dimension',
      header: 'Bound dimension',
      accessorFn: mapping => dimensionKeys.get(sources.get(mapping.bridgeId)!.sourceDimensionId) ?? '',
    },
    { accessorKey: 'itemKey', header: 'Item key' },
    { id: 'sign', header: 'Sign', accessorFn: mapping => signText(mapping.factor) },
    {
      id: 'actions',
      header: '',
      cell: ({ row: { original } }) =>
        isEditable && (
          <div className="flex justify-end">
            <Button
              variant="ghost"
              size="icon-xs"
              aria-label={`Flip the sign of ${original.itemKey}`}
              onClick={() => save(dataRow.mappings.map(mapping => (mapping === original ? { ...mapping, factor: -mapping.factor } : mapping)))}
            >
              <FlipVertical2 />
            </Button>
            <Button
              variant="ghost"
              size="icon-xs"
              aria-label={`Remove ${original.itemKey}`}
              onClick={() => save(dataRow.mappings.filter(mapping => mapping !== original))}
            >
              <Trash2 />
            </Button>
          </div>
        ),
    },
  ];

  return (
    <div data-slot="row-mapping-table" className="space-y-3">
      <EntityTable
        columns={columns}
        data={row.mappings}
        isLoading={false}
        searchPlaceholder="Search items..."
        emptyMessage="No items yet; the row shows nothing until a source maps items to it."
        toolbar={
          isEditable && (
            <Button size="sm" className="ml-auto" disabled={design.sources.length === 0} onClick={() => setPicking(true)}>
              <ListChecks />
              Pick items
            </Button>
          )
        }
      />
      {isEditable && design.sources.length === 0 && (
        <p className="text-sm text-muted-foreground">Add a source in the Sources panel first; its items are what rows take.</p>
      )}
      <ApiErrorAlert error={replace.error} />
      <MappedCodes row={row} mappingSetKeys={mappingSetKeys} />
      {picking && <RowMappingSheet versionId={versionId} design={design} row={row} onClose={() => setPicking(false)} />}
    </div>
  );
}

/** The codes mapping sets into the report dimension map to the row, by mapping set; nothing when there are none. */
function MappedCodes({ row, mappingSetKeys }: { row: DesignRow; mappingSetKeys: ReadonlyMap<number, string> }) {
  if (row.mappedCodes.length === 0) {
    return null;
  }
  return (
    <div data-slot="row-mapped-codes" className="space-y-2 text-sm">
      <div className="text-muted-foreground">Mapped by mapping sets, which the Mapping sets pages edit:</div>
      <div className="flex flex-wrap gap-1">
        {row.mappedCodes.map(code => (
          <Badge key={`${code.mappingSetId}:${code.sourceKey}`} variant="outline">
            {mappingSetKeys.get(code.mappingSetId)}: {code.sourceKey}
          </Badge>
        ))}
      </div>
    </div>
  );
}
