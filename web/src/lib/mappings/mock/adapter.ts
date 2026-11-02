import { createSnapshotChangeSet } from '@/lib/api/changes'
import { DOTTED_KEY_PATTERN } from '@/lib/api/contract'
import {
  allowedModes,
  ALLOCATION_LABEL,
  canHaveBridges,
  canHaveMappingSets,
  CARDINALITY_LABEL,
  type ApiResult,
  type Bridge,
  type BridgeListItem,
  type BridgeMapping,
  type BridgeVersion,
  type BridgeVersionContent,
  type BridgeVersionSummary,
  type DeleteBlocker,
  type DeleteCheck,
  type MappingDimensionRef,
  type MappingEntry,
  type MappingSetListItem,
  type MappingsApi,
  type MappingsDirectory,
  type MappingVersion,
  type MappingVersionContent,
  type MappingVersionSummary,
  type Problem,
  type Violation,
} from '../contract'
import { LIVE_SNAPSHOT_ID, seedSnapshots } from '@/lib/snapshots/mock/data'
import { indexMembers } from '../rules'
import { createSeed, type Db } from './data'
import { bridgeAllocation, bridgeViolations, mappingCoverage, mappingSetViolations } from './rules'

const now = () => new Date().toISOString()
const delay = () => new Promise((r) => setTimeout(r, 120))
const uid = (p: string) => `${p}-${Math.random().toString(36).slice(2, 9)}`
const clone = <T,>(v: T): T => structuredClone(v)

const PROBLEM_TYPE: Record<Problem['status'], string> = {
  400: 'urn:qe:problem:invalid-input',
  401: 'urn:qe:problem:unauthorized',
  403: 'urn:qe:problem:forbidden',
  404: 'urn:qe:problem:not-found',
  409: 'urn:qe:problem:conflict',
  422: 'urn:qe:problem:rules-violated',
  500: 'urn:qe:problem:internal',
}
const ok = <T,>(data: T): ApiResult<T> => ({ ok: true, data })
function fail<T>(status: Problem['status'], title: string, more: Partial<Pick<Problem, 'detail' | 'errors' | 'violations'>> = {}): ApiResult<T> {
  return { ok: false, problem: { type: PROBLEM_TYPE[status], title, status, errors: {}, ...more } }
}
const notFound = <T,>(what: string) => fail<T>(404, `${what} not found`)
const invalid = <T,>(errors: Record<string, string[]>) => fail<T>(400, 'Check the highlighted fields', { errors })
const frozen = <T,>() => fail<T>(409, 'Version is frozen', { detail: 'Frozen versions are read-only. Edit the live version instead.' })
const blocked = <T,>(title: string, check: DeleteCheck) => fail<T>(409, title, { detail: check.message!, violations: check.reasons })

