import { createContext, useCallback, useContext, useState, type ReactNode } from 'react'
import useSWR, { useSWRConfig } from 'swr'
import { unwrap } from '@/lib/api/contract'
import { apiMode, tenantHttp } from '@/lib/api/session'
import { DATASET_COMMANDS, DATASET_EDITS, type DatasetsApi, type DatasetSourceObjectKind } from './contract'
import { createHttpDatasetsApi } from './http/adapter'
import type { TenantLookups } from './lookups'
import { createMockDatasetsApi } from './mock/adapter'
import { createMockTenantLookups } from './mock/lookups'
import { ChangeScopeProvider, useStagedApi } from '@/components/shell/changes'

const ApiContext = createContext<DatasetsApi | null>(null)
const LookupsContext = createContext<TenantLookups | null>(null)

/**
 * Supplies the adapters: an explicit `api`, else the HTTP adapter in http mode, else the in-memory mock. The tenant lookups stay
 * on the mock until the tenant module serves them.
 */
export function DatasetsApiProvider({ children, api, lookups }: { children: ReactNode; api?: DatasetsApi; lookups?: TenantLookups }) {
  const [instance] = useState(() => api ?? (apiMode === 'http' ? createHttpDatasetsApi(tenantHttp) : createMockDatasetsApi()))
  const [lookupsInstance] = useState(() => lookups ?? createMockTenantLookups())
  return (
    <LookupsContext.Provider value={lookupsInstance}>
      <ApiContext.Provider value={instance}>{children}</ApiContext.Provider>
    </LookupsContext.Provider>
  )
}

export function useDatasetsApi() {
  const api = useContext(ApiContext)
  if (!api) throw new Error('useDatasetsApi must be used inside DatasetsApiProvider')
  return api
}

function useLookups() {
  const lookups = useContext(LookupsContext)
  if (!lookups) throw new Error('useLookups must be used inside DatasetsApiProvider')
  return lookups
}

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

const STATIC = { revalidateOnFocus: false, revalidateIfStale: false }

export const keys = {
  vocab: ['ds', 'vocab'] as const,
  catalog: ['ds', 'catalog'] as const,
  members: (dimensionId: string) => ['ds', 'members', dimensionId] as const,
  sources: ['ds', 'sources'] as const,
  list: ['ds', 'list'] as const,
  dataset: (id: string) => ['ds', 'dataset', id] as const,
  version: (id: string) => ['ds', 'version', id] as const,
  columns: (kind: DatasetSourceObjectKind, name: string) => ['ds', 'columns', kind, name] as const,
}

const LOOKUP_KEYS = new Set<string>([keys.vocab[1], keys.catalog[1], 'members'])

export function useVocabularies() {
  const lookups = useLookups()
  return useSWR(keys.vocab, () => lookups.getVocabularies(), STATIC)
}
export function useCatalog() {
  const lookups = useLookups()
  return useSWR(keys.catalog, () => lookups.getCatalog(), STATIC)
}
export function usePublishedMembers(dimensionId: string | null | undefined) {
  const lookups = useLookups()
  return useSWR(dimensionId ? keys.members(dimensionId) : null, () => lookups.getPublishedMembers(dimensionId!), STATIC)
}
export function useSourceObjects() {
  const api = useDatasetsApi()
  return useSWR(keys.sources, () => api.listSourceObjects(), STATIC)
}
export function useSourceColumns(kind: DatasetSourceObjectKind | null | undefined, name: string | null | undefined) {
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
  return useCallback(() => mutate((key) => Array.isArray(key) && key[0] === 'ds' && !LOOKUP_KEYS.has(key[1])), [mutate])
}
