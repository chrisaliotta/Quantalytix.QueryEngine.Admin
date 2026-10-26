import {
  RELEASE_KEY_PATTERN,
  SET_KEY_PATTERN,
  type ApiResult,
  type AssumptionSet,
  type CheckReport,
  type ManifestEntry,
  type Release,
  type ReleaseDetail,
  type ReleaseListItem,
  type ReleasesApi,
  type SetComparison,
  type SetListItem,
} from '../contract'
import { activePins, diffManifests, manifestOf, pinRows, readiness, runReleaseChecks } from '../rules'
import { createSeed, type Db } from './data'

const LATENCY = 90
const wait = () => new Promise((r) => setTimeout(r, LATENCY))
const clone = <T,>(v: T): T => structuredClone(v)
const now = () => new Date().toISOString()
let seq = 0
const newId = (prefix: string) => `${prefix}-${Date.now().toString(36)}-${(seq++).toString(36)}`

const ok = <T,>(data: T): ApiResult<T> => ({ ok: true, data: clone(data) })
const fail = (title: string, errors: Record<string, string[]> = {}, detail?: string): ApiResult<never> => ({ ok: false, problem: { title, errors, detail } })

export function createMockReleasesApi(seed: Db = createSeed()): ReleasesApi {
  const db = seed
  const rel = (id: string) => db.releases.find((r) => r.id === id)
  const setOf = (id: string) => db.sets.find((s) => s.id === id)
  const bump = () => {
    db.tenant.semanticRevision++
  }

  const listItem = (r: Release): ReleaseListItem => {
    const isActive = r.id === db.tenant.activeReleaseId
    return {
      ...r,
      isActive,
      datasetCount: db.manifests.filter((m) => m.releaseId === r.id).length,
      diffVsActive: isActive ? null : diffManifests(db, db.tenant.activeReleaseId, manifestOf(db, db.tenant.activeReleaseId), r.id, manifestOf(db, r.id)),
    }
  }

  const setItem = (s: AssumptionSet): SetListItem => {
    const rows = pinRows(db, s.id)
    return {
      ...s,
      pinCount: rows.length,
      livePins: rows.filter((p) => p.state === 'live').length,
      changedSinceCapture: s.status === 'historical' ? rows.filter((p) => p.versionId !== p.publishedVersionId).length : 0,
    }
  }

  const check = (id: string, manifest?: ManifestEntry[]): CheckReport => runReleaseChecks(db, id, manifest ?? manifestOf(db, id), !!manifest)

  return {
    async getCatalog() {
      await wait()
      return clone({ tenant: db.tenant, datasets: db.datasets, versions: db.versions, identities: db.identities, reports: db.reports, releases: db.releases })
    },

    async listReleases() {
      await wait()
      return clone([...db.releases].sort((a, b) => b.createdAt.localeCompare(a.createdAt)).map(listItem))
    },

    async getRelease(id) {
      await wait()
      const r = rel(id)
      if (!r) return fail('Release not found')
      const isActive = id === db.tenant.activeReleaseId
      const detail: ReleaseDetail = { release: r, isActive, manifest: db.manifests.filter((m) => m.releaseId === id) }
      return ok(detail)
    },

    async diffReleases(fromId, toId, toManifest) {
      await wait()
      return clone(diffManifests(db, fromId, manifestOf(db, fromId), toId, toManifest ?? manifestOf(db, toId)))
    },

    async checkRelease(id, manifest) {
      await wait()
      return clone(check(id, manifest))
    },

    async createRelease({ key, copyFromReleaseId }) {
      await wait()
      const k = key.trim()
      if (!RELEASE_KEY_PATTERN.test(k)) return fail('Invalid key', { key: ['Use letters, digits, _ . - /, starting with a letter or digit (128 at most).'] })
      if (db.releases.some((r) => r.key === k)) return fail('Key taken', { key: [`A release with key '${k}' already exists.`] })
      const r: Release = { id: newId('rel'), key: k, status: 'draft', createdAt: now(), publishedAt: null }
      db.releases.push(r)
      if (copyFromReleaseId) db.manifests.push(...manifestOf(db, copyFromReleaseId).map((m) => ({ releaseId: r.id, ...m })))
      return ok(r)
    },

    async saveManifest(id, manifest) {
      await wait()
      const r = rel(id)
      if (!r) return fail('Release not found')
      if (r.status !== 'draft') return fail('The release is not a draft.')
      db.manifests = [...db.manifests.filter((m) => m.releaseId !== id), ...manifest.map((m) => ({ releaseId: id, ...m }))]
      return ok(r)
    },

    async validateRelease(id) {
      await wait()
      const r = rel(id)
      if (!r) return fail('Release not found')
      if (r.status !== 'draft') return fail('The release is not a draft.')
      const report = check(id)
      if (!report.violations.length) r.status = 'validated'
      return ok({ release: r, report })
    },

    async publishRelease(id) {
      await wait()
      const r = rel(id)
      if (!r) return fail('Release not found')
      if (r.status !== 'validated') return fail('Only a validated release can be published.')
      const report = check(id)
      if (!report.violations.length) {
        r.status = 'published'
        r.publishedAt = now()
      }
      return ok({ release: r, report })
    },

    async backToDraft(id) {
      await wait()
      const r = rel(id)
      if (!r || r.status !== 'validated') return fail('Only a validated release can go back to draft.')
      r.status = 'draft'
      return ok(r)
    },

    async retireRelease(id) {
      await wait()
      const r = rel(id)
      if (!r) return fail('Release not found')
      if (id === db.tenant.activeReleaseId) return fail('Release is active', {}, `Release '${r.key}' is the active release; activate another release before retiring it.`)
      if (r.status !== 'published') return fail('Only a published release can be retired.')
      r.status = 'retired'
      return ok(r)
    },

    async activateRelease(id) {
      await wait()
      const r = rel(id)
      if (!r) return fail('Release not found')
      if (r.status !== 'published') return fail('Release is not published', {}, `${r.key} is ${r.status}; only published releases can be activated.`)
      db.tenant.activeReleaseId = id
      bump()
      return ok(db.tenant)
    },

    async deleteRelease(id) {
      await wait()
      const r = rel(id)
      if (!r) return fail('Release not found')
      if (r.status !== 'draft') return fail('Only a draft can be deleted.', {}, 'Retire a published release instead.')
      db.releases = db.releases.filter((x) => x.id !== id)
      db.manifests = db.manifests.filter((m) => m.releaseId !== id)
      return ok(null)
    },

    async listSets() {
      await wait()
      const active = db.sets.find((s) => s.status === 'active') as AssumptionSet
      const historical = db.sets.filter((s) => s.status === 'historical').sort((a, b) => (b.capturedAt ?? '').localeCompare(a.capturedAt ?? ''))
      return clone({ active: setItem(active), historical: historical.map(setItem) })
    },

    async getSet(id) {
      await wait()
      const s = setOf(id)
      if (!s) return fail('Assumption set not found')
      const rows = pinRows(db, id)
      const readsReleaseId = s.releaseId ?? db.tenant.activeReleaseId
      return ok({ set: s, readsReleaseId, pins: rows, readiness: s.status === 'historical' ? readiness(db, id) : null })
    },

    async compareSets(fromSetId, toSetId) {
      await wait()
      const owned = new Set(db.identities.filter((i) => i.reportId).map((i) => i.id))
      const pinsOf = (id: string) => new Map(pinRows(db, id).filter((p) => !owned.has(p.identityId)).map((p) => [p.identityId, p]))
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
      const releaseOf = (id: string) => setOf(id)?.releaseId ?? db.tenant.activeReleaseId
      const fromRel = releaseOf(fromSetId)
      const toRel = releaseOf(toSetId)
      if (fromRel !== toRel) result.release = diffManifests(db, fromRel, manifestOf(db, fromRel), toRel, manifestOf(db, toRel))
      return clone(result)
    },

    async capturePreview() {
      await wait()
      const pins = activePins(db)
      const owned = (id: string) => !!db.identities.find((i) => i.id === id)?.reportId
      const count = (kind: string, reportOwned: boolean) => pins.filter((p) => p.kind === kind && owned(p.identityId) === reportOwned).length
      return {
        dimensions: count('dimension', false),
        mappingSets: count('mapping_set', false),
        bridges: count('bridge', false),
        leftOutDimensions: count('dimension', true),
        leftOutBridges: count('bridge', true),
        releaseId: db.tenant.activeReleaseId,
      }
    },

    async captureSet({ key, displayName, description }) {
      await wait()
      const k = key.trim()
      const errors: Record<string, string[]> = {}
      if (!SET_KEY_PATTERN.test(k)) errors.key = ['Use a letter, then letters, digits or underscores (128 at most).']
      else if (db.sets.some((s) => s.key === k)) errors.key = [`An assumption set with key '${k}' already exists.`]
      if (!displayName.trim()) errors.displayName = ['Enter a display name.']
      if ((description ?? '').length > 1024) errors.description = ['1,024 characters at most.']
      if (Object.keys(errors).length) return fail('Cannot capture', errors)
      const at = now()
      const s: AssumptionSet = { id: newId('set'), key: k, displayName: displayName.trim(), description: description?.trim() || null, status: 'historical', releaseId: db.tenant.activeReleaseId, capturedAt: at, createdAt: at }
      db.sets.push(s)
      const owned = (id: string) => !!db.identities.find((i) => i.id === id)?.reportId
      db.pins.push(...activePins(db).filter((p) => !owned(p.identityId)).map((p) => ({ setId: s.id, ...p })))
      bump()
      return ok(s)
    },

    async relabelSet(id, { displayName, description }) {
      await wait()
      const s = setOf(id)
      if (!s) return fail('Assumption set not found')
      const errors: Record<string, string[]> = {}
      if (!displayName.trim()) errors.displayName = ['Enter a display name.']
      if ((description ?? '').length > 1024) errors.description = ['1,024 characters at most.']
      if (Object.keys(errors).length) return fail('Cannot save', errors)
      s.displayName = displayName.trim()
      s.description = description?.trim() || null
      return ok(s)
    },

    async deleteSet(id) {
      await wait()
      const s = setOf(id)
      if (!s) return fail('Assumption set not found')
      if (s.status === 'active') return fail('Cannot delete', {}, `Assumption set 'active' is the active set, which holds the default versions; it cannot be deleted.`)
      db.sets = db.sets.filter((x) => x.id !== id)
      db.pins = db.pins.filter((p) => p.setId !== id)
      bump()
      return ok(null)
    },
  }
}
