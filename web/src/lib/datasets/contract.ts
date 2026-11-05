import type { ChangeSetPort } from '@/lib/api/changes'
import type { ApiResult, BindingKind, ComponentRole, DeleteCheck, Id, MeasureKind, Periodicity, RoundingMode, SnapshotMembership, Violation, ViolationTarget } from '@/lib/api/contract'

export type { ApiResult, BindingKind, ComponentRole, DeleteBlocker, DeleteCheck, Id, MeasureKind, Periodicity, Problem, RoundingMode, Violation, ViolationTarget } from '@/lib/api/contract'

/**
 * Business contract for the dataset manager. Every type mirrors the schema of the same name in
 * api/openapi/contract/datasets.yaml (blueprint 11).
 *
 * The UI depends only on these types and on the `DatasetsApi` port. A concrete adapter (the in-memory mock today,
 * an HTTP client later) implements the port and owns every validation decision: the UI renders what the adapter
 * returns and never checks input itself.
 */

/* ───────────────────────── Enumerations (labels come from the vocabularies) ───────────────────────── */

export type DatasetSourceObjectKind = 'table' | 'view'
export type DatasetFieldSourceKind = 'column' | 'json_path' | 'expression'
export type DatasetDataType = 'boolean' | 'int32' | 'int64' | 'decimal' | 'float' | 'double' | 'string' | 'date' | 'datetime' | 'timestamp' | 'json'
export type DatasetMeasureOutputDataType = Exclude<DatasetDataType, 'float' | 'json'>
export type DatasetGrainKind = 'row' | 'series'
/** `null` reads a null member (the schema's value, not JSON null). */
export type DatasetRaggedBehavior = 'null' | 'self' | 'exclude'
export type DatasetPeriodAlignment = 'observation' | 'start' | 'end'
/** `null` reads a null member (the schema's value, not JSON null). */
export type DatasetUnmappedBehavior = 'null' | 'exclude' | 'unknown'
export type DatasetTimeValueType = 'date' | 'timestamp'
export type DatasetObservationKind = 'instant' | 'snapshot' | 'interval'
export type DatasetCumulativeOrigin = 'output_range_start' | 'fixed_start'
export type DatasetAggregationFunction = 'sum' | 'avg' | 'min' | 'max' | 'count' | 'count_distinct'
export type DatasetCompoundingForm = 'decay' | 'growth'
export type DatasetValueNullBehavior = 'exclude' | 'zero'
export type DatasetNegativeWeightBehavior = 'allow' | 'exclude'
export type DatasetEmptySetBehavior = 'null' | 'zero'
export type DatasetBridgeFactorBehavior = 'ignore' | 'value'
export type DatasetBridgeAllocationBehavior = 'apply' | 'ignore'
export type DatasetTotalBehavior = 'recompute' | 'sum_children'
export type DatasetObservationSelection = 'all' | 'first_in_period' | 'last_in_period' | 'at_start' | 'at_end'
export type DatasetValueTimeRole = 'point' | 'interval_open' | 'interval_close'
export type DatasetBoundaryMatch = 'exact' | 'on_or_before' | 'strictly_before'
export type DatasetDurationUnit = 'calendar_day' | 'elapsed_second'
export type DatasetPartialIntervalBehavior = 'exact_only' | 'uniform_estimate'
export type DatasetMissingPeriodBehavior = 'null' | 'zero'
export type DatasetMaterializationState = 'building' | 'ready' | 'failed'
export type DatasetTab = 'fields' | 'grains' | 'bindings' | 'measures' | 'settings'

/* ───────────────────────── Source introspection ───────────────────────── */

export interface DatasetSourceObject {
  name: string
  kind: DatasetSourceObjectKind
  /** Created by the API through "New dataset" and registered; fields may add columns to it. */
  isManaged: boolean
  /** Null when the engine has not measured the object yet. */
  stats: DatasetSourceObjectStats | null
}

export interface DatasetSourceObjectStats {
  rowCount: number
  /** On-disk size; a view reports its materialized storage, 0 if none. */
  sizeBytes: number
  measuredAt: string
}

export interface DatasetSourceColumn {
  name: string
  nativeType: string
  /** The field type this column reads as, or null when fields cannot read it. */
  mappedType: DatasetDataType | null
  numericPrecision: number | null
  numericScale: number | null
  isNullable: boolean
  /** The API added the column on a field's behalf. */
  isGenerated: boolean
}

/* ───────────────────────── Dataset and versions ───────────────────────── */

/** The dataset identity, with one version's enabled flag: the live version's, or a frozen version's own. */
export interface Dataset {
  id: Id
  key: string
  displayName: string
  isEnabled: boolean
}

