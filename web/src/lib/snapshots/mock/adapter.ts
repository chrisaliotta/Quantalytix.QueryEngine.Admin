import type { Problem } from '@/lib/api/contract'
import { catalog as tenantCatalog, catalogSnapshotsOf } from '@/lib/tenant/mock/data'
import {
  COUNT_OF,
  SNAPSHOT_KEY_PATTERN,
  type ApiResult,
  type Snapshot,
  type SnapshotComparison,
  type SnapshotCounts,
  type SnapshotEntry,
  type SnapshotListItem,
  type SnapshotRestore,
  type SnapshotsApi,
} from '../contract'
import { seedContent, seedEntries, seedRestores, seedSnapshots } from './data'

const LATENCY = 90
const wait = () => new Promise((r) => setTimeout(r, LATENCY))
const clone = <T,>(v: T): T => structuredClone(v)
const now = () => new Date().toISOString()
const CALLER = 'analyst@bank.example'

const ok = <T,>(data: T): ApiResult<T> => ({ ok: true, data: clone(data) })
const fail = (status: Problem['status'], title: string, more: Partial<Problem> = {}): ApiResult<never> => ({
  ok: false,
  problem: { type: 'about:blank', title, status, errors: {}, ...more },
})
const notFound = () => fail(404, 'Snapshot not found')
const liveRefused = (action: string) => fail(409, `The live snapshot cannot be ${action}.`)

function countsOf(entries: SnapshotEntry[]): SnapshotCounts {
  const counts: SnapshotCounts = { datasets: 0, dimensions: 0, mappingSets: 0, bridges: 0, calculationModels: 0, reports: 0 }
  for (const e of entries) counts[COUNT_OF[e.kind]]++
  return counts
}

function labelErrors(displayName: string, description: string | null) {
  const errors: Record<string, string[]> = {}
  if (!displayName.trim()) errors.displayName = ['Enter a display name.']
  if ((description ?? '').length > 1024) errors.description = ['1,024 characters at most.']
  return errors
}

/**
 * In-memory `SnapshotsApi` over its own seed. Every command that changes what the engine reads advances the shared mock tenant's
 * `semanticRevision` and keeps its catalog's snapshot list current, as the API does.
 */
export function createMockSnapshotsApi(): SnapshotsApi {
  const snapshots = clone(seedSnapshots)
  const entries = clone(seedEntries)
  const content = clone(seedContent)
  const restores = clone(seedRestores)
  let seq = 0

  const find = (id: string) => snapshots.find((s) => s.id === id)
  const live = () => snapshots.find((s) => s.isLive)!
  const restoresOf = (id: string) => restores.filter((r) => r.snapshotId === id).sort((a, b) => b.restoredAt.localeCompare(a.restoredAt))
  const item = (s: Snapshot): SnapshotListItem => ({ ...s, counts: countsOf(entries[s.id]), lastRestore: restoresOf(s.id)[0] ?? null })

  function advance() {
    tenantCatalog.tenant.semanticRevision += 1
    tenantCatalog.snapshots = catalogSnapshotsOf(snapshots)
  }

  return {
    async listSnapshots() {
      await wait()
      const frozen = snapshots.filter((s) => !s.isLive).sort((a, b) => b.capturedAt!.localeCompare(a.capturedAt!))
      return clone({ live: item(live()), frozen: frozen.map(item) })
    },

    async getSnapshot(id) {
      await wait()
      const s = find(id)
      return s ? ok({ snapshot: s, entries: entries[id], restores: restoresOf(id) }) : notFound()
    },

    async compareSnapshots(fromId, toId) {
      await wait()
      const from = new Map(entries[fromId].map((e) => [e.identityId, e]))
      const to = new Map(entries[toId].map((e) => [e.identityId, e]))
      const result: SnapshotComparison = { fromSnapshotId: fromId, toSnapshotId: toId, added: [], removed: [], common: [] }
      for (const [id, e] of to) {
        const f = from.get(id)
        if (f) result.common.push({ kind: e.kind, identityId: id, key: e.key, fromVersionId: f.versionId, toVersionId: e.versionId, differs: content[fromId][id] !== content[toId][id] })
        else result.added.push(e)
      }
      for (const [id, e] of from) if (!to.has(id)) result.removed.push(e)
      return clone(result)
    },

    async capturePreview() {
      await wait()
      return countsOf(entries[live().id])
    },

    async captureSnapshot({ key, displayName, description }) {
      await wait()
      const errors = labelErrors(displayName, description)
      if (!SNAPSHOT_KEY_PATTERN.test(key)) errors.key = ['Use a letter, then letters, digits, underscores or dots (128 at most).']
      if (Object.keys(errors).length) return fail(400, 'Cannot capture', { errors })
      if (snapshots.some((s) => s.key === key)) return fail(409, 'Key taken', { errors: { key: [`A snapshot with key '${key}' already exists.`] } })
      const at = now()
      const s: Snapshot = { id: `snap_${key}_${seq++}`, key, displayName: displayName.trim(), description: description?.trim() || null, isLive: false, capturedAt: at, capturedBy: CALLER, createdAt: at }
      snapshots.push(s)
      entries[s.id] = entries[live().id].map((e) => ({ ...e, versionId: `${e.identityId}@${key}` }))
      content[s.id] = { ...content[live().id] }
      advance()
      return ok(s)
    },

    async restoreSnapshot(id) {
      await wait()
      const s = find(id)
      if (!s) return notFound()
      if (s.isLive) return liveRefused('restored')
      const liveId = live().id
      const liveVersions = new Map(entries[liveId].map((e) => [e.identityId, e.versionId]))
      entries[liveId] = entries[id].map((e) => ({ ...e, versionId: liveVersions.get(e.identityId) ?? `${e.identityId}@live.${seq++}` }))
      content[liveId] = { ...content[id] }
      restores.push({ snapshotId: id, restoredAt: now(), restoredBy: CALLER } satisfies SnapshotRestore)
      advance()
      return ok(tenantCatalog.tenant)
    },

    async relabelSnapshot(id, { displayName, description }) {
      await wait()
      const s = find(id)
      if (!s) return notFound()
      if (s.isLive) return liveRefused('relabelled')
      const errors = labelErrors(displayName, description)
      if (Object.keys(errors).length) return fail(400, 'Cannot save', { errors })
      s.displayName = displayName.trim()
      s.description = description?.trim() || null
      tenantCatalog.snapshots = catalogSnapshotsOf(snapshots)
      return ok(s)
    },

    async deleteSnapshot(id) {
      await wait()
      const s = find(id)
      if (!s) return notFound()
      if (s.isLive) return liveRefused('deleted')
      snapshots.splice(snapshots.indexOf(s), 1)
      delete entries[id]
      delete content[id]
      restores.splice(0, restores.length, ...restores.filter((r) => r.snapshotId !== id))
      advance()
      return ok(null)
    },
  }
}
