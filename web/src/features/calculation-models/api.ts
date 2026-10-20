import { useMutation, useQuery, type QueryKey } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { queryClient } from '@/api/queryClient';
import { lookupQueryKey } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import type { CalculationModelInput, ModelVersionInput, NewCalculationModelInput, NewModelVersionInput } from './types';

// Query keys start with [resource, tenantId] so invalidateTenantQueries reaches them.
function modelsKey(tenantId: string) {
  return ['calculation-models', tenantId] as const;
}

function versionKey(tenantId: string, versionId: number) {
  return ['model-versions', tenantId, versionId] as const;
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

/** One calculation model, with its current-version pointer. */
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

/** One model version; `null` keeps the query idle until a version is selected. */
export function useModelVersion(versionId: number | null) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: versionKey(tenantId, versionId ?? 0),
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/model-versions/{versionId}', {
          params: { path: { tenantId, versionId: versionId as number } },
        })
      ),
    enabled: versionId !== null,
  });
}

/** Creates a calculation model without versions. */
export function useCreateCalculationModel() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: NewCalculationModelInput) =>
      unwrap(await apiClient.POST('/api/tenants/{tenantId}/calculation-models', { params: { path: { tenantId } }, body })),
    onSuccess: () => invalidate(modelsKey(tenantId), lookupQueryKey(tenantId, 'calculation-models')),
  });
}

/** Edits a calculation model's name and availability. */
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

/** Deletes a model whose versions are all drafts and that no report version reads; a 409 names what blocks it. */
export function useDeleteCalculationModel() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (modelId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/calculation-models/{modelId}', {
          params: { path: { tenantId, modelId } },
        })
      ),
    onSuccess: () =>
      invalidate(
        modelsKey(tenantId),
        lookupQueryKey(tenantId, 'calculation-models'),
        lookupQueryKey(tenantId, 'model-versions')
      ),
  });
}

/** Creates a draft model version, empty or cloned with all its metrics. */
export function useCreateModelVersion(modelId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: NewModelVersionInput) =>
      unwrap(
        await apiClient.POST('/api/tenants/{tenantId}/calculation-models/{modelId}/versions', {
          params: { path: { tenantId, modelId } },
          body,
        })
      ),
    onSuccess: () => invalidate(lookupQueryKey(tenantId, 'model-versions')),
  });
}

/** Edits a draft or published version's currency. */
export function useUpdateModelVersion(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: ModelVersionInput) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/model-versions/{versionId}', {
          params: { path: { tenantId, versionId } },
          body,
        })
      ),
    onSuccess: () => invalidate(versionKey(tenantId, versionId), lookupQueryKey(tenantId, 'model-versions')),
  });
}

/** Deletes a draft version with its content. */
export function useDeleteModelVersion() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (versionId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/model-versions/{versionId}', {
          params: { path: { tenantId, versionId } },
        })
      ),
    onSuccess: () => invalidate(modelsKey(tenantId), lookupQueryKey(tenantId, 'model-versions')),
  });
}
