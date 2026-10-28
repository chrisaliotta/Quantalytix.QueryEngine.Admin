import { beforeEach, describe, expect, it, onTestFinished } from 'vitest'
import { unwrap } from '@/lib/api/contract'
import { createLiveHttp, isLiveRun, liveTenant } from '@/lib/api/live-http'
import type { DimensionMemberInput, DimensionsApi, DimensionVersionContent, Id } from './contract'
import { createHttpDimensionsApi } from './http/adapter'
import { createMockDimensionsApi } from './mock/adapter'

/**
 * Parity rule (blueprint 00, 02): the mock and HTTP adapters answer the `DimensionsApi` port the same way, so screens
 * built on the mock keep working against the API. Every test runs on the mock; with `QE_LIVE_API=1` it also runs on
 * the HTTP adapter against a live tenant. Live tests create and delete their own user dimension and never assume an
 * id format.
 */

const adapters = [
  { name: 'mock', create: async () => createMockDimensionsApi() },
  ...(isLiveRun
    ? [{ name: 'http', create: async () => createHttpDimensionsApi((await createLiveHttp()).forTenant(liveTenant.tenantId)) }]
    : []),
]

const uniqueKey = () => `Parity${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`

const memberInput = (key: string, parentId: Id | null = null): DimensionMemberInput => ({
  key,
  displayName: key,
  description: null,
  parentId,
  levelId: null,
  isAssignable: true,
  sortOrder: null,
  properties: {},
})

const memberByKey = (content: DimensionVersionContent, key: string) => content.members.find((m) => m.key === key)!

