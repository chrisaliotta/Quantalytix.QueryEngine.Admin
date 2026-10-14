import type { components } from '@/api/schema';

type Schemas = components['schemas'];

/** A dataset: the stable identity that query requests name. */
export type Dataset = Schemas['DatasetRecord'];

/** A version of a dataset. */
export type DatasetVersion = Schemas['DatasetVersionRecord'];

/** Body of `POST .../datasets/{datasetId}/versions`: a clone source, or an empty draft's source object. */
export type DatasetVersionInput = Schemas['DatasetVersionInput'];

/** A field of a dataset version. */
export type Field = Schemas['FieldRecord'];

/** Body of `POST .../fields`. */
export type FieldInput = Schemas['FieldInput'];

/** Body of `PUT .../fields/{fieldId}`: a field input without its fixed key. */
export type FieldUpdateInput = Schemas['FieldUpdateInput'];

/** A grain with its field IDs in ordinal order. */
export type Grain = Schemas['GrainDetail'];

/** A table or view of the tenant database that datasets can query. */
export type SourceObject = Schemas['SourceObject'];

/** A column of a source object with the field type it maps to; `dataType` is null for types fields cannot read. */
export type SourceColumn = Schemas['SourceColumn'];
