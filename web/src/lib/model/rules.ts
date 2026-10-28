import type { Attribution, CalcModel, Driver, ModelMeasure, ModelVariable, RuleViolation } from './types'

export const DAY_PARAMS = ['@periodDays', '@yearDays'] as const
export const FUNCTIONS = ['CASE', 'COALESCE', 'NULLIF', 'ABS', 'POWER', 'YEARFRAC'] as const
export const MAX_DRIVERS = 8

export type Metric = (ModelVariable & { type: 'variable' }) | (ModelMeasure & { type: 'measure' })

export function metricsOf(model: CalcModel): Metric[] {
  return [
    ...model.variables.map((v) => ({ ...v, type: 'variable' as const })),
    ...model.measures.map((m) => ({ ...m, type: 'measure' as const })),
  ]
}

export function findMetric(model: CalcModel, key: string) {
  return metricsOf(model).find((m) => m.key === key)
}

/** Metric keys a formula reads, in order of first use. */
export function readsOf(formula: string) {
  const out: string[] = []
  for (const m of formula.matchAll(/\[m\.([A-Z0-9_]+)\]/g)) if (!out.includes(m[1])) out.push(m[1])
  return out
}

/** Day parameters an expression reads; YEARFRAC counts as reading the period. */
export function paramsOf(formula: string) {
  if (hasYearfrac(formula)) return [...DAY_PARAMS]
  return DAY_PARAMS.filter((p) => formula.includes(p))
}

export const RESERVED_PARAMS = ['periodDays', 'yearDays']
export const PARAM_KEY = /^[A-Za-z][A-Za-z0-9_]*$/

export const DAY_COUNTS = [
  { value: 'actual_actual', label: 'Actual/Actual', description: 'Actual days over 365 or 366, by the start date’s year' },
  { value: 'actual_365', label: 'Actual/365', description: 'Actual days over 365' },
  { value: 'actual_360', label: 'Actual/360', description: 'Actual days over 360' },
  { value: 'thirty_360_us', label: '30/360 US', description: '30-day months over 360, US rules' },
  { value: 'thirty_e_360', label: '30E/360', description: '30-day months over 360, Eurobond' },
] as const
const CONVENTIONS: string[] = DAY_COUNTS.map((d) => d.value)

export function dayCountLabel(value: string) {
  return DAY_COUNTS.find((d) => d.value === value)?.label ?? value
}

/** Declared-parameter keys a formula reads (not @periodDays / @yearDays). */
export function paramRefsOf(formula: string) {
  const out: string[] = []
  for (const m of formula.matchAll(/@([A-Za-z][A-Za-z0-9_]*)/g))
    if (!RESERVED_PARAMS.includes(m[1]) && !out.includes(m[1])) out.push(m[1])
  return out
}

export function hasYearfrac(formula: string) {
  return /\bYEARFRAC\s*\(/i.test(formula)
}

/** The first language 1.2 feature a formula uses, for messages. */
export function feature12(formula: string): string | null {
  const ref = paramRefsOf(formula)[0]
  if (ref) return `the parameter @${ref}`
  if (hasYearfrac(formula)) return 'YEARFRAC'
  if (/'[^']*'/.test(formula)) return 'text comparisons'
  return null
}

const LANGUAGE_RANK = { '1.0': 0, '1.1': 1, '1.2': 2 } as const

/** Type errors against the model's declared parameters. */
export function parameterErrors(model: CalcModel, formula: string): string[] {
  const errors: string[] = []
  const params = model.parameters
  for (const key of paramRefsOf(formula)) {
    const p = params.find((x) => x.key === key)
    if (!p) {
      errors.push(`@${key} is no declared parameter of the model.`)
      continue
    }
    if (p.type !== 'text') continue
    for (const m of formula.matchAll(new RegExp(`@${key}\\b`, 'g'))) {
      const i = m.index ?? 0
      const before = formula.slice(0, i).trimEnd()
      const after = formula.slice(i + m[0].length).trimStart()
      const ok = /YEARFRAC\s*\($/i.test(before) || /(=|<>)$/.test(before) || /^(=|<>|IN\b)/i.test(after)
      if (!ok) {
        errors.push(`@${key} is text; it can only be compared with text or passed to YEARFRAC.`)
        break
      }
    }
  }
  for (const m of formula.matchAll(/YEARFRAC\s*\(\s*([^)]*)\)/gi)) {
    const arg = m[1].trim()
    const quoted = /^'([^']*)'$/.exec(arg)
    if (quoted) {
      if (!CONVENTIONS.includes(quoted[1])) errors.push(`'${quoted[1]}' is no day count convention.`)
      continue
    }
    const ref = /^@([A-Za-z][A-Za-z0-9_]*)$/.exec(arg)
    const p = ref ? params.find((x) => x.key === ref[1]) : undefined
    if (!ref) errors.push('YEARFRAC takes a convention in quotes or a text parameter.')
    else if (p && (p.type !== 'text' || !p.allowedValues.length || p.allowedValues.some((v) => !CONVENTIONS.includes(v))))
      errors.push(`YEARFRAC(@${p.key}) needs a text parameter whose allowed values are all day count conventions.`)
  }
  return [...new Set(errors)]
}

