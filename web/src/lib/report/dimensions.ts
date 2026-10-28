import type { BreakoutDimension, DimMember, Grouping, GroupingKind, ReportState, ScenarioMember, Source, UnfitHandling } from './types'

export interface DimNode {
  key: string
  label: string
  level?: string
  children?: DimNode[]
}

export interface DimLevel {
  key: string
  label: string
}

/** A shared dimension, as read from its published version. */
export interface Dimension {
  key: string
  name: string
  version: number
  levels: DimLevel[]
  members: DimNode[]
}

interface IndexedNode {
  node: DimNode
  parent?: string
  depth: number
  order: number
}

const BRANCH: Dimension = {
  key: 'branch',
  name: 'Branch',
  version: 1,
  levels: [
    { key: 'root', label: 'Root' },
    { key: 'region', label: 'Region' },
    { key: 'branch', label: 'Branch' },
  ],
  members: [
    {
      key: 'ALL',
      label: 'All branches',
      level: 'root',
      children: [
        {
          key: 'NORTH',
          label: 'North',
          level: 'region',
          children: [
            { key: 'BR01', label: 'Central', level: 'branch' },
            { key: 'BR02', label: 'Lakeside', level: 'branch' },
          ],
        },
        {
          key: 'SOUTH',
          label: 'South',
          level: 'region',
          children: [
            { key: 'BR03', label: 'Central', level: 'branch' },
            { key: 'BR05', label: 'No facts', level: 'branch' },
          ],
        },
        { key: 'BR04', label: 'Direct-to-root branch', level: 'branch' },
      ],
    },
    { key: 'UNKNOWN', label: 'Unknown', level: 'branch' },
  ],
}

const OFFICER: Dimension = {
  key: 'officer',
  name: 'Officer',
  version: 3,
  levels: [],
  members: [
    { key: 'OF01', label: 'A. Okafor' },
    { key: 'OF02', label: 'M. Lindqvist' },
    { key: 'OF03', label: 'R. Patel' },
    { key: 'OF04', label: 'S. Moreau' },
    { key: 'OF05', label: 'J. Tanaka' },
  ],
}

function accountDimension(accounts: DimMember[]): Dimension {
  const toNode = (m: DimMember, depth: number): DimNode => ({
    key: m.key,
    label: m.label,
    level: depth === 0 ? 'category' : 'account',
    children: m.children?.map((c) => toNode(c, depth + 1)),
  })
  return {
    key: 'account',
    name: 'Account',
    version: 2,
    levels: [
      { key: 'root', label: 'Root' },
      { key: 'category', label: 'Category' },
      { key: 'account', label: 'Account' },
    ],
    members: [{ key: 'ALL_ACCOUNTS', label: 'All accounts', level: 'root', children: accounts.map((a) => toNode(a, 0)) }],
  }
}

const RATE_SHOCK: Dimension = {
  key: 'rateShock',
  name: 'RateShock',
  version: 1,
  levels: [],
  members: [
    { key: 'BASE', label: 'Base rates' },
    { key: 'UP200', label: '+200 bp' },
    { key: 'DN100', label: '−100 bp' },
  ],
}

function scenarioDimension(members: ScenarioMember[]): Dimension {
  const toNode = (m: ScenarioMember): DimNode => ({ key: m.key, label: m.label, children: m.children?.map(toNode) })
  return { key: 'scenario', name: 'Scenario', version: 1, levels: [], members: members.map(toNode) }
}

export interface RoleInfo {
  role: string
  name: string
  dimension: string
  /** Scenario dimensions hold alternatives a run picks one of. */
  scenario?: boolean
  /** Plural noun for the role's members, used in sentences. */
  memberNoun?: string
}

export const ROLES: RoleInfo[] = [
  { role: 'branch', name: 'Branch', dimension: 'branch' },
  { role: 'originationBranch', name: 'Origination branch', dimension: 'branch' },
  { role: 'account', name: 'Account', dimension: 'account' },
  { role: 'officer', name: 'Loan officer', dimension: 'officer' },
  { role: 'planVersion', name: 'Plan version', dimension: 'scenario', scenario: true, memberNoun: 'versions' },
  { role: 'rateShock', name: 'Rate shock', dimension: 'rateShock', scenario: true, memberNoun: 'rate shocks' },
]

export const SCENARIO_ROLES = ROLES.filter((r) => r.scenario)

/** Roles a bridge source can read its items from. */
export const SOURCE_DIMENSION_ROLES = [
  { value: 'account', label: 'Account', hint: 'account' },
  { value: 'product', label: 'Product', hint: 'product' },
  { value: 'costCenter', label: 'Cost center', hint: 'costCenter' },
]

/** The published dimension of each role, by role key; filled from the tenant in http mode (http-catalog.ts), empty in mock mode. */
export const PUBLISHED_DIMENSIONS = new Map<string, Dimension>()

