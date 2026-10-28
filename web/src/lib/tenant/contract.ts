import type {
  AssumptionSetStatus,
  BindingKind,
  ComponentRole,
  DefinitionOrigin,
  DimensionKind,
  DimensionPurpose,
  Id,
  MeasureKind,
  Periodicity,
  RoundingMode,
  SourceSelection,
  VariableMissingBehavior,
  VersionStatus,
  VocabularyTerm,
} from '@/lib/api/contract'

export type {
  AssumptionSetStatus,
  BindingKind,
  ComponentRole,
  DefinitionOrigin,
  DimensionKind,
  DimensionPurpose,
  Id,
  MeasureKind,
  Periodicity,
  RoundingMode,
  SourceSelection,
  VariableMissingBehavior,
  VersionStatus,
  VocabularyTerm,
} from '@/lib/api/contract'

/**
 * Tenant contract (blueprint 10): the tenant's state, the one catalog every module's pickers and cross-module links
 * read (OI-13), published members per dimension, and every vocabulary. Object types mirror the schemas of the same
 * name in api/openapi/contract/tenant.yaml. Unions carry the vocabulary's name; the ones more than one module reads
 * live in the shared kernel.
 */

/* ───────────────────────── Vocabulary values ───────────────────────── */

export type DataType = 'boolean' | 'int32' | 'int64' | 'decimal' | 'string' | 'date' | 'datetime' | 'timestamp' | 'json'
export type MeasureOutputDataType = Exclude<DataType, 'json'>
export type SourceObjectKind = 'table' | 'view'
export type FieldSourceKind = 'column' | 'json_path' | 'expression'
export type GrainKind = 'row' | 'series'
export type RangeDataType = 'decimal' | 'date' | 'datetime'
export type EditPolicy = 'open' | 'extend' | 'locked'
export type GroupingMode = 'member' | 'level' | 'ancestor' | 'depth'
export type RaggedBehavior = 'null' | 'self' | 'exclude'
export type PeriodAlignment = 'observation' | 'start' | 'end'
export type UnmappedBehavior = 'null' | 'exclude' | 'unknown'
export type TimeValueType = 'date' | 'timestamp'
export type ObservationKind = 'instant' | 'snapshot' | 'interval'
export type CumulativeOrigin = 'output_range_start' | 'fixed_start'
export type AggregationFunction = 'sum' | 'avg' | 'min' | 'max' | 'count' | 'count_distinct'
export type ValueNullBehavior = 'exclude' | 'zero'
export type NegativeWeightBehavior = 'allow' | 'exclude'
export type EmptySetBehavior = 'null' | 'zero'
export type BridgeFactorBehavior = 'ignore' | 'value'
export type BridgeAllocationBehavior = 'apply' | 'ignore'
export type TotalBehavior = 'recompute' | 'sum_children'
export type ObservationSelection = 'all' | 'first_in_period' | 'last_in_period' | 'at_start' | 'at_end'
export type ValueTimeRole = 'point' | 'interval_open' | 'interval_close'
export type BoundaryMatch = 'exact' | 'on_or_before' | 'strictly_before'
export type DurationUnit = 'calendar_day' | 'elapsed_second'
export type PartialIntervalBehavior = 'exact_only' | 'uniform_estimate'
export type MissingPeriodBehavior = 'null' | 'zero'
export type Cardinality = 'many_to_one' | 'many_to_many'
export type AllocationMode = 'none' | 'partition' | 'replicate'
export type FormulaLanguageVersion = '1.0' | '1.1' | '1.2'
export type AttributionMethod = 'shapley' | 'sequential'
export type ParameterValueType = 'number' | 'text'
export type DayCountConvention = 'actual_actual' | 'actual_365' | 'actual_360' | 'thirty_360_us' | 'thirty_e_360'
export type MappingMethod = 'bridge' | 'direct'
export type SelectionScope = 'exact' | 'descendants'
export type ColumnValueKind = 'variable' | 'calculated'
export type SubtotalPosition = 'above' | 'below'
export type StyleRole = 'header' | 'subtotal' | 'total' | 'detail' | 'formula'
export type FontWeight = 'normal' | 'medium' | 'semibold' | 'bold'
export type BorderStyle = 'none' | 'thin' | 'medium' | 'thick' | 'double'
export type ReportLayout = 'statement' | 'list'
export type Provider = 'starrocks'

