import { createSnapshotChangeSet } from '@/lib/api/changes'
import {
  KEY_PATTERN,
  type ApiResult,
  type Dimension,
  type DimensionDetail,
  type DimensionListItem,
  type DimensionProfile,
  type DimensionsApi,
  type DimensionVersion,
  type DimensionLevel,
  type DimensionMember,
  type DimensionMemberPosition,
  type Problem,
  type RangeBand,
  type DimensionValidationReport,
  type DimensionVersionSummary,
} from '../contract'
import { descendantIds, indexTree } from '../tree'
import { createSeed, type Db } from './data'
import { checkDelete, validate } from './rules'

const LATENCY = 90
const wait = () => new Promise((r) => setTimeout(r, LATENCY))
const clone = <T,>(v: T): T => structuredClone(v)
const today = () => new Date().toISOString().slice(0, 10)
let seq = 0
const newId = (prefix: string) => `${prefix}-${Date.now().toString(36)}-${(seq++).toString(36)}`

const PROBLEM_TYPE: Record<Problem['status'], string> = {
  400: 'urn:qe:problem:invalid-input',
  401: 'urn:qe:problem:unauthorized',
  403: 'urn:qe:problem:forbidden',
  404: 'urn:qe:problem:not-found',
  409: 'urn:qe:problem:conflict',
  422: 'urn:qe:problem:rules-violated',
  500: 'urn:qe:problem:server-error',
}

const ok = <T,>(data: T): ApiResult<T> => ({ ok: true, data: clone(data) })
const fail = (title: string, errors: Record<string, string[]> = {}, status: Problem['status'] = 400, detail?: string): ApiResult<never> => ({
  ok: false,
  problem: { type: PROBLEM_TYPE[status], title, status, errors, detail },
})
const notFound = (what: string) => fail(`${what} not found`, {}, 404)

/**
 * Seeds predate the no-draft model. Fold any open draft into the live line: an existing live version is frozen
 * and the draft becomes live; a lone draft simply goes live.
 */
function normalizeVersions(db: Db) {
  for (const d of db.dimensions) {
    const versions = db.versions.filter((v) => v.dimensionId === d.id).sort((a, b) => a.versionNumber - b.versionNumber)
    const open = versions.filter((v) => v.status === 'draft' || v.status === 'validated').at(-1)
    if (!open) continue
    for (const v of versions) {
      if (v === open) continue
      if (v.status === 'published' || v.status === 'draft' || v.status === 'validated') {
        v.status = 'retired'
        v.retiredAt = v.retiredAt ?? open.createdAt
        v.supersededBy = open.versionNumber
      }
    }
    open.status = 'published'
    open.publishedAt = open.publishedAt ?? open.createdAt
  }
}

