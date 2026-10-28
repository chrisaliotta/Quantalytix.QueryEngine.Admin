import type { TenantApi, TenantOverview } from '../contract'
import { catalog, overviewRows, publishedMembers, vocabularies } from './data'

/** Counts the catalog's identities as the API does: report-owned dimensions and bridges are not counted (OI-22). */
function overview(): TenantOverview {
  return {
    ...catalog.tenant,
    counts: {
      datasets: catalog.datasets.length,
      dimensions: catalog.dimensions.filter((d) => d.purpose === 'analytical').length,
      mappingSets: catalog.mappingSets.length,
      bridges: catalog.bridges.filter((b) => b.ownerReportId === null).length,
      releases: catalog.releases.length,
      assumptionSets: catalog.assumptionSets.length,
      models: catalog.models.length,
      reports: catalog.reports.length,
      activeMeasures: overviewRows.activeMeasures,
    },
    drafts: { datasetVersions: overviewRows.draftDatasetVersions, releases: catalog.releases.filter((r) => r.status === 'draft').length },
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
      if (!dimension?.publishedVersionId) return null
      return { dimensionId, versionId: dimension.publishedVersionId, members: structuredClone(publishedMembers[dimensionId]) }
    },
    async getVocabularies() {
      return structuredClone(vocabularies)
    },
  }
}
