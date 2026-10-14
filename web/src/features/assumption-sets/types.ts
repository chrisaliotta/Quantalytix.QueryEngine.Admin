import type { components } from '@/api/schema';

type Schemas = components['schemas'];

/** A `qe_assumption_set` row. */
export type AssumptionSet = Schemas['AssumptionSetRecord'];

/** A pin of an assumption set: the exact dimension version it captures for a dimension. */
export type AssumptionPin = Schemas['AssumptionSetDimensionRecord'];

/** The body of `POST assumption-sets`. */
export type AssumptionSetCreateInput = Schemas['AssumptionSetCreateInput'];

/** The body of `PUT assumption-sets/{id}`: the settings, without the fixed key. */
export type AssumptionSetUpdateInput = Schemas['AssumptionSetUpdateInput'];