/** Identity edits and the live version's enabled flag; the key is fixed after creation. */
export interface DatasetPatch {
  displayName?: string
  isEnabled?: boolean
}

/** One version of a dataset and the snapshot it belongs to. The live version is editable; a frozen one is read-only. */
export interface DatasetVersionSummary extends SnapshotMembership {
  id: Id
  datasetId: Id
  sourceObject: string
  createdAt: string
}

export interface DatasetListItem extends Dataset {
  /** One per snapshot holding the dataset: the live version first, then frozen ones newest first. */
  versions: DatasetVersionSummary[]
  /** The live version, which change sets edit and requests that name no snapshot read. */
  liveVersionId: Id
  /** Storage statistics of the live version's source object. */
  storage: DatasetSourceObjectStats | null
}

export interface DatasetVersionSettings {
  sourceObjectKind: DatasetSourceObjectKind
  sourceObject: string
  supportsAggregate: boolean
  supportsDetail: boolean
  defaultTimeBindingId: Id | null
  expressionLanguageVersion: string
  securityPolicyKey: string | null
  /** Free JSON text; carries no query semantics. */
  properties: string
}

/** Something that reads an item. Deleting the item is refused while it has blocking dependents. */
export interface DatasetDependent {
  tab: DatasetTab
  /** Null for the settings tab. */
  itemId: Id | null
  itemKey: string
  /** How the dependent reads the item, e.g. "grain component", "formula", "default time binding". */
  relation: string
  blocksDelete: boolean
}

export interface DatasetVersion extends DatasetVersionSummary {
  /** The dataset identity as the change set leaves it. */
  dataset: Dataset
  settings: DatasetVersionSettings
  fields: DatasetField[]
  grains: DatasetGrain[]
  bindings: DatasetBinding[]
  measures: DatasetMeasure[]
  /** Item id → what reads it. Computed by the API from the version's dependency graph. */
  dependents: Record<Id, DatasetDependent[]>
}

/* ───────────────────────── Fields ───────────────────────── */

/**
 * A request for the database engine to store a JSON field's extracted value in the source object's materialized
 * view, so queries read a stored column instead of parsing JSON per row. The field's identity never changes: the
 * planner reads the view when it is ready and falls back to the JSON path otherwise.
 */
export interface DatasetFieldMaterialization {
  viewName: string
  columnName: string
  state: DatasetMaterializationState
  requestedAt: string
  refreshedAt: string | null
  error: string | null
}

