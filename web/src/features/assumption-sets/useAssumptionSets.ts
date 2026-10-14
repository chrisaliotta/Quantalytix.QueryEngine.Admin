import { useMutation, useQuery } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { queryClient } from '@/api/queryClient';
import { lookupQueryKey } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import type { AssumptionPin, AssumptionSetCreateInput, AssumptionSetUpdateInput } from './types';

/** Query-key prefix of a tenant's assumption sets, their pins included. */
export function assumptionSetsQueryKey(tenantId: string) {
  return ['assumption-sets', tenantId] as const;
}

/** Refreshes the assumption set pages and, after a create, rename or delete, the assumption-sets lookup. */
function invalidateAssumptionSets(tenantId: string) {
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: assumptionSetsQueryKey(tenantId) }),
    queryClient.invalidateQueries({ queryKey: lookupQueryKey(tenantId, 'assumption-sets') }),
  ]);
}

/** The tenant's assumption sets, ordered by key. */
export function useAssumptionSets() {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: assumptionSetsQueryKey(tenantId),
    queryFn: async () =>
      unwrap(await apiClient.GET('/api/tenants/{tenantId}/assumption-sets', { params: { path: { tenantId } } })),
  });
}

/** One assumption set. */
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

/** The pins of one assumption set. */
export function useAssumptionPins(assumptionSetId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: [...assumptionSetsQueryKey(tenantId), assumptionSetId, 'dimensions'],
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/assumption-sets/{assumptionSetId}/dimensions', {
          params: { path: { tenantId, assumptionSetId } },
        })
      ),
  });
}

/** Creates a draft assumption set. */
export function useCreateAssumptionSet() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: AssumptionSetCreateInput) =>
      unwrap(await apiClient.POST('/api/tenants/{tenantId}/assumption-sets', { params: { path: { tenantId } }, body })),
    onSuccess: () => invalidateAssumptionSets(tenantId),
  });
}

/** Changes a draft assumption set's display name, description and effective time. */
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
    onSuccess: () => invalidateAssumptionSets(tenantId),
  });
}

/** Deletes a draft assumption set and its pins. */
export function useDeleteAssumptionSet() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (assumptionSetId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/assumption-sets/{assumptionSetId}', {
          params: { path: { tenantId, assumptionSetId } },
        })
      ),
    onSuccess: () => invalidateAssumptionSets(tenantId),
  });
}

/** Replaces the pins of a draft assumption set. */
export function useSaveAssumptionPins(assumptionSetId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (dimensions: Pick<AssumptionPin, 'dimensionId' | 'dimensionVersionId'>[]) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/assumption-sets/{assumptionSetId}/dimensions', {
          params: { path: { tenantId, assumptionSetId } },
          body: { dimensions },
        })
      ),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: assumptionSetsQueryKey(tenantId) }),
  });
}

/** Pins, for each dimension, its published version effective at the set's effective time; resolves to all pins. */
export function useCaptureAssumptionPins(assumptionSetId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (dimensionIds: number[]) =>
      unwrap(
        await apiClient.POST('/api/tenants/{tenantId}/assumption-sets/{assumptionSetId}/capture', {
          params: { path: { tenantId, assumptionSetId } },
          body: { dimensionIds },
        })
      ),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: assumptionSetsQueryKey(tenantId) }),
  });
}
