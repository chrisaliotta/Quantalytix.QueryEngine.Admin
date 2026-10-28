import type { Problem } from '@/lib/api/contract'
import type {
  Attribution as ApiAttribution,
  ModelDocument,
  ModelListItem,
  ModelMeasure as ApiMeasure,
  ModelOperation,
  ModelParameter as ApiParameter,
  ModelVariable as ApiVariable,
} from '@/lib/models/contract'
import { refusalOf } from '@/lib/reports/client'
import type { DisplayFormat } from '@/lib/report/types'
import type { Attribution, CalcModel, Driver, ModelMeasure, ModelParameter, ModelVariable, ReportRef, Rounding, RuleViolation } from './types'

/**
 * Translation between the editor's `CalcModel` and the API's `ModelDocument` (blueprint 15). An item the user did not touch keeps
 * the stored values the editor cannot show (a null precision, scale or rounding mode, the output data type).
 */

const driverOf = (d: { key: string; displayName: string; inputs: string[] }): Driver => ({ key: d.key, name: d.displayName, inputs: d.inputs })

/** An attribution without drivers uses the defaults of its expression, which the server reports as the metric's effective drivers. */
function attributionOf(doc: ModelDocument, metricKey: string): { attribution?: Attribution } {
  const stored = doc.attributions.find((a) => a.metricKey === metricKey)
  if (!stored) return {}
  const useDefault = stored.drivers.length === 0
  return {
    attribution: {
      ...(stored.expression ? { expression: stored.expression } : {}),
      ...(stored.weightVariableKey ? { weight: stored.weightVariableKey } : {}),
      useDefault,
      drivers: (useDefault ? (doc.effectiveDrivers[metricKey] ?? []) : stored.drivers).map(driverOf),
    },
  }
}

export function modelFromDocument(doc: ModelDocument): CalcModel {
  return {
    key: doc.key,
    name: doc.displayName,
    currency: doc.currencyCode,
    calendar: doc.calendarKey,
    language: doc.formulaLanguage,
    method: doc.attributionMethod,
    enabled: doc.isEnabled,
    parameters: doc.parameters.map((p) => ({
      key: p.key,
      name: p.displayName,
      type: p.valueType,
      allowedValues: p.allowedValues ?? [],
      defaultNumber: p.defaultNumber,
      defaultText: p.defaultText,
      description: p.description ?? '',
    })),
    variables: doc.variables.map((v) => ({
      key: v.key,
      name: v.displayName,
      format: v.displayFormat as DisplayFormat,
      unit: v.unitKey ?? '',
      missing: v.missingBehavior,
      ...attributionOf(doc, v.key),
    })),
    measures: doc.measures.map((m) => ({
      key: m.key,
      name: m.displayName,
      formula: m.formula,
      precision: m.outputPrecision ?? 0,
      scale: m.outputScale ?? 0,
      rounding: (m.roundingMode ?? 'half_even') as Rounding,
      unit: m.unitKey ?? '',
      format: m.displayFormat as DisplayFormat,
      ...attributionOf(doc, m.key),
    })),
  }
}

/** The library rows of the models screen: the reports that read each model. */
export function reportRefsOf(models: ModelListItem[]): ReportRef[] {
  return models.flatMap((m) =>
    m.usedBy.map((r) => ({ key: r.reportKey, name: r.displayName, type: '', model: m.key, layout: 'statement' as const, defaultProfile: '', enabled: r.isEnabled })),
  )
}

const same = (a: unknown, b: unknown) => JSON.stringify(a) === JSON.stringify(b)
const nullIfEmpty = (s: string) => (s === '' ? null : s)

/** `next` where it differs from `prev`, else the stored value. */
const kept = <T, S>(prev: T | undefined, next: T, stored: S | undefined, toApi: (v: T) => S): S =>
  prev !== undefined && stored !== undefined && same(prev, next) ? stored : toApi(next)

function variableInput(v: ModelVariable): ApiVariable {
  return { key: v.key, displayName: v.name, displayFormat: v.format, unitKey: nullIfEmpty(v.unit), missingBehavior: v.missing }
}

function measureInput(m: ModelMeasure, prev: ModelMeasure | undefined, stored: ApiMeasure | undefined): ApiMeasure {
  return {
    key: m.key,
    displayName: m.name,
    formula: m.formula,
    outputDataType: stored?.outputDataType ?? 'decimal',
    outputPrecision: kept(prev?.precision, m.precision, stored?.outputPrecision, (v) => v),
    outputScale: kept(prev?.scale, m.scale, stored?.outputScale, (v) => v),
    roundingMode: kept(prev?.rounding, m.rounding, stored?.roundingMode, (v) => v),
    unitKey: nullIfEmpty(m.unit),
    displayFormat: m.format,
  }
}

