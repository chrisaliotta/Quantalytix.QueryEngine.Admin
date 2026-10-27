import type { TenantLookups } from '../lookups'
import { catalog, publishedMembers, vocabularies } from './data'

const delay = () => new Promise((r) => setTimeout(r, 90 + Math.random() * 120))

export function createMockTenantLookups(): TenantLookups {
  return {
    async getCatalog() {
      await delay()
      return structuredClone(catalog)
    },
    async getPublishedMembers(dimensionId) {
      await delay()
      return structuredClone(publishedMembers[dimensionId] ?? null)
    },
    async getVocabularies() {
      await delay()
      return structuredClone(vocabularies)
    },
  }
}
