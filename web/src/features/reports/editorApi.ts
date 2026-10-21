import { useMutation, useQuery } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { queryClient } from '@/api/queryClient';
import { lookupQueryKey } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import { invalidateVersionContent } from '@/shared/versionContent';
import { reportEditorKey, reportKey, reportVariableScopesKey } from './queryKeys';
import type { EditorView, NewReportSourceInput, ReportRowInput, ReportSourceInput, SourceItemInput, SourceVariableInput } from './types';

// Query keys start with [resource, tenantId] so invalidateTenantQueries reaches them.
function unmappedKey(tenantId: string, reportId: number, sourceId?: number) {
  return sourceId === undefined
    ? (['report-unmapped', tenantId, reportId] as const)
    : (['report-unmapped', tenantId, reportId, sourceId] as const);
}

/** Caches the editor view a write answers with and refreshes the unmapped items it may have changed. */
function storeView(tenantId: string, view: EditorView) {
  const reportId = view.report.reportId;
  queryClient.setQueryData(reportEditorKey(tenantId, reportId), view);
  return queryClient.invalidateQueries({ queryKey: unmappedKey(tenantId, reportId) });
}

/**
 * Caches the editor view a source write answers with, and refreshes what the write changed besides: the report's profiles,
 * whose picks a source write completes or removes, the report's default profile, and the bridges a bridge source owns.
 */
function storeSourceWrite(tenantId: string, view: EditorView) {
  const reportId = view.report.reportId;
  return Promise.all([
    storeView(tenantId, view),
    invalidateVersionContent(tenantId, 'reports', reportId),
    queryClient.invalidateQueries({ queryKey: reportKey(tenantId, reportId) }),
    queryClient.invalidateQueries({ queryKey: ['bridges', tenantId] }),
    queryClient.invalidateQueries({ queryKey: lookupQueryKey(tenantId, 'bridges') }),
  ]);
}

/** A report's editor view: its rows with the items each bridge source maps to them, and its sources with their variables. */
export function useReportEditor(reportId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: reportEditorKey(tenantId, reportId),
    queryFn: async () =>
      unwrap(await apiClient.GET('/api/tenants/{tenantId}/reports/{reportId}/editor', { params: { path: { tenantId, reportId } } })),
  });
}

/** Replaces a report's row tree; a removed row takes its items, member option and scopes with it. */
export function useReplaceReportRows(reportId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (rows: ReportRowInput[]) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/reports/{reportId}/rows', { params: { path: { tenantId, reportId } }, body: { rows } })
      ),
    onSuccess: view =>
      Promise.all([
        storeView(tenantId, view),
        queryClient.invalidateQueries({ queryKey: reportVariableScopesKey(tenantId, reportId) }),
        queryClient.invalidateQueries({ queryKey: lookupQueryKey(tenantId, 'members') }),
      ]),
  });
}

/** Creates a source: a bridge source with its own published bridge, or a direct source by its field. */
export function useCreateReportSource(reportId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: NewReportSourceInput) =>
      unwrap(
        await apiClient.POST('/api/tenants/{tenantId}/reports/{reportId}/sources', { params: { path: { tenantId, reportId } }, body })
      ),
    onSuccess: view => storeSourceWrite(tenantId, view),
  });
}

/** Edits a source's label and time field. */
export function useUpdateReportSource(reportId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async ({ sourceId, body }: { sourceId: number; body: ReportSourceInput }) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/reports/{reportId}/sources/{sourceId}', {
          params: { path: { tenantId, reportId, sourceId } },
          body,
        })
      ),
    onSuccess: view => storeSourceWrite(tenantId, view),
  });
}

/** Deletes a source with its variables, the profile picks that name it and a bridge source's bridge. */
export function useDeleteReportSource(reportId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (sourceId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/reports/{reportId}/sources/{sourceId}', {
          params: { path: { tenantId, reportId, sourceId } },
        })
      ),
    onSuccess: view => storeSourceWrite(tenantId, view),
  });
}

/** Replaces the variables a source supplies; profiles that pick no source for one of them pick this source. */
export function useReplaceSourceVariables(reportId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async ({ sourceId, variables }: { sourceId: number; variables: SourceVariableInput[] }) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/reports/{reportId}/sources/{sourceId}/variables', {
          params: { path: { tenantId, reportId, sourceId } },
          body: { variables },
        })
      ),
    onSuccess: view => storeSourceWrite(tenantId, view),
  });
}

/** Replaces the items a bridge source maps to one data row; an item listed from another row moves here. */
export function useReplaceSourceItems(reportId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async ({ sourceId, rowKey, items }: { sourceId: number; rowKey: string; items: SourceItemInput[] }) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/reports/{reportId}/sources/{sourceId}/items', {
          params: { path: { tenantId, reportId, sourceId } },
          body: { rowKey, items },
        })
      ),
    onSuccess: view => storeView(tenantId, view),
  });
}

/** The items of a bridge source that map to no row. */
export function useUnmappedItems(reportId: number, sourceId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: unmappedKey(tenantId, reportId, sourceId),
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/reports/{reportId}/sources/{sourceId}/unmapped', {
          params: { path: { tenantId, reportId, sourceId } },
        })
      ),
  });
}
