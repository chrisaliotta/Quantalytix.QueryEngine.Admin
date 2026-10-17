import type { ColumnDef } from '@tanstack/react-table';
import { ModelContentTab } from './ModelContentTab';
import type { Variable } from './types';
import { VariableSheet } from './VariableSheet';

const columns: ColumnDef<Variable>[] = [
  { accessorKey: 'variableKey', header: 'Key' },
  { accessorKey: 'displayName', header: 'Name' },
  { accessorKey: 'variableRole', header: 'Role' },
  { accessorKey: 'targetMemberKey', header: 'Target member', cell: ({ row }) => row.original.targetMemberKey ?? 'All mapped' },
  { accessorKey: 'selectionScope', header: 'Scope' },
  { accessorKey: 'missingBehavior', header: 'Missing' },
];

/** The version's variables: the logical inputs a profile fills from one of their sources. */
export function VariablesTab({ versionId, isDraft }: { versionId: number; isDraft: boolean }) {
  return (
    <ModelContentTab
      kind="variables"
      versionId={versionId}
      isDraft={isDraft}
      columns={columns}
      noun="variable"
      identify={variable => ({ id: variable.variableId, key: variable.variableKey })}
      deleteDescription="Its sources, their profile picks and their override routes are deleted too. A variable that a report column returns is kept."
      renderSheet={(variable, onClose) => (
        <VariableSheet versionId={versionId} variable={variable} readOnly={!isDraft} onClose={onClose} />
      )}
    />
  );
}
