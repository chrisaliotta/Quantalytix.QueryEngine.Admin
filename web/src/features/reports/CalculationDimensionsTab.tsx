import type { ColumnDef } from '@tanstack/react-table';
import { EnabledBadge } from '@/shared/EnabledBadge';
import { useLookup } from '@/shared/useLookup';
import { VersionContentTab } from '@/shared/VersionContentTab';
import { CalculationDimensionSheet } from './CalculationDimensionSheet';
import type { CalculationDimension } from './types';

/** The grouping of a canonical dimension, such as `level region`; empty for a scalar one. */
function grouping(dimension: CalculationDimension) {
  const reference = dimension.groupingLevelKey ?? dimension.groupingAncestorMemberKey ?? dimension.groupingDepth;
  return [dimension.groupingMode, reference].filter(part => part != null).join(' ');
}

/** The version's calculation dimensions: what the report can break out or filter by. */
export function CalculationDimensionsTab({ versionId, isEditable }: { versionId: number; isEditable: boolean }) {
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
    { id: 'grouping', header: 'Grouping', accessorFn: grouping },
    { accessorKey: 'isGroupable', header: 'Groupable', cell: ({ row }) => (row.original.isGroupable ? 'Yes' : '') },
    { accessorKey: 'isFilterable', header: 'Filterable', cell: ({ row }) => (row.original.isFilterable ? 'Yes' : '') },
    { accessorKey: 'isEnabled', header: 'Enabled', cell: ({ row }) => <EnabledBadge enabled={row.original.isEnabled} /> },
  ];

  return (
    <VersionContentTab
      kind="calculation-dimensions"
      versionId={versionId}
      isEditable={isEditable}
      columns={columns}
      noun="dimension"
      identify={dimension => ({ id: dimension.calculationDimensionId, key: dimension.dimensionKey })}
      deleteDescription="Its routes are deleted too. Report defaults name dimensions by key, so validating the version reports a default that no longer resolves."
      renderSheet={(dimension, onClose) => (
        <CalculationDimensionSheet versionId={versionId} dimension={dimension} readOnly={!isEditable} onClose={onClose} />
      )}
    />
  );
}
