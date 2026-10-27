import { createSnapshotChangeSet } from '@/lib/api/changes'
import type {
  ApiResult,
  AttributionDriver,
  CalcModel,
  DeleteCheck,
  Id,
  ModelDocument,
  ModelFormulaCheck,
  ModelFormulaError,
  ModelFormulaLanguage,
  ModelListItem,
  ModelRuleCode,
  ModelsApi,
  Problem,
  Violation,
} from '../contract'
import { createSeed, type Db, type StoredModel } from './data'

const wait = () => new Promise((r) => setTimeout(r, 90))
const clone = <T,>(v: T): T => structuredClone(v)

const ok = <T,>(data: T): ApiResult<T> => ({ ok: true, data: clone(data) })
const fail = (status: Problem['status'], code: string, title: string, extra: Partial<Problem> = {}): ApiResult<never> => ({
  ok: false,
  problem: { type: `urn:qe:problem:${code}`, title, status, errors: {}, ...extra },
})
const notFound = () => fail(404, 'not-found', 'Calculation model not found')
const invalid = (path: string, message: string) => fail(400, 'invalid-input', 'The input is invalid.', { errors: { [path]: [message] } })

const METRIC_REF = /\[m\.([A-Za-z0-9_]+)\]/g
const PARAM_REF = /@([A-Za-z][A-Za-z0-9_]*)/g
const DAY_PARAMS = ['periodDays', 'yearDays']
const LANGUAGE_RANK: Record<ModelFormulaLanguage, number> = { '1.0': 0, '1.1': 1, '1.2': 2 }

interface Finding extends ModelFormulaError {
  code: ModelRuleCode
}

const metricKeys = (model: StoredModel) => [...model.variables, ...model.measures].map((m) => m.key)
const unique = (values: string[]) => [...new Set(values)]

