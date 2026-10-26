import { createContext, useCallback, useContext, useState, type ReactNode } from 'react'
import useSWR, { useSWRConfig } from 'swr'
import { useDimensionsApi } from '@/lib/dimensions/client'
import type { ApiResult, MappingsApi, ProblemDetails } from './contract'
import { createMockMappingsApi } from './mock/adapter'

const ApiContext = createContext<MappingsApi | null>(null)

/** Supplies the adapter; must sit inside DimensionsApiProvider because the mock resolves members through it. */
export function MappingsApiProvider({ children, api }: { children: ReactNode; api?: MappingsApi }) {
  const dims = useDimensionsApi()
  const [instance] = useState(() => api ?? createMockMappingsApi(dims))
  return <ApiContext.Provider value={instance}>{children}</ApiContext.Provider>
}

export function useMappingsApi() {
  const api = useContext(ApiContext)
  if (!api) throw new Error('useMappingsApi must be used inside MappingsApiProvider')
  return api
}

export class MappingProblemError extends Error {
  constructor(public problem: ProblemDetails) {
    super(problem.title)
  }
}

async function unwrap<T>(p: Promise<ApiResult<T>>): Promise<T> {
  const r = await p
  if (!r.ok) throw new MappingProblemError(r.problem)
  return r.data
}

const OPTS = { revalidateOnFocus: false }

export const mapKeys = {
  refs: ['map', 'refs'] as const,
  members: (dimensionId: string) => ['map', 'members', dimensionId] as const,
  sets: (dimensionId: string) => ['map', 'sets', dimensionId] as const,
  set: (id: string) => ['map', 'set', id] as const,
  setVersion: (id: string) => ['map', 'setVersion', id] as const,
  bridges: (dimensionId: string) => ['map', 'bridges', dimensionId] as const,
  bridge: (id: string) => ['map', 'bridge', id] as const,
  bridgeVersion: (id: string) => ['map', 'bridgeVersion', id] as const,
  reportBridge: (reportKey: string, sourceKey: string) => ['map', 'reportBridge', reportKey, sourceKey] as const,
  counts: ['map', 'counts'] as const,
  deleteSet: (id: string) => ['map', 'deleteSet', id] as const,
  deleteBridge: (id: string) => ['map', 'deleteBridge', id] as const,
}

export function useDimensionRefs() {
  const api = useMappingsApi()
  return useSWR(mapKeys.refs, () => api.listDimensionRefs(), OPTS)
}
export function usePublishedMembers(dimensionId: string | null) {
  const api = useMappingsApi()
  return useSWR(dimensionId ? mapKeys.members(dimensionId) : null, () => api.getPublishedMembers(dimensionId!), OPTS)
}
export function useMappingSets(dimensionId: string) {
  const api = useMappingsApi()
  return useSWR(mapKeys.sets(dimensionId), () => api.listMappingSets({ dimensionId }), OPTS)
}
export function useMappingSet(id: string | null) {
  const api = useMappingsApi()
  return useSWR(id ? mapKeys.set(id) : null, () => unwrap(api.getMappingSet(id!)), { ...OPTS, shouldRetryOnError: false })
}
export function useMappingVersion(id: string | null) {
  const api = useMappingsApi()
  return useSWR(id ? mapKeys.setVersion(id) : null, () => unwrap(api.getMappingVersion(id!)), { ...OPTS, keepPreviousData: true })
}
export function useBridges(dimensionId: string) {
  const api = useMappingsApi()
  return useSWR(mapKeys.bridges(dimensionId), () => api.listBridges({ dimensionId }), OPTS)
}
export function useBridge(id: string | null) {
  const api = useMappingsApi()
  return useSWR(id ? mapKeys.bridge(id) : null, () => unwrap(api.getBridge(id!)), { ...OPTS, shouldRetryOnError: false })
}
export function useBridgeVersion(id: string | null) {
  const api = useMappingsApi()
  return useSWR(id ? mapKeys.bridgeVersion(id) : null, () => unwrap(api.getBridgeVersion(id!)), { ...OPTS, keepPreviousData: true })
}
export function useReportBridge(reportKey: string, sourceKey: string) {
  const api = useMappingsApi()
  return useSWR(mapKeys.reportBridge(reportKey, sourceKey), () => api.findReportBridge(reportKey, sourceKey), OPTS)
}
export interface DimensionMappingCounts {
  mappingSets: number
  bridges: number
  reportBridges: number
}

/** Per-dimension totals for the dimension list. Report bridges count against their source dimension, where they are listed. */
export function useMappingCountsByDimension() {
  const api = useMappingsApi()
  return useSWR(
    mapKeys.counts,
    async () => {
      const [sets, bridges] = await Promise.all([api.listMappingSets(), api.listBridges()])
      const counts = new Map<string, DimensionMappingCounts>()
      const entry = (id: string) => {
        let c = counts.get(id)
        if (!c) counts.set(id, (c = { mappingSets: 0, bridges: 0, reportBridges: 0 }))
        return c
      }
      for (const s of sets) entry(s.dimensionId).mappingSets++
      for (const b of bridges) {
        if (b.owner) {
          entry(b.sourceDimensionId).reportBridges++
        } else {
          entry(b.sourceDimensionId).bridges++
          if (b.targetDimensionId !== b.sourceDimensionId) entry(b.targetDimensionId).bridges++
        }
      }
      return counts
    },
    OPTS,
  )
}

export function useMappingSetDeleteCheck(id: string | null) {
  const api = useMappingsApi()
  return useSWR(id ? mapKeys.deleteSet(id) : null, () => api.checkDeleteMappingSet(id!), { ...OPTS, revalidateOnMount: true, dedupingInterval: 0 })
}
export function useBridgeDeleteCheck(id: string | null) {
  const api = useMappingsApi()
  return useSWR(id ? mapKeys.deleteBridge(id) : null, () => api.checkDeleteBridge(id!), { ...OPTS, revalidateOnMount: true, dedupingInterval: 0 })
}

/** Revalidates mapping data, plus dimension data when a dimension change can affect member resolution. */
export function useRefreshMappings() {
  const { mutate } = useSWRConfig()
  return useCallback(() => mutate((key) => Array.isArray(key) && key[0] === 'map'), [mutate])
}
