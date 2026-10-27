import { sampleReports } from '@/lib/report/mockData'
import type { BreakoutDimension, DimMember, Filter, ReportState, RowNode, Source } from '@/lib/report/types'
import type { Id, ReportDocument, FilterNode, Grouping, ReportRow, ReportRowKind, ReportSource, ReportSourceRule } from '../contract'

/** An item of a role's dimension that a bridge rule can name. */
export interface ItemNode {
  key: string
  children?: ItemNode[]
}

export interface Db {
  reports: ReportDocument[]
  models: { id: Id; key: string }[]
  /** Members of each role's dimension, by role id. */
  items: Record<Id, ItemNode[]>
  /** Values of each dataset field, by dataset id then field key. */
  fieldValues: Record<Id, Record<string, string[]>>
}

export const roleIdOf = (roleKey: string) => `role_${roleKey}`
export const datasetIdOf = (datasetKey: string) => `dataset_${datasetKey}`

/** Seeds the mock from the prototype's `ReportState` samples (lib/report/mockData.ts). */
export function createSeed(): Db {
  const models = [...new Set(sampleReports.map((s) => s.settings.model))].map((key, i) => ({ id: `model_${i + 1}`, key }))
  const datasets = sampleReports[0].datasets
  const roleKeys = new Set(datasets.flatMap((d) => d.roles))
  return {
    reports: sampleReports.map((state, i) => toDocument(state, `report_${i + 1}`, models.find((m) => m.key === state.settings.model)!.id)),
    models,
    items: Object.fromEntries([...roleKeys].map((role) => [roleIdOf(role), role === 'account' ? toItems(sampleReports[0].accounts) : []])),
    fieldValues: Object.fromEntries(datasets.map((d) => [datasetIdOf(d.key), Object.fromEntries(d.fields.map((f) => [f, d.fieldValues?.[f] ?? []]))])),
  }
}

function toItems(members: DimMember[]): ItemNode[] {
  return members.map((m) => ({ key: m.key, children: m.children && toItems(m.children) }))
}

function toDocument(state: ReportState, id: Id, modelId: Id): ReportDocument {
  const s = state.settings
  const folders = new Set(flattenItems(toItems(state.accounts)).filter((i) => i.children?.length).map((i) => i.key))
  return {
    id,
    key: s.key,
    isEnabled: state.enabled,
    modelId,
    reportDimensionId: `${id}_dimension`,
    reportDimensionVersionId: `${id}_dimension_v1`,
    settings: {
      displayName: s.name,
      reportType: s.type,
      subtotalPosition: s.subtotals === 'Above' ? 'above' : 'below',
      defaultProfileKey: s.defaultProfile,
      periodicity: s.periodicity.toLowerCase() as ReportDocument['settings']['periodicity'],
      layout: s.layout === 'Statement' ? 'statement' : 'list',
      defaultBreakout: s.defaultBreakout,
      defaultFilter: toFilter(s.defaultFilters),
    },
    rows: toRows(state.rows, null),
    sources: state.sources.map((source) => toSource(source, id, folders)),
    profiles: state.profiles.map((p) => ({ key: p.key, displayName: p.name, isEnabled: p.enabled, picks: p.picks })),
    calculationDimensions: state.breakoutDims.map(toCalculationDimension),
    scenarios: state.reportScenarios.map((sc) => ({ roleId: roleIdOf(sc.role) })),
    variableScopes: state.rowScopes.map((sc) => ({ variableKey: sc.variable, scopeMemberKey: sc.rowKey, selectionScope: sc.scope === 'row' ? 'exact' : 'descendants' })),
    columns: state.columns.map((c) => ({ key: c.key, displayName: c.label, valueKind: c.kind, metricKey: c.metric, displayFormat: c.format, isVisible: c.visible })),
    styles: state.styles.map((st) => ({
      key: st.key,
      displayName: st.name,
      defaultRole: st.defaultRole ?? null,
      fontFamily: st.fontFamily ?? null,
      fontSize: st.fontSize ?? null,
      fontWeight: st.fontWeight ?? null,
      isItalic: st.italic ?? false,
      textColor: st.textColor ?? null,
      backgroundColor: st.backgroundColor ?? null,
      borderTop: st.borderTop ?? null,
      borderBottom: st.borderBottom ?? null,
      indent: st.indent ?? null,
    })),
  }
}

