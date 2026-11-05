import type { TenantApi, TenantCatalog as FullTenantCatalog, Vocabularies as AllVocabularies } from '@/lib/tenant/contract'

export type { PublishedMembers } from '@/lib/tenant/contract'

/**
 * The tenant-wide lookups the dataset screens read: a projection of `TenantApi` (blueprint 10), which the tenant module's
 * adapter satisfies as is.
 */

export type Vocabularies = Pick<
  AllVocabularies,
  | 'fieldSourceKind'
  | 'dataType'
  | 'measureOutputDataType'
  | 'grainKind'
  | 'bindingKind'
  | 'raggedBehavior'
  | 'periodAlignment'
  | 'unmappedBehavior'
  | 'timeValueType'
  | 'observationKind'
  | 'periodicity'
  | 'cumulativeOrigin'
  | 'measureKind'
  | 'compoundingForm'
  | 'aggregationFunction'
  | 'componentRole'
  | 'componentRolesByKind'
  | 'valueNullBehavior'
  | 'negativeWeightBehavior'
  | 'emptySetBehavior'
  | 'bridgeFactorBehavior'
  | 'bridgeAllocationBehavior'
  | 'totalBehavior'
  | 'roundingMode'
  | 'observationSelection'
  | 'valueTimeRole'
  | 'boundaryMatch'
  | 'durationUnit'
  | 'partialIntervalBehavior'
  | 'missingPeriodBehavior'
  | 'sourceObjectKind'
  | 'expressionLanguageVersion'
  | 'timeZone'
>

export type TenantCatalog = Pick<FullTenantCatalog, 'dimensions' | 'roles' | 'mappingSets' | 'bridges'>

export interface TenantLookups extends Pick<TenantApi, 'getPublishedMembers'> {
  getCatalog(): Promise<TenantCatalog>
  getVocabularies(): Promise<Vocabularies>
}
