export type RowKind = 'line' | 'subtotal' | 'heading' | 'formula'

export interface RowNode {
  key: string
  label: string
  kind: RowKind
  hidden: boolean
  measureKey?: string
  columnKey?: string
  /** Empty follows the row role's default style. */
  styleKey?: string
  /** Own parameter values; missing keys inherit. */
  parameters?: Record<string, string | number>
  children: RowNode[]
}

export type RowRole = 'header' | 'subtotal' | 'total' | 'detail' | 'formula'
export type FontWeight = 'normal' | 'medium' | 'semibold' | 'bold'
export type BorderStyle = 'none' | 'thin' | 'medium' | 'thick' | 'double'

export interface ReportStyle {
  key: string
  name: string
  defaultRole?: RowRole | null
  fontFamily?: string
  fontSize?: number
  fontWeight?: FontWeight
  italic?: boolean
  textColor?: string
  backgroundColor?: string
  borderTop?: BorderStyle
  borderBottom?: BorderStyle
  indent?: number
}

export interface Variable {
  key: string
  name: string
}

export interface CalculatedMeasure {
  key: string
  name: string
  formula: string
}

export type ColumnKind = 'variable' | 'calculated'
export type DisplayFormat = 'currency' | 'percent' | 'number' | 'basisPoints'

export interface Column {
  key: string
  label: string
  kind: ColumnKind
  metric: string
  format: DisplayFormat
  visible: boolean
}

export interface DimMember {
  key: string
  label: string
  children?: DimMember[]
}

export type FilterOp = '=' | '!=' | 'in' | '>' | '<'

export interface Filter {
  field: string
  op: FilterOp
  value: string
}

export interface Binding {
  id: string
  variable: string
  measure: string
  filters: Filter[]
  /** What a missing value becomes; 'default' follows the variable. */
  missing?: 'default' | 'null' | 'zero'
}

export interface Mapping {
  id: string
  rowKey: string
  memberKey: string
  /** +1 includes the member, -1 cancels an inclusion (an exclusion). */
  factor: number
}

export type SourceMethod = 'mapped' | 'matched'

export interface Source {
  key: string
  name: string
  dataset: string
  method: SourceMethod
  dimension?: string
  rowKeyField?: string
  timeField: string
  bindings: Binding[]
  mappings: Mapping[]
  /** Accounts intentionally left out of this report (not counted as coverage gaps). */
  leftOut: string[]
  /** Defaults to grouping them under an empty value. */
  unfitMembers?: UnfitHandling
}

export interface Dataset {
  key: string
  name: string
  measures: string[]
  fields: string[]
  timeFields: string[]
  fieldValues?: Record<string, string[]>
  /** Dimension roles the dataset binds, e.g. account, branch. */
  roles: string[]
}

export interface Profile {
  key: string
  name: string
  enabled: boolean
  picks: Record<string, string>
}

/** How a dimension's members turn into breakout rows and what a filter compares. Null inputs are not chosen yet. */
export type Grouping =
  | { kind: 'member' }
  | { kind: 'level'; level: string | null }
  | { kind: 'ancestor'; member: string | null }
  | { kind: 'depth'; steps: number | null }

export type GroupingKind = Grouping['kind']

export interface BreakoutDimension {
  key: string
  /** Display alias; defaults to the dimension role's name. */
  name: string
  /** Key the report exposes this dimension under; defaults to the dimension role's key. */
  aliasKey: string
  role: string
  grouping: Grouping
  groupable: boolean
  filterable: boolean
  enabled: boolean
}

/** What a source does with members that have no group under the chosen grouping. */
export type UnfitHandling = 'empty' | 'own' | 'omit'

/** A dimension role whose members a run chooses one of; never broken out or filtered by. */
export interface ReportScenario {
  role: string
  name: string
}

export type ScenarioStatus = 'approved' | 'superseded' | 'draft'

export interface ScenarioMember {
  key: string
  label: string
  status?: ScenarioStatus
  asOf?: string
  horizon?: string
  children?: ScenarioMember[]
}

export interface AssumptionSet {
  key: string
  label: string
  captured: boolean
  note?: string
}

export type PeriodShift = 'same' | 'PM' | 'PQ' | 'PY'

export interface ComparisonSide {
  /** 'actual', a plan version key, or `latest:<folderKey>` for the latest approved under a folder. */
  scenario: string
  period: PeriodShift
  assumptions: string
}

export interface Comparison {
  id: string
  key: string
  label: string
  base: ComparisonSide
  compare: ComparisonSide
}

export type ScopeKind = 'row' | 'subtree'

export interface RowScope {
  id: string
  variable: string
  rowKey: string
  scope: ScopeKind
}

export interface ReportSettings {
  key: string
  name: string
  type: string
  model: string
  currency: string
  periodicity: 'Day' | 'Month' | 'Quarter' | 'Year'
  layout: 'Statement' | 'List'
  subtotals: 'Above' | 'Below'
  defaultProfile: string
  defaultBreakout: string[]
  defaultFilters: Filter[]
}

export interface ReportState {
  settings: ReportSettings
  enabled: boolean
  rows: RowNode[]
  variables: Variable[]
  measures: CalculatedMeasure[]
  columns: Column[]
  accounts: DimMember[]
  datasets: Dataset[]
  sources: Source[]
  profiles: Profile[]
  breakoutDims: BreakoutDimension[]
  rowScopes: RowScope[]
  reportScenarios: ReportScenario[]
  scenarios: ScenarioMember[]
  assumptionSets: AssumptionSet[]
  comparisons: Comparison[]
  styles: ReportStyle[]
}
