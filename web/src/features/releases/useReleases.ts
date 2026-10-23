import { useMutation, useQuery } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { invalidateTenantQueries, queryClient } from '@/api/queryClient';
import { lookupQueryKey } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import type { ReleaseCreateInput, ReleaseDataset } from './types';

/** Query-key prefix of a tenant's releases, their manifests included. */
export function releasesQueryKey(tenantId: string) {
  return ['releases', tenantId] as const;
}

/** Refreshes the release list, the release pages and the releases lookup after a create or delete. */
function invalidateReleases(tenantId: string) {
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: releasesQueryKey(tenantId) }),
    queryClient.invalidateQueries({ queryKey: lookupQueryKey(tenantId, 'releases') }),
  ]);
}

/** The tenant's releases, newest first, each with whether it is active and how many datasets and reports it pins. */
export function useReleases() {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: releasesQueryKey(tenantId),
    queryFn: async () =>
      unwrap(await apiClient.GET('/api/tenants/{tenantId}/releases', { params: { path: { tenantId } } })),
  });
}

/** One release. */
export function useRelease(semanticReleaseId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: [...releasesQueryKey(tenantId), semanticReleaseId],
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/releases/{semanticReleaseId}', {
          params: { path: { tenantId, semanticReleaseId } },
        })
      ),
  });
}

/** The manifest rows of one release. */
export function useReleaseDatasets(semanticReleaseId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: [...releasesQueryKey(tenantId), semanticReleaseId, 'datasets'],
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/releases/{semanticReleaseId}/datasets', {
          params: { path: { tenantId, semanticReleaseId } },
        })
      ),
  });
}

/** Creates a draft release, optionally copying another release's manifest. */
export function useCreateRelease() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: ReleaseCreateInput) =>
      unwrap(await apiClient.POST('/api/tenants/{tenantId}/releases', { params: { path: { tenantId } }, body })),
    onSuccess: () => invalidateReleases(tenantId),
  });
}

/** Deletes a draft release with its manifest. */
export function useDeleteRelease() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (semanticReleaseId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/releases/{semanticReleaseId}', {
          params: { path: { tenantId, semanticReleaseId } },
        })
      ),
    onSuccess: () => invalidateReleases(tenantId),
  });
}

/** Replaces the manifest of a draft release. */
export function useSaveReleaseDatasets(semanticReleaseId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (datasets: Pick<ReleaseDataset, 'datasetId' | 'datasetVersionId'>[]) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/releases/{semanticReleaseId}/datasets', {
          params: { path: { tenantId, semanticReleaseId } },
          body: { datasets },
        })
      ),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: releasesQueryKey(tenantId) }),
  });
}

/**
 * Activates a published release. Every query of the tenant is refreshed, because the active release
 * and the semantic revision change what the overview and the release pages show.
 */
export function useActivateRelease() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (semanticReleaseId: number) =>
      unwrap(
        await apiClient.POST('/api/tenants/{tenantId}/releases/{semanticReleaseId}/activate', {
          params: { path: { tenantId, semanticReleaseId } },
        })
      ),
    onSuccess: () => invalidateTenantQueries(tenantId),
  });
}
