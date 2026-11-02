import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import useSWR, { useSWRConfig } from 'swr'
import { toast } from 'sonner'
import { unwrap, type Problem } from '@/lib/api/contract'
import { apiMode, tenantHttp } from '@/lib/api/session'
import { useTenantCatalog } from '@/lib/tenant/client'
import type { TenantCatalogSnapshot } from '@/lib/tenant/contract'
import type { ApiResult, Id, SnapshotsApi } from './contract'
import { createHttpSnapshotsApi } from './http/adapter'
import { createMockSnapshotsApi } from './mock/adapter'

const ApiContext = createContext<SnapshotsApi | null>(null)

/** Supplies the adapter: an explicit `api`, else the HTTP adapter in http mode, else the in-memory mock. */
export function SnapshotsApiProvider({ children, api }: { children: ReactNode; api?: SnapshotsApi }) {
  const [instance] = useState(() => api ?? (apiMode === 'http' ? createHttpSnapshotsApi(tenantHttp) : createMockSnapshotsApi()))
  return <ApiContext.Provider value={instance}>{children}</ApiContext.Provider>
}

export function useSnapshotsApi() {
  const api = useContext(ApiContext)
  if (!api) throw new Error('useSnapshotsApi must be used inside SnapshotsApiProvider')
  return api
}

const OPTS = { revalidateOnFocus: false }

export function useSnapshotList() {
  const api = useSnapshotsApi()
  return useSWR(['snap', 'list'], () => api.listSnapshots(), OPTS)
}
export function useSnapshot(id: Id | null) {
  const api = useSnapshotsApi()
  return useSWR(id ? ['snap', 'snapshot', id] : null, () => unwrap(api.getSnapshot(id!)), { ...OPTS, shouldRetryOnError: false })
}
export function useSnapshotComparison(fromId: Id | null, toId: Id | null) {
  const api = useSnapshotsApi()
  return useSWR(fromId && toId ? ['snap', 'compare', fromId, toId] : null, () => api.compareSnapshots(fromId!, toId!), OPTS)
}
export function useCapturePreview() {
  const api = useSnapshotsApi()
  return useSWR(['snap', 'capturePreview'], () => api.capturePreview(), { ...OPTS, revalidateOnMount: true, dedupingInterval: 0 })
}

/** Snapshot names by id, from the tenant catalog, so any module labels a version by the snapshot that holds it. */
export function useSnapshotNames(): Map<Id, TenantCatalogSnapshot> {
  const { data } = useTenantCatalog()
  return useMemo(() => new Map((data?.snapshots ?? []).map((s) => [s.id, s])), [data])
}

/**
 * Runs one snapshot command: keeps its problem for forms, toasts on success, then revalidates every read, since capture, restore
 * and delete change which versions every module lists and restore rewrites live content.
 */
export function useSnapshotCall() {
  const { mutate } = useSWRConfig()
  const [saving, setSaving] = useState(false)
  const [problem, setProblem] = useState<Problem | null>(null)
  const run = useCallback(
    async <T,>(call: () => Promise<ApiResult<T>>, success: (data: T) => string): Promise<ApiResult<T>> => {
      setSaving(true)
      const result = await call()
      setSaving(false)
      if (!result.ok) {
        setProblem(result.problem)
        if (!Object.keys(result.problem.errors).some((k) => k !== '')) toast.error(result.problem.title, { description: result.problem.detail })
        return result
      }
      setProblem(null)
      await mutate(() => true)
      toast.success(success(result.data))
      return result
    },
    [mutate],
  )
  const errorsFor = useCallback((path: string) => problem?.errors[path], [problem])
  return { run, saving, problem, setProblem, errorsFor }
}
