import type { FilterNode, FilterOperand, Grouping as ApiGrouping, Id, Periodicity } from '@/lib/api/contract'
import type {
  ReportCalculationDimension,
  ReportColumn,
  ReportDocument,
  ReportOperation,
  ReportProfile,
  ReportRow,
  ReportRowKind,
  ReportSettings as ApiSettings,
  ReportSourceInput,
  ReportSourceRule,
  ReportSourceVariable,
  ReportStyle as ApiStyle,
  ReportVariableScope,
} from '@/lib/reports/contract'
import type { CalcModel } from '@/lib/model/types'
import type { ReportCatalog } from './http-catalog'
import { allRows } from './tree'
import type {
  Binding,
  BreakoutDimension,
  Column,
  Dataset,
  DisplayFormat,
  Filter,
  FilterOp,
  Grouping,
  Mapping,
  ReportSettings,
  ReportState,
  ReportStyle,
  RowKind,
  RowNode,
  Source,
} from './types'

/**
 * Translation between the editor's `ReportState` and the API's `ReportDocument` (blueprint 16). Loading builds the state from the
 * stored document plus what the report reads from other modules; saving turns the difference between the saved and the edited state
 * into one change set. A part the user did not touch keeps its stored value, so what the state cannot express (a filter with `or`, a
 * column that follows its metric's label) survives a save.
 */

/** What translation reads besides the document: the catalog and the report's model. */
export interface BridgeContext {
  catalog: ReportCatalog
  /** The report's model, as the models store holds it. */
  model: CalcModel | undefined
  modelKey: (id: Id) => string
  modelId: (key: string) => Id
  /** Values of each direct source's row key field that are no row key, by source key (the server's coverage). */
  unmapped: Record<string, string[]>
}

const same = (a: unknown, b: unknown) => JSON.stringify(a) === JSON.stringify(b)

/* ───────────────────────── vocabularies ───────────────────────── */

const PERIODICITY: Record<Periodicity, ReportSettings['periodicity']> = { day: 'Day', month: 'Month', quarter: 'Quarter', year: 'Year' }
const periodicityOf = (p: ReportSettings['periodicity']) => p.toLowerCase() as Periodicity

const API_OP: Record<Exclude<FilterOp, 'in'>, 'eq' | 'ne' | 'gt' | 'lt'> = { '=': 'eq', '!=': 'ne', '>': 'gt', '<': 'lt' }
const STATE_OP = Object.fromEntries(Object.entries(API_OP).map(([k, v]) => [v, k])) as Record<string, FilterOp>

/* ───────────────────────── filters ───────────────────────── */

const literal = (value: string): FilterOperand => ({ type: 'literal', value })
const operandId = (o: FilterOperand) => ('id' in o ? o.id : null)
const literalText = (o: FilterOperand) => (o.type === 'literal' ? String(o.value) : null)

/** `field` reads a dataset field; `dimension` a calculation dimension key. */
function filterNode(filters: Filter[], type: 'field' | 'dimension'): FilterNode | null {
  const nodes = filters.map((f): FilterNode => {
    const left: FilterOperand = { type, id: f.field }
    if (f.op === 'in')
      return {
        operator: 'in',
        left,
        values: f.value
          .split(',')
          .map((v) => v.trim())
          .filter(Boolean)
          .map(literal),
      }
    return { operator: API_OP[f.op], left, right: literal(f.value) }
  })
  if (!nodes.length) return null
  return nodes.length === 1 ? nodes[0] : { operator: 'and', operands: nodes }
}

/** The conditions of a filter the editor can show (an `and` of comparisons of one operand with literals), else null. */
function filtersOf(node: FilterNode | null): Filter[] | null {
  if (!node) return []
  const parts = node.operator === 'and' ? node.operands : [node]
  const out: Filter[] = []
  for (const p of parts) {
    if (p.operator === 'in') {
      const field = operandId(p.left)
      const values = p.values.map(literalText)
      if (!field || values.some((v) => v === null)) return null
      out.push({ field, op: 'in', value: values.join(', ') })
    } else if ('right' in p && STATE_OP[p.operator]) {
      const field = operandId(p.left)
      const value = literalText(p.right)
      if (!field || value === null) return null
      out.push({ field, op: STATE_OP[p.operator], value })
    } else return null
  }
  return out
}

