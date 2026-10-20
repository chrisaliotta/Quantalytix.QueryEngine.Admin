import type { components } from '@/api/schema';

type Schemas = components['schemas'];

/** A release with whether it is active and how many datasets it pins (`GET releases`). */
export type ReleaseSummary = Schemas['ReleaseSummary'];

/** A `qe_semantic_release` row. */
export type SemanticRelease = Schemas['SemanticReleaseRecord'];

/** A manifest row: the dataset version a release pins for a dataset. */
export type ReleaseDataset = Schemas['ReleaseDatasetRecord'];

/** The body of `POST releases`. */
export type ReleaseCreateInput = Schemas['ReleaseCreateInput'];
