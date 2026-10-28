import { describe, expect, it } from 'vitest'
import { unwrap, type Id } from '@/lib/api/contract'
import type { ReportOperation, ReportsApi } from '@/lib/reports/contract'
import { createMockReportsApi } from '@/lib/reports/mock/adapter'
import { catalog, publishedMembers } from '@/lib/tenant/mock/data'
import type { PublishedMember } from '@/lib/tenant/contract'
import type { ReportCatalog } from './http-catalog'
import { reportFromDocument, reportOperations, type BridgeContext } from './http-bridge'
import type { DimMember, ReportState } from './types'

/**
 * In http mode the report editor loads a `ReportDocument` into the prototype's `ReportState` and saves by turning the difference
 * between the saved and the edited state into one change set (persistence.ts). These tests run that translation against the
 * ReportsApi mock adapter, so they confirm what no live API is needed for: a report saved without edits writes nothing, and the
 * operations of an edit, applied by the port, store what the editor shows.
 */

const tree = (members: PublishedMember[], parentKey: string | null = null): DimMember[] =>
  members
    .filter((m) => m.parentKey === parentKey)
    .map((m) => {
      const children = tree(members, m.key)
      return { key: m.key, label: m.displayName, ...(children.length ? { children } : {}) }
    })

const reportCatalog: ReportCatalog = {
  roles: catalog.roles,
  datasets: new Map(
    catalog.datasets.map((d) => [d.id, { key: d.key, name: d.displayName, measures: [], fields: [], timeFields: [], roles: [] }]),
  ),
  items: new Map(catalog.roles.map((r) => [r.id, tree(publishedMembers[r.dimensionId] ?? [])])),
}

const context: BridgeContext = {
  catalog: reportCatalog,
  model: undefined,
  modelKey: (id) => id,
  modelId: (key) => key,
  unmapped: {},
}

/** Applies one operation through the port, as the server applies it from the change set. */
function apply(api: ReportsApi, id: Id, op: ReportOperation) {
  switch (op.op) {
    case 'updateSettings':
      return api.updateSettings(id, op.patch)
    case 'saveRow':
      return api.saveRow(id, { ...op.row, index: op.index })
    case 'moveRow':
      return api.moveRow(id, op.key, op.to)
    case 'deleteRow':
      return api.deleteRow(id, op.key)
    case 'saveSource':
      return api.saveSource(id, op.input)
    case 'deleteSource':
      return api.deleteSource(id, op.key)
    case 'setSourceVariables':
      return api.setSourceVariables(id, op.sourceKey, op.variables)
    case 'setRowRules':
      return api.setRowRules(id, op.sourceKey, op.rowKey, op.rules)
    case 'setLeftOut':
      return api.setLeftOut(id, op.sourceKey, op.itemKeys)
    case 'saveProfile':
      return api.saveProfile(id, op.profile)
    case 'deleteProfile':
      return api.deleteProfile(id, op.key)
    case 'saveCalculationDimension':
      return api.saveCalculationDimension(id, { ...op.input, originalKey: op.originalKey })
    case 'deleteCalculationDimension':
      return api.deleteCalculationDimension(id, op.key)
    case 'setScenarios':
      return api.setScenarios(id, op.roleIds)
    case 'setVariableScopes':
      return api.setVariableScopes(id, op.scopes)
    case 'setColumns':
      return api.setColumns(id, op.columns)
    case 'saveStyle':
      return api.saveStyle(id, op.style)
    case 'deleteStyle':
      return api.deleteStyle(id, op.key)
  }
}

async function margin() {
  const api = createMockReportsApi()
  const id = (await api.listReports()).find((r) => r.key === 'bank.margin_statement')!.id
  return { api, id, doc: await unwrap(api.getReport(id)) }
}

describe('report http bridge', () => {
  it('writes nothing for a report saved without edits', async () => {
    const api = createMockReportsApi()
    for (const summary of await api.listReports()) {
      const doc = await unwrap(api.getReport(summary.id))
      const state = reportFromDocument(doc, context)
      expect(reportOperations(doc, state, structuredClone(state), context)).toEqual([])
    }
  })

  it('stores the rows, mappings, columns, profiles and settings the editor shows after an edit', async () => {
    const { api, id, doc } = await margin()
    const saved = reportFromDocument(doc, context)
    const edited: ReportState = structuredClone(saved)
    edited.settings.name = 'Bank margin (edited)'
    const total = edited.rows.find((r) => r.key === 'total')!
    total.children.find((r) => r.key === 'LOANS')!.label = 'Loans and leases'
    total.children.splice(2, 0, { key: 'CARDS', label: 'Cards', kind: 'line', hidden: false, children: [] })
    edited.sources.find((s) => s.key === 'loans_by_branch')!.mappings.push({ id: 'm_new', rowKey: 'CARDS', memberKey: 'BR_UNK', factor: 1 })
    edited.sources.find((s) => s.key === 'loans_by_branch')!.leftOut = []
    edited.columns.find((c) => c.key === 'income')!.visible = false
    edited.profiles.push({ key: 'alt', name: 'Alternative', enabled: true, picks: { AVERAGE_BALANCE: 'loans_by_branch' } })

    api.beginChanges()
    for (const op of reportOperations(doc, saved, edited, context)) expect((await apply(api, id, op)).ok).toBe(true)
    const stored = await unwrap(api.getReport(id))
    const reloaded = reportFromDocument(stored, context)

    expect(reloaded.settings).toEqual(edited.settings)
    expect(reloaded.rows).toEqual(edited.rows)
    expect(reloaded.columns).toEqual(edited.columns)
    expect(reloaded.profiles).toEqual(edited.profiles)
    const mappings = (state: ReportState) =>
      state.sources.find((s) => s.key === 'loans_by_branch')!.mappings.map(({ rowKey, memberKey, factor }) => ({ rowKey, memberKey, factor }))
    expect(mappings(reloaded)).toEqual(expect.arrayContaining(mappings(edited)))
    expect(mappings(reloaded)).toHaveLength(mappings(edited).length)
    // A column that follows its metric's label keeps following it.
    expect(stored.columns.find((c) => c.key === 'balance')!.displayName).toBeNull()
  })
})
