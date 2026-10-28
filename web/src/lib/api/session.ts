import { unwrap } from './contract'
import { createHttpClient, type HttpConfig } from './http'

/**
 * Development session until the tenant module's sign-in lands (blueprint 10): the API mode and tenant come from the
 * Vite env, and the bearer token is minted by `POST /development/token` and kept in sessionStorage.
 */

export type ApiMode = 'http' | 'mock'

export const apiMode: ApiMode = import.meta.env.VITE_API_MODE ?? 'mock'
export const tenantId = import.meta.env.VITE_API_TENANT ?? 'development'

const TOKEN_KEY = 'qe.accessToken'

interface DevelopmentToken {
  accessToken: string
  tenantId: string
  systemAdmin: boolean
  expiresUtc: string
}

const config: HttpConfig = {
  baseUrl: '',
  token: () => sessionStorage.getItem(TOKEN_KEY),
  onUnauthorized: () => sessionStorage.removeItem(TOKEN_KEY),
}

/** Paths relative to `/api`. */
export const apiHttp = createHttpClient(config)

/** Paths relative to `/api/tenants/{tenantId}`. */
export const tenantHttp = apiHttp.forTenant(tenantId)

export async function ensureDevelopmentToken() {
  if (sessionStorage.getItem(TOKEN_KEY)) return
  const token = await unwrap(createHttpClient(config, '').post<DevelopmentToken>('/development/token', { tenantId, systemAdmin: false }))
  sessionStorage.setItem(TOKEN_KEY, token.accessToken)
}
