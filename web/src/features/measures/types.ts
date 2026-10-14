import type { components } from '@/api/schema';

type Schemas = components['schemas'];

/** A measure with its components, the measures its formula depends on and its time policy. */
export type MeasureDetail = Schemas['MeasureDetail'];

/** A `qe_measure` row. */
export type Measure = Schemas['MeasureRecord'];

/** A `qe_measure_time_policy` row. */
export type MeasureTimePolicy = Schemas['MeasureTimePolicyRecord'];

/** Body of `POST .../measures`. */
export type MeasureInput = Schemas['MeasureInput'];

/** Body of `PUT .../measures/{measureId}`: a measure input without its fixed key. */
export type MeasureUpdateInput = Schemas['MeasureUpdateInput'];

/** The time policy section of a measure body. */
export type MeasureTimePolicyInput = Schemas['MeasureTimePolicyInput'];
