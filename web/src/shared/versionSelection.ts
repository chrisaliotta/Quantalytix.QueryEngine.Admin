import type { LookupItem } from './types';

/**
 * The version a versioned page shows: the one the `version` search parameter names when it is a version of the page's
 * dataset, dimension, mapping set or bridge, otherwise the latest. Lookups list versions by version number, so the latest
 * is the last.
 */
export function selectedVersion(versions: LookupItem[], requested: string | null): LookupItem | null {
  const named = versions.find(version => String(version.id) === requested);
  if (named) {
    return named;
  }
  return versions.at(-1) ?? null;
}
