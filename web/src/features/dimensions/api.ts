import { useMutation, useQuery, type QueryKey } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { queryClient } from '@/api/queryClient';
import type { LookupKind } from '@/shared/types';
import { lookupQueryKey } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import type {
  DimensionInput,
  DimensionVersionInput,
  LevelInput,
  MemberInput,
  MemberPlacement,
  NewDimensionInput,
  NewDimensionVersionInput,
  NewLevelInput,
  NewMemberInput,
  RangeBandInput,
} from './types';

// Query keys start with [resource, tenantId] so invalidateTenantQueries reaches them.
function dimensionsKey(tenantId: string) {
  return ['dimensions', tenantId] as const;
}

function versionKey(tenantId: string, versionId: number) {
  return ['dimension-versions', tenantId, versionId] as const;
}

function levelsKey(tenantId: string, versionId: number) {
  return ['dimension-levels', tenantId, versionId] as const;
}

function membersKey(tenantId: string, versionId: number) {
  return ['dimension-members', tenantId, versionId] as const;
}

function rangeBandsKey(tenantId: string, versionId: number) {
  return ['range-bands', tenantId, versionId] as const;
}

/** Marks the given queries and lookup kinds of the tenant stale. */
function refresh(tenantId: string, keys: QueryKey[], lookups: LookupKind[]) {
  const all = [...keys, ...lookups.map(kind => lookupQueryKey(tenantId, kind))];
  return Promise.all(all.map(queryKey => queryClient.invalidateQueries({ queryKey })));
}

/** Every dimension of the current tenant, by key. */
export function useDimensions() {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: dimensionsKey(tenantId),
    queryFn: async () =>
      unwrap(await apiClient.GET('/api/tenants/{tenantId}/dimensions', { params: { path: { tenantId } } })),
  });
}

/** One dimension. */
export function useDimension(dimensionId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: [...dimensionsKey(tenantId), dimensionId],
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/dimensions/{dimensionId}', {
          params: { path: { tenantId, dimensionId } },
        })
      ),
  });
}

/** One dimension version; `null` keeps the query idle until a version is selected. */
export function useDimensionVersion(versionId: number | null) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: versionKey(tenantId, versionId ?? 0),
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/dimension-versions/{dimensionVersionId}', {
          params: { path: { tenantId, dimensionVersionId: versionId as number } },
        })
      ),
    enabled: versionId !== null,
  });
}

/** The levels of a dimension version, by ordinal. */
export function useLevels(versionId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: levelsKey(tenantId, versionId),
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/dimension-versions/{dimensionVersionId}/levels', {
          params: { path: { tenantId, dimensionVersionId: versionId } },
        })
      ),
  });
}

/** The members of a dimension version, by sort order. */
export function useMembers(versionId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: membersKey(tenantId, versionId),
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/dimension-versions/{dimensionVersionId}/members', {
          params: { path: { tenantId, dimensionVersionId: versionId } },
        })
      ),
  });
}

/** The range bands of a range dimension version, by ordinal. */
export function useRangeBands(versionId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: rangeBandsKey(tenantId, versionId),
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/dimension-versions/{dimensionVersionId}/range-bands', {
          params: { path: { tenantId, dimensionVersionId: versionId } },
        })
      ),
  });
}

/** Creates a dimension with its draft version 1. */
export function useCreateDimension() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: NewDimensionInput) =>
      unwrap(await apiClient.POST('/api/tenants/{tenantId}/dimensions', { params: { path: { tenantId } }, body })),
    onSuccess: () => refresh(tenantId, [dimensionsKey(tenantId)], ['dimensions']),
  });
}

/** Edits a dimension's identity. */
export function useUpdateDimension(dimensionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: DimensionInput) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/dimensions/{dimensionId}', {
          params: { path: { tenantId, dimensionId } },
          body,
        })
      ),
    onSuccess: () => refresh(tenantId, [dimensionsKey(tenantId)], ['dimensions']),
  });
}

/** Deletes a dimension whose versions are all drafts; a 409 names what blocks it. */
export function useDeleteDimension() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (dimensionId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/dimensions/{dimensionId}', {
          params: { path: { tenantId, dimensionId } },
        })
      ),
    onSuccess: () => refresh(tenantId, [dimensionsKey(tenantId)], ['dimensions', 'dimension-versions']),
  });
}