/** Every input an expression reads: metrics then day parameters. */
export function inputsOf(formula: string) {
  return [...readsOf(formula), ...paramsOf(formula)]
}

export function attributionExpression(metric: Metric) {
  if (metric.attribution?.expression) return metric.attribution.expression
  return metric.type === 'measure' ? metric.formula : ''
}

export function defaultDrivers(model: CalcModel, expression: string): Driver[] {
  const drivers: Driver[] = readsOf(expression).map((k) => ({
    key: k.toLowerCase(),
    name: findMetric(model, k)?.name ?? k,
    inputs: [k],
  }))
  const params = paramsOf(expression)
  if (params.length) drivers.push({ key: 'days', name: 'Days', inputs: [...params] })
  return drivers
}

/** The drivers a metric splits into, or null when it can't be attributed. */
export function driversOf(model: CalcModel, metric: Metric): Driver[] | null {
  if (metric.attribution && !metric.attribution.useDefault) return metric.attribution.drivers
  const expr = attributionExpression(metric)
  if (!expr) return null
  return defaultDrivers(model, expr)
}

export type AttributionChip =
  | { kind: 'formula'; count: number }
  | { kind: 'own'; count: number; weight?: string }
  | { kind: 'none' }

export function attributionChip(model: CalcModel, metric: Metric): AttributionChip {
  if (metric.attribution) {
    const drivers = driversOf(model, metric) ?? []
    const weight = metric.attribution.weight ? findMetric(model, metric.attribution.weight)?.name : undefined
    return { kind: 'own', count: drivers.length, weight }
  }
  if (metric.type === 'measure') return { kind: 'formula', count: defaultDrivers(model, metric.formula).length }
  return { kind: 'none' }
}

