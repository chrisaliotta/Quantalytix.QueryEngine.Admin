import { beforeEach, describe, expect, it, onTestFinished } from 'vitest'
import { unwrap } from '@/lib/api/contract'
import { createLiveHttp, isLiveRun, liveTenant } from '@/lib/api/live-http'
import type { DatasetFieldInput, DatasetsApi, Id } from './contract'
import { createHttpDatasetsApi } from './http/adapter'
import { createMockDatasetsApi } from './mock/adapter'

/**
 * Parity rule (blueprint 00, 02): the mock and HTTP adapters answer the `DatasetsApi` port the same way, so screens built on the
 * mock keep working against the API. Every test runs on the mock; with `QE_LIVE_API=1` it also runs on the HTTP adapter against a
 * live tenant. Live tests create a dataset over an existing table, delete it when they finish, never capture it into a snapshot
 * (a dataset a frozen snapshot holds cannot be deleted) and never assume an id format.
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

  /** A dataset over the tenant's first table, deleted when the test finishes, with its live version. */
  async function liveDataset() {
    const table = (await api.listSourceObjects()).find((s) => s.kind === 'table')!
    const key = uniqueKey()
    const dataset = await unwrap(api.createDataset({ mode: 'existing', key, displayName: key, sourceObjectKind: 'table', sourceObject: table.name }))
    onTestFinished(async () => {
      await api.discardChanges()
      await api.deleteDataset(dataset.id)
    })
    return { dataset, versionId: dataset.liveVersionId }
  }

  /** The key of a field the live version made from a column. */
  async function columnFieldKey(versionId: Id) {
    return (await unwrap(api.getVersion(versionId))).fields[0].key
  }

  /** A dataset over an existing table starts with only its live version, whose fields read the table's readable columns. */
  it('creates a dataset with its live version over an existing table', async () => {
    const { dataset, versionId } = await liveDataset()

    expect(dataset.versions).toMatchObject([{ id: versionId, isLive: true }])
    const version = await unwrap(api.getVersion(versionId))
    expect(version.fields.length).toBeGreaterThan(0)
    expect(version.fields.every((f) => f.fieldSourceKind === 'column')).toBe(true)
    expect((await api.listDatasets()).map((d) => d.id)).toContain(dataset.id)
  })

  /** Dataset keys are fixed and unique (D4, OI-36): a second dataset with the same key is refused. */
  it('refuses a taken dataset key', async () => {
    const { dataset } = await liveDataset()
    const table = (await api.listSourceObjects()).find((s) => s.kind === 'table')!

    const taken = await api.createDataset({ mode: 'existing', key: dataset.key, displayName: 'Again', sourceObjectKind: 'table', sourceObject: table.name })

    expect(taken.ok).toBe(false)
  })

  /**
   * Universal editing rule: staged edits are visible to reads while pending, a later edit can reference an item created earlier in
   * the batch, and the commit's `idMap` turns every temporary id into the stored id.
   */
  it('stages a field and a grain over it until commit and maps temporary ids', async () => {
    const { versionId } = await liveDataset()
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
    const { dataset, versionId } = await liveDataset()

    api.beginChanges()
    const pending = await unwrap(api.updateDataset(dataset.id, { displayName: 'Parity renamed' }))
    expect(pending.displayName).toBe('Parity renamed')
    expect((await unwrap(api.getVersion(versionId))).dataset.displayName).toBe('Parity renamed')
    expect((await unwrap(api.getDataset(dataset.id))).displayName).toBe('Parity renamed')

    await unwrap(api.commitChanges())
    expect((await unwrap(api.getDataset(dataset.id))).displayName).toBe('Parity renamed')
  })

  /** Floating-point fields carry no precision or scale: a double field is stored as sent, with both null. */
  it('stores a double field without a precision or scale', async () => {
    const { versionId } = await liveDataset()
    const readsKey = await columnFieldKey(versionId)

    api.beginChanges()
    await unwrap(api.saveField(versionId, { ...expressionField('parityRate', readsKey), dataType: 'double', numericPrecision: null, numericScale: null }))
    await unwrap(api.commitChanges())

    const stored = (await unwrap(api.getVersion(versionId))).fields.find((f) => f.key === 'parityRate')
    expect(stored).toMatchObject({ dataType: 'double', numericPrecision: null, numericScale: null })
  })

  /** Discard drops every pending edit; nothing reaches the stored version. */
  it('discards pending edits', async () => {
    const { versionId } = await liveDataset()
    const readsKey = await columnFieldKey(versionId)

    api.beginChanges()
    await unwrap(api.saveField(versionId, expressionField('dropped', readsKey)))
    await api.discardChanges()

    expect(api.hasPendingChanges()).toBe(false)
    expect((await unwrap(api.getVersion(versionId))).fields.map((f) => f.key)).not.toContain('dropped')
  })

  /** Field keys are unique per version (D4): a duplicate is refused as invalid input on `key`. */
  it('refuses a duplicate field key', async () => {
    const { versionId } = await liveDataset()
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
    const { versionId } = await liveDataset()
    const field = (await unwrap(api.getVersion(versionId))).fields[0]

    api.beginChanges()
    const grain = await unwrap(api.saveGrain(versionId, { key: 'parityRow', grainKind: 'row', fieldIds: [field.id] }))
    const refused = await api.deleteField(versionId, field.id)

    expect(refused.ok).toBe(false)
    if (!refused.ok) expect(refused.problem.status).toBe(409)
    const dependents = (await unwrap(api.getVersion(versionId))).dependents[field.id]
    expect(dependents).toContainEqual(expect.objectContaining({ tab: 'grains', itemId: grain.id, blocksDelete: true }))
  })

  /** Live is valid after every commit: a staged edit that breaks a commit rule is reported by the preview and refuses the commit. */
  it('refuses committing an edit that breaks a commit rule', async () => {
    const { versionId } = await liveDataset()

    api.beginChanges()
    await unwrap(api.saveField(versionId, expressionField('parityBroken', 'parityMissing')))

    expect(api.pendingViolations().map((v) => v.code)).toContain('expression_field_references')
    const refused = await api.commitChanges()
    expect(refused.ok ? 200 : refused.problem.status).toBe(422)
  })

  /** A frozen snapshot's version never changes: an edit staged on it is refused with 409. Live, this needs a captured dataset. */
  it('refuses editing a frozen version', async () => {
    const frozen = (await api.listDatasets()).flatMap((d) => d.versions).find((v) => !v.isLive)!
    const readsKey = await columnFieldKey(frozen.id)

    api.beginChanges()
    const refused = await api.saveField(frozen.id, expressionField('parityFrozen', readsKey))
    await api.discardChanges()

    expect(refused.ok ? 200 : refused.problem.status).toBe(409)
  })

  /** D10: a dataset a frozen snapshot holds a version of cannot be deleted; its delete check names the snapshot. */
  it('refuses deleting a dataset a frozen snapshot holds', async () => {
    const held = (await api.listDatasets()).find((d) => d.versions.some((v) => !v.isLive))!

    const check = await api.checkDeleteDataset(held.id)

    expect(check.allowed).toBe(false)
    expect(check.reasons.map((r) => r.code)).toContain('referenced_by_snapshot')
  })

  /** A dataset only live holds deletes, after which it is not found. */
  it('deletes a dataset only live holds', async () => {
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
