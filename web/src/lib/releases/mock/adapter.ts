import { createSnapshotChangeSet } from '@/lib/api/changes'
import type { Problem } from '@/lib/api/contract'
import {
  RELEASE_KEY_PATTERN,
  SET_KEY_PATTERN,
  type ApiResult,
  type AssumptionSet,
  type Release,
  type ReleaseDetail,
  type ReleaseListItem,
  type ReleasesApi,
  type SetComparison,
  type SetListItem,
} from '../contract'
import { catalog as tenantCatalog } from '@/lib/tenant/mock/data'
import { createSeed, type Db } from './data'
import { activePins, diffReleases, manifestGate, manifestHash, manifestOf, pinRows, readiness, runReleaseChecks } from './rules'

const LATENCY = 90
const wait = () => new Promise((r) => setTimeout(r, LATENCY))
const clone = <T,>(v: T): T => structuredClone(v)
const now = () => new Date().toISOString()
let seq = 0
const newId = (prefix: string) => `${prefix}-${Date.now().toString(36)}-${(seq++).toString(36)}`

const ok = <T,>(data: T): ApiResult<T> => ({ ok: true, data: clone(data) })
const fail = (status: Problem['status'], title: string, more: Partial<Problem> = {}): ApiResult<never> => ({
  ok: false,
  problem: { type: 'about:blank', title, status, errors: {}, ...more },
})
const notFound = (what: string) => fail(404, `${what} not found`)