/** The filter to store: the stored one while the editor's conditions are unchanged, so a filter the editor cannot show survives. */
const keptFilter = (prev: Filter[] | undefined, next: Filter[], stored: FilterNode | null | undefined, type: 'field' | 'dimension') =>
  prev !== undefined && stored !== undefined && same(prev, next) ? stored : filterNode(next, type)

/* ───────────────────────── groupings ───────────────────────── */

function groupingOf(g: ApiGrouping): Grouping {
  if (g.mode === 'level') return { kind: 'level', level: g.levelKey }
  if (g.mode === 'ancestor') return { kind: 'ancestor', member: g.memberKey }
  if (g.mode === 'depth') return { kind: 'depth', steps: g.depth }
  return { kind: 'member' }
}

/** An incomplete grouping goes as is; the server refuses it with the field to fix. */
function apiGrouping(g: Grouping): ApiGrouping {
  if (g.kind === 'level') return { mode: 'level', levelKey: g.level ?? '' }
  if (g.kind === 'ancestor') return { mode: 'ancestor', memberKey: g.member ?? '' }
  if (g.kind === 'depth') return { mode: 'depth', depth: g.steps ?? 0 }
  return { mode: 'member' }
}

/* ───────────────────────── rows ───────────────────────── */

/** `data` rows are lines; a `group` that shows values a subtotal; a `group` that does not and a `section` are headings (OI-48). */
function rowKindOf(row: ReportRow): RowKind {
  if (row.kind === 'data') return 'line'
  if (row.kind === 'group') return row.showsValues ? 'subtotal' : 'heading'
  return row.kind === 'section' ? 'heading' : 'formula'
}

const isMemberKind = (kind: RowKind) => kind === 'line' || kind === 'subtotal'

/** A stored heading keeps its kind; a new one is a member `group` when it has member rows under it, else a `section` (OI-48). */
function apiRowKind(node: RowNode, stored: ReportRow | undefined): ReportRowKind {
  if (node.kind === 'line') return 'data'
  if (node.kind === 'subtotal') return 'group'
  if (node.kind === 'formula') return 'formula'
  if (stored && (stored.kind === 'group' || stored.kind === 'section')) return stored.kind
  return allRows(node.children).some((c) => isMemberKind(c.kind)) ? 'group' : 'section'
}

function rowTree(rows: ReportRow[]): RowNode[] {
  const nodes = new Map<string, RowNode>()
  const roots: RowNode[] = []
  for (const r of rows) {
    const node: RowNode = {
      key: r.key,
      label: r.label,
      kind: rowKindOf(r),
      hidden: !r.isVisible,
      ...(r.calculatedMeasureKey ? { measureKey: r.calculatedMeasureKey } : {}),
      ...(r.valueColumnKey ? { columnKey: r.valueColumnKey } : {}),
      ...(r.styleKey ? { styleKey: r.styleKey } : {}),
      ...(Object.keys(r.parameters).length ? { parameters: r.parameters } : {}),
      children: [],
    }
    nodes.set(r.key, node)
    if (r.parentKey) nodes.get(r.parentKey)!.children.push(node)
    else roots.push(node)
  }
  return roots
}

interface PlacedRow {
  node: RowNode
  parentKey: string | null
  index: number
}

function placedRows(nodes: RowNode[], parentKey: string | null = null): PlacedRow[] {
  return nodes.flatMap((node, index) => [{ node, parentKey, index }, ...placedRows(node.children, node.key)])
}

