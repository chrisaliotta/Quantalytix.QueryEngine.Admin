import { useMutation, useQuery } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { invalidateTenantQueries, queryClient } from '@/api/queryClient';
import type { VersionStatus } from '@/shared/types';
import { lookupQueryKey } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import { invalidateVersionContent } from '@/shared/versionContent';
import { reportDesignKey, reportVariableScopesKey, reportVersionKey } from './queryKeys';
import type { DesignRowInput, DesignSourceInput, NewDesignSourceInput, ReportDesign, RowMappingsInput } from './types';

// Query keys start with [resource, tenantId] so invalidateTenantQueries reaches them.
function coverageKey(tenantId: string, versionId: number, bridgeId?: number) {
  return bridgeId === undefined
    ? (['report-design-coverage', tenantId, versionId] as const)
    : (['report-design-coverage', tenantId, versionId, bridgeId] as const);
}

/** Caches the design a write answers with and refreshes the coverage it may have changed. */
function storeDesign(tenantId: string, versionId: number, design: ReportDesign) {
  queryClient.setQueryData(reportDesignKey(tenantId, versionId), design);
  return queryClient.invalidateQueries({ queryKey: coverageKey(tenantId, versionId) });
}

/**
 * Caches the design a source write answers with, and refreshes what the write changed besides: the version's sources and
 * profiles, which the Sources and Profiles tabs list, the version's default profile, and the bridges a new source adds.
 */
function storeSourceWrite(tenantId: string, versionId: number, design: ReportDesign) {
  return Promise.all([
    storeDesign(tenantId, versionId, design),
    invalidateVersionContent(tenantId, 'variable-sources', versionId),
    queryClient.invalidateQueries({ queryKey: reportVersionKey(tenantId, versionId) }),
    queryClient.invalidateQueries({ queryKey: lookupQueryKey(tenantId, 'bridges') }),
  ]);
}

/**
 * A report version's design: its rows with the source items mapped to each, and its sources. Draft, validated and published
 * versions have one; `null` keeps the query idle, as for a retired version.
 */
export function useReportDesign(versionId: number | null) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: reportDesignKey(tenantId, versionId ?? 0),
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/report-versions/{versionId}/design', {
          params: { path: { tenantId, versionId: versionId as number } },
        })
      ),
    enabled: versionId !== null,
  });
}

/** Replaces a design's row tree; a removed row loses its mappings, options and scopes first. */
export function useReplaceDesignRows(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (rows: DesignRowInput[]) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/report-versions/{versionId}/design/rows', {
          params: { path: { tenantId, versionId } },
          body: { rows },
        })
      ),
    onSuccess: design =>
      Promise.all([
        storeDesign(tenantId, versionId, design),
        queryClient.invalidateQueries({ queryKey: reportVariableScopesKey(tenantId, versionId) }),
        queryClient.invalidateQueries({ queryKey: lookupQueryKey(tenantId, 'members') }),
      ]),
  });
}

/** Creates a design source: its bridge and one variable source per variable. */
export function useCreateDesignSource(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: NewDesignSourceInput) =>
      unwrap(
        await apiClient.POST('/api/tenants/{tenantId}/report-versions/{versionId}/design/sources', {
          params: { path: { tenantId, versionId } },
          body,
        })
      ),
    onSuccess: design => storeSourceWrite(tenantId, versionId, design),
  });
}

/** Edits a design source's label, dataset, role, time field, filter and variables. */
export function useUpdateDesignSource(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async ({ bridgeId, body }: { bridgeId: number; body: DesignSourceInput }) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/report-versions/{versionId}/design/sources/{bridgeId}', {
          params: { path: { tenantId, versionId, bridgeId } },
          body,
        })
      ),
    onSuccess: design => storeSourceWrite(tenantId, versionId, design),
  });
}

/** Deletes a design source's variable sources, and its bridge when only drafts of it exist. */
export function useDeleteDesignSource(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (bridgeId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/report-versions/{versionId}/design/sources/{bridgeId}', {
          params: { path: { tenantId, versionId, bridgeId } },
        })
      ),
    onSuccess: design => storeSourceWrite(tenantId, versionId, design),
  });
}

/** Replaces the source items that map to one data row; an item listed from another row moves here. */
export function useReplaceRowMappings(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: RowMappingsInput) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/report-versions/{versionId}/design/row-mappings', {
          params: { path: { tenantId, versionId } },
          body,
        })
      ),
    onSuccess: design => storeDesign(tenantId, versionId, design),
  });
}

/** The items of a design source that map to no row; `null` keeps the query idle until a source is chosen. */
export function useDesignCoverage(versionId: number, bridgeId: number | null) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: coverageKey(tenantId, versionId, bridgeId ?? 0),
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/report-versions/{versionId}/design/sources/{bridgeId}/coverage', {
          params: { path: { tenantId, versionId, bridgeId: bridgeId as number } },
        })
      ),
    enabled: bridgeId !== null,
  });
}

/**
 * Moves a report version's design to another status, with its report dimension's and bridges' versions; on success every
 * query of the tenant is refreshed. Takes the variables of `useVersionTransition`, so the version header can use either.
 */
export function useReportDesignTransition() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async ({ id, status }: { id: number; status: VersionStatus }) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/report-versions/{versionId}/design/status', {
          params: { path: { tenantId, versionId: id } },
          body: { status },
        })
      ),
    onSuccess: () => invalidateTenantQueries(tenantId),
  });
}

/** Checks a report version's design against the publication rules of its versions together, changing nothing. */
export function useReportDesignCheck() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (id: number) =>
      unwrap(
        await apiClient.POST('/api/tenants/{tenantId}/report-versions/{versionId}/design/validation', {
          params: { path: { tenantId, versionId: id } },
        })
      ),
  });
}
