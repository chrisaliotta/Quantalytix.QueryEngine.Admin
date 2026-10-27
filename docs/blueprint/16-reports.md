# 16 Reports

## Purpose and sources

A report defines rows, the sources that feed them, the profiles that pick a source per variable, the dimensions a run
can break out or filter by, scenarios, variable scopes, columns and styles, over one calculation model. The engine runs
a report while it is enabled, always on the active assumption set and with its model as it stands.

Sources: `web2/src/lib/report/` (`types.ts`, `store.tsx`, `mockData.ts`, `coverage.ts`, `profiles.ts`,
`scenarios.ts`, `dimensions.ts`, `rowStyle.ts`, `metricGraph.ts`) and `components/report/*`,
`components/reports/*`; current `ReportsController`, `ReportEditorController`, `ReportSourcesController`,
`SourceProfilesController`, `CalculationDimensionsController`, `ReportScenariosController`,
`ReportVariableScopesController`, `ReportColumnsController`, `ReportStylesController`; schema `report/`,
`calculation/`; schema README 2.18.0–2.26.0; engine README report contract 2.1.

`web2` has no port for reports. Its store holds each report as one `ReportState` document, with copies of data other
modules own, and computes readiness, coverage and profile resolution in the browser. This file defines the contract
and how it maps to the schema (OI-46).

## Domain rules

| # | Rule | Source |
|---|---|---|
| P1 | Reports are not versioned. `is_enabled` decides whether the engine sees a report; a new report starts disabled. | Schema README 2.19.0 |
| P2 | Enabling a report checks it against every report rule; every later write to an enabled report is checked before it is made. | API README; schema doc §15–16 |
| P3 | A report owns one report dimension (`dimension_purpose = report`), created with the report's key and name, either with a single root or copied from a categorical or hierarchical "start from" dimension. Its one version is published and edited in place through the report. | Schema README 2.18.0, 2.19.0; API `NewReportInput` |
| P4 | `report_dimension_id` and `calculation_model_id` are fixed after creation; `report_key` is fixed. | Schema; API (OI-47) |
| P5 | Rows are the report dimension's members in hierarchy order, plus overlay `section` and `formula` lines; member options relabel, hide or make headers of members. Overlay keys never collide with member keys. Row keys are fixed once created. | Schema README 2.17.0 item 7; API `ReportRowInput` |
| P6 | A source reads one dataset on one time field and reaches rows by `bridge` (its own report bridge, from a role the dataset binds) or `direct` (a field whose values are row keys). | Schema README 2.19.0 item 2 |
| P7 | A bridge source's bridge belongs to that source alone, is `many_to_many` + `replicate`, and is the only kind with `descendants` rules; a `member` rule names an assignable member. A factor other than 1 requires the variable's measure to have `bridge_factor_behavior = value`. | §18 rules 20–21 |
| P8 | Every enabled profile of an enabled report picks, for each variable the report reads, a source that supplies it; the default profile is enabled. | §18 rule 20 |
| P9 | A role is a scenario of a report at most once and never also one of its calculation dimensions; a scenario's dimension has exactly one role. | §18 rule 7 |
| P10 | Row parameters name a row and a model parameter, set the value its type names, within allowed values. | §18 rule 10 |
| P11 | Styles: at most one per default role; colors `#RRGGBB`; a style a row names belongs to the report. | §18 rule 11 |
| P12 | Deleting a report cascades to its dimension, bridges and content; it is never blocked. | API |

## Report storage: one source of truth (Agreed, OI-48 to OI-50)

`ReportState` stores rows, mappings, breakouts and copies of other modules' data in one document. The schema stores
them across the report tables, the report dimension and the report bridges, and the mappings module already shows
report bridges with versions and row counts that the editor never writes. The schema is the store, the
`ReportDocument` read model is assembled from it, and every editor operation writes to the table that owns the fact.

