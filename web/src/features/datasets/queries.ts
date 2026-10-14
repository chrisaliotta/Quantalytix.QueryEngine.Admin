import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { useTenantId } from '@/shared/useTenantId';

/** Every dataset of the current tenant, sorted by key. */
export function useDatasets() {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: ['datasets', tenantId],
    queryFn: async () =>
      unwrap(await apiClient.GET('/api/tenants/{tenantId}/datasets', { params: { path: { tenantId } } })),
  });
}

/** One dataset of the current tenant. */
export function useDataset(datasetId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: ['datasets', tenantId, datasetId],
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/datasets/{datasetId}', {
          params: { path: { tenantId, datasetId } },
        })
      ),
  });
}

/** One dataset version, with its source object and settings. */
export function useDatasetVersion(versionId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: ['dataset-versions', tenantId, versionId],
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/dataset-versions/{versionId}', {
          params: { path: { tenantId, versionId } },
        })
      ),
  });
}

/** The fields of a dataset version, sorted by key. */
export function useFields(versionId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: ['fields', tenantId, versionId],
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/dataset-versions/{versionId}/fields', {
          params: { path: { tenantId, versionId } },
        })
      ),
  });
}

/** The grains of a dataset version, sorted by key, each with its field IDs in order. */
export function useGrains(versionId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: ['grains', tenantId, versionId],
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/dataset-versions/{versionId}/grains', {
          params: { path: { tenantId, versionId } },
        })
      ),
  });
}

/** The tables and views of the tenant database that datasets can query, sorted by name. */
export function useSourceObjects() {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: ['source-objects', tenantId],
    queryFn: async () =>
      unwrap(await apiClient.GET('/api/tenants/{tenantId}/source-objects', { params: { path: { tenantId } } })),
  });
}

/** The columns of a table or view, in ordinal order, with the field type each maps to. */
export function useSourceColumns(name: string) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: ['source-columns', tenantId, name],
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/source-objects/{name}/columns', {
          params: { path: { tenantId, name } },
        })
      ),
  });
}
