import type { ChangeSetPort } from '@/lib/api/changes'

/**
 * Business contract for the dataset manager.
 *
 * The UI depends only on these types and on the `DatasetsApi` port. A concrete adapter (the in-memory mock today,
 * an HTTP client later) implements the port and owns every validation decision: the UI renders what the adapter
 * returns and never checks input itself.
 */

/* ───────────────────────── Vocabularies ───────────────────────── */

export type VersionStatus = 'draft' | 'validated' | 'published' | 'retired'
export type FieldSourceKind = 'column' | 'json_path' | 'expression'
export type DataType = 'boolean' | 'int32' | 'int64' | 'decimal' | 'string' | 'date' | 'datetime' | 'timestamp' | 'json'
export type GrainKind = 'row' | 'series'
export type BindingKind = 'direct' | 'mapped' | 'range' | 'bridge' | 'time'
export type RaggedBehavior = 'self' | 'exclude'
export type PeriodAlignment = 'observation' | 'start' | 'end'
export type UnmappedBehavior = 'exclude' | 'unknown'
export type TimeValueType = 'date' | 'timestamp'
export type ObservationKind = 'instant' | 'snapshot' | 'interval'
export type Periodicity = 'day' | 'month' | 'quarter' | 'year'
export type CumulativeOrigin = 'output_range_start' | 'fixed_start'
export type MeasureKind = 'aggregate' | 'weighted_sum' | 'weighted_average' | 'period_average' | 'formula' | 'specialized'
export type AggregationFunction = 'sum' | 'avg' | 'min' | 'max' | 'count' | 'count_distinct'
export type ComponentRole = 'value' | 'weight' | 'period_days' | 'numerator' | 'denominator'
export type ValueNullBehavior = 'exclude' | 'zero'
export type NegativeWeightBehavior = 'allow' | 'exclude'
export type EmptySetBehavior = 'null' | 'zero'
export type BridgeFactorBehavior = 'ignore' | 'value'
export type BridgeAllocationBehavior = 'apply' | 'ignore'
export type TotalBehavior = 'recompute' | 'sum_children'
export type RoundingMode = 'half_up' | 'half_even' | 'down' | 'up'
export type ObservationSelection = 'all' | 'first_in_period' | 'last_in_period' | 'at_start' | 'at_end'
export type ValueTimeRole = 'point' | 'interval_open' | 'interval_close'
export type BoundaryMatch = 'exact' | 'on_or_before' | 'strictly_before'
export type DurationUnit = 'calendar_day' | 'elapsed_second'
export type PartialIntervalBehavior = 'exact_only' | 'uniform_estimate'
export type MissingPeriodBehavior = 'null' | 'zero'
export type SourceObjectKind = 'table' | 'view'
export type DimensionKind = 'standard' | 'range' | 'time'

/** Server-supplied enumerations. Selects render these; the UI never hard-codes a list. */
export interface VocabularyTerm<V extends string = string> {
  value: V
  label: string
  description?: string
}

export interface Vocabularies {
  versionStatus: VocabularyTerm<VersionStatus>[]
  fieldSourceKind: VocabularyTerm<FieldSourceKind>[]
  dataType: VocabularyTerm<DataType>[]
  grainKind: VocabularyTerm<GrainKind>[]
  bindingKind: VocabularyTerm<BindingKind>[]
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
  /** Which component roles each measure kind reads, and which of those are required. */
  componentRolesByKind: Record<MeasureKind, { role: ComponentRole; required: boolean }[]>
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
  sourceObjectKind: VocabularyTerm<SourceObjectKind>[]
  expressionLanguageVersion: VocabularyTerm[]
  timeZone: VocabularyTerm[]
}

/* ───────────────────────── Tenant-wide lookups ───────────────────────── */

export interface DimensionMember {
  key: string
  label: string
}

export interface Dimension {
  id: string
  dimensionKey: string
  displayName: string
  dimensionKind: DimensionKind
  /** Members of the dimension's published version; `unknownMemberKey` must be one of these. */
  publishedMembers: DimensionMember[]
}

export interface DimensionRole {
  id: string
  roleKey: string
  displayName: string
  dimensionId: string
}

export interface MappingSet {
  id: string
  mappingSetKey: string
  displayName: string
  dimensionId: string
}

export interface Bridge {
  id: string
  bridgeKey: string
  displayName: string
  fromDimensionId: string
  toDimensionId: string
}

