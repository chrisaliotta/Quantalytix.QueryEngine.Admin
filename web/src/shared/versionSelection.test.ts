/**
 * Why these tests exist: the dataset, dimension, mapping set and bridge pages keep the selected version in `?version=`,
 * so links and reloads reopen it. A missing or stale parameter, such as a deleted draft's ID, must fall back to the
 * latest version.
 */
import { describe, expect, it } from 'vitest';
import type { LookupItem } from './types';
import { selectedVersion } from './versionSelection';

const versions: LookupItem[] = [
  { id: 11, key: 'v1', label: 'v1 · loan_daily', status: 'published' },
  { id: 12, key: 'v2', label: 'v2 · loan_daily', status: 'draft' },
];

describe('selectedVersion', () => {
  it('selects the version the parameter names', () => {
    expect(selectedVersion(versions, '11')?.id).toBe(11);
  });

  it('falls back to the latest version when the parameter is missing or names no version', () => {
    expect(selectedVersion(versions, null)?.id).toBe(12);
    expect(selectedVersion(versions, '99')?.id).toBe(12);
  });

  it('selects nothing when there are no versions', () => {
    expect(selectedVersion([], '11')).toBeNull();
  });
});
