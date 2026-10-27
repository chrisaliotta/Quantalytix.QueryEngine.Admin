import type { TenantApi } from '../contract'
import { catalog, overview, publishedMembers, vocabularies } from './data'

export function createMockTenantApi(): TenantApi {
  return {
    async getOverview() {
      return structuredClone({ ...overview, ...catalog.tenant })
    },
    async getCatalog() {
      return structuredClone(catalog)
    },
    async getPublishedMembers(dimensionId) {
      const dimension = catalog.dimensions.find((d) => d.id === dimensionId)
      if (!dimension?.publishedVersionId) return null
      return { dimensionId, versionId: dimension.publishedVersionId, members: structuredClone(publishedMembers[dimensionId]) }
    },
    async getVocabularies() {
      return structuredClone(vocabularies)
    },
  }
}