export function roleInfo(role: string) {
  return ROLES.find((r) => r.role === role)
}

/** Alias and alias key a dimension falls back to when none is given. */
export function roleDefaults(role: string) {
  const r = ROLES.find((x) => x.role === role)
  return { name: r?.name ?? '', aliasKey: r?.role ?? '' }
}

export function dimensionForRole(state: ReportState, role: string): Dimension | undefined {
  const published = PUBLISHED_DIMENSIONS.get(role)
  if (published) return published
  const key = ROLES.find((r) => r.role === role)?.dimension
  if (key === 'branch') return BRANCH
  if (key === 'officer') return OFFICER
  if (key === 'account') return accountDimension(state.accounts)
  if (key === 'scenario') return scenarioDimension(state.scenarios)
  if (key === 'rateShock') return RATE_SHOCK
  return undefined
}

export function versionLabel(dim: Dimension) {
  return `${dim.name} · v${dim.version} published`
}

export function isFlat(dim: Dimension) {
  return dim.levels.length === 0 && dim.members.every((m) => !m.children?.length)
}

export function indexDimension(dim: Dimension) {
  const index = new Map<string, IndexedNode>()
  let order = 0
  const walk = (nodes: DimNode[], parent: string | undefined, depth: number) => {
    for (const node of nodes) {
      index.set(node.key, { node, parent, depth, order: order++ })
      if (node.children) walk(node.children, node.key, depth + 1)
    }
  }
  walk(dim.members, undefined, 0)
  return index
}

/** Members that hold data: the leaves, including Unknown. */
export function dataMembers(dim: Dimension) {
  return [...indexDimension(dim).values()].filter((n) => !n.node.children?.length).map((n) => n.node)
}

/** Members sharing a label also show their key: "Central (BR01)". */
export function memberLabeler(dim: Dimension) {
  const counts = new Map<string, number>()
  for (const { node } of indexDimension(dim).values()) counts.set(node.label, (counts.get(node.label) ?? 0) + 1)
  return (node: DimNode) => ((counts.get(node.label) ?? 0) > 1 ? `${node.label} (${node.key})` : node.label)
}

/** The deepest path from a top-level member, counted in steps. */
export function maxDepth(dim: Dimension) {
  return Math.max(0, ...[...indexDimension(dim).values()].map((n) => n.depth))
}

/** Leaves (other than top-level ones like Unknown) sit at different depths. */
export function raggedness(dim: Dimension) {
  const index = indexDimension(dim)
  const leaves = [...index.values()].filter((n) => !n.node.children?.length && n.depth > 0)
  const depths = new Set(leaves.map((n) => n.depth))
  if (depths.size < 2) return null
  const deepest = Math.max(...depths)
  const shallow = leaves.filter((n) => n.depth < deepest).map((n) => n.node.key)
  return { shallow }
}

export function levelLabel(dim: Dimension, key: string | null) {
  return dim.levels.find((l) => l.key === key)?.label ?? key ?? ''
}

function stepsText(n: number) {
  return `${n} ${n === 1 ? 'step' : 'steps'} up`
}

export const GROUPING_OPTIONS: { kind: GroupingKind; title: string; description: string }[] = [
  { kind: 'member', title: 'Each member', description: 'One row per member: every branch on its own.' },
  { kind: 'level', title: 'At a level', description: 'Roll each member up to its ancestor at a level.' },
  { kind: 'ancestor', title: 'Under one member', description: 'Fold one part of the tree into a single row.' },
  { kind: 'depth', title: 'Steps up', description: 'Replace each member with its ancestor a number of steps up.' },
]

export function emptyGrouping(kind: GroupingKind): Grouping {
  if (kind === 'level') return { kind, level: null }
  if (kind === 'ancestor') return { kind, member: null }
  if (kind === 'depth') return { kind, steps: null }
  return { kind: 'member' }
}

export function storedForm(g: Grouping) {
  if (g.kind === 'level') return `level:${g.level ?? ''}`
  if (g.kind === 'ancestor') return `ancestor:${g.member ?? ''}`
  if (g.kind === 'depth') return `depth:${g.steps ?? ''}`
  return 'member'
}

export function storedFormOf(kind: GroupingKind) {
  return kind === 'member' ? 'member' : kind
}

export function groupingSummary(g: Grouping, dim?: Dimension) {
  if (g.kind === 'level') return g.level ? `Level: ${dim ? levelLabel(dim, g.level) : g.level}` : 'Level: not chosen'
  if (g.kind === 'ancestor') {
    if (!g.member) return 'Under: not chosen'
    const node = dim ? indexDimension(dim).get(g.member)?.node : undefined
    return `Under: ${node?.label ?? g.member}`
  }
  if (g.kind === 'depth') return g.steps == null ? 'Steps up: not chosen' : stepsText(g.steps)
  return 'Each member'
}