export interface Release {
  id: string
  releaseKey: string
  displayName: string
  isActive: boolean
  /** One pinned version per dataset. */
  pins: { datasetId: string; versionId: string }[]
}

export interface TenantCatalog {
  dimensions: Dimension[]
  roles: DimensionRole[]
  mappingSets: MappingSet[]
  bridges: Bridge[]
  releases: Release[]
}

/* ───────────────────────── Source introspection ───────────────────────── */

export interface SourceObject {
  name: string
  kind: SourceObjectKind
  /** Created and owned by Meridian through "New dataset"; fields may add columns to it. */
  isManaged?: boolean
  /** Engine-reported storage statistics; null when the engine has not measured the object yet. */
  stats?: SourceObjectStats | null
}

export interface SourceObjectStats {
  rowCount: number
  /** Total on-disk size including indexes and TOAST. Views report the size of their materialized storage, 0 if none. */
  sizeBytes: number
  measuredAt: string
}

export interface SourceColumn {
  name: string
  nativeType: string
  /** The field type this column reads as, or null when fields cannot read it. */
  mappedType: DataType | null
  numericPrecision: number | null
  numericScale: number | null
  isNullable: boolean
  /** A column the API added to the table on a field's behalf. */
  isGenerated?: boolean
}

/* ───────────────────────── Materialization ───────────────────────── */

export type MaterializationState = 'building' | 'ready' | 'failed'

/**
 * A request for the database engine to store a JSON field's extracted value in the source object's materialized
 * view, so queries read a stored column instead of parsing JSON per row. The field's identity never changes: the
 * planner reads the view when it is ready and falls back to the JSON path otherwise.
 */
export interface FieldMaterialization {
  viewName: string
  columnName: string
  state: MaterializationState
  requestedAt: string
  refreshedAt: string | null
  error: string | null
}

/* ───────────────────────── Dataset and versions ───────────────────────── */

export interface Dataset {
  id: string
  datasetKey: string
  displayName: string
  isEnabled: boolean
}

export interface ReleasePin {
  releaseId: string
  releaseKey: string
  isActive: boolean
}

export interface DatasetVersionSummary {
  id: string
  datasetId: string
  versionNumber: number
  status: VersionStatus
  sourceObject: string
  createdAt: string
  publishedAt: string | null
  /** The version this draft was cloned from, if any. */
  clonedFromVersionId: string | null
  releases: ReleasePin[]
}

export interface DatasetListItem extends Dataset {
  versions: DatasetVersionSummary[]
  /** The version the active release pins: what the engine currently reads. */
  activeVersionId: string | null
  /** Storage statistics of the latest version's source object. */
  storage: SourceObjectStats | null
}

export interface DatasetField {
  id: string
  fieldKey: string
  displayName: string
  fieldSourceKind: FieldSourceKind
  sourceColumn: string | null
  jsonPath: string | null
  expressionText: string | null
  /** JSON path fields only. Managed by `materializeField` / `dematerializeField`, not by `saveField`. */
  materialization: FieldMaterialization | null
  dataType: DataType
  numericPrecision: number | null
  numericScale: number | null
  isNullable: boolean
  unitKey: string | null
  isSelectable: boolean
  isFilterable: boolean
  isSortable: boolean
  isGroupable: boolean
  isEnabled: boolean
}

export interface DatasetGrain {
  id: string
  grainKey: string
  grainKind: GrainKind
  /** Ordered: position is the component ordinal. */
  fieldIds: string[]
}

export interface TimeBindingExtension {
  timeValueType: TimeValueType
  sourceTimeZone: string | null
  defaultQueryTimeZone: string
  observationKind: ObservationKind
  nativePeriodicity: Periodicity | null
  intervalEndFieldId: string | null
  allowedPeriodicities: Periodicity[]
  cumulativeOrigin: CumulativeOrigin
  cumulativeFixedStart: string | null
}

export interface DatasetBinding {
  id: string
  bindingKind: BindingKind
  /** Non-time bindings: the role requests address the binding by. */
  dimensionRoleId: string | null
  /** Time bindings: the time-kind dimension. */
  dimensionId: string | null
  sourceFieldId: string | null
  mappingSetId: string | null
  bridgeId: string | null
  sourceDimensionBindingId: string | null
  raggedBehavior: RaggedBehavior | null
  defaultPeriodAlignment: PeriodAlignment
  unmappedBehavior: UnmappedBehavior | null
  unknownMemberKey: string | null
  isRequired: boolean
  isGroupable: boolean
  isFilterable: boolean
  isEnabled: boolean
  time: TimeBindingExtension | null
}

