import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { useTenantId } from '@/shared/useTenantId';
import { effectiveVersion } from './effectiveVersion';
import type { DimensionVersion } from './types';

/** The bindings of a dataset version, sorted by display name, each with its time extension. */
export function useBindings(versionId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: ['bindings', tenantId, versionId],
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/dataset-versions/{versionId}/bindings', {
          params: { path: { tenantId, versionId } },
        })
      ),
  });
}

/** The versions of a dimension with their validity windows; `null` keeps the query idle until a dimension is chosen. */
export function useDimensionVersions(dimensionId: number | null) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: ['dimensions', tenantId, dimensionId, 'versions'],
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/dimensions/{dimensionId}/versions', {
          params: { path: { tenantId, dimensionId: dimensionId as number } },
        })
      ),
    enabled: dimensionId !== null,
  });
}

/** The dimension version a binding's levels and members come from, as `useGroupingVersion` resolves it. */
export interface GroupingVersion {
  /** The pinned version, else the version in effect now; null while loading or when no version is in effect. */
  versionId: number | null;
  /** The version in effect now, when the binding pins none and one is. */
  effective?: DimensionVersion;
  isPending: boolean;
}

/**
 * Resolves the dimension version a binding's grouping level, grouping ancestor and unknown member belong to, as the API
 * checks them: the pinned version, or the published version of the dimension in effect now when the binding pins none.
 * "Now" is the time the caller mounted, so the answer stays put while a sheet is open.
 */
export function useGroupingVersion(dimensionId: number | null, pinnedVersionId: number | null): GroupingVersion {
  const [mountedAt] = useState(() => new Date());
  const versions = useDimensionVersions(pinnedVersionId === null ? dimensionId : null);
  if (pinnedVersionId !== null) {
    return { versionId: pinnedVersionId, isPending: false };
  }
  const effective = effectiveVersion(versions.data ?? [], mountedAt);
  return { versionId: effective?.dimensionVersionId ?? null, effective, isPending: versions.isLoading };
}
