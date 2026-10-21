import type { components } from '@/api/schema';

type Schemas = components['schemas'];

/**
 * A calculation model: the metrics reports evaluate, which several reports can share, with the currency, calendar and formula
 * language they are read in (`qe_calculation_model`).
 */
export type CalculationModel = Schemas['CalculationModelRecord'];

/** A logical input that a report's profile fills from one of its sources, with how clients show it (`qe_variable`). */
export type Variable = Schemas['VariableRecord'];

/** A formula over variables and other calculated measures (`qe_calculated_measure`). */
export type CalculatedMeasure = Schemas['CalculatedMeasureRecord'];

/** Body of `POST calculation-models`. */
export type NewCalculationModelInput = Schemas['NewCalculationModelInput'];

/** Body of `PUT calculation-models/{modelId}`. */
export type CalculationModelInput = Schemas['CalculationModelInput'];
