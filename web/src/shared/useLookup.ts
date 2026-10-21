import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import type { DimensionPurpose, LookupKind } from './types';
import { useTenantId } from './useTenantId';

/** Query-key prefix of one lookup kind; invalidate it after creating, renaming or deleting that entity. */
export function lookupQueryKey(tenantId: string, kind: LookupKind) {
  return ['lookups', tenantId, kind] as const;
}

/**
 * Lookup items of one kind for the current tenant. Pass `parentId` for parent-scoped kinds; `null` means the parent is not
 * chosen yet and keeps the query idle. `purpose` narrows the `dimensions` kind to dimensions of that purpose.
 */
export function useLookup(kind: LookupKind, parentId?: number | null, purpose?: DimensionPurpose) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: [...lookupQueryKey(tenantId, kind), parentId, purpose],
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/lookups/{kind}', {
          params: { path: { tenantId, kind }, query: { parentId: parentId ?? undefined, purpose } },
        })
      ),
    enabled: parentId !== null,
  });
}
