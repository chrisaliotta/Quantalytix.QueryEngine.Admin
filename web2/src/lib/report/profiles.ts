import { matchedValues, sourceReachesRow, sourceSuppliesOnRow } from './coverage'
import { leafVariables } from './metricGraph'
import { allRows, findRow } from './tree'
import type { Binding, Profile, ReportState, Source } from './types'

export function getProfile(state: ReportState, key: string): Profile | undefined {
  return state.profiles.find((p) => p.key === key)
}

export function suppliersOf(state: ReportState, variable: string): Source[] {
  return state.sources.filter((s) => s.bindings.some((b) => b.variable === variable))
}

export function bindingFor(source: Source, variable: string): Binding | undefined {
  return source.bindings.find((b) => b.variable === variable)
}

/** Variables the report reads, with the column labels and formula rows that read each. */
export function variableUsage(state: ReportState): Map<string, string[]> {
  const usage = new Map<string, string[]>()
  const add = (v: string, label: string) => {
    const list = usage.get(v) ?? []
    if (!list.includes(label)) list.push(label)
    usage.set(v, list)
  }
  for (const col of state.columns.filter((c) => c.visible)) {
    for (const v of leafVariables(col.metric, state)) add(v, col.label)
  }
  for (const row of allRows(state.rows).filter((r) => r.kind === 'formula' && r.measureKey)) {
    for (const v of leafVariables(row.measureKey!, state)) add(v, row.label)
  }
  return new Map(state.variables.filter((v) => usage.has(v.key)).map((v) => [v.key, usage.get(v.key)!]))
}

/** Variables a line row resolves itself (visible columns only; formula rows resolve report-wide). */
export function lineVariables(state: ReportState): string[] {
  const out = new Set<string>()
  for (const col of state.columns.filter((c) => c.visible)) leafVariables(col.metric, state).forEach((v) => out.add(v))
  return state.variables.map((v) => v.key).filter((k) => out.has(k))
}

export function pickedSource(state: ReportState, profile: Profile | undefined, variable: string): Source | undefined {
  const key = profile?.picks[variable]
  const source = state.sources.find((s) => s.key === key)
  return source && bindingFor(source, variable) ? source : undefined
}

/** Variables (that the report reads) this profile takes from the given source. */
export function variablesReadFrom(state: ReportState, profileKey: string, sourceKey: string): string[] {
  const profile = getProfile(state, profileKey)
  return [...variableUsage(state).keys()].filter((v) => pickedSource(state, profile, v)?.key === sourceKey)
}

export function sourceReadBy(state: ReportState, profileKey: string, sourceKey: string) {
  return variablesReadFrom(state, profileKey, sourceKey).length > 0
}

export function unresolvedVariables(state: ReportState, profileKey: string): string[] {
  const profile = getProfile(state, profileKey)
  return [...variableUsage(state).keys()].filter((v) => !pickedSource(state, profile, v))
}

function inScope(state: ReportState, variable: string, rowKey: string) {
  const scopes = state.rowScopes.filter((s) => s.variable === variable)
  if (!scopes.length) return true
  return scopes.some((s) => {
    if (s.rowKey === rowKey) return true
    if (s.scope !== 'subtree') return false
    const root = findRow(state.rows, s.rowKey)
    return !!root && allRows(root.children).some((r) => r.key === rowKey)
  })
}

export type VarStatus = 'ok' | 'shadowed' | 'gap' | 'unresolved' | 'na'

export interface VarResolution {
  variable: string
  status: VarStatus
  picked?: Source
  binding?: Binding
  /** Plain-language explanation for gap / unresolved / na. */
  reason?: string
  /** Other sources that do supply this variable on the row. */
  others: Source[]
}

export function resolveForProfile(state: ReportState, rowKey: string, variable: string, profileKey: string): VarResolution {
  const suppliers = suppliersOf(state, variable)
  if (!suppliers.length) return { variable, status: 'unresolved', reason: 'No source supplies this variable', others: [] }
  const profile = getProfile(state, profileKey)
  const picked = pickedSource(state, profile, variable)
  if (!picked) return { variable, status: 'unresolved', reason: `${profileKey} has no source picked for this variable`, others: [] }
  if (!inScope(state, variable, rowKey)) {
    return { variable, status: 'na', picked, reason: 'Outside this variable’s row scope', others: [] }
  }

  const result = sourceSuppliesOnRow(picked, variable, rowKey, state)
  const others = suppliers.filter((s) => s.key !== picked.key && sourceSuppliesOnRow(s, variable, rowKey, state).ok)
  if (result.ok) return { variable, status: others.length ? 'shadowed' : 'ok', picked, binding: result.binding, others }

  const rowReceives = suppliers.some((s) => sourceReachesRow(s, rowKey, state))
  if (!rowReceives) {
    return { variable, status: 'na', picked, reason: 'No source that supplies this is mapped to this row', others }
  }

  let reason: string
  if (!sourceReachesRow(picked, rowKey, state)) {
    reason =
      picked.method === 'matched'
        ? `${profileKey} reads this from ${picked.name}, which only matches ${matchedValues(picked, state).join(', ')} by row key`
        : `${profileKey} reads this from ${picked.name}, which isn't mapped to this row`
  } else {
    reason = `${profileKey} reads this from ${result.reason?.replace(picked.name, picked.name + ', which')}`
  }
  return { variable, status: 'gap', picked, binding: bindingFor(picked, variable), reason, others }
}

export interface RowGap {
  rowKey: string
  variable: string
  reason: string
}

export function rowGaps(state: ReportState, rowKey: string, profileKey: string): RowGap[] {
  const row = findRow(state.rows, rowKey)
  if (row?.kind !== 'line') return []
  return lineVariables(state)
    .map((v) => resolveForProfile(state, rowKey, v, profileKey))
    .filter((r) => r.status === 'gap')
    .map((r) => ({ rowKey, variable: r.variable, reason: r.reason ?? '' }))
}

export function profileGaps(state: ReportState, profileKey: string): RowGap[] {
  return allRows(state.rows)
    .filter((r) => r.kind === 'line')
    .flatMap((r) => rowGaps(state, r.key, profileKey))
}

/** Sources on a row that the profile actually reads (picks for at least one variable the report reads). */
export function readSourcesOnRow(state: ReportState, rowKey: string, profileKey: string) {
  const onRow = state.sources.filter((s) => sourceReachesRow(s, rowKey, state))
  return {
    read: onRow.filter((s) => sourceReadBy(state, profileKey, s.key)),
    unread: onRow.filter((s) => !sourceReadBy(state, profileKey, s.key)),
  }
}

/** Seed picks for a new profile: take the only binder where there is one. */
export function autoPicks(state: ReportState): Record<string, string> {
  const picks: Record<string, string> = {}
  for (const v of state.variables) {
    const s = suppliersOf(state, v.key)
    if (s.length === 1) picks[v.key] = s[0].key
  }
  return picks
}
