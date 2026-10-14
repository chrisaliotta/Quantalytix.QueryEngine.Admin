import { SELECT_ALL_DEFAULT_VALUE } from '@registry/components/MultiSelect/constants';
import type { LookupItem } from '@/shared/types';

/**
 * The dimension IDs a capture dialog's MultiSelect value names. The registry MultiSelect reports
 * "Select all" as a single sentinel value, which stands for every dimension.
 */
export function selectedDimensionIds(selected: string[], dimensions: LookupItem[]): number[] {
  if (selected.includes(SELECT_ALL_DEFAULT_VALUE)) {
    return dimensions.map(item => item.id);
  }
  return selected.map(Number);
}
