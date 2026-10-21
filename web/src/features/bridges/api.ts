import { useMutation, useQuery, type QueryKey } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { queryClient } from '@/api/queryClient';
import type { DimensionPurpose } from '@/shared/types';
import { lookupQueryKey } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import type {
  BridgeInput,
  BridgeMappingInput,
  BridgeVersionInput,
  NewBridgeInput,
  NewBridgeVersionInput,
} from './types';

// Query keys start with [resource, tenantId] so invalidateTenantQueries reaches them.
function bridgesKey(tenantId: string) {
  return ['bridges', tenantId] as const;
}

function versionKey(tenantId: string, versionId: number) {
  return ['bridge-versions', tenantId, versionId] as const;
}

function mappingsKey(tenantId: string, versionId: number) {
  return ['bridge-mappings', tenantId, versionId] as const;
}

/** Marks the given queries stale. */
function invalidate(...queryKeys: QueryKey[]) {
  return Promise.all(queryKeys.map(queryKey => queryClient.invalidateQueries({ queryKey })));
}

/** The bridges of the current tenant with their purposes, by key; `purpose` narrows them to analytical or report bridges. */
export function useBridges(purpose?: DimensionPurpose) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: [...bridgesKey(tenantId), 'list', purpose],
    queryFn: async () =>
      unwrap(await apiClient.GET('/api/tenants/{tenantId}/bridges', { params: { path: { tenantId }, query: { purpose } } })),
  });
}

/** One bridge with its purpose and, for a report bridge, the report source that owns it. */
export function useBridge(bridgeId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: [...bridgesKey(tenantId), bridgeId],
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/bridges/{bridgeId}', { params: { path: { tenantId, bridgeId } } })
      ),
  });
}

/** One bridge version; `null` keeps the query idle until a version is selected. */
export function useBridgeVersion(versionId: number | null) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: versionKey(tenantId, versionId ?? 0),
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/bridge-versions/{versionId}', {
          params: { path: { tenantId, versionId: versionId as number } },
        })
      ),
    enabled: versionId !== null,
  });
}

/** The mappings of a bridge version. */
export function useBridgeMappings(versionId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: mappingsKey(tenantId, versionId),
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/bridge-versions/{versionId}/mappings', {
          params: { path: { tenantId, versionId } },
        })
      ),
  });
}

/** Creates a bridge without versions. */
export function useCreateBridge() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: NewBridgeInput) =>
      unwrap(await apiClient.POST('/api/tenants/{tenantId}/bridges', { params: { path: { tenantId } }, body })),
    onSuccess: () => invalidate(bridgesKey(tenantId), lookupQueryKey(tenantId, 'bridges')),
  });
}

/** Edits a bridge's name and availability. */
export function useUpdateBridge(bridgeId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: BridgeInput) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/bridges/{bridgeId}', {
          params: { path: { tenantId, bridgeId } },
          body,
        })
      ),
    onSuccess: () => invalidate(bridgesKey(tenantId), lookupQueryKey(tenantId, 'bridges')),
  });
}

/** Deletes a bridge whose versions are all drafts; a 409 names what blocks it. */
export function useDeleteBridge() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (bridgeId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/bridges/{bridgeId}', {
          params: { path: { tenantId, bridgeId } },
        })
      ),
    onSuccess: () =>
      invalidate(
        bridgesKey(tenantId),
        lookupQueryKey(tenantId, 'bridges'),
        lookupQueryKey(tenantId, 'bridge-versions')
      ),
  });
}

/** Creates a draft version of a bridge, empty or a copy of another version's mappings. */
export function useCreateBridgeVersion(bridgeId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: NewBridgeVersionInput) =>
      unwrap(
        await apiClient.POST('/api/tenants/{tenantId}/bridges/{bridgeId}/versions', {
          params: { path: { tenantId, bridgeId } },
          body,
        })
      ),
    onSuccess: () => invalidate(lookupQueryKey(tenantId, 'bridge-versions')),
  });
}

/** Edits a draft or published version's cardinality and allocation mode. */
export function useUpdateBridgeVersion(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: BridgeVersionInput) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/bridge-versions/{versionId}', {
          params: { path: { tenantId, versionId } },
          body,
        })
      ),
    onSuccess: () => invalidate(versionKey(tenantId, versionId), lookupQueryKey(tenantId, 'bridge-versions')),
  });
}

/** Deletes a draft version with its mappings. */
export function useDeleteBridgeVersion() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (versionId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/bridge-versions/{versionId}', {
          params: { path: { tenantId, versionId } },
        })
      ),
    onSuccess: () => invalidate(bridgesKey(tenantId), lookupQueryKey(tenantId, 'bridge-versions')),
  });
}

/** Replaces every mapping of a draft or published version; the stored mappings replace the cached list. */
export function useReplaceBridgeMappings(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (mappings: BridgeMappingInput[]) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/bridge-versions/{versionId}/mappings', {
          params: { path: { tenantId, versionId } },
          body: { mappings },
        })
      ),
    onSuccess: mappings => {
      queryClient.setQueryData(mappingsKey(tenantId, versionId), mappings);
    },
  });
}
