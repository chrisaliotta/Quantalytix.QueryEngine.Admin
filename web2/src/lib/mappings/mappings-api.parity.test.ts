import { beforeEach, describe, expect, it, onTestFinished } from 'vitest'
import { unwrap } from '@/lib/api/contract'
import { createLiveHttp, isLiveRun, liveTenant } from '@/lib/api/live-http'
import { createHttpTenantApi } from '@/lib/tenant/http/adapter'
import { canHaveBridges, canHaveMappingSets, newTempId, type MappingDimensionRef, type MappingsApi, type MappingsDirectory } from './contract'
import { createTenantDirectory } from './directory'
import { createHttpMappingsApi } from './http/adapter'
import { createMockMappingsApi } from './mock/adapter'
import { createMockMappingLookups } from './mock/lookups'

/**
 * Parity rule (blueprint 00, 02): the mock and HTTP adapters answer the `MappingsApi` port the same way, so the mapping set and bridge
 * editors built on the mock keep working against the API. Every test runs on the mock; with `QE_LIVE_API=1` it also runs on the HTTP
 * adapter against a live tenant. Tests pick their dimensions from the directory, create and delete their own mapping sets and bridges,
 * and never assume an id format.
 */

const adapters = [
  {
    name: 'mock',
    create: async () => {
      const directory = createTenantDirectory(createMockMappingLookups())
      return { api: createMockMappingsApi(directory), directory }
    },
  },
  ...(isLiveRun
    ? [
        {
          name: 'http',
          create: async () => {
            const http = await createLiveHttp()
            return { api: createHttpMappingsApi(http.forTenant(liveTenant.tenantId)), directory: createTenantDirectory(createHttpTenantApi(http, liveTenant.tenantId)) }
          },
        },
      ]
    : []),
]

const uniqueKey = () => `parity.m${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`

