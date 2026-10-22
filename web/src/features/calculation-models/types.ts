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

/**
 * How a metric's change between two values splits into the effects of its drivers, with the variable that weighs its member
 * contributions (`qe_metric_attribution` with its `qe_metric_attribution_driver` rows).
 */
export type MetricAttribution = Schemas['MetricAttribution'];

/** One driver of an attribution: the expression's inputs that switch together. */
export type AttributionDriver = Schemas['AttributionDriver'];

/** Body of `PUT calculation-models/{modelId}/attributions/{metricKey}`. */
export type MetricAttributionInput = Schemas['MetricAttributionInput'];

/**
 * A value the model declares for its formulas to read as `@key`, a number or text, which each report sets on its rows; a row
 * without one inherits its nearest ancestor's, else the default (`qe_model_parameter`).
 */
export type ModelParameter = Schemas['ModelParameter'];

/** Body of `PUT calculation-models/{modelId}/parameters/{parameterKey}`. */
export type ModelParameterInput = Schemas['ModelParameterInput'];

/** The `parameter_value_type` vocabulary. */
export type ParameterValueType = 'number' | 'text';
