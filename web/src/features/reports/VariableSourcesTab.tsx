import type { ColumnDef } from '@tanstack/react-table';
import { EnabledBadge } from '@/shared/EnabledBadge';
import { useLookup } from '@/shared/useLookup';
import { VersionContentTab } from '@/shared/VersionContentTab';
import type { Report, ReportVersion, VariableSource } from './types';
import { VariableSourceSheet } from './VariableSourceSheet';

export interface VariableSourcesTabProps {
  report: Report;
  version: ReportVersion;
  isEditable: boolean;
}

/** The version's variable sources: where each variable of the model reads its values, classified into the report's dimension. */
export function VariableSourcesTab({ report, version, isEditable }: VariableSourcesTabProps) {
  const datasets = useLookup('datasets');
  const roles = useLookup('dimension-roles');
  const datasetKeys = new Map(datasets.data?.map(item => [item.id, item.key]));
  const roleKeys = new Map(roles.data?.map(item => [item.id, item.key]));

  /** What the source classifies by: its field, or the role a bridge starts from. */
  function classifiedBy(source: VariableSource) {
    if (source.classificationRoleId == null) {
      return source.classificationFieldKey;
    }
    return roleKeys.get(source.classificationRoleId) ?? source.classificationRoleId;
  }

  const columns: ColumnDef<VariableSource>[] = [
    { accessorKey: 'sourceKey', header: 'Key' },
    { accessorKey: 'variableKey', header: 'Variable' },
    { id: 'dataset', header: 'Dataset', accessorFn: source => datasetKeys.get(source.datasetId) ?? source.datasetId },
    { id: 'classification', header: 'Classification', accessorFn: source => `${source.classificationKind} ${classifiedBy(source)}` },
    { accessorKey: 'timeFieldKey', header: 'Time field' },
    { accessorKey: 'measureKey', header: 'Measure' },
    { accessorKey: 'isEnabled', header: 'Enabled', cell: ({ row }) => <EnabledBadge enabled={row.original.isEnabled} /> },
  ];

  return (
    <VersionContentTab
      kind="variable-sources"
      versionId={version.reportVersionId}
      isEditable={isEditable}
      columns={columns}
      noun="source"
      identify={source => ({ id: source.variableSourceId, key: source.sourceKey })}
      deleteDescription="The profile picks that name the source are deleted too."
      renderSheet={(source, onClose) => (
        <VariableSourceSheet report={report} version={version} source={source} readOnly={!isEditable} onClose={onClose} />
      )}
    />
  );
}
