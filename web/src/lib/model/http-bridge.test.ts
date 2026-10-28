import { describe, expect, it } from 'vitest'
import { unwrap, type Id } from '@/lib/api/contract'
import type { ModelOperation, ModelsApi } from '@/lib/models/contract'
import { createMockModelsApi } from '@/lib/models/mock/adapter'
import { modelFromDocument, modelOperations } from './http-bridge'
import type { CalcModel } from './types'

/**
 * In http mode the models screen loads a `ModelDocument` into the prototype's `CalcModel` and saves a model's draft as one change
 * set (persistence.ts). These tests run that translation against the ModelsApi mock adapter: a model saved without edits writes
 * nothing, and the operations of an edit, applied by the port, store what the screen shows.
 */

function apply(api: ModelsApi, id: Id, op: ModelOperation) {
  switch (op.op) {
    case 'updateModel':
      return api.updateModel(id, op.patch)
    case 'saveVariable':
      return api.saveVariable(id, { ...op.input, originalKey: op.originalKey })
    case 'deleteVariable':
      return api.deleteVariable(id, op.key)
    case 'saveMeasure':
      return api.saveMeasure(id, { ...op.input, originalKey: op.originalKey })
    case 'deleteMeasure':
      return api.deleteMeasure(id, op.key)
    case 'saveParameter':
      return api.saveParameter(id, { ...op.input, originalKey: op.originalKey })
    case 'deleteParameter':
      return api.deleteParameter(id, op.key)
    case 'setAttribution':
      return api.setAttribution(id, op.input)
    case 'clearAttribution':
      return api.clearAttribution(id, op.metricKey)
  }
}

describe('model http bridge', () => {
  it('writes nothing for a model saved without edits', async () => {
    const api = createMockModelsApi()
    for (const item of await api.listModels()) {
      const doc = await unwrap(api.getModel(item.id))
      const model = modelFromDocument(doc)
      expect(modelOperations(doc, model, structuredClone(model))).toEqual([])
    }
  })

  it('stores the settings, variables, measures and attributions the screen shows after an edit', async () => {
    const api = createMockModelsApi()
    const { id } = (await api.listModels()).find((m) => m.key === 'bank.margin_model')!
    const doc = await unwrap(api.getModel(id))
    const saved = modelFromDocument(doc)
    const edited: CalcModel = structuredClone(saved)
    edited.name = 'Bank margin (edited)'
    edited.variables.find((v) => v.key === 'INTEREST_EXPENSE')!.name = 'Interest paid'
    edited.measures.push({ key: 'SPREAD', name: 'Spread', formula: '[m.YIELD] - [m.COST_OF_FUNDS]', precision: 28, scale: 6, rounding: 'half_even', unit: '', format: 'percent' })
    edited.measures.find((m) => m.key === 'SPREAD')!.attribution = {
      useDefault: false,
      drivers: [
        { key: 'asset', name: 'Asset yield', inputs: ['YIELD'] },
        { key: 'funding', name: 'Funding cost', inputs: ['COST_OF_FUNDS'] },
      ],
    }

    api.beginChanges()
    for (const op of modelOperations(doc, saved, edited)) expect((await apply(api, id, op)).ok).toBe(true)
    const reloaded = modelFromDocument(await unwrap(api.getModel(id)))

    expect(reloaded).toEqual(edited)
  })
})
