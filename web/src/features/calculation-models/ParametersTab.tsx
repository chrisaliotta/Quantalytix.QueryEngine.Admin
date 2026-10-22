import type { ColumnDef } from '@tanstack/react-table';
import { ContentTab } from '@/shared/ContentTab';
import { parameterDefault } from '@/shared/modelParameters';
import { useDeleteModelParameter, useModelParameters } from './api';
import { ParameterSheet } from './ParameterSheet';
import type { ModelParameter } from './types';

const columns: ColumnDef<ModelParameter>[] = [
  { accessorKey: 'parameterKey', header: 'Key', cell: ({ row }) => <code className="text-xs">@{row.original.parameterKey}</code> },
  { accessorKey: 'displayName', header: 'Name' },
  { accessorKey: 'valueType', header: 'Type' },
  {
    id: 'allowedValues',
    header: 'Allowed values',
    accessorFn: parameter =>
      parameter.valueType === 'text' ? (parameter.allowedValues?.join(', ') ?? 'Any text') : 'Any number',
  },
  { id: 'default', header: 'Default', accessorFn: parameterDefault },
  { accessorKey: 'description', header: 'Description' },
];

/** The model's parameters: the values its formulas read as `@key`, which each report sets on its rows. */
export function ParametersTab({ modelId }: { modelId: number }) {
  const parameters = useModelParameters(modelId);
  const deleteParameter = useDeleteModelParameter(modelId);

  return (
    <ContentTab
      rows={parameters}
      columns={columns}
      noun="parameter"
      keyOf={parameter => parameter.parameterKey}
      deleteDescription="A parameter a formula of the model reads, or a report's row sets, is kept."
      deleteRow={{ mutate: parameter => deleteParameter.mutate(parameter.parameterKey), error: deleteParameter.error }}
      renderSheet={(parameter, onClose) => <ParameterSheet modelId={modelId} parameter={parameter} onClose={onClose} />}
    />
  );
}
