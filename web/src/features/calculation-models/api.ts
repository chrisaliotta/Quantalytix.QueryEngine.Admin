import { useMutation, useQuery, type QueryKey } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { queryClient } from '@/api/queryClient';
import { lookupQueryKey } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import type { CalculationModelInput, NewCalculationModelInput } from './types';

// Query keys start with [resource, tenantId] so invalidateTenantQueries reaches them.
function modelsKey(tenantId: string) {
  return ['calculation-models', tenantId] as const;
}

/** Marks the given queries stale. */
function invalidate(...queryKeys: QueryKey[]) {
  return Promise.all(queryKeys.map(queryKey => queryClient.invalidateQueries({ queryKey })));
}

/** Every calculation model of the current tenant, by key. */
export function useCalculationModels() {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: modelsKey(tenantId),
    queryFn: async () =>
      unwrap(await apiClient.GET('/api/tenants/{tenantId}/calculation-models', { params: { path: { tenantId } } })),
  });
}

/** One calculation model, with its settings. */
export function useCalculationModel(modelId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: [...modelsKey(tenantId), modelId],
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/calculation-models/{modelId}', {
          params: { path: { tenantId, modelId } },
        })
      ),
  });
}

/** Creates a calculation model without variables or calculated measures. */
export function useCreateCalculationModel() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: NewCalculationModelInput) =>
      unwrap(await apiClient.POST('/api/tenants/{tenantId}/calculation-models', { params: { path: { tenantId } }, body })),
    onSuccess: () => invalidate(modelsKey(tenantId), lookupQueryKey(tenantId, 'calculation-models')),
  });
}

/** Edits a calculation model's name, currency and availability. */
export function useUpdateCalculationModel(modelId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: CalculationModelInput) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/calculation-models/{modelId}', {
          params: { path: { tenantId, modelId } },
          body,
        })
      ),
    onSuccess: () => invalidate(modelsKey(tenantId), lookupQueryKey(tenantId, 'calculation-models')),
  });
}

/** Deletes a model that no report reads, with its variables and calculated measures; a 409 names the reports that read it. */
export function useDeleteCalculationModel() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (modelId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/calculation-models/{modelId}', {
          params: { path: { tenantId, modelId } },
        })
      ),
    onSuccess: () => invalidate(modelsKey(tenantId), lookupQueryKey(tenantId, 'calculation-models')),
  });
}
