import type { components } from '@/api/schema';

type Schemas = components['schemas'];

/** A report: its rows are a dimension of its own, it reads one calculation model, and the engine sees it while enabled (`qe_report`). */
export type Report = Schemas['ReportRecord'];

/** A column of a report: the key of the model metric it returns, with optional label and format overrides (`qe_report_column`). */
export type ReportColumn = Schemas['ReportColumnRecord'];

/** The row whose branch of the report a variable reads, and whether its descendants count (`qe_report_variable_scope`). */
export type ReportVariableScope = Schemas['ReportVariableScopeRecord'];

/** A member of a dimension version, with its parent, its sort order and whether it takes source values. */
export type DimensionMember = Schemas['DimensionMemberRecord'];

/** A source profile with its picks: the source each variable reads under the profile. */
export type SourceProfile = Schemas['SourceProfile'];

/** One pick of a source profile. */
export type SourceProfilePick = Schemas['SourceProfilePick'];

/** A dimension the report can break out or filter by: a role with a grouping of its template's members (`qe_calculation_dimension`). */
export type CalculationDimension = Schemas['CalculationDimensionRecord'];

/** Body of `POST reports`. */
export type NewReportInput = Schemas['NewReportInput'];

/** Body of `PUT reports/{reportId}`. */
export type ReportInput = Schemas['ReportInput'];

/** Body of `PUT reports/{reportId}/settings`. */
export type ReportSettingsInput = Schemas['ReportSettingsInput'];

/** One column of `PUT reports/{reportId}/columns`, naming its metric by key. */
export type ReportColumnInput = Schemas['ReportColumnInput'];

/** One scope of `PUT reports/{reportId}/variable-scopes`. */
export type ReportVariableScopeInput = Schemas['ReportVariableScopeInput'];

/** A report as its editor shows it: its rows with the items each bridge source maps to them, and its sources. */
export type EditorView = Schemas['EditorView'];

/** One row of a report: a member of the report dimension or an overlay row. */
export type EditorRow = Schemas['EditorRow'];

/** One rule a bridge source adds into a row: an item, or every item under a member, with a factor; an entry of the source's bridge. */
export type EditorRowItem = Schemas['EditorRowItem'];

/** One source of a report, with its dataset, item dimension, bridge version, unmapped count and variables. */
export type EditorSource = Schemas['EditorSource'];

/** A dataset a report reads, and whether its rows reach the report's rows by bridge or by field (`qe_report_source`). */
export type ReportSource = Schemas['ReportSourceRecord'];

/** The dataset measure a source supplies one variable with, and its filter (`qe_report_source_variable`). */
export type ReportSourceVariable = Schemas['ReportSourceVariableRecord'];

/** One row of `PUT reports/{reportId}/rows`. */
export type ReportRowInput = Schemas['ReportRowInput'];

/** Body of `POST reports/{reportId}/sources`. */
export type NewReportSourceInput = Schemas['NewReportSourceInput'];

/** Body of `PUT reports/{reportId}/sources/{sourceId}`. */
export type ReportSourceInput = Schemas['ReportSourceInput'];

/** One variable of `PUT reports/{reportId}/sources/{sourceId}/variables`. */
export type SourceVariableInput = Schemas['SourceVariableInput'];

/** One rule of `PUT reports/{reportId}/sources/{sourceId}/items`. */
export type SourceItemInput = Schemas['SourceItemInput'];

/** The `report_row_kind` vocabulary: data and group rows are members of the report dimension, sections and formulas overlay rows. */
export type ReportRowKind = 'data' | 'group' | 'section' | 'formula';

/** The `report_mapping_method` vocabulary: a source reaches the rows through its own bridge, or by a field of row keys. */
export type ReportMappingMethod = 'bridge' | 'direct';

/** The `source_selection` vocabulary: a rule takes its member alone, or the member and every member under it. */
export type SourceSelection = 'member' | 'descendants';
