import { unwrap, type ChangeSetCommit } from '@/lib/api/contract'
import { apiMode, tenantHttp } from '@/lib/api/session'
import type { Id, ModelDocument, ModelOperation } from '@/lib/models/contract'
import { sharedModelsApi } from '@/lib/models/client'
import { initialModels, reportRefs } from './data'
import { modelFromDocument, modelOperations, reportRefsOf, violationsOf } from './http-bridge'
import type { CalcModel, ReportRef, RuleViolation } from './types'

/** Where the models screen keeps its models: in memory (mock mode) or the Admin API (http mode). */
export interface ModelPersistence {
  models: CalcModel[]
  reports: ReportRef[]
  /** Stores `next` over the saved `prev`; resolves with the refusal, if any. */
  save(prev: CalcModel, next: CalcModel): Promise<RuleViolation[] | null>
  /** Creates a model; resolves with the stored model or the refusal. */
  create(model: CalcModel): Promise<{ ok: true; model: CalcModel } | { ok: false; violations: RuleViolation[] }>
  remove(key: string): Promise<RuleViolation[] | null>
}

const memory: ModelPersistence = {
  models: initialModels,
  reports: reportRefs,
  save: async () => null,
  create: async (model) => ({ ok: true, model }),
  remove: async () => null,
}

/** The stored document of each model, by key: its id, and the values the editor cannot show. */
const stored = new Map<string, ModelDocument>()
let loaded: Pick<ModelPersistence, 'models' | 'reports'> = { models: [], reports: [] }

/** Reads every model before the app renders (http mode), so the provider starts from them like it starts from the mock data. */
export async function loadModels() {
  const api = sharedModelsApi()
  const list = await api.listModels()
  const docs = await Promise.all(list.map((m) => unwrap(api.getModel(m.id))))
  for (const doc of docs) stored.set(doc.key, doc)
  loaded = { models: docs.map(modelFromDocument), reports: reportRefsOf(list) }
}

/** The id of a model by key, for the report store's translation. */
export function modelIdOf(key: string): Id | undefined {
  return stored.get(key)?.id
}

/** The key of a model by id. */
export function modelKeyOf(id: Id): string | undefined {
  return [...stored.values()].find((doc) => doc.id === id)?.key
}

const http: Omit<ModelPersistence, 'models' | 'reports'> = {
  async save(prev, next) {
    const doc = stored.get(prev.key)!
    const operations = modelOperations(doc, prev, next)
    if (!operations.length) return null
    const result = await tenantHttp.post<ChangeSetCommit<ModelDocument>>(`/calculation-models/${doc.id}/changes`, { operations } satisfies { operations: ModelOperation[] })
    if (!result.ok) return violationsOf(result.problem)
    stored.set(next.key, result.data.state)
    return null
  },
  async create(model) {
    const api = sharedModelsApi()
    const created = await api.createModel({
      key: model.key,
      displayName: model.name,
      currencyCode: model.currency,
      formulaLanguage: model.language,
      attributionMethod: model.method,
    })
    if (!created.ok) return { ok: false, violations: violationsOf(created.problem) }
    const doc = await unwrap(api.getModel(created.data.id))
    stored.set(doc.key, doc)
    return { ok: true, model: modelFromDocument(doc) }
  },
  async remove(key) {
    const result = await sharedModelsApi().deleteModel(stored.get(key)!.id)
    if (!result.ok) return violationsOf(result.problem)
    stored.delete(key)
    return null
  },
}

export function modelPersistence(): ModelPersistence {
  return apiMode === 'http' ? { ...loaded, ...http } : memory
}
