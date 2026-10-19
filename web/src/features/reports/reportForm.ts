import type { ReportInput } from './types';

/** Values of the report identity fields; the payload is JSON text, empty for none. */
export type ReportValues = Omit<ReportInput, 'payloadJson'> & { payloadJson: string };