function parameterInput(p: ModelParameter): ApiParameter {
  return {
    key: p.key,
    displayName: p.name,
    valueType: p.type,
    allowedValues: p.allowedValues.length ? p.allowedValues : null,
    defaultNumber: p.defaultNumber,
    defaultText: p.defaultText,
    description: nullIfEmpty(p.description),
  }
}

function attributionInput(metricKey: string, a: Attribution): ApiAttribution {
  return {
    metricKey,
    expression: a.expression ?? null,
    weightVariableKey: a.weight ?? null,
    drivers: a.useDefault ? [] : a.drivers.map((d) => ({ key: d.key, displayName: d.name, inputs: d.inputs })),
  }
}

/** Saves of new or changed items and deletes of removed ones, matched by key (keys are fixed). */
function itemOperations<T extends { key: string }>(
  prev: T[],
  next: T[],
  save: (item: T, before: T | undefined) => ModelOperation,
  remove: (key: string) => ModelOperation,
): { saves: ModelOperation[]; deletes: ModelOperation[] } {
  const before = new Map(prev.map((i) => [i.key, i]))
  const after = new Set(next.map((i) => i.key))
  const strip = (i: T) => ({ ...i, attribution: undefined })
  return {
    saves: next.filter((i) => !before.has(i.key) || !same(strip(before.get(i.key)!), strip(i))).map((i) => save(i, before.get(i.key))),
    deletes: prev.filter((i) => !after.has(i.key)).map((i) => remove(i.key)),
  }
}

/**
 * The change set that turns the saved model into `next`. Settings come first, so a newer formula language is in place before the
 * formulas that need it; deletes come last, after the formulas that stopped reading what they delete.
 */
export function modelOperations(stored: ModelDocument, prev: CalcModel, next: CalcModel): ModelOperation[] {
  const patch = Object.fromEntries(
    (
      [
        ['displayName', prev.name, next.name],
        ['currencyCode', prev.currency, next.currency],
        ['calendarKey', prev.calendar, next.calendar],
        ['formulaLanguage', prev.language, next.language],
        ['attributionMethod', prev.method, next.method],
        ['isEnabled', prev.enabled, next.enabled],
      ] as const
    )
      .filter(([, a, b]) => a !== b)
      .map(([field, , value]) => [field, value]),
  )
  const parameters = itemOperations(
    prev.parameters,
    next.parameters,
    (p, before) => ({ op: 'saveParameter', input: parameterInput(p), ...(before ? { originalKey: p.key } : {}) }),
    (key) => ({ op: 'deleteParameter', key }),
  )
  const variables = itemOperations(
    prev.variables,
    next.variables,
    (v, before) => ({ op: 'saveVariable', input: variableInput(v), ...(before ? { originalKey: v.key } : {}) }),
    (key) => ({ op: 'deleteVariable', key }),
  )
  const measures = itemOperations(
    prev.measures,
    next.measures,
    (m, before) => ({ op: 'saveMeasure', input: measureInput(m, before, stored.measures.find((s) => s.key === m.key)), ...(before ? { originalKey: m.key } : {}) }),
    (key) => ({ op: 'deleteMeasure', key }),
  )
  const prevAttribution = new Map([...prev.variables, ...prev.measures].map((m) => [m.key, m.attribution]))
  const attributions = [...next.variables, ...next.measures].flatMap((m): ModelOperation[] => {
    const before = prevAttribution.get(m.key)
    if (same(before, m.attribution)) return []
    if (m.attribution) return [{ op: 'setAttribution', input: attributionInput(m.key, m.attribution) }]
    return prevAttribution.has(m.key) ? [{ op: 'clearAttribution', metricKey: m.key }] : []
  })
  return [
    ...(Object.keys(patch).length ? [{ op: 'updateModel' as const, patch }] : []),
    ...parameters.saves,
    ...variables.saves,
    ...measures.saves,
    ...attributions,
    ...measures.deletes,
    ...variables.deletes,
    ...parameters.deletes,
  ]
}

/** A server refusal in the shape the model screens show. */
export function violationsOf(problem: Problem): RuleViolation[] {
  return refusalOf(problem).map((v) => ({ rule: v.code, message: v.message }))
}
