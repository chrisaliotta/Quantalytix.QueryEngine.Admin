import { createContext, useCallback, useContext, useState, type ReactNode } from 'react'
import useSWR, { useSWRConfig } from 'swr'
import { unwrap } from '@/lib/api/contract'
import { apiMode, tenantHttp } from '@/lib/api/session'
import { ChangeScopeProvider, useStagedApi } from '@/components/shell/changes'
import type { Problem } from '@/lib/api/contract'
import { REPORT_COMMANDS, REPORT_EDITS, type ApiResult, type Id, type ReportsApi, type Violation } from './contract'
import { createHttpReportsApi } from './http/adapter'
import { createMockReportsApi } from './mock/adapter'

let shared: ReportsApi | null = null

/** The page's one adapter, the HTTP adapter in http mode or else the in-memory mock, so every reader sees the same reports. */
export function sharedReportsApi(): ReportsApi {
  shared ??= apiMode === 'http' ? createHttpReportsApi(tenantHttp) : createMockReportsApi()
  return shared
}

const ApiContext = createContext<ReportsApi | null>(null)

/** Supplies the adapter: an explicit `api`, else the shared one. */
export function ReportsApiProvider({ children, api }: { children: ReactNode; api?: ReportsApi }) {
  const [instance] = useState(() => api ?? sharedReportsApi())
  return <ApiContext.Provider value={instance}>{children}</ApiContext.Provider>
}

export function useReportsApi() {
  const api = useContext(ApiContext)
  if (!api) throw new Error('useReportsApi must be used inside ReportsApiProvider')
  return api
}

/**
 * Batches every edit inside one report: rows, sources, profiles, dimensions, scopes, columns, styles and settings stay pending
 * until Save. Children keep calling `useReportsApi()`; they receive the staged port.
 */
export function ReportEditScope({ reportId, label, children }: { reportId: Id; label: string; children: ReactNode }) {
  const base = useReportsApi()
  const refresh = useRefreshReports()
  const { api, scope } = useStagedApi(base, { id: `report:${reportId}`, label, edits: REPORT_EDITS, commands: REPORT_COMMANDS, onSettled: refresh })
  return (
    <ApiContext.Provider value={api}>
      <ChangeScopeProvider scope={scope}>{children}</ChangeScopeProvider>
    </ApiContext.Provider>
  )
}

const OPTS = { revalidateOnFocus: false }

export const reportKeys = {
  list: ['reports', 'list'] as const,
  report: (id: Id) => ['reports', 'report', id] as const,
  diagnostics: (id: Id) => ['reports', 'diagnostics', id] as const,
  unmapped: (id: Id, sourceKey: string) => ['reports', 'unmapped', id, sourceKey] as const,
}

export function useReportList() {
  const api = useReportsApi()
  return useSWR(reportKeys.list, () => api.listReports(), OPTS)
}

export function useReportDocument(id: Id | null) {
  const api = useReportsApi()
  return useSWR(id ? reportKeys.report(id) : null, () => unwrap(api.getReport(id!)), { ...OPTS, keepPreviousData: true })
}

/** The open report's readiness, coverage and resolution: the pending state's while edits are staged (OI-05). */
export function useReportDiagnostics(id: Id) {
  const api = useReportsApi()
  return useSWR(reportKeys.diagnostics(id), () => api.getDiagnostics(id), { ...OPTS, keepPreviousData: true })
}

export function useUnmappedItems(id: Id, sourceKey: string | null) {
  const api = useReportsApi()
  return useSWR(sourceKey ? reportKeys.unmapped(id, sourceKey) : null, () => api.getUnmappedItems(id, sourceKey!), OPTS)
}

/** The library's report with a key; undefined while the list loads or when no report has it. */
export function useReportByKey(key: string) {
  const { data } = useReportList()
  return data?.find((report) => report.key === key)
}

/** Revalidates everything a report write can change. */
export function useRefreshReports() {
  const { mutate } = useSWRConfig()
  return useCallback(() => mutate((key) => Array.isArray(key) && key[0] === 'reports'), [mutate])
}

/** What a refused call shows: the rules it breaks, else each input error, else its summary. */
export function refusalOf(problem: Problem): Violation[] {
  if (problem.violations?.length) return problem.violations
  const errors = Object.entries(problem.errors).flatMap(([path, messages]) => messages.map((message) => ({ code: path || 'invalid_input', message })))
  return errors.length ? errors : [{ code: 'refused', message: problem.detail ?? problem.title }]
}

export type RunReportEdit = (call: (api: ReportsApi) => Promise<ApiResult<unknown>>) => Promise<Violation[] | null>

/** Runs a port call against the surrounding (staged) `ReportsApi`; refreshes on success, or answers the server's refusal. */
export function useReportEdit(): RunReportEdit {
  const api = useReportsApi()
  const refresh = useRefreshReports()
  return useCallback(
    async (call) => {
      const result = await call(api)
      if (!result.ok) return refusalOf(result.problem)
      await refresh()
      return null
    },
    [api, refresh],
  )
}
