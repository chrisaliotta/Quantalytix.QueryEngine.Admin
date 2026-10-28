import { createContext, useCallback, useContext, useState, type ReactNode } from 'react'
import useSWR, { useSWRConfig } from 'swr'
import { unwrap } from '@/lib/api/contract'
import { apiMode, tenantHttp } from '@/lib/api/session'
import { DIMENSION_COMMANDS, DIMENSION_EDITS, type DimensionDeleteTarget, type DefinitionOrigin, type DimensionsApi } from './contract'
import { createHttpDimensionsApi } from './http/adapter'
import { createMockDimensionsApi } from './mock/adapter'
import { ChangeScopeProvider, useStagedApi } from '@/components/shell/changes'

const ApiContext = createContext<DimensionsApi | null>(null)

/** Supplies the adapter: an explicit `api`, else the HTTP adapter in http mode, else the in-memory mock. */
export function DimensionsApiProvider({ children, api }: { children: ReactNode; api?: DimensionsApi }) {
  const [instance] = useState(() => api ?? (apiMode === 'http' ? createHttpDimensionsApi(tenantHttp) : createMockDimensionsApi()))
  return <ApiContext.Provider value={instance}>{children}</ApiContext.Provider>
}

export function useDimensionsApi() {
  const api = useContext(ApiContext)
  if (!api) throw new Error('useDimensionsApi must be used inside DimensionsApiProvider')
  return api
}

/**
 * Batches every edit inside one dimension: members, levels, bands, roles and properties stay pending until Save.
 * Children keep calling `useDimensionsApi()`; they receive the staged port.
 */
export function DimensionEditScope({ dimensionId, label, children }: { dimensionId: string; label: string; children: ReactNode }) {
  const base = useDimensionsApi()
  const refresh = useRefreshDimensions()
  const { api, scope } = useStagedApi(base, { id: `dimension:${dimensionId}`, label, edits: DIMENSION_EDITS, commands: DIMENSION_COMMANDS, onSettled: refresh })
  return (
    <ApiContext.Provider value={api}>
      <ChangeScopeProvider scope={scope}>{children}</ChangeScopeProvider>
    </ApiContext.Provider>
  )
}

const OPTS = { revalidateOnFocus: false }

export const dimKeys = {
  list: (origin: DefinitionOrigin) => ['dim', 'list', origin] as const,
  profiles: ['dim', 'profiles'] as const,
  dimension: (id: string) => ['dim', 'dimension', id] as const,
  version: (id: string) => ['dim', 'version', id] as const,
  deleteCheck: (t: DimensionDeleteTarget) => ['dim', 'deleteCheck', JSON.stringify(t)] as const,
}

export function useDimensionList(origin: DefinitionOrigin) {
  const api = useDimensionsApi()
  return useSWR(dimKeys.list(origin), () => api.listDimensions(origin), OPTS)
}
export function useAvailableProfiles() {
  const api = useDimensionsApi()
  return useSWR(dimKeys.profiles, () => api.listAvailableProfiles(), OPTS)
}
export function useDimension(id: string | null) {
  const api = useDimensionsApi()
  return useSWR(id ? dimKeys.dimension(id) : null, () => unwrap(api.getDimension(id!)), { ...OPTS, shouldRetryOnError: false })
}
export function useDimensionVersion(id: string | null) {
  const api = useDimensionsApi()
  return useSWR(id ? dimKeys.version(id) : null, () => unwrap(api.getVersion(id!)), { ...OPTS, keepPreviousData: true })
}
/** Fetches blockers when a delete dialog opens; null target skips the request. */
export function useDeleteCheck(target: DimensionDeleteTarget | null) {
  const api = useDimensionsApi()
  return useSWR(target ? dimKeys.deleteCheck(target) : null, () => api.checkDelete(target!), { ...OPTS, revalidateOnMount: true, dedupingInterval: 0 })
}

/** Revalidates everything a dimension write can change. */
export function useRefreshDimensions() {
  const { mutate } = useSWRConfig()
  return useCallback(() => mutate((key) => Array.isArray(key) && key[0] === 'dim'), [mutate])
}
