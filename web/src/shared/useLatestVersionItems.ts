import type { LookupItem, LookupKind } from './types';
import { useLookup } from './useLookup';

/** The lookups that list the versions of a living identity, with their statuses. */
type LivingVersionLookup = 'dimension-versions' | 'model-versions';

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
 * The published version of a dimension or calculation model: the version the active assumption set pins, which requests
 * read by default. `undefined` until the versions load, and `null` when the identity has no published version.
 * @example usePublishedVersion('model-versions', modelId)
 */
export function usePublishedVersion(versionLookup: LivingVersionLookup, ownerId: number | null): LookupItem | null | undefined {
  const versions = useLookup(versionLookup, ownerId);
  if (!versions.data) {
    return undefined;
  }
  return versions.data.find(version => version.status === 'published') ?? null;
}

/**
 * The items of the published version of a dimension or calculation model, such as a dimension's members or a model's
 * calculated measures, or `null` until the identity is chosen and both lookups load; an empty list while it has no
 * published version. Bindings, mapping entries, bridge mappings and reports name these items by key, and requests resolve
 * the keys in the version the active assumption set pins, which is the published one.
 * @example usePublishedVersionItems('dimension-versions', dimensionId, 'members')
 */
export function usePublishedVersionItems(
  versionLookup: LivingVersionLookup,
  ownerId: number | null,
  itemLookup: LookupKind
): LookupItem[] | null {
  const published = usePublishedVersion(versionLookup, ownerId);
  const items = useLookup(itemLookup, published?.id ?? null);
  if (published === null) {
    return [];
  }
  return items.data ?? null;
}