describe.each(adapters)('DimensionsApi port over the $name adapter', ({ create }) => {
  let api: DimensionsApi

  beforeEach(async () => {
    api = await create()
  })

  /** A removable hierarchical user dimension, deleted when the test finishes. */
  async function userDimension() {
    const key = uniqueKey()
    const created = await unwrap(
      api.createDimension({ key, displayName: key, kind: 'hierarchical', rangeDataType: null, firstRole: { key: `${key}Role`, displayName: key }, isEnabled: true, isRemovable: true }),
    )
    onTestFinished(async () => {
      await api.discardChanges()
      await api.deleteDimension(created.dimensionId)
    })
    return created
  }

  /** Commits a parent and a child member and returns the stored version. */
  async function committedTree(versionId: Id) {
    api.beginChanges()
    const parent = await unwrap(api.saveMember(versionId, memberInput('Parent')))
    await unwrap(api.saveMember(versionId, memberInput('Child', parent.id)))
    await unwrap(api.commitChanges())
    return unwrap(api.getVersion(versionId))
  }

  /** M2: a created user dimension starts with a live version 1 and is listed under origin `user` only. */
  it('creates a user dimension whose version 1 is live', async () => {
    const { dimensionId, versionId } = await userDimension()

    const detail = await unwrap(api.getDimension(dimensionId))
    expect(detail.origin).toBe('user')
    expect(detail.versions).toMatchObject([{ id: versionId, versionNumber: 1, status: 'published' }])
    expect((await api.listDimensions('user')).map((d) => d.id)).toContain(dimensionId)
    expect((await api.listDimensions('system')).map((d) => d.id)).not.toContain(dimensionId)
  })

  /**
   * Universal editing rule: staged edits are visible to reads while pending, a later edit can reference an item
   * created earlier in the batch, and the commit's `idMap` turns every temporary id into the stored id.
   */
  it('stages members until commit and maps temporary ids to stored ids', async () => {
    const { versionId } = await userDimension()

    api.beginChanges()
    const parent = await unwrap(api.saveMember(versionId, memberInput('Parent')))
    const child = await unwrap(api.saveMember(versionId, memberInput('Child', parent.id)))
    expect(child.parentId).toBe(parent.id)

    const pending = await unwrap(api.getVersion(versionId))
    expect(pending.members.map((m) => m.key).sort()).toEqual(['Child', 'Parent'])
    expect(api.hasPendingChanges()).toBe(true)

    const { idMap } = await unwrap(api.commitChanges())
    expect(api.hasPendingChanges()).toBe(false)
    const stored = await unwrap(api.getVersion(versionId))
    const storedId = (id: Id) => idMap[id] ?? id
    expect(memberByKey(stored, 'Parent').id).toBe(storedId(parent.id))
    expect(memberByKey(stored, 'Child').id).toBe(storedId(child.id))
    expect(memberByKey(stored, 'Child').parentId).toBe(storedId(parent.id))
    expect(stored.members.some((m) => m.id in idMap)).toBe(false)
  })

  /** Discard drops every pending edit; nothing reaches the stored version. */
  it('discards pending edits', async () => {
    const { versionId } = await userDimension()

    api.beginChanges()
    await unwrap(api.saveMember(versionId, memberInput('Dropped')))
    await api.discardChanges()

    expect(api.hasPendingChanges()).toBe(false)
    expect((await unwrap(api.getVersion(versionId))).members).toEqual([])
  })

  /** Member keys are unique per version: a duplicate is refused as invalid input on `key` and leaves the batch as it was. */
  it('refuses a duplicate member key without recording it', async () => {
    const { versionId } = await userDimension()

    api.beginChanges()
    await unwrap(api.saveMember(versionId, memberInput('Same')))
    const duplicate = await api.saveMember(versionId, memberInput('Same'))

    expect(duplicate.ok).toBe(false)
    if (duplicate.ok) return
    expect(duplicate.problem.status).toBe(400)
    expect(Object.keys(duplicate.problem.errors)).toContain('key')
    expect((await unwrap(api.getVersion(versionId))).members.map((m) => m.key)).toEqual(['Same'])
  })

  /** Delete checks: a member with children is blocked with the `has_children` reason. */
  it('blocks deleting a member that has children', async () => {
    const { versionId } = await userDimension()
    const stored = await committedTree(versionId)

    const check = await api.checkDelete({ type: 'member', versionId, memberId: memberByKey(stored, 'Parent').id })

    expect(check.allowed).toBe(false)
    expect(check.reasons.map((r) => r.code)).toContain('has_children')
  })

  /** OI-14: freezing the live version retires it and continues in a new live version n+1. */
  it('freezes the live version into a new live version', async () => {
    const { dimensionId, versionId } = await userDimension()

    const { live } = await unwrap(api.freezeVersion(versionId, 'Parity freeze'))

    expect(live).toMatchObject({ versionNumber: 2, status: 'published' })
    const versions = (await unwrap(api.getDimension(dimensionId))).versions
    expect(versions.find((v) => v.id === versionId)?.status).toBe('retired')
  })

  /** Only frozen versions can be deleted; deleting the live version is a conflict. */
  it('deletes a frozen version and refuses the live one', async () => {
    const { dimensionId, versionId } = await userDimension()
    const { live } = await unwrap(api.freezeVersion(versionId, null))

    const refused = await api.deleteVersion(live!.id)
    expect(refused.ok).toBe(false)
    if (!refused.ok) expect(refused.problem.status).toBe(409)

    await unwrap(api.deleteVersion(versionId))
    expect((await unwrap(api.getDimension(dimensionId))).versions.map((v) => v.id)).toEqual([live!.id])
  })

  /** A removable user dimension deletes, after which it is not found. */
  it('deletes a removable user dimension', async () => {
    const key = uniqueKey()
    const { dimensionId } = await unwrap(
      api.createDimension({ key, displayName: key, kind: 'categorical', rangeDataType: null, firstRole: { key: `${key}Role`, displayName: key }, isEnabled: true, isRemovable: true }),
    )

    await unwrap(api.deleteDimension(dimensionId))

    const gone = await api.getDimension(dimensionId)
    expect(gone.ok).toBe(false)
    if (!gone.ok) expect(gone.problem.status).toBe(404)
  })
})
