import { useMutation, useQuery, type QueryKey } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { queryClient } from '@/api/queryClient';
import { lookupQueryKey } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import type { MappingEntryInput, MappingSetInput, NewMappingSetInput, NewMappingVersionInput } from './types';

// Query keys start with [resource, tenantId] so invalidateTenantQueries reaches them.
function mappingSetsKey(tenantId: string) {
  return ['mapping-sets', tenantId] as const;
}

function entriesKey(tenantId: string, versionId: number) {
  return ['mapping-entries', tenantId, versionId] as const;
}

/** Marks the given queries stale. */
function invalidate(...queryKeys: QueryKey[]) {
  return Promise.all(queryKeys.map(queryKey => queryClient.invalidateQueries({ queryKey })));
}

/** Every mapping set of the current tenant, by key. */
export function useMappingSets() {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: mappingSetsKey(tenantId),
    queryFn: async () =>
      unwrap(await apiClient.GET('/api/tenants/{tenantId}/mapping-sets', { params: { path: { tenantId } } })),
  });
}

/** One mapping set. */
export function useMappingSet(mappingSetId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: [...mappingSetsKey(tenantId), mappingSetId],
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/mapping-sets/{mappingSetId}', {
          params: { path: { tenantId, mappingSetId } },
        })
      ),
  });
}

/** The entries of a mapping version, by source key. */
export function useMappingEntries(versionId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: entriesKey(tenantId, versionId),
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/mapping-versions/{versionId}/entries', {
          params: { path: { tenantId, versionId } },
        })
      ),
  });
}

/** Creates a mapping set without versions. */
export function useCreateMappingSet() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: NewMappingSetInput) =>
      unwrap(await apiClient.POST('/api/tenants/{tenantId}/mapping-sets', { params: { path: { tenantId } }, body })),
    onSuccess: () => invalidate(mappingSetsKey(tenantId), lookupQueryKey(tenantId, 'mapping-sets')),
  });
}

/** Edits a mapping set's source namespace and name. */
export function useUpdateMappingSet(mappingSetId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: MappingSetInput) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/mapping-sets/{mappingSetId}', {
          params: { path: { tenantId, mappingSetId } },
          body,
        })
      ),
    onSuccess: () => invalidate(mappingSetsKey(tenantId), lookupQueryKey(tenantId, 'mapping-sets')),
  });
}

/** Deletes a mapping set whose versions are all drafts; a 409 names what blocks it. */
export function useDeleteMappingSet() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (mappingSetId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/mapping-sets/{mappingSetId}', {
          params: { path: { tenantId, mappingSetId } },
        })
      ),
    onSuccess: () =>
      invalidate(
        mappingSetsKey(tenantId),
        lookupQueryKey(tenantId, 'mapping-sets'),
        lookupQueryKey(tenantId, 'mapping-versions')
      ),
  });
}

/** Creates a draft version of a mapping set, empty or cloned; the answer names the source keys a clone dropped. */
export function useCreateMappingVersion(mappingSetId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: NewMappingVersionInput) =>
      unwrap(
        await apiClient.POST('/api/tenants/{tenantId}/mapping-sets/{mappingSetId}/versions', {
          params: { path: { tenantId, mappingSetId } },
          body,
        })
      ),
    onSuccess: () => invalidate(lookupQueryKey(tenantId, 'mapping-versions')),
  });
}

/** Deletes a draft mapping version with its entries. */
export function useDeleteMappingVersion() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (versionId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/mapping-versions/{versionId}', {
          params: { path: { tenantId, versionId } },
        })
      ),
    onSuccess: () => invalidate(lookupQueryKey(tenantId, 'mapping-versions')),
  });
}

/** Adds an entry to a draft version. */
export function useCreateEntry(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: MappingEntryInput) =>
      unwrap(
        await apiClient.POST('/api/tenants/{tenantId}/mapping-versions/{versionId}/entries', {
          params: { path: { tenantId, versionId } },
          body,
        })
      ),
    onSuccess: () => invalidate(entriesKey(tenantId, versionId)),
  });
}

/** Edits an entry of a draft version. */
export function useUpdateEntry(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async ({ entryId, body }: { entryId: number; body: MappingEntryInput }) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/mapping-versions/{versionId}/entries/{entryId}', {
          params: { path: { tenantId, versionId, entryId } },
          body,
        })
      ),
    onSuccess: () => invalidate(entriesKey(tenantId, versionId)),
  });
}

/** Deletes an entry of a draft version. */
export function useDeleteEntry(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (entryId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/mapping-versions/{versionId}/entries/{entryId}', {
          params: { path: { tenantId, versionId, entryId } },
        })
      ),
    onSuccess: () => invalidate(entriesKey(tenantId, versionId)),
  });
}

/** Replaces every entry of a draft version; the stored entries replace the cached list. */
export function useReplaceEntries(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (entries: MappingEntryInput[]) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/mapping-versions/{versionId}/entries', {
          params: { path: { tenantId, versionId } },
          body: { entries },
        })
      ),
    onSuccess: entries => {
      queryClient.setQueryData(entriesKey(tenantId, versionId), entries);
    },
  });
}
