import { useCallback, useMemo } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { useNavigationGuard } from '@/components/shell/changes'
import type { SnapshotTab } from '@/lib/snapshots/contract'
import { useEditor } from '@/lib/report/store'

export const paths = {
  reports: '/reports',
  report: (id: string) => `/reports/${id}`,
  models: '/calculation-models',
  model: (key: string) => `/calculation-models/${key}`,
  datasets: '/datasets',
  dataset: (id: string) => `/datasets/${id}`,
  dimensions: '/dimensions',
  dimension: (id: string) => `/dimensions/${id}`,
  snapshots: '/snapshots',
  snapshot: (id: string) => `/snapshots/${id}`,
}

/** Query-string values; null or undefined leaves the key out. */
export type SearchValues = Record<string, string | null | undefined>

const withQuery = (path: string, values: SearchValues = {}) => {
  const query = new URLSearchParams(Object.entries(values).filter((e): e is [string, string] => !!e[1])).toString()
  return query ? `${path}?${query}` : path
}

/** Sets or clears query-string values on the current page in place, so view state survives a refresh without adding history entries. */
export function useSetSearchParams() {
  const [, setSearchParams] = useSearchParams()
  return useCallback(
    (values: SearchValues) =>
      setSearchParams(
        (prev) => {
          for (const [k, v] of Object.entries(values)) {
            if (v) prev.set(k, v)
            else prev.delete(k)
          }
          return prev
        },
        { replace: true },
      ),
    [setSearchParams],
  )
}

/** One query-string value and its setter. */
export function useSearchParam(name: string): [string | null, (value: string | null) => void] {
  const [searchParams] = useSearchParams()
  const set = useSetSearchParams()
  const setValue = useCallback((value: string | null) => set({ [name]: value }), [set, name])
  return [searchParams.get(name), setValue]
}

/** Cross-screen navigation. Every jump waits for the user to save or discard pending edits. */
export function useAppNav() {
  const navigate = useNavigate()
  const guard = useNavigationGuard()
  const { library } = useEditor()

  return useMemo(() => {
    const go = (path: string, values?: SearchValues) => guard(() => navigate(withQuery(path, values)))

    const reportByKey = (key: string) => {
      const item = library.find((l) => l.report.settings.key === key)
      if (item) return go(paths.report(item.id))
      go(paths.reports)
      toast.message(`Report ${key} is not in this workspace.`)
    }

    return {
      go,
      reports: () => go(paths.reports),
      report: (id: string) => go(paths.report(id)),
      reportByKey,
      models: () => go(paths.models),
      model: (key: string) => go(paths.model(key)),
      datasets: () => go(paths.datasets),
      dataset: (id: string, versionId?: string) => go(paths.dataset(id), { version: versionId }),
      dimensions: () => go(paths.dimensions),
      dimension: (id: string, values?: SearchValues) => go(paths.dimension(id), values),
      /** A mapping set is edited on its dimension's Mappings tab. */
      mappingSet: (dimensionId: string, id: string, versionId?: string) => go(paths.dimension(dimensionId), { tab: 'mappings', ms: id, msv: versionId }),
      /** An analytical bridge is edited on its source dimension's Bridges tab. */
      bridge: (dimensionId: string, id: string, versionId?: string) => go(paths.dimension(dimensionId), { tab: 'bridges', br: id, brv: versionId }),
      snapshots: () => go(paths.snapshots),
      snapshot: (id: string, tab?: SnapshotTab) => go(paths.snapshot(id), { tab: tab !== 'contents' ? tab : null }),
    }
  }, [navigate, guard, library])
}
