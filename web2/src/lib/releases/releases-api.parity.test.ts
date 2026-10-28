import { beforeEach, describe, expect, it, onTestFinished } from 'vitest'
import { unwrap } from '@/lib/api/contract'
import { createLiveHttp, isLiveRun, liveTenant } from '@/lib/api/live-http'
import type { ReleasesApi } from './contract'
import { createHttpReleasesApi } from './http/adapter'
import { createMockReleasesApi } from './mock/adapter'

/**
 * Parity rule (blueprint 00, 02): the mock and HTTP adapters answer the `ReleasesApi` port the same way, so screens built on the
 * mock keep working against the API. Every test runs on the mock; with `QE_LIVE_API=1` it also runs on the HTTP adapter against a
 * live tenant. Live tests create draft releases and historical sets, delete them when they finish, never publish or activate (the
 * tenant's active release must not move) and never assume an id format.
 */

const adapters = [
  { name: 'mock', create: async () => createMockReleasesApi() },
  ...(isLiveRun ? [{ name: 'http', create: async () => createHttpReleasesApi((await createLiveHttp()).forTenant(liveTenant.tenantId)) }] : []),
]

const uniqueKey = (prefix: string) => `${prefix}${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`

describe.each(adapters)('ReleasesApi port over the $name adapter', ({ create }) => {
  let api: ReleasesApi

  beforeEach(async () => {
    api = await create()
  })

  /** The active release, which every tenant the suite runs on has. */
  async function activeRelease() {
    return (await api.listReleases()).find((r) => r.isActive)!
  }

  /** A draft release copying the active release's manifest, deleted when the test finishes. */
  async function draftRelease() {
    const active = await activeRelease()
    const draft = await unwrap(api.createRelease({ key: uniqueKey('parity/r'), copyFromReleaseId: active.id }))
    onTestFinished(async () => {
      await api.discardChanges()
      await api.deleteRelease(draft.id)
    })
    return { active, draft }
  }

  /** A new release is a draft that starts from the copied release's manifest (R7); the list carries it with its diff from the active one. */
  it('creates a draft copying the active release manifest', async () => {
    const { active, draft } = await draftRelease()

    expect(draft).toMatchObject({ status: 'draft', publishedAt: null, contentHash: null })
    const detail = await unwrap(api.getRelease(draft.id))
    expect(detail.manifest).toEqual((await unwrap(api.getRelease(active.id))).manifest)
    const item = (await api.listReleases()).find((r) => r.id === draft.id)!
    expect(item).toMatchObject({ isActive: false, datasetCount: detail.manifest.length })
    expect(item.diffVsActive).toMatchObject({ fromReleaseId: active.id, toReleaseId: draft.id, added: [], removed: [], changed: [] })
  })

  /** Release keys are unique (R7): a second release with the same key is refused. */
  it('refuses a taken release key', async () => {
    const { draft } = await draftRelease()

    const taken = await api.createRelease({ key: draft.key, copyFromReleaseId: null })

    expect(taken.ok).toBe(false)
  })

  /**
   * Universal editing rule and OI-37: a staged manifest is what reads and the check answer while pending, the check says it ran
   * over unsaved edits, and discarding puts the saved manifest back.
   */
  it('stages a manifest that reads and the check see until discarded', async () => {
    const { draft } = await draftRelease()
    const saved = (await unwrap(api.getRelease(draft.id))).manifest

    api.beginChanges()
    await unwrap(api.saveManifest(draft.id, []))

    expect((await unwrap(api.getRelease(draft.id))).manifest).toEqual([])
    const report = await api.checkRelease(draft.id)
    expect(report.unsaved).toBe(true)
    expect(report.violations.map((v) => v.code)).toContain('manifest_not_empty')
    await api.discardChanges()
    expect((await unwrap(api.getRelease(draft.id))).manifest).toEqual(saved)
  })

  /** A manifest that pins one dataset's version under another dataset is refused (a release gate), and nothing is staged. */
  it('refuses pinning a version under another dataset', async () => {
    const { draft } = await draftRelease()
    const [first, second] = (await api.listManifestDatasets()).filter((d) => d.versions.length > 0)

    api.beginChanges()
    const result = await api.saveManifest(draft.id, [{ datasetId: first.id, datasetVersionId: second.versions[0].id }])

    expect(result.ok).toBe(false)
    if (!result.ok) expect(result.problem.status).toBe(422)
  })

  /** Validate answers a failed rule run as data: the release stays a draft and the report names the broken rules. */
  it('answers a failed validation as a report', async () => {
    const { draft } = await draftRelease()
    api.beginChanges()
    await unwrap(api.saveManifest(draft.id, []))
    await unwrap(api.commitChanges())

    const result = await unwrap(api.validateRelease(draft.id))

    expect(result.release.status).toBe('draft')
    expect(result.report.violations.map((v) => v.code)).toContain('manifest_not_empty')
  })

  /** Only a draft is deleted (R6): deleting the active release is refused. */
  it('refuses deleting a release that is not a draft', async () => {
    const active = await activeRelease()

    const result = await api.deleteRelease(active.id)

    expect(result.ok).toBe(false)
    if (!result.ok) expect(result.problem.status).toBe(409)
  })

  /** The active set holds the default versions (R8): it is neither relabelled nor deleted. */
  it('refuses relabelling or deleting the active set', async () => {
    const { active } = await api.listSets()

    const relabel = await api.relabelSet(active.id, { displayName: 'Renamed', description: null })
    const remove = await api.deleteSet(active.id)

    expect([relabel.ok, remove.ok]).toEqual([false, false])
  })

  /** A capture is a historical set over the active release (R9) that can be relabelled, but not its key, and deleted (R10). */
  it('captures, relabels and deletes a historical set', async () => {
    const preview = await api.capturePreview()
    const key = uniqueKey('parity_s')

    const set = await unwrap(api.captureSet({ key, displayName: 'Parity capture', description: null }))
    onTestFinished(async () => {
      await api.deleteSet(set.id)
    })

    expect(set).toMatchObject({ key, status: 'historical', releaseId: preview.releaseId })
    const detail = await unwrap(api.getSet(set.id))
    expect(detail.pins.length).toBe(preview.dimensions + preview.mappingSets + preview.bridges)
    expect(detail.pins.every((p) => p.state !== 'report_owned')).toBe(true)
    expect((await unwrap(api.relabelSet(set.id, { displayName: 'Renamed', description: 'Parity' }))).displayName).toBe('Renamed')
    expect((await api.listSets()).historical.map((s) => s.id)).toContain(set.id)
    expect((await api.captureSet({ key, displayName: 'Again', description: null })).ok).toBe(false)
  })
})
