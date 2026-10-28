import type { BreakoutDimension, Profile, ReportScenario, ReportState, Source } from './types'

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
