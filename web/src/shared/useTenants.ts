import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';

/** Query key of the tenants the user can see; they come from the control plane, so the key has no tenant segment. */
export function tenantsQueryKey() {
  return ['tenants'] as const;
}

/** Tenants visible to the user: all of them for a SystemAdmin, otherwise their own. */
export function useTenants() {
  return useQuery({
    queryKey: tenantsQueryKey(),
    queryFn: async () => unwrap(await apiClient.GET('/api/tenants')),
  });
}
