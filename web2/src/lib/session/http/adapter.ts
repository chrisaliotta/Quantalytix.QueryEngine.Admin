import { unwrap } from '@/lib/api/contract'
import type { HttpClient } from '@/lib/api/http'
import type { components } from '@/lib/api/schema'
import type { AccessibleTenant, Session, SessionApi } from '../contract'

type Schemas = components['schemas']

/** `SessionApi` over the Admin API; `http` resolves paths under `/api`. */
export function createHttpSessionApi(http: HttpClient): SessionApi {
  return {
    async getSession(): Promise<Session> {
      return unwrap(http.get<Schemas['Session']>('/me'))
    },
    async listTenants(): Promise<AccessibleTenant[]> {
      return unwrap(http.get<Schemas['AccessibleTenant'][]>('/tenants'))
    },
  }
}
