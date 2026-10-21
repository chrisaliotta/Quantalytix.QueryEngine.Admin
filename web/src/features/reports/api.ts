import { useMutation, useQuery, type QueryKey } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { queryClient } from '@/api/queryClient';
import { lookupQueryKey } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import { reportColumnsKey, reportEditorKey, reportKey, reportsKey, reportVariableScopesKey } from './queryKeys';
import type { NewReportInput, Report, ReportColumnInput, ReportInput, ReportSettingsInput, ReportVariableScopeInput } from './types';

/** The dimensions feature's key of a dimension version's members, so a member edit there refreshes what reads them here. */
function dimensionMembersKey(tenantId: string, dimensionVersionId: number) {
  return ['dimension-members', tenantId, dimensionVersionId] as const;
}

/** Marks the given queries stale. */
function invalidate(...queryKeys: QueryKey[]) {
  return Promise.all(queryKeys.map(queryKey => queryClient.invalidateQueries({ queryKey })));
}

/** Caches a report a write answers with, and refreshes the list and the editor view, which holds it too. */
function storeReport(tenantId: string, report: Report) {
  queryClient.setQueryData(reportKey(tenantId, report.reportId), report);
  return invalidate(reportsKey(tenantId), reportEditorKey(tenantId, report.reportId));
}

/** Every report of the current tenant, by key. */
export function useReports() {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: reportsKey(tenantId),
    queryFn: async () => unwrap(await apiClient.GET('/api/tenants/{tenantId}/reports', { params: { path: { tenantId } } })),
  });
}

/** One report, with its settings and whether it is enabled. */
export function useReport(reportId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: reportKey(tenantId, reportId),
    queryFn: async () =>
      unwrap(await apiClient.GET('/api/tenants/{tenantId}/reports/{reportId}', { params: { path: { tenantId, reportId } } })),
  });
}

/** The columns of a report, in display order. */
export function useReportColumns(reportId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: reportColumnsKey(tenantId, reportId),
    queryFn: async () =>
      unwrap(await apiClient.GET('/api/tenants/{tenantId}/reports/{reportId}/columns', { params: { path: { tenantId, reportId } } })),
  });
}

/** The variable scopes of a report; a variable without one reads every row its sources reach. */
export function useReportVariableScopes(reportId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: reportVariableScopesKey(tenantId, reportId),
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/reports/{reportId}/variable-scopes', { params: { path: { tenantId, reportId } } })
      ),
  });
}

/**
 * The members of a dimension version, with their parents, sort orders and whether they take source values, such as the
 * members of a bridge source's item dimension; `null` keeps the query idle until the version is known.
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

/** Creates a disabled report over a model with a published version, with its own report dimension. */
export function useCreateReport() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: NewReportInput) =>
      unwrap(await apiClient.POST('/api/tenants/{tenantId}/reports', { params: { path: { tenantId } }, body })),
    onSuccess: () => invalidate(reportsKey(tenantId), lookupQueryKey(tenantId, 'reports'), lookupQueryKey(tenantId, 'dimensions')),
  });
}

/** Edits a report's name, type and payload; the engine reloads when the report is enabled. */
export function useUpdateReport(reportId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: ReportInput) =>
      unwrap(await apiClient.PUT('/api/tenants/{tenantId}/reports/{reportId}', { params: { path: { tenantId, reportId } }, body })),
    onSuccess: report => Promise.all([storeReport(tenantId, report), invalidate(lookupQueryKey(tenantId, 'reports'))]),
  });
}

/** Replaces a report's default profile, subtotal position and defaults; an enabled report refuses settings that break its rules. */
export function useUpdateReportSettings(reportId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: ReportSettingsInput) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/reports/{reportId}/settings', { params: { path: { tenantId, reportId } }, body })
      ),
    onSuccess: report => storeReport(tenantId, report),
  });
}

/** Enables or disables a report; enabling answers 422 with the rules the report breaks, and it stays disabled. */
export function useSetReportEnabled(reportId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (isEnabled: boolean) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/reports/{reportId}/status', {
          params: { path: { tenantId, reportId } },
          body: { isEnabled },
        })
      ),
    onSuccess: report => storeReport(tenantId, report),
  });
}

/** Checks a report against every report rule, enabled or not, changing nothing. */
export function useReportCheck(reportId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async () =>
      unwrap(await apiClient.POST('/api/tenants/{tenantId}/reports/{reportId}/validation', { params: { path: { tenantId, reportId } } })),
  });
}

/** Deletes a report with its content, its sources' bridges and its report dimension. */
export function useDeleteReport() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (reportId: number) =>
      unwrap(await apiClient.DELETE('/api/tenants/{tenantId}/reports/{reportId}', { params: { path: { tenantId, reportId } } })),
    onSuccess: () =>
      invalidate(
        reportsKey(tenantId),
        lookupQueryKey(tenantId, 'reports'),
        lookupQueryKey(tenantId, 'dimensions'),
        lookupQueryKey(tenantId, 'bridges')
      ),
  });
}

/** Replaces a report's columns; the stored columns replace the cached list. */
export function useReplaceReportColumns(reportId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (columns: ReportColumnInput[]) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/reports/{reportId}/columns', {
          params: { path: { tenantId, reportId } },
          body: { columns },
        })
      ),
    onSuccess: columns => {
      queryClient.setQueryData(reportColumnsKey(tenantId, reportId), columns);
      return invalidate(lookupQueryKey(tenantId, 'report-columns'), reportEditorKey(tenantId, reportId));
    },
  });
}

/** Replaces a report's variable scopes; the stored scopes replace the cached list. */
export function useReplaceReportVariableScopes(reportId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (scopes: ReportVariableScopeInput[]) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/reports/{reportId}/variable-scopes', {
          params: { path: { tenantId, reportId } },
          body: { scopes },
        })
      ),
    onSuccess: scopes => {
      queryClient.setQueryData(reportVariableScopesKey(tenantId, reportId), scopes);
    },
  });
}
