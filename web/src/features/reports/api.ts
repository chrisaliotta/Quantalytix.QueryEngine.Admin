import { useMutation, useQuery, type QueryKey } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { queryClient } from '@/api/queryClient';
import { lookupQueryKey } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import type {
  NewReportInput,
  NewReportVersionInput,
  ReportColumnInput,
  ReportInput,
  ReportLineInput,
  ReportVersionInput,
} from './types';

// Query keys start with [resource, tenantId] so invalidateTenantQueries reaches them.
function reportsKey(tenantId: string) {
  return ['reports', tenantId] as const;
}

function versionKey(tenantId: string, versionId: number) {
  return ['report-versions', tenantId, versionId] as const;
}

function columnsKey(tenantId: string, versionId: number) {
  return ['report-columns', tenantId, versionId] as const;
}

function linesKey(tenantId: string, versionId: number) {
  return ['report-lines', tenantId, versionId] as const;
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
    queryKey: versionKey(tenantId, versionId ?? 0),
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

/** The lines of a report version, in pre-order. */
export function useReportLines(versionId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: linesKey(tenantId, versionId),
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/report-versions/{versionId}/lines', {
          params: { path: { tenantId, versionId } },
        })
      ),
  });
}

/** Creates a report without versions. */
export function useCreateReport() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: NewReportInput) =>
      unwrap(await apiClient.POST('/api/tenants/{tenantId}/reports', { params: { path: { tenantId } }, body })),
    onSuccess: () => invalidate(reportsKey(tenantId), lookupQueryKey(tenantId, 'reports')),
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

/** Deletes a report whose versions are all drafts; a 409 names what blocks it. */
export function useDeleteReport() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (reportId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/reports/{reportId}', { params: { path: { tenantId, reportId } } })
      ),
    onSuccess: () =>
      invalidate(reportsKey(tenantId), lookupQueryKey(tenantId, 'reports'), lookupQueryKey(tenantId, 'report-versions')),
  });
}

/** Creates a draft report version, empty or a copy of another version's columns and lines. */
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
    onSuccess: () => invalidate(lookupQueryKey(tenantId, 'report-versions')),
  });
}

/** Edits a draft or published version's default profile and defaults. */
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
    onSuccess: () => invalidate(versionKey(tenantId, versionId)),
  });
}

/** Deletes a draft version with its lines and columns. */
export function useDeleteReportVersion() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (versionId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/report-versions/{versionId}', {
          params: { path: { tenantId, versionId } },
        })
      ),
    onSuccess: () => invalidate(reportsKey(tenantId), lookupQueryKey(tenantId, 'report-versions')),
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
      return invalidate(lookupQueryKey(tenantId, 'report-columns'), linesKey(tenantId, versionId));
    },
  });
}

/** Replaces the line tree of a draft or published version; the stored lines replace the cached list. */
export function useReplaceReportLines(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (lines: ReportLineInput[]) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/report-versions/{versionId}/lines', {
          params: { path: { tenantId, versionId } },
          body: { lines },
        })
      ),
    onSuccess: lines => {
      queryClient.setQueryData(linesKey(tenantId, versionId), lines);
    },
  });
}
