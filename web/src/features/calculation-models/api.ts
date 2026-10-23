import { useMutation, useQuery, type QueryKey } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { queryClient } from '@/api/queryClient';
import { lookupQueryKey } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import type { CalculationModelInput, MetricAttributionInput, ModelParameterInput, NewCalculationModelInput } from './types';

// Query keys start with [resource, tenantId] so invalidateTenantQueries reaches them.
function modelsKey(tenantId: string) {
  return ['calculation-models', tenantId] as const;
}

function attributionsKey(tenantId: string, calculationModelId: number) {
  return [...modelsKey(tenantId), calculationModelId, 'attributions'] as const;
}

function parametersKey(tenantId: string, calculationModelId: number) {
  return [...modelsKey(tenantId), calculationModelId, 'parameters'] as const;
}

/** The reports feature's key of every report's editor view, which shows the parameters of the report's model. */
function reportEditorsKey(tenantId: string) {
  return ['report-editor', tenantId] as const;
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
export function useCalculationModel(calculationModelId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: [...modelsKey(tenantId), calculationModelId],
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/calculation-models/{calculationModelId}', {
          params: { path: { tenantId, calculationModelId } },
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

/** Edits a calculation model's name, currency, availability, formula language and attribution method; a 422 names the formulas a language lacks a function for. */
export function useUpdateCalculationModel(calculationModelId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: CalculationModelInput) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/calculation-models/{calculationModelId}', {
          params: { path: { tenantId, calculationModelId } },
          body,
        })
      ),
    onSuccess: () => invalidate(modelsKey(tenantId), lookupQueryKey(tenantId, 'calculation-models')),
  });
}

/** Every attribution of a model's metrics, by metric key, with its drivers by sort order. */
export function useAttributions(calculationModelId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: attributionsKey(tenantId, calculationModelId),
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/calculation-models/{calculationModelId}/attributions', {
          params: { path: { tenantId, calculationModelId } },
        })
      ),
  });
}

/** Replaces a metric's attribution with its drivers; a 422 names the model rules it breaks. */
export function useReplaceAttribution(calculationModelId: number, metricKey: string) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: MetricAttributionInput) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/calculation-models/{calculationModelId}/attributions/{metricKey}', {
          params: { path: { tenantId, calculationModelId, metricKey } },
          body,
        })
      ),
    onSuccess: () => invalidate(attributionsKey(tenantId, calculationModelId)),
  });
}

/** Deletes a metric's attribution with its drivers, which leaves a calculated measure the default drivers of its formula. */
export function useDeleteAttribution(calculationModelId: number, metricKey: string) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async () =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/calculation-models/{calculationModelId}/attributions/{metricKey}', {
          params: { path: { tenantId, calculationModelId, metricKey } },
        })
      ),
    onSuccess: () => invalidate(attributionsKey(tenantId, calculationModelId)),
  });
}

/** Every parameter a model declares, by key. */
export function useModelParameters(calculationModelId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: parametersKey(tenantId, calculationModelId),
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/calculation-models/{calculationModelId}/parameters', {
          params: { path: { tenantId, calculationModelId } },
        })
      ),
  });
}

/**
 * Writes a parameter whole under its key, creating it or replacing the one stored; a 422 names the formulas or report rows the
 * change would break, such as a new type a formula reads otherwise.
 */
export function useSaveModelParameter(calculationModelId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async ({ parameterKey, body }: { parameterKey: string; body: ModelParameterInput }) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/calculation-models/{calculationModelId}/parameters/{parameterKey}', {
          params: { path: { tenantId, calculationModelId, parameterKey } },
          body,
        })
      ),
    onSuccess: () => invalidate(parametersKey(tenantId, calculationModelId), reportEditorsKey(tenantId)),
  });
}

/** Deletes a parameter; a 422 names the formulas that read it and the report rows that set it. */
export function useDeleteModelParameter(calculationModelId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (parameterKey: string) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/calculation-models/{calculationModelId}/parameters/{parameterKey}', {
          params: { path: { tenantId, calculationModelId, parameterKey } },
        })
      ),
    onSuccess: () => invalidate(parametersKey(tenantId, calculationModelId), reportEditorsKey(tenantId)),
  });
}

/** Deletes a model that no report reads, with its variables, calculated measures, attributions and parameters; a 409 names the reports that read it. */
export function useDeleteCalculationModel() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (calculationModelId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/calculation-models/{calculationModelId}', {
          params: { path: { tenantId, calculationModelId } },
        })
      ),
    onSuccess: () => invalidate(modelsKey(tenantId), lookupQueryKey(tenantId, 'calculation-models')),
  });
}
