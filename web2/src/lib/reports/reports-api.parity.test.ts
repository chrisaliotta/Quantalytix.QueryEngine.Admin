import { beforeEach, describe, expect, it, onTestFinished } from 'vitest'
import { unwrap } from '@/lib/api/contract'
import { createLiveHttp, isLiveRun, liveTenant } from '@/lib/api/live-http'
import type { ReportRow, ReportsApi, ReportSummary } from './contract'
import { createHttpReportsApi } from './http/adapter'
import { createMockReportsApi } from './mock/adapter'

/**
 * Parity rule (blueprint 00, 02): the mock and HTTP adapters answer the `ReportsApi` port the same way, so the report screens
 * built on the mock keep working against the API. Every test runs on the mock; with `QE_LIVE_API=1` it also runs on the HTTP
 * adapter against a live tenant. Live tests create and delete their own reports, copy the seeded margin statement where they need
 * sources, and never assume an id format.
 */

const adapters = [
  { name: 'mock', create: async () => createMockReportsApi() },
  ...(isLiveRun
    ? [{ name: 'http', create: async () => createHttpReportsApi((await createLiveHttp()).forTenant(liveTenant.tenantId)) }]
    : []),
]

const SEEDED = 'bank.margin_statement'

const uniqueKey = () => `parity_r${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`

const row = (key: string, kind: ReportRow['kind'], parentKey: string | null): ReportRow => ({
  key,
  label: key,
  kind,
  parentKey,
  isVisible: true,
  showsValues: true,
  calculatedMeasureKey: null,
  valueColumnKey: null,
  styleKey: null,
  parameters: {},
})

describe.each(adapters)('ReportsApi port over the $name adapter', ({ create }) => {
  let api: ReportsApi

  beforeEach(async () => {
    api = await create()
  })

  async function seeded() {
    return (await api.listReports()).find((r) => r.key === SEEDED)!
  }

  /** A report of the test's own, over the seeded report's model or copying it, deleted when the test finishes. */
  async function ownReport(copyFrom?: ReportSummary): Promise<ReportSummary> {
    const model = copyFrom ?? (await seeded())
    const key = uniqueKey()
    const report = await unwrap(api.createReport({ key, displayName: key, reportType: 'test', modelId: model.modelId, copyFromReportId: copyFrom?.id }))
    onTestFinished(async () => {
      await api.discardChanges()
      await api.deleteReport(report.id)
    })
    return report
  }

  /** A new report starts disabled with a single top-level group row, its total, and is listed. */
  it('creates a disabled report with a single root row', async () => {
    const report = await ownReport()

    expect(report.isEnabled).toBe(false)
    const document = await unwrap(api.getReport(report.id))
    expect(document.rows).toHaveLength(1)
    expect(document.rows[0]).toMatchObject({ kind: 'group', parentKey: null })
    expect((await api.listReports()).some((r) => r.id === report.id)).toBe(true)
  })

  /** A key another report has is refused as a conflict (409). */
  it('refuses a taken report key', async () => {
    const report = await ownReport()

    const taken = await api.createReport({ key: report.key, displayName: 'Again', reportType: 'test', modelId: report.modelId })

    expect(taken.ok).toBe(false)
    if (!taken.ok) expect(taken.problem.status).toBe(409)
  })

  /** A data row saved under the total is stored as its child once the change set commits. */
  it('saves a row under the total', async () => {
    const report = await ownReport()
    const root = (await unwrap(api.getReport(report.id))).rows[0]

    api.beginChanges()
    await unwrap(api.saveRow(report.id, { ...row('LOANS', 'data', root.key), index: 0 }))
    await unwrap(api.commitChanges())

    const stored = await unwrap(api.getReport(report.id))
    expect(stored.rows.map((r) => r.key)).toEqual([root.key, 'LOANS'])
    expect(stored.rows[1].parentKey).toBe(root.key)
  })

  /** The total is the one top-level member row, so deleting it is refused as invalid input (400). */
  it('refuses deleting the total row', async () => {
    const report = await ownReport()
    const root = (await unwrap(api.getReport(report.id))).rows[0]

    api.beginChanges()
    const refused = await api.deleteRow(report.id, root.key)

    expect(refused.ok).toBe(false)
    if (!refused.ok) expect(refused.problem.status).toBe(400)
  })

  /** An edit naming a source the report does not have is a conflict (409): another author may have removed it. */
  it('answers a conflict for a source the report lacks', async () => {
    const report = await ownReport()

    api.beginChanges()
    const refused = await api.deleteSource(report.id, 'nope')

    expect(refused.ok).toBe(false)
    if (!refused.ok) expect(refused.problem.status).toBe(409)
  })

  /** A copy starts disabled with the copied report's rows, sources and profiles. */
  it('copies a report into a disabled report', async () => {
    const original = await unwrap(api.getReport((await seeded()).id))

    const copy = await unwrap(api.getReport((await ownReport(await seeded())).id))

    expect(copy.isEnabled).toBe(false)
    expect(copy.rows.map((r) => r.key)).toEqual(original.rows.map((r) => r.key))
    expect(copy.sources.map((s) => s.key)).toEqual(original.sources.map((s) => s.key))
    expect(copy.profiles.map((p) => p.key)).toEqual(original.profiles.map((p) => p.key))
  })

  /** Deleting a calculation dimension also removes it from the default breakout (OI-56). */
  it('removes a deleted calculation dimension from the default breakout', async () => {
    const report = await ownReport(await seeded())
    const dimension = (await unwrap(api.getReport(report.id))).calculationDimensions[0]

    api.beginChanges()
    await unwrap(api.updateSettings(report.id, { defaultBreakout: [dimension.key] }))
    await unwrap(api.deleteCalculationDimension(report.id, dimension.key))

    expect((await unwrap(api.getReport(report.id))).settings.defaultBreakout).toEqual([])
  })

  /** The default profile cannot be deleted: the edit is refused with `default_profile_enabled` (422). */
  it('refuses deleting the default profile', async () => {
    const report = await ownReport(await seeded())
    const { defaultProfileKey } = (await unwrap(api.getReport(report.id))).settings

    api.beginChanges()
    const refused = await api.deleteProfile(report.id, defaultProfileKey!)

    expect(refused.ok).toBe(false)
    if (!refused.ok) expect(refused.problem.violations?.map((v) => v.code)).toContain('default_profile_enabled')
  })

  /** Rules go into data rows only: rules into the total, a group, are refused as invalid input (400). */
  it('refuses bridge rules into a group row', async () => {
    const report = await ownReport(await seeded())
    const document = await unwrap(api.getReport(report.id))
    const bridge = document.sources.find((s) => s.method === 'bridge')!
    const group = document.rows.find((r) => r.kind === 'group')!

    api.beginChanges()
    const refused = await api.setRowRules(report.id, bridge.key, group.key, [])

    expect(refused.ok).toBe(false)
    if (!refused.ok) expect(refused.problem.status).toBe(400)
  })

  /** A report without columns breaks a report rule, so enabling it is refused (422) and it stays disabled. */
  it('refuses enabling a report that breaks a rule', async () => {
    const report = await ownReport()

    const refused = await api.setEnabled(report.id, true)

    expect(refused.ok).toBe(false)
    if (!refused.ok) expect(refused.problem.status).toBe(422)
    expect((await api.listReports()).find((r) => r.id === report.id)!.isEnabled).toBe(false)
  })
})
