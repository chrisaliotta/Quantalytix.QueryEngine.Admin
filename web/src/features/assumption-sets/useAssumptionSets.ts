import { useMutation, useQuery } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { queryClient } from '@/api/queryClient';
import { useTenantId } from '@/shared/useTenantId';
import type { AssumptionCaptureInput, AssumptionSetUpdateInput } from './types';

/**
 * Query-key prefix of a tenant's assumption sets, their pins included. Publishing a living version refreshes every query
 * of the tenant, which takes in the repointed active set.
 */
export function assumptionSetsQueryKey(tenantId: string) {
  return ['assumption-sets', tenantId] as const;
}

/** The tenant's assumption sets: the active one, then historical sets, most recently captured first. */
export function useAssumptionSets() {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: assumptionSetsQueryKey(tenantId),
    queryFn: async () =>
      unwrap(await apiClient.GET('/api/tenants/{tenantId}/assumption-sets', { params: { path: { tenantId } } })),
  });
}

/** One assumption set with its pins, each labelled with its identity key, version number and status. */
export function useAssumptionSet(assumptionSetId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: [...assumptionSetsQueryKey(tenantId), assumptionSetId],
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/assumption-sets/{assumptionSetId}', {
          params: { path: { tenantId, assumptionSetId } },
        })
      ),
  });
}

/** Captures the active set's pins as a new historical set under its own key. */
export function useCaptureAssumptionSet() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: AssumptionCaptureInput) =>
      unwrap(await apiClient.POST('/api/tenants/{tenantId}/assumption-sets', { params: { path: { tenantId } }, body })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: assumptionSetsQueryKey(tenantId) }),
  });
}

/** Changes an assumption set's display name and description; its key and pins stay. */
export function useUpdateAssumptionSet(assumptionSetId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: AssumptionSetUpdateInput) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/assumption-sets/{assumptionSetId}', {
          params: { path: { tenantId, assumptionSetId } },
          body,
        })
      ),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: assumptionSetsQueryKey(tenantId) }),
  });
}

/** Deletes a historical assumption set with its pins; the API refuses the active set with 409. */
export function useDeleteAssumptionSet() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (assumptionSetId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/assumption-sets/{assumptionSetId}', {
          params: { path: { tenantId, assumptionSetId } },
        })
      ),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: assumptionSetsQueryKey(tenantId) }),
  });
}
