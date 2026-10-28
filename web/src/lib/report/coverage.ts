import { groupingIssue } from './dimensions'
import { leafVariables } from './metricGraph'
import { profileGaps, unresolvedVariables } from './profiles'
import { allRows, findRow } from './tree'
import type { Binding, DimMember, Mapping, Profile, ReportState, Source } from './types'

export interface MemberInfo {
  member: DimMember
  isFolder: boolean
  parentKey: string | null
}

export function memberIndex(accounts: DimMember[]) {
  const map = new Map<string, MemberInfo>()
  const walk = (members: DimMember[], parentKey: string | null) => {
    for (const m of members) {
      map.set(m.key, { member: m, isFolder: !!m.children, parentKey })
      if (m.children) walk(m.children, m.key)
    }
  }
  walk(accounts, null)
  return map
}

export function leafAccounts(accounts: DimMember[]): DimMember[] {
  return accounts.flatMap((m) => (m.children ? leafAccounts(m.children) : [m]))
}

export function memberLabel(key: string, accounts: DimMember[]) {
  const info = memberIndex(accounts).get(key)
  if (!info) return key
  return info.isFolder ? `${key} folder` : `${key} ${info.member.label}`
}

export function mappingCovers(mapping: Mapping, leafKey: string, index: Map<string, MemberInfo>) {
  if (mapping.memberKey === leafKey) return true
  let parent = index.get(leafKey)?.parentKey ?? null
  while (parent) {
    if (parent === mapping.memberKey) return true
    parent = index.get(parent)?.parentKey ?? null
  }
  return false
}

export function mappingsOnRow(source: Source, rowKey: string) {
  return source.mappings.filter((m) => m.rowKey === rowKey)
}

/**
 * Net scale per account that lands on a row from one source. An exclusion cancels what folders contribute,
 * but a direct mapping of the same account still counts — that is how an account is broken out of a folder.
 */
export function rowLeafFactors(source: Source, rowKey: string, accounts: DimMember[]) {
  const index = memberIndex(accounts)
  const result = new Map<string, number>()
  const rowMappings = mappingsOnRow(source, rowKey)
  for (const leaf of leafAccounts(accounts)) {
    const touching = rowMappings.filter((m) => mappingCovers(m, leaf.key, index))
    if (!touching.length) continue
    const excluded = touching.some((m) => m.factor < 0)
    const viaFolders = touching.filter((m) => m.factor > 0 && m.memberKey !== leaf.key).reduce((s, m) => s + m.factor, 0)
    const direct = touching.filter((m) => m.factor > 0 && m.memberKey === leaf.key).reduce((s, m) => s + m.factor, 0)
    result.set(leaf.key, (excluded ? 0 : viaFolders) + direct)
  }
  return result
}

/** A direct inclusion of an account that is also excluded from a folder on the same row. */
export function isBrokenOutOnRow(source: Source, rowKey: string, leafKey: string) {
  return (
    isExcludedOnRow(source, rowKey, leafKey) &&
    source.mappings.some((m) => m.rowKey === rowKey && m.memberKey === leafKey && m.factor > 0)
  )
}

export function isExcludedOnRow(source: Source, rowKey: string, leafKey: string) {
  return source.mappings.some((m) => m.rowKey === rowKey && m.memberKey === leafKey && m.factor < 0)
}

export function matchedValues(source: Source, state: ReportState) {
  if (source.method !== 'matched' || !source.rowKeyField) return []
  return state.datasets.find((d) => d.key === source.dataset)?.fieldValues?.[source.rowKeyField] ?? []
}

export function sourceReachesRow(source: Source, rowKey: string, state: ReportState) {
  if (source.method === 'matched') return matchedValues(source, state).includes(rowKey)
  return source.mappings.some((m) => m.rowKey === rowKey)
}

export function sourcesOnRow(state: ReportState, rowKey: string) {
  return state.sources.filter((s) => sourceReachesRow(s, rowKey, state))
}

export interface CoverageEntry {
  key: string
  label: string
}

export interface MultiCountEntry {
  key: string
  label: string
  net: number
  parts: { rowKey: string; factor: number }[]
  note: string
}

export interface SourceCoverage {
  unmapped: CoverageEntry[]
  leftOut: CoverageEntry[]
  multi: MultiCountEntry[]
}

