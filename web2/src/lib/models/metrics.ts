/**
 * Reading helpers over a model's content for the model screens and the mock adapter. They describe a formula (what it
 * reads, its default drivers); whether the model accepts an edit is the server's answer (`checkFormula`, refusals).
 */
import type { Attribution, AttributionDriver, ModelDocument, ModelMeasure, ModelVariable } from './contract'

export const DAY_PARAMS = ['@periodDays', '@yearDays'] as const
/** Context parameters every formula reads; no model declares a parameter by either name. */
export const RESERVED_PARAMS = ['periodDays', 'yearDays']
export const FUNCTIONS = ['CASE', 'COALESCE', 'NULLIF', 'ABS', 'POWER', 'YEARFRAC'] as const
/** Shapley attribution evaluates every subset of a metric's drivers. */
export const MAX_DRIVERS = 8
export const DAYS_DRIVER = 'days'

export const DAY_COUNTS = [
  { value: 'actual_actual', label: 'Actual/Actual', description: 'Actual days over 365 or 366, by the start date’s year' },
  { value: 'actual_365', label: 'Actual/365', description: 'Actual days over 365' },
  { value: 'actual_360', label: 'Actual/360', description: 'Actual days over 360' },
  { value: 'thirty_360_us', label: '30/360 US', description: '30-day months over 360, US rules' },
  { value: 'thirty_e_360', label: '30E/360', description: '30-day months over 360, Eurobond' },
] as const

export function dayCountLabel(value: string) {
  return DAY_COUNTS.find((d) => d.value === value)?.label ?? value
}

export type ModelMetric = (ModelVariable & { type: 'variable' }) | (ModelMeasure & { type: 'measure' })

type ModelContent = Pick<ModelDocument, 'variables' | 'measures'>

export function metricsOf(model: ModelContent): ModelMetric[] {
  return [...model.variables.map((v) => ({ ...v, type: 'variable' as const })), ...model.measures.map((m) => ({ ...m, type: 'measure' as const }))]
}

export function findMetric(model: ModelContent, key: string) {
  return metricsOf(model).find((m) => m.key === key)
}

export function attributionOf(model: Pick<ModelDocument, 'attributions'>, metricKey: string): Attribution | undefined {
  return model.attributions.find((a) => a.metricKey === metricKey)
}

/** Metric keys a formula reads, in order of first use. */
export function readsOf(formula: string) {
  return [...new Set([...formula.matchAll(/\[m\.([A-Za-z0-9_]+)\]/g)].map((m) => m[1]))]
}

/** Declared-parameter keys a formula reads, in order of first use; never the context parameters. */
export function paramRefsOf(formula: string) {
  return [...new Set([...formula.matchAll(/@([A-Za-z][A-Za-z0-9_]*)/g)].map((m) => m[1]))].filter((key) => !RESERVED_PARAMS.includes(key))
}

/**
 * The inputs drivers switch: the metrics read, then both days inputs when the expression reads either or calls
 * YEARFRAC, the ones it reads first in reading order.
 */
export function inputsOf(expression: string) {
  const days = [...new Set([...expression.matchAll(/@(periodDays|yearDays)\b/g)].map((m) => `@${m[1]}`))]
  const readsDays = days.length > 0 || /\bYEARFRAC\s*\(/i.test(expression)
  return [...readsOf(expression), ...(readsDays ? [...new Set([...days, ...DAY_PARAMS])] : [])]
}

/** The engine's default drivers: one per metric read, keyed and named as the metric, then one `days` driver. */
export function defaultDrivers(model: ModelContent, expression: string): AttributionDriver[] {
  const inputs = inputsOf(expression)
  const metrics = inputs
    .filter((input) => !input.startsWith('@'))
    .map((key) => ({ key, displayName: findMetric(model, key)?.displayName ?? key, inputs: [key] }))
  const days = inputs.some((input) => input.startsWith('@')) ? [{ key: DAYS_DRIVER, displayName: 'Days', inputs: [...DAY_PARAMS] }] : []
  return [...metrics, ...days]
}
