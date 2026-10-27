import { beforeEach, describe, expect, it } from 'vitest'
import { createLiveHttp, isLiveRun, liveTenant } from '@/lib/session/live-http'
import type { TenantApi } from './contract'
import { createHttpTenantApi } from './http/adapter'
import { createMockTenantApi } from './mock/adapter'

/**
 * Parity rule (blueprint 00, 02): the mock and HTTP adapters answer the `TenantApi` port the same way, so screens built on the
 * mock keep working against the API. Every test runs on the mock; with `QE_LIVE_API=1` it also runs on the HTTP adapter against
 * a live tenant. The tests only read, and never assume an id format.
 */

const adapters = [
  { name: 'mock', create: async () => createMockTenantApi() },
  ...(isLiveRun ? [{ name: 'http', create: async () => createHttpTenantApi(await createLiveHttp(), liveTenant.tenantId) }] : []),
]

describe.each(adapters)('TenantApi port over the $name adapter', ({ create }) => {
  let api: TenantApi

  beforeEach(async () => {
    api = await create()
  })

  /** OI-11: every vocabulary a select renders is served, including the open ones, and every measure kind lists its component roles. */
  it('serves every vocabulary, keyed by name', async () => {
    const vocabularies = await api.getVocabularies()

    for (const key of ['editPolicy', 'expressionLanguageVersion', 'timeZone', 'displayFormat'] as const) {
      expect(vocabularies[key].length).toBeGreaterThan(0)
    }
    expect(Object.keys(vocabularies.componentRolesByKind).sort()).toEqual(vocabularies.measureKind.map((t) => t.value).sort())
    expect(vocabularies.componentRolesByKind.weighted_average).toEqual([
      { role: 'value', required: true },
      { role: 'weight', required: true },
    ])
  })

  /** The catalog and the overview report the same tenant state, so a revision display agrees with the cached catalog. */
  it('reports one tenant state in the overview and the catalog', async () => {
    const [overview, catalog] = await Promise.all([api.getOverview(), api.getCatalog()])

    const { counts: _counts, drafts: _drafts, ...info } = overview
    expect(info).toEqual(catalog.tenant)
  })

  /** OI-22: the overview counts analytical dimensions and bridges only; report-owned ones are in the catalog only so references resolve. */
  it('counts the catalog without report-owned rows', async () => {
    const [{ counts, drafts }, catalog] = await Promise.all([api.getOverview(), api.getCatalog()])

    expect(counts).toMatchObject({
      datasets: catalog.datasets.length,
      dimensions: catalog.dimensions.filter((d) => d.purpose === 'analytical').length,
      mappingSets: catalog.mappingSets.length,
      bridges: catalog.bridges.filter((b) => b.ownerReportId === null).length,
      releases: catalog.releases.length,
      assumptionSets: catalog.assumptionSets.length,
      models: catalog.models.length,
      reports: catalog.reports.length,
    })
    expect(drafts.releases).toBe(catalog.releases.filter((r) => r.status === 'draft').length)
  })

  /** Exactly the release the tenant points runtime queries at is active; a report dimension names its report. */
  it('marks the active release and report ownership', async () => {
    const catalog = await api.getCatalog()

    expect(catalog.releases.filter((r) => r.isActive).map((r) => r.id)).toEqual(catalog.tenant.activeReleaseId ? [catalog.tenant.activeReleaseId] : [])
    const reportIds = new Set(catalog.reports.map((r) => r.id))
    for (const dimension of catalog.dimensions) {
      expect(dimension.ownerReportId === null || reportIds.has(dimension.ownerReportId)).toBe(true)
      expect(dimension.ownerReportId !== null).toBe(dimension.purpose === 'report')
    }
  })

  /** Pickers read members of the live version in hierarchy order: every parent precedes its children. */
  it('reads the published members of a dimension in hierarchy order', async () => {
    const dimension = (await api.getCatalog()).dimensions.find((d) => d.publishedVersionId !== null)!

    const published = (await api.getPublishedMembers(dimension.id))!

    expect(published).toMatchObject({ dimensionId: dimension.id, versionId: dimension.publishedVersionId })
    const seen = new Set<string>()
    for (const member of published.members) {
      if (member.parentKey !== null) expect(seen).toContain(member.parentKey)
      seen.add(member.key)
    }
  })

  /** A dimension the tenant does not hold has no published members (the API's 404). */
  it('answers null for an unknown dimension', async () => {
    expect(await api.getPublishedMembers('missing')).toBeNull()
  })
})
