import { analyzeBands, type BandIssueKind } from '../bands'
import { USAGE_KIND_LABEL, type DeleteBlocker, type DeleteBlockerCode, type DeleteCheck, type DimensionDeleteTarget, type DimensionTab, type DimensionUsageKind, type DimensionUsageReference, type DimensionViolation, type DimensionViolationCode, type Violation } from '../contract'
import { findCycles } from '../tree'
import type { Db } from './data'

const quote = (names: string[]) => [...new Set(names)].map((n) => (n.startsWith("'") ? n : `'${n}'`)).join(', ')

const REF_PHRASE: Record<DimensionUsageKind, string> = {
  dataset_binding: 'it is bound by dataset versions',
  mapping_set: 'referenced by mapping sets',
  bridge: 'joined by bridges',
  calculation_dimension: 'grouped by calculation dimensions',
  report_scenario: 'used as a scenario by reports',
  report_source: 'used by report sources',
  assumption_set: 'pinned by assumption sets',
}

const blocker = (code: DeleteBlockerCode, message: string): DeleteBlocker => ({ code, message })

/** "it is bound by dataset versions ('loans.details' v3)", one blocker per referencing kind. */
function refBlockers(refs: DimensionUsageReference[]) {
  const byKind = new Map<DimensionUsageKind, string[]>()
  for (const r of refs) byKind.set(r.kind, [...(byKind.get(r.kind) ?? []), r.label ?? r.name])
  return [...byKind].map(([kind, names]) => blocker(`referenced_by_${kind}`, `${REF_PHRASE[kind]} (${quote(names)})`))
}

function result(subject: string, reasons: DeleteBlocker[]): DeleteCheck {
  return reasons.length ? { allowed: false, reasons, message: `${subject} cannot be deleted: ${reasons.map((r) => r.message).join('; ')}.` } : { allowed: true, reasons: [], message: null }
}

export function checkDelete(db: Db, target: DimensionDeleteTarget): DeleteCheck {
  switch (target.type) {
    case 'dimension': {
      const d = db.dimensions.find((x) => x.id === target.dimensionId)!
      if (d.origin === 'system') return result(`Dimension '${d.displayName}'`, [blocker('system_dimension', 'it is a system dimension')])
      const reasons: DeleteBlocker[] = []
      if (!d.isRemovable) reasons.push(blocker('not_removable', 'it is marked not removable'))
      reasons.push(...refBlockers(db.usage[d.id] ?? []))
      return result(`Dimension '${d.displayName}'`, reasons)
    }
    case 'version': {
      const v = db.versions.find((x) => x.id === target.versionId)!
      const reasons: DeleteBlocker[] = []
      if (v.status === 'published') reasons.push(blocker('live_version', 'it is the live version'))
      reasons.push(...refBlockers((db.usage[v.dimensionId] ?? []).filter((r) => r.pinsVersion === v.versionNumber)))
      return result(`Version ${v.versionNumber}`, reasons)
    }
    case 'member': {
      const v = db.versions.find((x) => x.id === target.versionId)!
      const m = db.members.find((x) => x.id === target.memberId)!
      const d = db.dimensions.find((x) => x.id === v.dimensionId)!
      const profile = d.profileKey ? db.profiles.find((p) => p.key === d.profileKey) : null
      const reasons: DeleteBlocker[] = []
      if (profile?.requiredMemberKeys.includes(m.key)) reasons.push(blocker('required_by_profile', `it is required by the ${profile.displayName} profile`))
      const kids = db.members.filter((x) => x.versionId === v.id && x.parentId === m.id)
      if (kids.length) reasons.push(blocker('has_children', `it has child members (${kids.length === 1 ? `'${kids[0].key}'` : `${kids.length}: ${quote(kids.slice(0, 3).map((k) => k.key))}${kids.length > 3 ? ', …' : ''}`})`))
      const bands = (db.bands[v.id] ?? []).filter((b) => b.memberId === m.id)
      if (bands.length) reasons.push(blocker('assigned_by_bands', `it is assigned by range bands (${bands.map((b) => b.ordinal).join(', ')})`))
      // Only the live version is read by other objects.
      if (v.status === 'published') {
        const named = (db.usage[d.id] ?? []).filter((r) => r.memberKeys?.includes(m.key))
        if (named.length) reasons.push(blocker('named_by_consumers', `it is named by ${named.map((r) => `${USAGE_KIND_LABEL[r.kind].toLowerCase()} ${r.label ?? `'${r.name}'`} (${r.detail})`).join(', ')}`))
      }
      return result(`Member '${m.key}'`, reasons)
    }
    case 'level': {
      const l = db.levels.find((x) => x.id === target.levelId)!
      const used = db.members.filter((m) => m.levelId === l.id).length
      return result(`Level '${l.key}'`, used ? [blocker('referenced_by_member', `${used} ${used === 1 ? 'member uses' : 'members use'} it`)] : [])
    }
    case 'role': {
      const r = db.roles.find((x) => x.id === target.roleId)!
      const d = db.dimensions.find((x) => x.id === r.dimensionId)!
      const profile = d.profileKey ? db.profiles.find((p) => p.key === d.profileKey) : null
      const reasons: DeleteBlocker[] = []
      if (profile?.requiredRoleKeys.includes(r.key)) reasons.push(blocker('required_by_profile', `it is required by the ${profile.displayName} profile`))
      reasons.push(...refBlockers((db.usage[d.id] ?? []).filter((u) => u.roleKey === r.key)))
      return result(`Role '${r.key}'`, reasons)
    }
  }
}

