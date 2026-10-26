import { createContext, useCallback, useContext, useState, type ReactNode } from 'react'
import useSWR, { useSWRConfig } from 'swr'
import type { ApiResult, DatasetsApi, SourceObjectKind, ValidationProblemDetails } from './contract'
import { createMockDatasetsApi } from './mock/adapter'
import { ChangeScopeProvider, useStagedApi } from '@/components/shell/changes'

const ApiContext = createContext<DatasetsApi | null>(null)

/** Supplies the adapter. Swap `createMockDatasetsApi` for an HTTP adapter without touching any screen. */
export function DatasetsApiProvider({ children, api }: { children: ReactNode; api?: DatasetsApi }) {
  const [instance] = useState(() => api ?? createMockDatasetsApi())
  return <ApiContext.Provider value={instance}>{children}</ApiContext.Provider>
}

export function useDatasetsApi() {
  const api = useContext(ApiContext)
  if (!api) throw new Error('useDatasetsApi must be used inside DatasetsApiProvider')
  return api
}

const DATASET_EDITS = [
  'createDataset',
  'renameSourceTable',
  'updateDataset',
  'saveField',
  'deleteField',
  'addFieldsFromColumns',
  'saveGrain',
  'deleteGrain',
  'saveBinding',
  'deleteBinding',
  'saveMeasure',
  'deleteMeasure',
  'saveSettings',
] as const satisfies readonly (keyof DatasetsApi)[]
const DATASET_COMMANDS = [
  'deleteDataset',
  'createDraft',
  'deleteDraft',
  'validateVersion',
  'publishVersion',
  'reopenVersion',
  'retireVersion',
  'materializeField',
  'refreshMaterialization',
  'dematerializeField',
] as const satisfies readonly (keyof DatasetsApi)[]

/**
 * Batches every draft edit inside one dataset until Save. Lifecycle steps and engine jobs (validate, publish,
 * materialize) only run on saved state. Children keep calling `useDatasetsApi()` and receive the staged port.
 */
export function DatasetEditScope({ datasetId, label, children }: { datasetId: string; label: string; children: ReactNode }) {
  const base = useDatasetsApi()
  const refresh = useRefreshDatasets()
  const { api, scope } = useStagedApi(base, { id: `dataset:${datasetId}`, label, edits: DATASET_EDITS, commands: DATASET_COMMANDS, onSettled: refresh })
  return (
    <ApiContext.Provider value={api}>
      <ChangeScopeProvider scope={scope}>{children}</ChangeScopeProvider>
    </ApiContext.Provider>
  )
}

export class ProblemError extends Error {
  constructor(public problem: ValidationProblemDetails) {
    super(problem.title)
  }
}

async function unwrap<T>(p: Promise<ApiResult<T>>): Promise<T> {
  const r = await p
  if (!r.ok) throw new ProblemError(r.problem)
  return r.data
}

const STATIC = { revalidateOnFocus: false, revalidateIfStale: false }

export const keys = {
  vocab: ['ds', 'vocab'] as const,
  catalog: ['ds', 'catalog'] as const,
  sources: ['ds', 'sources'] as const,
  list: ['ds', 'list'] as const,
  dataset: (id: string) => ['ds', 'dataset', id] as const,
  version: (id: string) => ['ds', 'version', id] as const,
  columns: (kind: SourceObjectKind, name: string) => ['ds', 'columns', kind, name] as const,
}

export function useVocabularies() {
  const api = useDatasetsApi()
  return useSWR(keys.vocab, () => api.getVocabularies(), STATIC)
}
export function useCatalog() {
  const api = useDatasetsApi()
  return useSWR(keys.catalog, () => api.getTenantCatalog(), STATIC)
}
export function useSourceObjects() {
  const api = useDatasetsApi()
  return useSWR(keys.sources, () => api.listSourceObjects(), STATIC)
}
export function useSourceColumns(kind: SourceObjectKind | null | undefined, name: string | null | undefined) {
  const api = useDatasetsApi()
  return useSWR(kind && name ? keys.columns(kind, name) : null, () => unwrap(api.getSourceColumns(kind!, name!)), { revalidateOnFocus: false })
}
export function useDatasetList() {
  const api = useDatasetsApi()
  return useSWR(keys.list, () => api.listDatasets(), { revalidateOnFocus: false })
}
export function useDataset(id: string | null) {
  const api = useDatasetsApi()
  return useSWR(id ? keys.dataset(id) : null, () => unwrap(api.getDataset(id!)), { revalidateOnFocus: false })
}
export function useVersion(id: string | null) {
  const api = useDatasetsApi()
  return useSWR(id ? keys.version(id) : null, () => unwrap(api.getVersion(id!)), {
    revalidateOnFocus: false,
    keepPreviousData: true,
    refreshInterval: (v) => (v?.fields.some((f) => f.materialization?.state === 'building') ? 1000 : 0),
  })
}

/** Revalidates everything a write to one dataset can change: the list, the dataset, its versions, source objects and columns. */
export function useRefreshDatasets() {
  const { mutate } = useSWRConfig()
  return useCallback(
    () => mutate((key) => Array.isArray(key) && key[0] === 'ds' && key[1] !== 'vocab' && key[1] !== 'catalog'),
    [mutate],
  )
}
