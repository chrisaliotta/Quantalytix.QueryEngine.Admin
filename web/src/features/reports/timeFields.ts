import type { LookupItem } from '@/shared/types';

/** Data types a time field can have. */
const TIME_TYPES = ['date', 'datetime', 'timestamp'];

/** Whether a dataset field, as the `fields` lookup lists it with its data type, can place a source's rows in report periods. */
export function isTimeField(field: LookupItem) {
  return TIME_TYPES.includes(field.classifier ?? '');
}