const deleteCheck = (message: string, reasons: DeleteBlocker[]): DeleteCheck => (reasons.length ? { allowed: false, message, reasons } : { allowed: true, message: null, reasons: [] })
const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? '' : 's'}`
const snapshotKey = (id: string) => seedSnapshots.find((s) => s.id === id)!.key

export function createMockMappingsApi(directory: MappingsDirectory, seed: Db = createSeed()): MappingsApi {
  let db = seed
  /** Live versions edited in the open change set, and their rule findings, which refuse the commit while any remains (B11). */
  const touched = new Set<string>()
  let findings: Violation[] = []
  const changes = createSnapshotChangeSet(
    () => db,
    (snapshot) => {
      db = snapshot
    },
    () => findings,
  )

  /* ───────────── lookups ───────────── */

  const setOf = (v: MappingVersion) => db.mappingSets.find((s) => s.id === v.mappingSetId)!
  const bridgeOf = (v: BridgeVersion) => db.bridges.find((b) => b.id === v.bridgeId)!
  const mappingVersion = (id: string) => db.mappingVersions.find((v) => v.id === id)
  const bridgeVersion = (id: string) => db.bridgeVersions.find((v) => v.id === id)
  const nameOf = (refs: MappingDimensionRef[], id: string) => refs.find((d) => d.id === id)!.displayName

  const mappingSummary = (v: MappingVersion): MappingVersionSummary => ({
    id: v.id,
    snapshotId: v.snapshotId,
    isLive: v.isLive,
    createdAt: v.createdAt,
    entryCount: db.entries.filter((e) => e.versionId === v.id).length,
  })

  const bridgeSummary = (v: BridgeVersion): BridgeVersionSummary => ({
    id: v.id,
    snapshotId: v.snapshotId,
    isLive: v.isLive,
    cardinality: v.cardinality,
    allocationMode: v.allocationMode,
    createdAt: v.createdAt,
    mappingCount: db.bridgeMappings.filter((m) => m.versionId === v.id).length,
  })

  /** The live version first, then frozen ones newest first. */
  const byPlace = <V extends { isLive: boolean; createdAt: string }>(versions: V[]) => [...versions].sort((a, b) => Number(b.isLive) - Number(a.isLive) || b.createdAt.localeCompare(a.createdAt))

  function setListItem(setId: string, refs: MappingDimensionRef[]): MappingSetListItem {
    const s = db.mappingSets.find((x) => x.id === setId)!
    const versions = byPlace(db.mappingVersions.filter((v) => v.mappingSetId === s.id)).map(mappingSummary)
    return { ...clone(s), dimension: refs.find((d) => d.id === s.dimensionId)!, versions, liveVersionId: versions[0].id, liveEntryCount: versions[0].entryCount, bindings: clone(db.bindings[s.id]) }
  }

  function bridgeListItem(bridgeId: string, refs: MappingDimensionRef[]): BridgeListItem {
    const b = db.bridges.find((x) => x.id === bridgeId)!
    const versions = byPlace(db.bridgeVersions.filter((v) => v.bridgeId === b.id)).map(bridgeSummary)
    return {
      ...clone(b),
      source: refs.find((d) => d.id === b.sourceDimensionId)!,
      target: refs.find((d) => d.id === b.targetDimensionId)!,
      versions,
      liveVersionId: versions[0].id,
      liveMappingCount: versions[0].mappingCount,
      bindings: clone(db.bindings[b.id]),
    }
  }
  const liveMappingVersion = (setId: string) => db.mappingVersions.find((v) => v.mappingSetId === setId && v.isLive)!
  const liveBridgeVersion = (bridgeId: string) => db.bridgeVersions.find((v) => v.bridgeId === bridgeId && v.isLive)!

  async function mappingContent(v: MappingVersion): Promise<MappingVersionContent> {
    const set = setOf(v)
    const [refs, members] = await Promise.all([directory.listDimensionRefs(), directory.getPublishedMembers(set.dimensionId)])
    const entries = clone(db.entries.filter((e) => e.versionId === v.id))
    return { version: clone(v), entries, coverage: mappingCoverage(entries, members), violations: mappingSetViolations(entries, indexMembers(members), nameOf(refs, set.dimensionId)) }
  }

  async function bridgeContent(v: BridgeVersion): Promise<BridgeVersionContent> {
    const b = bridgeOf(v)
    const [refs, source, target] = await Promise.all([directory.listDimensionRefs(), directory.getPublishedMembers(b.sourceDimensionId), directory.getPublishedMembers(b.targetDimensionId)])
    const mappings = clone(db.bridgeMappings.filter((m) => m.versionId === v.id))
    const violations = bridgeViolations(
      v.cardinality,
      v.allocationMode,
      mappings,
      { name: nameOf(refs, b.sourceDimensionId), members: indexMembers(source) },
      { name: nameOf(refs, b.targetDimensionId), members: indexMembers(target) },
      b.owner !== null,
    )
    return { version: clone(v), mappings, allocation: bridgeAllocation(mappings), violations }
  }

  function ownedByReport<T>(b: Bridge): ApiResult<T> | null {
    return b.owner ? fail(409, 'Owned by a report', { detail: `${b.displayName} is owned by ${b.owner.reportKey} and changes as you map rows on its Rows page.` }) : null
  }

  /* ───────────── delete checks ───────────── */

  function identityDeleteCheck(objectId: string, label: string): DeleteCheck {
    const reasons: DeleteBlocker[] = [
      ...db.bindings[objectId].map((b) => ({ code: 'referenced_by_dataset_binding' as const, message: `${b.datasetKey} binds ${b.fieldKey} → ${b.roleKey}` })),
      ...[...db.mappingVersions.filter((v) => v.mappingSetId === objectId), ...db.bridgeVersions.filter((v) => v.bridgeId === objectId)]
        .filter((v) => !v.isLive)
        .map((v) => ({ code: 'referenced_by_snapshot' as const, message: `Snapshot ${snapshotKey(v.snapshotId)} holds a version of it` })),
    ]
    return deleteCheck(`This ${label} is in use (${plural(reasons.length, 'reference')}). Remove them first.`, reasons)
  }

  function bridgeDeleteCheck(id: string): DeleteCheck {
    const { owner } = db.bridges.find((x) => x.id === id)!
    if (owner) return deleteCheck(`Owned by ${owner.reportKey}.`, [{ code: 'owned_by_report', message: `Remove source ${owner.sourceKey} from ${owner.reportKey} to delete its bridge.` }])
    return identityDeleteCheck(id, 'bridge')
  }

  async function touchedViolations() {
    const found = await Promise.all(
      [...touched].map(async (id) => {
        const mv = mappingVersion(id)
        return mv ? (await mappingContent(mv)).violations : (await bridgeContent(bridgeVersion(id)!)).violations
      }),
    )
    return found.flat()
  }

  /** Records an edit of a live version and refreshes the batch's findings, as a preview does. */
  async function touch(versionId: string) {
    touched.add(versionId)
    findings = await touchedViolations()
  }

  /* ───────────── the port ───────────── */

  return {
    ...changes,
    async commitChanges() {
      findings = await touchedViolations()
      const result = await changes.commitChanges()
      if (result.ok) touched.clear()
      return result
    },
    async discardChanges() {
      touched.clear()
      findings = []
      return changes.discardChanges()
    },

    /* mapping set reads */

    async listMappingSets(filter) {
      await delay()
      const refs = await directory.listDimensionRefs()
      return db.mappingSets
        .filter((s) => !filter?.dimensionId || s.dimensionId === filter.dimensionId)
        .map((s) => setListItem(s.id, refs))
        .sort((a, b) => a.key.localeCompare(b.key))
    },
    async getMappingSet(id) {
      await delay()
      if (!db.mappingSets.some((s) => s.id === id)) return notFound('Mapping set')
      return ok(setListItem(id, await directory.listDimensionRefs()))
    },
    async getMappingVersion(versionId) {
      await delay()
      const v = mappingVersion(versionId)
      return v ? ok(await mappingContent(v)) : notFound('Mapping version')
    },
    async checkDeleteMappingSet(id) {
      await delay()
      return identityDeleteCheck(id, 'mapping set')
    },

    /* mapping set edits */

    async updateMappingSet(id, patch) {
      await delay()
      const errors: Record<string, string[]> = {}
      if (patch.displayName !== undefined && !patch.displayName.trim()) errors.displayName = ['Display name is required.']
      if (patch.sourceNamespace !== undefined && !patch.sourceNamespace.trim()) errors.sourceNamespace = ['Source namespace is required.']
      if (Object.keys(errors).length) return invalid(errors)
      const s = db.mappingSets.find((x) => x.id === id)!
      Object.assign(s, { displayName: patch.displayName?.trim() ?? s.displayName, sourceNamespace: patch.sourceNamespace?.trim() ?? s.sourceNamespace })
      await touch(liveMappingVersion(id).id)
      return ok(clone(s))
    },
    async saveEntry(versionId, input) {
      await delay()
      if (!mappingVersion(versionId)!.isLive) return frozen()
      const errors: Record<string, string[]> = {}
      if (input.sourceKeyValue === '') errors.sourceKeyValue = ['Source key is required.']
      if (!input.memberKey) errors.memberKey = ['Choose a member.']
      if (Object.keys(errors).length) return invalid(errors)
      const entry: MappingEntry = { id: input.id, versionId, sourceKeyValue: input.sourceKeyValue, memberKey: input.memberKey }
      db.entries = [...db.entries.filter((e) => e.id !== input.id), entry]
      await touch(versionId)
      return ok(clone(entry))
    },
    async deleteEntry(versionId, entryId) {
      await delay()
      if (!mappingVersion(versionId)!.isLive) return frozen()
      db.entries = db.entries.filter((e) => e.id !== entryId)
      await touch(versionId)
      return ok(null)
    },
    async replaceEntries(versionId, entries) {
      await delay()
      if (!mappingVersion(versionId)!.isLive) return frozen()
      const next = entries.map((e) => ({ id: e.id, versionId, sourceKeyValue: e.sourceKeyValue, memberKey: e.memberKey }))
      db.entries = [...db.entries.filter((e) => e.versionId !== versionId), ...next]
      await touch(versionId)
      return ok(clone(next))
    },

    /* mapping set commands */

    async createMappingSet(input) {
      await delay()
      const errors: Record<string, string[]> = {}
      if (!DOTTED_KEY_PATTERN.test(input.key)) errors.key = ['Use letters, digits, dots or underscores, starting with a letter.']
      if (!input.displayName.trim()) errors.displayName = ['Display name is required.']
      if (!input.sourceNamespace.trim()) errors.sourceNamespace = ['Source namespace is required.']
      if (Object.keys(errors).length) return invalid(errors)
      if (db.mappingSets.some((s) => s.key === input.key)) return fail(409, 'Key taken', { detail: `Mapping set '${input.key}' already exists.` })
      const ref = (await directory.listDimensionRefs()).find((d) => d.id === input.dimensionId)!
      if (!canHaveMappingSets(ref)) return fail(409, 'Dimension cannot receive mapping sets', { detail: `Only analytical categorical or hierarchical dimensions can; ${ref.displayName} cannot.` })
      const id = uid('ms')
      const t = now()
      const v: MappingVersion = { id: uid('ver'), mappingSetId: id, snapshotId: LIVE_SNAPSHOT_ID, isLive: true, createdAt: t }
      db.mappingSets.push({ id, key: input.key, displayName: input.displayName.trim(), sourceNamespace: input.sourceNamespace.trim(), dimensionId: input.dimensionId })
      db.mappingVersions.push(v)
      db.bindings[id] = []
      return ok({ mappingSetId: id, versionId: v.id })
    },
    async deleteMappingSet(id) {
      await delay()
      const check = identityDeleteCheck(id, 'mapping set')
      if (!check.allowed) return blocked('Mapping set is in use', check)
      const versionIds = new Set(db.mappingVersions.filter((v) => v.mappingSetId === id).map((v) => v.id))
      db.entries = db.entries.filter((e) => !versionIds.has(e.versionId))
      db.mappingVersions = db.mappingVersions.filter((v) => !versionIds.has(v.id))
      db.mappingSets = db.mappingSets.filter((s) => s.id !== id)
      delete db.bindings[id]
      return ok(null)
    },
    /* bridge reads */

    async listBridges(filter) {
      await delay()
      const refs = await directory.listDimensionRefs()
      return db.bridges
        .filter((b) => !filter?.dimensionId || b.sourceDimensionId === filter.dimensionId || b.targetDimensionId === filter.dimensionId)
        .map((b) => bridgeListItem(b.id, refs))
        .sort((a, b) => Number(!!a.owner) - Number(!!b.owner) || a.key.localeCompare(b.key))
    },
    async getBridge(id) {
      await delay()
      if (!db.bridges.some((b) => b.id === id)) return notFound('Bridge')
      return ok(bridgeListItem(id, await directory.listDimensionRefs()))
    },
    async findReportBridge(reportId, sourceKey) {
      await delay()
      const b = db.bridges.find((x) => x.owner?.reportId === reportId && x.owner.sourceKey === sourceKey)
      return b ? bridgeListItem(b.id, await directory.listDimensionRefs()) : null
    },
    async getBridgeVersion(versionId) {
      await delay()
      const v = bridgeVersion(versionId)
      return v ? ok(await bridgeContent(v)) : notFound('Bridge version')
    },
    async checkDeleteBridge(id) {
      await delay()
      return bridgeDeleteCheck(id)
    },

    /* bridge edits */

    async updateBridge(id, patch) {
      await delay()
      const b = db.bridges.find((x) => x.id === id)!
      const refused = ownedByReport<Bridge>(b)
      if (refused) return refused
      if (patch.displayName !== undefined && !patch.displayName.trim()) return invalid({ displayName: ['Display name is required.'] })
      Object.assign(b, { ...patch, displayName: patch.displayName?.trim() ?? b.displayName })
      await touch(liveBridgeVersion(id).id)
      return ok(clone(b))
    },
    async updateBridgeVersion(versionId, patch) {
      await delay()
      const v = bridgeVersion(versionId)!
      const refused = ownedByReport<BridgeVersion>(bridgeOf(v)) ?? (v.isLive ? null : frozen<BridgeVersion>())
      if (refused) return refused
      if (!allowedModes(patch.cardinality).includes(patch.allocationMode))
        return invalid({ allocationMode: [`${CARDINALITY_LABEL[patch.cardinality]} does not allow allocation ${ALLOCATION_LABEL[patch.allocationMode].toLowerCase()}.`] })
      Object.assign(v, patch)
      await touch(versionId)
      return ok(clone(v))
    },
    async saveBridgeMapping(versionId, input) {
      await delay()
      const v = bridgeVersion(versionId)!
      const refused = ownedByReport<BridgeMapping>(bridgeOf(v)) ?? (v.isLive ? null : frozen<BridgeMapping>())
      if (refused) return refused
      const errors: Record<string, string[]> = {}
      if (!input.sourceMemberKey) errors.sourceMemberKey = ['Choose a source member.']
      if (!input.targetMemberKey) errors.targetMemberKey = ['Choose a target member.']
      if (!Number.isFinite(input.factor)) errors.factor = ['Factor must be a number.']
      if (!Number.isFinite(input.allocationWeight)) errors.allocationWeight = ['Weight must be a number.']
      if (Object.keys(errors).length) return invalid(errors)
      const existing = db.bridgeMappings.find((m) => m.id === input.id)
      const next: BridgeMapping = { ...input, versionId, sourceSelection: input.sourceSelection ?? existing?.sourceSelection ?? 'member' }
      db.bridgeMappings = [...db.bridgeMappings.filter((m) => m.id !== input.id), next]
      await touch(versionId)
      return ok(clone(next))
    },
    async deleteBridgeMapping(versionId, mappingId) {
      await delay()
      const v = bridgeVersion(versionId)!
      const refused = ownedByReport<null>(bridgeOf(v)) ?? (v.isLive ? null : frozen<null>())
      if (refused) return refused
      db.bridgeMappings = db.bridgeMappings.filter((m) => m.id !== mappingId)
      await touch(versionId)
      return ok(null)
    },
    async replaceBridgeMappings(versionId, rows) {
      await delay()
      const v = bridgeVersion(versionId)!
      const refused = ownedByReport<BridgeMapping[]>(bridgeOf(v)) ?? (v.isLive ? null : frozen<BridgeMapping[]>())
      if (refused) return refused
      const next = rows.map((r): BridgeMapping => ({ ...r, versionId, sourceSelection: r.sourceSelection ?? 'member' }))
      db.bridgeMappings = [...db.bridgeMappings.filter((m) => m.versionId !== versionId), ...next]
      await touch(versionId)
      return ok(clone(next))
    },

    /* bridge commands */

    async createBridge(input) {
      await delay()
      const errors: Record<string, string[]> = {}
      if (!DOTTED_KEY_PATTERN.test(input.key)) errors.key = ['Use letters, digits, dots or underscores, starting with a letter.']
      if (!input.displayName.trim()) errors.displayName = ['Display name is required.']
      if (Object.keys(errors).length) return invalid(errors)
      if (db.bridges.some((b) => b.key === input.key)) return fail(409, 'Key taken', { detail: `Bridge '${input.key}' already exists.` })
      const refs = await directory.listDimensionRefs()
      const src = refs.find((d) => d.id === input.sourceDimensionId)!
      const tgt = refs.find((d) => d.id === input.targetDimensionId)!
      if (src.id === tgt.id) return fail(409, 'Endpoints must differ', { detail: 'Source and target must be different dimensions.' })
      if (!canHaveBridges(src) || !canHaveBridges(tgt)) return fail(409, 'Time dimensions cannot be bridged')
      if (src.purpose === 'report' || tgt.purpose === 'report') return fail(409, 'Report dimensions are bridged by their report', { detail: 'Bridges into report dimensions are created by mapping rows in a report.' })
      const id = uid('bridge')
      const t = now()
      const v: BridgeVersion = { id: uid('ver'), bridgeId: id, snapshotId: LIVE_SNAPSHOT_ID, isLive: true, cardinality: 'many_to_one', allocationMode: 'none', isEnabled: input.isEnabled, createdAt: t }
      db.bridges.push({ id, key: input.key, displayName: input.displayName.trim(), sourceDimensionId: src.id, targetDimensionId: tgt.id, isEnabled: input.isEnabled, owner: null })
      db.bridgeVersions.push(v)
      db.bindings[id] = []
      return ok({ bridgeId: id, versionId: v.id })
    },
    async deleteBridge(id) {
      await delay()
      const check = bridgeDeleteCheck(id)
      if (!check.allowed) return blocked('Bridge cannot be deleted', check)
      const versionIds = new Set(db.bridgeVersions.filter((v) => v.bridgeId === id).map((v) => v.id))
      db.bridgeMappings = db.bridgeMappings.filter((m) => !versionIds.has(m.versionId))
      db.bridgeVersions = db.bridgeVersions.filter((v) => !versionIds.has(v.id))
      db.bridges = db.bridges.filter((b) => b.id !== id)
      delete db.bindings[id]
      return ok(null)
    },
  }
}
