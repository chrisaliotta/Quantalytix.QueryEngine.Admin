import type { ColumnDef } from '@tanstack/react-table';
import { EnabledBadge } from '@/shared/EnabledBadge';
import { useLookup } from '@/shared/useLookup';
import { VersionContentTab } from '@/shared/VersionContentTab';
import { CalculationDimensionSheet } from './CalculationDimensionSheet';
import type { CalculationDimension } from './types';

/** The default grouping of a role's members, such as `level region`. */
function grouping(dimension: CalculationDimension) {
  const reference = dimension.groupingLevelKey ?? dimension.groupingAncestorMemberKey ?? dimension.groupingDepth;
  return [dimension.groupingMode, reference].filter(part => part != null).join(' ');
}

/** The version's calculation dimensions: the roles the report can break out or filter by, each with its default grouping. */
export function CalculationDimensionsTab({ versionId, isEditable }: { versionId: number; isEditable: boolean }) {
  const roles = useLookup('dimension-roles');
  const roleLabels = new Map(roles.data?.map(item => [item.id, item.label]));

  const columns: ColumnDef<CalculationDimension>[] = [
    { accessorKey: 'dimensionKey', header: 'Key' },
    { accessorKey: 'displayName', header: 'Name' },
    { id: 'role', header: 'Role', accessorFn: dimension => roleLabels.get(dimension.roleId) ?? dimension.roleId },
    { id: 'grouping', header: 'Default grouping', accessorFn: grouping },
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
      deleteDescription="Report defaults name dimensions by key, so validating the version reports a default that no longer resolves."
      renderSheet={(dimension, onClose) => (
        <CalculationDimensionSheet versionId={versionId} dimension={dimension} readOnly={!isEditable} onClose={onClose} />
      )}
    />
  );
}