/** Creates a draft version of a dimension, empty or cloned (which also clones its mapping versions). */
export function useCreateDimensionVersion(dimensionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: NewDimensionVersionInput) =>
      unwrap(
        await apiClient.POST('/api/tenants/{tenantId}/dimensions/{dimensionId}/versions', {
          params: { path: { tenantId, dimensionId } },
          body,
        })
      ),
    onSuccess: () => refresh(tenantId, [], ['dimension-versions', 'mapping-versions']),
  });
}

/** Edits a draft version's range data type and validity window. */
export function useUpdateDimensionVersion(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: DimensionVersionInput) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/dimension-versions/{dimensionVersionId}', {
          params: { path: { tenantId, dimensionVersionId: versionId } },
          body,
        })
      ),
    onSuccess: () => refresh(tenantId, [versionKey(tenantId, versionId)], ['dimension-versions']),
  });
}

/** Deletes a draft version with its content; a 409 names what pins it. */
export function useDeleteDimensionVersion() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (versionId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/dimension-versions/{dimensionVersionId}', {
          params: { path: { tenantId, dimensionVersionId: versionId } },
        })
      ),
    onSuccess: () => refresh(tenantId, [], ['dimension-versions', 'mapping-versions']),
  });
}

/** Adds a level to a draft version. */
export function useCreateLevel(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: NewLevelInput) =>
      unwrap(
        await apiClient.POST('/api/tenants/{tenantId}/dimension-versions/{dimensionVersionId}/levels', {
          params: { path: { tenantId, dimensionVersionId: versionId } },
          body,
        })
      ),
    onSuccess: () => refresh(tenantId, [levelsKey(tenantId, versionId)], ['levels']),
  });
}

/** Edits a level of a draft version. */
export function useUpdateLevel(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async ({ levelId, body }: { levelId: number; body: LevelInput }) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/dimension-versions/{dimensionVersionId}/levels/{levelId}', {
          params: { path: { tenantId, dimensionVersionId: versionId, levelId } },
          body,
        })
      ),
    onSuccess: () => refresh(tenantId, [levelsKey(tenantId, versionId)], ['levels']),
  });
}

/** Deletes a level of a draft version; a 409 names the members that use it. */
export function useDeleteLevel(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (levelId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/dimension-versions/{dimensionVersionId}/levels/{levelId}', {
          params: { path: { tenantId, dimensionVersionId: versionId, levelId } },
        })
      ),
    onSuccess: () => refresh(tenantId, [levelsKey(tenantId, versionId)], ['levels']),
  });
}

/** Adds a member to a draft version. */
export function useCreateMember(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: NewMemberInput) =>
      unwrap(
        await apiClient.POST('/api/tenants/{tenantId}/dimension-versions/{dimensionVersionId}/members', {
          params: { path: { tenantId, dimensionVersionId: versionId } },
          body,
        })
      ),
    onSuccess: () => refresh(tenantId, [membersKey(tenantId, versionId)], ['members']),
  });
}

/** Edits a member of a draft version. */
export function useUpdateMember(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async ({ memberId, body }: { memberId: number; body: MemberInput }) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/dimension-versions/{dimensionVersionId}/members/{memberId}', {
          params: { path: { tenantId, dimensionVersionId: versionId, memberId } },
          body,
        })
      ),
    onSuccess: () => refresh(tenantId, [membersKey(tenantId, versionId)], ['members']),
  });
}

/** Deletes a member of a draft version; a 409 names its children and references. */
export function useDeleteMember(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (memberId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/dimension-versions/{dimensionVersionId}/members/{memberId}', {
          params: { path: { tenantId, dimensionVersionId: versionId, memberId } },
        })
      ),
    onSuccess: () => refresh(tenantId, [membersKey(tenantId, versionId)], ['members']),
  });
}

/** Stores a drag-and-drop reparent or reorder; the answer is the version's members, which replace the cached list. */
export function useReorderMembers(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (items: MemberPlacement[]) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/dimension-versions/{dimensionVersionId}/members/order', {
          params: { path: { tenantId, dimensionVersionId: versionId } },
          body: { items },
        })
      ),
    onSuccess: members => {
      queryClient.setQueryData(membersKey(tenantId, versionId), members);
      return refresh(tenantId, [], ['members']);
    },
  });
}

/** Replaces every band of a draft range version. */
export function useReplaceRangeBands(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (bands: RangeBandInput[]) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/dimension-versions/{dimensionVersionId}/range-bands', {
          params: { path: { tenantId, dimensionVersionId: versionId } },
          body: { bands },
        })
      ),
    onSuccess: bands => {
      queryClient.setQueryData(rangeBandsKey(tenantId, versionId), bands);
    },
  });
}
