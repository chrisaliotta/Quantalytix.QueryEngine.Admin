/** The tabs of a dimension's page that other pages open it on. */
export type DimensionTab = 'mappings' | 'bridges';

/** A dimension's page, on one of its tabs when one is given. */
export function dimensionPath(tenantId: string, dimensionId: number, tab?: DimensionTab) {
  return `/t/${tenantId}/dimensions/${dimensionId}${tab ? `?tab=${tab}` : ''}`;
}

/** A mapping set's page, under the dimension its codes map onto. */
export function mappingSetPath(tenantId: string, dimensionId: number, mappingSetId: number) {
  return `${dimensionPath(tenantId, dimensionId)}/mapping-sets/${mappingSetId}`;
}

/** An analytical bridge's page, under the dimension it leads into. */
export function bridgePath(tenantId: string, targetDimensionId: number, bridgeId: number) {
  return `${dimensionPath(tenantId, targetDimensionId)}/bridges/${bridgeId}`;
}
