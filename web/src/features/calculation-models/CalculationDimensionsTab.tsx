import type { ColumnDef } from '@tanstack/react-table';
import { EnabledBadge } from '@/shared/EnabledBadge';
import { useLookup } from '@/shared/useLookup';
import { CalculationDimensionSheet } from './CalculationDimensionSheet';
import { ModelContentTab } from './ModelContentTab';
import type { CalculationDimension } from './types';

/** The version's calculation dimensions: what reports can break out or filter by. */
export function CalculationDimensionsTab({ versionId, isDraft }: { versionId: number; isDraft: boolean }) {
  const dimensions = useLookup('dimensions');
  const dimensionKeys = new Map(dimensions.data?.map(item => [item.id, item.key]));

  const columns: ColumnDef<CalculationDimension>[] = [
    { accessorKey: 'dimensionKey', header: 'Key' },
    { accessorKey: 'displayName', header: 'Name' },
    { accessorKey: 'valueKind', header: 'Value kind' },
    {
      id: 'values',
      header: 'Values',
      accessorFn: dimension =>
        dimension.dimensionId == null ? (dimension.valueDataType ?? '') : (dimensionKeys.get(dimension.dimensionId) ?? dimension.dimensionId),
    },
    { accessorKey: 'isGroupable', header: 'Groupable', cell: ({ row }) => (row.original.isGroupable ? 'Yes' : '') },
    { accessorKey: 'isFilterable', header: 'Filterable', cell: ({ row }) => (row.original.isFilterable ? 'Yes' : '') },
    { accessorKey: 'isEnabled', header: 'Enabled', cell: ({ row }) => <EnabledBadge enabled={row.original.isEnabled} /> },
  ];

  return (
    <ModelContentTab
      kind="calculation-dimensions"
      versionId={versionId}
      isDraft={isDraft}
      columns={columns}
      noun="dimension"
      identify={dimension => ({ id: dimension.calculationDimensionId, key: dimension.dimensionKey })}
      deleteDescription="Its routes are deleted too. Report defaults name dimensions by key, so validating a report version reports a default that no longer resolves."
      renderSheet={(dimension, onClose) => (
        <CalculationDimensionSheet versionId={versionId} dimension={dimension} readOnly={!isDraft} onClose={onClose} />
      )}
    />
  );
}
