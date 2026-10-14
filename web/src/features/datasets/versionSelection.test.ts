/**
 * Why these tests exist: the dataset page keeps the selected version in `?version=`, so links and reloads reopen it.
 * A missing or stale parameter, such as a deleted draft's ID, must fall back to the latest version.
 */
import { describe, expect, it } from 'vitest';
import type { LookupItem } from '@/shared/types';
import { selectVersionId } from './versionSelection';

const versions: LookupItem[] = [
  { id: 11, key: 'v1', label: 'v1 · loan_daily', status: 'published' },
  { id: 12, key: 'v2', label: 'v2 · loan_daily', status: 'draft' },
];

describe('selectVersionId', () => {
  it('selects the version the parameter names', () => {
    expect(selectVersionId(versions, '11')).toBe(11);
  });

  it('falls back to the latest version when the parameter is missing or names no version', () => {
    expect(selectVersionId(versions, null)).toBe(12);
    expect(selectVersionId(versions, '99')).toBe(12);
  });

  it('selects nothing for a dataset without versions', () => {
    expect(selectVersionId([], '11')).toBeNull();
  });
});
