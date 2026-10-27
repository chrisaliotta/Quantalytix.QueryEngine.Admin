import { initialModels, reportRefs } from '@/lib/model/data'
import type * as Prototype from '@/lib/model/types'
import type { Attribution, CalcModel, Id, ModelMeasure, ModelParameter, ModelReportUse, ModelVariable } from '../contract'

/** A model as the mock stores it: the document without its server-computed parts. */
export interface StoredModel extends CalcModel {
  variables: ModelVariable[]
  measures: ModelMeasure[]
  parameters: ModelParameter[]
  attributions: Attribution[]
}

export interface StoredReportUse {
  modelId: Id
  use: ModelReportUse
}

export interface Db {
  models: StoredModel[]
  reports: StoredReportUse[]
}

const toVariable = (v: Prototype.ModelVariable): ModelVariable => ({
  key: v.key,
  displayName: v.name,
  displayFormat: v.format,
  unitKey: v.unit || null,
  missingBehavior: v.missing,
})

const toMeasure = (m: Prototype.ModelMeasure): ModelMeasure => ({
  key: m.key,
  displayName: m.name,
  formula: m.formula,
  outputDataType: 'decimal',
  outputPrecision: m.precision,
  outputScale: m.scale,
  roundingMode: m.rounding,
  unitKey: m.unit || null,
  displayFormat: m.format,
})

const toParameter = (p: Prototype.ModelParameter): ModelParameter => ({
  key: p.key,
  displayName: p.name,
  valueType: p.type,
  allowedValues: p.allowedValues.length ? p.allowedValues : null,
  defaultNumber: p.defaultNumber,
  defaultText: p.defaultText,
  description: p.description || null,
})

/** Only explicit attributions are rows; `useDefault` ones are computed by `effectiveDrivers`. */
function toAttributions(metrics: (Prototype.ModelVariable | Prototype.ModelMeasure)[]): Attribution[] {
  return metrics.flatMap(({ key, attribution: a }) =>
    a && !a.useDefault
      ? [{ metricKey: key, expression: a.expression ?? null, weightVariableKey: a.weight ?? null, drivers: a.drivers.map((d) => ({ key: d.key, displayName: d.name, inputs: d.inputs })) }]
      : [],
  )
}

export function createSeed(): Db {
  const models: StoredModel[] = initialModels.map((m, i) => ({
    id: `model-${i + 1}`,
    key: m.key,
    displayName: m.name,
    currencyCode: m.currency,
    calendarKey: m.calendar,
    formulaLanguage: m.language,
    attributionMethod: m.method,
    isEnabled: m.enabled,
    variables: m.variables.map(toVariable),
    measures: m.measures.map(toMeasure),
    parameters: m.parameters.map(toParameter),
    attributions: toAttributions([...m.variables, ...m.measures]),
  }))
  const reports: StoredReportUse[] = reportRefs.map((r, i) => ({
    modelId: models.find((m) => m.key === r.model)!.id,
    use: { reportId: `report-${i + 1}`, reportKey: r.key, displayName: r.name, isEnabled: r.enabled },
  }))
  return structuredClone({ models, reports })
}
