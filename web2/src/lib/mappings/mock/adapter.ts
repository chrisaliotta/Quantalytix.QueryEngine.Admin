import type { DimensionsApi } from '@/lib/dimensions/contract'
import {
  allowedModes,
  canHaveBridges,
  canHaveMappingSets,
  type ApiResult,
  type AssumptionPin,
  type Bridge,
  type BridgeListItem,
  type BridgeMapping,
  type BridgeMappingInput,
  type BridgeVersion,
  type BridgeVersionSummary,
  type DeleteCheck,
  type DimensionRef,
  type EntryInput,
  type MappingEntry,
  type MappingSetListItem,
  type MappingsApi,
  type MappingVersion,
  type MappingVersionSummary,
  type MemberRef,
  type ProblemDetails,
  type ValidationReport,
  type Violation,
} from '../contract'
import { allocationSummary, allocationIssueCount, bridgeViolations, indexMembers, mappingSetViolations, sumsToOne } from '../rules'
import { createSeed, type Db } from './data'

const KEY_RE = /^[a-z][a-z0-9_.]*$/
const now = () => new Date().toISOString()
const today = () => now().slice(0, 10)
const delay = () => new Promise((r) => setTimeout(r, 120))
const uid = (p: string) => `${p}-${Math.random().toString(36).slice(2, 9)}`

const ok = <T,>(data: T): ApiResult<T> => ({ ok: true, data })
function fail<T>(status: number, title: string, detail?: string, errors: Record<string, string[]> = {}): ApiResult<T> {
  const problem: ProblemDetails = { status, title, detail, errors }
  return { ok: false, problem }
}
const notFound = <T,>(what: string) => fail<T>(404, `${what} not found`)
const clone = <T,>(v: T): T => structuredClone(v)

