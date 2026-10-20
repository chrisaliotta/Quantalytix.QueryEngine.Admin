import type { ColumnDef } from '@tanstack/react-table';
import { VersionContentTab } from '@/shared/VersionContentTab';
import type { Variable } from './types';
import { VariableSheet } from './VariableSheet';

const columns: ColumnDef<Variable>[] = [
  { accessorKey: 'variableKey', header: 'Key' },
  { accessorKey: 'displayName', header: 'Name' },
  { accessorKey: 'variableRole', header: 'Role' },
  { accessorKey: 'displayFormat', header: 'Format' },
  { accessorKey: 'unitKey', header: 'Unit' },
  { accessorKey: 'missingBehavior', header: 'Missing' },
];

/** The version's variables: the logical inputs each report's profiles fill from the report's own sources. */
export function VariablesTab({ versionId, isEditable }: { versionId: number; isEditable: boolean }) {
  return (
    <VersionContentTab
      kind="variables"
      versionId={versionId}
      isEditable={isEditable}
      columns={columns}
      noun="variable"
      identify={variable => ({ id: variable.variableId, key: variable.variableKey })}
      deleteDescription="A variable of the published version that a published report reads, through a column, source, target or formula, is kept."
      renderSheet={(variable, onClose) => (
        <VariableSheet versionId={versionId} variable={variable} readOnly={!isEditable} onClose={onClose} />
      )}
    />
  );
}