export function flattenItems(items: ItemNode[]): ItemNode[] {
  return items.flatMap((i) => [i, ...flattenItems(i.children ?? [])])
}

/** OI-48: a heading over member rows is a `group` that shows no values; any other heading is a `section`. */
function kindOf(node: RowNode): ReportRowKind {
  const hasMembers = node.children.some((c) => c.kind !== 'formula')
  switch (node.kind) {
    case 'formula':
      return 'formula'
    case 'heading':
      return hasMembers ? 'group' : 'section'
    default:
      return node.children.length ? 'group' : 'data'
  }
}

function toRows(nodes: RowNode[], parentKey: string | null): ReportRow[] {
  return nodes.flatMap((n) => {
    const kind = kindOf(n)
    const row: ReportRow = {
      key: n.key,
      label: n.label,
      kind,
      parentKey,
      isVisible: !n.hidden,
      showsValues: n.kind !== 'heading' && kind !== 'section',
      calculatedMeasureKey: n.measureKey ?? null,
      valueColumnKey: n.columnKey ?? null,
      styleKey: n.styleKey ?? null,
      parameters: n.parameters ?? {},
    }
    return [row, ...toRows(n.children, n.key)]
  })
}

/** OI-52: the multiplier is dropped; the factor is the only number a rule carries. */
function toSource(source: Source, reportId: Id, folders: Set<string>): ReportSource {
  const bridge = source.method === 'mapped'
  const rules: Record<string, ReportSourceRule[]> = {}
  for (const m of source.mappings) {
    ;(rules[m.rowKey] ??= []).push({ itemKey: m.memberKey, selection: folders.has(m.memberKey) ? 'descendants' : 'member', factor: m.factor })
  }
  return {
    key: source.key,
    displayName: source.name,
    datasetId: datasetIdOf(source.dataset),
    timeFieldKey: source.timeField,
    method: bridge ? 'bridge' : 'direct',
    roleId: bridge ? roleIdOf(source.dimension!) : null,
    fieldKey: source.rowKeyField ?? null,
    bridgeId: bridge ? `${reportId}_bridge_${source.key}` : null,
    variables: source.bindings.map((b) => ({
      variableKey: b.variable,
      measureKey: b.measure,
      filter: toFilter(b.filters),
      missingBehavior: b.missing === 'null' || b.missing === 'zero' ? b.missing : null,
    })),
    rules,
    leftOut: source.leftOut,
  }
}

const COMPARISON = { '=': 'eq', '!=': 'ne', '>': 'gt', '<': 'lt' } as const

function toFilter(filters: Filter[]): FilterNode | null {
  const nodes = filters.map((f): FilterNode => {
    const left = { type: 'field' as const, id: f.field }
    if (f.op === 'in') return { operator: 'in', left, values: f.value.split(',').map((v) => ({ type: 'literal', value: v.trim() })) }
    return { operator: COMPARISON[f.op], left, right: { type: 'literal', value: f.value } }
  })
  if (nodes.length === 0) return null
  return nodes.length === 1 ? nodes[0] : { operator: 'and', operands: nodes }
}

function toGrouping(g: BreakoutDimension['grouping']): Grouping {
  switch (g.kind) {
    case 'member':
      return { mode: 'member' }
    case 'level':
      return { mode: 'level', levelKey: g.level! }
    case 'ancestor':
      return { mode: 'ancestor', memberKey: g.member! }
    case 'depth':
      return { mode: 'depth', depth: g.steps! }
  }
}

function toCalculationDimension(d: BreakoutDimension) {
  return {
    key: d.aliasKey,
    displayName: d.name,
    roleId: roleIdOf(d.role),
    grouping: toGrouping(d.grouping),
    isGroupable: d.groupable,
    isFilterable: d.filterable,
    isEnabled: d.enabled,
  }
}