describe.each(adapters)('MappingsApi port over the $name adapter', ({ create }) => {
  let api: MappingsApi
  let directory: MappingsDirectory

  beforeEach(async () => {
    ;({ api, directory } = await create())
  })

  /** Live analytical dimensions that pass a test, with the key of an assignable member of each. */
  async function liveDimensions(accepts: (d: MappingDimensionRef) => boolean) {
    const found: { ref: MappingDimensionRef; memberKey: string }[] = []
    for (const ref of await directory.listDimensionRefs()) {
      if (ref.purpose !== 'analytical' || !ref.publishedVersionId || !accepts(ref)) continue
      const member = (await directory.getPublishedMembers(ref.id))?.find((m) => m.isAssignable)
      if (member) found.push({ ref, memberKey: member.key })
    }
    return found
  }

  /** A new mapping set onto a live dimension that takes mapping sets, deleted when the test finishes. */
  async function mappingSet() {
    const [{ ref, memberKey }] = await liveDimensions(canHaveMappingSets)
    const key = uniqueKey()
    const created = await unwrap(api.createMappingSet({ key, displayName: key, sourceNamespace: 'parity', dimensionId: ref.id }))
    onTestFinished(async () => {
      await api.discardChanges()
      await api.deleteMappingSet(created.mappingSetId)
    })
    return { ...created, memberKey }
  }

  /** A new analytical bridge between two live dimensions, deleted when the test finishes. */
  async function bridge() {
    const [source, target] = await liveDimensions(canHaveBridges)
    const key = uniqueKey()
    const created = await unwrap(api.createBridge({ key, displayName: key, sourceDimensionId: source.ref.id, targetDimensionId: target.ref.id, isEnabled: true }))
    onTestFinished(async () => {
      await api.discardChanges()
      await api.deleteBridge(created.bridgeId)
    })
    return { ...created, sourceKey: source.memberKey, targetKey: target.memberKey }
  }

  /** OI-27: a created mapping set starts with an empty live version 1. */
  it('creates a mapping set with an empty live version 1', async () => {
    const { mappingSetId, versionId } = await mappingSet()

    const detail = await unwrap(api.getMappingSet(mappingSetId))
    expect(detail.versions).toMatchObject([{ id: versionId, versionNumber: 1, status: 'published', entryCount: 0 }])
    expect((await unwrap(api.getMappingVersion(versionId))).entries).toEqual([])
  })

  /**
   * Universal editing rule: a staged entry is visible to reads while pending under its temporary id, and the commit's `idMap` turns
   * it into the stored id.
   */
  it('stages an entry until commit and maps its temporary id', async () => {
    const { versionId, memberKey } = await mappingSet()

    api.beginChanges()
    const entry = await unwrap(api.saveEntry(versionId, { id: newTempId(), sourceKeyValue: '003000', memberKey }))
    expect((await unwrap(api.getMappingVersion(versionId))).entries.map((e) => e.id)).toEqual([entry.id])

    const { idMap } = await unwrap(api.commitChanges())
    const stored = await unwrap(api.getMappingVersion(versionId))
    expect(stored.entries).toMatchObject([{ id: idMap[entry.id] ?? entry.id, sourceKeyValue: '003000', memberKey }])
  })

  /** B11: an entry that breaks a rule is flagged on the pending content, and the commit is refused while it remains. */
  it('flags a broken entry and refuses the commit', async () => {
    const { versionId } = await mappingSet()

    api.beginChanges()
    const entry = await unwrap(api.saveEntry(versionId, { id: newTempId(), sourceKeyValue: 'X1', memberKey: 'NO_SUCH_MEMBER_KEY' }))
    const pending = await unwrap(api.getMappingVersion(versionId))
    expect(pending.violations).toContainEqual(expect.objectContaining({ code: 'entry_member_in_version', target: expect.objectContaining({ itemId: entry.id }) }))

    const refused = await api.commitChanges()
    expect(refused.ok).toBe(false)
    if (!refused.ok) expect(refused.problem.status).toBe(422)
    await api.discardChanges()
    expect((await unwrap(api.getMappingVersion(versionId))).entries).toEqual([])
  })

  /** OI-14: freeze continues in a live version 2; only the frozen version can then be deleted. */
  it('freezes the live version and deletes only the frozen one', async () => {
    const { mappingSetId, versionId } = await mappingSet()

    const { live } = await unwrap(api.freezeMappingVersion(versionId, 'Parity freeze'))
    expect(live).toMatchObject({ versionNumber: 2, status: 'published' })
    const refused = await api.deleteMappingVersion(live!.id)
    expect(refused.ok).toBe(false)
    if (!refused.ok) expect(refused.problem.status).toBe(409)

    await unwrap(api.deleteMappingVersion(versionId))
    expect((await unwrap(api.getMappingSet(mappingSetId))).versions.map((v) => v.id)).toEqual([live!.id])
  })

  /** B12: an unused mapping set may be deleted, after which it is not found. */
  it('deletes an unused mapping set', async () => {
    const [{ ref }] = await liveDimensions(canHaveMappingSets)
    const key = uniqueKey()
    const { mappingSetId } = await unwrap(api.createMappingSet({ key, displayName: key, sourceNamespace: 'parity', dimensionId: ref.id }))

    expect(await api.checkDeleteMappingSet(mappingSetId)).toMatchObject({ allowed: true, reasons: [] })
    await unwrap(api.deleteMappingSet(mappingSetId))

    const gone = await api.getMappingSet(mappingSetId)
    expect(gone.ok).toBe(false)
    if (!gone.ok) expect(gone.problem.status).toBe(404)
  })

  /** OI-27: a created bridge starts with an empty live version 1, many_to_one / none. */
  it('creates a bridge with an empty many-to-one live version 1', async () => {
    const { bridgeId, versionId } = await bridge()

    const detail = await unwrap(api.getBridge(bridgeId))
    expect(detail.owner).toBeNull()
    expect(detail.versions).toMatchObject([{ id: versionId, versionNumber: 1, status: 'published', cardinality: 'many_to_one', allocationMode: 'none', mappingCount: 0 }])
  })

  /**
   * OI-26, B11: settings change in place on the live version; a partition whose weights do not sum to 1 is flagged on its source member
   * and refuses the commit.
   */
  it('edits settings in place and flags a partition that does not sum to 1', async () => {
    const { versionId, sourceKey, targetKey } = await bridge()

    api.beginChanges()
    const version = await unwrap(api.updateBridgeVersion(versionId, { cardinality: 'many_to_many', allocationMode: 'partition' }))
    expect(version).toMatchObject({ id: versionId, cardinality: 'many_to_many', allocationMode: 'partition' })
    await unwrap(api.saveBridgeMapping(versionId, { id: newTempId(), sourceMemberKey: sourceKey, targetMemberKey: targetKey, factor: 1, allocationWeight: 0.5 }))
    const pending = await unwrap(api.getBridgeVersion(versionId))
    expect(pending.allocation.sources).toEqual([{ sourceMemberKey: sourceKey, targetCount: 1, weightSum: 0.5 }])
    expect(pending.violations).toContainEqual(expect.objectContaining({ code: 'allocation_partition', target: expect.objectContaining({ itemKey: sourceKey }) }))

    const refused = await api.commitChanges()
    expect(refused.ok).toBe(false)
    if (!refused.ok) expect(refused.problem.status).toBe(422)
  })

  /** B9: a report bridge is found by its report and source, and refuses edits as owned by its report. */
  it('finds a report bridge and refuses to edit it', async () => {
    const reportBridge = (await api.listBridges()).find((b) => b.owner)!

    const found = await api.findReportBridge(reportBridge.owner!.reportId, reportBridge.owner!.sourceKey)
    expect(found?.id).toBe(reportBridge.id)
    api.beginChanges()
    const refused = await api.updateBridge(reportBridge.id, { displayName: 'Parity' })
    await api.discardChanges()
    expect(refused.ok).toBe(false)
    if (!refused.ok) expect(refused.problem.status).toBe(409)
  })

  /** Delete checks: the live version of a bridge is not deletable, with the `live_version` reason. */
  it('blocks deleting the live bridge version', async () => {
    const { versionId } = await bridge()

    const check = await api.checkDeleteBridgeVersion(versionId)

    expect(check.allowed).toBe(false)
    expect(check.reasons.map((r) => r.code)).toContain('live_version')
  })
})
