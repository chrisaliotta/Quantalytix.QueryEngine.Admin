import type { BreakoutDimension, ComparisonSide, Profile, ReportScenario, ReportState, ScenarioMember, Source } from './types'

export const LATEST_PREFIX = 'latest:'

export function flattenScenarios(members: ScenarioMember[], path: string[] = []): (ScenarioMember & { path: string[] })[] {
  return members.flatMap((m) => [{ ...m, path }, ...flattenScenarios(m.children ?? [], [...path, m.label])])
}

export function findScenario(state: ReportState, key: string) {
  return flattenScenarios(state.scenarios).find((m) => m.key === key)
}

/** Plan versions (leaves) under a member. */
export function versionsUnder(member: ScenarioMember): ScenarioMember[] {
  if (!member.children?.length) return member.status ? [member] : []
  return member.children.flatMap(versionsUnder)
}

/** Folders that hold versions directly, the ones "latest approved" can point at. */
export function versionFolders(state: ReportState) {
  return flattenScenarios(state.scenarios).filter((m) => m.children?.some((c) => !c.children?.length))
}

export function latestApproved(member: ScenarioMember) {
  return versionsUnder(member)
    .filter((v) => v.status === 'approved')
    .sort((a, b) => (b.asOf ?? '').localeCompare(a.asOf ?? ''))[0]
}

export interface ResolvedScenario {
  kind: 'actual' | 'version' | 'latest'
  label: string
  version?: ScenarioMember
  folder?: ScenarioMember
}

export function resolveScenario(state: ReportState, key: string): ResolvedScenario {
  if (key === 'actual') return { kind: 'actual', label: 'Actual' }
  if (key.startsWith(LATEST_PREFIX)) {
    const folder = findScenario(state, key.slice(LATEST_PREFIX.length))
    const version = folder ? latestApproved(folder) : undefined
    return { kind: 'latest', label: `Latest approved in ${folder?.label ?? '?'}`, folder, version }
  }
  const version = findScenario(state, key)
  return { kind: 'version', label: version?.label ?? key, version }
}

export const PERIOD_LABEL: Record<ComparisonSide['period'], string> = {
  same: 'the same period',
  PM: 'the prior month',
  PQ: 'the prior quarter',
  PY: 'the same month a year earlier',
}

export function sideSentence(state: ReportState, side: ComparisonSide) {
  const r = resolveScenario(state, side.scenario)
  const set = state.assumptionSets.find((a) => a.key === side.assumptions)
  const scenario = r.kind === 'actual' ? 'Actual' : r.kind === 'latest' ? `${r.label} (now ${r.version?.label ?? 'none'})` : r.label
  return `${scenario}, ${PERIOD_LABEL[side.period]}${set && set.key !== 'active' ? `, under ${set.label}` : ''}`
}

function boundRoles(state: ReportState, source: Source) {
  return state.datasets.find((d) => d.key === source.dataset)?.roles ?? []
}

/** Scenarios of the report whose role the source's dataset binds. */
export function sourceScenarios(state: ReportState, source: Source): ReportScenario[] {
  const roles = boundRoles(state, source)
  return state.reportScenarios.filter((sc) => roles.includes(sc.role))
}

export function profileSources(state: ReportState, profile: Profile) {
  return [...new Set(Object.values(profile.picks))]
    .map((k) => state.sources.find((s) => s.key === k))
    .filter((s): s is Source => !!s)
}

/** Scenarios a run under the profile must name a member of. */
export function profileScenarios(state: ReportState, profile: Profile): ReportScenario[] {
  const roles = new Set(profileSources(state, profile).flatMap((s) => boundRoles(state, s)))
  return state.reportScenarios.filter((sc) => roles.has(sc.role))
}

/** Sources that bind the role, each with the profiles that pick it. */
export function scenarioReaders(state: ReportState, role: string) {
  return state.sources
    .filter((s) => boundRoles(state, s).includes(role))
    .map((source) => ({ source, profiles: state.profiles.filter((p) => Object.values(p.picks).includes(source.key)) }))
}

export function profileMix(state: ReportState, profile: Profile) {
  const sources = profileSources(state, profile)
  const plan = sources.some((s) => sourceScenarios(state, s).length > 0)
  const actual = sources.some((s) => sourceScenarios(state, s).length === 0)
  return { readsPlan: plan, mixed: plan && actual }
}

/** Whether a dimension can be used under a profile, with the reason it can't. */
export function dimensionAvailability(state: ReportState, dim: BreakoutDimension, profile: Profile): { ok: boolean; reason: string } {
  const sources = profileSources(state, profile)
  for (const s of sources) {
    const ds = state.datasets.find((d) => d.key === s.dataset)
    if (!ds?.roles.includes(dim.role)) return { ok: false, reason: `${s.key} reads ${s.dataset}, which does not bind dimension ${dim.role}` }
  }
  return { ok: true, reason: `Every source of ${profile.name} binds dimension ${dim.role}.` }
}

export function methodLabel(source: Pick<Source, 'method' | 'dimension' | 'rowKeyField'>) {
  return source.method === 'mapped' ? `Bridge from ${source.dimension ?? 'account'}` : `Direct on ${source.rowKeyField ?? 'a field'}`
}
