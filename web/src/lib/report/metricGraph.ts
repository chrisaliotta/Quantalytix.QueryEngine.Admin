import type { CalculatedMeasure, Variable } from './types'

export interface MetricNode {
  key: string
  kind: 'variable' | 'calculated' | 'unknown'
  children: MetricNode[]
}

interface Model {
  variables: Variable[]
  measures: CalculatedMeasure[]
}

const REF_PATTERN = /\[m\.([A-Za-z0-9_]+)\]/g

export function parseRefs(formula: string): string[] {
  const refs = new Set<string>()
  for (const match of formula.matchAll(REF_PATTERN)) refs.add(match[1])
  return [...refs]
}

export function metricTree(key: string, model: Model, seen: Set<string> = new Set()): MetricNode {
  if (model.variables.some((v) => v.key === key)) return { key, kind: 'variable', children: [] }
  const measure = model.measures.find((m) => m.key === key)
  if (!measure || seen.has(key)) return { key, kind: 'unknown', children: [] }
  const nextSeen = new Set(seen).add(key)
  return {
    key,
    kind: 'calculated',
    children: parseRefs(measure.formula).map((ref) => metricTree(ref, model, nextSeen)),
  }
}

/** Every model variable a metric reads, directly or through other calculated measures. */
export function leafVariables(key: string, model: Model): string[] {
  const out = new Set<string>()
  const walk = (node: MetricNode) => {
    if (node.kind === 'variable') out.add(node.key)
    node.children.forEach(walk)
  }
  walk(metricTree(key, model))
  return [...out]
}

export function isCalculated(key: string, model: Model) {
  return model.measures.some((m) => m.key === key)
}
