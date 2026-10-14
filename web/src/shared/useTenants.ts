import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';

/** Tenants visible to the user: all of them for a SystemAdmin, otherwise their own. */
export function useTenants() {
  return useQuery({
    queryKey: ['tenants'],
    queryFn: async () => unwrap(await apiClient.GET('/api/tenants')),
  });
}
