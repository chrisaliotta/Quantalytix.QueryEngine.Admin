import type { components } from '@/api/schema';

type Schemas = components['schemas'];

/** A calculation model: the metrics reports evaluate, which several reports can share (`qe_calculation_model`). */
export type CalculationModel = Schemas['CalculationModelRecord'];

/** A version of a calculation model (`qe_calculation_model_version`). */
export type CalculationModelVersion = Schemas['CalculationModelVersionRecord'];

/** A logical input that a report's profile fills from one of its sources, with how clients show it (`qe_variable`). */
export type Variable = Schemas['VariableRecord'];

/** A formula over variables and other calculated measures (`qe_calculated_measure`). */
export type CalculatedMeasure = Schemas['CalculatedMeasureRecord'];

/** Body of `POST calculation-models`. */
export type NewCalculationModelInput = Schemas['NewCalculationModelInput'];

/** Body of `PUT calculation-models/{modelId}`. */
export type CalculationModelInput = Schemas['CalculationModelInput'];

/** Body of `POST calculation-models/{modelId}/versions`. */
export type NewModelVersionInput = Schemas['NewModelVersionInput'];

/** Body of `PUT model-versions/{versionId}`. */
export type ModelVersionInput = Schemas['ModelVersionInput'];
