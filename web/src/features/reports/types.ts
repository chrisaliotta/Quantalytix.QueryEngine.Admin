import type { components } from '@/api/schema';

type Schemas = components['schemas'];

/** A report: the line and column layout the engine executes over a calculation model (`qe_report`). */
export type Report = Schemas['ReportRecord'];

/** A version of a report over one model version, with its defaults (`qe_report_version`). */
export type ReportVersion = Schemas['ReportVersionRecord'];

/** A column of a report version: the model metric it returns (`qe_report_column`). */
export type ReportColumn = Schemas['ReportColumnRecord'];

/** A line of a report version: a section, a report-item member or a formula (`qe_report_line`). */
export type ReportLine = Schemas['ReportLineRecord'];

/** Body of `POST reports`. */
export type NewReportInput = Schemas['NewReportInput'];

/** Body of `PUT reports/{reportId}`. */
export type ReportInput = Schemas['ReportInput'];

/** Body of `POST reports/{reportId}/versions`. */
export type NewReportVersionInput = Schemas['NewReportVersionInput'];

/** Body of `PUT report-versions/{versionId}`. */
export type ReportVersionInput = Schemas['ReportVersionInput'];

/** Answer of `POST reports/{reportId}/versions`: the new draft and the columns and lines a clone dropped. */
export type CreatedReportVersion = Schemas['CreatedReportVersion'];

/** One column of `PUT report-versions/{versionId}/columns`, naming its metric by key. */
export type ReportColumnInput = Schemas['ReportColumnInput'];

/** One line of `PUT report-versions/{versionId}/lines`, naming its parent, measure and column by key. */
export type ReportLineInput = Schemas['ReportLineInput'];
