import type { components } from '@/api/schema';

type Schemas = components['schemas'];

/** A report with its own report dimension, whose members are its rows and which it creates (`qe_report`). */
export type Report = Schemas['ReportRecord'];

/** A version of a report over one calculation model, with its data, rows, columns and defaults (`qe_report_version`). */
export type ReportVersion = Schemas['ReportVersionRecord'];

/** A column of a report version: the key of the model metric it returns, with optional label and format overrides (`qe_report_column`). */
export type ReportColumn = Schemas['ReportColumnRecord'];

/** The member whose branch of the report a variable reads, and whether its descendants count (`qe_report_variable_scope`). */
export type ReportVariableScope = Schemas['ReportVariableScopeRecord'];

/** A member of a dimension version, with its parent, its sort order and whether it takes source values. */
export type DimensionMember = Schemas['DimensionMemberRecord'];

/** Where a variable can read its values: a dataset's classification field, time field, measure and filter (`qe_variable_source`). */
export type VariableSource = Schemas['VariableSourceRecord'];

/** A source profile with its picks: the source each variable reads under the profile. */
export type SourceProfile = Schemas['SourceProfile'];

/** One pick of a source profile. */
export type SourceProfilePick = Schemas['SourceProfilePick'];

/** A dimension the report can break out or filter by: a role with a grouping of its template's members (`qe_calculation_dimension`). */
export type CalculationDimension = Schemas['CalculationDimensionRecord'];

/** How a source classifies a dataset's rows into the report's dimension: the kind, the field or role, and the mapping set or bridge. */
export type ClassificationInput = Schemas['ClassificationInput'];

/** Body of `POST reports`. */
export type NewReportInput = Schemas['NewReportInput'];

/** Body of `PUT reports/{reportId}`. */
export type ReportInput = Schemas['ReportInput'];

/** Body of `POST reports/{reportId}/versions`. */
export type NewReportVersionInput = Schemas['NewReportVersionInput'];

/** Body of `PUT report-versions/{versionId}`. */
export type ReportVersionInput = Schemas['ReportVersionInput'];

/** One column of `PUT report-versions/{versionId}/columns`, naming its metric by key. */
export type ReportColumnInput = Schemas['ReportColumnInput'];

/** One scope of `PUT report-versions/{versionId}/variable-scopes`. */
export type ReportVariableScopeInput = Schemas['ReportVariableScopeInput'];

/** A report version as one design: its rows with the source items mapped to each, and its sources. */
export type ReportDesign = Schemas['ReportDesign'];

/** One row of a design: a member of the report dimension or an overlay row. */
export type DesignRow = Schemas['DesignRow'];

/** One source item mapped to a row: an entry of the source's bridge. */
export type DesignRowMapping = Schemas['DesignRowMapping'];

/** One source of a design: a bridge from a bound dimension, with the variable sources that classify through it. */
export type DesignSource = Schemas['DesignSource'];

/** One row of `PUT report-versions/{versionId}/design/rows`. */
export type DesignRowInput = Schemas['DesignRowInput'];

/** Body of `PUT report-versions/{versionId}/design/sources/{bridgeId}`. */
export type DesignSourceInput = Schemas['DesignSourceInput'];

/** Body of `POST report-versions/{versionId}/design/sources`. */
export type NewDesignSourceInput = Schemas['NewDesignSourceInput'];

/** Body of `PUT report-versions/{versionId}/design/row-mappings`. */
export type RowMappingsInput = Schemas['RowMappingsInput'];

/** One item of a row's mapping list. */
export type RowMappingInput = Schemas['RowMappingInput'];

/** The `design_row_kind` vocabulary: data and group rows are members of the report dimension, sections and formulas overlay rows. */
export type DesignRowKind = 'data' | 'group' | 'section' | 'formula';