function apiRow({ node, parentKey }: PlacedRow, stored: ReportRow | undefined): ReportRow {
  const kind = apiRowKind(node, stored)
  return {
    key: node.key,
    label: node.label,
    kind,
    parentKey,
    isVisible: !node.hidden,
    showsValues: node.kind !== 'heading',
    calculatedMeasureKey: node.measureKey ?? null,
    valueColumnKey: node.columnKey ?? null,
    styleKey: node.styleKey ?? null,
    parameters: node.parameters ?? {},
  }
}

const ownRow = ({ children: _children, ...own }: RowNode) => own

/* ───────────────────────── sources ───────────────────────── */

const roleKeyOf = (ctx: BridgeContext, id: Id) => ctx.catalog.roles.find((r) => r.id === id)?.key ?? id
const roleIdOf = (ctx: BridgeContext, key: string) => ctx.catalog.roles.find((r) => r.key === key)?.id ?? key
const datasetKeyOf = (ctx: BridgeContext, id: Id) => ctx.catalog.datasets.get(id)?.key ?? id
const datasetIdOf = (ctx: BridgeContext, key: string) => [...ctx.catalog.datasets].find(([, d]) => d.key === key)?.[0] ?? key

function bindingOf(v: ReportSourceVariable, index: number): Binding {
  return {
    id: `b${index + 1}`,
    variable: v.variableKey,
    measure: v.measureKey,
    filters: filtersOf(v.filter) ?? [],
    ...(v.missingBehavior ? { missing: v.missingBehavior } : {}),
  }
}

function sourceInput(s: Source, ctx: BridgeContext): ReportSourceInput {
  const bridge = s.method === 'mapped'
  return {
    key: s.key,
    displayName: s.name,
    datasetId: datasetIdOf(ctx, s.dataset),
    timeFieldKey: s.timeField,
    method: bridge ? 'bridge' : 'direct',
    roleId: bridge ? roleIdOf(ctx, s.dimension ?? '') : null,
    fieldKey: bridge ? null : (s.rowKeyField ?? null),
  }
}

const bindingShape = (b: Binding) => ({ variable: b.variable, measure: b.measure, filters: b.filters, missing: b.missing ?? 'default' })

function sourceVariables(prev: Source | undefined, next: Source, stored: ReportDocument['sources'][number] | undefined): ReportSourceVariable[] {
  return next.bindings.map((b) => {
    const before = prev?.bindings.find((x) => x.variable === b.variable)
    const storedFilter = stored?.variables.find((v) => v.variableKey === b.variable)?.filter
    return {
      variableKey: b.variable,
      measureKey: b.measure,
      filter: keptFilter(before?.filters, b.filters, storedFilter, 'field'),
      missingBehavior: b.missing && b.missing !== 'default' ? b.missing : null,
    }
  })
}

/** A mapping of a folder takes the items under it; a mapping of a leaf takes the item. */
function rulesByRow(source: Source, folders: Set<string>): Map<string, ReportSourceRule[]> {
  const out = new Map<string, ReportSourceRule[]>()
  for (const m of source.mappings) {
    const rules = out.get(m.rowKey) ?? []
    rules.push({ itemKey: m.memberKey, selection: folders.has(m.memberKey) ? 'descendants' : 'member', factor: m.factor })
    out.set(m.rowKey, rules)
  }
  return out
}

const sortedRules = (rules: ReportSourceRule[] | undefined) =>
  [...(rules ?? [])].sort((a, b) => a.itemKey.localeCompare(b.itemKey) || a.factor - b.factor)

function folderKeys(state: ReportState) {
  const out = new Set<string>()
  const walk = (members: ReportState['accounts']) => {
    for (const m of members)
      if (m.children) {
        out.add(m.key)
        walk(m.children)
      }
  }
  walk(state.accounts)
  return out
}

/** The item tree a report's bridge sources read: the first bridge source's role, else the first role. */
function accountsOf(doc: ReportDocument, ctx: BridgeContext) {
  const roleId = doc.sources.find((s) => s.method === 'bridge')?.roleId ?? ctx.catalog.roles[0]?.id
  return roleId ? (ctx.catalog.items.get(roleId) ?? []) : []
}

