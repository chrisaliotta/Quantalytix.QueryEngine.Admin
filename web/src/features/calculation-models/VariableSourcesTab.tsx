import type { ColumnDef } from '@tanstack/react-table';
import { EnabledBadge } from '@/shared/EnabledBadge';
import { useLookup } from '@/shared/useLookup';
import { ModelContentTab } from './ModelContentTab';
import type { VariableSource } from './types';
import { VariableSourceSheet } from './VariableSourceSheet';

/** The version's variable sources: where each variable can read its values. */
export function VariableSourcesTab({ versionId, isEditable }: { versionId: number; isEditable: boolean }) {
  const variables = useLookup('variables', versionId);
  const datasets = useLookup('datasets');
  const variableKeys = new Map(variables.data?.map(item => [item.id, item.key]));
  const datasetKeys = new Map(datasets.data?.map(item => [item.id, item.key]));

  const columns: ColumnDef<VariableSource>[] = [
    { accessorKey: 'sourceKey', header: 'Key' },
    { id: 'variable', header: 'Variable', accessorFn: source => variableKeys.get(source.variableId) ?? source.variableId },
    { id: 'dataset', header: 'Dataset', accessorFn: source => datasetKeys.get(source.datasetId) ?? source.datasetId },
    { accessorKey: 'reportFieldKey', header: 'Report field' },
    { accessorKey: 'timeFieldKey', header: 'Time field' },
    { accessorKey: 'measureKey', header: 'Measure' },
    { accessorKey: 'isEnabled', header: 'Enabled', cell: ({ row }) => <EnabledBadge enabled={row.original.isEnabled} /> },
  ];

  return (
    <ModelContentTab
      kind="variable-sources"
      versionId={versionId}
      isEditable={isEditable}
      columns={columns}
      noun="source"
      identify={source => ({ id: source.variableSourceId, key: source.sourceKey })}
      deleteDescription="The profile picks and override routes that name the source are deleted too."
      renderSheet={(source, onClose) => (
        <VariableSourceSheet versionId={versionId} source={source} readOnly={!isEditable} onClose={onClose} />
      )}
    />
  );
}
