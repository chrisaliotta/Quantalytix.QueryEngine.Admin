import { createSeed } from '@/lib/dimensions/mock/data'
import type { MappingLookups } from '../lookups'

const delay = () => new Promise((r) => setTimeout(r, 60))

/**
 * Mock lookups over the dimensions mock's seed, whose dimension ids the mappings mock names. Report dimensions are included so report
 * bridges resolve their targets.
 */
export function createMockMappingLookups(): MappingLookups {
  const db = createSeed()
  const liveOf = (dimensionId: string) => db.versions.find((v) => v.dimensionId === dimensionId && v.status === 'published')

  return {
    async getCatalog() {
      await delay()
      return {
        dimensions: db.dimensions.map(({ id, key, displayName, kind, purpose }) => {
          const live = liveOf(id)
          return { id, key, displayName, kind, purpose, publishedVersionId: live?.id ?? null, publishedVersionNumber: live?.versionNumber ?? null }
        }),
      }
    },
    async getPublishedMembers(dimensionId) {
      await delay()
      const live = liveOf(dimensionId)
      if (!live) return null
      const members = db.members.filter((m) => m.versionId === live.id).sort((a, b) => a.sortOrder - b.sortOrder)
      const keyOf = new Map(members.map((m) => [m.id, m.key]))
      const levelKeyOf = new Map(db.levels.filter((l) => l.versionId === live.id).map((l) => [l.id, l.key]))
      return {
        dimensionId,
        versionId: live.id,
        members: members.map((m) => ({
          key: m.key,
          displayName: m.displayName,
          parentKey: m.parentId ? keyOf.get(m.parentId)! : null,
          isAssignable: m.isAssignable,
          levelKey: m.levelId ? levelKeyOf.get(m.levelId)! : null,
        })),
      }
    },
  }
}
