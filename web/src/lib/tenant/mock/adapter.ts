import type { TenantApi, TenantOverview } from '../contract'
import { catalog, liveMeasureCount, publishedMembers, vocabularies } from './data'

/** Counts the catalog's identities as the API does: report-owned dimensions and bridges are not counted (OI-22). */
function overview(): TenantOverview {
  return {
    ...catalog.tenant,
    counts: {
      datasets: catalog.datasets.length,
      dimensions: catalog.dimensions.filter((d) => d.purpose === 'analytical').length,
      mappingSets: catalog.mappingSets.length,
      bridges: catalog.bridges.filter((b) => b.ownerReportId === null).length,
      frozenSnapshots: catalog.snapshots.filter((s) => !s.isLive).length,
      models: catalog.models.length,
      reports: catalog.reports.length,
      liveMeasures: liveMeasureCount,
    },
  }
}

export function createMockTenantApi(): TenantApi {
  return {
    async getOverview() {
      return structuredClone(overview())
    },
    async getCatalog() {
      return structuredClone(catalog)
    },
    async getPublishedMembers(dimensionId) {
      const dimension = catalog.dimensions.find((d) => d.id === dimensionId)
      if (!dimension) return null
      return { dimensionId, versionId: dimension.liveVersionId, members: structuredClone(publishedMembers[dimensionId]) }
    },
    async getVocabularies() {
      return structuredClone(vocabularies)
    },
  }
}
