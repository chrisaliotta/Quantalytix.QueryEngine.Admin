import { analyzeBands } from '../bands'
import type { DeleteCheck, DeleteTarget, UsageKind, UsageReference, Violation } from '../contract'
import { findCycles } from '../tree'
import type { Db } from './data'

const quote = (names: string[]) => [...new Set(names)].map((n) => (n.startsWith("'") ? n : `'${n}'`)).join(', ')

const REF_PHRASE: Record<UsageKind, string> = {
  'dataset binding': 'it is bound by dataset versions',
  'mapping set': 'referenced by mapping sets',
  bridge: 'joined by bridges',
  'calculation dimension': 'grouped by calculation dimensions',
  'report scenario': 'used as a scenario by reports',
  report: 'used by reports',
  'assumption set': 'pinned by assumption sets',
}

/** "it is bound by dataset versions ('loans.details' v3); referenced by mapping sets ('loan.purpose')". */
function phraseRefs(refs: UsageReference[]) {
  const byKind = new Map<UsageKind, string[]>()
  for (const r of refs) byKind.set(r.kind, [...(byKind.get(r.kind) ?? []), r.label ?? r.name])
  return [...byKind].map(([kind, names]) => `${REF_PHRASE[kind]} (${quote(names)})`)
}

function result(subject: string, reasons: string[]): DeleteCheck {
  return reasons.length ? { allowed: false, reasons, message: `${subject} cannot be deleted: ${reasons.join('; ')}.` } : { allowed: true, reasons: [], message: null }
}

export function checkDelete(db: Db, target: DeleteTarget): DeleteCheck {
  switch (target.type) {
    case 'dimension': {
      const d = db.dimensions.find((x) => x.id === target.dimensionId)
      if (!d) return result('This dimension', ['it no longer exists'])
      if (d.origin === 'system') return result(`Dimension '${d.displayName}'`, ['it is a system dimension'])
      const reasons: string[] = []
      if (!d.isRemovable) reasons.push('it is marked not removable')
      reasons.push(...phraseRefs(db.usage[d.id] ?? []))
      return result(`Dimension '${d.displayName}'`, reasons)
    }
    case 'version': {
      const v = db.versions.find((x) => x.id === target.versionId)
      if (!v) return result('This version', ['it no longer exists'])
      const reasons: string[] = []
      if (v.status === 'published') reasons.push('it is the live version')
      const pins = (db.usage[v.dimensionId] ?? []).filter((r) => r.pinsVersion === v.versionNumber)
      reasons.push(...phraseRefs(pins))
      return result(`Version ${v.versionNumber}`, reasons)
    }
    case 'member': {
      const v = db.versions.find((x) => x.id === target.versionId)
      const m = db.members.find((x) => x.id === target.memberId)
      if (!v || !m) return result('This member', ['it no longer exists'])
      const d = db.dimensions.find((x) => x.id === v.dimensionId)!
      const profile = d.profileKey ? db.profiles.find((p) => p.key === d.profileKey) : null
      const reasons: string[] = []
      if (profile?.requiredMemberKeys.includes(m.key)) reasons.push(`it is required by the ${profile.displayName} profile`)
      const kids = db.members.filter((x) => x.versionId === v.id && x.parentId === m.id)
      if (kids.length) reasons.push(`it has child members (${kids.length === 1 ? `'${kids[0].key}'` : `${kids.length}: ${quote(kids.slice(0, 3).map((k) => k.key))}${kids.length > 3 ? ', …' : ''}`})`)
      const bands = (db.bands[v.id] ?? []).filter((b) => b.memberId === m.id)
      if (bands.length) reasons.push(`it is assigned by range bands (${bands.map((b) => b.ordinal).join(', ')})`)
      // Only the live version is read by other objects; drafts may drop named keys and fail validation instead.
      if (v.status === 'published') {
        const named = (db.usage[d.id] ?? []).filter((r) => r.memberKeys?.includes(m.key))
        if (named.length) reasons.push(`it is named by ${named.map((r) => `${r.kind} ${r.label ?? `'${r.name}'`} (${r.detail})`).join(', ')}`)
      }
      return result(`Member '${m.key}'`, reasons)
    }
    case 'level': {
      const l = db.levels.find((x) => x.id === target.levelId)
      if (!l) return result('This level', ['it no longer exists'])
      const used = db.members.filter((m) => m.levelId === l.id).length
      return result(`Level '${l.key}'`, used ? [`${used} ${used === 1 ? 'member uses' : 'members use'} it`] : [])
    }
    case 'role': {
      const r = db.roles.find((x) => x.id === target.roleId)
      if (!r) return result('This role', ['it no longer exists'])
      const d = db.dimensions.find((x) => x.id === r.dimensionId)!
      const profile = d.profileKey ? db.profiles.find((p) => p.key === d.profileKey) : null
      const reasons: string[] = []
      if (profile?.requiredRoleKeys.includes(r.key)) reasons.push(`it is required by the ${profile.displayName} profile`)
      reasons.push(...phraseRefs((db.usage[d.id] ?? []).filter((u) => u.roleKey === r.key)))
      return result(`Role '${r.key}'`, reasons)
    }
  }
}