export function fieldError(g: Grouping) {
  if (g.kind === 'level' && !g.level) return 'Choose a level.'
  if (g.kind === 'ancestor' && !g.member) return 'Choose a member.'
  if (g.kind === 'depth' && (g.steps == null || g.steps < 0)) return 'Enter 0 or more steps.'
  return null
}

export interface MappingRow {
  member: DimNode
  label: string
  group: string
  unfit: boolean
}

export interface GroupRow {
  key: string
  label: string
  count: number
  empty: boolean
}

export interface GroupingPreview {
  mapping: MappingRow[]
  groups: GroupRow[]
  emptyLabel: string | null
}

/** Applies a grouping to the dimension's published members. Null when the grouping is incomplete. */
export function previewGrouping(dim: Dimension, g: Grouping): GroupingPreview | null {
  if (fieldError(g)) return null
  const index = indexDimension(dim)
  const label = memberLabeler(dim)
  const ancestors = (key: string) => {
    const chain: IndexedNode[] = []
    let cur = index.get(key)
    while (cur) {
      chain.push(cur)
      cur = cur.parent ? index.get(cur.parent) : undefined
    }
    return chain
  }

  let emptyLabel: string | null = null
  const groupOf = (key: string): IndexedNode | null => {
    const chain = ancestors(key)
    switch (g.kind) {
      case 'member':
        return chain[0]
      case 'level':
        emptyLabel = `(No ${levelLabel(dim, g.level).toLowerCase()})`
        return chain.find((n) => n.node.level === g.level) ?? null
      case 'ancestor': {
        const target = index.get(g.member!)
        emptyLabel = `(Not under ${target ? label(target.node) : g.member})`
        return chain.find((n) => n.node.key === g.member) ?? null
      }
      case 'depth':
        emptyLabel = `(No ancestor ${stepsText(g.steps!)})`
        return chain[g.steps!] ?? null
    }
  }

  const groups = new Map<string, GroupRow & { order: number }>()
  let emptyCount = 0
  const mapping = dataMembers(dim).map((member) => {
    const group = groupOf(member.key)
    if (group) {
      const existing = groups.get(group.node.key)
      if (existing) existing.count++
      else groups.set(group.node.key, { key: group.node.key, label: label(group.node), count: 1, empty: false, order: group.order })
    } else emptyCount++
    return { member, label: label(member), group: group ? label(group.node) : emptyLabel!, unfit: !group }
  })

  const ordered: GroupRow[] = [...groups.values()].sort((a, b) => a.order - b.order).map(({ order: _order, ...rest }) => rest)
  if (emptyCount) ordered.push({ key: '__empty__', label: emptyLabel!, count: emptyCount, empty: true })
  return { mapping, groups: ordered, emptyLabel: emptyCount ? emptyLabel : null }
}

/** Groups a filter on this dimension compares, for its value picker. */
export function filterValues(state: ReportState, d: BreakoutDimension) {
  const dim = dimensionForRole(state, d.role)
  if (!dim) return undefined
  return previewGrouping(dim, d.grouping)
    ?.groups.filter((g) => !g.empty)
    .map((g) => ({ value: g.key, label: g.label }))
}

export const UNFIT_LABEL: Record<UnfitHandling, string> = {
  empty: 'Groups them under an empty value',
  own: 'Keeps them as their own member',
  omit: 'Leaves them out',
}

/** Sources whose dataset binds the dimension's role. */
export function sourcesForRole(state: ReportState, role: string): Source[] {
  return state.sources.filter((s) => state.datasets.find((d) => d.key === s.dataset)?.roles.includes(role))
}

export interface GroupingIssue {
  rule?: string
  message: string
}

/** Problems with a dimension's stored grouping against its role's published dimension. */
export function groupingIssue(state: ReportState, d: BreakoutDimension): GroupingIssue | null {
  if (!d.role) return { message: `${d.name} needs a dimension.` }
  const dim = dimensionForRole(state, d.role)
  if (!dim) return { message: `${d.name} reads dimension ${d.role}, which is not published.` }
  const missing = fieldError(d.grouping)
  if (missing) return { message: `${d.name}: ${missing}` }
  const where = `Report '${state.settings.key}' calculation dimension '${d.key}'`
  const published = `v${dim.version}, the published version of its dimension.`
  if (d.grouping.kind === 'level' && !dim.levels.some((l) => l.key === (d.grouping as { level: string }).level)) {
    return { rule: 'calculation_grouping_resolve', message: `${where} groups at level '${d.grouping.level}', which is not a level of ${published}` }
  }
  if (d.grouping.kind === 'ancestor' && !indexDimension(dim).has(d.grouping.member!)) {
    return { rule: 'calculation_grouping_resolve', message: `${where} groups under member '${d.grouping.member}', which is not a member of ${published}` }
  }
  return null
}
