import { useCallback } from 'react'
import useSWR, { useSWRConfig } from 'swr'
import type { TenantApi } from './contract'
import { createMockTenantApi } from './mock/adapter'

/** One in-memory store for the whole session, so every revision display agrees. */
const api: TenantApi = createMockTenantApi()

const OVERVIEW_KEY = ['tenant', 'overview']

export function useTenantOverview() {
  return useSWR(OVERVIEW_KEY, () => api.getOverview(), { revalidateOnFocus: false })
}

/** Re-reads the tenant state after a command that advances `semanticRevision`. */
export function useRefreshTenant() {
  const { mutate } = useSWRConfig()
  return useCallback(() => mutate(OVERVIEW_KEY), [mutate])
}