export function createMockDimensionsApi(seed: Db = createSeed()): DimensionsApi {
  const db = seed
  normalizeVersions(db)

  function provisionSystemDimension(p: DimensionProfile) {
    const dimensionId = newId('dim')
    const name = (k: string) => p.seedNames?.[k] ?? k
    db.dimensions.push({ id: dimensionId, key: p.displayName.replace(/\s+/g, ''), displayName: p.displayName, kind: p.kind, origin: 'system', purpose: 'analytical', ownerReport: null, profileKey: p.key, isEnabled: true, isRemovable: false })
    for (const k of p.requiredRoleKeys) db.roles.push({ id: newId('role'), dimensionId, key: k, displayName: name(k), description: null })
    const versionId = newId('ver')
    db.versions.push({ id: versionId, dimensionId, versionNumber: 1, status: 'published', rangeDataType: null, createdAt: today(), publishedAt: today(), retiredAt: null, supersededBy: null, freezeNote: null })
    p.requiredMemberKeys.forEach((k, i) =>
      db.members.push({ id: newId('mbr'), versionId, key: k, displayName: name(k), description: null, parentId: null, levelId: null, isAssignable: true, sortOrder: i + 1, properties: {} }),
    )
    db.usage[dimensionId] = []
    return { dimensionId, versionId }
  }

  // System dimension types always exist: every manager-visible single-instance type is provisioned up front.
  for (const p of db.profiles) {
    if (p.visibility === 'manager' && p.cardinality === 'one' && !db.dimensions.some((d) => d.profileKey === p.key)) provisionSystemDimension(p)
  }

  const profileOf = (d: Dimension): DimensionProfile | null => (d.profileKey ? (db.profiles.find((p) => p.key === d.profileKey) ?? null) : null)
  const policyOf = (d: Dimension) => profileOf(d)?.editPolicy ?? null
  const dimOf = (versionId: string) => {
    const v = db.versions.find((x) => x.id === versionId)
    return v ? { v, d: db.dimensions.find((x) => x.id === v.dimensionId)! } : null
  }
  const resolvable = (d: Dimension) => profileOf(d)?.visibility !== 'hidden'
  const reachable = (d: Dimension) => d.purpose === 'analytical' && resolvable(d)

  function summary(v: DimensionVersion): DimensionVersionSummary {
    return {
      id: v.id,
      versionNumber: v.versionNumber,
      status: v.status,
      publishedAt: v.publishedAt,
      retiredAt: v.retiredAt,
      supersededBy: v.supersededBy,
      freezeNote: v.freezeNote,
      memberCount: db.members.filter((m) => m.versionId === v.id).length,
    }
  }
  function listItem(d: Dimension): DimensionListItem {
    const versions = db.versions
      .filter((v) => v.dimensionId === d.id)
      .sort((a, b) => a.versionNumber - b.versionNumber)
      .map(summary)
    return {
      ...d,
      profile: profileOf(d),
      roles: db.roles.filter((r) => r.dimensionId === d.id),
      versions,
      publishedMemberCount: versions.find((v) => v.status === 'published')?.memberCount ?? null,
    }
  }

  /** Writes allowed only on the live version, and never under a locked profile. */
  function editGuard(versionId: string) {
    const found = dimOf(versionId)
    if (!found) return { error: notFound('Version') }
    const { v, d } = found
    if (policyOf(d) === 'locked') return { error: fail('Read-only', {}, 409, `${d.displayName} is managed by ${profileOf(d)!.owner}.`) }
    if (v.status !== 'published') return { error: fail('Read-only version', {}, 409, `Version ${v.versionNumber} is frozen.`) }
    return { v, d }
  }

  const keyErrors = (key: string) => (!key ? ['Enter a key.'] : !KEY_PATTERN.test(key) ? ['Start with a letter, then use letters, digits or underscores.'] : null)
  const renumber = (list: DimensionMember[]) => list.forEach((m, i) => (m.sortOrder = i + 1))
  const siblings = (versionId: string, parentId: string | null) =>
    db.members.filter((m) => m.versionId === versionId && m.parentId === parentId).sort((a, b) => a.sortOrder - b.sortOrder)

  /** Delete blockers for removing a set of members together; children inside the set don't block their parent. */
  function memberBlockers(versionId: string, removing: DimensionMember[]) {
    const ids = new Set(removing.map((m) => m.id))
    const out: string[] = []
    for (const m of removing) {
      const kidsStay = db.members.some((x) => x.versionId === versionId && x.parentId === m.id && !ids.has(x.id))
      const reasons = checkDelete(db, { type: 'member', versionId, memberId: m.id }).reasons.filter((r) => kidsStay || r.code !== 'has_children')
      if (reasons.length) out.push(`'${m.key}' — ${reasons.map((r) => r.message).join(', ')}`)
    }
    return out
  }

  function copyVersionContent(fromId: string, toId: string) {
    const levelMap = new Map<string, string>()
    for (const l of db.levels.filter((x) => x.versionId === fromId)) {
      const id = newId('lvl')
      levelMap.set(l.id, id)
      db.levels.push({ ...clone(l), id, versionId: toId })
    }
    const memberMap = new Map<string, string>()
    const source = db.members.filter((x) => x.versionId === fromId)
    for (const m of source) memberMap.set(m.id, newId('mbr'))
    for (const m of source)
      db.members.push({
        ...clone(m),
        id: memberMap.get(m.id)!,
        versionId: toId,
        parentId: m.parentId ? (memberMap.get(m.parentId) ?? null) : null,
        levelId: m.levelId ? (levelMap.get(m.levelId) ?? null) : null,
      })
    if (db.bands[fromId]) db.bands[toId] = db.bands[fromId].map((b) => ({ ...b, memberId: memberMap.get(b.memberId) ?? b.memberId }))
  }

  function report(versionId: string): DimensionValidationReport {
    return { versionId, checkedAt: new Date().toISOString(), violations: validate(db, versionId) }
  }

  return {
    ...createSnapshotChangeSet(
      () => db,
      (snapshot) => Object.assign(db, snapshot),
    ),
    async listDimensions(origin) {
      await wait()
      return clone(
        db.dimensions
          .filter((d) => d.origin === origin && reachable(d) && (origin === 'user' || profileOf(d)?.visibility === 'manager'))
          .map(listItem)
          .sort((a, b) => a.displayName.localeCompare(b.displayName)),
      )
    },

    async listAllDimensions() {
      await wait()
      return clone(db.dimensions.filter(resolvable).map(listItem))
    },

    async listAvailableProfiles() {
      await wait()
      return clone(
        db.profiles
          .filter((p) => p.visibility === 'manager' && p.cardinality === 'one')
          .map((profile) => ({ profile, dimensionId: db.dimensions.find((d) => d.profileKey === profile.key)?.id ?? null })),
      )
    },

    async getDimension(id) {
      await wait()
      const d = db.dimensions.find((x) => x.id === id)
      if (!d || !reachable(d)) return notFound('Dimension')
      const detail: DimensionDetail = { ...listItem(d), usage: db.usage[d.id] ?? [] }
      return ok(detail)
    },

    async getVersion(versionId) {
      await wait()
      const found = dimOf(versionId)
      if (!found || !resolvable(found.d)) return notFound('Version')
      return ok({
        version: found.v,
        levels: db.levels.filter((l) => l.versionId === versionId).sort((a, b) => a.ordinal - b.ordinal),
        members: db.members.filter((m) => m.versionId === versionId),
        bands: [...(db.bands[versionId] ?? [])].sort((a, b) => a.ordinal - b.ordinal),
      })
    },

    async createDimension(input) {
      await wait()
      const errors: Record<string, string[]> = {}
      const key = input.key.trim()
      const ke = keyErrors(key)
      if (ke) errors.key = ke
      else if (db.dimensions.some((d) => d.key.toLowerCase() === key.toLowerCase())) errors.key = [`Another dimension already uses the key '${key}'.`]
      if (!input.displayName.trim()) errors.displayName = ['Enter a display name.']
      if (input.firstRole && input.kind !== 'time') {
        const rk = keyErrors(input.firstRole.key.trim())
        if (rk) errors['firstRole.key'] = rk
        else if (db.roles.some((r) => r.key.toLowerCase() === input.firstRole!.key.trim().toLowerCase())) errors['firstRole.key'] = [`The role key '${input.firstRole.key.trim()}' is already used by another dimension.`]
        if (!input.firstRole.displayName.trim()) errors['firstRole.displayName'] = ['Enter a role name.']
      }
      if (Object.keys(errors).length) return fail('Check the highlighted fields', errors)

      const dimensionId = newId('dim')
      db.dimensions.push({ id: dimensionId, key, displayName: input.displayName.trim(), kind: input.kind, origin: 'user', purpose: 'analytical', ownerReport: null, profileKey: null, isEnabled: input.isEnabled, isRemovable: input.isRemovable })
      if (input.firstRole && input.kind !== 'time') db.roles.push({ id: newId('role'), dimensionId, key: input.firstRole.key.trim(), displayName: input.firstRole.displayName.trim(), description: null })
      const versionId = newId('ver')
      db.versions.push({ id: versionId, dimensionId, versionNumber: 1, status: 'published', rangeDataType: input.kind === 'range' ? input.rangeDataType : null, createdAt: today(), publishedAt: today(), retiredAt: null, supersededBy: null, freezeNote: null })
      db.usage[dimensionId] = []
      return ok({ dimensionId, versionId })
    },

    async defineSystemDimension(profileKey) {
      await wait()
      const p = db.profiles.find((x) => x.key === profileKey && x.visibility === 'manager')
      if (!p) return notFound('Profile')
      if (p.cardinality === 'one' && db.dimensions.some((d) => d.profileKey === p.key)) return fail(`${p.displayName} is already defined`, {}, 409)
      return ok(provisionSystemDimension(p))
    },

    async updateDimension(id, patch) {
      await wait()
      const d = db.dimensions.find((x) => x.id === id)
      if (!d) return notFound('Dimension')
      if (policyOf(d) === 'locked') return fail('Read-only', {}, 409, `${d.displayName} is managed by ${profileOf(d)!.owner}.`)
      if (patch.displayName !== undefined && !patch.displayName.trim()) return fail('Check the highlighted fields', { displayName: ['Enter a display name.'] })
      if (patch.isRemovable !== undefined && d.origin === 'system') return fail('System dimensions are never removable', {}, 409)
      Object.assign(d, { ...patch, displayName: patch.displayName?.trim() ?? d.displayName })
      return ok(d)
    },

    async checkDelete(target) {
      await wait()
      return clone(checkDelete(db, target))
    },

    async deleteDimension(id) {
      await wait()
      const c = checkDelete(db, { type: 'dimension', dimensionId: id })
      if (!c.allowed) return fail('Cannot delete', {}, 409, c.message ?? undefined)
      const versionIds = new Set(db.versions.filter((v) => v.dimensionId === id).map((v) => v.id))
      db.dimensions = db.dimensions.filter((d) => d.id !== id)
      db.versions = db.versions.filter((v) => !versionIds.has(v.id))
      db.members = db.members.filter((m) => !versionIds.has(m.versionId))
      db.levels = db.levels.filter((l) => !versionIds.has(l.versionId))
      db.roles = db.roles.filter((r) => r.dimensionId !== id)
      for (const v of versionIds) delete db.bands[v]
      delete db.usage[id]
      return ok(null)
    },

    async updateVersion(versionId, patch) {
      await wait()
      const g = editGuard(versionId)
      if ('error' in g) return g.error!
      if (g.d.kind !== 'range') return fail('Only range dimensions have a range data type', {}, 409)
      if (g.v.rangeDataType !== patch.rangeDataType && db.bands[versionId]?.length)
        db.bands[versionId] = db.bands[versionId].map((b) => ({ ...b, lower: null, upper: null }))
      g.v.rangeDataType = patch.rangeDataType
      return ok(g.v)
    },

    async checkVersion(versionId) {
      await wait()
      if (!dimOf(versionId)) return notFound('Version')
      return ok(report(versionId))
    },

    async freezeVersion(versionId, note) {
      await wait()
      const g = editGuard(versionId)
      if ('error' in g) return g.error!
      const { v, d } = g
      const r = report(versionId)
      if (r.violations.length) return ok({ report: r, live: null })
      const versions = db.versions.filter((x) => x.dimensionId === d.id)
      const next: DimensionVersion = {
        id: newId('ver'),
        dimensionId: d.id,
        versionNumber: Math.max(...versions.map((x) => x.versionNumber)) + 1,
        status: 'published',
        rangeDataType: v.rangeDataType,
        createdAt: today(),
        publishedAt: today(),
        retiredAt: null,
        supersededBy: null,
        freezeNote: null,
      }
      v.status = 'retired'
      v.retiredAt = today()
      v.supersededBy = next.versionNumber
      v.freezeNote = note?.trim() || null
      db.versions.push(next)
      copyVersionContent(v.id, next.id)
      for (const u of db.usage[d.id] ?? []) if (u.kind === 'assumption_set' && u.name === 'active') {
        u.pinsVersion = next.versionNumber
        u.detail = `active → v${next.versionNumber}`
      }
      return ok({ report: r, live: summary(next) })
    },

    async deleteVersion(versionId) {
      await wait()
      const found = dimOf(versionId)
      if (!found) return notFound('Version')
      if (found.v.status === 'published') return fail('Cannot delete', {}, 409, `v${found.v.versionNumber} is the live version.`)
      const c = checkDelete(db, { type: 'version', versionId })
      if (!c.allowed) return fail('Cannot delete', {}, 409, c.message ?? undefined)
      if (db.versions.filter((x) => x.dimensionId === found.d.id).length === 1) return fail('Cannot delete', {}, 409, `Version ${found.v.versionNumber} is the only version. Delete the dimension instead.`)
      db.versions = db.versions.filter((x) => x.id !== versionId)
      db.members = db.members.filter((m) => m.versionId !== versionId)
      db.levels = db.levels.filter((l) => l.versionId !== versionId)
      delete db.bands[versionId]
      return ok(null)
    },

    async saveMember(versionId, input) {
      await wait()
      const g = editGuard(versionId)
      if ('error' in g) return g.error!
      const existing = input.id ? db.members.find((m) => m.id === input.id && m.versionId === versionId) : null
      if (input.id && !existing) return notFound('Member')
      const errors: Record<string, string[]> = {}
      const key = existing ? existing.key : input.key.trim()
      if (!existing) {
        const ke = keyErrors(key)
        if (ke) errors.key = ke
        else if (db.members.some((m) => m.versionId === versionId && m.key === key)) errors.key = [`Another member in this version uses '${key}'.`]
      }
      if (!input.displayName.trim()) errors.displayName = ['Enter a display name.']
      const parentId = g.d.kind === 'hierarchical' ? input.parentId : null
      if (parentId) {
        const index = indexTree(db.members.filter((m) => m.versionId === versionId))
        if (!index.byId.has(parentId)) errors.parentId = ['Choose a member in this version.']
        else if (existing && (parentId === existing.id || descendantIds(index, existing.id).has(parentId))) errors.parentId = ['A member cannot sit under itself or its descendants.']
      }
      const profile = profileOf(g.d)
      for (const f of profile?.memberPropertySchema ?? []) {
        const val = input.properties[f.key]
        if (f.required && (val === undefined || val === null || val === '')) errors[`properties.${f.key}`] = [`${f.label} is required.`]
      }
      if (Object.keys(errors).length) return fail('Check the highlighted fields', errors)

      if (existing) {
        const moved = existing.parentId !== parentId
        Object.assign(existing, {
          displayName: input.displayName.trim(),
          description: input.description?.trim() || null,
          levelId: input.levelId,
          isAssignable: input.isAssignable,
          properties: input.properties,
          parentId,
        })
        if (moved) existing.sortOrder = siblings(versionId, parentId).length + 1
        if (input.sortOrder !== null) existing.sortOrder = input.sortOrder
        return ok(existing)
      }
      const m: DimensionMember = {
        id: newId('mbr'),
        versionId,
        key,
        displayName: input.displayName.trim(),
        description: input.description?.trim() || null,
        parentId,
        levelId: input.levelId,
        isAssignable: input.isAssignable,
        sortOrder: input.sortOrder ?? siblings(versionId, parentId).length + 1,
        properties: input.properties,
      }
      db.members.push(m)
      return ok(m)
    },

    async deleteMember(versionId, memberId) {
      await wait()
      const g = editGuard(versionId)
      if ('error' in g) return g.error!
      const c = checkDelete(db, { type: 'member', versionId, memberId })
      if (!c.allowed) return fail('Cannot delete', {}, 409, c.message ?? undefined)
      const m = db.members.find((x) => x.id === memberId)!
      db.members = db.members.filter((x) => x.id !== memberId)
      renumber(siblings(versionId, m.parentId))
      return ok(null)
    },

    async moveMember(versionId, memberId, to) {
      await wait()
      const g = editGuard(versionId)
      if ('error' in g) return g.error!
      const m = db.members.find((x) => x.id === memberId && x.versionId === versionId)
      if (!m) return notFound('Member')
      const parentId = g.d.kind === 'hierarchical' ? to.parentId : null
      if (parentId) {
        const index = indexTree(db.members.filter((x) => x.versionId === versionId))
        if (parentId === m.id || descendantIds(index, m.id).has(parentId)) return fail('A member cannot move under itself or its descendants', {}, 422)
      }
      const fromList = siblings(versionId, m.parentId)
      const previous: DimensionMemberPosition = { parentId: m.parentId, index: fromList.indexOf(m) }
      const remaining = fromList.filter((x) => x.id !== m.id)
      renumber(remaining)
      m.parentId = parentId
      const target = siblings(versionId, parentId).filter((x) => x.id !== m.id)
      target.splice(Math.max(0, Math.min(to.index, target.length)), 0, m)
      renumber(target)
      return ok(previous)
    },

    async importMembers(versionId, rows, mode = 'merge') {
      await wait()
      const g = editGuard(versionId)
      if ('error' in g) return g.error!
      const hierarchical = g.d.kind === 'hierarchical'
      const existing = db.members.filter((m) => m.versionId === versionId)
      const byKey = new Map(existing.map((m) => [m.key, m]))
      const imported = new Set(rows.map((r) => r.key))
      const errors: Record<string, string[]> = {}
      const batch = new Set<string>()
      rows.forEach((r, i) => {
        const ke = keyErrors(r.key)
        if (ke) errors[`rows.${i}`] = ke
        else if (batch.has(r.key)) errors[`rows.${i}`] = [`Key '${r.key}' appears more than once.`]
        else if (r.parentKey && hierarchical) {
          const parentKnown = imported.has(r.parentKey) || (mode === 'merge' && byKey.has(r.parentKey))
          if (r.parentKey === r.key) errors[`rows.${i}`] = ['A member cannot be its own parent.']
          else if (!parentKnown) errors[`rows.${i}`] = [mode === 'replace' ? `Parent '${r.parentKey}' is not in the import.` : `Parent '${r.parentKey}' is not in this version or the import.`]
        }
        batch.add(r.key)
      })
      if (Object.keys(errors).length) return fail('Some rows cannot be imported', errors)

      const removing = mode === 'replace' ? existing.filter((m) => !imported.has(m.key)) : []
      const blocked = memberBlockers(versionId, removing)
      if (blocked.length) return fail('Cannot replace members', {}, 409, `These members would be removed but are still needed: ${blocked.join('; ')}.`)

      // Final parent of every key after the import, to reject cycles introduced by re-parenting.
      const parentOf = new Map<string, string | null>()
      for (const m of existing) if (!removing.includes(m)) parentOf.set(m.key, m.parentId ? (existing.find((p) => p.id === m.parentId)?.key ?? null) : null)
      for (const r of rows) parentOf.set(r.key, hierarchical ? r.parentKey : null)
      for (const [i, r] of rows.entries()) {
        const seen = new Set<string>([r.key])
        for (let p = parentOf.get(r.key) ?? null; p; p = parentOf.get(p) ?? null) {
          if (seen.has(p)) {
            errors[`rows.${i}`] = [`Parent '${r.parentKey}' would create a cycle.`]
            break
          }
          seen.add(p)
        }
      }
      if (Object.keys(errors).length) return fail('Some rows cannot be imported', errors)

      const removedIds = new Set(removing.map((m) => m.id))
      db.members = db.members.filter((m) => !removedIds.has(m.id))
      const added: DimensionMember[] = []
      const updated: DimensionMember[] = []
      const ids = new Map([...byKey].filter(([, m]) => !removedIds.has(m.id)).map(([k, m]) => [k, m.id]))
      for (const r of rows) {
        const m = byKey.get(r.key)
        if (m) {
          m.displayName = r.displayName || r.key
          updated.push(m)
        } else {
          const id = newId('mbr')
          ids.set(r.key, id)
          added.push({ id, versionId, key: r.key, displayName: r.displayName || r.key, description: null, parentId: null, levelId: null, isAssignable: true, sortOrder: 0, properties: {} })
        }
      }
      for (const r of rows) {
        const m = byKey.get(r.key) ?? added.find((a) => a.key === r.key)!
        m.parentId = hierarchical && r.parentKey ? (ids.get(r.parentKey) ?? null) : null
      }
      for (const m of added) {
        m.sortOrder = db.members.filter((x) => x.versionId === versionId && x.parentId === m.parentId).length + 1
        db.members.push(m)
      }
      const parents = new Set(db.members.filter((m) => m.versionId === versionId).map((m) => m.parentId))
      for (const p of parents) renumber(siblings(versionId, p))
      return ok({ added, updated, removed: removing.length })
    },

    async clearMembers(versionId) {
      await wait()
      const g = editGuard(versionId)
      if ('error' in g) return g.error!
      const all = db.members.filter((m) => m.versionId === versionId)
      const blocked = memberBlockers(versionId, all)
      if (blocked.length) return fail('Cannot clear members', {}, 409, `Some members are still needed: ${blocked.join('; ')}.`)
      db.members = db.members.filter((m) => m.versionId !== versionId)
      return ok({ removed: all.length })
    },

    async saveLevel(versionId, input) {
      await wait()
      const g = editGuard(versionId)
      if ('error' in g) return g.error!
      const levels = db.levels.filter((l) => l.versionId === versionId)
      const existing = input.id ? levels.find((l) => l.id === input.id) : null
      const errors: Record<string, string[]> = {}
      const key = existing ? existing.key : input.key.trim()
      if (!existing) {
        const ke = keyErrors(key)
        if (ke) errors.key = ke
        else if (levels.some((l) => l.key === key)) errors.key = [`Another level uses '${key}'.`]
      }
      if (!input.displayName.trim()) errors.displayName = ['Enter a display name.']
      if (Object.keys(errors).length) return fail('Check the highlighted fields', errors)
      const periodicity = g.d.kind === 'time' ? input.periodicity : null
      if (existing) {
        Object.assign(existing, { displayName: input.displayName.trim(), isLeaf: input.isLeaf, periodicity })
        return ok(existing)
      }
      const l: DimensionLevel = { id: newId('lvl'), versionId, key, displayName: input.displayName.trim(), ordinal: levels.length + 1, isLeaf: input.isLeaf, periodicity }
      db.levels.push(l)
      return ok(l)
    },

    async deleteLevel(versionId, levelId) {
      await wait()
      const g = editGuard(versionId)
      if ('error' in g) return g.error!
      const c = checkDelete(db, { type: 'level', versionId, levelId })
      if (!c.allowed) return fail('Cannot delete', {}, 409, c.message ?? undefined)
      db.levels = db.levels.filter((l) => l.id !== levelId)
      db.levels.filter((l) => l.versionId === versionId).sort((a, b) => a.ordinal - b.ordinal).forEach((l, i) => (l.ordinal = i + 1))
      return ok(null)
    },

    async reorderLevels(versionId, orderedIds) {
      await wait()
      const g = editGuard(versionId)
      if ('error' in g) return g.error!
      orderedIds.forEach((id, i) => {
        const l = db.levels.find((x) => x.id === id && x.versionId === versionId)
        if (l) l.ordinal = i + 1
      })
      return ok(db.levels.filter((l) => l.versionId === versionId).sort((a, b) => a.ordinal - b.ordinal))
    },

    async saveBands(versionId, bands) {
      await wait()
      const g = editGuard(versionId)
      if ('error' in g) return g.error!
      if (g.d.kind !== 'range') return fail('Only range dimensions have bands', {}, 409)
      if (!g.v.rangeDataType) return fail('Set the range data type in Settings before adding bands', {}, 409)
      const errors: Record<string, string[]> = {}
      bands.forEach((b, i) => {
        if (!b.memberId) errors[`bands.${i}.memberId`] = ['Choose a member.']
      })
      if (Object.keys(errors).length) return fail('Check the highlighted bands', errors)
      const saved: RangeBand[] = bands.map((b, i) => ({ ...b, ordinal: i + 1 }))
      db.bands[versionId] = saved
      return ok(saved)
    },

    async saveRole(dimensionId, input) {
      await wait()
      const d = db.dimensions.find((x) => x.id === dimensionId)
      if (!d) return notFound('Dimension')
      const profile = profileOf(d)
      if (profile?.editPolicy === 'locked') return fail('Read-only', {}, 409, `${d.displayName} is managed by ${profile.owner}.`)
      if (d.kind === 'time') return fail('Time dimensions have no roles', {}, 409)
      const existing = input.id ? db.roles.find((r) => r.id === input.id && r.dimensionId === dimensionId) : null
      if (!existing && profile?.singleRole) return fail('Cannot add a role', {}, 409, `The ${profile.displayName} profile allows exactly one role.`)
      const errors: Record<string, string[]> = {}
      const key = existing ? existing.key : input.key.trim()
      if (!existing) {
        const ke = keyErrors(key)
        if (ke) errors.key = ke
        else if (db.roles.some((r) => r.key.toLowerCase() === key.toLowerCase())) errors.key = [`The role key '${key}' is already used by a dimension.`]
      }
      if (!input.displayName.trim()) errors.displayName = ['Enter a display name.']
      if (Object.keys(errors).length) return fail('Check the highlighted fields', errors)
      if (existing) {
        Object.assign(existing, { displayName: input.displayName.trim(), description: input.description?.trim() || null })
        return ok(existing)
      }
      const r = { id: newId('role'), dimensionId, key, displayName: input.displayName.trim(), description: input.description?.trim() || null }
      db.roles.push(r)
      return ok(r)
    },

    async deleteRole(roleId) {
      await wait()
      const r = db.roles.find((x) => x.id === roleId)
      if (!r) return notFound('Role')
      const d = db.dimensions.find((x) => x.id === r.dimensionId)!
      if (policyOf(d) === 'locked') return fail('Read-only', {}, 409)
      const c = checkDelete(db, { type: 'role', roleId })
      if (!c.allowed) return fail('Cannot delete', {}, 409, c.message ?? undefined)
      db.roles = db.roles.filter((x) => x.id !== roleId)
      return ok(null)
    },
  }
}
