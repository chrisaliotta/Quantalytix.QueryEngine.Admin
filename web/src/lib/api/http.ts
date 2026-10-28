import type { ApiResult, Problem } from './contract'

/** Transport settings, supplied once by the session layer. */
export interface HttpConfig {
  /** Origin of the Admin API without a trailing slash; `''` when web2 is served from the API's origin. */
  baseUrl: string
  /** Bearer token for the next request, or null to send none. */
  token(): string | null
  /** Runs on every 401 so the session can clear itself and route to sign-in (02, Error mapping). */
  onUnauthorized(): void
}

export type Query = Record<string, string | number | boolean | null | undefined>

/**
 * Typed fetch over the Admin API. Every call resolves to an `ApiResult`: 2xx bodies are the data (204 is `null`),
 * error bodies are the `Problem`. Only HTTP adapters (`lib/<module>/http`) use it.
 */
export interface HttpClient {
  get<T>(path: string, query?: Query): Promise<ApiResult<T>>
  post<T>(path: string, body?: unknown): Promise<ApiResult<T>>
  put<T>(path: string, body: unknown): Promise<ApiResult<T>>
  patch<T>(path: string, body: unknown): Promise<ApiResult<T>>
  delete<T = null>(path: string): Promise<ApiResult<T>>
  /** A client for one tenant: paths are relative to `/api/tenants/{tenantId}`. */
  forTenant(tenantId: string): HttpClient
}

/** A client whose paths are relative to `prefix`, `/api` by default. */
export function createHttpClient(config: HttpConfig, prefix = '/api'): HttpClient {
  async function send<T>(method: string, path: string, body?: unknown, query?: Query): Promise<ApiResult<T>> {
    const url = new URL(`${config.baseUrl}${prefix}${path}`, window.location.origin)
    for (const [name, value] of Object.entries(query ?? {})) {
      if (value !== null && value !== undefined) url.searchParams.set(name, String(value))
    }
    const headers = new Headers({ Accept: 'application/json, application/problem+json' })
    const token = config.token()
    if (token) headers.set('Authorization', `Bearer ${token}`)
    if (body !== undefined) headers.set('Content-Type', 'application/json')

    const response = await fetch(url, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) })
    if (response.status === 401) config.onUnauthorized()
    if (response.ok) return { ok: true, data: response.status === 204 ? (null as T) : await response.json() }
    return { ok: false, problem: await problemOf(response) }
  }

  return {
    get: (path, query) => send('GET', path, undefined, query),
    post: (path, body) => send('POST', path, body),
    put: (path, body) => send('PUT', path, body),
    patch: (path, body) => send('PATCH', path, body),
    delete: (path) => send('DELETE', path),
    forTenant: (tenantId) => createHttpClient(config, `${prefix}/tenants/${encodeURIComponent(tenantId)}`),
  }
}

/** Gateways and proxies in front of the API can answer without a problem body; those become a title-only Problem. */
async function problemOf(response: Response): Promise<Problem> {
  if (response.headers.get('Content-Type')?.includes('json')) return response.json()
  return { type: 'about:blank', title: response.statusText || `HTTP ${response.status}`, status: response.status as Problem['status'], errors: {} }
}
