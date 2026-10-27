import { createSnapshotChangeSet, refill } from '@/lib/api/changes'
import type { Problem } from '@/lib/api/contract'
import type {
  ApiResult,
  Id,
  ReportDiagnostics,
  ReportDiagnosticsStep,
  ReportDiagnosticsStepName,
  ReportCreateInput,
  ReportDocument,
  FilterNode,
  FilterOperand,
  ReportRow,
  ReportSource,
  ReportSourceCoverage,
  ReportSummary,
  ReportVariableResolution,
  ReportsApi,
  Violation,
} from '../contract'
import { createSeed, flattenItems, type Db, type ItemNode } from './data'

const LATENCY = 90
const wait = () => new Promise((r) => setTimeout(r, LATENCY))
const clone = <T,>(v: T): T => structuredClone(v)

const ok = <T,>(data: T): ApiResult<T> => ({ ok: true, data: clone(data) })
const fail = (status: Problem['status'], title: string, extra: Partial<Problem> = {}): ApiResult<never> => ({
  ok: false,
  problem: { type: 'about:blank', title, status, errors: {}, ...extra },
})
type ReportContent = Omit<ReportDocument, 'id' | 'key' | 'isEnabled' | 'modelId' | 'reportDimensionId' | 'reportDimensionVersionId'>

const violated = (violations: Violation[]) => fail(422, 'The change would break a report rule.', { type: 'urn:qe:problem:rules-violated', violations })

/**
 * In-memory `ReportsApi`. Cascades follow blueprint 16; diagnostics are simplified: coverage is computed, and the
 * steps check only the default profile, profile completeness, scenario roles and column keys.
 */
