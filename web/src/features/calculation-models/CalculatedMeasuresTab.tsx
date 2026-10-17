import type { ColumnDef } from '@tanstack/react-table';
import { CalculatedMeasureSheet } from './CalculatedMeasureSheet';
import { ModelContentTab } from './ModelContentTab';
import type { CalculatedMeasure } from './types';

const columns: ColumnDef<CalculatedMeasure>[] = [
  { accessorKey: 'measureKey', header: 'Key' },
  { accessorKey: 'displayName', header: 'Name' },
  { accessorKey: 'expressionText', header: 'Formula', cell: ({ row }) => <code className="text-xs">{row.original.expressionText}</code> },
  {
    id: 'output',
    header: 'Output',
    accessorFn: measure =>
      measure.outputPrecision === null || measure.outputPrecision === undefined
        ? measure.outputDataType
        : `${measure.outputDataType}(${measure.outputPrecision}, ${measure.outputScale ?? 0})`,
  },
  { accessorKey: 'displayFormat', header: 'Format' },
];

/** The version's calculated measures: formulas over variables and other calculated measures. */
export function CalculatedMeasuresTab({ versionId, isDraft }: { versionId: number; isDraft: boolean }) {
  return (
    <ModelContentTab
      kind="calculated-measures"
      versionId={versionId}
      isDraft={isDraft}
      columns={columns}
      noun="calculated measure"
      identify={measure => ({ id: measure.calculatedMeasureId, key: measure.measureKey })}
      deleteDescription="A calculated measure that a report column returns, or that a formula line evaluates, is kept."
      renderSheet={(measure, onClose) => (
        <CalculatedMeasureSheet versionId={versionId} measure={measure} readOnly={!isDraft} onClose={onClose} />
      )}
    />
  );
}
