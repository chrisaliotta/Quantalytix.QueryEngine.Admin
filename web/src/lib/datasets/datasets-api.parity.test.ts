import { beforeEach, describe, expect, it, onTestFinished } from 'vitest'
import { unwrap } from '@/lib/api/contract'
import { createLiveHttp, isLiveRun, liveTenant } from '@/lib/api/live-http'
import type { DatasetFieldInput, DatasetsApi, Id } from './contract'
import { createHttpDatasetsApi } from './http/adapter'
import { createMockDatasetsApi } from './mock/adapter'

/**
 * Parity rule (blueprint 00, 02): the mock and HTTP adapters answer the `DatasetsApi` port the same way, so screens built on the
 * mock keep working against the API. Every test runs on the mock; with `QE_LIVE_API=1` it also runs on the HTTP adapter against a
 * live tenant. Live tests create a dataset over an existing table, delete it when they finish, never publish (a published version
 * cannot be deleted) and never assume an id format.
 */

const adapters = [
  { name: 'mock', create: async () => createMockDatasetsApi() },
  ...(isLiveRun
    ? [{ name: 'http', create: async () => createHttpDatasetsApi((await createLiveHttp()).forTenant(liveTenant.tenantId)) }]
    : []),
]

const uniqueKey = () => `parity.d${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`

/** An expression field over another field of the version. */
const expressionField = (key: string, readsKey: string): DatasetFieldInput => ({
  key,
  displayName: key,
  fieldSourceKind: 'expression',
  sourceColumn: null,
  jsonPath: null,
  expressionText: `[f.${readsKey}]`,
  dataType: 'decimal',
  numericPrecision: 18,
  numericScale: 2,
  isNullable: true,
  unitKey: null,
  isSelectable: true,
  isFilterable: true,
  isSortable: true,
  isGroupable: false,
  isEnabled: true,
})

