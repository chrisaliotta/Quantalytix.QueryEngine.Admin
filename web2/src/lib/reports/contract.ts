import type { ApiResult, ChangeSetCommit, ChangeSetPreview, ChangeSetRequest, Id, Violation } from '@/lib/api/contract'
import type { ChangeSetPort } from '@/lib/api/changes'

/**
 * Business contract for reports (blueprint 16). Every type mirrors the schema of the same name in
 * api/openapi/contract/reports.yaml. Readiness, coverage and profile resolution are computed by the server and arrive
 * as `ReportDiagnostics` (OI-05).
 */

export type { ApiResult, Id, Violation } from '@/lib/api/contract'

/* ───────────────────────── vocabularies ───────────────────────── */

export type ReportPeriodicity = 'day' | 'month' | 'quarter' | 'year'
export type ReportLayout = 'statement' | 'list'
export type ReportSubtotalPosition = 'above' | 'below'
/** `data` and `group` rows are report dimension members; `section` and `formula` rows are overlay lines (OI-48). */
export type ReportRowKind = 'data' | 'group' | 'section' | 'formula'
export type ReportMappingMethod = 'bridge' | 'direct'
/** `exact` needs a leaf member. */
export type ReportSelectionScope = 'exact' | 'descendants'
export type ReportRuleSelection = 'member' | 'descendants'
export type ReportMissingBehavior = 'null' | 'zero'
export type ReportStyleRole = 'header' | 'subtotal' | 'total' | 'detail' | 'formula'
export type ReportFontWeight = 'normal' | 'medium' | 'semibold' | 'bold'
export type ReportBorderStyle = 'none' | 'thin' | 'medium' | 'thick' | 'double'
export type ReportColumnValueKind = 'variable' | 'calculated'

/* ───────────────────────── grouping and filters ───────────────────────── */

export type ReportGrouping =
  | { mode: 'member' }
  | { mode: 'level'; levelKey: string }
  | { mode: 'ancestor'; memberKey: string }
  | { mode: 'depth'; depth: number }

/** The query engine's structured `filterNode`; dimension operands name calculation dimension keys. */
export type ReportFilterNode =
  | { operator: 'and' | 'or'; operands: ReportFilterNode[] }
  | { operator: 'not'; operand: ReportFilterNode }
  | { operator: 'eq' | 'ne' | 'lt' | 'le' | 'gt' | 'ge'; left: ReportFilterOperand; right: ReportFilterOperand }
  | { operator: 'in'; left: ReportFilterOperand; values: ReportFilterOperand[] }
  | { operator: 'isNull' | 'isNotNull'; operand: ReportFilterOperand }

export type ReportFilterOperand =
  | { type: 'field' | 'measure' | 'parameter'; id: string }
  | { type: 'dimension'; id: string; grouping?: ReportGrouping }
  | { type: 'literal'; value: string | number | boolean | null }

/* ───────────────────────── read models ───────────────────────── */

export interface ReportSummary {
  id: Id
  key: string
  displayName: string
  reportType: string
  modelId: Id
  modelKey: string
  isEnabled: boolean
  periodicity: ReportPeriodicity | null
  profileCount: number
  sourceCount: number
}

export interface ReportSettings {
  displayName: string
  reportType: string
  subtotalPosition: ReportSubtotalPosition
  defaultProfileKey: string | null
  periodicity: ReportPeriodicity | null
  layout: ReportLayout
  /** Calculation dimension keys. */
  defaultBreakout: string[]
  /** Over calculation dimension keys. */
  defaultFilter: ReportFilterNode | null
}

export type ReportSettingsPatch = Partial<ReportSettings>

export interface ReportRow {
  key: string
  label: string
  kind: ReportRowKind
  parentKey: string | null
  isVisible: boolean
  /** Member rows: false makes a header that shows no values. */
  showsValues: boolean
  /** Formula rows. */
  calculatedMeasureKey: string | null
  /** Formula rows. */
  valueColumnKey: string | null
  styleKey: string | null
  /** Own model parameter values; missing keys inherit. */
  parameters: Record<string, string | number>
}

export interface ReportRowPosition {
  parentKey: string | null
  /** Index among the new siblings after the move. */
  index: number
}

export interface ReportSourceRule {
  itemKey: string
  selection: ReportRuleSelection
  /** The only number a rule carries; -1 on an item already included excludes it (OI-52). */
  factor: number
}

export interface ReportSourceVariable {
  variableKey: string
  measureKey: string
  /** Over the dataset's filterable fields, applied before aggregation. */
  filter: ReportFilterNode | null
  /** Null keeps the variable's own. */
  missingBehavior: ReportMissingBehavior | null
}

export interface ReportSourceInput {
  key: string
  displayName: string
  datasetId: Id
  timeFieldKey: string
  method: ReportMappingMethod
  /** Bridge sources. */
  roleId: Id | null
  /** Direct sources. */
  fieldKey: string | null
}

