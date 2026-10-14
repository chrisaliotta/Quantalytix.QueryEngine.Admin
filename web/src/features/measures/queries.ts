import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { useTenantId } from '@/shared/useTenantId';

/** The measures of a dataset version, sorted by key, each with its components, formula dependencies and time policy. */
export function useMeasures(versionId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: ['measures', tenantId, versionId],
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/dataset-versions/{versionId}/measures', {
          params: { path: { tenantId, versionId } },
        })
      ),
  });
}
