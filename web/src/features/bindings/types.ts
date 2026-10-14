import type { components } from '@/api/schema';

type Schemas = components['schemas'];

/** A dimension binding with its time extension and the bridge that owns its bridge version. */
export type BindingDetail = Schemas['BindingDetail'];

/** A `qe_dimension_binding` row. */
export type DimensionBinding = Schemas['DimensionBindingRecord'];

/** A `qe_time_binding` row: the time extension of a time binding. */
export type TimeBinding = Schemas['TimeBindingRecord'];

/** Body of `POST .../bindings` and `PUT .../bindings/{bindingId}`. */
export type BindingInput = Schemas['BindingInput'];

/** The time extension section of a binding body. */
export type TimeBindingInput = Schemas['TimeBindingInput'];

/** A version of a dimension with its validity window. */
export type DimensionVersion = Schemas['DimensionVersionRecord'];
