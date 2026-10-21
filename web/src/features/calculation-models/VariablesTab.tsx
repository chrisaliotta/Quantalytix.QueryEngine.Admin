import type { ColumnDef } from '@tanstack/react-table';
import { useState } from 'react';
import { OwnedContentTab } from '@/shared/OwnedContentTab';
import { useAttributions } from './api';
import { attributionColumn } from './attributionColumn';
import { AttributionSheet } from './AttributionSheet';
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

/** The model's variables: the logical inputs each report's profiles fill from the report's own sources, each with its attribution. */
export function VariablesTab({ modelId }: { modelId: number }) {
  const attributions = useAttributions(modelId);
  const [attributing, setAttributing] = useState<Variable | null>(null);
  const withAttribution = [...columns, attributionColumn<Variable>(attributions.data, variable => variable.variableKey, true, setAttributing)];

  return (
    <>
      <OwnedContentTab
        kind="variables"
        ownerId={modelId}
        columns={withAttribution}
        noun="variable"
        identify={variable => ({ id: variable.variableId, key: variable.variableKey })}
        deleteDescription="A variable that a formula or attribution of the model, or an enabled report through a column, scope, source or pick, reads is kept."
        renderSheet={(variable, onClose) => <VariableSheet modelId={modelId} variable={variable} onClose={onClose} />}
      />
      {attributing && attributions.data && (
        <AttributionSheet
          modelId={modelId}
          metricKey={attributing.variableKey}
          formula={null}
          attribution={attributions.data.find(attribution => attribution.metricKey === attributing.variableKey)}
          onClose={() => setAttributing(null)}
        />
      )}
    </>
  );
}