export interface DatasetFieldDefinition {
  key: string
  displayName: string
  fieldSourceKind: DatasetFieldSourceKind
  sourceColumn: string | null
  jsonPath: string | null
  expressionText: string | null
  dataType: DatasetDataType
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

export interface DatasetField extends DatasetFieldDefinition {
  id: Id
  /** JSON path fields only. Managed by `materializeField` / `dematerializeField`, not by `saveField`. */
  materialization: DatasetFieldMaterialization | null
}

/** `id` absent or temporary (`tmp:<uuid>`) creates; a stored id updates. Keys are fixed after creation. */
export interface DatasetFieldInput extends DatasetFieldDefinition {
  id?: Id
  /**
   * Column fields on a managed table: `sourceColumn` names a column that does not exist yet, and the API adds it to
   * the table (typed from `dataType`, precision, scale and nullability) before saving the field.
   */
  createColumn?: boolean
  /** JSON path fields: request materialization once the field is saved. */
  materialize?: boolean
}

/* ───────────────────────── Grains ───────────────────────── */

export interface DatasetGrainDefinition {
  key: string
  grainKind: DatasetGrainKind
  /** Ordered: position is the component ordinal. */
  fieldIds: Id[]
}

export interface DatasetGrain extends DatasetGrainDefinition {
  id: Id
}

export interface DatasetGrainInput extends DatasetGrainDefinition {
  id?: Id
}

/* ───────────────────────── Bindings ───────────────────────── */

export interface DatasetTimeBinding {
  timeValueType: DatasetTimeValueType
  sourceTimeZone: string | null
  defaultQueryTimeZone: string
  observationKind: DatasetObservationKind
  nativePeriodicity: Periodicity | null
  intervalEndFieldId: Id | null
  allowedPeriodicities: Periodicity[]
  cumulativeOrigin: DatasetCumulativeOrigin
  cumulativeFixedStart: string | null
}

export interface DatasetBindingDefinition {
  bindingKind: BindingKind
  /** Non-time bindings: the role requests address the binding by. */
  dimensionRoleId: Id | null
  /** Time bindings: the time-kind dimension. */
  dimensionId: Id | null
  sourceFieldId: Id | null
  mappingSetId: Id | null
  bridgeId: Id | null
  sourceDimensionBindingId: Id | null
  raggedBehavior: DatasetRaggedBehavior
  defaultPeriodAlignment: DatasetPeriodAlignment
  unmappedBehavior: DatasetUnmappedBehavior
  unknownMemberKey: string | null
  /** Set by the server; a required binding cannot be disabled or deleted. */
  isRequired: boolean
  isGroupable: boolean
  isFilterable: boolean
  isEnabled: boolean
  time: DatasetTimeBinding | null
}

export interface DatasetBinding extends DatasetBindingDefinition {
  id: Id
}

export interface DatasetBindingInput extends DatasetBindingDefinition {
  id?: Id
}

/* ───────────────────────── Measures ───────────────────────── */

export interface DatasetMeasureComponent {
  role: ComponentRole
  fieldId: Id
}

export interface DatasetMeasureTimePolicy {
  requiredTimeBindingId: Id | null
  selectionDatasetGrainId: Id | null
  observationSelection: DatasetObservationSelection
  valueTimeRole: DatasetValueTimeRole
  boundaryMatch: DatasetBoundaryMatch
  maxLookbackSeconds: number | null
  durationUnit: DatasetDurationUnit
  partialIntervalBehavior: DatasetPartialIntervalBehavior
  missingPeriodBehavior: DatasetMissingPeriodBehavior
}

export interface DatasetMeasureDefinition {
  key: string
  displayName: string
  isEnabled: boolean
  measureKind: MeasureKind
  aggregationFunction: DatasetAggregationFunction | null
  /** Formula measures, and the per-step rate of compound measures; only formulas read the period (expression language 1.2). */
  expressionText: string | null
  /** Compound measures: the step the rate compounds over. */
  compoundingPeriodicity: Periodicity | null
  /** Compound measures: decay is 1 - prod(1 - r), growth is prod(1 + r) - 1. */
  compoundingForm: DatasetCompoundingForm | null
  implementationKey: string | null
  implementationVersion: string | null
  configurationJson: string | null
  components: DatasetMeasureComponent[]
  rowFilterExpression: string | null
  valueNullBehavior: DatasetValueNullBehavior
  negativeWeightBehavior: DatasetNegativeWeightBehavior
  emptySetBehavior: DatasetEmptySetBehavior
  bridgeFactorBehavior: DatasetBridgeFactorBehavior
  bridgeAllocationBehavior: DatasetBridgeAllocationBehavior
  totalBehavior: DatasetTotalBehavior
  outputDataType: DatasetMeasureOutputDataType
  outputPrecision: number | null
  outputScale: number | null
  roundingMode: RoundingMode | null
  unitKey: string | null
  accumulatorPrecision: number
  accumulatorScale: number
  timePolicy: DatasetMeasureTimePolicy | null
}

export interface DatasetMeasure extends DatasetMeasureDefinition {
  id: Id
}

export interface DatasetMeasureInput extends DatasetMeasureDefinition {
  id?: Id
}

/* ───────────────────────── Rule findings ───────────────────────── */

export interface DatasetViolationTarget extends ViolationTarget {
  area: DatasetTab
}

/** A commit-rule finding; every one points at the tab (and item) to fix. */
export interface DatasetViolation extends Violation {
  target: DatasetViolationTarget
}

export interface DatasetSettingsSaveResult {
  settings: DatasetVersionSettings
  /** Re-verification of `field_columns_exist` after a source change. */
  warnings: DatasetViolation[]
}

/* ───────────────────────── Command inputs ───────────────────────── */

/** A physical column the API creates. Each one also becomes a column field of the same name. */
export interface DatasetNewColumnInput {
  name: string
  dataType: DatasetDataType
  numericPrecision: number | null
  numericScale: number | null
  isNullable: boolean
}

/** Registers a table or view that already exists in the tenant database. */
export interface DatasetCreateFromExistingInput {
  mode: 'existing'
  key: string
  displayName: string
  sourceObjectKind: DatasetSourceObjectKind
  sourceObject: string
}

/** Creates a managed table with these columns (possibly none), then a live version whose fields read them. */
export interface DatasetCreateWithNewTableInput {
  mode: 'new'
  key: string
  displayName: string
  tableName: string
  columns: DatasetNewColumnInput[]
}

export type DatasetCreateInput = DatasetCreateFromExistingInput | DatasetCreateWithNewTableInput

/* ───────────────────────── Change set ───────────────────────── */

/** One staged edit of a dataset version, named after the edit method it records. */
export type DatasetOperation =
  | { op: 'updateDataset'; patch: DatasetPatch }
  | { op: 'saveSettings'; settings: DatasetVersionSettings }
  | { op: 'renameSourceTable'; tableName: string }
  | { op: 'saveField'; input: DatasetFieldInput }
  | { op: 'deleteField'; fieldId: Id }
  | { op: 'addFieldsFromColumns'; columns: string[] }
  | { op: 'saveGrain'; input: DatasetGrainInput }
  | { op: 'deleteGrain'; grainId: Id }
  | { op: 'saveBinding'; input: DatasetBindingInput }
  | { op: 'deleteBinding'; bindingId: Id }
  | { op: 'saveMeasure'; input: DatasetMeasureInput }
  | { op: 'deleteMeasure'; measureId: Id }

/* ───────────────────────── Port ───────────────────────── */

/**
 * Edits stage into the change set of a dataset's live version and apply on `commitChanges`, which the commit rules refuse while
 * any finding remains; see lib/api/changes.ts. Frozen versions are read-only.
 */
export interface DatasetsApi extends ChangeSetPort {
  // reads
  listSourceObjects(): Promise<DatasetSourceObject[]>
  getSourceColumns(kind: DatasetSourceObjectKind, name: string): Promise<ApiResult<DatasetSourceColumn[]>>
  listDatasets(): Promise<DatasetListItem[]>
  getDataset(datasetId: Id): Promise<ApiResult<DatasetListItem>>
  /** Any version, live or frozen. */
  getVersion(versionId: Id): Promise<ApiResult<DatasetVersion>>
  checkDeleteDataset(datasetId: Id): Promise<DeleteCheck>

