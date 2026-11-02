import { beforeEach, describe, expect, it, onTestFinished } from 'vitest'
import { unwrap } from '@/lib/api/contract'
import { createLiveHttp, isLiveRun, liveTenant } from '@/lib/api/live-http'
import { COUNT_OF, type SnapshotsApi } from './contract'
import { createHttpSnapshotsApi } from './http/adapter'
import { createMockSnapshotsApi } from './mock/adapter'

/**
 * Parity rule (blueprint 00, 02): the mock and HTTP adapters answer the `SnapshotsApi` port the same way, so screens built on the
 * mock keep working against the API. Every test runs on the mock; with `QE_LIVE_API=1` it also runs on the HTTP adapter against a
 * live tenant. Live tests capture snapshots and delete them when they finish, never restore (live must not change) and never
 * assume an id format.
 */

const adapters = [
  { name: 'mock', create: async () => createMockSnapshotsApi() },
  ...(isLiveRun ? [{ name: 'http', create: async () => createHttpSnapshotsApi((await createLiveHttp()).forTenant(liveTenant.tenantId)) }] : []),
]

const uniqueKey = () => `parity_${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`

describe.each(adapters)('SnapshotsApi port over the $name adapter', ({ create }) => {
  let api: SnapshotsApi

  beforeEach(async () => {
    api = await create()
  })

  /** A frozen snapshot captured now, deleted when the test finishes. */
  async function capture() {
    const snapshot = await unwrap(api.captureSnapshot({ key: uniqueKey(), displayName: 'Parity capture', description: null }))
    onTestFinished(async () => {
      await api.deleteSnapshot(snapshot.id)
    })
    return snapshot
  }

  /** R2: exactly one snapshot is live, keyed `live`; it is neither relabelled, deleted nor restored. */
  it('refuses relabelling, deleting or restoring the live snapshot', async () => {
    const { live } = await api.listSnapshots()

    const results = await Promise.all([api.relabelSnapshot(live.id, { displayName: 'Renamed', description: null }), api.deleteSnapshot(live.id), api.restoreSnapshot(live.id)])

    expect(live).toMatchObject({ key: 'live', isLive: true, capturedAt: null, lastRestore: null })
    expect(results.map((r) => (r.ok ? 200 : r.problem.status))).toEqual([409, 409, 409])
  })

  /** R4: capture copies every live version of every kind, so the new snapshot holds what the preview counted, under new version ids. */
  it('captures every live version into a frozen snapshot', async () => {
    const preview = await api.capturePreview()

    const snapshot = await capture()

    expect(snapshot).toMatchObject({ isLive: false, displayName: 'Parity capture' })
    const { live, frozen } = await api.listSnapshots()
    expect(frozen.find((s) => s.id === snapshot.id)?.counts).toEqual(preview)
    const detail = await unwrap(api.getSnapshot(snapshot.id))
    for (const [kind, field] of Object.entries(COUNT_OF)) expect(detail.entries.filter((e) => e.kind === kind).length).toBe(preview[field])
    const comparison = await api.compareSnapshots(live.id, snapshot.id)
    expect(comparison).toMatchObject({ added: [], removed: [] })
    expect(comparison.common.every((p) => p.fromVersionId !== p.toVersionId)).toBe(true)
  })

  /** R12: snapshot keys are unique; a second capture under a taken key is refused. */
  it('refuses a taken snapshot key', async () => {
    const snapshot = await capture()

    const again = await api.captureSnapshot({ key: snapshot.key, displayName: 'Again', description: null })

    expect(again.ok ? 201 : again.problem.status).toBe(409)
  })

  /** R8: relabelling changes the labels of a frozen snapshot, never its key. */
  it('relabels a frozen snapshot', async () => {
    const snapshot = await capture()

    const relabelled = await unwrap(api.relabelSnapshot(snapshot.id, { displayName: 'Renamed', description: 'Parity' }))

    expect(relabelled).toMatchObject({ id: snapshot.id, key: snapshot.key, displayName: 'Renamed', description: 'Parity' })
  })
})
