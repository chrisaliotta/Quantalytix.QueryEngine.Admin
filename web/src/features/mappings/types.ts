import type { components } from '@/api/schema';

type Schemas = components['schemas'];

/** A source code system that maps onto one canonical dimension (`qe_mapping_set`). */
export type MappingSet = Schemas['MappingSetRecord'];

/** One source value of a mapping version and the member it maps to (`qe_dimension_mapping`). */
export type MappingEntry = Schemas['DimensionMappingRecord'];

/** Body of `POST mapping-sets`. */
export type NewMappingSetInput = Schemas['NewMappingSetInput'];

/** Body of `PUT mapping-sets/{mappingSetId}`. */
export type MappingSetInput = Schemas['MappingSetInput'];

/** Body of `POST mapping-sets/{mappingSetId}/versions`. */
export type NewMappingVersionInput = Schemas['NewMappingVersionInput'];

/** Answer of `POST mapping-sets/{mappingSetId}/versions`: the new draft and the source keys a clone dropped. */
export type CreatedMappingVersion = Schemas['CreatedMappingVersion'];

/** Body of `POST` and `PUT .../entries/{entryId}`, and one item of the bulk `PUT .../entries`. */
export type MappingEntryInput = Schemas['MappingEntryInput'];