| `web2` `ReportState` | Stored as | Notes |
|---|---|---|
| `settings.key`, `name`, `type` | `qe_report.report_key`, `display_name`, `report_type` | key fixed (OI-47) |
| `settings.model` | `qe_report.calculation_model_id` | fixed in the schema; `web2` lets the user change it (OI-47) |
| `settings.currency` | the model's `currency_code` | read-only on the report |
| `settings.periodicity`, `layout` | `default_settings_json.periodicity`, `.layout` | (OI-54) |
| `settings.subtotals` | `subtotal_position` | |
| `settings.defaultProfile` | `default_profile_key` | |
| `settings.defaultBreakout` | `default_pivot_json.rowDimensions` | (OI-54) |
| `settings.defaultFilters` | `default_filter_json` (`filterNode` over calculation dimensions) | (OI-54) |
| `enabled` | `is_enabled` (a command, not an edit; see below) | |
| `rows` (`line`, `subtotal`, `heading`, `formula`) | report dimension members + `qe_report_member_option` + `qe_report_line` | kinds map to `data`, `group`, `section`, `formula` (OI-48) |
| `rows[].styleKey`, `hidden` | `qe_report_member_option` / `qe_report_line` `style_key`, `is_visible` | |
| `rows[].parameters` | `qe_report_row_parameter` | |
| `sources` (`mapped`, `matched`) | `qe_report_source` (`bridge`, `direct`) | `matched` = `direct` with `field_key` |
| `sources[].mappings` (`factor` ±1, `multiplier`) | the source's report bridge: `qe_bridge_mapping` rules (`source_selection`, `factor`); the multiplier is dropped and the factor is the only number users edit | (OI-49, OI-52) |
| `sources[].leftOut` | `qe_report_source_left_out` (new, [SG-10](91-schema-gaps.md#sg-10-report-source-left-out-items)) | (OI-51) |
| `sources[].unfitMembers` | dropped | (OI-51) |
| `sources[].bindings` | `qe_report_source_variable` (`measure_key`, `source_filter_json`, `missing_behavior`) | |
| `profiles` | `qe_calculation_source_profile` + `_variable` | |
| `breakoutDims` | `qe_calculation_dimension` | `aliasKey` = `dimension_key`; `name` = `display_name`; grouping = `grouping_mode` + parameters |
| `reportScenarios` | `qe_report_scenario` | display name comes from the role (OI-50) |
| `rowScopes` (`row`, `subtree`) | `qe_report_variable_scope` (`exact`, `descendants`) | one scope per variable in the schema (OI-50) |
| `columns` | `qe_report_column` | `kind` = `value_kind`; `metric` = `metric_key` |
| `styles` | `qe_report_style` | |
| `variables`, `measures` | the model | removed from the report (OI-55) |
| `accounts`, `datasets`, `scenarios`, `assumptionSets` | other modules (dimension members, datasets, scenario dimension members, assumption sets) | removed from the report (OI-55) |
| `comparisons` | run-time request input (report contract 2.1 `comparisons[]`) | not stored (OI-55) |

## Frontend contract (Agreed, new `lib/reports/contract.ts`)

```ts
// Shared kernel: Periodicity, Grouping, FilterNode, SourceSelection, VariableMissingBehavior.
export type ReportRowKind = 'data' | 'group' | 'section' | 'formula'
export type ReportMappingMethod = 'bridge' | 'direct'
export type ReportSelectionScope = 'exact' | 'descendants'
// ReportLayout, ReportSubtotalPosition, ReportStyleRole, ReportFontWeight, ReportBorderStyle, ReportColumnValueKind: the schema's values

export interface ReportSummary {
  id: Id; key: string; displayName: string; reportType: string          // key: DottedKey (OI-29)
  modelId: Id; modelKey: string; isEnabled: boolean
  periodicity: Periodicity | null; profileCount: number; sourceCount: number
}

export interface ReportSettings {
  displayName: string
  reportType: string
  subtotalPosition: ReportSubtotalPosition
  defaultProfileKey: string | null
  periodicity: Periodicity | null
  layout: ReportLayout
  defaultBreakout: string[]                 // calculation dimension keys
  defaultFilter: FilterNode | null          // over calculation dimension keys
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
  calculatedMeasureKey: string | null       // formula rows
  valueColumnKey: string | null             // formula rows
  styleKey: string | null
  parameters: Record<string, string | number>
}
export interface ReportRowPosition { parentKey: string | null; index: number }

export interface ReportSourceRule { itemKey: string; selection: SourceSelection; factor: number }
export interface ReportSourceVariable { variableKey: string; measureKey: string; filter: FilterNode | null; missingBehavior: VariableMissingBehavior | null }

export interface ReportSourceInput {
  key: string
  displayName: string
  datasetId: Id
  timeFieldKey: string
  method: ReportMappingMethod
  roleId: Id | null                         // bridge sources
  fieldKey: string | null                   // direct sources
}
export interface ReportSource extends ReportSourceInput {
  bridgeId: Id | null                       // bridge sources: the report bridge it owns
  variables: ReportSourceVariable[]
  /** Bridge sources: rules per row key. */
  rules: Record<string, ReportSourceRule[]>
  /** Bridge sources: items excluded on purpose; coverage does not count them as gaps. */
  leftOut: string[]
}

export interface ReportProfile { key: string; displayName: string; isEnabled: boolean; picks: Record<string, string> } // variable -> source key
export interface ReportCalculationDimension {
  key: string; displayName: string; roleId: Id; grouping: Grouping
  isGroupable: boolean; isFilterable: boolean; isEnabled: boolean
}
export interface ReportScenario { roleId: Id }
export interface ReportVariableScope { variableKey: string; scopeMemberKey: string; selectionScope: ReportSelectionScope }
export interface ReportColumn { key: string; displayName: string | null; valueKind: ReportColumnValueKind; metricKey: string; displayFormat: string | null; isVisible: boolean }
export interface ReportStyle {
  key: string; displayName: string; defaultRole: ReportStyleRole | null
  fontFamily: string | null; fontSize: number | null; fontWeight: ReportFontWeight | null
  isItalic: boolean; textColor: string | null; backgroundColor: string | null
  borderTop: ReportBorderStyle | null; borderBottom: ReportBorderStyle | null; indent: number | null
}

export interface ReportDocument {
  id: Id; key: string; isEnabled: boolean
  modelId: Id; reportDimensionId: Id; reportDimensionVersionId: Id
  settings: ReportSettings
  rows: ReportRow[]                         // hierarchy order
  sources: ReportSource[]
  profiles: ReportProfile[]
  calculationDimensions: ReportCalculationDimension[]
  scenarios: ReportScenario[]
  variableScopes: ReportVariableScope[]
  columns: ReportColumn[]                   // sort order = position
  styles: ReportStyle[]
}

/** Server-computed; replaces coverage.ts, profiles.ts, scenarios.ts and readiness in the browser (OI-05). */
export interface ReportDiagnostics {
  ready: boolean                            // all report rules pass: the report can be enabled
  steps: ReportDiagnosticsStep[]            // { step: 'rows' | 'sources' | 'variables' | 'items' | 'profiles' | 'dimensions' | 'columns'; status: 'ok' | 'warn' | 'fail'; violations }
  coverage: Record<string, ReportSourceCoverage>                         // by source key
  resolution: Record<string, Record<string, ReportVariableResolution>>   // profile -> variable -> status
}
export interface ReportSourceCoverage { unmapped: string[]; multi: ReportCoverageMulti[]; leftOut: string[] }
export interface ReportCoverageMulti { itemKey: string; net: number; rows: string[] }
export interface ReportVariableResolution { status: 'ok' | 'shadowed' | 'gap' | 'unresolved' | 'na'; sourceKey: string | null; rowGaps: string[] }

export interface ReportCreateInput { key: string; displayName: string; reportType: string; modelId: Id; startFromDimensionId?: Id; copyFromReportId?: Id }
export interface ReportEnabledInput { isEnabled: boolean }
```

`FilterNode` (the schema's filter JSON shape) and `Grouping` are shared kernel types (`common.yaml`), as are
`Periodicity`, `SourceSelection` (a rule's selection, the same values as a bridge mapping's) and
`VariableMissingBehavior` (a source variable's override of the model variable's). Every other component carries a
`Report` prefix because contract names are global (`ReportProfile`, `ReportColumn`, `ReportSourceInput`).

### Port

```ts
export interface ReportsApi extends ChangeSetPort {
  // reads
  listReports(): Promise<ReportSummary[]>
  getReport(id: Id): Promise<ApiResult<ReportDocument>>
  getDiagnostics(id: Id): Promise<ReportDiagnostics>          // saved state; pending state via preview
  getUnmappedItems(id: Id, sourceKey: string): Promise<string[]>

  // edits (staged per report; every edit names the report first)
  updateSettings(reportId: Id, patch: ReportSettingsPatch): Promise<ApiResult<ReportSettings>>
  saveRow(reportId: Id, row: ReportRow & { index: number }): Promise<ApiResult<ReportRow>>
  moveRow(reportId: Id, key: string, to: ReportRowPosition): Promise<ApiResult<null>>
  deleteRow(reportId: Id, key: string): Promise<ApiResult<null>>              // children move up; server cascades rules and scopes
  saveSource(reportId: Id, input: ReportSourceInput & { copyFromSourceKey?: string }): Promise<ApiResult<ReportSource>>
  deleteSource(reportId: Id, key: string): Promise<ApiResult<null>>           // server cascades profile picks
  setSourceVariables(reportId: Id, sourceKey: string, variables: ReportSourceVariable[]): Promise<ApiResult<null>>
  setRowRules(reportId: Id, sourceKey: string, rowKey: string, rules: ReportSourceRule[]): Promise<ApiResult<null>>
  setLeftOut(reportId: Id, sourceKey: string, itemKeys: string[]): Promise<ApiResult<null>>
  saveProfile(reportId: Id, profile: ReportProfile & { originalKey?: string }): Promise<ApiResult<ReportProfile>>
  deleteProfile(reportId: Id, key: string): Promise<ApiResult<null>>
  saveCalculationDimension(reportId: Id, input: ReportCalculationDimension & { originalKey?: string }): Promise<ApiResult<ReportCalculationDimension>>
  deleteCalculationDimension(reportId: Id, key: string): Promise<ApiResult<null>>   // server cascades default breakout and filter
  setScenarios(reportId: Id, roleIds: Id[]): Promise<ApiResult<null>>
  setVariableScopes(reportId: Id, scopes: ReportVariableScope[]): Promise<ApiResult<null>>
  setColumns(reportId: Id, columns: ReportColumn[]): Promise<ApiResult<null>>
  saveStyle(reportId: Id, style: ReportStyle): Promise<ApiResult<ReportStyle>>
  deleteStyle(reportId: Id, key: string): Promise<ApiResult<null>>

  // commands
  createReport(input: ReportCreateInput): Promise<ApiResult<ReportSummary>>
  setEnabled(id: Id, isEnabled: boolean): Promise<ApiResult<ReportSummary>>
  deleteReport(id: Id): Promise<ApiResult<null>>
}
```

**Edit methods take `reportId` first (Agreed, G1).** Like every other port, an edit names the aggregate it changes
instead of relying on a bound id; the staged port keys the change set by it, and one change set covers one report, so
every edit of a batch names the same `reportId`. The operation union carries no `reportId`: the route names the
report.

**`createReport.copyFromReportId`** serves `web2`'s "New report" (copies rows, sources and profiles of another
report, starts disabled) and "Duplicate" (`_copyN` key suggested by the client). The server copies the report
dimension, report bridges and all content (new behavior; today `POST reports` only starts from a dimension). With
`modelId` different from the source report's, it is also how a user "changes the model": the copy drops what does not
resolve in the new model and starts disabled (OI-47).

**Enable is a command.** `web2` toggles `enabled` as a staged edit and refuses Save while an enabled report is not
ready. `setEnabled` runs on saved state; enabling answers 422 with the failing rules; while enabled,
every commit passes the report gate or is refused with 422 (the current API's behavior).

### Sources and report bridges

A bridge source owns one report bridge. Creating a bridge source creates the bridge (published v1, `many_to_many`,
`replicate`, source dimension = the role's dimension, target = the report dimension) in the same commit; deleting the
source deletes it. `setRowRules` writes the bridge's `qe_bridge_mapping` rows for that row (one row per rule;
`target_member_key` = row key). The mappings module reads these bridges and refuses to write them
([13](13-mappings-and-bridges.md)). This removes the duplicate copy `web2` keeps in `Source.mappings`.

The factor is the only number a rule carries (OI-52). `web2`'s separate multiplier (`multiplier-control.tsx`,
`mapping.setMultiplier`) is removed: a user who wants half an account sets the factor to 0.5, and an exclusion is a −1
rule on the same item, which is what `breakOut` already produces.

## HTTP API

Prefix `/api/tenants/{t}`. Open to any tenant user (OI-09).

| Method | Route | Request | Response | Errors | Port |
|---|---|---|---|---|---|
| GET | `reports` | | `ReportSummary[]` | | `listReports` |
| POST | `reports` | `ReportCreateInput` | `201 ReportSummary` | 400, 409 key taken | `createReport` |
| GET | `reports/{id}` | | `ReportDocument` | 404 | `getReport` |
| GET | `reports/{id}/diagnostics` | | `ReportDiagnostics` | 404 | `getDiagnostics` |
| GET | `reports/{id}/sources/{sourceKey}/unmapped` | | `string[]` | 404 | `getUnmappedItems` |
| POST | `reports/{id}/changes/preview` | `ChangeSetRequest<ReportOperation>` | `ChangeSetPreview<ReportDocument> & { diagnostics: ReportDiagnostics }` | 400, 409, 422 (enabled report gate) | edits |
| POST | `reports/{id}/changes` | `ChangeSetRequest<ReportOperation>` | `ChangeSetCommit<ReportDocument>` | 400, 422 | `commitChanges` |
| PUT | `reports/{id}/enabled` | `ReportEnabledInput` | `ReportSummary` | 422 rules | `setEnabled` |
| DELETE | `reports/{id}` | | `204` | 404 | `deleteReport` |

Preview responses include `diagnostics`, so the readiness badge, coverage dialog, profile matrix and "what this row
shows" panel render the pending state without browser-side rules.

## Change-set operations

```ts
type ReportOperation =
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
```

**Server-side cascades (Agreed, OI-56)**, replacing the partial ones in the browser:

| Operation | Cascade |
|---|---|
| `deleteRow` | children move up; its bridge rules, scopes, row parameters and member option go; formula rows naming it as parent move up |
| `deleteSource` | its bridge goes; profile picks naming it are cleared (`web2` leaves them dangling) |
| `deleteCalculationDimension` | removed from default breakout **and** default filter (`web2` cleans breakout only) |
| `deleteStyle` | refused with `report_styles_resolve` while a row names it |
| `deleteProfile` | refused while it is the default |
| a model change | not offered (P4); see OI-47 |

## Rule codes

Report rules (current API): `report_items_resolve`, `calculation_grouping_resolve`, `scenario_roles_distinct`,
`default_profile_enabled`, `report_defaults_resolve`, `column_keys_distinct`, `report_metrics_resolve`,
`profiles_complete`, `report_sources_resolve`, `report_factors_apply`, `report_row_parameters_resolve`,
`report_styles_resolve`. Diagnostics steps group them: `rows` (`report_items_resolve`, formula rows), `sources`,
`variables` and `profiles` (`profiles_complete`, `report_sources_resolve`), `items` (coverage, warnings only),
`dimensions` (`calculation_grouping_resolve`, `scenario_roles_distinct`), `columns` (`column_keys_distinct`,
`report_metrics_resolve`).

## Preview and run (out of scope, OI-53)

Report preview and run are not part of this refactor. `web2`'s static Preview tab (`preview/preview-tab.tsx`, unreachable
from the editor nav) is removed or left unreachable, and the Admin API does not call the engine. Comparisons, which
`ReportState` stored, are run-time inputs of report contract 2.1 and are not stored (OI-55); they return with a future
preview feature.

## Deltas from the current API

| Change | Item |
|---|---|
| Keep | The report gate; report dimension and bridge creation with the report and source; cascade delete; the 12 report rules |
| Change | `GET reports/{id}/editor` becomes `GET reports/{id}` returning the whole `ReportDocument` (today: rows and sources only; columns, profiles, dimensions, scenarios, scopes and styles need separate calls) |
| Change | `PUT rows`, `POST/PUT/DELETE sources`, `PUT sources/{id}/variables`, `PUT sources/{id}/items`, profiles, calculation dimensions, scenarios, variable scopes, columns and styles endpoints become report change-set operations |
| Change | `PUT reports/{id}/settings` and `PUT reports/{id}` become `updateSettings` |
| Change | `PUT reports/{id}/status` becomes `PUT reports/{id}/enabled` |
| Change | `POST reports/{id}/validation` becomes `GET diagnostics` with coverage and resolution |
| New | `copyFromReportId` on create, diagnostics, change-set preview, left-out items |

## Decisions applied

[OI-01](90-open-issues.md#oi-01), [OI-05](90-open-issues.md#oi-05), [OI-46](90-open-issues.md#oi-46),
[OI-47](90-open-issues.md#oi-47), [OI-48](90-open-issues.md#oi-48), [OI-49](90-open-issues.md#oi-49),
[OI-50](90-open-issues.md#oi-50), [OI-51](90-open-issues.md#oi-51), [OI-52](90-open-issues.md#oi-52),
[OI-53](90-open-issues.md#oi-53), [OI-54](90-open-issues.md#oi-54), [OI-55](90-open-issues.md#oi-55),
[OI-56](90-open-issues.md#oi-56).
