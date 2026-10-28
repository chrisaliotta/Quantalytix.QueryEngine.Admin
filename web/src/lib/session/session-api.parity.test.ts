import { beforeEach, describe, expect, it } from 'vitest'
import type { SessionApi } from './contract'
import { createHttpSessionApi } from './http/adapter'
import { createLiveHttp, isLiveRun } from '@/lib/api/live-http'
import { createMockSessionApi } from './mock/adapter'

/**
 * Parity rule (blueprint 00, 02): the mock and HTTP adapters answer the `SessionApi` port the same way, so the shell built on
 * the mock keeps working against the API. Every test runs on the mock; with `QE_LIVE_API=1` it also runs on the HTTP adapter
 * against a live tenant.
 */

const adapters = [
  { name: 'mock', create: async () => createMockSessionApi() },
  ...(isLiveRun ? [{ name: 'http', create: async () => createHttpSessionApi(await createLiveHttp()) }] : []),
]

describe.each(adapters)('SessionApi port over the $name adapter', ({ create }) => {
  let api: SessionApi

  beforeEach(async () => {
    api = await create()
  })

  /** Blueprint 10: the session names the caller and carries a display name slot; there are no tenant roles (OI-09). */
  it('describes the caller', async () => {
    const session = await api.getSession()

    expect(session.subject).not.toBe('')
    expect(Object.keys(session).sort()).toEqual(['displayName', 'isSystemAdmin', 'subject', 'tenantId'])
  })

  /** A caller reaches its own tenant: the session's tenant is among the accessible tenants, which come ordered by tenant id. */
  it('lists the tenants the caller reaches, including its own', async () => {
    const [session, tenants] = await Promise.all([api.getSession(), api.listTenants()])
    const ids = tenants.map((t) => t.tenantId)

    expect(ids).toContain(session.tenantId)
    expect(ids).toEqual([...ids].sort())
  })
})
