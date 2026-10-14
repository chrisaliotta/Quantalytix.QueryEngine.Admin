/**
 * Why these tests exist: the capture dialog sends the selected dimension IDs to `POST .../capture`.
 * The registry MultiSelect reports "Select all" as a sentinel string instead of the options, so
 * without this mapping the dialog would send an invalid ID instead of every dimension.
 */
import { describe, expect, it } from 'vitest';
import { SELECT_ALL_DEFAULT_VALUE } from '@registry/components/MultiSelect/constants';
import type { LookupItem } from '@/shared/types';
import { selectedDimensionIds } from './captureSelection';

const DIMENSIONS: LookupItem[] = [
  { id: 3001, key: 'Branch', label: 'Branch' },
  { id: 3002, key: 'Product', label: 'Product' },
  { id: 3003, key: 'Calendar', label: 'Calendar' },
];

describe('selectedDimensionIds', () => {
  it('turns the selected option values back into dimension IDs', () => {
    expect(selectedDimensionIds(['3001', '3003'], DIMENSIONS)).toEqual([3001, 3003]);
  });

  it('requests every dimension when "Select all" is chosen', () => {
    expect(selectedDimensionIds([SELECT_ALL_DEFAULT_VALUE], DIMENSIONS)).toEqual([3001, 3002, 3003]);
  });
});