export function sourceCoverage(source: Source, state: ReportState): SourceCoverage {
  if (source.method === 'matched') {
    const rowKeys = new Set(allRows(state.rows).map((r) => r.key))
    const unmapped = matchedValues(source, state)
      .filter((v) => !rowKeys.has(v))
      .map((v) => ({ key: v, label: v }))
    return { unmapped, leftOut: [], multi: [] }
  }
  const index = memberIndex(state.accounts)
  const unmapped: CoverageEntry[] = []
  const leftOut: CoverageEntry[] = []
  const multi: MultiCountEntry[] = []

  for (const leaf of leafAccounts(state.accounts)) {
    const touching = source.mappings.filter((m) => mappingCovers(m, leaf.key, index))
    if (!touching.length) {
      const entry = { key: leaf.key, label: leaf.label }
      if (source.leftOut.includes(leaf.key)) leftOut.push(entry)
      else unmapped.push(entry)
      continue
    }
    const byRow = new Map<string, number>()
    for (const m of touching) byRow.set(m.rowKey, (byRow.get(m.rowKey) ?? 0) + m.factor)
    const net = [...byRow.values()].reduce((a, b) => a + b, 0)
    const parts = touching.map((m) => ({ rowKey: m.rowKey, factor: m.factor }))
    const zeroRow = [...byRow.entries()].find(([, f]) => f === 0)
    if (net !== 1) {
      multi.push({
        key: leaf.key,
        label: leaf.label,
        net,
        parts,
        note: net === 0 ? 'not counted' : `counted x${net}`,
      })
    } else if (zeroRow && touching.length > 1) {
      multi.push({ key: leaf.key, label: leaf.label, net: 0, parts: parts.filter((p) => p.rowKey === zeroRow[0]), note: `not counted on ${zeroRow[0]}` })
    }
  }
  return { unmapped, leftOut, multi }
}

export function rowsMappingMember(source: Source, memberKey: string, accounts: DimMember[], exceptRow?: string) {
  const index = memberIndex(accounts)
  const info = index.get(memberKey)
  const rows = new Set<string>()
  for (const m of source.mappings) {
    if (m.rowKey === exceptRow || m.factor <= 0) continue
    const coversMember = m.memberKey === memberKey || (!info?.isFolder && mappingCovers(m, memberKey, index))
    if (coversMember) rows.add(m.rowKey)
  }
  return [...rows]
}

/* ---------- resolving what a row shows ---------- */

export interface Supply {
  ok: boolean
  source?: Source
  binding?: Binding
  reason?: string
  alternative?: string
}

function accountFilter(binding: Binding) {
  return binding.filters.find((f) => f.field === 'accountKey' && f.op === '=')?.value
}

/** Whether a source actually delivers a variable to a row, considering the binding's account filter. */
export function sourceSuppliesOnRow(source: Source, variable: string, rowKey: string, state: ReportState): { ok: boolean; binding?: Binding; reason?: string } {
  const binding = source.bindings.find((b) => b.variable === variable)
  if (!binding) return { ok: false, reason: `${source.name} doesn't supply this variable` }
  if (!sourceReachesRow(source, rowKey, state)) {
    return { ok: false, binding, reason: `${source.name} isn't mapped to this row` }
  }
  const account = accountFilter(binding)
  if (account && source.method === 'mapped') {
    const factors = rowLeafFactors(source, rowKey, state.accounts)
    if (!factors.get(account)) {
      const elsewhere = rowsMappingMember(source, account, state.accounts, rowKey)
      const where = elsewhere.length ? `to ${elsewhere.join(', ')}, not ${rowKey}` : 'to no row'
      return { ok: false, binding, reason: `${source.name} maps ${account} ${where}` }
    }
  }
  return { ok: true, binding }
}

export function profileFor(state: ReportState, profileKey: string): Profile | undefined {
  return state.profiles.find((p) => p.key === profileKey)
}

export function resolveVariableOnRow(state: ReportState, rowKey: string, variable: string, profileKey: string): Supply {
  const suppliers = state.sources.filter((s) => s.bindings.some((b) => b.variable === variable))
  if (!suppliers.length) return { ok: false, reason: 'no source supplies this variable' }
  const profile = profileFor(state, profileKey)
  const pickedKey = profile?.picks[variable] ?? suppliers[0].key
  const picked = state.sources.find((s) => s.key === pickedKey) ?? suppliers[0]
  const result = sourceSuppliesOnRow(picked, variable, rowKey, state)
  if (result.ok) return { ok: true, source: picked, binding: result.binding }

  const alt = suppliers.find((s) => s.key !== picked.key && sourceSuppliesOnRow(s, variable, rowKey, state).ok)
  const altProfile = alt && state.profiles.find((p) => p.picks[variable] === alt.key)
  return {
    ok: false,
    source: picked,
    reason: result.reason,
    alternative: alt ? `${alt.name} supplies it here${altProfile ? ` under profile ${altProfile.key}` : ''}` : undefined,
  }
}

export function resolveVariableReportLevel(state: ReportState, variable: string, profileKey: string): Supply {
  const profile = profileFor(state, profileKey)
  const suppliers = state.sources.filter((s) => s.bindings.some((b) => b.variable === variable))
  if (!suppliers.length) return { ok: false, reason: 'no source supplies this variable' }
  const source = state.sources.find((s) => s.key === profile?.picks[variable]) ?? suppliers[0]
  const binding = source.bindings.find((b) => b.variable === variable)
  if (!binding) return { ok: false, source, reason: `${source.name} doesn't supply this variable` }
  return { ok: true, source, binding }
}

