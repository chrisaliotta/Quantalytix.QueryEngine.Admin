import type { components } from '@/api/schema';

type Schemas = components['schemas'];

/** A calculation model: the variables and formulas reports evaluate (`qe_calculation_model`). */
export type CalculationModel = Schemas['CalculationModelRecord'];

/** A version of a calculation model (`qe_calculation_model_version`). */
export type CalculationModelVersion = Schemas['CalculationModelVersionRecord'];

/** A logical input that a profile fills from one of its sources (`qe_variable`). */
export type Variable = Schemas['VariableRecord'];

/** Where a variable can read its values: a dataset's report field, time field, measure and filter (`qe_variable_source`). */
export type VariableSource = Schemas['VariableSourceRecord'];

/** A source profile with its picks: the source each variable reads under the profile. */
export type SourceProfile = Schemas['SourceProfile'];

/** One pick of a source profile. */
export type SourceProfilePick = Schemas['SourceProfilePick'];

/** A formula over variables and other calculated measures (`qe_calculated_measure`). */
export type CalculatedMeasure = Schemas['CalculatedMeasureRecord'];

/** A dimension reports can break out or filter by (`qe_calculation_dimension`). */
export type CalculationDimension = Schemas['CalculationDimensionRecord'];

/** The field that produces a calculation dimension in one dataset (`qe_calculation_dimension_binding`). */
export type CalculationRoute = Schemas['CalculationRouteRecord'];

/** Body of `POST calculation-models`. */
export type NewCalculationModelInput = Schemas['NewCalculationModelInput'];

/** Body of `PUT calculation-models/{modelId}`. */
export type CalculationModelInput = Schemas['CalculationModelInput'];

/** Body of `POST calculation-models/{modelId}/versions`. */
export type NewModelVersionInput = Schemas['NewModelVersionInput'];

/** Body of `PUT model-versions/{versionId}`. */
export type ModelVersionInput = Schemas['ModelVersionInput'];

/** The rows and bodies of each kind of model version content, by the route segment the API serves it under. */
export interface ModelContentTypes {
  variables: { item: Variable; input: Schemas['VariableInput'] };
  'variable-sources': { item: VariableSource; input: Schemas['VariableSourceInput'] };
  profiles: { item: SourceProfile; input: Schemas['SourceProfileInput'] };
  'calculated-measures': { item: CalculatedMeasure; input: Schemas['CalculatedMeasureInput'] };
  'calculation-dimensions': { item: CalculationDimension; input: Schemas['CalculationDimensionInput'] };
  routes: { item: CalculationRoute; input: Schemas['CalculationRouteInput'] };
}

/** A kind of model version content. */
export type ModelContentKind = keyof ModelContentTypes;

/** A row of one content kind. */
export type ModelContentItem<K extends ModelContentKind> = ModelContentTypes[K]['item'];

/** The create and edit body of one content kind. */
export type ModelContentInput<K extends ModelContentKind> = ModelContentTypes[K]['input'];
