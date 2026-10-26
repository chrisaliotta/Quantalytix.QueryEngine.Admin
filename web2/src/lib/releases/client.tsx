import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import useSWR, { useSWRConfig } from 'swr'
import { toast } from 'sonner'
import type { ApiResult, Catalog, ManifestEntry, ProblemDetails, ReleasesApi } from './contract'
import { createMockReleasesApi } from './mock/adapter'

/** One in-memory store for the whole session, so the header chip, lists and pages agree. */
let shared: ReleasesApi | null = null
const defaultApi = () => (shared ??= createMockReleasesApi())

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

export class ReleaseProblemError extends Error {
  constructor(public problem: ProblemDetails) {
    super(problem.title)
  }
}

async function unwrap<T>(p: Promise<ApiResult<T>>): Promise<T> {
  const r = await p
  if (!r.ok) throw new ReleaseProblemError(r.problem)
  return r.data
}

const OPTS = { revalidateOnFocus: false }

export function useCatalog() {
  const api = useReleasesApi()
  return useSWR(['rel', 'catalog'], () => api.getCatalog(), OPTS)
}
export function useReleaseList() {
  const api = useReleasesApi()
  return useSWR(['rel', 'list'], () => api.listReleases(), OPTS)
}
export function useRelease(id: string | null) {
  const api = useReleasesApi()
  return useSWR(id ? ['rel', 'release', id] : null, () => unwrap(api.getRelease(id as string)), { ...OPTS, shouldRetryOnError: false })
}
export function useReleaseDiff(fromId: string | null, toId: string | null, toManifest?: ManifestEntry[]) {
  const api = useReleasesApi()
  const sig = toManifest ? JSON.stringify(toManifest) : ''
  return useSWR(fromId && toId ? ['rel', 'diff', fromId, toId, sig] : null, () => api.diffReleases(fromId as string, toId as string, toManifest), OPTS)
}
export function useSetList() {
  const api = useReleasesApi()
  return useSWR(['rel', 'sets'], () => api.listSets(), OPTS)
}
export function useSet(id: string | null) {
  const api = useReleasesApi()
  return useSWR(id ? ['rel', 'set', id] : null, () => unwrap(api.getSet(id as string)), { ...OPTS, shouldRetryOnError: false })
}
export function useSetComparison(fromSetId: string | null, toSetId: string | null) {
  const api = useReleasesApi()
  return useSWR(fromSetId && toSetId ? ['rel', 'setCompare', fromSetId, toSetId] : null, () => api.compareSets(fromSetId as string, toSetId as string), OPTS)
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
  const [problem, setProblem] = useState<ProblemDetails | null>(null)
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

/** Name lookups over the catalog. */
export function useLookup() {
  const { data } = useCatalog()
  return useMemo(() => makeLookup(data), [data])
}

export function makeLookup(c: Catalog | undefined) {
  const datasets = new Map(c?.datasets.map((d) => [d.id, d]))
  const versions = new Map(c?.versions.map((v) => [v.id, v]))
  const identities = new Map(c?.identities.map((i) => [i.id, i]))
  const identityVersions = new Map(c?.identities.flatMap((i) => i.versions.map((v) => [v.id, { ...v, identityId: i.id }] as const)))
  const releases = new Map(c?.releases.map((r) => [r.id, r]))
  const reports = new Map(c?.reports.map((r) => [r.id, r]))
  return {
    ready: !!c,
    catalog: c,
    dataset: (id: string) => datasets.get(id),
    version: (id: string) => versions.get(id),
    identity: (id: string) => identities.get(id),
    identityVersion: (id: string) => identityVersions.get(id),
    release: (id: string) => releases.get(id),
    report: (id: string) => reports.get(id),
    versionsOf: (datasetId: string) => (c?.versions ?? []).filter((v) => v.datasetId === datasetId).sort((a, b) => b.versionNumber - a.versionNumber),
    latestPublished: (datasetId: string) =>
      (c?.versions ?? []).filter((v) => v.datasetId === datasetId && v.status === 'published').sort((a, b) => b.versionNumber - a.versionNumber)[0],
  }
}
export type Lookup = ReturnType<typeof makeLookup>
