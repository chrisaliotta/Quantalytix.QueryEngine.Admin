import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import useSWR, { useSWRConfig } from 'swr'
import { toast } from 'sonner'
import { ChangeScopeProvider, useStagedApi } from '@/components/shell/changes'
import { unwrap, type Problem } from '@/lib/api/contract'
import { apiMode, tenantHttp } from '@/lib/api/session'
import { RELEASE_COMMANDS, RELEASE_EDITS, type ApiResult, type ManifestDataset, type PinIdentity, type ReleaseListItem, type ReleaseReportRef, type ReleasesApi } from './contract'
import { createHttpReleasesApi } from './http/adapter'
import { createMockReleasesApi } from './mock/adapter'

/**
 * One adapter for the whole session, so the header chip, lists and pages agree: the HTTP adapter in http mode, else the in-memory
 * mock. Activation answers the tenant state, which the tenant module's reads refresh from.
 */
let shared: ReleasesApi | null = null
const defaultApi = () => (shared ??= apiMode === 'http' ? createHttpReleasesApi(tenantHttp) : createMockReleasesApi())

const ApiContext = createContext<ReleasesApi | null>(null)

export function ReleasesApiProvider({ children, api }: { children: ReactNode; api?: ReleasesApi }) {
  const [instance] = useState(() => api ?? defaultApi())
  return <ApiContext.Provider value={instance}>{children}</ApiContext.Provider>
}

export function useReleasesApi() {
  const api = useContext(ApiContext)
  if (!api) throw new Error('useReleasesApi must be used inside ReleasesApiProvider')
  return api
}

/**
 * Batches manifest edits of one draft release until Save. Children keep calling `useReleasesApi()`; they receive the
 * staged port.
 */
export function ReleaseEditScope({ releaseId, label, children }: { releaseId: string; label: string; children: ReactNode }) {
  const base = useReleasesApi()
  const refresh = useRefreshReleases()
  const { api, scope } = useStagedApi(base, { id: `release:${releaseId}`, label, edits: RELEASE_EDITS, commands: RELEASE_COMMANDS, onSettled: refresh })
  return (
    <ApiContext.Provider value={api}>
      <ChangeScopeProvider scope={scope}>{children}</ChangeScopeProvider>
    </ApiContext.Provider>
  )
}

const OPTS = { revalidateOnFocus: false }

export function useReleaseList() {
  const api = useReleasesApi()
  return useSWR(['rel', 'list'], () => api.listReleases(), OPTS)
}
export function useRelease(id: string | null) {
  const api = useReleasesApi()
  return useSWR(id ? ['rel', 'release', id] : null, () => unwrap(api.getRelease(id!)), { ...OPTS, shouldRetryOnError: false })
}
export function useReleaseDiff(fromId: string | null, toId: string | null) {
  const api = useReleasesApi()
  return useSWR(fromId && toId ? ['rel', 'diff', fromId, toId] : null, () => api.diffReleases(fromId!, toId!), OPTS)
}
export function useManifestDatasets() {
  const api = useReleasesApi()
  return useSWR(['rel', 'manifestDatasets'], () => api.listManifestDatasets(), OPTS)
}
export function useIdentities() {
  const api = useReleasesApi()
  return useSWR(['rel', 'identities'], () => api.listIdentities(), OPTS)
}
export function useReportReads() {
  const api = useReleasesApi()
  return useSWR(['rel', 'reportReads'], () => api.listReportReads(), OPTS)
}
export function useSetList() {
  const api = useReleasesApi()
  return useSWR(['rel', 'sets'], () => api.listSets(), OPTS)
}
export function useSet(id: string | null) {
  const api = useReleasesApi()
  return useSWR(id ? ['rel', 'set', id] : null, () => unwrap(api.getSet(id!)), { ...OPTS, shouldRetryOnError: false })
}
export function useSetComparison(fromSetId: string | null, toSetId: string | null) {
  const api = useReleasesApi()
  return useSWR(fromSetId && toSetId ? ['rel', 'setCompare', fromSetId, toSetId] : null, () => api.compareSets(fromSetId!, toSetId!), OPTS)
}
export function useCapturePreview(enabled: boolean) {
  const api = useReleasesApi()
  return useSWR(enabled ? ['rel', 'capturePreview'] : null, () => api.capturePreview(), OPTS)
}

export function useRefreshReleases() {
  const { mutate } = useSWRConfig()
  return useCallback(() => mutate((key) => Array.isArray(key) && key[0] === 'rel'), [mutate])
}

/** Runs one write: keeps its problem for forms, toasts on success, and revalidates release data. */
export function useRelCall() {
  const refresh = useRefreshReleases()
  const [saving, setSaving] = useState(false)
  const [problem, setProblem] = useState<Problem | null>(null)
  const run = useCallback(
    async <T,>(call: () => Promise<ApiResult<T>>, success?: string | ((data: T) => string | null)): Promise<{ ok: true; data: T } | { ok: false }> => {
      setSaving(true)
      const result = await call()
      setSaving(false)
      if (!result.ok) {
        setProblem(result.problem)
        if (!Object.keys(result.problem.errors).some((k) => k !== '')) toast.error(result.problem.title, { description: result.problem.detail })
        return { ok: false }
      }
      setProblem(null)
      await refresh()
      const message = typeof success === 'function' ? success(result.data) : success
      if (message) toast.success(message)
      return { ok: true, data: result.data }
    },
    [refresh],
  )
  const errorsFor = useCallback((path: string) => problem?.errors[path], [problem])
  return { run, saving, problem, setProblem, errorsFor }
}

interface LookupSources {
  releases?: ReleaseListItem[]
  datasets?: ManifestDataset[]
  identities?: PinIdentity[]
  reports?: ReleaseReportRef[]
}

function makeLookup({ releases = [], datasets = [], identities = [], reports = [] }: LookupSources) {
  const datasetById = new Map(datasets.map((d) => [d.id, d]))
  const versions = new Map(datasets.flatMap((d) => d.versions.map((v) => [v.id, { ...v, datasetId: d.id }] as const)))
  const identityById = new Map(identities.map((i) => [i.id, i]))
  const identityVersions = new Map(identities.flatMap((i) => i.versions.map((v) => [v.id, { ...v, identityId: i.id }] as const)))
  const releaseById = new Map(releases.map((r) => [r.id, r]))
  const reportById = new Map(reports.map((r) => [r.id, r]))
  const versionsOf = (datasetId: string) => datasetById.get(datasetId)?.versions ?? []
  return {
    datasets,
    activeRelease: releases.find((r) => r.isActive),
    dataset: (id: string) => datasetById.get(id),
    version: (id: string) => versions.get(id),
    identity: (id: string) => identityById.get(id),
    identityVersion: (id: string) => identityVersions.get(id),
    release: (id: string) => releaseById.get(id),
    report: (id: string) => reportById.get(id),
    /** Newest first. */
    versionsOf,
    latestPublished: (datasetId: string) => versionsOf(datasetId).find((v) => v.status === 'published'),
  }
}
export type Lookup = ReturnType<typeof makeLookup>

/** Name lookups over the reads the release and set screens render from. */
export function useLookup() {
  const { data: releases } = useReleaseList()
  const { data: datasets } = useManifestDatasets()
  const { data: identities } = useIdentities()
  const { data: reports } = useReportReads()
  return useMemo(() => makeLookup({ releases, datasets, identities, reports }), [releases, datasets, identities, reports])
}
