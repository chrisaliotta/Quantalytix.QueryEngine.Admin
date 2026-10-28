import { vi } from 'vitest'
import { unwrap } from '@/lib/api/contract'
import { createHttpClient, type HttpClient } from '@/lib/api/http'

/**
 * Test support for the parity suites: an `/api` client against a live API with a development token for a non-admin caller of
 * one tenant. Vitest mirrors process.env into import.meta.env; the app tsconfig carries no Node types.
 */

export const liveTenant = {
  baseUrl: import.meta.env.QE_API_BASE_URL ?? 'http://localhost:5180',
  tenantId: import.meta.env.QE_TENANT_ID ?? 'development',
}

export const isLiveRun = import.meta.env.QE_LIVE_API === '1'

let liveToken: Promise<string> | undefined

export async function createLiveHttp(): Promise<HttpClient> {
  vi.stubGlobal('window', { location: { origin: liveTenant.baseUrl } })
  let token: string | null = null
  const config = { baseUrl: '', token: () => token, onUnauthorized: () => {} }
  liveToken ??= unwrap(
    createHttpClient(config, '').post<{ accessToken: string }>('/development/token', { tenantId: liveTenant.tenantId, systemAdmin: false }),
  ).then((t) => t.accessToken)
  token = await liveToken
  return createHttpClient(config)
}