/** The component roles a measure kind reads. */
export type VocabularyComponentRoleRequirements = { role: ComponentRole; required: boolean }[]

/**
 * Every enumeration a select renders (OI-11). Labels and option lists come from here; the UI never hard-codes them.
 * `expressionLanguageVersion`, `displayFormat` and `timeZone` offer values of free-text columns.
 */
export interface Vocabularies {
  versionStatus: VocabularyTerm<VersionStatus>[]
  assumptionSetStatus: VocabularyTerm<AssumptionSetStatus>[]
  dataType: VocabularyTerm<DataType>[]
  measureOutputDataType: VocabularyTerm<MeasureOutputDataType>[]
  sourceObjectKind: VocabularyTerm<SourceObjectKind>[]
  fieldSourceKind: VocabularyTerm<FieldSourceKind>[]
  grainKind: VocabularyTerm<GrainKind>[]
  dimensionKind: VocabularyTerm<DimensionKind>[]
  dimensionPurpose: VocabularyTerm<DimensionPurpose>[]
  definitionOrigin: VocabularyTerm<DefinitionOrigin>[]
  rangeDataType: VocabularyTerm<RangeDataType>[]
  editPolicy: VocabularyTerm<EditPolicy>[]
  bindingKind: VocabularyTerm<BindingKind>[]
  groupingMode: VocabularyTerm<GroupingMode>[]
  raggedBehavior: VocabularyTerm<RaggedBehavior>[]
  periodAlignment: VocabularyTerm<PeriodAlignment>[]
  unmappedBehavior: VocabularyTerm<UnmappedBehavior>[]
  timeValueType: VocabularyTerm<TimeValueType>[]
  observationKind: VocabularyTerm<ObservationKind>[]
  periodicity: VocabularyTerm<Periodicity>[]
  cumulativeOrigin: VocabularyTerm<CumulativeOrigin>[]
  measureKind: VocabularyTerm<MeasureKind>[]
  aggregationFunction: VocabularyTerm<AggregationFunction>[]
  componentRole: VocabularyTerm<ComponentRole>[]
  componentRolesByKind: Record<MeasureKind, VocabularyComponentRoleRequirements>
  valueNullBehavior: VocabularyTerm<ValueNullBehavior>[]
  negativeWeightBehavior: VocabularyTerm<NegativeWeightBehavior>[]
  emptySetBehavior: VocabularyTerm<EmptySetBehavior>[]
  bridgeFactorBehavior: VocabularyTerm<BridgeFactorBehavior>[]
  bridgeAllocationBehavior: VocabularyTerm<BridgeAllocationBehavior>[]
  totalBehavior: VocabularyTerm<TotalBehavior>[]
  roundingMode: VocabularyTerm<RoundingMode>[]
  observationSelection: VocabularyTerm<ObservationSelection>[]
  valueTimeRole: VocabularyTerm<ValueTimeRole>[]
  boundaryMatch: VocabularyTerm<BoundaryMatch>[]
  durationUnit: VocabularyTerm<DurationUnit>[]
  partialIntervalBehavior: VocabularyTerm<PartialIntervalBehavior>[]
  missingPeriodBehavior: VocabularyTerm<MissingPeriodBehavior>[]
  expressionLanguageVersion: VocabularyTerm[]
  timeZone: VocabularyTerm[]
  cardinality: VocabularyTerm<Cardinality>[]
  allocationMode: VocabularyTerm<AllocationMode>[]
  sourceSelection: VocabularyTerm<SourceSelection>[]
  formulaLanguageVersion: VocabularyTerm<FormulaLanguageVersion>[]
  attributionMethod: VocabularyTerm<AttributionMethod>[]
  parameterValueType: VocabularyTerm<ParameterValueType>[]
  variableMissingBehavior: VocabularyTerm<VariableMissingBehavior>[]
  dayCountConvention: VocabularyTerm<DayCountConvention>[]
  displayFormat: VocabularyTerm[]
  mappingMethod: VocabularyTerm<MappingMethod>[]
  selectionScope: VocabularyTerm<SelectionScope>[]
  columnValueKind: VocabularyTerm<ColumnValueKind>[]
  subtotalPosition: VocabularyTerm<SubtotalPosition>[]
  styleRole: VocabularyTerm<StyleRole>[]
  fontWeight: VocabularyTerm<FontWeight>[]
  borderStyle: VocabularyTerm<BorderStyle>[]
  layout: VocabularyTerm<ReportLayout>[]
  provider: VocabularyTerm<Provider>[]
}

