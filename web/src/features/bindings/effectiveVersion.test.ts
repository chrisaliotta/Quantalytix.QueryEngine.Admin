/**
 * Why these tests exist: the binding sheet lists the levels and members of an unpinned binding from the dimension's
 * version in effect now, the same version the API's grouping_references_in_version rule checks them against. Picking a
 * different version, such as a draft or an earlier published one, would offer references the rule then rejects.
 */
import { describe, expect, it } from 'vitest';
import { effectiveVersion } from './effectiveVersion';
import type { DimensionVersion } from './types';

const NOW = new Date('2026-10-14T17:30:00Z');

describe('effectiveVersion', () => {
  it('picks the published version whose window contains now', () => {
    const versions = [
      version(1, 'published', '2026-01-01T00:00:00Z', '2026-07-01T00:00:00Z'),
      version(2, 'published', '2026-07-01T00:00:00Z', null),
    ];

    expect(effectiveVersion(versions, NOW)?.versionNumber).toBe(2);
  });

  it('ignores drafts and retired versions even when their window contains now', () => {
    const versions = [version(1, 'draft', '2026-01-01T00:00:00Z', null), version(2, 'retired', '2026-01-01T00:00:00Z', null)];

    expect(effectiveVersion(versions, NOW)).toBeUndefined();
  });

  // The window is half-open: it includes its start and excludes its end.
  it('includes the window start and excludes its end', () => {
    const ending = [version(1, 'published', '2026-01-01T00:00:00Z', '2026-10-14T17:30:00Z')];
    const starting = [version(2, 'published', '2026-10-14T17:30:00Z', null)];

    expect(effectiveVersion(ending, NOW)).toBeUndefined();
    expect(effectiveVersion(starting, NOW)?.versionNumber).toBe(2);
  });

  it('finds nothing before the first version starts or for a version without a start', () => {
    const versions = [version(1, 'published', '2027-01-01T00:00:00Z', null), version(2, 'published', null, null)];

    expect(effectiveVersion(versions, NOW)).toBeUndefined();
  });
});

function version(versionNumber: number, status: string, validFrom: string | null, validTo: string | null): DimensionVersion {
  return { dimensionVersionId: 700 + versionNumber, dimensionId: 7000, versionNumber, status, validFrom, validTo };
}
