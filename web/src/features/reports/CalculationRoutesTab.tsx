import type { ColumnDef } from '@tanstack/react-table';
import { useLookup } from '@/shared/useLookup';
import { VersionContentTab } from '@/shared/VersionContentTab';
import { CalculationRouteSheet } from './CalculationRouteSheet';
import type { CalculationRoute } from './types';

/** The version's routes: how each dataset's rows classify into each calculation dimension. */
export function CalculationRoutesTab({ versionId, isEditable }: { versionId: number; isEditable: boolean }) {
  const dimensions = useLookup('calculation-dimensions', versionId);
  const datasets = useLookup('datasets');
  const sources = useLookup('variable-sources', versionId);
  const dimensionKeys = new Map(dimensions.data?.map(item => [item.id, item.key]));
  const datasetKeys = new Map(datasets.data?.map(item => [item.id, item.key]));
  const sourceKeys = new Map(sources.data?.map(item => [item.id, item.key]));

  const columns: ColumnDef<CalculationRoute>[] = [
    {
      id: 'dimension',
      header: 'Dimension',
      accessorFn: route => dimensionKeys.get(route.calculationDimensionId) ?? route.calculationDimensionId,
    },
    { id: 'dataset', header: 'Dataset', accessorFn: route => datasetKeys.get(route.datasetId) ?? route.datasetId },
    {
      id: 'source',
      header: 'For source',
      accessorFn: route => (route.variableSourceId == null ? 'Default' : (sourceKeys.get(route.variableSourceId) ?? route.variableSourceId)),
    },
    { accessorKey: 'routeKind', header: 'Kind' },
    { accessorKey: 'fieldKey', header: 'Field' },
  ];

  return (
    <VersionContentTab
      kind="routes"
      versionId={versionId}
      isEditable={isEditable}
      columns={columns}
      noun="route"
      identify={route => ({ id: route.calculationBindingId, key: route.fieldKey })}
      deleteDescription="Nothing else references a route."
      renderSheet={(route, onClose) => (
        <CalculationRouteSheet versionId={versionId} route={route} readOnly={!isEditable} onClose={onClose} />
      )}
    />
  );
}