export function createMockReportsApi(db: Db = createSeed()): ReportsApi {
  const report = (id: Id) => db.reports.find((r) => r.id === id)!
  const source = (doc: ReportDocument, key: string) => doc.sources.find((s) => s.key === key)!

  function summaryOf(doc: ReportDocument): ReportSummary {
    return {
      id: doc.id,
      key: doc.key,
      displayName: doc.settings.displayName,
      reportType: doc.settings.reportType,
      modelId: doc.modelId,
      modelKey: db.models.find((m) => m.id === doc.modelId)!.key,
      isEnabled: doc.isEnabled,
      periodicity: doc.settings.periodicity,
      profileCount: doc.profiles.length,
      sourceCount: doc.sources.length,
    }
  }

  function unmapped(doc: ReportDocument, s: ReportSource): string[] {
    if (s.method === 'direct') {
      const rowKeys = new Set(doc.rows.map((r) => r.key))
      return db.fieldValues[s.datasetId][s.fieldKey!].filter((v) => !rowKeys.has(v))
    }
    const reached = new Set(Object.keys(leafFactors(s, db.items[s.roleId!])))
    return leaves(db.items[s.roleId!]).filter((k) => !reached.has(k) && !s.leftOut.includes(k))
  }

  function coverageOf(doc: ReportDocument, s: ReportSource): ReportSourceCoverage {
    const multi =
      s.method === 'bridge'
        ? Object.entries(leafFactors(s, db.items[s.roleId!]))
            .map(([itemKey, byRow]) => ({ itemKey, rows: Object.keys(byRow).filter((r) => byRow[r] !== 0), net: Object.values(byRow).reduce((a, b) => a + b, 0) }))
            .filter((m) => m.rows.length > 1)
        : []
    return { unmapped: unmapped(doc, s), multi, leftOut: s.leftOut }
  }

  function diagnose(doc: ReportDocument): ReportDiagnostics {
    const coverage = Object.fromEntries(doc.sources.map((s) => [s.key, coverageOf(doc, s)]))
    const variables = [...new Set(doc.sources.flatMap((s) => s.variables.map((v) => v.variableKey)))]
    const resolution = Object.fromEntries(
      doc.profiles.map((p) => [
        p.key,
        Object.fromEntries(
          variables.map((v): [string, ReportVariableResolution] => {
            const picked = doc.sources.find((s) => s.key === p.picks[v] && s.variables.some((sv) => sv.variableKey === v))
            return [v, { status: picked ? 'ok' : 'unresolved', sourceKey: picked?.key ?? null, rowGaps: [] }]
          }),
        ),
      ]),
    )

    const found: Partial<Record<ReportDiagnosticsStepName, Violation[]>> = {}
    const add = (step: ReportDiagnosticsStepName, v: Violation) => (found[step] ??= []).push(v)
    if (!doc.profiles.some((p) => p.key === doc.settings.defaultProfileKey && p.isEnabled)) {
      add('profiles', { code: 'default_profile_enabled', message: 'The default profile must be one of the enabled profiles.', target: { area: 'profiles' } })
    }
    for (const p of doc.profiles.filter((p) => p.isEnabled)) {
      for (const [v, r] of Object.entries(resolution[p.key])) {
        if (r.status === 'unresolved') add('profiles', { code: 'profiles_complete', message: `Profile '${p.displayName}' picks no source for ${v}.`, target: { area: 'profiles', itemKey: p.key } })
      }
    }
    const dimensionRoles = new Set(doc.calculationDimensions.map((d) => d.roleId))
    for (const sc of doc.scenarios.filter((sc) => dimensionRoles.has(sc.roleId))) {
      add('dimensions', { code: 'scenario_roles_distinct', message: 'A scenario role is also a calculation dimension.', target: { area: 'dimensions', itemId: sc.roleId } })
    }
    const columnKeys = doc.columns.map((c) => c.key)
    for (const key of new Set(columnKeys.filter((k, i) => columnKeys.indexOf(k) !== i))) {
      add('columns', { code: 'column_keys_distinct', message: `Column key '${key}' is used twice.`, target: { area: 'columns', itemKey: key } })
    }

    const anyUnmapped = Object.values(coverage).some((c) => c.unmapped.length > 0)
    const steps = (['rows', 'sources', 'variables', 'items', 'profiles', 'dimensions', 'columns'] as const).map((step): ReportDiagnosticsStep => {
      const violations = found[step] ?? []
      const status = violations.length ? 'fail' : step === 'items' && anyUnmapped ? 'warn' : 'ok'
      return { step, status, violations }
    })
    return { ready: steps.every((s) => s.status !== 'fail'), steps, coverage, resolution }
  }

  function placeRows(doc: ReportDocument, block: ReportRow[], parentKey: string | null, index: number) {
    const rows = doc.rows
    const siblings = rows.filter((r) => r.parentKey === parentKey)
    const at =
      index < siblings.length
        ? rows.indexOf(siblings[index])
        : siblings.length
          ? rows.indexOf(siblings.at(-1)!) + subtree(rows, siblings.at(-1)!.key).length
          : parentKey === null
            ? rows.length
            : rows.findIndex((r) => r.key === parentKey) + 1
    block[0].parentKey = parentKey
    rows.splice(at, 0, ...block)
  }

  function takeRows(doc: ReportDocument, key: string): ReportRow[] {
    const block = subtree(doc.rows, key)
    doc.rows.splice(doc.rows.indexOf(block[0]), block.length)
    return block
  }

  /** Without `copyFromReportId` the rows are a single root; the mock does not copy a start-from dimension. */
  function createDocument(input: ReportCreateInput, id: Id): ReportDocument {
    const content: ReportContent = input.copyFromReportId
      ? clone(report(input.copyFromReportId))
      : {
          settings: { displayName: input.displayName, reportType: input.reportType, subtotalPosition: 'above', defaultProfileKey: null, periodicity: null, layout: 'statement', defaultBreakout: [], defaultFilter: null },
          rows: [{ key: 'TOTAL', label: input.displayName, kind: 'data', parentKey: null, isVisible: true, showsValues: true, calculatedMeasureKey: null, valueColumnKey: null, styleKey: null, parameters: {} }],
          sources: [],
          profiles: [],
          calculationDimensions: [],
          scenarios: [],
          variableScopes: [],
          columns: [],
          styles: [],
        }
    for (const s of content.sources) s.bridgeId &&= `${id}_bridge_${s.key}`
    return {
      ...content,
      id,
      key: input.key,
      isEnabled: false,
      modelId: input.modelId,
      reportDimensionId: `${id}_dimension`,
      reportDimensionVersionId: `${id}_dimension_v1`,
      settings: { ...content.settings, displayName: input.displayName, reportType: input.reportType },
    }
  }

  let seq = db.reports.length
  const changes = createSnapshotChangeSet(
    () => db.reports,
    (snapshot) => refill(db.reports, snapshot),
  )

  return {
    ...changes,

    async listReports() {
      await wait()
      return clone(db.reports.map(summaryOf))
    },
    async getReport(id) {
      await wait()
      const doc = db.reports.find((r) => r.id === id)
      return doc ? ok(doc) : fail(404, 'Report not found')
    },
    async getDiagnostics(id) {
      await wait()
      return diagnose(report(id))
    },
    async getUnmappedItems(id, sourceKey) {
      await wait()
      const doc = report(id)
      return unmapped(doc, source(doc, sourceKey))
    },

    async updateSettings(reportId, patch) {
      await wait()
      const doc = report(reportId)
      Object.assign(doc.settings, patch)
      return ok(doc.settings)
    },
    async saveRow(reportId, { index, ...row }) {
      await wait()
      const doc = report(reportId)
      const block = doc.rows.some((r) => r.key === row.key) ? takeRows(doc, row.key) : [row]
      block[0] = row
      placeRows(doc, block, row.parentKey, index)
      return ok(row)
    },
    async moveRow(reportId, key, to) {
      await wait()
      const doc = report(reportId)
      placeRows(doc, takeRows(doc, key), to.parentKey, to.index)
      return ok(null)
    },
    async deleteRow(reportId, key) {
      await wait()
      const doc = report(reportId)
      const row = doc.rows.find((r) => r.key === key)!
      for (const child of doc.rows.filter((r) => r.parentKey === key)) child.parentKey = row.parentKey
      doc.rows.splice(doc.rows.indexOf(row), 1)
      for (const s of doc.sources) delete s.rules[key]
      doc.variableScopes = doc.variableScopes.filter((sc) => sc.scopeMemberKey !== key)
      return ok(null)
    },
    async saveSource(reportId, { copyFromSourceKey, ...input }) {
      await wait()
      const doc = report(reportId)
      const existing = doc.sources.find((s) => s.key === input.key)
      if (existing) return ok(Object.assign(existing, input))
      const copied = copyFromSourceKey ? clone(source(doc, copyFromSourceKey)) : { variables: [], rules: {}, leftOut: [] }
      const created: ReportSource = {
        ...input,
        bridgeId: input.method === 'bridge' ? `tmp:${crypto.randomUUID()}` : null,
        variables: copied.variables,
        rules: copied.rules,
        leftOut: copied.leftOut,
      }
      doc.sources.push(created)
      return ok(created)
    },
    async deleteSource(reportId, key) {
      await wait()
      const doc = report(reportId)
      doc.sources = doc.sources.filter((s) => s.key !== key)
      for (const p of doc.profiles) {
        for (const [variable, sourceKey] of Object.entries(p.picks)) if (sourceKey === key) delete p.picks[variable]
      }
      return ok(null)
    },
    async setSourceVariables(reportId, sourceKey, variables) {
      await wait()
      source(report(reportId), sourceKey).variables = variables
      return ok(null)
    },
    async setRowRules(reportId, sourceKey, rowKey, rules) {
      await wait()
      const s = source(report(reportId), sourceKey)
      if (rules.length) s.rules[rowKey] = rules
      else delete s.rules[rowKey]
      return ok(null)
    },
    async setLeftOut(reportId, sourceKey, itemKeys) {
      await wait()
      source(report(reportId), sourceKey).leftOut = itemKeys
      return ok(null)
    },
    async saveProfile(reportId, { originalKey, ...profile }) {
      await wait()
      const doc = report(reportId)
      if (originalKey === undefined) doc.profiles.push(profile)
      else {
        doc.profiles[doc.profiles.findIndex((p) => p.key === originalKey)] = profile
        if (doc.settings.defaultProfileKey === originalKey) doc.settings.defaultProfileKey = profile.key
      }
      return ok(profile)
    },
    async deleteProfile(reportId, key) {
      await wait()
      const doc = report(reportId)
      if (doc.settings.defaultProfileKey === key) {
        return violated([{ code: 'default_profile_enabled', message: 'The default profile cannot be deleted.', target: { area: 'profiles', itemKey: key } }])
      }
      doc.profiles = doc.profiles.filter((p) => p.key !== key)
      return ok(null)
    },
    async saveCalculationDimension(reportId, { originalKey, ...input }) {
      await wait()
      const doc = report(reportId)
      if (originalKey === undefined) doc.calculationDimensions.push(input)
      else {
        doc.calculationDimensions[doc.calculationDimensions.findIndex((d) => d.key === originalKey)] = input
        renameDimension(doc, originalKey, input.key)
      }
      return ok(input)
    },
    async deleteCalculationDimension(reportId, key) {
      await wait()
      const doc = report(reportId)
      doc.calculationDimensions = doc.calculationDimensions.filter((d) => d.key !== key)
      renameDimension(doc, key, null)
      return ok(null)
    },
    async setScenarios(reportId, roleIds) {
      await wait()
      report(reportId).scenarios = roleIds.map((roleId) => ({ roleId }))
      return ok(null)
    },
    async setVariableScopes(reportId, scopes) {
      await wait()
      report(reportId).variableScopes = scopes
      return ok(null)
    },
    async setColumns(reportId, columns) {
      await wait()
      report(reportId).columns = columns
      return ok(null)
    },
    async saveStyle(reportId, style) {
      await wait()
      const doc = report(reportId)
      const at = doc.styles.findIndex((s) => s.key === style.key)
      if (at === -1) doc.styles.push(style)
      else doc.styles[at] = style
      return ok(style)
    },
    async deleteStyle(reportId, key) {
      await wait()
      const doc = report(reportId)
      if (doc.rows.some((r) => r.styleKey === key)) {
        return violated([{ code: 'report_styles_resolve', message: 'A row still uses this style.', target: { area: 'styles', itemKey: key } }])
      }
      doc.styles = doc.styles.filter((s) => s.key !== key)
      return ok(null)
    },

    async createReport(input) {
      await wait()
      if (db.reports.some((r) => r.key === input.key)) return fail(409, 'Report key taken', { detail: `A report with key '${input.key}' already exists.` })
      const doc = createDocument(input, `report_${++seq}`)
      db.reports.push(doc)
      return ok(summaryOf(doc))
    },
    async setEnabled(id, isEnabled) {
      await wait()
      const doc = report(id)
      if (isEnabled) {
        const failing = diagnose(doc).steps.flatMap((s) => (s.status === 'fail' ? s.violations : []))
        if (failing.length) return violated(failing)
      }
      doc.isEnabled = isEnabled
      return ok(summaryOf(doc))
    },
    async deleteReport(id) {
      await wait()
      db.reports.splice(db.reports.indexOf(report(id)), 1)
      return ok(null)
    },
  }
}

