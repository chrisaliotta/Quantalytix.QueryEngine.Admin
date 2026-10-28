import type { TenantApi, TenantCatalogDimension } from '@/lib/tenant/contract'

/**
 * The tenant-wide lookups the mapping and bridge editors read (OI-13): a projection of `TenantApi` (blueprint 10), which the tenant
 * module's adapter satisfies as is.
 */

export type MappingCatalogDimension = Pick<TenantCatalogDimension, 'id' | 'key' | 'displayName' | 'kind' | 'purpose' | 'publishedVersionId' | 'publishedVersionNumber'>

export interface MappingLookups extends Pick<TenantApi, 'getPublishedMembers'> {
  getCatalog(): Promise<{ dimensions: MappingCatalogDimension[] }>
}
