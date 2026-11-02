import type { Id, MappingDimensionRef, MappingsDirectory, MemberRef } from './contract'
import type { MappingLookups } from './lookups'

/** The directory over the tenant catalog and live members (OI-13). */
export function createTenantDirectory(lookups: MappingLookups): MappingsDirectory {
  return {
    async listDimensionRefs(): Promise<MappingDimensionRef[]> {
      const { dimensions } = await lookups.getCatalog()
      return dimensions.map(({ id, key, displayName, kind, purpose, liveVersionId }) => ({ id, key, displayName, kind, purpose, versionId: liveVersionId }))
    },
    async getPublishedMembers(dimensionId: Id): Promise<MemberRef[] | null> {
      const published = await lookups.getPublishedMembers(dimensionId)
      return published && published.members.map(({ key, displayName, parentKey, isAssignable }) => ({ key, displayName, parentKey, isAssignable }))
    },
  }
}
