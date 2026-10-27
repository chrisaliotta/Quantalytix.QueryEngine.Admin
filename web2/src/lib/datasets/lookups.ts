import type { Id, VersionStatus, VocabularyTerm } from '@/lib/api/contract'
import type {
  DatasetAggregationFunction,
  DatasetBindingKind,
  DatasetBoundaryMatch,
  DatasetBridgeAllocationBehavior,
  DatasetBridgeFactorBehavior,
  DatasetComponentRole,
  DatasetCumulativeOrigin,
  DatasetDataType,
  DatasetDurationUnit,
  DatasetEmptySetBehavior,
  DatasetFieldSourceKind,
  DatasetGrainKind,
  DatasetMeasureKind,
  DatasetMissingPeriodBehavior,
  DatasetNegativeWeightBehavior,
  DatasetObservationKind,
  DatasetObservationSelection,
  DatasetPartialIntervalBehavior,
  DatasetPeriodAlignment,
  DatasetPeriodicity,
  DatasetRaggedBehavior,
  DatasetRoundingMode,
  DatasetSourceObjectKind,
  DatasetTimeValueType,
  DatasetTotalBehavior,
  DatasetUnmappedBehavior,
  DatasetValueNullBehavior,
  DatasetValueTimeRole,
} from './contract'

/**
 * The tenant-wide lookups the dataset screens read: a projection of `TenantApi` (blueprint 10). These types follow
 * blueprint 10's shapes so the screens switch to `lib/tenant` once that module serves them (P5).
 */

export type DimensionKind = 'categorical' | 'hierarchical' | 'range' | 'time'

export interface Vocabularies {
  versionStatus: VocabularyTerm<VersionStatus>[]
  fieldSourceKind: VocabularyTerm<DatasetFieldSourceKind>[]
  dataType: VocabularyTerm<DatasetDataType>[]
  grainKind: VocabularyTerm<DatasetGrainKind>[]
  bindingKind: VocabularyTerm<DatasetBindingKind>[]
  raggedBehavior: VocabularyTerm<DatasetRaggedBehavior>[]
  periodAlignment: VocabularyTerm<DatasetPeriodAlignment>[]
  unmappedBehavior: VocabularyTerm<DatasetUnmappedBehavior>[]
  timeValueType: VocabularyTerm<DatasetTimeValueType>[]
  observationKind: VocabularyTerm<DatasetObservationKind>[]
  periodicity: VocabularyTerm<DatasetPeriodicity>[]
  cumulativeOrigin: VocabularyTerm<DatasetCumulativeOrigin>[]
  measureKind: VocabularyTerm<DatasetMeasureKind>[]
  aggregationFunction: VocabularyTerm<DatasetAggregationFunction>[]
  componentRole: VocabularyTerm<DatasetComponentRole>[]
  /** Which component roles each measure kind reads, and which of those are required. */
  componentRolesByKind: Record<DatasetMeasureKind, { role: DatasetComponentRole; required: boolean }[]>
  valueNullBehavior: VocabularyTerm<DatasetValueNullBehavior>[]
  negativeWeightBehavior: VocabularyTerm<DatasetNegativeWeightBehavior>[]
  emptySetBehavior: VocabularyTerm<DatasetEmptySetBehavior>[]
  bridgeFactorBehavior: VocabularyTerm<DatasetBridgeFactorBehavior>[]
  bridgeAllocationBehavior: VocabularyTerm<DatasetBridgeAllocationBehavior>[]
  totalBehavior: VocabularyTerm<DatasetTotalBehavior>[]
  roundingMode: VocabularyTerm<DatasetRoundingMode>[]
  observationSelection: VocabularyTerm<DatasetObservationSelection>[]
  valueTimeRole: VocabularyTerm<DatasetValueTimeRole>[]
  boundaryMatch: VocabularyTerm<DatasetBoundaryMatch>[]
  durationUnit: VocabularyTerm<DatasetDurationUnit>[]
  partialIntervalBehavior: VocabularyTerm<DatasetPartialIntervalBehavior>[]
  missingPeriodBehavior: VocabularyTerm<DatasetMissingPeriodBehavior>[]
  sourceObjectKind: VocabularyTerm<DatasetSourceObjectKind>[]
  expressionLanguageVersion: VocabularyTerm[]
  timeZone: VocabularyTerm[]
}

export interface TenantCatalog {
  dimensions: { id: Id; key: string; displayName: string; kind: DimensionKind }[]
  roles: { id: Id; key: string; displayName: string; dimensionId: Id }[]
  mappingSets: { id: Id; key: string; displayName: string; dimensionId: Id }[]
  bridges: { id: Id; key: string; displayName: string; sourceDimensionId: Id; targetDimensionId: Id }[]
  releases: { id: Id; key: string; status: VersionStatus; isActive: boolean }[]
}

export interface PublishedMembers {
  dimensionId: Id
  versionId: Id
  members: { key: string; displayName: string }[]
}

export interface TenantLookups {
  getCatalog(): Promise<TenantCatalog>
  /** Members of a dimension's published version, or null when none is published. */
  getPublishedMembers(dimensionId: Id): Promise<PublishedMembers | null>
  getVocabularies(): Promise<Vocabularies>
}