const violation = (code: DimensionViolationCode, area: DimensionTab, message: string, itemId?: string): DimensionViolation => ({
  code,
  message,
  target: itemId === undefined ? { area } : { area, itemId },
})

const BAND_CODE = {
  overlap: 'bands_non_overlapping',
  order: 'band_bounds_ordered',
  member: 'band_member_in_version',
  openLower: 'band_open_bounds_at_ends',
  openUpper: 'band_open_bounds_at_ends',
} as const satisfies Record<Exclude<BandIssueKind, 'gap'>, DimensionViolationCode>

export function validate(db: Db, versionId: string): DimensionViolation[] {
  const v = db.versions.find((x) => x.id === versionId)!
  const d = db.dimensions.find((x) => x.id === v.dimensionId)!
  const profile = d.profileKey ? db.profiles.find((p) => p.key === d.profileKey) : null
  const members = db.members.filter((m) => m.versionId === v.id)
  const levels = db.levels.filter((l) => l.versionId === v.id)
  const out: DimensionViolation[] = []

  const dupes = <T extends { id: string; key: string }>(items: T[]) => {
    const seen = new Map<string, T[]>()
    for (const i of items) seen.set(i.key, [...(seen.get(i.key) ?? []), i])
    return [...seen].filter(([, list]) => list.length > 1)
  }
  for (const [key, list] of dupes(members)) out.push(violation('member_key_unique', 'members', `Duplicate member key '${key}' (${list.length} members: ${list.map((m) => m.displayName).join(', ')}).`, list[1].id))
  for (const [key, list] of dupes(levels)) out.push(violation('level_key_unique', 'levels', `Duplicate level key '${key}'.`, list[1].id))

  const byId = new Map(members.map((m) => [m.id, m]))
  for (const cycle of findCycles(members)) {
    const keys = cycle.map((id) => byId.get(id)!.key)
    out.push(violation('hierarchy_acyclic', 'members', `Parent links form a cycle: ${[...keys, keys[0]].join(' → ')}.`, cycle[0]))
  }

  if (d.kind === 'range') {
    if (!v.rangeDataType) out.push(violation('range_data_type_matches_kind', 'properties', 'Set the range data type before freezing.'))
    else
      for (const issue of analyzeBands(db.bands[v.id] ?? [], v.rangeDataType, new Set(byId.keys()))) {
        if (issue.kind === 'gap') continue
        out.push(violation(BAND_CODE[issue.kind], 'bands', issue.message, String(issue.ordinals[0])))
      }
  }

  if (profile) {
    const keys = new Set(members.map((m) => m.key))
    for (const k of profile.requiredMemberKeys) if (!keys.has(k)) out.push(violation('profile_required_keys_present', 'members', `Required member '${k}' is missing (${profile.displayName} profile).`))
    const roleKeys = new Set(db.roles.filter((r) => r.dimensionId === d.id).map((r) => r.key))
    for (const k of profile.requiredRoleKeys) if (!roleKeys.has(k)) out.push(violation('profile_required_keys_present', 'roles', `Required role '${k}' is missing (${profile.displayName} profile).`))
  }

  const present = new Set(members.map((m) => m.key))
  const missing = new Map<string, string[]>()
  for (const ref of db.usage[d.id] ?? []) for (const k of ref.memberKeys ?? []) if (!present.has(k)) missing.set(k, [...(missing.get(k) ?? []), `${USAGE_KIND_LABEL[ref.kind].toLowerCase()} ${ref.name}`])
  if (missing.size)
    out.push(
      violation(
        'dimension_consumers_resolve',
        'usage',
        `Keys other objects name are missing from v${v.versionNumber}: ${[...missing].map(([k, by]) => `${k} (${by.join(', ')})`).join('; ')}.`,
      ),
    )
  return out
}