/** The row and its descendants, which hierarchy order keeps contiguous. */
function subtree(rows: ReportRow[], key: string): ReportRow[] {
  const start = rows.findIndex((r) => r.key === key)
  const keys = new Set([key])
  let end = start + 1
  while (end < rows.length && keys.has(rows[end].parentKey!)) keys.add(rows[end++].key)
  return rows.slice(start, end)
}

function leaves(items: ItemNode[]): string[] {
  return flattenItems(items)
    .filter((i) => !i.children?.length)
    .map((i) => i.key)
}

/** Leaf item → row key → net factor, over a bridge source's rules. */
function leafFactors(s: ReportSource, items: ItemNode[]): Record<string, Record<string, number>> {
  const byKey = new Map(flattenItems(items).map((i) => [i.key, i]))
  const out: Record<string, Record<string, number>> = {}
  for (const [rowKey, rules] of Object.entries(s.rules)) {
    for (const rule of rules) {
      const item = byKey.get(rule.itemKey)!
      const reached = rule.selection === 'descendants' ? leaves([item]) : [item.key]
      for (const leaf of reached) {
        const byRow = (out[leaf] ??= {})
        byRow[rowKey] = (byRow[rowKey] ?? 0) + rule.factor
      }
    }
  }
  return out
}

/** Renames a calculation dimension in the default breakout and filter, or removes it there when `to` is null. */
function renameDimension(doc: ReportDocument, key: string, to: string | null) {
  const breakout = doc.settings.defaultBreakout
  doc.settings.defaultBreakout = to === null ? breakout.filter((k) => k !== key) : breakout.map((k) => (k === key ? to : k))
  doc.settings.defaultFilter = doc.settings.defaultFilter && rewriteFilter(doc.settings.defaultFilter, key, to)
}

/** Null when a removed dimension empties the node. */
function rewriteFilter(node: FilterNode, key: string, to: string | null): FilterNode | null {
  const hit = (o: FilterOperand): o is Extract<FilterOperand, { type: 'dimension' }> => o.type === 'dimension' && o.id === key
  const rename = (o: FilterOperand): FilterOperand => (hit(o) ? { ...o, id: to! } : o)
  switch (node.operator) {
    case 'and':
    case 'or': {
      const operands = node.operands.map((n) => rewriteFilter(n, key, to)).filter((n) => n !== null)
      return operands.length ? { ...node, operands } : null
    }
    case 'not': {
      const operand = rewriteFilter(node.operand, key, to)
      return operand && { ...node, operand }
    }
    case 'in':
      if (to === null && [node.left, ...node.values].some(hit)) return null
      return { ...node, left: rename(node.left), values: node.values.map(rename) }
    case 'isNull':
    case 'isNotNull':
      if (to === null && hit(node.operand)) return null
      return { ...node, operand: rename(node.operand) }
    default:
      if (to === null && (hit(node.left) || hit(node.right))) return null
      return { ...node, left: rename(node.left), right: rename(node.right) }
  }
}
