import type { ReportInput } from './types';

/** Values of the report identity fields; the payload is JSON text. */
export type ReportValues = Omit<ReportInput, 'payloadJson'> & { payloadJson: string | null };

/** A JSON text field's value as the API takes optional JSON: null when it is blank. */
export function optionalJson(value: string | null | undefined) {
  return value?.trim() ? value : null;
}