export interface ReportSource extends ReportSourceInput {
  /** Bridge sources: the report bridge the source owns. */
  bridgeId: Id | null
  variables: ReportSourceVariable[]
  /** Bridge sources: rules by row key. */
  rules: Record<string, ReportSourceRule[]>
  /** Bridge sources: items excluded on purpose; coverage does not count them as gaps. */
  leftOut: string[]
}

export interface ReportProfile {
  key: string
  displayName: string
  isEnabled: boolean
  /** Variable key → source key. */
  picks: Record<string, string>
}

export interface ReportCalculationDimension {
  key: string
  displayName: string
  roleId: Id
  grouping: ReportGrouping
  isGroupable: boolean
  isFilterable: boolean
  isEnabled: boolean
}

/** Its display name is the role's (OI-50). */
export interface ReportScenario {
  roleId: Id
}

/** One scope per variable (OI-50). */
export interface ReportVariableScope {
  variableKey: string
  scopeMemberKey: string
  selectionScope: ReportSelectionScope
}

export interface ReportColumn {
  key: string
  /** Null keeps the metric's label. */
  displayName: string | null
  valueKind: ReportColumnValueKind
  metricKey: string
  /** Null keeps the metric's format. */
  displayFormat: string | null
  isVisible: boolean
}

/** A null property keeps the client's own styling. */
export interface ReportStyle {
  key: string
  displayName: string
  defaultRole: ReportStyleRole | null
  fontFamily: string | null
  /** Points. */
  fontSize: number | null
  fontWeight: ReportFontWeight | null
  isItalic: boolean
  /** `#RRGGBB`. */
  textColor: string | null
  /** `#RRGGBB`. */
  backgroundColor: string | null
  borderTop: ReportBorderStyle | null
  borderBottom: ReportBorderStyle | null
  /** Indent levels added to the row's depth. */
  indent: number | null
}

export interface ReportDocument {
  id: Id
  key: string
  isEnabled: boolean
  modelId: Id
  reportDimensionId: Id
  reportDimensionVersionId: Id
  settings: ReportSettings
  /** Hierarchy order. */
  rows: ReportRow[]
  sources: ReportSource[]
  profiles: ReportProfile[]
  calculationDimensions: ReportCalculationDimension[]
  scenarios: ReportScenario[]
  variableScopes: ReportVariableScope[]
  /** Sort order is the position. */
  columns: ReportColumn[]
  styles: ReportStyle[]
}

/* ───────────────────────── diagnostics ───────────────────────── */

export type ReportDiagnosticsStepName = 'rows' | 'sources' | 'variables' | 'items' | 'profiles' | 'dimensions' | 'columns'

export interface ReportDiagnosticsStep {
  step: ReportDiagnosticsStepName
  status: 'ok' | 'warn' | 'fail'
  violations: Violation[]
}

export interface ReportCoverageMulti {
  itemKey: string
  /** Sum of the factors with which the item reaches rows. */
  net: number
  /** Row keys. */
  rows: string[]
}

export interface ReportSourceCoverage {
  unmapped: string[]
  /** Items that reach more than one row. */
  multi: ReportCoverageMulti[]
  leftOut: string[]
}

export interface ReportVariableResolution {
  status: 'ok' | 'shadowed' | 'gap' | 'unresolved' | 'na'
  sourceKey: string | null
  /** Row keys the picked source does not supply. */
  rowGaps: string[]
}

/** Server-computed; replaces coverage.ts, profiles.ts, scenarios.ts and readiness in the browser (OI-05). */
export interface ReportDiagnostics {
  /** Every report rule passes, so the report can be enabled. */
  ready: boolean
  steps: ReportDiagnosticsStep[]
  /** By source key. */
  coverage: Record<string, ReportSourceCoverage>
  /** Profile key → variable key → resolution. */
  resolution: Record<string, Record<string, ReportVariableResolution>>
}

/* ───────────────────────── commands ───────────────────────── */

/** `startFromDimensionId` and `copyFromReportId` exclude each other. */
export interface ReportCreateInput {
  key: string
  displayName: string
  reportType: string
  modelId: Id
  /** A categorical or hierarchical dimension whose published members become the rows. */
  startFromDimensionId?: Id
  /** A report whose rows, sources and content are copied; with another `modelId`, what does not resolve is dropped. */
  copyFromReportId?: Id
}

export interface ReportEnabledInput {
  isEnabled: boolean
}

/* ───────────────────────── change set ───────────────────────── */