/** Direct sources match row keys against their field's values: those the server reports as unmapped, and the member row keys. */
function datasetsOf(doc: ReportDocument, ctx: BridgeContext): Dataset[] {
  const memberKeys = doc.rows.filter((r) => r.kind === 'data' || r.kind === 'group').map((r) => r.key)
  return [...ctx.catalog.datasets].map(([id, d]) => {
    const direct = doc.sources.filter((s) => s.method === 'direct' && s.datasetId === id && s.fieldKey)
    if (!direct.length) return d
    const fieldValues = Object.fromEntries(direct.map((s) => [s.fieldKey!, [...new Set([...(ctx.unmapped[s.key] ?? []), ...memberKeys])]]))
    return { ...d, fieldValues: { ...d.fieldValues, ...fieldValues } }
  })
}

/* ───────────────────────── columns and styles ───────────────────────── */

function metricOf(ctx: BridgeContext, key: string) {
  return ctx.model?.variables.find((v) => v.key === key) ?? ctx.model?.measures.find((m) => m.key === key)
}

function columnOf(c: ReportColumn, ctx: BridgeContext): Column {
  const metric = metricOf(ctx, c.metricKey)
  return {
    key: c.key,
    label: c.displayName ?? metric?.name ?? c.metricKey,
    kind: c.valueKind,
    metric: c.metricKey,
    format: (c.displayFormat ?? metric?.format ?? 'number') as DisplayFormat,
    visible: c.isVisible,
  }
}

function apiColumn(c: Column, prev: Column | undefined, stored: ReportColumn | undefined): ReportColumn {
  const unchanged = prev && stored && prev.metric === c.metric
  return {
    key: c.key,
    displayName: unchanged && prev.label === c.label ? stored.displayName : c.label,
    valueKind: c.kind,
    metricKey: c.metric,
    displayFormat: unchanged && prev.format === c.format ? stored.displayFormat : c.format,
    isVisible: c.visible,
  }
}

function styleOf(s: ApiStyle): ReportStyle {
  return {
    key: s.key,
    name: s.displayName,
    defaultRole: s.defaultRole,
    ...(s.fontFamily !== null ? { fontFamily: s.fontFamily } : {}),
    ...(s.fontSize !== null ? { fontSize: s.fontSize } : {}),
    ...(s.fontWeight !== null ? { fontWeight: s.fontWeight } : {}),
    italic: s.isItalic,
    ...(s.textColor !== null ? { textColor: s.textColor } : {}),
    ...(s.backgroundColor !== null ? { backgroundColor: s.backgroundColor } : {}),
    ...(s.borderTop !== null ? { borderTop: s.borderTop } : {}),
    ...(s.borderBottom !== null ? { borderBottom: s.borderBottom } : {}),
    ...(s.indent !== null ? { indent: s.indent } : {}),
  }
}

function apiStyle(s: ReportStyle): ApiStyle {
  return {
    key: s.key,
    displayName: s.name,
    defaultRole: s.defaultRole ?? null,
    fontFamily: s.fontFamily ?? null,
    fontSize: s.fontSize ?? null,
    fontWeight: s.fontWeight ?? null,
    isItalic: s.italic ?? false,
    textColor: s.textColor ?? null,
    backgroundColor: s.backgroundColor ?? null,
    borderTop: s.borderTop ?? null,
    borderBottom: s.borderBottom ?? null,
    indent: s.indent ?? null,
  }
}

/* ───────────────────────── load ───────────────────────── */

