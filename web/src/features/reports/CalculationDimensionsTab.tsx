import type { ColumnDef } from '@tanstack/react-table';
import { EnabledBadge } from '@/shared/EnabledBadge';
import { OwnedContentTab } from '@/shared/OwnedContentTab';
import { useLookup } from '@/shared/useLookup';
import { CalculationDimensionSheet } from './CalculationDimensionSheet';
import type { CalculationDimension } from './types';

/** The default grouping of a role's members, such as `level region`. */
function grouping(dimension: CalculationDimension) {
  const reference = dimension.groupingLevelKey ?? dimension.groupingAncestorMemberKey ?? dimension.groupingDepth;
  return [dimension.groupingMode, reference].filter(part => part != null).join(' ');
}

/** The report's calculation dimensions: the roles it can break out or filter by, each with its default grouping. */
export function CalculationDimensionsTab({ reportId }: { reportId: number }) {
  const roles = useLookup('dimension-roles');
  const roleLabels = new Map(roles.data?.map(item => [item.id, item.label]));

  const columns: ColumnDef<CalculationDimension>[] = [
    { accessorKey: 'dimensionKey', header: 'Key' },
    { accessorKey: 'displayName', header: 'Name' },
    { id: 'role', header: 'Role', accessorFn: dimension => roleLabels.get(dimension.dimensionRoleId) ?? dimension.dimensionRoleId },
    { id: 'grouping', header: 'Default grouping', accessorFn: grouping },
    { accessorKey: 'isGroupable', header: 'Groupable', cell: ({ row }) => (row.original.isGroupable ? 'Yes' : '') },
    { accessorKey: 'isFilterable', header: 'Filterable', cell: ({ row }) => (row.original.isFilterable ? 'Yes' : '') },
    { accessorKey: 'isEnabled', header: 'Enabled', cell: ({ row }) => <EnabledBadge enabled={row.original.isEnabled} /> },
  ];

  return (
    <OwnedContentTab
      kind="calculation-dimensions"
      ownerId={reportId}
      columns={columns}
      noun="dimension"
      identify={dimension => ({ id: dimension.calculationDimensionId, key: dimension.dimensionKey })}
      deleteDescription="Report defaults name dimensions by key, so an enabled report keeps a dimension its defaults name."
      renderSheet={(dimension, onClose) => (
        <CalculationDimensionSheet reportId={reportId} dimension={dimension} onClose={onClose} />
      )}
    />
  );
}
