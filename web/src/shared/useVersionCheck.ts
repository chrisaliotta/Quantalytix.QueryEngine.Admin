import { useMutation } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import type { VersionKind } from './types';
import { useTenantId } from './useTenantId';

/**
 * Mutation that runs the publication rules of a version of `kind` without changing it: the Validate check of a published
 * living version, whose live edits no rules gate.
 * @example check.mutate(versionId)
 */
export function useVersionCheck(kind: VersionKind) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (id: number) =>
      unwrap(
        await apiClient.POST('/api/tenants/{tenantId}/versions/{kind}/{id}/validation', {
          params: { path: { tenantId, kind, id } },
        })
      ),
  });
}

/** A mutation that runs a version's publication rules without changing it, such as `useVersionCheck`'s. */
export type VersionCheckMutation = ReturnType<typeof useVersionCheck>;
