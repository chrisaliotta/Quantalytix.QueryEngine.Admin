import { createHttpChangeSet } from '@/lib/api/changes'
import { unwrap, type Problem } from '@/lib/api/contract'
import type { HttpClient } from '@/lib/api/http'
import type { components } from '@/lib/api/schema'
import type { ApiResult, Id, ReportChangeSetPreview, ReportDocument, ReportOperation, ReportsApi } from '../contract'

type Schemas = components['schemas']

const reportPath = (id: Id) => `/reports/${id}`

const INPUT_PREFIX = /^(input|patch|row|profile|style)\./

/** Form helpers read the same error keys as the mock: `key`, not `row.key`. */
function withoutInputPrefix(problem: Problem): Problem {
  const errors = Object.fromEntries(Object.entries(problem.errors).map(([path, messages]) => [path.replace(INPUT_PREFIX, ''), messages]))
  return { ...problem, errors }
}

const byKey = <T extends { key: string }>(items: T[], key: string) => items.find((item) => item.key === key)!

/**
 * `http` is a tenant client: paths are relative to `/api/tenants/{tenantId}`. Report edits stage on the report's change set,
 * whose previews carry the pending report's diagnostics; reads of the open report answer from the pending preview.
 */
export function createHttpReportsApi(http: HttpClient): ReportsApi {
  const changes = createHttpChangeSet<ReportOperation, ReportDocument>(http)
  const pending = (id: Id) => {
    const preview = changes.pending() as ReportChangeSetPreview | null
    return preview?.state.id === id ? preview : null
  }

  async function edit<T>(id: Id, op: ReportOperation, answer: (state: ReportDocument) => T): Promise<ApiResult<T>> {
    const result = await changes.stage(reportPath(id), op satisfies Schemas['ReportOperation'])
    if (!result.ok) return { ok: false, problem: withoutInputPrefix(result.problem) }
    return { ok: true, data: answer(result.data.state) }
  }

  return {
    ...changes,
    listReports: () => unwrap(http.get<Schemas['ReportSummary'][]>('/reports')),
    async getReport(id) {
      const preview = pending(id)
      if (preview) return { ok: true, data: preview.state }
      return http.get<Schemas['ReportDocument']>(reportPath(id))
    },
    async getDiagnostics(id) {
      return pending(id)?.diagnostics ?? unwrap(http.get<Schemas['ReportDiagnostics']>(`${reportPath(id)}/diagnostics`))
    },
    async getUnmappedItems(id, sourceKey) {
      return pending(id)?.diagnostics.coverage[sourceKey]?.unmapped ?? unwrap(http.get<string[]>(`${reportPath(id)}/sources/${encodeURIComponent(sourceKey)}/unmapped`))
    },

    updateSettings: (id, patch) => edit(id, { op: 'updateSettings', patch }, (s) => s.settings),
    saveRow: (id, { index, ...row }) => edit(id, { op: 'saveRow', row, index }, (s) => byKey(s.rows, row.key)),
    moveRow: (id, key, to) => edit(id, { op: 'moveRow', key, to }, () => null),
    deleteRow: (id, key) => edit(id, { op: 'deleteRow', key }, () => null),
    saveSource: (id, { copyFromSourceKey, ...input }) => edit(id, { op: 'saveSource', input, copyFromSourceKey }, (s) => byKey(s.sources, input.key)),
    deleteSource: (id, key) => edit(id, { op: 'deleteSource', key }, () => null),
    setSourceVariables: (id, sourceKey, variables) => edit(id, { op: 'setSourceVariables', sourceKey, variables }, () => null),
    setRowRules: (id, sourceKey, rowKey, rules) => edit(id, { op: 'setRowRules', sourceKey, rowKey, rules }, () => null),
    setLeftOut: (id, sourceKey, itemKeys) => edit(id, { op: 'setLeftOut', sourceKey, itemKeys }, () => null),
    saveProfile: (id, { originalKey, ...profile }) => edit(id, { op: 'saveProfile', profile, originalKey }, (s) => byKey(s.profiles, profile.key)),
    deleteProfile: (id, key) => edit(id, { op: 'deleteProfile', key }, () => null),
    saveCalculationDimension: (id, { originalKey, ...input }) =>
      edit(id, { op: 'saveCalculationDimension', input, originalKey }, (s) => byKey(s.calculationDimensions, input.key)),
    deleteCalculationDimension: (id, key) => edit(id, { op: 'deleteCalculationDimension', key }, () => null),
    setScenarios: (id, roleIds) => edit(id, { op: 'setScenarios', roleIds }, () => null),
    setVariableScopes: (id, scopes) => edit(id, { op: 'setVariableScopes', scopes }, () => null),
    setColumns: (id, columns) => edit(id, { op: 'setColumns', columns }, () => null),
    saveStyle: (id, style) => edit(id, { op: 'saveStyle', style }, (s) => byKey(s.styles, style.key)),
    deleteStyle: (id, key) => edit(id, { op: 'deleteStyle', key }, () => null),

    createReport: (input) => http.post<Schemas['ReportSummary']>('/reports', input satisfies Schemas['ReportCreateInput']),
    setEnabled: (id, isEnabled) => http.put<Schemas['ReportSummary']>(`${reportPath(id)}/enabled`, { isEnabled } satisfies Schemas['ReportEnabledInput']),
    deleteReport: (id) => http.delete(reportPath(id)),
  }
}
