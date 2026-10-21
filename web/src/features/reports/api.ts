import { useMutation, useQuery, type QueryKey } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { queryClient } from '@/api/queryClient';
import { lookupQueryKey } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import { reportDesignKey, reportVariableScopesKey, reportVersionKey } from './queryKeys';
import type {
  NewReportInput,
  NewReportVersionInput,
  ReportColumnInput,
  ReportInput,
  ReportVariableScopeInput,
  ReportVersionInput,
} from './types';

// Query keys start with [resource, tenantId] so invalidateTenantQueries reaches them.
function reportsKey(tenantId: string) {
  return ['reports', tenantId] as const;
}

function columnsKey(tenantId: string, versionId: number) {
  return ['report-columns', tenantId, versionId] as const;
}

/** The dimensions feature's key of a dimension version's members, so a member edit there refreshes what reads them here. */
function dimensionMembersKey(tenantId: string, dimensionVersionId: number) {
  return ['dimension-members', tenantId, dimensionVersionId] as const;
}

/** Marks the given queries stale. */
function invalidate(...queryKeys: QueryKey[]) {
  return Promise.all(queryKeys.map(queryKey => queryClient.invalidateQueries({ queryKey })));
}

/** Every report of the current tenant, by key. */
export function useReports() {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: reportsKey(tenantId),
    queryFn: async () => unwrap(await apiClient.GET('/api/tenants/{tenantId}/reports', { params: { path: { tenantId } } })),
  });
}

/** One report, with its current-version pointer. */
export function useReport(reportId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: [...reportsKey(tenantId), reportId],
    queryFn: async () =>
      unwrap(await apiClient.GET('/api/tenants/{tenantId}/reports/{reportId}', { params: { path: { tenantId, reportId } } })),
  });
}

/** One report version; `null` keeps the query idle until a version is selected. */
export function useReportVersion(versionId: number | null) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: reportVersionKey(tenantId, versionId ?? 0),
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/report-versions/{versionId}', {
          params: { path: { tenantId, versionId: versionId as number } },
        })
      ),
    enabled: versionId !== null,
  });
}

/** The columns of a report version, in display order. */
export function useReportColumns(versionId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: columnsKey(tenantId, versionId),
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/report-versions/{versionId}/columns', {
          params: { path: { tenantId, versionId } },
        })
      ),
  });
}

/** The variable scopes of a report version; a variable without one reads every row its sources classify. */
export function useReportVariableScopes(versionId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: reportVariableScopesKey(tenantId, versionId),
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/report-versions/{versionId}/variable-scopes', {
          params: { path: { tenantId, versionId } },
        })
      ),
  });
}

/**
 * The members of a dimension version, with their parents, sort orders and whether they take source values, such as the
 * members of a design source's bound dimension; `null` keeps the query idle until the version is known.
 */
export function useDimensionVersionMembers(dimensionVersionId: number | null) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: dimensionMembersKey(tenantId, dimensionVersionId ?? 0),
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/dimension-versions/{dimensionVersionId}/members', {
          params: { path: { tenantId, dimensionVersionId: dimensionVersionId as number } },
        })
      ),
    enabled: dimensionVersionId !== null,
  });
}

/** Creates a report with its own report dimension and without versions. */
export function useCreateReport() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: NewReportInput) =>
      unwrap(await apiClient.POST('/api/tenants/{tenantId}/reports', { params: { path: { tenantId } }, body })),
    onSuccess: () => invalidate(reportsKey(tenantId), lookupQueryKey(tenantId, 'reports'), lookupQueryKey(tenantId, 'dimensions')),
  });
}

/** Edits a report's name, type, availability and payload; a change refreshes the engine's cached definitions. */
export function useUpdateReport(reportId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: ReportInput) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/reports/{reportId}', { params: { path: { tenantId, reportId } }, body })
      ),
    onSuccess: () => invalidate(reportsKey(tenantId), lookupQueryKey(tenantId, 'reports')),
  });
}

/** Deletes a report whose versions are all drafts, with its report dimension and bridges; a 409 names what blocks it. */
export function useDeleteReport() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (reportId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/reports/{reportId}', { params: { path: { tenantId, reportId } } })
      ),
    onSuccess: () =>
      invalidate(
        reportsKey(tenantId),
        lookupQueryKey(tenantId, 'reports'),
        lookupQueryKey(tenantId, 'report-versions'),
        lookupQueryKey(tenantId, 'dimensions')
      ),
  });
}

/**
 * Creates the report's draft, empty or a copy of another version's content, with drafts of its report dimension and bridges
 * that copy their published versions.
 */
export function useCreateReportVersion(reportId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: NewReportVersionInput) =>
      unwrap(
        await apiClient.POST('/api/tenants/{tenantId}/reports/{reportId}/versions', {
          params: { path: { tenantId, reportId } },
          body,
        })
      ),
    onSuccess: () => invalidate(lookupQueryKey(tenantId, 'report-versions'), lookupQueryKey(tenantId, 'dimension-versions')),
  });
}

/** Edits a draft or published version's default profile, subtotal position and defaults. */
export function useUpdateReportVersion(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: ReportVersionInput) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/report-versions/{versionId}', {
          params: { path: { tenantId, versionId } },
          body,
        })
      ),
    onSuccess: () => invalidate(reportVersionKey(tenantId, versionId)),
  });
}

/** Deletes the report's draft with all its content and the drafts paired with it. */
export function useDeleteReportVersion() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (versionId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/report-versions/{versionId}', {
          params: { path: { tenantId, versionId } },
        })
      ),
    onSuccess: () =>
      invalidate(reportsKey(tenantId), lookupQueryKey(tenantId, 'report-versions'), lookupQueryKey(tenantId, 'dimension-versions')),
  });
}

/** Replaces the columns of a draft or published version; the stored columns replace the cached list. */
export function useReplaceReportColumns(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (columns: ReportColumnInput[]) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/report-versions/{versionId}/columns', {
          params: { path: { tenantId, versionId } },
          body: { columns },
        })
      ),
    onSuccess: columns => {
      queryClient.setQueryData(columnsKey(tenantId, versionId), columns);
      return invalidate(lookupQueryKey(tenantId, 'report-columns'), reportDesignKey(tenantId, versionId));
    },
  });
}

/** Replaces the variable scopes of a draft or published version; the stored scopes replace the cached list. */
export function useReplaceReportVariableScopes(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (scopes: ReportVariableScopeInput[]) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/report-versions/{versionId}/variable-scopes', {
          params: { path: { tenantId, versionId } },
          body: { scopes },
        })
      ),
    onSuccess: scopes => {
      queryClient.setQueryData(reportVariableScopesKey(tenantId, versionId), scopes);
    },
  });
}