export function reportFromDocument(doc: ReportDocument, ctx: BridgeContext): ReportState {
  const s = doc.settings
  return {
    settings: {
      key: doc.key,
      name: s.displayName,
      type: s.reportType,
      model: ctx.modelKey(doc.modelId),
      currency: ctx.model?.currency ?? '',
      periodicity: PERIODICITY[s.periodicity ?? 'month'],
      layout: s.layout === 'list' ? 'List' : 'Statement',
      subtotals: s.subtotalPosition === 'below' ? 'Below' : 'Above',
      defaultProfile: s.defaultProfileKey ?? '',
      defaultBreakout: s.defaultBreakout,
      defaultFilters: filtersOf(s.defaultFilter) ?? [],
    },
    enabled: doc.isEnabled,
    rows: rowTree(doc.rows),
    variables: ctx.model?.variables.map((v) => ({ key: v.key, name: v.name })) ?? [],
    measures: ctx.model?.measures.map((m) => ({ key: m.key, name: m.name, formula: m.formula })) ?? [],
    columns: doc.columns.map((c) => columnOf(c, ctx)),
    accounts: accountsOf(doc, ctx),
    datasets: datasetsOf(doc, ctx),
    sources: doc.sources.map((src) => {
      const bridge = src.method === 'bridge'
      let n = 0
      return {
        key: src.key,
        name: src.displayName,
        dataset: datasetKeyOf(ctx, src.datasetId),
        method: bridge ? 'mapped' : 'matched',
        ...(bridge ? { dimension: roleKeyOf(ctx, src.roleId!) } : { rowKeyField: src.fieldKey! }),
        timeField: src.timeFieldKey,
        bindings: src.variables.map(bindingOf),
        mappings: Object.entries(src.rules).flatMap(([rowKey, rules]) =>
          rules.map((r): Mapping => ({ id: `${src.key}_r${++n}`, rowKey, memberKey: r.itemKey, factor: r.factor })),
        ),
        leftOut: src.leftOut,
      }
    }),
    profiles: doc.profiles.map((p) => ({ key: p.key, name: p.displayName, enabled: p.isEnabled, picks: p.picks })),
    breakoutDims: doc.calculationDimensions.map((d) => ({
      key: d.key,
      name: d.displayName,
      aliasKey: d.key,
      role: roleKeyOf(ctx, d.roleId),
      grouping: groupingOf(d.grouping),
      groupable: d.isGroupable,
      filterable: d.isFilterable,
      enabled: d.isEnabled,
    })),
    rowScopes: doc.variableScopes.map((v, i) => ({
      id: `scope_${i + 1}`,
      variable: v.variableKey,
      rowKey: v.scopeMemberKey,
      scope: v.selectionScope === 'exact' ? 'row' : 'subtree',
    })),
    reportScenarios: doc.scenarios.map((sc) => ({ role: roleKeyOf(ctx, sc.roleId), name: ctx.catalog.roles.find((r) => r.id === sc.roleId)?.displayName ?? sc.roleId })),
    scenarios: doc.scenarios.length ? (ctx.catalog.items.get(doc.scenarios[0].roleId) ?? []) : [],
    styles: doc.styles.map(styleOf),
  }
}

/* ───────────────────────── save ───────────────────────── */

/** Saves of new or changed items and deletes of removed ones, matched by `id`. */
function itemDiff<T>(prev: T[], next: T[], id: (item: T) => string) {
  const before = new Map(prev.map((i) => [id(i), i]))
  const after = new Set(next.map(id))
  return {
    saved: next.filter((i) => !same(before.get(id(i)), i)).map((i) => ({ item: i, before: before.get(id(i)) })),
    removed: prev.filter((i) => !after.has(id(i))),
  }
}