export interface MeasureComponent {
  role: ComponentRole
  fieldId: string
}

export interface TimePolicy {
  requiredTimeBindingId: string | null
  selectionDatasetGrainId: string | null
  observationSelection: ObservationSelection
  valueTimeRole: ValueTimeRole
  boundaryMatch: BoundaryMatch
  maxLookbackSeconds: number | null
  durationUnit: DurationUnit
  partialIntervalBehavior: PartialIntervalBehavior
  missingPeriodBehavior: MissingPeriodBehavior
}

export interface DatasetMeasure {
  id: string
  measureKey: string
  displayName: string
  isEnabled: boolean
  measureKind: MeasureKind
  aggregationFunction: AggregationFunction | null
  expressionText: string | null
  implementationKey: string | null
  implementationVersion: string | null
  configurationJson: string | null
  components: MeasureComponent[]
  rowFilterExpression: string | null
  valueNullBehavior: ValueNullBehavior
  negativeWeightBehavior: NegativeWeightBehavior
  emptySetBehavior: EmptySetBehavior
  bridgeFactorBehavior: BridgeFactorBehavior
  bridgeAllocationBehavior: BridgeAllocationBehavior
  totalBehavior: TotalBehavior
  outputDataType: DataType
  outputPrecision: number | null
  outputScale: number | null
  roundingMode: RoundingMode
  unitKey: string | null
  accumulatorPrecision: number
  accumulatorScale: number
  timePolicy: TimePolicy | null
}

export interface DatasetVersionSettings {
  sourceObjectKind: SourceObjectKind
  sourceObject: string
  supportsAggregate: boolean
  supportsDetail: boolean
  defaultTimeBindingId: string | null
  expressionLanguageVersion: string
  securityPolicyKey: string | null
  /** Free JSON text; carries no query semantics. */
  properties: string
}

export type DatasetTab = 'fields' | 'grains' | 'bindings' | 'measures' | 'settings'

/** Something that reads an item. Deleting the item is refused while it has blocking dependents. */
export interface Dependent {
  tab: DatasetTab
  itemId: string | null
  itemKey: string
  /** How the dependent reads the item, e.g. "grain component", "formula", "default time binding". */
  relation: string
  blocksDelete: boolean
}

export interface DatasetVersion extends DatasetVersionSummary {
  settings: DatasetVersionSettings
  fields: DatasetField[]
  grains: DatasetGrain[]
  bindings: DatasetBinding[]
  measures: DatasetMeasure[]
  /** Item id → what reads it. Computed by the API from the version's dependency graph. */
  dependents: Record<string, Dependent[]>
}

/* ───────────────────────── Problems and rule violations ───────────────────────── */

/** RFC 7807 problem with per-input errors, keyed by input path (e.g. `sourceColumn`, `time.intervalEndFieldId`). */
export interface ValidationProblemDetails {
  type: string
  title: string
  status: 400 | 404 | 409 | 422
  detail?: string
  errors: Record<string, string[]>
  /** Present on refused deletes: the items that keep the target alive. */
  dependents?: Dependent[]
}

export interface PublicationViolation {
  code: string
  message: string
  tab: DatasetTab
  itemId: string | null
  itemKey: string | null
}

export type ApiResult<T> = { ok: true; data: T } | { ok: false; problem: ValidationProblemDetails }

/* ───────────────────────── Write inputs ───────────────────────── */

/** Create when `id` is absent; update otherwise. Keys are fixed after creation. */
export type FieldInput = Omit<DatasetField, 'id' | 'materialization'> & {
  id?: string
  /**
   * Column fields on a table source: `sourceColumn` names a column that does not exist yet, and the API adds it to
   * the table (typed from `dataType`, precision, scale and nullability) before saving the field.
   */
  createColumn?: boolean
  /** JSON path fields: request materialization once the field is saved. */
  materialize?: boolean
}
export type GrainInput = Omit<DatasetGrain, 'id'> & { id?: string }
export type BindingInput = Omit<DatasetBinding, 'id'> & { id?: string }
export type MeasureInput = Omit<DatasetMeasure, 'id'> & { id?: string }

/** A physical column the API creates. Each one also becomes a column field of the same name. */
export interface NewColumnInput {
  name: string
  dataType: DataType
  numericPrecision: number | null
  numericScale: number | null
  isNullable: boolean
}

