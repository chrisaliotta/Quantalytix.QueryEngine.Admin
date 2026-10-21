import type { ColumnDef } from '@tanstack/react-table';
import { OwnedContentTab } from '@/shared/OwnedContentTab';
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

/** The model's variables: the logical inputs each report's profiles fill from the report's own sources. */
export function VariablesTab({ modelId }: { modelId: number }) {
  return (
    <OwnedContentTab
      kind="variables"
      ownerId={modelId}
      columns={columns}
      noun="variable"
      identify={variable => ({ id: variable.variableId, key: variable.variableKey })}
      deleteDescription="A variable that a formula of the model, or an enabled report through a column, scope, source or pick, reads is kept."
      renderSheet={(variable, onClose) => <VariableSheet modelId={modelId} variable={variable} onClose={onClose} />}
    />
  );
}
