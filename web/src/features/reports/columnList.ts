import type { ReportColumn, ReportColumnInput } from './types';

/**
 * A stored column as the replace body takes it: its metric named by key, its ID kept so that the formula lines placing
 * values in it stay valid.
 */
export function toColumnInput(column: ReportColumn): ReportColumnInput {
  return {
    reportColumnId: column.reportColumnId,
    columnKey: column.columnKey,
    displayName: column.displayName,
    valueKind: column.valueKind,
    metricKey: column.metricKey,
    displayFormat: column.displayFormat ?? null,
    isVisible: column.isVisible,
    payloadJson: column.payloadJson ?? null,
  };
}
