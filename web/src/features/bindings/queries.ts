import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { useTenantId } from '@/shared/useTenantId';

/** The bindings of a dataset version, sorted by display name, each with its time extension. */
export function useBindings(versionId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: ['bindings', tenantId, versionId],
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/dataset-versions/{versionId}/bindings', {
          params: { path: { tenantId, versionId } },
        })
      ),
  });
}
