import type { LookupItem, LookupKind } from './types';
import { useLookup } from './useLookup';

/**
 * The items of an identity's latest version, such as the fields of a dataset's newest version, or `null` until the
 * identity is chosen and both lookups load. References by key resolve in whatever version a release pins, so the newest
 * version's keys are the ones to offer while authoring.
 * @example useLatestVersionItems('dataset-versions', datasetId, 'measures')
 */
export function useLatestVersionItems(
  versionLookup: 'dataset-versions' | 'dimension-versions',
  ownerId: number | null,
  itemLookup: LookupKind
): LookupItem[] | null {
  const versions = useLookup(versionLookup, ownerId);
  const items = useLookup(itemLookup, versions.data?.at(-1)?.id ?? null);
  return items.data ?? null;
}