/* ---------- readiness ---------- */

export type StepId = 'rows' | 'sources' | 'variables' | 'items' | 'profiles' | 'dimensions' | 'columns' | 'ready'

export interface ReadinessIssue {
  message: string
  rule?: string
  dimensionKey?: string
  sourceKey?: string
  rowKey?: string
  profileKey?: string
}

function profileStep(state: ReportState, profileKey: string): ReadinessStep {
  const unresolved = unresolvedVariables(state, profileKey)
  const gaps = profileGaps(state, profileKey)
  const issues: ReadinessIssue[] = [
    ...unresolved.map((v) => ({ profileKey, message: `${profileKey}: ${v} has no source picked` })),
    ...gaps.map((g) => ({ rowKey: g.rowKey, message: `${rowLabel(state, g.rowKey)} · ${g.variable}: ${g.reason}` })),
  ]
  return {
    id: 'profiles',
    label: 'Profile issues',
    status: unresolved.length ? 'fail' : gaps.length ? 'warn' : 'pass',
    issues,
  }
}

export interface ReadinessStep {
  id: StepId
  label: string
  status: 'pass' | 'warn' | 'fail'
  issues: ReadinessIssue[]
}

export function readiness(state: ReportState, profileKey: string = state.settings.defaultProfile): ReadinessStep[] {
  const lines = allRows(state.rows).filter((r) => r.kind === 'line')
  const visibleColumns = state.columns.filter((c) => c.visible)
  const usedVariables = new Set(visibleColumns.flatMap((c) => leafVariables(c.metric, state)))
  const unbound = [...usedVariables].filter((v) => !state.sources.some((s) => s.bindings.some((b) => b.variable === v)))

  const itemIssues: ReadinessIssue[] = []
  for (const source of state.sources) {
    const { unmapped } = sourceCoverage(source, state)
    if (!unmapped.length) continue
    const noun = source.method === 'matched' ? 'values' : 'accounts'
    itemIssues.push({
      sourceKey: source.key,
      message: `${source.name}: ${unmapped.length} ${noun} not mapped to any row (${unmapped.map((u) => (u.key === u.label ? u.key : `${u.key} ${u.label}`)).join(', ')})`,
    })
  }
  const formulaIssues = allRows(state.rows)
    .filter((r) => r.kind === 'formula' && (!r.measureKey || !r.columnKey))
    .map((r) => ({ message: `Formula row "${r.label}" needs a calculated measure and a column` }))

  const steps: ReadinessStep[] = [
    {
      id: 'rows',
      label: 'Rows',
      status: lines.length && !formulaIssues.length ? 'pass' : 'fail',
      issues: lines.length ? formulaIssues : [{ message: 'Add at least one line that takes data' }, ...formulaIssues],
    },
    {
      id: 'sources',
      label: 'Sources',
      status: state.sources.length ? 'pass' : 'fail',
      issues: state.sources.length ? [] : [{ message: 'Add a source so rows can receive data' }],
    },
    {
      id: 'variables',
      label: 'Variables bound',
      status: unbound.length ? 'fail' : 'pass',
      issues: unbound.map((v) => ({ message: `${v} is used by a column but no source supplies it` })),
    },
    { id: 'items', label: 'Items mapped', status: itemIssues.length ? 'warn' : 'pass', issues: itemIssues },
    profileStep(state, profileKey),
    dimensionStep(state),
    {
      id: 'columns',
      label: 'Columns',
      status: visibleColumns.length ? 'pass' : 'fail',
      issues: visibleColumns.length ? [] : [{ message: 'Show at least one column' }],
    },
  ]
  const blockers: ReadinessIssue[] = steps
    .filter((s) => s.status === 'fail' && s.id !== 'profiles')
    .map((b) => ({ message: `${b.label} needs attention` }))
  for (const p of state.profiles.filter((x) => x.enabled)) {
    const unresolved = unresolvedVariables(state, p.key)
    if (unresolved.length) {
      blockers.push({ profileKey: p.key, message: `Profile ${p.key} can't resolve ${unresolved.join(', ')}` })
    }
  }
  steps.push({
    id: 'ready',
    label: 'Ready to enable',
    status: blockers.length ? 'fail' : 'pass',
    issues: blockers,
  })
  return steps
}

function dimensionStep(state: ReportState): ReadinessStep {
  const issues: ReadinessIssue[] = state.breakoutDims
    .filter((d) => d.enabled)
    .flatMap((d) => {
      const issue = groupingIssue(state, d)
      return issue ? [{ ...issue, dimensionKey: d.key }] : []
    })
  return { id: 'dimensions', label: 'Dimension groupings', status: issues.length ? 'fail' : 'pass', issues }
}

export function lineKeys(state: ReportState) {
  return allRows(state.rows).filter((r) => r.kind === 'line').map((r) => r.key)
}

export function rowLabel(state: ReportState, key: string) {
  return findRow(state.rows, key)?.label ?? key
}