describe.each(adapters)('DatasetsApi port over the $name adapter', ({ create }) => {
  let api: DatasetsApi

  beforeEach(async () => {
    api = await create()
  })

  /** A dataset over the tenant's first table, deleted when the test finishes, with its draft version 1. */
  async function draftDataset() {
    const table = (await api.listSourceObjects()).find((s) => s.kind === 'table')!
    const key = uniqueKey()
    const dataset = await unwrap(api.createDataset({ mode: 'existing', key, displayName: key, sourceObjectKind: 'table', sourceObject: table.name }))
    onTestFinished(async () => {
      await api.discardChanges()
      await api.deleteDataset(dataset.id)
    })
    return { dataset, versionId: dataset.versions[0].id }
  }

  /** The key of a field draft version 1 made from a column. */
  async function columnFieldKey(versionId: Id) {
    return (await unwrap(api.getVersion(versionId))).fields[0].key
  }

  /** A dataset over an existing table starts with draft version 1, whose fields read the table's readable columns. */
  it('creates a dataset with a draft version 1 over an existing table', async () => {
    const { dataset, versionId } = await draftDataset()

    expect(dataset.versions).toMatchObject([{ id: versionId, versionNumber: 1, status: 'draft' }])
    const version = await unwrap(api.getVersion(versionId))
    expect(version.fields.length).toBeGreaterThan(0)
    expect(version.fields.every((f) => f.fieldSourceKind === 'column')).toBe(true)
    expect((await api.listDatasets()).map((d) => d.id)).toContain(dataset.id)
  })

  /** Dataset keys are fixed and unique (D4, OI-36): a second dataset with the same key is refused. */
  it('refuses a taken dataset key', async () => {
    const { dataset } = await draftDataset()
    const table = (await api.listSourceObjects()).find((s) => s.kind === 'table')!

    const taken = await api.createDataset({ mode: 'existing', key: dataset.key, displayName: 'Again', sourceObjectKind: 'table', sourceObject: table.name })

    expect(taken.ok).toBe(false)
  })

  /**
   * Universal editing rule: staged edits are visible to reads while pending, a later edit can reference an item created earlier in
   * the batch, and the commit's `idMap` turns every temporary id into the stored id.
   */
  it('stages a field and a grain over it until commit and maps temporary ids', async () => {
    const { versionId } = await draftDataset()
    const readsKey = await columnFieldKey(versionId)

    api.beginChanges()
    const field = await unwrap(api.saveField(versionId, expressionField('parityShare', readsKey)))
    const grain = await unwrap(api.saveGrain(versionId, { key: 'parityRow', grainKind: 'row', fieldIds: [field.id] }))
    expect(grain.fieldIds).toEqual([field.id])
    expect((await unwrap(api.getVersion(versionId))).fields.map((f) => f.key)).toContain('parityShare')

    const { idMap } = await unwrap(api.commitChanges())
    const storedId = (id: Id) => idMap[id] ?? id
    const stored = await unwrap(api.getVersion(versionId))
    const storedField = stored.fields.find((f) => f.key === 'parityShare')!
    expect(storedField.id).toBe(storedId(field.id))
    expect(stored.grains.find((g) => g.key === 'parityRow')!.fieldIds).toEqual([storedId(field.id)])
  })

  /** An identity edit is staged with the version's content: reads show the pending display name until the commit stores it. */
  it('stages an identity edit until commit', async () => {
    const { dataset, versionId } = await draftDataset()

    api.beginChanges()
    const pending = await unwrap(api.updateDataset(dataset.id, { displayName: 'Parity renamed' }))
    expect(pending.displayName).toBe('Parity renamed')
    expect((await unwrap(api.getVersion(versionId))).dataset.displayName).toBe('Parity renamed')
    expect((await unwrap(api.getDataset(dataset.id))).displayName).toBe('Parity renamed')

    await unwrap(api.commitChanges())
    expect((await unwrap(api.getDataset(dataset.id))).displayName).toBe('Parity renamed')
  })

  /** Discard drops every pending edit; nothing reaches the stored version. */
  it('discards pending edits', async () => {
    const { versionId } = await draftDataset()
    const readsKey = await columnFieldKey(versionId)

    api.beginChanges()
    await unwrap(api.saveField(versionId, expressionField('dropped', readsKey)))
    await api.discardChanges()

    expect(api.hasPendingChanges()).toBe(false)
    expect((await unwrap(api.getVersion(versionId))).fields.map((f) => f.key)).not.toContain('dropped')
  })

  /** Field keys are unique per version (D4): a duplicate is refused as invalid input on `key`. */
  it('refuses a duplicate field key', async () => {
    const { versionId } = await draftDataset()
    const readsKey = await columnFieldKey(versionId)

    api.beginChanges()
    const duplicate = await api.saveField(versionId, expressionField(readsKey, readsKey))

    expect(duplicate.ok).toBe(false)
    if (duplicate.ok) return
    expect(duplicate.problem.status).toBe(400)
    expect(Object.keys(duplicate.problem.errors)).toContain('key')
  })

  /** A field a grain reads by id is not deleted, and the version's dependents name the grain (OI-35). */
  it('refuses deleting a field that a grain reads', async () => {
    const { versionId } = await draftDataset()
    const field = (await unwrap(api.getVersion(versionId))).fields[0]

    api.beginChanges()
    const grain = await unwrap(api.saveGrain(versionId, { key: 'parityRow', grainKind: 'row', fieldIds: [field.id] }))
    const refused = await api.deleteField(versionId, field.id)

    expect(refused.ok).toBe(false)
    if (!refused.ok) expect(refused.problem.status).toBe(409)
    const dependents = (await unwrap(api.getVersion(versionId))).dependents[field.id]
    expect(dependents).toContainEqual(expect.objectContaining({ tab: 'grains', itemId: grain.id, blocksDelete: true }))
  })

  /** A dataset has at most one open version (OI-23): another draft while version 1 is a draft is a conflict. */
  it('refuses a second open version', async () => {
    const { dataset, versionId } = await draftDataset()

    const second = await api.createDraft(dataset.id, { mode: 'clone', fromVersionId: versionId })

    expect(second.ok).toBe(false)
    if (!second.ok) expect(second.problem.status).toBe(409)
  })

  /** A draft that no release pins can be deleted (OI-17); its delete check says so. */
  it('allows deleting an unpinned draft', async () => {
    const { versionId } = await draftDataset()

    const check = await api.checkDeleteVersion(versionId)

    expect(check).toMatchObject({ allowed: true, reasons: [] })
  })

  /**
   * The engine reads a dataset only through the version the active release pins (D2), so retiring that version is refused with 422
   * `release_pins_retired_version` and nothing changes. Live, this needs a tenant whose active release pins a dataset version.
   */
  it('refuses retiring the version the active release pins', async () => {
    const pinned = (await api.listDatasets()).find((d) => d.activeVersionId)!

    const refused = await api.retireVersion(pinned.activeVersionId!)

    expect(refused.ok).toBe(false)
    if (refused.ok) return
    expect(refused.problem.status).toBe(422)
    expect(refused.problem.violations?.map((v) => v.code)).toEqual(['release_pins_retired_version'])
    const after = await unwrap(api.getVersion(pinned.activeVersionId!))
    expect(after.status).toBe('published')
  })

  /** A dataset whose versions are all drafts deletes, after which it is not found. */
  it('deletes a dataset of drafts', async () => {
    const table = (await api.listSourceObjects()).find((s) => s.kind === 'table')!
    const key = uniqueKey()
    const dataset = await unwrap(api.createDataset({ mode: 'existing', key, displayName: key, sourceObjectKind: 'table', sourceObject: table.name }))

    await unwrap(api.deleteDataset(dataset.id))

    const gone = await api.getDataset(dataset.id)
    expect(gone.ok).toBe(false)
    if (!gone.ok) expect(gone.problem.status).toBe(404)
  })

  /**
   * A managed table exists only for the datasets over it (OI-33): deleting the only dataset that reads a table it created drops the
   * table, which then leaves the source object list. Live, this creates and drops a real table.
   */
  it('drops the managed table only the deleted dataset reads', async () => {
    const key = uniqueKey()
    const tableName = key.replace('.', '_')
    const dataset = await unwrap(
      api.createDataset({ mode: 'new', key, displayName: key, tableName, columns: [{ name: 'amount', dataType: 'decimal', numericPrecision: 18, numericScale: 2, isNullable: true }] }),
    )
    expect((await api.listSourceObjects()).find((s) => s.name === tableName)).toMatchObject({ kind: 'table', isManaged: true })

    await unwrap(api.deleteDataset(dataset.id))

    expect((await api.listSourceObjects()).map((s) => s.name)).not.toContain(tableName)
  })
})
