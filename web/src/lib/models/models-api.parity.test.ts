import { beforeEach, describe, expect, it, onTestFinished } from 'vitest'
import { unwrap } from '@/lib/api/contract'
import { createLiveHttp, isLiveRun, liveTenant } from '@/lib/api/live-http'
import type { ModelMeasure, ModelsApi, ModelVariable } from './contract'
import { createHttpModelsApi } from './http/adapter'
import { createMockModelsApi } from './mock/adapter'

/**
 * Parity rule (blueprint 00, 02): the mock and HTTP adapters answer the `ModelsApi` port the same way, so the model screens
 * built on the mock keep working against the API. Every test runs on the mock; with `QE_LIVE_API=1` it also runs on the
 * HTTP adapter against a live tenant. Live tests create and delete their own model and never assume an id format.
 */

const adapters = [
  { name: 'mock', create: async () => createMockModelsApi() },
  ...(isLiveRun
    ? [{ name: 'http', create: async () => createHttpModelsApi((await createLiveHttp()).forTenant(liveTenant.tenantId)) }]
    : []),
]

const uniqueKey = () => `parity.m${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`

const variable = (key: string): ModelVariable => ({ key, displayName: key, displayFormat: 'currency', unitKey: null, missingBehavior: 'zero' })

const measure = (key: string, formula: string): ModelMeasure => ({
  key,
  displayName: key,
  formula,
  outputDataType: 'decimal',
  outputPrecision: 28,
  outputScale: 6,
  roundingMode: null,
  unitKey: null,
  displayFormat: 'percent',
})

