import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { useTenantId } from '@/shared/useTenantId';

/** The current tenant's identity, semantic revision, active release, counts and drafts in progress. */
export function useOverview() {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: ['overview', tenantId],
    queryFn: async () =>
      unwrap(await apiClient.GET('/api/tenants/{tenantId}/overview', { params: { path: { tenantId } } })),
  });
}
