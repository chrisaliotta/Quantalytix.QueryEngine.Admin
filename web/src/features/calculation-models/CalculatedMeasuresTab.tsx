import type { ColumnDef } from '@tanstack/react-table';
import { OwnedContentTab } from '@/shared/OwnedContentTab';
import { CalculatedMeasureSheet } from './CalculatedMeasureSheet';
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

/** The model's calculated measures: formulas over variables and other calculated measures. */
export function CalculatedMeasuresTab({ modelId }: { modelId: number }) {
  return (
    <OwnedContentTab
      kind="calculated-measures"
      ownerId={modelId}
      columns={columns}
      noun="calculated measure"
      identify={measure => ({ id: measure.calculatedMeasureId, key: measure.measureKey })}
      deleteDescription="A calculated measure that another formula of the model, or an enabled report's column or formula row, reads is kept."
      renderSheet={(measure, onClose) => <CalculatedMeasureSheet modelId={modelId} measure={measure} onClose={onClose} />}
    />
  );
}
