import type { LookupItem, LookupKind } from './types';
import { useLookup } from './useLookup';

/**
 * The items of an identity's latest version, such as the fields of a dataset's newest version, or `null` until the
 * identity is chosen and both lookups load. Report sources and routes name dataset keys that resolve in whatever dataset
 * version a release pins, so the newest version's keys are the ones to offer while authoring.
 * @example useLatestVersionItems('dataset-versions', datasetId, 'measures')
 */
export function useLatestVersionItems(versionLookup: 'dataset-versions', ownerId: number | null, itemLookup: LookupKind): LookupItem[] | null {
  const versions = useLookup(versionLookup, ownerId);
  const items = useLookup(itemLookup, versions.data?.at(-1)?.id ?? null);
  return items.data ?? null;
}

/**
 * The items of a dimension's published version, such as its members or levels, or `null` until the dimension is chosen and
 * both lookups load; an empty list while it has no published version. Bindings, mapping entries, bridge mappings and
 * calculation dimensions name these items by key, and requests resolve the keys in the version the active assumption set
 * pins, which is the published one.
 * @example usePublishedDimensionItems(dimensionId, 'members')
 */
export function usePublishedDimensionItems(dimensionId: number | null, itemLookup: LookupKind): LookupItem[] | null {
  const versions = useLookup('dimension-versions', dimensionId);
  const published = versions.data && (versions.data.find(version => version.status === 'published') ?? null);
  const items = useLookup(itemLookup, published?.id ?? null);
  if (published === null) {
    return [];
  }
  return items.data ?? null;
}