export type ReportOperation =
  | { op: 'updateSettings'; patch: ReportSettingsPatch }
  | { op: 'saveRow'; row: ReportRow; index: number }
  | { op: 'moveRow'; key: string; to: ReportRowPosition }
  | { op: 'deleteRow'; key: string }
  | { op: 'saveSource'; input: ReportSourceInput; copyFromSourceKey?: string }
  | { op: 'deleteSource'; key: string }
  | { op: 'setSourceVariables'; sourceKey: string; variables: ReportSourceVariable[] }
  | { op: 'setRowRules'; sourceKey: string; rowKey: string; rules: ReportSourceRule[] }
  | { op: 'setLeftOut'; sourceKey: string; itemKeys: string[] }
  | { op: 'saveProfile'; profile: ReportProfile; originalKey?: string }
  | { op: 'deleteProfile'; key: string }
  | { op: 'saveCalculationDimension'; input: ReportCalculationDimension; originalKey?: string }
  | { op: 'deleteCalculationDimension'; key: string }
  | { op: 'setScenarios'; roleIds: Id[] }
  | { op: 'setVariableScopes'; scopes: ReportVariableScope[] }
  | { op: 'setColumns'; columns: ReportColumn[] }
  | { op: 'saveStyle'; style: ReportStyle }
  | { op: 'deleteStyle'; key: string }

export type ReportChangeSetRequest = ChangeSetRequest<ReportOperation>
/** Carries the pending state's diagnostics so readiness, coverage and resolution render before Save. */
export type ReportChangeSetPreview = ChangeSetPreview<ReportDocument> & { diagnostics: ReportDiagnostics }
export type ReportChangeSetCommit = ChangeSetCommit<ReportDocument>

/* ───────────────────────── the port ───────────────────────── */

/**
 * Edits stage into the open report's change set and apply on `commitChanges`; see lib/api/changes.ts. One change set
 * covers one report, so every edit of a batch names the same `reportId`.
 */
export interface ReportsApi extends ChangeSetPort {
  listReports(): Promise<ReportSummary[]>
  getReport(id: Id): Promise<ApiResult<ReportDocument>>
  /** Stored state; pending state is diagnosed by the change-set preview. */
  getDiagnostics(id: Id): Promise<ReportDiagnostics>
  getUnmappedItems(id: Id, sourceKey: string): Promise<string[]>

  updateSettings(reportId: Id, patch: ReportSettingsPatch): Promise<ApiResult<ReportSettings>>
  saveRow(reportId: Id, row: ReportRow & { index: number }): Promise<ApiResult<ReportRow>>
  moveRow(reportId: Id, key: string, to: ReportRowPosition): Promise<ApiResult<null>>
  /** Children move up; the server cascades rules and scopes. */
  deleteRow(reportId: Id, key: string): Promise<ApiResult<null>>
  saveSource(reportId: Id, input: ReportSourceInput & { copyFromSourceKey?: string }): Promise<ApiResult<ReportSource>>
  /** The server deletes its bridge and clears profile picks naming it. */
  deleteSource(reportId: Id, key: string): Promise<ApiResult<null>>
  setSourceVariables(reportId: Id, sourceKey: string, variables: ReportSourceVariable[]): Promise<ApiResult<null>>
  setRowRules(reportId: Id, sourceKey: string, rowKey: string, rules: ReportSourceRule[]): Promise<ApiResult<null>>
  setLeftOut(reportId: Id, sourceKey: string, itemKeys: string[]): Promise<ApiResult<null>>
  saveProfile(reportId: Id, profile: ReportProfile & { originalKey?: string }): Promise<ApiResult<ReportProfile>>
  /** Refused while it is the default. */
  deleteProfile(reportId: Id, key: string): Promise<ApiResult<null>>
  saveCalculationDimension(reportId: Id, input: ReportCalculationDimension & { originalKey?: string }): Promise<ApiResult<ReportCalculationDimension>>
  /** The server removes it from the default breakout and the default filter. */
  deleteCalculationDimension(reportId: Id, key: string): Promise<ApiResult<null>>
  setScenarios(reportId: Id, roleIds: Id[]): Promise<ApiResult<null>>
  setVariableScopes(reportId: Id, scopes: ReportVariableScope[]): Promise<ApiResult<null>>
  setColumns(reportId: Id, columns: ReportColumn[]): Promise<ApiResult<null>>
  saveStyle(reportId: Id, style: ReportStyle): Promise<ApiResult<ReportStyle>>
  /** Refused with `report_styles_resolve` while a row names it. */
  deleteStyle(reportId: Id, key: string): Promise<ApiResult<null>>

  createReport(input: ReportCreateInput): Promise<ApiResult<ReportSummary>>
  /** Enabling answers 422 with the failing report rules. */
  setEnabled(id: Id, isEnabled: boolean): Promise<ApiResult<ReportSummary>>
  deleteReport(id: Id): Promise<ApiResult<null>>
}

export const REPORT_EDITS = [
  'updateSettings',
  'saveRow',
  'moveRow',
  'deleteRow',
  'saveSource',
  'deleteSource',
  'setSourceVariables',
  'setRowRules',
  'setLeftOut',
  'saveProfile',
  'deleteProfile',
  'saveCalculationDimension',
  'deleteCalculationDimension',
  'setScenarios',
  'setVariableScopes',
  'setColumns',
  'saveStyle',
  'deleteStyle',
] as const satisfies readonly (keyof ReportsApi & ReportOperation['op'])[]

export const REPORT_COMMANDS = ['createReport', 'setEnabled', 'deleteReport'] as const satisfies readonly (keyof ReportsApi)[]