function settingsPatch(prev: ReportState, next: ReportState, stored: ApiSettings): Partial<ApiSettings> {
  const a = prev.settings
  const b = next.settings
  const aliasOf = (state: ReportState) => (key: string) => state.breakoutDims.find((d) => d.key === key)?.aliasKey ?? key
  const breakout = (state: ReportState) => state.settings.defaultBreakout.map(aliasOf(state))
  const filters = (state: ReportState) => state.settings.defaultFilters.map((f) => ({ ...f, field: aliasOf(state)(f.field) }))
  const patch: Partial<ApiSettings> = {}
  if (a.name !== b.name) patch.displayName = b.name
  if (a.type !== b.type) patch.reportType = b.type
  if (a.subtotals !== b.subtotals) patch.subtotalPosition = b.subtotals === 'Below' ? 'below' : 'above'
  if (a.defaultProfile !== b.defaultProfile) patch.defaultProfileKey = b.defaultProfile || null
  if (a.periodicity !== b.periodicity) patch.periodicity = periodicityOf(b.periodicity)
  if (a.layout !== b.layout) patch.layout = b.layout === 'List' ? 'list' : 'statement'
  if (!same(breakout(prev), breakout(next))) patch.defaultBreakout = breakout(next)
  if (!same(filters(prev), filters(next))) patch.defaultFilter = keptFilter(filters(prev), filters(next), stored.defaultFilter, 'dimension')
  return patch
}

function rowOperations(stored: ReportDocument, prev: ReportState, next: ReportState): { deletes: ReportOperation[]; saves: ReportOperation[] } {
  const before = new Map(placedRows(prev.rows).map((p) => [p.node.key, p]))
  const after = placedRows(next.rows)
  const afterKeys = new Set(after.map((p) => p.node.key))
  const siblings = (rows: PlacedRow[], parentKey: string | null) => rows.filter((p) => p.parentKey === parentKey).map((p) => p.node.key)
  const reordered = new Set(
    [...new Set(after.map((p) => p.parentKey))].filter((parent) => !same(siblings([...before.values()], parent), siblings(after, parent))),
  )
  const changed = (p: PlacedRow) => {
    const b = before.get(p.node.key)
    return !b || b.parentKey !== p.parentKey || reordered.has(p.parentKey) || !same(ownRow(b.node), ownRow(p.node))
  }
  return {
    deletes: [...before.keys()].filter((key) => !afterKeys.has(key)).map((key) => ({ op: 'deleteRow', key })),
    saves: after.filter(changed).map((p) => ({ op: 'saveRow', row: apiRow(p, stored.rows.find((r) => r.key === p.node.key)), index: p.index })),
  }
}

function sourceOperations(stored: ReportDocument, prev: ReportState, next: ReportState, ctx: BridgeContext) {
  const { saved, removed } = itemDiff(prev.sources, next.sources, (s) => s.key)
  const rowKeys = new Set(allRows(next.rows).map((r) => r.key))
  const prevFolders = folderKeys(prev)
  const nextFolders = folderKeys(next)
  const inputs: ReportOperation[] = []
  const content: ReportOperation[] = []
  for (const { item: s, before } of saved) {
    const storedSource = stored.sources.find((x) => x.key === s.key)
    if (!before || !same(sourceInput(before, ctx), sourceInput(s, ctx))) inputs.push({ op: 'saveSource', input: sourceInput(s, ctx) })
    if (!same(before?.bindings.map(bindingShape), s.bindings.map(bindingShape)))
      content.push({ op: 'setSourceVariables', sourceKey: s.key, variables: sourceVariables(before, s, storedSource) })
    if (s.method !== 'mapped') continue
    const rulesBefore = before ? rulesByRow(before, prevFolders) : new Map<string, ReportSourceRule[]>()
    const rulesAfter = rulesByRow(s, nextFolders)
    for (const rowKey of new Set([...rulesBefore.keys(), ...rulesAfter.keys()])) {
      if (!rowKeys.has(rowKey) || same(sortedRules(rulesBefore.get(rowKey)), sortedRules(rulesAfter.get(rowKey)))) continue
      content.push({ op: 'setRowRules', sourceKey: s.key, rowKey, rules: rulesAfter.get(rowKey) ?? [] })
    }
    if (!same([...(before?.leftOut ?? [])].sort(), [...s.leftOut].sort())) content.push({ op: 'setLeftOut', sourceKey: s.key, itemKeys: s.leftOut })
  }
  return { inputs, content, deletes: removed.map((s): ReportOperation => ({ op: 'deleteSource', key: s.key })) }
}

