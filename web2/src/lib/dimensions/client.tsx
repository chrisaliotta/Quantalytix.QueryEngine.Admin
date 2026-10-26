import { createContext, useCallback, useContext, useState, type ReactNode } from 'react'
import useSWR, { useSWRConfig } from 'swr'
import type { ApiResult, DeleteTarget, DimensionsApi, Origin, ProblemDetails } from './contract'
import { createMockDimensionsApi } from './mock/adapter'
import { ChangeScopeProvider, useStagedApi } from '@/components/shell/changes'

const ApiContext = createContext<DimensionsApi | null>(null)

/** Supplies the adapter. Swap `createMockDimensionsApi` for an HTTP adapter without touching any screen. */
export function DimensionsApiProvider({ children, api }: { children: ReactNode; api?: DimensionsApi }) {
  const [instance] = useState(() => api ?? createMockDimensionsApi())
  return <ApiContext.Provider value={instance}>{children}</ApiContext.Provider>
}

export function useDimensionsApi() {
  const api = useContext(ApiContext)
  if (!api) throw new Error('useDimensionsApi must be used inside DimensionsApiProvider')
  return api
}

const DIMENSION_EDITS = [
  'updateDimension',
  'updateVersion',
  'saveMember',
  'deleteMember',
  'moveMember',
  'importMembers',
  'clearMembers',
  'saveLevel',
  'deleteLevel',
  'reorderLevels',
  'saveBands',
  'saveRole',
  'deleteRole',
] as const satisfies readonly (keyof DimensionsApi)[]
const DIMENSION_COMMANDS = ['freezeVersion', 'deleteVersion', 'deleteDimension', 'createDimension', 'defineSystemDimension'] as const satisfies readonly (keyof DimensionsApi)[]

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

export class ProblemError extends Error {
  constructor(public problem: ProblemDetails) {
    super(problem.title)
  }
}

async function unwrap<T>(p: Promise<ApiResult<T>>): Promise<T> {
  const r = await p
  if (!r.ok) throw new ProblemError(r.problem)
  return r.data
}

const OPTS = { revalidateOnFocus: false }

export const dimKeys = {
  list: (origin: Origin) => ['dim', 'list', origin] as const,
  profiles: ['dim', 'profiles'] as const,
  dimension: (id: string) => ['dim', 'dimension', id] as const,
  version: (id: string) => ['dim', 'version', id] as const,
  deleteCheck: (t: DeleteTarget) => ['dim', 'deleteCheck', JSON.stringify(t)] as const,
}

/** Report-owned dimensions; pass `enabled=false` for non-admins so no request is made. */
export function useHiddenDimensions(enabled: boolean) {
  const api = useDimensionsApi()
  return useSWR(enabled ? ['dim', 'hidden'] : null, () => unwrap(api.listHiddenDimensions()), OPTS)
}

export function useDimensionList(origin: Origin) {
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
export function useDeleteCheck(target: DeleteTarget | null) {
  const api = useDimensionsApi()
  return useSWR(target ? dimKeys.deleteCheck(target) : null, () => api.checkDelete(target!), { ...OPTS, revalidateOnMount: true, dedupingInterval: 0 })
}

/** Revalidates everything a dimension write can change. */
export function useRefreshDimensions() {
  const { mutate } = useSWRConfig()
  return useCallback(() => mutate((key) => Array.isArray(key) && key[0] === 'dim'), [mutate])
}