export type CreateDatasetInput =
  /** Registers a table or view that already exists in the tenant database. */
  | { mode: 'existing'; datasetKey: string; displayName: string; sourceObjectKind: SourceObjectKind; sourceObject: string }
  /** Creates a managed table with these columns (possibly none), then a draft whose fields read them. */
  | { mode: 'new'; datasetKey: string; displayName: string; tableName: string; columns: NewColumnInput[] }

export type CreateDraftInput =
  | { mode: 'empty'; sourceObjectKind: SourceObjectKind; sourceObject: string }
  | { mode: 'clone'; fromVersionId: string }

export interface ValidationReport {
  versionId: string
  status: VersionStatus
  violations: PublicationViolation[]
  checkedAt: string
}

export interface SettingsSaveResult {
  settings: DatasetVersionSettings
  /** Re-verification of `field_columns_exist` after a source change. */
  warnings: PublicationViolation[]
}

/* ───────────────────────── Port ───────────────────��───── */

/** Draft edits stage into the change set and apply on `commitChanges`; see lib/api/changes.ts. */
export interface DatasetsApi extends ChangeSetPort {
  getVocabularies(): Promise<Vocabularies>
  getTenantCatalog(): Promise<TenantCatalog>
  listSourceObjects(): Promise<SourceObject[]>
  getSourceColumns(kind: SourceObjectKind, name: string): Promise<ApiResult<SourceColumn[]>>

  listDatasets(): Promise<DatasetListItem[]>
  getDataset(datasetId: string): Promise<ApiResult<DatasetListItem>>
  createDataset(input: CreateDatasetInput): Promise<ApiResult<DatasetListItem>>
  updateDataset(datasetId: string, patch: Partial<Pick<Dataset, 'datasetKey' | 'displayName' | 'isEnabled'>>): Promise<ApiResult<Dataset>>
  deleteDataset(datasetId: string): Promise<ApiResult<null>>

  getVersion(versionId: string): Promise<ApiResult<DatasetVersion>>
  createDraft(datasetId: string, input: CreateDraftInput): Promise<ApiResult<DatasetVersionSummary>>
  deleteDraft(versionId: string): Promise<ApiResult<null>>
  validateVersion(versionId: string): Promise<ApiResult<ValidationReport>>
  /** Publishes when every rule passes; otherwise returns the report with the version unchanged. */
  publishVersion(versionId: string): Promise<ApiResult<ValidationReport>>
  /** Validated → draft, so an author can keep editing before publishing. */
  reopenVersion(versionId: string): Promise<ApiResult<DatasetVersionSummary>>
  retireVersion(versionId: string): Promise<ApiResult<DatasetVersionSummary>>

  saveField(versionId: string, input: FieldInput): Promise<ApiResult<DatasetField>>
  deleteField(versionId: string, fieldId: string): Promise<ApiResult<null>>
  addFieldsFromColumns(versionId: string, columns: string[]): Promise<ApiResult<DatasetField[]>>
  /**
   * Asks the engine to add a JSON path field to the source object's materialized view. Returns at once with state
   * `building`; poll the version for `ready` or `failed`. Allowed on any non-retired version: storage is physical
   * and does not change what the version means.
   */
  materializeField(versionId: string, fieldId: string): Promise<ApiResult<DatasetField>>
  /** Rebuilds the stored value, e.g. after a failure. */
  refreshMaterialization(versionId: string, fieldId: string): Promise<ApiResult<DatasetField>>
  /** Drops the column from the materialized view; the field reads the JSON path again. */
  dematerializeField(versionId: string, fieldId: string): Promise<ApiResult<DatasetField>>

  saveGrain(versionId: string, input: GrainInput): Promise<ApiResult<DatasetGrain>>
  deleteGrain(versionId: string, grainId: string): Promise<ApiResult<null>>

  saveBinding(versionId: string, input: BindingInput): Promise<ApiResult<DatasetBinding>>
  deleteBinding(versionId: string, bindingId: string): Promise<ApiResult<null>>

  saveMeasure(versionId: string, input: MeasureInput): Promise<ApiResult<DatasetMeasure>>
  deleteMeasure(versionId: string, measureId: string): Promise<ApiResult<null>>

  saveSettings(versionId: string, settings: DatasetVersionSettings): Promise<ApiResult<SettingsSaveResult>>
  /**
   * Renames the managed table a draft reads. Only allowed while the draft is the table's sole reader, so no other
   * version or published query ever saw the old name.
   */
  renameSourceTable(versionId: string, tableName: string): Promise<ApiResult<DatasetVersionSettings>>
}
