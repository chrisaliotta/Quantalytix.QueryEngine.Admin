import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react'
import useSWR, { useSWRConfig } from 'swr'
import { unwrap } from '@/lib/api/contract'
import { apiMode, tenantHttp } from '@/lib/api/session'
import { MODEL_COMMANDS, MODEL_EDITS, type Id, type ModelsApi } from './contract'
import { createHttpModelsApi } from './http/adapter'
import { createMockModelsApi } from './mock/adapter'
import { ChangeScopeProvider, useStagedApi } from '@/components/shell/changes'

let shared: ModelsApi | null = null

/** The page's one adapter, the HTTP adapter in http mode or else the in-memory mock, so every reader sees the same models. */
export function sharedModelsApi(): ModelsApi {
  shared ??= apiMode === 'http' ? createHttpModelsApi(tenantHttp) : createMockModelsApi()
  return shared
}

const ApiContext = createContext<ModelsApi | null>(null)

/** Supplies the adapter: an explicit `api`, else the shared one. */
export function ModelsApiProvider({ children, api }: { children: ReactNode; api?: ModelsApi }) {
  const [instance] = useState(() => api ?? sharedModelsApi())
  return <ApiContext.Provider value={instance}>{children}</ApiContext.Provider>
}

export function useModelsApi() {
  const api = useContext(ApiContext)
  if (!api) throw new Error('useModelsApi must be used inside ModelsApiProvider')
  return api
}

/**
 * Batches every edit inside one model: settings, variables, measures, parameters and attributions stay pending until Save.
 * Children keep calling `useModelsApi()`; they receive the staged port.
 */
export function ModelEditScope({ modelId, label, children }: { modelId: Id; label: string; children: ReactNode }) {
  const base = useModelsApi()
  const refresh = useRefreshModels()
  const { api, scope } = useStagedApi(base, { id: `model:${modelId}`, label, edits: MODEL_EDITS, commands: MODEL_COMMANDS, onSettled: refresh })
  return (
    <ApiContext.Provider value={api}>
      <ChangeScopeProvider scope={scope}>{children}</ChangeScopeProvider>
    </ApiContext.Provider>
  )
}

const OPTS = { revalidateOnFocus: false }

export const modelKeys = {
  list: ['models', 'list'] as const,
  model: (id: Id) => ['models', 'model', id] as const,
  deleteCheck: (id: Id) => ['models', 'deleteCheck', id] as const,
  formula: (id: Id, formula: string, selfKey: string | null) => ['models', 'formula', id, formula, selfKey] as const,
}

export function useModelList() {
  const api = useModelsApi()
  return useSWR(modelKeys.list, () => api.listModels(), OPTS)
}

export function useModelDocument(id: Id | null) {
  const api = useModelsApi()
  return useSWR(id ? modelKeys.model(id) : null, () => unwrap(api.getModel(id!)), { ...OPTS, keepPreviousData: true })
}

/** Fetches blockers when a delete dialog opens; a null id skips the request. */
export function useModelDeleteCheck(id: Id | null) {
  const api = useModelsApi()
  return useSWR(id ? modelKeys.deleteCheck(id) : null, () => api.checkDeleteModel(id!), { ...OPTS, revalidateOnMount: true, dedupingInterval: 0 })
}

/** The server's check of a formula against the saved model (OI-43), once typing pauses; an empty formula is not checked. */
export function useFormulaCheck(id: Id, formula: string, selfKey: string | null) {
  const api = useModelsApi()
  const [settled, setSettled] = useState(formula)
  useEffect(() => {
    const timer = setTimeout(() => setSettled(formula), 250)
    return () => clearTimeout(timer)
  }, [formula])
  return useSWR(settled.trim() ? modelKeys.formula(id, settled, selfKey) : null, () => api.checkFormula(id, settled, selfKey), { ...OPTS, keepPreviousData: true })
}

/** Revalidates everything a model write can change, including the report editor's compatibility view. */
export function useRefreshModels() {
  const { mutate } = useSWRConfig()
  return useCallback(() => mutate((key) => Array.isArray(key) && key[0] === 'models'), [mutate])
}