describe.each(adapters)('ModelsApi port over the $name adapter', ({ create }) => {
  let api: ModelsApi

  beforeEach(async () => {
    api = await create()
  })

  /** A model of the test's own, deleted when the test finishes. */
  async function ownModel() {
    const key = uniqueKey()
    const model = await unwrap(api.createModel({ key, displayName: key, currencyCode: 'USD', formulaLanguage: '1.0', attributionMethod: 'shapley' }))
    onTestFinished(async () => {
      await api.discardChanges()
      await api.deleteModel(model.id)
    })
    return model
  }

  /** Commits variables `INCOME` and `BALANCE` to the model. */
  async function withVariables(id: string) {
    api.beginChanges()
    await unwrap(api.saveVariable(id, variable('INCOME')))
    await unwrap(api.saveVariable(id, variable('BALANCE')))
    await unwrap(api.commitChanges())
  }

  /** A created model is disabled, empty, on the Gregorian calendar, and listed with no readers. */
  it('creates a disabled, empty model', async () => {
    const model = await ownModel()

    expect(model).toMatchObject({ isEnabled: false, calendarKey: 'gregorian' })
    const listed = (await api.listModels()).find((m) => m.id === model.id)
    expect(listed).toMatchObject({ variableCount: 0, measureCount: 0, usedBy: [] })
  })

  /** A key another model has is refused as a conflict (409). */
  it('refuses a taken model key', async () => {
    const model = await ownModel()

    const taken = await api.createModel({ key: model.key, displayName: 'Again', currencyCode: 'USD', formulaLanguage: '1.0', attributionMethod: 'shapley' })

    expect(taken.ok).toBe(false)
    if (!taken.ok) expect(taken.problem.status).toBe(409)
  })

  /** Universal editing rule: staged edits are visible to reads while pending, and a commit stores them; discard drops them. */
  it('stages edits until commit and discards them on request', async () => {
    const model = await ownModel()

    api.beginChanges()
    await unwrap(api.saveVariable(model.id, variable('INCOME')))
    expect((await unwrap(api.getModel(model.id))).variables.map((v) => v.key)).toEqual(['INCOME'])
    await unwrap(api.commitChanges())
    expect(api.hasPendingChanges()).toBe(false)

    api.beginChanges()
    await unwrap(api.saveVariable(model.id, variable('DROPPED')))
    await api.discardChanges()

    expect((await unwrap(api.getModel(model.id))).variables.map((v) => v.key)).toEqual(['INCOME'])
  })

  /** C1: variables and calculated measures share one key namespace; a taken key is invalid input on `key`. */
  it('refuses a metric key the model has', async () => {
    const model = await ownModel()
    await withVariables(model.id)

    api.beginChanges()
    const duplicate = await api.saveMeasure(model.id, measure('INCOME', '1'))

    expect(duplicate.ok).toBe(false)
    if (duplicate.ok) return
    expect(duplicate.problem.status).toBe(400)
    expect(Object.keys(duplicate.problem.errors)).toContain('key')
  })

  /** C5, C9: keys are fixed once saved; an edit that changes one is invalid input on `key`. */
  it('refuses changing a saved key', async () => {
    const model = await ownModel()
    await withVariables(model.id)

    api.beginChanges()
    const renamed = await api.saveVariable(model.id, { ...variable('REVENUE'), originalKey: 'INCOME' })

    expect(renamed.ok).toBe(false)
    if (!renamed.ok) expect(Object.keys(renamed.problem.errors)).toContain('key')
  })

  /** OI-42: an edit that introduces a model-rule violation is refused with 422 and the rule, and the batch stays as it was. */
  it('refuses an edit that introduces a violation', async () => {
    const model = await ownModel()
    await withVariables(model.id)

    api.beginChanges()
    const refused = await api.saveMeasure(model.id, measure('YIELD', '[m.INCOME] / [m.MISSING]'))

    expect(refused.ok).toBe(false)
    if (refused.ok) return
    expect(refused.problem.status).toBe(422)
    expect(refused.problem.violations?.map((v) => v.code)).toContain('formula_references_resolve')
    expect((await unwrap(api.getModel(model.id))).measures).toEqual([])
  })

  /** A calculated measure without an attribution has the engine's default drivers: one per metric read, keyed as the metric. */
  it('computes default drivers for a calculated measure', async () => {
    const model = await ownModel()
    await withVariables(model.id)

    api.beginChanges()
    await unwrap(api.saveMeasure(model.id, measure('YIELD', '[m.INCOME] / [m.BALANCE] * @yearDays / @periodDays')))

    const drivers = (await unwrap(api.getModel(model.id))).effectiveDrivers['YIELD']
    expect(drivers.map((d) => d.key)).toEqual(['INCOME', 'BALANCE', 'days'])
    expect(drivers[2].inputs.slice().sort()).toEqual(['@periodDays', '@yearDays'])
  })

  /** OI-43: a formula check reports an unknown metric over its own text, and the metrics the formula reads. */
  it('checks a formula against the saved model', async () => {
    const model = await ownModel()
    await withVariables(model.id)
    const formula = '[m.INCOME] / [m.RATE]'

    const check = await api.checkFormula(model.id, formula, null)

    expect(check.reads).toEqual(['INCOME', 'RATE'])
    expect(check.requiresLanguage).toBe('1.0')
    expect(check.errors.map((e) => formula.slice(e.start, e.end))).toEqual(['[m.RATE]'])
  })

  /** C8: a model a report reads cannot be deleted; its delete check names each report with `referenced_by_report`. */
  it('blocks deleting a model a report reads', async () => {
    const read = (await api.listModels()).find((m) => m.usedBy.length > 0)!

    const check = await api.checkDeleteModel(read.id)
    const refused = await api.deleteModel(read.id)

    expect(check.allowed).toBe(false)
    expect(check.reasons.map((r) => r.code)).toEqual(read.usedBy.map(() => 'referenced_by_report'))
    expect(refused.ok).toBe(false)
    if (!refused.ok) expect(refused.problem.status).toBe(409)
  })

  /** A model no report reads deletes, after which it is not found. */
  it('deletes a model no report reads', async () => {
    const key = uniqueKey()
    const model = await unwrap(api.createModel({ key, displayName: key, currencyCode: 'USD', formulaLanguage: '1.2', attributionMethod: 'sequential' }))

    expect((await api.checkDeleteModel(model.id)).allowed).toBe(true)
    await unwrap(api.deleteModel(model.id))

    const gone = await api.getModel(model.id)
    expect(gone.ok).toBe(false)
    if (!gone.ok) expect(gone.problem.status).toBe(404)
  })
})