export function createMockReleasesApi(seed: Db = createSeed()): ReleasesApi {
  const db = seed
  const changeSet = createSnapshotChangeSet(
    () => db.manifests,
    (manifests) => {
      db.manifests = manifests
    },
  )
  const rel = (id: string) => db.releases.find((r) => r.id === id)
  const setOf = (id: string) => db.sets.find((s) => s.id === id)
  const reportOwned = (identityId: string) => !!db.identities.find((i) => i.id === identityId)!.reportId

  const listItem = (r: Release): ReleaseListItem => {
    const isActive = r.id === db.activeReleaseId
    const activation = isActive ? db.activations.findLast((a) => a.releaseId === r.id) : undefined
    return {
      ...r,
      isActive,
      datasetCount: db.manifests.filter((m) => m.releaseId === r.id).length,
      diffVsActive: isActive ? null : diffReleases(db, db.activeReleaseId, r.id),
      activatedAt: activation?.activatedAt ?? null,
      activatedBy: activation?.activatedBy ?? null,
    }
  }

  const detailOf = (r: Release): ReleaseDetail => ({
    release: r,
    isActive: r.id === db.activeReleaseId,
    manifest: manifestOf(db, r.id),
    activations: db.activations.filter((a) => a.releaseId === r.id).reverse(),
  })

  const setItem = (s: AssumptionSet): SetListItem => {
    const rows = pinRows(db, s.id)
    return {
      ...s,
      pinCount: rows.length,
      livePins: rows.filter((p) => p.state === 'live').length,
      changedSinceCapture: s.status === 'historical' ? rows.filter((p) => p.versionId !== p.publishedVersionId).length : 0,
    }
  }

  const labelErrors = (displayName: string, description: string | null) => {
    const errors: Record<string, string[]> = {}
    if (!displayName.trim()) errors.displayName = ['Enter a display name.']
    if ((description ?? '').length > 1024) errors.description = ['1,024 characters at most.']
    return errors
  }

  const transition = (r: Release, to: 'validated' | 'published') => {
    const report = runReleaseChecks(db, r.id, false)
    if (report.violations.length) return ok({ release: r, report })
    r.status = to
    if (to === 'published') {
      r.publishedAt = now()
      r.contentHash = manifestHash(manifestOf(db, r.id))
    }
    return ok({ release: r, report })
  }

  return {
    ...changeSet,

    async listReleases() {
      await wait()
      return clone([...db.releases].sort((a, b) => b.createdAt.localeCompare(a.createdAt)).map(listItem))
    },

    async getRelease(id) {
      await wait()
      const r = rel(id)
      return r ? ok(detailOf(r)) : notFound('Release')
    },

    async diffReleases(fromId, toId) {
      await wait()
      return clone(diffReleases(db, fromId, toId))
    },

    async checkRelease(id) {
      await wait()
      return clone(runReleaseChecks(db, id, changeSet.hasPendingChanges()))
    },

    async listManifestDatasets() {
      await wait()
      return clone(db.datasets)
    },

    async listIdentities() {
      await wait()
      return clone(db.identities)
    },

    async listReportReads() {
      await wait()
      return clone(db.reports)
    },

    async saveManifest(id, manifest) {
      await wait()
      const r = rel(id)
      if (!r) return notFound('Release')
      if (r.status !== 'draft') return fail(409, 'The release is not a draft.')
      const violations = manifestGate(db, manifest)
      if (violations.length) return fail(422, 'The manifest cannot be saved', { detail: violations[0].message, violations })
      db.manifests = [...db.manifests.filter((m) => m.releaseId !== id), ...manifest.map((m) => ({ releaseId: id, ...m }))]
      return ok(detailOf(r))
    },

    async createRelease({ key, copyFromReleaseId }) {
      await wait()
      const k = key.trim()
      if (!RELEASE_KEY_PATTERN.test(k)) return fail(400, 'Invalid key', { errors: { key: ['Use letters, digits, _ . - /, starting with a letter or digit (128 at most).'] } })
      if (db.releases.some((r) => r.key === k)) return fail(409, 'Key taken', { errors: { key: [`A release with key '${k}' already exists.`] } })
      const r: Release = { id: newId('rel'), key: k, status: 'draft', createdAt: now(), publishedAt: null, contentHash: null }
      db.releases.push(r)
      if (copyFromReleaseId) db.manifests.push(...manifestOf(db, copyFromReleaseId).map((m) => ({ releaseId: r.id, ...m })))
      return ok(r)
    },

    async validateRelease(id) {
      await wait()
      const r = rel(id)
      if (!r) return notFound('Release')
      if (r.status !== 'draft') return fail(409, 'The release is not a draft.')
      return transition(r, 'validated')
    },

    async publishRelease(id) {
      await wait()
      const r = rel(id)
      if (!r) return notFound('Release')
      if (r.status !== 'validated') return fail(409, 'Only a validated release can be published.')
      return transition(r, 'published')
    },

    async backToDraft(id) {
      await wait()
      const r = rel(id)
      if (!r) return notFound('Release')
      if (r.status !== 'validated') return fail(409, 'Only a validated release can go back to draft.')
      r.status = 'draft'
      return ok(r)
    },

    async retireRelease(id) {
      await wait()
      const r = rel(id)
      if (!r) return notFound('Release')
      if (r.status !== 'published') return fail(409, 'Only a published release can be retired.')
      if (id === db.activeReleaseId) {
        const detail = `Release '${r.key}' is the active release; activate another release before retiring it.`
        return fail(422, 'Release is active', { detail, violations: [{ code: 'release_not_active', message: detail }] })
      }
      r.status = 'retired'
      return ok(r)
    },

    async activateRelease(id) {
      await wait()
      const r = rel(id)
      if (!r) return notFound('Release')
      if (r.status !== 'published') return fail(409, 'Release is not published', { detail: `${r.key} is ${r.status}; only published releases can be activated.` })
      const activation = { releaseId: id, previousReleaseId: db.activeReleaseId, activatedAt: now(), activatedBy: 'analyst@bank.example' }
      db.activations.push(activation)
      db.activeReleaseId = id
      tenantCatalog.tenant.activeReleaseId = id
      tenantCatalog.tenant.semanticRevision += 1
      return ok(tenantCatalog.tenant)
    },

    async deleteRelease(id) {
      await wait()
      const r = rel(id)
      if (!r) return notFound('Release')
      if (r.status !== 'draft') return fail(409, 'Only a draft can be deleted.', { detail: 'Retire a published release instead.' })
      db.releases = db.releases.filter((x) => x.id !== id)
      db.manifests = db.manifests.filter((m) => m.releaseId !== id)
      return ok(null)
    },

    async listSets() {
      await wait()
      const active = db.sets.find((s) => s.status === 'active')!
      const historical = db.sets.filter((s) => s.status === 'historical').sort((a, b) => b.capturedAt!.localeCompare(a.capturedAt!))
      return clone({ active: setItem(active), historical: historical.map(setItem) })
    },

    async getSet(id) {
      await wait()
      const s = setOf(id)
      if (!s) return notFound('Assumption set')
      const historical = s.status === 'historical'
      return ok({ set: s, readsReleaseId: s.releaseId ?? db.activeReleaseId, pins: pinRows(db, id), readiness: historical ? readiness(db, id) : null })
    },

    async compareSets(fromSetId, toSetId) {
      await wait()
      const pinsOf = (id: string) => new Map(pinRows(db, id).filter((p) => !reportOwned(p.identityId)).map((p) => [p.identityId, p]))
      const from = pinsOf(fromSetId)
      const to = pinsOf(toSetId)
      const result: SetComparison = { fromSetId, toSetId, added: [], removed: [], changed: [], unchanged: 0, release: null }
      for (const [id, p] of to) {
        const f = from.get(id)
        if (!f) result.added.push({ kind: p.kind, identityId: id, versionId: p.versionId })
        else if (f.versionId !== p.versionId) result.changed.push({ kind: p.kind, identityId: id, fromVersionId: f.versionId, toVersionId: p.versionId })
        else result.unchanged++
      }
      for (const [id, p] of from) if (!to.has(id)) result.removed.push({ kind: p.kind, identityId: id, versionId: p.versionId })
      const releaseOf = (id: string) => setOf(id)!.releaseId ?? db.activeReleaseId
      const fromRel = releaseOf(fromSetId)
      const toRel = releaseOf(toSetId)
      if (fromRel !== toRel) result.release = diffReleases(db, fromRel, toRel)
      return clone(result)
    },

    async capturePreview() {
      await wait()
      const pins = activePins(db)
      const count = (kind: string, owned: boolean) => pins.filter((p) => p.kind === kind && reportOwned(p.identityId) === owned).length
      return {
        dimensions: count('dimension', false),
        mappingSets: count('mapping_set', false),
        bridges: count('bridge', false),
        leftOutDimensions: count('dimension', true),
        leftOutBridges: count('bridge', true),
        releaseId: db.activeReleaseId,
      }
    },

    async captureSet({ key, displayName, description }) {
      await wait()
      const k = key.trim()
      const errors = labelErrors(displayName, description)
      if (!SET_KEY_PATTERN.test(k)) errors.key = ['Use a letter, then letters, digits or underscores (128 at most).']
      if (Object.keys(errors).length) return fail(400, 'Cannot capture', { errors })
      if (db.sets.some((s) => s.key === k)) return fail(409, 'Key taken', { errors: { key: [`An assumption set with key '${k}' already exists.`] } })
      const at = now()
      const s: AssumptionSet = { id: newId('set'), key: k, displayName: displayName.trim(), description: description?.trim() || null, status: 'historical', releaseId: db.activeReleaseId, capturedAt: at, createdAt: at }
      db.sets.push(s)
      db.pins.push(...activePins(db).filter((p) => !reportOwned(p.identityId)).map((p) => ({ setId: s.id, ...p })))
      return ok(s)
    },

    async relabelSet(id, { displayName, description }) {
      await wait()
      const s = setOf(id)
      if (!s) return notFound('Assumption set')
      if (s.status === 'active') return fail(409, 'The active set cannot be relabelled.')
      const errors = labelErrors(displayName, description)
      if (Object.keys(errors).length) return fail(400, 'Cannot save', { errors })
      s.displayName = displayName.trim()
      s.description = description?.trim() || null
      return ok(s)
    },

    async deleteSet(id) {
      await wait()
      const s = setOf(id)
      if (!s) return notFound('Assumption set')
      if (s.status === 'active') return fail(409, 'Cannot delete', { detail: `Assumption set 'active' is the active set, which holds the default versions; it cannot be deleted.` })
      db.sets = db.sets.filter((x) => x.id !== id)
      db.pins = db.pins.filter((p) => p.setId !== id)
      return ok(null)
    },
  }
}
