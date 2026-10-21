import { useMutation } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { invalidateTenantQueries } from '@/api/queryClient';
import type { VersionKind, VersionStatus } from './types';
import { useTenantId } from './useTenantId';

/**
 * Mutation that moves a version of `kind` to another status. On success every query of the
 * tenant is refreshed; a 409 or 422 (with `violations`) rejects with an `ApiProblem`.
 * @example transition.mutate({ id: versionId, status: 'validated' })
 */
export function useVersionTransition(kind: VersionKind) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async ({ id, status }: { id: number; status: VersionStatus }) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/versions/{kind}/{id}/status', {
          params: { path: { tenantId, kind, id } },
          body: { status },
        })
      ),
    onSuccess: () => invalidateTenantQueries(tenantId),
  });
}
