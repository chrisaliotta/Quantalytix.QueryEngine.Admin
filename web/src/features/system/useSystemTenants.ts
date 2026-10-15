import { useMutation, useQuery } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { queryClient } from '@/api/queryClient';
import { tenantsQueryKey } from '@/shared/useTenants';
import type { TenantCreateInput, TenantUpdateInput } from './types';

/** Query key of the tenant directory; it is control-plane data, so it has no tenant segment. */
const TENANT_DIRECTORY_KEY = ['system-tenants'] as const;

/** Refreshes the directory and the tenant switcher's list (`useTenants`) after a write. */
function invalidateTenantDirectory() {
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: TENANT_DIRECTORY_KEY }),
    queryClient.invalidateQueries({ queryKey: tenantsQueryKey() }),
  ]);
}

/** Every tenant in the directory, with masked connection strings. SystemAdmins only; others get 403. */
export function useTenantDirectory() {
  return useQuery({
    queryKey: TENANT_DIRECTORY_KEY,
    queryFn: async () => unwrap(await apiClient.GET('/api/system/tenants')),
  });
}

/** Registers a tenant; the API verifies its connection string first. */
export function useCreateTenant() {
  return useMutation({
    mutationFn: async (body: TenantCreateInput) => unwrap(await apiClient.POST('/api/system/tenants', { body })),
    onSuccess: invalidateTenantDirectory,
  });
}

/** Edits a tenant's provider, cache lifetime and, when one is given, its connection string. */
export function useUpdateTenant() {
  return useMutation({
    mutationFn: async ({ tenantId, body }: { tenantId: string; body: TenantUpdateInput }) =>
      unwrap(await apiClient.PUT('/api/system/tenants/{tenantId}', { params: { path: { tenantId } }, body })),
    onSuccess: invalidateTenantDirectory,
  });
}

/** Permits or stops query routing for a tenant. */
export function useSetTenantEnabled() {
  return useMutation({
    mutationFn: async ({ tenantId, isEnabled }: { tenantId: string; isEnabled: boolean }) =>
      unwrap(
        await apiClient.PUT('/api/system/tenants/{tenantId}/enabled', {
          params: { path: { tenantId } },
          body: { isEnabled },
        })
      ),
    onSuccess: invalidateTenantDirectory,
  });
}

/** Tests a connection string for a tenant without saving it; resolves to `{ isValid, message }`. */
export function useVerifyTenantConnection() {
  return useMutation({
    mutationFn: async (body: { tenantId: string; connectionString: string }) =>
      unwrap(await apiClient.POST('/api/system/tenants/verify', { body })),
  });
}
