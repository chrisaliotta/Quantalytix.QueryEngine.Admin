import { createHttpChangeSet } from '@/lib/api/changes'
import { unwrap } from '@/lib/api/contract'
import type { HttpClient } from '@/lib/api/http'
import type { components } from '@/lib/api/schema'
import type { ApiResult, CalcModel, Id, ModelDocument, ModelOperation, ModelsApi, Problem } from '../contract'

type Schemas = components['schemas']

const modelPath = (id: Id) => `/calculation-models/${id}`


const INPUT_PREFIX = /^(input|patch)\./

/** Form helpers read the same error keys as the mock: `key`, not `input.key`. */
function withoutInputPrefix(problem: Problem): Problem {
  const errors = Object.fromEntries(Object.entries(problem.errors).map(([path, messages]) => [path.replace(INPUT_PREFIX, ''), messages]))
  return { ...problem, errors }
}

const settingsOf = ({ id, key, displayName, currencyCode, calendarKey, formulaLanguage, attributionMethod, isEnabled }: ModelDocument): CalcModel => ({
  id,
  key,
  displayName,
  currencyCode,
  calendarKey,
  formulaLanguage,
  attributionMethod,
  isEnabled,
})

const byKey = <T extends { key: string }>(items: T[], key: string) => items.find((item) => item.key === key)!

/** `http` is a tenant client: paths are relative to `/api/tenants/{tenantId}`. Model edits stage on the model's change set. */
export function createHttpModelsApi(http: HttpClient): ModelsApi {
  const changes = createHttpChangeSet<ModelOperation, ModelDocument>(http)

  async function edit<T>(id: Id, op: ModelOperation, answer: (state: ModelDocument) => T): Promise<ApiResult<T>> {
    const result = await changes.stage(modelPath(id), op satisfies Schemas['ModelOperation'])
    if (!result.ok) return { ok: false, problem: withoutInputPrefix(result.problem) }
    return { ok: true, data: answer(result.data.state) }
  }

  return {
    ...changes,
    listModels: () => unwrap(http.get<Schemas['ModelListItem'][]>('/calculation-models')),
    async getModel(id) {
      const pending = changes.pending()?.state
      if (pending?.id === id) return { ok: true, data: pending }
      return http.get<Schemas['ModelDocument']>(modelPath(id))
    },
    checkFormula: (id, formula, selfKey) =>
      unwrap(http.post<Schemas['ModelFormulaCheck']>(`${modelPath(id)}/formula-check`, { formula, selfKey } satisfies Schemas['ModelFormulaCheckRequest'])),
    checkDeleteModel: (id) => unwrap(http.get<Schemas['DeleteCheck']>(`${modelPath(id)}/delete-check`)),

    updateModel: (id, patch) => edit(id, { op: 'updateModel', patch }, settingsOf),
    saveVariable: (id, { originalKey, ...input }) => edit(id, { op: 'saveVariable', input, originalKey }, (s) => byKey(s.variables, input.key)),
    deleteVariable: (id, key) => edit(id, { op: 'deleteVariable', key }, () => null),
    saveMeasure: (id, { originalKey, ...input }) => edit(id, { op: 'saveMeasure', input, originalKey }, (s) => byKey(s.measures, input.key)),
    deleteMeasure: (id, key) => edit(id, { op: 'deleteMeasure', key }, () => null),
    saveParameter: (id, { originalKey, ...input }) => edit(id, { op: 'saveParameter', input, originalKey }, (s) => byKey(s.parameters, input.key)),
    deleteParameter: (id, key) => edit(id, { op: 'deleteParameter', key }, () => null),
    setAttribution: (id, input) => edit(id, { op: 'setAttribution', input }, (s) => s.attributions.find((a) => a.metricKey === input.metricKey)!),
    clearAttribution: (id, metricKey) => edit(id, { op: 'clearAttribution', metricKey }, () => null),

    createModel: (input) => http.post<Schemas['CalcModel']>('/calculation-models', input satisfies Schemas['CalcModelCreateInput']),
    deleteModel: (id) => http.delete(modelPath(id)),
  }
}