function dimensionInput(d: BreakoutDimension, ctx: BridgeContext): ReportCalculationDimension {
  return {
    key: d.aliasKey,
    displayName: d.name,
    roleId: roleIdOf(ctx, d.role),
    grouping: apiGrouping(d.grouping),
    isGroupable: d.groupable,
    isFilterable: d.filterable,
    isEnabled: d.enabled,
  }
}

const profileInput = (p: ReportState['profiles'][number]): ReportProfile => ({ key: p.key, displayName: p.name, isEnabled: p.enabled, picks: p.picks })

const scopeInput = (s: ReportState['rowScopes'][number]): ReportVariableScope => ({
  variableKey: s.variable,
  scopeMemberKey: s.rowKey,
  selectionScope: s.scope === 'row' ? 'exact' : 'descendants',
})

/**
 * The change set that turns the saved state `prev` into `next`, in an order each operation can apply in: what rows and settings
 * name (styles, dimensions, sources, columns) is saved before them, deletes of what they named come after them.
 */
export function reportOperations(stored: ReportDocument, prev: ReportState, next: ReportState, ctx: BridgeContext): ReportOperation[] {
  const styles = itemDiff(prev.styles, next.styles, (s) => s.key)
  const dimensions = itemDiff(prev.breakoutDims, next.breakoutDims, (d) => d.key)
  const profiles = itemDiff(prev.profiles, next.profiles, (p) => p.key)
  const sources = sourceOperations(stored, prev, next, ctx)
  const rows = rowOperations(stored, prev, next)
  const patch = settingsPatch(prev, next, stored.settings)
  const prevColumns = new Map(prev.columns.map((c) => [c.key, c]))
  const scenarios = (state: ReportState) => state.reportScenarios.map((s) => roleIdOf(ctx, s.role))

  return [
    ...styles.saved.map(({ item }): ReportOperation => ({ op: 'saveStyle', style: apiStyle(item) })),
    ...dimensions.saved.map(({ item, before }): ReportOperation => ({
      op: 'saveCalculationDimension',
      input: dimensionInput(item, ctx),
      ...(before ? { originalKey: before.aliasKey } : {}),
    })),
    ...sources.inputs,
    ...(same(prev.columns, next.columns)
      ? []
      : [{ op: 'setColumns' as const, columns: next.columns.map((c) => apiColumn(c, prevColumns.get(c.key), stored.columns.find((s) => s.key === c.key))) }]),
    ...rows.deletes,
    ...rows.saves,
    ...sources.content,
    ...profiles.saved.map(({ item }): ReportOperation => ({ op: 'saveProfile', profile: profileInput(item) })),
    ...(same(scenarios(prev), scenarios(next)) ? [] : [{ op: 'setScenarios' as const, roleIds: scenarios(next) }]),
    ...(same(prev.rowScopes.map(scopeInput), next.rowScopes.map(scopeInput)) ? [] : [{ op: 'setVariableScopes' as const, scopes: next.rowScopes.map(scopeInput) }]),
    ...(Object.keys(patch).length ? [{ op: 'updateSettings' as const, patch }] : []),
    ...profiles.removed.map((p): ReportOperation => ({ op: 'deleteProfile', key: p.key })),
    ...dimensions.removed.map((d): ReportOperation => ({ op: 'deleteCalculationDimension', key: d.aliasKey })),
    ...sources.deletes,
    ...styles.removed.map((s): ReportOperation => ({ op: 'deleteStyle', key: s.key })),
  ]
}

/** Settings the API has no field for; a change to them stays in the browser (see persistence.ts). */
export function clientOnlyChanges(prev: ReportState, next: ReportState): string[] {
  const out: string[] = []
  if (prev.settings.key !== next.settings.key) out.push('report key')
  if (prev.settings.model !== next.settings.model) out.push('calculation model')
  if (prev.settings.currency !== next.settings.currency) out.push('currency')
  return out
}
