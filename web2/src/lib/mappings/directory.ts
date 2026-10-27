import type { DimensionsApi } from '@/lib/dimensions/contract'
import type { MappingDimensionRef, MappingsDirectory, MemberRef } from './contract'

/**
 * The directory over the dimensions port's deprecated `listAllDimensions`. In P5 it switches to `TenantApi.getCatalog`
 * and `getPublishedMembers` (OI-13), and `listAllDimensions` is removed.
 */
export function createDimensionsDirectory(dims: DimensionsApi): MappingsDirectory {
  async function listDimensionRefs(): Promise<MappingDimensionRef[]> {
    return (await dims.listAllDimensions()).map((d) => {
      const live = d.versions.find((v) => v.status === 'published')
      return { id: d.id, key: d.key, displayName: d.displayName, kind: d.kind, purpose: d.purpose, publishedVersionId: live?.id ?? null, publishedVersionNumber: live?.versionNumber ?? null }
    })
  }

  async function getPublishedMembers(dimensionId: string): Promise<MemberRef[] | null> {
    const ref = (await listDimensionRefs()).find((d) => d.id === dimensionId)!
    if (!ref.publishedVersionId) return null
    const r = await dims.getVersion(ref.publishedVersionId)
    if (!r.ok) throw new Error(r.problem.title)
    const { members } = r.data
    const keyById = new Map(members.map((m) => [m.id, m.key]))
    return [...members]
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map((m) => ({ key: m.key, displayName: m.displayName, parentKey: m.parentId ? (keyById.get(m.parentId) ?? null) : null, isAssignable: m.isAssignable }))
  }

  return { listDimensionRefs, getPublishedMembers }
}
