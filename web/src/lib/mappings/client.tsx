import { createContext, useCallback, useContext, useState, type ReactNode } from 'react'
import useSWR, { useSWRConfig } from 'swr'
import { ChangeScopeProvider, useStagedApi } from '@/components/shell/changes'
import { unwrap } from '@/lib/api/contract'
import { apiMode, tenantHttp } from '@/lib/api/session'
import { tenantApi } from '@/lib/tenant/client'
import {
  BRIDGE_EDITS,
  MAPPING_SET_EDITS,
  MAPPINGS_COMMANDS,
  type ApiResult,
  type BridgeVersionSummary,
  type FreezeResult,
  type Id,
  type MappingsApi,
  type MappingsDirectory,
  type MappingVersionSummary,
  type ValidationReport,
} from './contract'
import { createTenantDirectory } from './directory'
import { createHttpMappingsApi } from './http/adapter'
import { createMockMappingsApi } from './mock/adapter'
import { createMockMappingLookups } from './mock/lookups'

const ApiContext = createContext<MappingsApi | null>(null)
const DirectoryContext = createContext<MappingsDirectory | null>(null)

/**
 * Supplies the adapter and the directory: an explicit `api`, else the HTTP adapter in http mode, else the in-memory mock. The
 * directory reads the tenant catalog and published members from `TenantApi` in http mode; the mock keeps its own lookups, whose
 * ids match the mock.
 */
export function MappingsApiProvider({ children, api }: { children: ReactNode; api?: MappingsApi }) {
  const [services] = useState(() => {
    const directory = createTenantDirectory(apiMode === 'http' ? tenantApi : createMockMappingLookups())
    return { directory, api: api ?? (apiMode === 'http' ? createHttpMappingsApi(tenantHttp) : createMockMappingsApi(directory)) }
  })
  return (
    <DirectoryContext.Provider value={services.directory}>
      <ApiContext.Provider value={services.api}>{children}</ApiContext.Provider>
    </DirectoryContext.Provider>
  )
}

export function useMappingsApi() {
  const api = useContext(ApiContext)
  if (!api) throw new Error('useMappingsApi must be used inside MappingsApiProvider')
  return api
}

function useDirectory() {
  return useContext(DirectoryContext)!
}

function MappingsEditScope({ id, label, edits, children }: { id: string; label: string; edits: readonly (keyof MappingsApi)[]; children: ReactNode }) {
  const base = useMappingsApi()
  const refresh = useRefreshMappings()
  const { api, scope } = useStagedApi(base, { id, label, edits, commands: MAPPINGS_COMMANDS, onSettled: refresh })
  return (
    <ApiContext.Provider value={api}>
      <ChangeScopeProvider scope={scope}>{children}</ChangeScopeProvider>
    </ApiContext.Provider>
  )
}

/** Batches every edit of one mapping set (identity and live entries) until Save. Children receive the staged port. */
export function MappingSetEditScope({ mappingSetId, label, children }: { mappingSetId: Id; label: string; children: ReactNode }) {
  return (
    <MappingsEditScope id={`mapping-set:${mappingSetId}`} label={label} edits={MAPPING_SET_EDITS}>
      {children}
    </MappingsEditScope>
  )
}

/** Batches every edit of one analytical bridge (identity, live settings and mappings) until Save. */
export function BridgeEditScope({ bridgeId, label, children }: { bridgeId: Id; label: string; children: ReactNode }) {
  return (
    <MappingsEditScope id={`bridge:${bridgeId}`} label={label} edits={BRIDGE_EDITS}>
      {children}
    </MappingsEditScope>
  )
}

const OPTS = { revalidateOnFocus: false }
const CHECK_OPTS = { ...OPTS, revalidateOnMount: true, dedupingInterval: 0 }

export type VersionKind = 'mapping' | 'bridge'

export const mapKeys = {
  refs: ['map', 'refs'] as const,
  members: (dimensionId: Id) => ['map', 'members', dimensionId] as const,
  sets: (dimensionId: Id) => ['map', 'sets', dimensionId] as const,
  set: (id: Id) => ['map', 'set', id] as const,
  setVersion: (id: Id) => ['map', 'setVersion', id] as const,
  bridges: (dimensionId: Id) => ['map', 'bridges', dimensionId] as const,
  bridge: (id: Id) => ['map', 'bridge', id] as const,
  bridgeVersion: (id: Id) => ['map', 'bridgeVersion', id] as const,
  reportBridge: (reportId: Id, sourceKey: string) => ['map', 'reportBridge', reportId, sourceKey] as const,
  counts: ['map', 'counts'] as const,
  deleteSet: (id: Id) => ['map', 'deleteSet', id] as const,
  deleteBridge: (id: Id) => ['map', 'deleteBridge', id] as const,
  deleteVersion: (kind: VersionKind, id: Id) => ['map', 'deleteVersion', kind, id] as const,
}

