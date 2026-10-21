import type { components } from '@/api/schema';

type Schemas = components['schemas'];

/** A `qe_assumption_set` row: the active set, which holds the default versions, or a captured historical set. */
export type AssumptionSet = Schemas['AssumptionSetRecord'];

/** One pin of an assumption set: the version of one dimension, mapping set or bridge. */
export type AssumptionPin = Schemas['AssumptionPin'];

/** The body of `POST assumption-sets`, which captures the active set as a historical set. */
export type AssumptionCaptureInput = Schemas['AssumptionCaptureInput'];

/** The body of `PUT assumption-sets/{id}`: the labels, without the fixed key. */
export type AssumptionSetUpdateInput = Schemas['AssumptionSetUpdateInput'];