/* ───────────────────────── Tenant state ───────────────────────── */

export interface TenantInfo {
  tenantKey: string
  metadataSchemaVersion: number
  /** The engine's cache key; clients revalidate the catalog when it changes. */
  semanticRevision: number
  activeReleaseId: Id | null
}

export interface TenantOverviewCounts {
  datasets: number
  /** Analytical dimensions only (OI-22). */
  dimensions: number
  mappingSets: number
  /** Analytical bridges only (OI-22). */
  bridges: number
  releases: number
  assumptionSets: number
  models: number
  reports: number
  /** Measures of the dataset versions the active release pins. */
  activeMeasures: number
}

/** Kinds that still have drafts; dimensions, mapping sets and bridges follow the living lifecycle (OI-14). */
export interface TenantOverviewDrafts {
  datasetVersions: number
  releases: number
}

export interface TenantOverview extends TenantInfo {
  counts: TenantOverviewCounts
  drafts: TenantOverviewDrafts
}

/* ───────────────────────── Catalog ───────────────────────── */

export interface TenantCatalogDataset {
  id: Id
  key: string
  displayName: string
  isEnabled: boolean
  /** The dataset version the active release pins. */
  activeVersionId: Id | null
}

export interface TenantCatalogDimension {
  id: Id
  key: string
  displayName: string
  kind: DimensionKind
  purpose: DimensionPurpose
  origin: DefinitionOrigin
  isEnabled: boolean
  publishedVersionId: Id | null
  publishedVersionNumber: number | null
  ownerReportId: Id | null
}

export interface TenantCatalogRole {
  id: Id
  key: string
  displayName: string
  dimensionId: Id
}

export interface TenantCatalogMappingSet {
  id: Id
  key: string
  displayName: string
  dimensionId: Id
}

export interface TenantCatalogBridge {
  id: Id
  key: string
  displayName: string
  sourceDimensionId: Id
  targetDimensionId: Id
  ownerReportId: Id | null
}

export interface TenantCatalogModel {
  id: Id
  key: string
  displayName: string
  isEnabled: boolean
}

export interface TenantCatalogReport {
  id: Id
  key: string
  displayName: string
  isEnabled: boolean
  modelId: Id
  reportDimensionId: Id
}

/** A release has a key and no display name (OI-39). */
export interface TenantCatalogRelease {
  id: Id
  key: string
  status: VersionStatus
  isActive: boolean
}

export interface TenantCatalogAssumptionSet {
  id: Id
  key: string
  displayName: string
  status: AssumptionSetStatus
}

/**
 * Names and ids for pickers and cross-module links, loaded once and cached on `tenant.semanticRevision`.
 * Report-owned dimensions and bridges (`ownerReportId` set) are here only so references resolve; no picker or list
 * offers them (OI-22).
 */
export interface TenantCatalog {
  tenant: TenantInfo
  datasets: TenantCatalogDataset[]
  dimensions: TenantCatalogDimension[]
  roles: TenantCatalogRole[]
  mappingSets: TenantCatalogMappingSet[]
  bridges: TenantCatalogBridge[]
  models: TenantCatalogModel[]
  reports: TenantCatalogReport[]
  releases: TenantCatalogRelease[]
  assumptionSets: TenantCatalogAssumptionSet[]
}

export interface PublishedMember {
  key: string
  displayName: string
  parentKey: string | null
  isAssignable: boolean
  levelKey: string | null
}

export interface PublishedMembers {
  dimensionId: Id
  versionId: Id
  members: PublishedMember[]
}

/* ───────────────────────── Port ───────────────────────── */

export interface TenantApi {
  /** `GET /api/tenants/{t}/overview` */
  getOverview(): Promise<TenantOverview>
  /** `GET /api/tenants/{t}/catalog` */
  getCatalog(): Promise<TenantCatalog>
  /** Members of a dimension's published version, or null when none is published. */
  getPublishedMembers(dimensionId: Id): Promise<PublishedMembers | null>
  /** `GET /api/vocabularies` */
  getVocabularies(): Promise<Vocabularies>
}
