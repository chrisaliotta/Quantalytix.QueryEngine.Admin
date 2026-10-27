import useSWR from 'swr'
import { apiHttp, apiMode } from '@/lib/api/session'
import type { SessionApi } from './contract'
import { createHttpSessionApi } from './http/adapter'
import { createMockSessionApi } from './mock/adapter'

/** HTTP in http mode, else the in-memory mock. */
const api: SessionApi = apiMode === 'http' ? createHttpSessionApi(apiHttp) : createMockSessionApi()

const OPTS = { revalidateOnFocus: false, revalidateIfStale: false }

/** The caller. `isSystemAdmin` only gates the tenant directory; there are no tenant roles (OI-09). */
export function useSession() {
  return useSWR(['session', 'me'], () => api.getSession(), OPTS)
}

/** The tenants the caller can reach. */
export function useAccessibleTenants() {
  return useSWR(['session', 'tenants'], () => api.listTenants(), OPTS)
}
