/**
 * Calculation models port (blueprint 15). Every type mirrors the schema of the same name in
 * api/openapi/contract/models.yaml.
 */
import type { ChangeSetPort } from '@/lib/api/changes'
import type { ApiResult, DeleteCheck, Id } from '@/lib/api/contract'

export type { ApiResult, ChangeSetCommit, ChangeSetPreview, ChangeSetRequest, DeleteCheck, Id, Problem, Violation } from '@/lib/api/contract'

export type ModelFormulaLanguage = '1.0' | '1.1' | '1.2'
export type AttributionMethod = 'shapley' | 'sequential'
export type ModelMissingBehavior = 'null' | 'zero'
export type ModelParameterValueType = 'number' | 'text'
export type ModelRoundingMode = 'half_even' | 'half_up' | 'down' | 'up' | 'truncate'
export type ModelOutputDataType = 'decimal' | 'int64' | 'int32'

export interface CalcModel {
  id: Id
  /** Fixed after creation (C9). */
  key: string
  displayName: string
  currencyCode: string
  calendarKey: string
  formulaLanguage: ModelFormulaLanguage
  attributionMethod: AttributionMethod
  isEnabled: boolean
}

export interface ModelVariable {
  key: string
  displayName: string
  displayFormat: string
  unitKey: string | null
  missingBehavior: ModelMissingBehavior
}

export interface ModelMeasure {
  key: string
  displayName: string
  formula: string
  outputDataType: ModelOutputDataType
  outputPrecision: number | null
  outputScale: number | null
  roundingMode: ModelRoundingMode | null
  unitKey: string | null
  displayFormat: string
}

export interface ModelParameter {
  key: string
  displayName: string
  valueType: ModelParameterValueType
  /** Null means any value. */
  allowedValues: string[] | null
  defaultNumber: number | null
  defaultText: string | null
  description: string | null
}

/** Position in `Attribution.drivers` is the sort order. */
export interface AttributionDriver {
  key: string
  displayName: string
  /** Metric keys and `@periodDays` / `@yearDays`. */
  inputs: string[]
}

export interface Attribution {
  metricKey: string
  /** Required for a variable; null on a calculated measure splits its own formula. */
  expression: string | null
  weightVariableKey: string | null
  drivers: AttributionDriver[]
}

export interface ModelReportUse {
  reportId: Id
  reportKey: string
  displayName: string
  isEnabled: boolean
}

export interface ModelListItem extends CalcModel {
  variableCount: number
  measureCount: number
  usedBy: ModelReportUse[]
}

export interface ModelDocument extends CalcModel {
  variables: ModelVariable[]
  measures: ModelMeasure[]
  parameters: ModelParameter[]
  /** Explicit attribution rows only; a metric without one uses default drivers (see effectiveDrivers). */
  attributions: Attribution[]
  /** Server-computed: drivers in effect per metric key, default or explicit. A metric that cannot be attributed has no entry. */
  effectiveDrivers: Record<string, AttributionDriver[]>
  usedBy: ModelReportUse[]
}

export interface CalcModelCreateInput {
  key: string
  displayName: string
  currencyCode: string
  formulaLanguage: ModelFormulaLanguage
  attributionMethod: AttributionMethod
}

export type CalcModelPatch = Partial<Pick<CalcModel, 'displayName' | 'currencyCode' | 'calendarKey' | 'formulaLanguage' | 'attributionMethod' | 'isEnabled'>>

export interface ModelFormulaError {
  message: string
  /** Zero-based offsets into the formula; `end` is exclusive. */
  start: number
  end: number
}

export interface ModelFormulaCheck {
  errors: ModelFormulaError[]
  /** Metric keys, in order of first use. */
  reads: string[]
  /** Declared `@param` keys. */
  parameters: string[]
  requiresLanguage: ModelFormulaLanguage
}

/** `originalKey` is present when an existing item is edited; keys are fixed, so a changed key is refused. */
export type ModelItemInput<T> = T & { originalKey?: string }

export type ModelOperation =
  | { op: 'updateModel'; patch: CalcModelPatch }
  | { op: 'saveVariable'; input: ModelVariable; originalKey?: string }
  | { op: 'deleteVariable'; key: string }
  | { op: 'saveMeasure'; input: ModelMeasure; originalKey?: string }
  | { op: 'deleteMeasure'; key: string }
  | { op: 'saveParameter'; input: ModelParameter; originalKey?: string }
  | { op: 'deleteParameter'; key: string }
  | { op: 'setAttribution'; input: Attribution }
  | { op: 'clearAttribution'; metricKey: string }

/** Rule codes of the model gate (15, Rule codes); report rules re-run for every enabled report over the model. */
export type ModelRuleCode =
  | 'formula_references_resolve'
  | 'formulas_acyclic'
  | 'formulas_fit_language'
  | 'formula_parameters_resolve'
  | 'attribution_references_resolve'
  | 'attribution_drivers'
  | 'attribution_weight_variable'
  | 'model_not_in_use'

/**
 * Edits are staged per model (one change set per model) and refused by preview when they introduce a model-rule or
 * enabled-report violation (OI-42). Commands run on saved state.
 */
export interface ModelsApi extends ChangeSetPort {
  // reads
  listModels(): Promise<ModelListItem[]>
  getModel(id: Id): Promise<ApiResult<ModelDocument>>
  /** Reads saved model state (OI-43). */
  checkFormula(id: Id, formula: string, selfKey: string | null): Promise<ModelFormulaCheck>
  checkDeleteModel(id: Id): Promise<DeleteCheck>

  // edits
  updateModel(id: Id, patch: CalcModelPatch): Promise<ApiResult<CalcModel>>
  saveVariable(id: Id, input: ModelItemInput<ModelVariable>): Promise<ApiResult<ModelVariable>>
  deleteVariable(id: Id, key: string): Promise<ApiResult<null>>
  saveMeasure(id: Id, input: ModelItemInput<ModelMeasure>): Promise<ApiResult<ModelMeasure>>
  deleteMeasure(id: Id, key: string): Promise<ApiResult<null>>
  saveParameter(id: Id, input: ModelItemInput<ModelParameter>): Promise<ApiResult<ModelParameter>>
  deleteParameter(id: Id, key: string): Promise<ApiResult<null>>
  setAttribution(id: Id, input: Attribution): Promise<ApiResult<Attribution>>
  clearAttribution(id: Id, metricKey: string): Promise<ApiResult<null>>

  // commands
  createModel(input: CalcModelCreateInput): Promise<ApiResult<CalcModel>>
  deleteModel(id: Id): Promise<ApiResult<null>>
}

export const MODEL_EDITS = [
  'updateModel',
  'saveVariable',
  'deleteVariable',
  'saveMeasure',
  'deleteMeasure',
  'saveParameter',
  'deleteParameter',
  'setAttribution',
  'clearAttribution',
] as const satisfies readonly (ModelOperation['op'] & keyof ModelsApi)[]

export const MODEL_COMMANDS = ['createModel', 'deleteModel'] as const satisfies readonly (keyof ModelsApi)[]