export function hasPower(formula: string) {
  return /\bPOWER\s*\(/i.test(formula)
}

export function formulaError(model: CalcModel, formula: string, selfKey?: string): string | null {
  if (!formula.trim()) return 'Enter a formula.'
  if (model.language === '1.0' && hasPower(formula)) return 'POWER needs formula language 1.1; change it in the model settings.'
  const f12 = feature12(formula)
  if (f12 && LANGUAGE_RANK[model.language] < 2) return `${f12[0].toUpperCase()}${f12.slice(1)} needs formula language 1.2; change it in the model settings.`
  const paramError = parameterErrors(model, formula)[0]
  if (paramError) return paramError
  const keys = new Set(metricsOf(model).map((m) => m.key))
  const unknown = readsOf(formula).find((k) => !keys.has(k))
  if (unknown) return `[m.${unknown}] is no variable or calculated measure of the model.`
  if (selfKey && readsOf(formula).includes(selfKey)) return 'A measure cannot read itself.'
  return null
}

export function attributionErrors(model: CalcModel, expression: string, drivers: Driver[]): string[] {
  const errors: string[] = []
  const inputs = inputsOf(expression)
  for (const input of inputs) {
    const owners = drivers.filter((d) => d.inputs.includes(input)).length
    if (owners === 0) errors.push(`${input} belongs to no driver; every input the expression reads needs exactly one.`)
    if (owners > 1) errors.push(`${input} belongs to ${owners} drivers; it may belong to only one.`)
  }
  for (const d of drivers)
    for (const input of d.inputs) {
      if (/^@/.test(input) && !RESERVED_PARAMS.includes(input.slice(1)))
        errors.push(`${input} is a declared parameter; a row has the same value on both sides of a comparison, so it is never a driver input.`)
      else if (!inputs.includes(input)) errors.push(`${input} is not read by the expression, so driver "${d.key}" can't take it.`)
    }
  if (drivers.length > MAX_DRIVERS) errors.push(`At most ${MAX_DRIVERS} drivers; this has ${drivers.length}.`)
  const seen = new Set<string>()
  for (const d of drivers) {
    if (!d.key.trim()) errors.push('Every driver needs a key.')
    else if (d.key.trim().toLowerCase() === 'residual') errors.push('"residual" is reserved; choose another driver key.')
    else if (seen.has(d.key)) errors.push(`Driver key "${d.key}" is used twice.`)
    seen.add(d.key)
  }
  const keys = new Set(metricsOf(model).map((m) => m.key))
  const unknown = readsOf(expression).find((k) => !keys.has(k))
  if (unknown) errors.push(`[m.${unknown}] is no metric of the model.`)
  return [...new Set(errors)]
}

/** Rules an edited model must still pass; a non-empty list refuses the edit. */
export function modelViolations(model: CalcModel): RuleViolation[] {
  const out: RuleViolation[] = []
  const keys = new Set(metricsOf(model).map((m) => m.key))
  for (const m of model.measures) {
    for (const k of readsOf(m.formula))
      if (!keys.has(k))
        out.push({
          rule: 'formula_references_resolve',
          message: `Calculated measure '${m.key}' reads [m.${k}], which is no variable or calculated measure of the model.`,
        })
  }
  for (const metric of metricsOf(model)) {
    const expr = metric.attribution?.expression
    if (!expr) continue
    for (const k of readsOf(expr))
      if (!keys.has(k))
        out.push({
          rule: 'attribution_references_resolve',
          message: `The attribution of '${metric.key}' reads [m.${k}], which is no metric of the model.`,
        })
  }
  if (model.language === '1.0') {
    for (const m of model.measures)
      if (hasPower(m.formula))
        out.push({
          rule: 'formulas_fit_language',
          message: `Calculated measure '${m.key}' uses POWER, which formula language 1.0 does not have.`,
        })
  }
  const expressions = [
    ...model.measures.map((m) => ({ text: m.formula, label: `Calculated measure '${m.key}'` })),
    ...metricsOf(model)
      .filter((m) => m.attribution?.expression)
      .map((m) => ({ text: m.attribution!.expression!, label: `The attribution of '${m.key}'` })),
  ]
  for (const e of expressions) {
    const f12 = feature12(e.text)
    if (f12 && LANGUAGE_RANK[model.language] < 2)
      out.push({ rule: 'formulas_fit_language', message: `${e.label} uses ${f12}, which formula language ${model.language} does not have.` })
    for (const err of parameterErrors(model, e.text))
      out.push({ rule: 'parameters_resolve', message: `${e.label}: ${err}` })
  }
  for (const metric of metricsOf(model)) {
    const a = metric.attribution
    if (!a?.weight) continue
    if (!model.variables.some((v) => v.key === a.weight))
      out.push({
        rule: 'attribution_references_resolve',
        message: `The attribution of '${metric.key}' weighs by '${a.weight}', which is no variable of the model.`,
      })
  }
  return out
}

export function attributionFor(model: CalcModel, metric: Metric): Attribution {
  return (
    metric.attribution ?? {
      useDefault: true,
      drivers: defaultDrivers(model, attributionExpression(metric)),
    }
  )
}
