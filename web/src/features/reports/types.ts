import type { components } from '@/api/schema';

type Schemas = components['schemas'];

/** A report: the report dimension whose members are its rows, fixed once created (`qe_report`). */
export type Report = Schemas['ReportRecord'];

/** A version of a report over one calculation model, with its data, rows, columns and defaults (`qe_report_version`). */
export type ReportVersion = Schemas['ReportVersionRecord'];

/** A column of a report version: the key of the model metric it returns, with optional label and format overrides (`qe_report_column`). */
export type ReportColumn = Schemas['ReportColumnRecord'];

/** An overlay row of a report version: a section or a formula placed among the report dimension's members (`qe_report_line`). */
export type ReportLine = Schemas['ReportLineRecord'];

/** How a report version shows one member of the report dimension (`qe_report_member_option`). */
export type ReportMemberOption = Schemas['ReportMemberOptionRecord'];

/** The member a variable reads at report scope, and how far below it (`qe_report_variable_target`). */
export type ReportVariableTarget = Schemas['ReportVariableTargetRecord'];

/** A member of the report dimension's version, with its parent and sort order. */
export type ReportDimensionMember = Schemas['DimensionMemberRecord'];

/** Where a variable can read its values: a dataset's classification field, time field, measure and filter (`qe_variable_source`). */
export type VariableSource = Schemas['VariableSourceRecord'];

/** A source profile with its picks: the source each variable reads under the profile. */
export type SourceProfile = Schemas['SourceProfile'];

/** One pick of a source profile. */
export type SourceProfilePick = Schemas['SourceProfilePick'];

/** A dimension the report can break out or filter by, with its grouping (`qe_calculation_dimension`). */
export type CalculationDimension = Schemas['CalculationDimensionRecord'];

/** How one dataset's rows classify into a calculation dimension (`qe_calculation_dimension_binding`). */
export type CalculationRoute = Schemas['CalculationRouteRecord'];

/** How a source or route classifies a dataset's rows into a dimension: the kind, the field and the mapping set or bridge. */
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

/** One overlay row of `PUT report-versions/{versionId}/lines`, naming its parent, measure and column by key. */
export type ReportLineInput = Schemas['ReportLineInput'];

/** One option of `PUT report-versions/{versionId}/member-options`. */
export type ReportMemberOptionInput = Schemas['ReportMemberOptionInput'];

/** One target of `PUT report-versions/{versionId}/variable-targets`. */
export type ReportVariableTargetInput = Schemas['ReportVariableTargetInput'];
