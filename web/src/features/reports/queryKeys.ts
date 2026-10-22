// Query keys start with [resource, tenantId] so invalidateTenantQueries reaches them.

/** The key of every report, which a report write refreshes. */
export function reportsKey(tenantId: string) {
  return ['reports', tenantId] as const;
}

/** The key of one report, with its settings and whether it is enabled. */
export function reportKey(tenantId: string, reportId: number) {
  return [...reportsKey(tenantId), reportId] as const;
}

/** The key of a report's editor view, which a column write refreshes because formula rows name their column. */
export function reportEditorKey(tenantId: string, reportId: number) {
  return ['report-editor', tenantId, reportId] as const;
}

/** The key of a report's variable scopes, which a row write refreshes because removing a row removes its scopes. */
export function reportVariableScopesKey(tenantId: string, reportId: number) {
  return ['report-variable-scopes', tenantId, reportId] as const;
}

/** The key of a report's columns. */
export function reportColumnsKey(tenantId: string, reportId: number) {
  return ['report-columns', tenantId, reportId] as const;
}

/** The key of a report's styles, which its rows name. */
export function reportStylesKey(tenantId: string, reportId: number) {
  return ['report-styles', tenantId, reportId] as const;
}
