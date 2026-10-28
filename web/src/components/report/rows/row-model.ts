import type { ReportDocument, ReportRow, ReportRowKind } from '@/lib/reports/contract'

/** Member rows are the report dimension's members; section and formula rows are overlay lines (OI-48). */
export const isMemberKind = (kind: ReportRowKind) => kind === 'data' || kind === 'group'

/** The rows under a parent, in order. */
export const childrenOf = (report: ReportDocument, parentKey: string | null) => report.rows.filter((r) => r.parentKey === parentKey)

/** A row's index among its siblings. */
export const indexOf = (report: ReportDocument, row: ReportRow) => childrenOf(report, row.parentKey).findIndex((r) => r.key === row.key)

/** How deep a row sits: 0 for the total. */
export function depthOf(report: ReportDocument, row: ReportRow) {
  let depth = 0
  for (let parent = row.parentKey; parent !== null; parent = report.rows.find((r) => r.key === parent)!.parentKey) depth++
  return depth
}

/** A new row of a kind under a parent; a formula row starts on a calculated measure and the report's first column. */
export function newRow(report: ReportDocument, kind: ReportRowKind, parentKey: string, measureKey: string | null): ReportRow {
  const taken = new Set(report.rows.map((r) => r.key))
  let n = 1
  while (taken.has(`ROW_${n}`)) n++
  return {
    key: `ROW_${n}`,
    label: 'New row',
    kind,
    parentKey,
    isVisible: true,
    showsValues: true,
    calculatedMeasureKey: kind === 'formula' ? measureKey : null,
    valueColumnKey: kind === 'formula' ? (report.columns[0]?.key ?? null) : null,
    styleKey: null,
    parameters: {},
  }
}
