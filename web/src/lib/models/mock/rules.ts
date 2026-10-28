import type { ModelFormulaError, ModelRuleCode, Violation } from '../contract'
import { defaultDrivers, inputsOf, MAX_DRIVERS, metricsOf, RESERVED_PARAMS } from '../metrics'
import type { StoredModel } from './data'

/**
 * A simplified stand-in for the server's model gate (blueprint 15, C2-C6): references, self-reads, declared parameters,
 * language and the attribution drivers. The server also re-runs the report rules of every enabled report over the model.
 */

interface Finding extends ModelFormulaError {
  code: ModelRuleCode
}

const METRIC_REF = /\[m\.([A-Za-z0-9_]+)\]/g
const PARAM_REF = /@([A-Za-z][A-Za-z0-9_]*)/g
const LANGUAGE_RANK = { '1.0': 0, '1.1': 1, '1.2': 2 } as const

function featureLanguage(formula: string, declared: string[]) {
  const params = [...formula.matchAll(PARAM_REF)].some((m) => declared.includes(m[1]))
  if (params || /\bYEARFRAC\s*\(/i.test(formula) || /'[^']*'/.test(formula)) return '1.2'
  return /\bPOWER\s*\(/i.test(formula) ? '1.1' : '1.0'
}

/** A formula's problems against the model, each over the text it concerns. */
export function formulaFindings(model: StoredModel, formula: string, selfKey: string | null): Finding[] {
  const metrics = metricsOf(model).map((m) => m.key)
  const declared = model.parameters.map((p) => p.key)
  const findings: Finding[] = []
  for (const m of formula.matchAll(METRIC_REF)) {
    const at = { start: m.index, end: m.index + m[0].length }
    if (m[1] === selfKey) findings.push({ code: 'formulas_acyclic', message: 'A calculated measure cannot read itself.', ...at })
    else if (!metrics.includes(m[1])) findings.push({ code: 'formula_references_resolve', message: `[m.${m[1]}] is no variable or calculated measure of the model.`, ...at })
  }
  for (const m of formula.matchAll(PARAM_REF)) {
    if (!RESERVED_PARAMS.includes(m[1]) && !declared.includes(m[1]))
      findings.push({ code: 'formula_parameters_resolve', message: `@${m[1]} is no parameter of the model, @periodDays or @yearDays.`, start: m.index, end: m.index + m[0].length })
  }
  return findings
}

/** The model rules' violations of a stored model. */
export function violationsOf(model: StoredModel): Violation[] {
  const out: Violation[] = []
  const keys = metricsOf(model).map((m) => m.key)
  const variables = model.variables.map((v) => v.key)
  const declared = model.parameters.map((p) => p.key)
  const language = (formula: string, subject: string, target: Violation['target']) => {
    const needed = featureLanguage(formula, declared)
    if (LANGUAGE_RANK[needed] > LANGUAGE_RANK[model.formulaLanguage])
      out.push({ code: 'formulas_fit_language', message: `${subject} needs formula language ${needed}.`, target })
  }

  for (const m of model.measures) {
    const target = { area: 'measures', itemKey: m.key }
    for (const f of formulaFindings(model, m.formula, m.key)) out.push({ code: f.code, message: `Calculated measure '${m.key}': ${f.message}`, target })
    language(m.formula, `Calculated measure '${m.key}'`, target)
  }

  for (const a of model.attributions) {
    const target = { area: 'attributions', itemKey: a.metricKey }
    const subject = `The attribution of '${a.metricKey}'`
    if (!keys.includes(a.metricKey)) out.push({ code: 'attribution_references_resolve', message: `${subject} names no variable or calculated measure of the model.`, target })
    else if (!a.expression && variables.includes(a.metricKey))
      out.push({ code: 'attribution_references_resolve', message: `${subject} needs an expression, because a variable has no formula to switch its drivers in.`, target })
    if (a.weightVariableKey && !variables.includes(a.weightVariableKey))
      out.push({ code: 'attribution_weight_variable', message: `${subject} weighs members by '${a.weightVariableKey}', which is no variable of the model.`, target })
    if (a.expression) {
      for (const f of formulaFindings(model, a.expression, null))
        out.push({ code: f.code === 'formula_references_resolve' ? 'attribution_references_resolve' : f.code, message: `${subject}: ${f.message}`, target })
      language(a.expression, subject, target)
    }

    const expression = a.expression ?? model.measures.find((m) => m.key === a.metricKey)?.formula
    if (!expression) continue
    const drivers = a.drivers.length ? a.drivers : defaultDrivers(model, expression)
    if (drivers.length < 1 || drivers.length > MAX_DRIVERS) out.push({ code: 'attribution_drivers', message: `${subject} has ${drivers.length} drivers; a metric has 1 to ${MAX_DRIVERS}.`, target })
    if (new Set(drivers.map((d) => d.key)).size < drivers.length) out.push({ code: 'attribution_drivers', message: `${subject} repeats a driver key.`, target })
    const inputs = inputsOf(expression)
    for (const input of inputs) {
      const owners = drivers.filter((d) => d.inputs.includes(input)).length
      if (owners !== 1) out.push({ code: 'attribution_drivers', message: `${subject} reads '${input}', which ${owners ? 'more than one driver' : 'no driver'} switches.`, target })
    }
    for (const input of new Set(drivers.flatMap((d) => d.inputs)))
      if (!inputs.includes(input)) out.push({ code: 'attribution_drivers', message: `${subject} has drivers that switch '${input}', which its expression does not read.`, target })
  }
  return out
}