function requiredLanguage(formula: string, parameters: string[]): ModelFormulaLanguage {
  if (parameters.length || /\bYEARFRAC\s*\(/i.test(formula) || /'[^']*'/.test(formula)) return '1.2'
  return /\bPOWER\s*\(/i.test(formula) ? '1.1' : '1.0'
}

/** A simplified stand-in for the server's ExpressionParser-based check: references, self-reads and language only. */
function analyze(model: StoredModel, formula: string, selfKey: string | null) {
  const metrics = metricKeys(model)
  const declared = model.parameters.map((p) => p.key)
  const findings: Finding[] = []
  const metricRefs = [...formula.matchAll(METRIC_REF)]
  const paramRefs = [...formula.matchAll(PARAM_REF)].filter((m) => !DAY_PARAMS.includes(m[1]))
  for (const m of metricRefs) {
    const at = { start: m.index, end: m.index + m[0].length }
    if (m[1] === selfKey) findings.push({ code: 'formulas_acyclic', message: 'A metric cannot read itself.', ...at })
    else if (!metrics.includes(m[1])) findings.push({ code: 'formula_references_resolve', message: `[m.${m[1]}] is no variable or calculated measure of the model.`, ...at })
  }
  for (const m of paramRefs) {
    if (!declared.includes(m[1])) findings.push({ code: 'formula_parameters_resolve', message: `@${m[1]} is no declared parameter of the model.`, start: m.index, end: m.index + m[0].length })
  }
  const reads = unique(metricRefs.map((m) => m[1]))
  const parameters = unique(paramRefs.map((m) => m[1]))
  const requiresLanguage = requiredLanguage(formula, parameters)
  if (LANGUAGE_RANK[requiresLanguage] > LANGUAGE_RANK[model.formulaLanguage]) {
    findings.push({ code: 'formulas_fit_language', message: `The formula needs formula language ${requiresLanguage}.`, start: 0, end: formula.length })
  }
  return { findings, reads, parameters, requiresLanguage }
}

function defaultDrivers(model: StoredModel, expression: string): AttributionDriver[] {
  const metrics = [...model.variables, ...model.measures]
  const drivers = analyze(model, expression, null).reads.map((key) => ({ key: key.toLowerCase(), displayName: metrics.find((m) => m.key === key)?.displayName ?? key, inputs: [key] }))
  const days = /\bYEARFRAC\s*\(/i.test(expression) ? DAY_PARAMS : DAY_PARAMS.filter((p) => expression.includes(`@${p}`))
  if (days.length) drivers.push({ key: 'days', displayName: 'Days', inputs: days.map((p) => `@${p}`) })
  return drivers
}

function effectiveDrivers(model: StoredModel): Record<string, AttributionDriver[]> {
  const out: Record<string, AttributionDriver[]> = {}
  for (const metric of [...model.variables, ...model.measures]) {
    const explicit = model.attributions.find((a) => a.metricKey === metric.key)
    const expression = explicit?.expression ?? ('formula' in metric ? metric.formula : null)
    if (explicit?.drivers.length) out[metric.key] = explicit.drivers
    else if (expression) out[metric.key] = defaultDrivers(model, expression)
  }
  return out
}

/** The model rules the mock gate enforces; the server also re-runs every enabled report's rules. */
function violationsOf(model: StoredModel): Violation[] {
  const variables = model.variables.map((v) => v.key)
  const out: Violation[] = model.measures.flatMap((m) =>
    analyze(model, m.formula, m.key).findings.map((f) => ({ code: f.code, message: `Calculated measure '${m.key}': ${f.message}`, target: { area: 'measures', itemKey: m.key } })),
  )
  for (const a of model.attributions) {
    const target = { area: 'attributions', itemKey: a.metricKey }
    if (!metricKeys(model).includes(a.metricKey)) out.push({ code: 'attribution_references_resolve', message: `'${a.metricKey}' is no metric of the model.`, target })
    if (a.weightVariableKey && !variables.includes(a.weightVariableKey))
      out.push({ code: 'attribution_weight_variable', message: `The attribution of '${a.metricKey}' weighs by '${a.weightVariableKey}', which is no variable of the model.`, target })
    if (a.expression)
      for (const f of analyze(model, a.expression, a.metricKey).findings)
        out.push({ code: f.code === 'formula_references_resolve' ? 'attribution_references_resolve' : f.code, message: `The attribution of '${a.metricKey}': ${f.message}`, target })
  }
  return out
}

const settingsOf = ({ id, key, displayName, currencyCode, calendarKey, formulaLanguage, attributionMethod, isEnabled }: StoredModel): CalcModel => ({
  id,
  key,
  displayName,
  currencyCode,
  calendarKey,
  formulaLanguage,
  attributionMethod,
  isEnabled,
})

/** Saves `input` into `list`: an edit (with `originalKey`) keeps its key; a new item needs a key free in `namespace`. */
function saveKeyed<T extends { key: string }>(list: T[], input: T & { originalKey?: string }, namespace: string[]): ApiResult<T> {
  const { originalKey, ...item } = input
  const row = item as T
  if (originalKey === undefined) {
    if (namespace.includes(row.key)) return invalid('key', `'${row.key}' is already used in this model.`)
    list.push(row)
    return ok(row)
  }
  if (row.key !== originalKey) return invalid('key', 'Keys are fixed once saved.')
  list[list.findIndex((x) => x.key === originalKey)] = row
  return ok(row)
}

export function createMockModelsApi(db: Db = createSeed()): ModelsApi {
  const find = (id: Id) => db.models.find((m) => m.id === id)
  const readers = (id: Id) => db.reports.filter((r) => r.modelId === id).map((r) => r.use)

  const documentOf = (model: StoredModel): ModelDocument => ({ ...model, effectiveDrivers: effectiveDrivers(model), usedBy: readers(model.id) })

  /** Applies an edit to a copy of the model and keeps it unless it introduces a model-rule violation (OI-42). */
  async function edit<T>(id: Id, apply: (model: StoredModel) => ApiResult<T>): Promise<ApiResult<T>> {
    await wait()
    const index = db.models.findIndex((m) => m.id === id)
    if (index < 0) return notFound()
    const before = new Set(violationsOf(db.models[index]).map((v) => v.message))
    const next = clone(db.models[index])
    const result = apply(next)
    if (!result.ok) return result
    const introduced = violationsOf(next).filter((v) => !before.has(v.message))
    if (introduced.length) return fail(422, 'rules-violated', 'This change was refused', { violations: introduced })
    db.models[index] = next
    return result
  }

  function checkDelete(id: Id): DeleteCheck {
    const uses = readers(id)
    if (!uses.length) return { allowed: true, message: null, reasons: [] }
    return {
      allowed: false,
      message: `Calculation model '${find(id)!.displayName}' cannot be deleted: ${uses.length} report(s) read it.`,
      reasons: uses.map((r) => ({ code: 'model_not_in_use', message: `Report '${r.displayName}' reads this model.`, target: { area: 'usedBy', itemId: r.reportId, itemKey: r.reportKey } })),
    }
  }

  return {
    ...createSnapshotChangeSet(
      () => db.models,
      (rows) => {
        db.models = rows
      },
    ),

    async listModels() {
      await wait()
      return clone(db.models.map((m): ModelListItem => ({ ...settingsOf(m), variableCount: m.variables.length, measureCount: m.measures.length, usedBy: readers(m.id) })))
    },
    async getModel(id) {
      await wait()
      const model = find(id)
      return model ? ok(documentOf(model)) : notFound()
    },
    async checkFormula(id, formula, selfKey): Promise<ModelFormulaCheck> {
      await wait()
      const { findings, reads, parameters, requiresLanguage } = analyze(find(id)!, formula, selfKey)
      return { errors: findings.map(({ message, start, end }) => ({ message, start, end })), reads, parameters, requiresLanguage }
    },
    async checkDeleteModel(id) {
      await wait()
      return checkDelete(id)
    },

    updateModel: (id, patch) =>
      edit(id, (m) => {
        Object.assign(m, patch)
        return ok(settingsOf(m))
      }),
    saveVariable: (id, input) => edit(id, (m) => saveKeyed(m.variables, input, metricKeys(m))),
    saveMeasure: (id, input) => edit(id, (m) => saveKeyed(m.measures, input, metricKeys(m))),
    saveParameter: (id, input) => edit(id, (m) => saveKeyed(m.parameters, input, m.parameters.map((p) => p.key))),
    deleteVariable: (id, key) =>
      edit(id, (m) => {
        m.variables = m.variables.filter((v) => v.key !== key)
        m.attributions = m.attributions.filter((a) => a.metricKey !== key)
        return ok(null)
      }),
    deleteMeasure: (id, key) =>
      edit(id, (m) => {
        m.measures = m.measures.filter((x) => x.key !== key)
        m.attributions = m.attributions.filter((a) => a.metricKey !== key)
        return ok(null)
      }),
    deleteParameter: (id, key) =>
      edit(id, (m) => {
        m.parameters = m.parameters.filter((p) => p.key !== key)
        return ok(null)
      }),
    setAttribution: (id, input) =>
      edit(id, (m) => {
        m.attributions = [...m.attributions.filter((a) => a.metricKey !== input.metricKey), input]
        return ok(input)
      }),
    clearAttribution: (id, metricKey) =>
      edit(id, (m) => {
        m.attributions = m.attributions.filter((a) => a.metricKey !== metricKey)
        return ok(null)
      }),

    async createModel(input) {
      await wait()
      if (db.models.some((m) => m.key === input.key)) return fail(409, 'conflict', 'The model key is taken.', { detail: `A model '${input.key}' already exists.` })
      const model: StoredModel = { ...input, id: `model-${Date.now().toString(36)}`, calendarKey: 'gregorian', isEnabled: false, variables: [], measures: [], parameters: [], attributions: [] }
      db.models.push(model)
      return ok(settingsOf(model))
    },
    async deleteModel(id) {
      await wait()
      const check = checkDelete(id)
      if (!check.allowed) return fail(409, 'delete-blocked', check.message!, { violations: check.reasons })
      db.models = db.models.filter((m) => m.id !== id)
      return ok(null)
    },
  }
}
