import { createSnapshotChangeSet } from '@/lib/api/changes'
import type {
  ApiResult,
  AttributionDriver,
  CalcModel,
  DeleteCheck,
  Id,
  ModelDocument,
  ModelFormulaCheck,
  ModelFormulaLanguage,
  ModelListItem,
  ModelsApi,
  Problem,
} from '../contract'
import { createSeed, type Db, type StoredModel } from './data'
import { formulaFindings, violationsOf } from './rules'
import { attributionOf, defaultDrivers, metricsOf, paramRefsOf, readsOf } from '../metrics'

const wait = () => new Promise((r) => setTimeout(r, 90))
const clone = <T,>(v: T): T => structuredClone(v)

const ok = <T,>(data: T): ApiResult<T> => ({ ok: true, data: clone(data) })
const fail = (status: Problem['status'], code: string, title: string, extra: Partial<Problem> = {}): ApiResult<never> => ({
  ok: false,
  problem: { type: `urn:qe:problem:${code}`, title, status, errors: {}, ...extra },
})
const notFound = () => fail(404, 'not-found', 'Calculation model not found')
const invalid = (path: string, message: string) => fail(400, 'invalid-input', 'The input is invalid.', { errors: { [path]: [message] } })

const LANGUAGE_RANK: Record<ModelFormulaLanguage, number> = { '1.0': 0, '1.1': 1, '1.2': 2 }

function requiredLanguage(formula: string, parameters: string[]): ModelFormulaLanguage {
  if (parameters.length || /\bYEARFRAC\s*\(/i.test(formula) || /'[^']*'/.test(formula)) return '1.2'
  return /\bPOWER\s*\(/i.test(formula) ? '1.1' : '1.0'
}

/** The drivers in effect per metric, as the server computes them: explicit, else the defaults of the expression in effect. */
function effectiveDrivers(model: StoredModel): Record<string, AttributionDriver[]> {
  const out: Record<string, AttributionDriver[]> = {}
  for (const metric of metricsOf(model)) {
    const explicit = attributionOf(model, metric.key)
    const expression = explicit?.expression ?? (metric.type === 'measure' ? metric.formula : null)
    if (explicit?.drivers.length) out[metric.key] = explicit.drivers
    else if (expression) out[metric.key] = defaultDrivers(model, expression)
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

/** Saves `input` into `list`: an edit (with `originalKey`) names a stored item and keeps its key; a new item needs a key free in `namespace`. */
function saveKeyed<T extends { key: string }>(list: T[], input: T & { originalKey?: string }, namespace: string[], noun: string): ApiResult<T> {
  const { originalKey, ...item } = input
  const row = item as unknown as T
  if (originalKey === undefined) {
    if (namespace.includes(row.key)) return invalid('key', `The model already has a ${noun} with key '${row.key}'.`)
    list.push(row)
    return ok(row)
  }
  const index = list.findIndex((x) => x.key === originalKey)
  if (index < 0) return invalid('originalKey', `The model has no ${noun} '${originalKey}'.`)
  if (row.key !== originalKey) return invalid('key', `'${originalKey}' keeps its key; keys are fixed once saved.`)
  list[index] = row
  return ok(row)
}

/** Removes the item with `key` from `list`, or refuses a key the model lacks. */
function removeKeyed<T>(list: T[], keyOf: (item: T) => string, key: string, path: string, noun: string): ApiResult<null> {
  const index = list.findIndex((x) => keyOf(x) === key)
  if (index < 0) return invalid(path, `The model has no ${noun} '${key}'.`)
  list.splice(index, 1)
  return ok(null)
}

const metricKeys = (model: StoredModel) => metricsOf(model).map((m) => m.key)
const withoutAttribution = (model: StoredModel, metricKey: string) => {
  model.attributions = model.attributions.filter((a) => a.metricKey !== metricKey)
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
      message: `Calculation model '${find(id)!.key}' cannot be deleted: ${uses.map((r) => `report '${r.reportKey}' reads it`).join('; ')}.`,
      reasons: uses.map((r) => ({ code: 'referenced_by_report' as const, message: `report '${r.reportKey}' reads it` })),
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
      const model = find(id)!
      const declared = model.parameters.map((p) => p.key)
      const parameters = paramRefsOf(formula).filter((key) => declared.includes(key))
      const requiresLanguage = requiredLanguage(formula, parameters)
      const errors = formulaFindings(model, formula, selfKey).map(({ message, start, end }) => ({ message, start, end }))
      if (LANGUAGE_RANK[requiresLanguage] > LANGUAGE_RANK[model.formulaLanguage])
        errors.push({ message: `The formula needs formula language ${requiresLanguage}.`, start: 0, end: formula.length })
      return { errors, reads: readsOf(formula), parameters, requiresLanguage }
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
    saveVariable: (id, input) => edit(id, (m) => saveKeyed(m.variables, input, metricKeys(m), 'variable or calculated measure')),
    saveMeasure: (id, input) => edit(id, (m) => saveKeyed(m.measures, input, metricKeys(m), 'variable or calculated measure')),
    saveParameter: (id, input) => edit(id, (m) => saveKeyed(m.parameters, input, m.parameters.map((p) => p.key), 'parameter')),
    deleteVariable: (id, key) =>
      edit(id, (m) => {
        withoutAttribution(m, key)
        return removeKeyed(m.variables, (v) => v.key, key, 'key', 'variable')
      }),
    deleteMeasure: (id, key) =>
      edit(id, (m) => {
        withoutAttribution(m, key)
        return removeKeyed(m.measures, (x) => x.key, key, 'key', 'calculated measure')
      }),
    deleteParameter: (id, key) => edit(id, (m) => removeKeyed(m.parameters, (p) => p.key, key, 'key', 'parameter')),
    setAttribution: (id, input) =>
      edit(id, (m) => {
        withoutAttribution(m, input.metricKey)
        m.attributions.push(input)
        return ok(input)
      }),
    clearAttribution: (id, metricKey) => edit(id, (m) => removeKeyed(m.attributions, (a) => a.metricKey, metricKey, 'metricKey', 'attribution of a metric')),

    async createModel(input) {
      await wait()
      if (db.models.some((m) => m.key === input.key)) return fail(409, 'conflict', 'Key taken', { detail: `A calculation model with key '${input.key}' already exists.` })
      const model: StoredModel = { ...input, id: `model-${crypto.randomUUID()}`, calendarKey: 'gregorian', isEnabled: false, variables: [], measures: [], parameters: [], attributions: [] }
      db.models.push(model)
      return ok(settingsOf(model))
    },
    async deleteModel(id) {
      await wait()
      const check = checkDelete(id)
      if (!check.allowed) return fail(409, 'delete-blocked', 'Delete blocked', { detail: check.message!, violations: check.reasons })
      db.models = db.models.filter((m) => m.id !== id)
      return ok(null)
    },
  }
}