export function createMockMappingsApi(dims: DimensionsApi, seed: Db = createSeed()): MappingsApi {
  const db = seed

  /* ───────────── dimension resolution (through the dimensions port) ───────────── */

  async function dimensionRefs(): Promise<DimensionRef[]> {
    return (await dims.listAllDimensions()).map((d) => {
      const pub = d.versions.find((v) => v.status === 'published')
      return { id: d.id, key: d.key, displayName: d.displayName, kind: d.kind, purpose: d.purpose, publishedVersionId: pub?.id ?? null, publishedVersionNumber: pub?.versionNumber ?? null }
    })
  }

  async function refOf(id: string): Promise<DimensionRef | null> {
    return (await dimensionRefs()).find((d) => d.id === id) ?? null
  }

  async function publishedMembers(dimensionId: string): Promise<MemberRef[] | null> {
    const ref = await refOf(dimensionId)
    if (!ref?.publishedVersionId) return null
    const r = await dims.getVersion(ref.publishedVersionId)
    if (!r.ok) return null
    const keyById = new Map(r.data.members.map((m) => [m.id, m.key]))
    return [...r.data.members]
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map((m) => ({ key: m.key, displayName: m.displayName, parentKey: m.parentId ? (keyById.get(m.parentId) ?? null) : null, isAssignable: m.isAssignable }))
  }

  /* ───────────── helpers ───────────── */

  const pinsFor = (objectId: string): AssumptionPin[] =>
    db.assumptionSets.flatMap((a) => {
      const vid = a.pins[objectId]
      if (!vid) return []
      const v = db.mappingVersions.find((x) => x.id === vid) ?? db.bridgeVersions.find((x) => x.id === vid)
      return v ? [{ assumptionSetKey: a.key, assumptionSetName: a.name, isActive: a.isActive, versionId: vid, versionNumber: v.versionNumber }] : []
    })

  const historicalPinsOn = (versionId: string) => db.assumptionSets.filter((a) => !a.isActive && Object.values(a.pins).includes(versionId))

  const msVersionSummary = (v: MappingVersion): MappingVersionSummary => ({
    id: v.id,
    versionNumber: v.versionNumber,
    status: v.status,
    publishedAt: v.publishedAt,
    retiredAt: v.retiredAt,
    entryCount: db.entries.filter((e) => e.versionId === v.id).length,
  })

  const brVersionSummary = (v: BridgeVersion): BridgeVersionSummary => ({
    id: v.id,
    versionNumber: v.versionNumber,
    status: v.status,
    cardinality: v.cardinality,
    allocationMode: v.allocationMode,
    publishedAt: v.publishedAt,
    retiredAt: v.retiredAt,
    mappingCount: db.bridgeMappings.filter((m) => m.versionId === v.id).length,
  })

  async function setListItem(setId: string, refs?: DimensionRef[]): Promise<MappingSetListItem | null> {
    const s = db.mappingSets.find((x) => x.id === setId)
    if (!s) return null
    const all = refs ?? (await dimensionRefs())
    const dimension = all.find((d) => d.id === s.dimensionId)
    if (!dimension) return null
    const versions = db.mappingVersions.filter((v) => v.mappingSetId === s.id).map(msVersionSummary)
    const pub = versions.find((v) => v.status === 'published')
    return { ...clone(s), dimension, versions, publishedEntryCount: pub?.entryCount ?? null, bindings: clone(db.bindings[s.id] ?? []) }
  }

  async function bridgeListItem(bridgeId: string, refs?: DimensionRef[]): Promise<BridgeListItem | null> {
    const b = db.bridges.find((x) => x.id === bridgeId)
    if (!b) return null
    const all = refs ?? (await dimensionRefs())
    const source = all.find((d) => d.id === b.sourceDimensionId)
    const target = all.find((d) => d.id === b.targetDimensionId)
    if (!source || !target) return null
    const versions = db.bridgeVersions.filter((v) => v.bridgeId === b.id).map(brVersionSummary)
    const pub = versions.find((v) => v.status === 'published')
    return { ...clone(b), source, target, versions, publishedMappingCount: pub?.mappingCount ?? null, bindings: clone(db.bindings[b.id] ?? []) }
  }

  const msVersion = (id: string) => db.mappingVersions.find((v) => v.id === id)
  const brVersion = (id: string) => db.bridgeVersions.find((v) => v.id === id)
  const bridgeOf = (v: BridgeVersion) => db.bridges.find((b) => b.id === v.bridgeId)!

  function reportBridgeBlock<T>(b: Bridge): ApiResult<T> | null {
    if (!b.owner) return null
    return fail(409, 'Report bridges are edited in their report', `${b.displayName} is owned by ${b.owner.reportKey} and changes as you map rows on its Rows page.`)
  }

  function versionEditBlock<T>(status: string): ApiResult<T> | null {
    if (status === 'validated') return fail(409, 'Version is validated', 'Return it to draft to make changes.')
    if (status === 'retired') return fail(409, 'Version is retired', 'Retired versions are read-only. Create a new draft instead.')
    return null
  }

  async function msReport(versionId: string): Promise<ApiResult<ValidationReport>> {
    const v = msVersion(versionId)
    if (!v) return notFound('Mapping version')
    const set = db.mappingSets.find((s) => s.id === v.mappingSetId)!
    const ref = await refOf(set.dimensionId)
    const members = indexMembers(await publishedMembers(set.dimensionId))
    const entries = db.entries.filter((e) => e.versionId === v.id)
    return ok({ versionId, checkedAt: now(), violations: mappingSetViolations(entries, members, ref?.displayName ?? set.dimensionId) })
  }

  async function brReport(versionId: string): Promise<ApiResult<ValidationReport>> {
    const v = brVersion(versionId)
    if (!v) return notFound('Bridge version')
    const b = bridgeOf(v)
    const [src, tgt, srcMembers, tgtMembers] = await Promise.all([refOf(b.sourceDimensionId), refOf(b.targetDimensionId), publishedMembers(b.sourceDimensionId), publishedMembers(b.targetDimensionId)])
    const mappings = db.bridgeMappings.filter((m) => m.versionId === v.id)
    const violations: Violation[] = bridgeViolations(
      v.cardinality,
      v.allocationMode,
      mappings,
      { name: src?.displayName ?? 'Source', members: indexMembers(srcMembers) },
      { name: tgt?.displayName ?? 'Target', members: indexMembers(tgtMembers) },
    )
    return ok({ versionId, checkedAt: now(), violations })
  }

  /** Publishes a version: retires the previous live one and repoints the active assumption set. */
  function promote<V extends { id: string; status: string; publishedAt: string | null; retiredAt: string | null }>(v: V, siblings: V[], objectId: string) {
    for (const s of siblings) {
      if (s.status === 'published') {
        s.status = 'retired'
        s.retiredAt = today()
      }
    }
    v.status = 'published'
    v.publishedAt = today()
    const active = db.assumptionSets.find((a) => a.isActive)
    if (active) active.pins[objectId] = v.id
  }

  function checkEntry(versionId: string, input: EntryInput, members: ReturnType<typeof indexMembers>): Record<string, string[]> {
    const errors: Record<string, string[]> = {}
    if (input.sourceKeyValue === '') errors.sourceKeyValue = ['Source key is required.']
    const clash = db.entries.find((e) => e.versionId === versionId && e.sourceKeyValue === input.sourceKeyValue && e.id !== input.id)
    if (clash) errors.sourceKeyValue = [`'${input.sourceKeyValue}' already maps to ${clash.memberKey}.`]
    if (!input.memberKey) errors.memberKey = ['Choose a member.']
    else if (members) {
      const m = members.get(input.memberKey)
      if (!m) errors.memberKey = [`${input.memberKey} is not a member of the published version.`]
      else if (!m.isAssignable) errors.memberKey = [`${m.displayName} is a folder and cannot receive source values.`]
    }
    return errors
  }

  async function bridgeMembers(b: Bridge) {
    const [s, t] = await Promise.all([publishedMembers(b.sourceDimensionId), publishedMembers(b.targetDimensionId)])
    return { src: indexMembers(s), tgt: indexMembers(t) }
  }

  function checkBridgeMapping(versionId: string, input: BridgeMappingInput, src: ReturnType<typeof indexMembers>, tgt: ReturnType<typeof indexMembers>, mode: string) {
    const errors: Record<string, string[]> = {}
    if (!input.sourceMemberKey) errors.sourceMemberKey = ['Choose a source member.']
    else if (src && !src.has(input.sourceMemberKey)) errors.sourceMemberKey = [`${input.sourceMemberKey} is not in the published source version.`]
    if (!input.targetMemberKey) errors.targetMemberKey = ['Choose a target member.']
    else if (tgt && !tgt.has(input.targetMemberKey)) errors.targetMemberKey = [`${input.targetMemberKey} is not in the published target version.`]
    if (!Number.isFinite(input.factor)) errors.factor = ['Factor must be a number.']
    if (!Number.isFinite(input.allocationWeight)) errors.allocationWeight = ['Weight must be a number.']
    else if (input.allocationWeight < 0) errors.allocationWeight = ['Weight cannot be negative.']
    else if (mode === 'none' && !sumsToOne(input.allocationWeight)) errors.allocationWeight = ['Allocation none requires a weight of exactly 1.']
    const clash = db.bridgeMappings.find((m) => m.versionId === versionId && m.id !== input.id && m.sourceMemberKey === input.sourceMemberKey && m.targetMemberKey === input.targetMemberKey)
    if (clash) errors.targetMemberKey = [`${input.sourceMemberKey} → ${input.targetMemberKey} already exists.`]
    return errors
  }

  /* ───────────── the port ───────────── */

  return {
    async listDimensionRefs() {
      await delay()
      return dimensionRefs()
    },
    async getPublishedMembers(dimensionId) {
      await delay()
      return publishedMembers(dimensionId)
    },

    /* mapping sets */

    async listMappingSets(filter) {
      await delay()
      const refs = await dimensionRefs()
      const items = await Promise.all(db.mappingSets.filter((s) => !filter?.dimensionId || s.dimensionId === filter.dimensionId).map((s) => setListItem(s.id, refs)))
      return items.filter((x): x is MappingSetListItem => !!x).sort((a, b) => a.key.localeCompare(b.key))
    },
    async getMappingSet(id) {
      await delay()
      const item = await setListItem(id)
      return item ? ok({ ...item, pins: pinsFor(id) }) : notFound('Mapping set')
    },
    async getMappingVersion(versionId) {
      await delay()
      const v = msVersion(versionId)
      if (!v) return notFound('Mapping version')
      return ok({ version: clone(v), entries: clone(db.entries.filter((e) => e.versionId === v.id)) })
    },
    async createMappingSet(input) {
      await delay()
      const errors: Record<string, string[]> = {}
      if (!KEY_RE.test(input.key)) errors.key = ['Use lowercase letters, digits, dots or underscores, starting with a letter.']
      else if (db.mappingSets.some((s) => s.key === input.key)) errors.key = [`Mapping set '${input.key}' already exists.`]
      if (!input.displayName.trim()) errors.displayName = ['Display name is required.']
      if (!input.sourceNamespace.trim()) errors.sourceNamespace = ['Source namespace is required.']
      const ref = await refOf(input.dimensionId)
      if (!ref) errors.dimensionId = ['Dimension not found.']
      else if (!canHaveMappingSets(ref)) errors.dimensionId = [`${ref.displayName} cannot receive mapping sets; only analytical categorical or hierarchical dimensions can.`]
      if (Object.keys(errors).length) return fail(422, 'Check the highlighted fields', undefined, errors)
      const id = uid('ms')
      const vid = `${id}-v1`
      db.mappingSets.push({ id, key: input.key, displayName: input.displayName.trim(), sourceNamespace: input.sourceNamespace.trim(), dimensionId: input.dimensionId })
      db.mappingVersions.push({ id: vid, mappingSetId: id, versionNumber: 1, status: 'draft', createdAt: today(), publishedAt: null, retiredAt: null })
      db.bindings[id] = []
      return ok({ mappingSetId: id, versionId: vid })
    },
    async updateMappingSet(id, patch) {
      await delay()
      const s = db.mappingSets.find((x) => x.id === id)
      if (!s) return notFound('Mapping set')
      if (patch.displayName !== undefined && !patch.displayName.trim()) return fail(422, 'Check the highlighted fields', undefined, { displayName: ['Display name is required.'] })
      if (patch.sourceNamespace !== undefined && !patch.sourceNamespace.trim()) return fail(422, 'Check the highlighted fields', undefined, { sourceNamespace: ['Source namespace is required.'] })
      Object.assign(s, { ...patch, displayName: patch.displayName?.trim() ?? s.displayName, sourceNamespace: patch.sourceNamespace?.trim() ?? s.sourceNamespace })
      return ok(clone(s))
    },
    async checkDeleteMappingSet(id): Promise<DeleteCheck> {
      await delay()
      const reasons = [
        ...(db.bindings[id] ?? []).map((b) => `${b.datasetKey} v${b.datasetVersion} binds ${b.fieldKey} → ${b.roleKey}`),
        ...db.assumptionSets.filter((a) => !a.isActive && a.pins[id]).map((a) => `Pinned by the ${a.name} assumption set`),
      ]
      return reasons.length ? { allowed: false, message: `This mapping set is in use (${reasons.length} reference${reasons.length === 1 ? '' : 's'}). Remove them first.`, reasons } : { allowed: true, message: null, reasons: [] }
    },
    async deleteMappingSet(id) {
      const check = await this.checkDeleteMappingSet(id)
      if (!check.allowed) return fail(409, 'Mapping set is in use', check.message ?? undefined)
      const versionIds = new Set(db.mappingVersions.filter((v) => v.mappingSetId === id).map((v) => v.id))
      db.entries = db.entries.filter((e) => !versionIds.has(e.versionId))
      db.mappingVersions = db.mappingVersions.filter((v) => !versionIds.has(v.id))
      db.mappingSets = db.mappingSets.filter((s) => s.id !== id)
      for (const a of db.assumptionSets) delete a.pins[id]
      delete db.bindings[id]
      return ok(null)
    },
    async createMappingVersion(setId, input) {
      await delay()
      const versions = db.mappingVersions.filter((v) => v.mappingSetId === setId)
      if (!versions.length && !db.mappingSets.some((s) => s.id === setId)) return notFound('Mapping set')
      const open = versions.find((v) => v.status === 'draft' || v.status === 'validated')
      if (open) return fail(409, 'A draft already exists', `Only one draft may exist at a time; v${open.versionNumber} is still open.`)
      const n = Math.max(0, ...versions.map((v) => v.versionNumber)) + 1
      const v: MappingVersion = { id: `${setId}-v${n}-${uid('x').slice(2)}`, mappingSetId: setId, versionNumber: n, status: 'draft', createdAt: today(), publishedAt: null, retiredAt: null }
      db.mappingVersions.push(v)
      if (input.mode === 'clone') {
        for (const e of db.entries.filter((x) => x.versionId === input.fromVersionId)) db.entries.push({ ...e, id: uid('e'), versionId: v.id })
      }
      return ok(msVersionSummary(v))
    },
    checkMappingVersion: msReport,
    async validateMappingVersion(versionId) {
      await delay()
      const v = msVersion(versionId)
      if (!v) return notFound('Mapping version')
      if (v.status !== 'draft') return fail(409, 'Only drafts can be validated')
      const r = await msReport(versionId)
      if (r.ok && !r.data.violations.length) v.status = 'validated'
      return r
    },
    async publishMappingVersion(versionId) {
      await delay()
      const v = msVersion(versionId)
      if (!v) return notFound('Mapping version')
      if (v.status !== 'draft' && v.status !== 'validated') return fail(409, 'Only drafts or validated versions can be published')
      const r = await msReport(versionId)
      if (r.ok && !r.data.violations.length) promote(v, db.mappingVersions.filter((x) => x.mappingSetId === v.mappingSetId && x.id !== v.id), v.mappingSetId)
      return r
    },
    async reopenMappingVersion(versionId) {
      await delay()
      const v = msVersion(versionId)
      if (!v) return notFound('Mapping version')
      if (v.status !== 'validated') return fail(409, 'Only validated versions can return to draft')
      v.status = 'draft'
      return ok(clone(v))
    },
    async deleteMappingVersion(versionId) {
      await delay()
      const v = msVersion(versionId)
      if (!v) return notFound('Mapping version')
      if (v.status !== 'draft' && v.status !== 'validated') return fail(409, 'Only drafts can be deleted', 'Published and retired versions are kept for history.')
      db.entries = db.entries.filter((e) => e.versionId !== versionId)
      db.mappingVersions = db.mappingVersions.filter((x) => x.id !== versionId)
      return ok(null)
    },
    async saveEntry(versionId, input) {
      await delay()
      const v = msVersion(versionId)
      if (!v) return notFound('Mapping version')
      const blocked = versionEditBlock<MappingEntry>(v.status)
      if (blocked) return blocked
      const set = db.mappingSets.find((s) => s.id === v.mappingSetId)!
      const errors = checkEntry(versionId, input, indexMembers(await publishedMembers(set.dimensionId)))
      if (Object.keys(errors).length) return fail(422, 'Check the highlighted fields', undefined, errors)
      const existing = input.id ? db.entries.find((e) => e.id === input.id && e.versionId === versionId) : undefined
      if (existing) {
        existing.sourceKeyValue = input.sourceKeyValue
        existing.memberKey = input.memberKey
        return ok(clone(existing))
      }
      const entry: MappingEntry = { id: uid('e'), versionId, sourceKeyValue: input.sourceKeyValue, memberKey: input.memberKey }
      db.entries.push(entry)
      return ok(clone(entry))
    },
    async deleteEntry(versionId, entryId) {
      await delay()
      const v = msVersion(versionId)
      if (!v) return notFound('Mapping version')
      const blocked = versionEditBlock<null>(v.status)
      if (blocked) return blocked
      db.entries = db.entries.filter((e) => !(e.id === entryId && e.versionId === versionId))
      return ok(null)
    },
    async replaceEntries(versionId, entries) {
      await delay()
      const v = msVersion(versionId)
      if (!v) return notFound('Mapping version')
      const blocked = versionEditBlock<MappingEntry[]>(v.status)
      if (blocked) return blocked
      const keys = new Set<string>()
      for (const e of entries) {
        if (keys.has(e.sourceKeyValue)) return fail(422, 'Duplicate source key', `'${e.sourceKeyValue}' appears more than once.`)
        keys.add(e.sourceKeyValue)
      }
      if (v.status === 'published') {
        const set = db.mappingSets.find((s) => s.id === v.mappingSetId)!
        const members = indexMembers(await publishedMembers(set.dimensionId))
        const bad = entries.find((e) => members && !members.get(e.memberKey)?.isAssignable)
        if (bad) return fail(422, 'Live versions must stay valid', `${bad.memberKey} is not an assignable member.`)
      }
      db.entries = db.entries.filter((e) => e.versionId !== versionId)
      const next = entries.map((e) => ({ id: e.id ?? uid('e'), versionId, sourceKeyValue: e.sourceKeyValue, memberKey: e.memberKey }))
      db.entries.push(...next)
      return ok(clone(next))
    },

    /* bridges */

    async listBridges(filter) {
      await delay()
      const refs = await dimensionRefs()
      const items = await Promise.all(
        db.bridges.filter((b) => !filter?.dimensionId || b.sourceDimensionId === filter.dimensionId || b.targetDimensionId === filter.dimensionId).map((b) => bridgeListItem(b.id, refs)),
      )
      return items.filter((x): x is BridgeListItem => !!x).sort((a, b) => Number(!!a.owner) - Number(!!b.owner) || a.key.localeCompare(b.key))
    },
    async getBridge(id) {
      await delay()
      const item = await bridgeListItem(id)
      return item ? ok({ ...item, pins: pinsFor(id) }) : notFound('Bridge')
    },
    async findReportBridge(reportKey, sourceKey) {
      await delay()
      const b = db.bridges.find((x) => x.owner?.reportKey === reportKey && x.owner.sourceKey === sourceKey)
      return b ? bridgeListItem(b.id) : null
    },
    async getBridgeVersion(versionId) {
      await delay()
      const v = brVersion(versionId)
      if (!v) return notFound('Bridge version')
      return ok({ version: clone(v), mappings: clone(db.bridgeMappings.filter((m) => m.versionId === v.id)) })
    },
    async createBridge(input) {
      await delay()
      const errors: Record<string, string[]> = {}
      if (!KEY_RE.test(input.key)) errors.key = ['Use lowercase letters, digits, dots or underscores, starting with a letter.']
      else if (db.bridges.some((b) => b.key === input.key)) errors.key = [`Bridge '${input.key}' already exists.`]
      if (!input.displayName.trim()) errors.displayName = ['Display name is required.']
      const [src, tgt] = await Promise.all([refOf(input.sourceDimensionId), refOf(input.targetDimensionId)])
      if (!src) errors.sourceDimensionId = ['Choose a source dimension.']
      else if (!canHaveBridges(src)) errors.sourceDimensionId = ['Time dimensions cannot be bridged.']
      else if (src.purpose === 'report') errors.sourceDimensionId = ['Report dimensions are bridge targets only, and only for report bridges.']
      if (!tgt) errors.targetDimensionId = ['Choose a target dimension.']
      else if (!canHaveBridges(tgt)) errors.targetDimensionId = ['Time dimensions cannot be bridged.']
      else if (tgt.purpose === 'report') errors.targetDimensionId = ['Bridges to report dimensions are created by mapping rows in a report.']
      if (src && tgt && src.id === tgt.id) errors.targetDimensionId = ['Source and target must be different dimensions.']
      if (Object.keys(errors).length) return fail(422, 'Check the highlighted fields', undefined, errors)
      const id = uid('bridge')
      db.bridges.push({ id, key: input.key, displayName: input.displayName.trim(), sourceDimensionId: input.sourceDimensionId, targetDimensionId: input.targetDimensionId, isEnabled: input.isEnabled, owner: null })
      db.bindings[id] = []
      return ok({ bridgeId: id })
    },
    async updateBridge(id, patch) {
      await delay()
      const b = db.bridges.find((x) => x.id === id)
      if (!b) return notFound('Bridge')
      const blocked = reportBridgeBlock<Bridge>(b)
      if (blocked) return blocked
      if (patch.displayName !== undefined && !patch.displayName.trim()) return fail(422, 'Check the highlighted fields', undefined, { displayName: ['Display name is required.'] })
      Object.assign(b, { ...patch, displayName: patch.displayName?.trim() ?? b.displayName })
      return ok(clone(b))
    },
    async checkDeleteBridge(id): Promise<DeleteCheck> {
      await delay()
      const b = db.bridges.find((x) => x.id === id)
      if (b?.owner) return { allowed: false, message: `Owned by ${b.owner.reportKey}. Remove the source from that report to delete its bridge.`, reasons: [] }
      const reasons = [
        ...(db.bindings[id] ?? []).map((x) => `${x.datasetKey} v${x.datasetVersion} remaps ${x.sourceRoleKey ?? x.fieldKey} → ${x.roleKey}`),
        ...db.assumptionSets.filter((a) => !a.isActive && a.pins[id]).map((a) => `Pinned by the ${a.name} assumption set`),
      ]
      return reasons.length ? { allowed: false, message: `This bridge is in use (${reasons.length} reference${reasons.length === 1 ? '' : 's'}). Remove them first.`, reasons } : { allowed: true, message: null, reasons: [] }
    },
    async deleteBridge(id) {
      const check = await this.checkDeleteBridge(id)
      if (!check.allowed) return fail(409, 'Bridge is in use', check.message ?? undefined)
      const versionIds = new Set(db.bridgeVersions.filter((v) => v.bridgeId === id).map((v) => v.id))
      db.bridgeMappings = db.bridgeMappings.filter((m) => !versionIds.has(m.versionId))
      db.bridgeVersions = db.bridgeVersions.filter((v) => !versionIds.has(v.id))
      db.bridges = db.bridges.filter((b) => b.id !== id)
      for (const a of db.assumptionSets) delete a.pins[id]
      delete db.bindings[id]
      return ok(null)
    },
    async createBridgeVersion(bridgeId, input) {
      await delay()
      const b = db.bridges.find((x) => x.id === bridgeId)
      if (!b) return notFound('Bridge')
      const blocked = reportBridgeBlock<BridgeVersionSummary>(b)
      if (blocked) return blocked
      if (!allowedModes(input.cardinality).includes(input.allocationMode)) return fail(422, 'Invalid settings', `${input.cardinality} does not allow allocation ${input.allocationMode}.`)
      const versions = db.bridgeVersions.filter((v) => v.bridgeId === bridgeId)
      const open = versions.find((v) => v.status === 'draft' || v.status === 'validated')
      if (open) return fail(409, 'A draft already exists', `Only one draft may exist at a time; v${open.versionNumber} is still open.`)
      const n = Math.max(0, ...versions.map((v) => v.versionNumber)) + 1
      const v: BridgeVersion = { id: `${bridgeId}-v${n}-${uid('x').slice(2)}`, bridgeId, versionNumber: n, status: 'draft', cardinality: input.cardinality, allocationMode: input.allocationMode, createdAt: today(), publishedAt: null, retiredAt: null }
      db.bridgeVersions.push(v)
      if (input.cloneFromVersionId) {
        for (const m of db.bridgeMappings.filter((x) => x.versionId === input.cloneFromVersionId)) db.bridgeMappings.push({ ...m, id: uid('m'), versionId: v.id })
      }
      return ok(brVersionSummary(v))
    },
    async updateBridgeVersion(versionId, patch) {
      await delay()
      const v = brVersion(versionId)
      if (!v) return notFound('Bridge version')
      const blocked = reportBridgeBlock<BridgeVersion>(bridgeOf(v))
      if (blocked) return blocked
      if (v.status !== 'draft') return fail(409, 'Settings are fixed after validation', 'Cardinality and allocation can only change on a draft.')
      if (!allowedModes(patch.cardinality).includes(patch.allocationMode)) return fail(422, 'Invalid settings', `${patch.cardinality} does not allow allocation ${patch.allocationMode}.`)
      v.cardinality = patch.cardinality
      v.allocationMode = patch.allocationMode
      return ok(clone(v))
    },
    checkBridgeVersion: brReport,
    async validateBridgeVersion(versionId) {
      await delay()
      const v = brVersion(versionId)
      if (!v) return notFound('Bridge version')
      if (v.status !== 'draft') return fail(409, 'Only drafts can be validated')
      const r = await brReport(versionId)
      if (r.ok && !r.data.violations.length) v.status = 'validated'
      return r
    },
    async publishBridgeVersion(versionId) {
      await delay()
      const v = brVersion(versionId)
      if (!v) return notFound('Bridge version')
      const blocked = reportBridgeBlock<ValidationReport>(bridgeOf(v))
      if (blocked) return blocked
      if (v.status !== 'draft' && v.status !== 'validated') return fail(409, 'Only drafts or validated versions can be published')
      const r = await brReport(versionId)
      if (r.ok && !r.data.violations.length) promote(v, db.bridgeVersions.filter((x) => x.bridgeId === v.bridgeId && x.id !== v.id), v.bridgeId)
      return r
    },
    async reopenBridgeVersion(versionId) {
      await delay()
      const v = brVersion(versionId)
      if (!v) return notFound('Bridge version')
      if (v.status !== 'validated') return fail(409, 'Only validated versions can return to draft')
      v.status = 'draft'
      return ok(clone(v))
    },
    async deleteBridgeVersion(versionId) {
      await delay()
      const v = brVersion(versionId)
      if (!v) return notFound('Bridge version')
      const blocked = reportBridgeBlock<null>(bridgeOf(v))
      if (blocked) return blocked
      if (v.status !== 'draft' && v.status !== 'validated') return fail(409, 'Only drafts can be deleted', 'Published and retired versions are kept for history.')
      if (historicalPinsOn(versionId).length) return fail(409, 'Version is pinned', 'An assumption set pins this version.')
      db.bridgeMappings = db.bridgeMappings.filter((m) => m.versionId !== versionId)
      db.bridgeVersions = db.bridgeVersions.filter((x) => x.id !== versionId)
      return ok(null)
    },
    async saveBridgeMapping(versionId, input) {
      await delay()
      const v = brVersion(versionId)
      if (!v) return notFound('Bridge version')
      const b = bridgeOf(v)
      const blocked = reportBridgeBlock<BridgeMapping>(b) ?? versionEditBlock<BridgeMapping>(v.status)
      if (blocked) return blocked
      const { src, tgt } = await bridgeMembers(b)
      const errors = checkBridgeMapping(versionId, input, src, tgt, v.allocationMode)
      if (Object.keys(errors).length) return fail(422, 'Check the highlighted fields', undefined, errors)
      const existing = input.id ? db.bridgeMappings.find((m) => m.id === input.id && m.versionId === versionId) : undefined
      const next: BridgeMapping = {
        id: existing?.id ?? uid('m'),
        versionId,
        sourceMemberKey: input.sourceMemberKey,
        targetMemberKey: input.targetMemberKey,
        factor: input.factor,
        allocationWeight: input.allocationWeight,
        sourceSelection: input.sourceSelection ?? existing?.sourceSelection ?? 'member',
      }
      if (v.status === 'published') {
        const after = [...db.bridgeMappings.filter((m) => m.versionId === versionId && m.id !== next.id), next]
        if (allocationIssueCount(allocationSummary(v.cardinality, v.allocationMode, after)))
          return fail(422, 'Live versions must stay valid', `This change would break the ${v.allocationMode} rules for ${next.sourceMemberKey}. Make the change in a new draft.`)
      }
      if (existing) Object.assign(existing, next)
      else db.bridgeMappings.push(next)
      return ok(clone(next))
    },
    async deleteBridgeMapping(versionId, mappingId) {
      await delay()
      const v = brVersion(versionId)
      if (!v) return notFound('Bridge version')
      const blocked = reportBridgeBlock<null>(bridgeOf(v)) ?? versionEditBlock<null>(v.status)
      if (blocked) return blocked
      if (v.status === 'published' && v.allocationMode === 'partition') {
        const m = db.bridgeMappings.find((x) => x.id === mappingId)
        const after = db.bridgeMappings.filter((x) => x.versionId === versionId && x.id !== mappingId)
        if (m && after.some((x) => x.sourceMemberKey === m.sourceMemberKey) && allocationIssueCount(allocationSummary(v.cardinality, v.allocationMode, after)))
          return fail(422, 'Live versions must stay valid', `Removing this mapping leaves ${m.sourceMemberKey}'s weights not summing to 1. Make the change in a new draft.`)
      }
      db.bridgeMappings = db.bridgeMappings.filter((m) => !(m.id === mappingId && m.versionId === versionId))
      return ok(null)
    },
    async replaceBridgeMappings(versionId, rows) {
      await delay()
      const v = brVersion(versionId)
      if (!v) return notFound('Bridge version')
      const blocked = reportBridgeBlock<BridgeMapping[]>(bridgeOf(v)) ?? versionEditBlock<BridgeMapping[]>(v.status)
      if (blocked) return blocked
      if (v.status === 'published' && allocationIssueCount(allocationSummary(v.cardinality, v.allocationMode, rows)))
        return fail(422, 'Live versions must stay valid', 'The pasted mappings break the allocation rules. Paste into a new draft instead.')
      db.bridgeMappings = db.bridgeMappings.filter((m) => m.versionId !== versionId)
      const next = rows.map((r) => ({ id: r.id ?? uid('m'), versionId, sourceMemberKey: r.sourceMemberKey, targetMemberKey: r.targetMemberKey, factor: r.factor, allocationWeight: r.allocationWeight, sourceSelection: r.sourceSelection ?? 'member' }))
      db.bridgeMappings.push(...next)
      return ok(clone(next))
    },
  }
}
