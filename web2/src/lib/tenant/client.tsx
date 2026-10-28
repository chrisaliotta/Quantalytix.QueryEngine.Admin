import { useCallback } from 'react'
import useSWR, { useSWRConfig } from 'swr'
import { apiHttp, apiMode, tenantId } from '@/lib/api/session'
import type { Id, TenantApi } from './contract'
import { createHttpTenantApi } from './http/adapter'
import { createMockTenantApi } from './mock/adapter'

/** One adapter for the whole session, so every revision display agrees: HTTP in http mode, else the in-memory mock. */
const api: TenantApi = apiMode === 'http' ? createHttpTenantApi(apiHttp, tenantId) : createMockTenantApi()

const OPTS = { revalidateOnFocus: false }

const tenantKeys = {
  overview: ['tenant', 'overview'] as const,
  catalog: ['tenant', 'catalog'] as const,
  vocabularies: ['tenant', 'vocabularies'] as const,
  publishedMembers: (dimensionId: Id) => ['tenant', 'publishedMembers', dimensionId] as const,
}

export function useTenantOverview() {
  return useSWR(tenantKeys.overview, () => api.getOverview(), OPTS)
}

/** The one catalog every picker reads (OI-13); `useRefreshTenant` reloads it when `semanticRevision` advances. */
export function useTenantCatalog() {
  return useSWR(tenantKeys.catalog, () => api.getCatalog(), OPTS)
}

/** Members of a dimension's published version, null when none is published; a null id skips the request. */
export function usePublishedMembers(dimensionId: Id | null) {
  return useSWR(dimensionId ? tenantKeys.publishedMembers(dimensionId) : null, () => api.getPublishedMembers(dimensionId!), OPTS)
}

export function useVocabularies() {
  return useSWR(tenantKeys.vocabularies, () => api.getVocabularies(), { ...OPTS, revalidateIfStale: false })
}

/** Re-reads the tenant state, catalog and published members after a command that advances `semanticRevision`. */
export function useRefreshTenant() {
  const { mutate } = useSWRConfig()
  return useCallback(() => mutate((key) => Array.isArray(key) && key[0] === 'tenant' && key[1] !== 'vocabularies'), [mutate])
}
