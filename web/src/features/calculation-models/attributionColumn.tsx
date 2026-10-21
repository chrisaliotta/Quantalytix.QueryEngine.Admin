import type { ColumnDef } from '@tanstack/react-table';
import { Button } from '@/components/ui/button';
import { attributionSummary } from './attributionForm';
import type { MetricAttribution } from './types';

/**
 * The column of the variables and calculated measures tables that summarizes each metric's attribution and opens its sheet for the
 * row, without opening the row's own sheet.
 */
export function attributionColumn<TRow>(
  attributions: MetricAttribution[] | undefined,
  metricKeyOf: (row: TRow) => string,
  isVariable: boolean,
  onOpen: (row: TRow) => void
): ColumnDef<TRow> {
  return {
    id: 'attribution',
    header: 'Attribution',
    cell: ({ row }) => {
      const metricKey = metricKeyOf(row.original);
      return (
        <Button
          variant="ghost"
          size="xs"
          onClick={event => {
            event.stopPropagation();
            onOpen(row.original);
          }}
        >
          {attributionSummary(attributions?.find(attribution => attribution.metricKey === metricKey), isVariable)}
        </Button>
      );
    },
  };
}
