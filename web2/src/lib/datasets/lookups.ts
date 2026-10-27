import type { TenantApi, TenantCatalog as FullTenantCatalog, Vocabularies as AllVocabularies } from '@/lib/tenant/contract'

export type { PublishedMembers } from '@/lib/tenant/contract'

/**
 * The tenant-wide lookups the dataset screens read: a projection of `TenantApi` (blueprint 10), so the screens take
 * a `TenantApi` as is once that module serves them (P5).
 */

export type Vocabularies = Pick<
  AllVocabularies,
  | 'versionStatus'
  | 'fieldSourceKind'
  | 'dataType'
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

export type TenantCatalog = Pick<FullTenantCatalog, 'dimensions' | 'roles' | 'mappingSets' | 'bridges' | 'releases'>

export interface TenantLookups extends Pick<TenantApi, 'getPublishedMembers'> {
  getCatalog(): Promise<TenantCatalog>
  getVocabularies(): Promise<Vocabularies>
}
