import type { LookupItem } from '@/shared/types';

/**
 * The version a dataset page shows: the one the `version` search parameter names when it is a version of the
 * dataset, otherwise the latest. The lookup lists versions by version number, so the latest is the last.
 */
export function selectVersionId(versions: LookupItem[], requested: string | null): number | null {
  const named = versions.find(version => String(version.id) === requested);
  if (named) {
    return named.id;
  }
  return versions.at(-1)?.id ?? null;
}
