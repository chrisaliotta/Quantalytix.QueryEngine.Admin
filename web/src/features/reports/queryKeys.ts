// Query keys start with [resource, tenantId] so invalidateTenantQueries reaches them.

/** The key of one report version, which a design's source writes refresh when they give it its default profile. */
export function reportVersionKey(tenantId: string, versionId: number) {
  return ['report-versions', tenantId, versionId] as const;
}

/** The key of a report version's design, which a column write refreshes because formula rows name their column. */
export function reportDesignKey(tenantId: string, versionId: number) {
  return ['report-design', tenantId, versionId] as const;
}

/** The key of a report version's variable scopes, which a design's row write refreshes because removing a row removes its scopes. */
export function reportVariableScopesKey(tenantId: string, versionId: number) {
  return ['report-variable-scopes', tenantId, versionId] as const;
}