export function validate(db: Db, versionId: string): Violation[] {
  const v = db.versions.find((x) => x.id === versionId)
  if (!v) return []
  const d = db.dimensions.find((x) => x.id === v.dimensionId)!
  const profile = d.profileKey ? db.profiles.find((p) => p.key === d.profileKey) : null
  const members = db.members.filter((m) => m.versionId === v.id)
  const levels = db.levels.filter((l) => l.versionId === v.id)
  const out: Violation[] = []

  const dupes = <T extends { id: string; key: string }>(items: T[]) => {
    const seen = new Map<string, T[]>()
    for (const i of items) seen.set(i.key, [...(seen.get(i.key) ?? []), i])
    return [...seen].filter(([, list]) => list.length > 1)
  }
  for (const [key, list] of dupes(members)) out.push({ code: 'duplicateMemberKey', tab: 'members', itemId: list[1].id, message: `Duplicate member key '${key}' (${list.length} members: ${list.map((m) => m.displayName).join(', ')}).` })
  for (const [key, list] of dupes(levels)) out.push({ code: 'duplicateLevelKey', tab: 'levels', itemId: list[1].id, message: `Duplicate level key '${key}'.` })

  const byId = new Map(members.map((m) => [m.id, m]))
  for (const cycle of findCycles(members)) {
    const keys = cycle.map((id) => byId.get(id)!.key)
    out.push({ code: 'cycle', tab: 'members', itemId: cycle[0], message: `Parent links form a cycle: ${[...keys, keys[0]].join(' → ')}.` })
  }

  if (d.kind === 'range') {
    if (!v.rangeDataType) out.push({ code: 'rangeType', tab: 'properties', message: 'Set the range data type before publishing.' })
    else
      for (const issue of analyzeBands(db.bands[v.id] ?? [], v.rangeDataType, new Set(byId.keys()))) {
        if (issue.kind === 'gap') continue
        const code = issue.kind === 'overlap' ? 'bandOverlap' : issue.kind === 'order' ? 'bandOrder' : issue.kind === 'member' ? 'bandMember' : 'bandOpenBound'
        out.push({ code, tab: 'bands', itemId: String(issue.ordinals[0]), message: issue.message })
      }
  }

  if (profile) {
    const keys = new Set(members.map((m) => m.key))
    for (const k of profile.requiredMemberKeys) if (!keys.has(k)) out.push({ code: 'requiredMember', tab: 'members', message: `Required member '${k}' is missing (${profile.displayName} profile).` })
    const roleKeys = new Set(db.roles.filter((r) => r.dimensionId === d.id).map((r) => r.key))
    for (const k of profile.requiredRoleKeys) if (!roleKeys.has(k)) out.push({ code: 'requiredRole', tab: 'roles', message: `Required role '${k}' is missing (${profile.displayName} profile).` })
  }

  const present = new Set(members.map((m) => m.key))
  const missing = new Map<string, string[]>()
  for (const ref of db.usage[d.id] ?? []) for (const k of ref.memberKeys ?? []) if (!present.has(k)) missing.set(k, [...(missing.get(k) ?? []), `${ref.kind} ${ref.name}`])
  if (missing.size)
    out.push({
      code: 'namedKeyMissing',
      tab: 'usage',
      message: `Keys other objects name are missing from v${v.versionNumber}: ${[...missing].map(([k, by]) => `${k} (${by.join(', ')})`).join('; ')}.`,
    })
  return out
}
