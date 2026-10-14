import type { DimensionVersion } from './types';

/**
 * The published version of a dimension whose validity window `[validFrom, validTo)` contains `now`: the version the
 * query engine resolves for a binding that pins none. Undefined when no published version is in effect.
 */
export function effectiveVersion(versions: DimensionVersion[], now: Date): DimensionVersion | undefined {
  return versions.find(version => version.status === 'published' && containsTime(version, now.getTime()));
}

/** Whether a version's half-open validity window contains a time; a missing end is open. */
function containsTime(version: DimensionVersion, time: number) {
  if (!version.validFrom || Date.parse(version.validFrom) > time) {
    return false;
  }
  return !version.validTo || time < Date.parse(version.validTo);
}