  // edits
  updateDataset(datasetId: Id, patch: DatasetPatch): Promise<ApiResult<Dataset>>
  saveSettings(versionId: Id, settings: DatasetVersionSettings): Promise<ApiResult<DatasetSettingsSaveResult>>
  /** Renames the managed table the live version reads. Refused while another version, live or frozen, reads it. */
  renameSourceTable(versionId: Id, tableName: string): Promise<ApiResult<DatasetVersionSettings>>
  saveField(versionId: Id, input: DatasetFieldInput): Promise<ApiResult<DatasetField>>
  deleteField(versionId: Id, fieldId: Id): Promise<ApiResult<null>>
  addFieldsFromColumns(versionId: Id, columns: string[]): Promise<ApiResult<DatasetField[]>>
  saveGrain(versionId: Id, input: DatasetGrainInput): Promise<ApiResult<DatasetGrain>>
  deleteGrain(versionId: Id, grainId: Id): Promise<ApiResult<null>>
  saveBinding(versionId: Id, input: DatasetBindingInput): Promise<ApiResult<DatasetBinding>>
  deleteBinding(versionId: Id, bindingId: Id): Promise<ApiResult<null>>
  saveMeasure(versionId: Id, input: DatasetMeasureInput): Promise<ApiResult<DatasetMeasure>>
  deleteMeasure(versionId: Id, measureId: Id): Promise<ApiResult<null>>

  // commands
  createDataset(input: DatasetCreateInput): Promise<ApiResult<DatasetListItem>>
  /** Allowed only while no frozen snapshot holds a version of the dataset. */
  deleteDataset(datasetId: Id): Promise<ApiResult<null>>
  /**
   * Asks the engine to add a JSON path field to the source object's materialized view. Returns at once with state
   * `building`; poll the version for `ready` or `failed`. Allowed on the live version only.
   */
  materializeField(versionId: Id, fieldId: Id): Promise<ApiResult<DatasetField>>
  /** Rebuilds the stored value, e.g. after a failure. */
  refreshMaterialization(versionId: Id, fieldId: Id): Promise<ApiResult<DatasetField>>
  /** Drops the column from the materialized view; the field reads the JSON path again. */
  dematerializeField(versionId: Id, fieldId: Id): Promise<ApiResult<DatasetField>>
}

/** Port methods that stage a `DatasetOperation` of the same name. */
export const DATASET_EDITS = [
  'updateDataset',
  'saveSettings',
  'renameSourceTable',
  'saveField',
  'deleteField',
  'addFieldsFromColumns',
  'saveGrain',
  'deleteGrain',
  'saveBinding',
  'deleteBinding',
  'saveMeasure',
  'deleteMeasure',
] as const satisfies readonly (DatasetOperation['op'] & keyof DatasetsApi)[]

/** Port methods that run on saved state at once and are refused while edits are pending. */
export const DATASET_COMMANDS = [
  'createDataset',
  'deleteDataset',
  'materializeField',
  'refreshMaterialization',
  'dematerializeField',
] as const satisfies readonly (keyof DatasetsApi)[]
