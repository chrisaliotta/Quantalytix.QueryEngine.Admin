import type { ColumnDef } from '@tanstack/react-table';
import { useState } from 'react';
import { OwnedContentTab } from '@/shared/OwnedContentTab';
import { useAttributions } from './api';
import { attributionColumn } from './attributionColumn';
import { AttributionSheet } from './AttributionSheet';
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

/** The model's calculated measures: formulas over variables and other calculated measures, each with its attribution. */
export function CalculatedMeasuresTab({ modelId }: { modelId: number }) {
  const attributions = useAttributions(modelId);
  const [attributing, setAttributing] = useState<CalculatedMeasure | null>(null);
  const withAttribution = [
    ...columns,
    attributionColumn<CalculatedMeasure>(attributions.data, measure => measure.measureKey, false, setAttributing),
  ];

  return (
    <>
      <OwnedContentTab
        kind="calculated-measures"
        ownerId={modelId}
        columns={withAttribution}
        noun="calculated measure"
        identify={measure => ({ id: measure.calculatedMeasureId, key: measure.measureKey })}
        deleteDescription="A calculated measure that another formula of the model, an attribution, or an enabled report's column or formula row reads is kept."
        renderSheet={(measure, onClose) => <CalculatedMeasureSheet modelId={modelId} measure={measure} onClose={onClose} />}
      />
      {attributing && attributions.data && (
        <AttributionSheet
          modelId={modelId}
          metricKey={attributing.measureKey}
          formula={attributing.expressionText}
          attribution={attributions.data.find(attribution => attribution.metricKey === attributing.measureKey)}
          onClose={() => setAttributing(null)}
        />
      )}
    </>
  );
}