export function useDimensionRefs() {
  const directory = useDirectory()
  return useSWR(mapKeys.refs, () => directory.listDimensionRefs(), OPTS)
}
export function usePublishedMembers(dimensionId: Id | null) {
  const directory = useDirectory()
  return useSWR(dimensionId ? mapKeys.members(dimensionId) : null, () => directory.getPublishedMembers(dimensionId!), OPTS)
}
export function useMappingSets(dimensionId: Id) {
  const api = useMappingsApi()
  return useSWR(mapKeys.sets(dimensionId), () => api.listMappingSets({ dimensionId }), OPTS)
}
export function useMappingSet(id: Id | null) {
  const api = useMappingsApi()
  return useSWR(id ? mapKeys.set(id) : null, () => unwrap(api.getMappingSet(id!)), { ...OPTS, shouldRetryOnError: false })
}
export function useMappingVersion(id: Id | null) {
  const api = useMappingsApi()
  return useSWR(id ? mapKeys.setVersion(id) : null, () => unwrap(api.getMappingVersion(id!)), { ...OPTS, keepPreviousData: true })
}
export function useBridges(dimensionId: Id) {
  const api = useMappingsApi()
  return useSWR(mapKeys.bridges(dimensionId), () => api.listBridges({ dimensionId }), OPTS)
}
export function useBridge(id: Id | null) {
  const api = useMappingsApi()
  return useSWR(id ? mapKeys.bridge(id) : null, () => unwrap(api.getBridge(id!)), { ...OPTS, shouldRetryOnError: false })
}
export function useBridgeVersion(id: Id | null) {
  const api = useMappingsApi()
  return useSWR(id ? mapKeys.bridgeVersion(id) : null, () => unwrap(api.getBridgeVersion(id!)), { ...OPTS, keepPreviousData: true })
}
export function useReportBridge(reportId: Id, sourceKey: string) {
  const api = useMappingsApi()
  return useSWR(mapKeys.reportBridge(reportId, sourceKey), () => api.findReportBridge(reportId, sourceKey), OPTS)
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
          entry(b.targetDimensionId).bridges++
        }
      }
      return counts
    },
    OPTS,
  )
}

export function useMappingSetDeleteCheck(id: Id | null) {
  const api = useMappingsApi()
  return useSWR(id ? mapKeys.deleteSet(id) : null, () => api.checkDeleteMappingSet(id!), CHECK_OPTS)
}
export function useBridgeDeleteCheck(id: Id | null) {
  const api = useMappingsApi()
  return useSWR(id ? mapKeys.deleteBridge(id) : null, () => api.checkDeleteBridge(id!), CHECK_OPTS)
}
export function useVersionDeleteCheck(kind: VersionKind, versionId: Id | null) {
  const api = useMappingsApi()
  const check = kind === 'mapping' ? api.checkDeleteMappingVersion : api.checkDeleteBridgeVersion
  return useSWR(versionId ? mapKeys.deleteVersion(kind, versionId) : null, () => check(versionId!), CHECK_OPTS)
}

export interface VersionCommands {
  check(versionId: Id): Promise<ApiResult<ValidationReport>>
  freeze(versionId: Id, note: string | null): Promise<ApiResult<FreezeResult<MappingVersionSummary | BridgeVersionSummary>>>
  remove(versionId: Id): Promise<ApiResult<null>>
}

/** The lifecycle commands of one version kind, so both editors share one version bar. */
export function useVersionCommands(kind: VersionKind): VersionCommands {
  const api = useMappingsApi()
  return kind === 'mapping'
    ? { check: api.checkMappingVersion, freeze: api.freezeMappingVersion, remove: api.deleteMappingVersion }
    : { check: api.checkBridgeVersion, freeze: api.freezeBridgeVersion, remove: api.deleteBridgeVersion }
}

/** Revalidates every mapping and bridge read. */
export function useRefreshMappings() {
  const { mutate } = useSWRConfig()
  return useCallback(() => mutate((key) => Array.isArray(key) && key[0] === 'map'), [mutate])
}
