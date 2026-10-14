import type { components } from '@/api/schema';

type Schemas = components['schemas'];

/** Row of `GET /api/tenants/{tenantId}/lookups/{kind}`. */
export type LookupItem = Schemas['LookupItem'];

/** Controlled vocabulary (e.g. `data_type`) with its allowed values. */
export type Vocabulary = Schemas['Vocabulary'];

/** One allowed value of a vocabulary. */
export type VocabularyValue = Schemas['VocabularyValue'];

/** A rule that rejected a version transition (422 `violations`). */
export type RuleViolation = Schemas['RuleViolation'];

/** Versioned resource kinds accepted by `PUT .../versions/{kind}/{id}/status` (the `version_kind` vocabulary). */
export type VersionKind =
  | 'dataset_version'
  | 'dimension_version'
  | 'mapping_version'
  | 'bridge_version'
  | 'semantic_release'
  | 'assumption_set';

/** Result of a version status change. */
export type VersionStatusResult = Schemas['VersionState'];

/** Signed-in identity from `GET /api/me`. */
export type Me = Schemas['SessionInfo'];

/** Tenant from `GET /api/tenants`. */
export type Tenant = Schemas['AccessibleTenant'];

/** Lifecycle states of versioned resources. */
export type VersionStatus = 'draft' | 'validated' | 'published' | 'retired';

/** Lookup kinds served by the lookups endpoint (comments name the expected `parentId`). */
export type LookupKind =
  | 'datasets'
  | 'dataset-versions' // dataset
  | 'fields' // dataset version; classifier = data type
  | 'grains'
  | 'bindings'
  | 'time-bindings'
  | 'measures'
  | 'dimensions'
  | 'dimension-versions' // dimension
  | 'levels' // dimension version
  | 'members' // dimension version; item parentId = parent member
  | 'mapping-sets' // optional dimension
  | 'mapping-versions' // mapping set
  | 'bridges'
  | 'bridge-versions' // bridge
  | 'releases'
  | 'assumption-sets';
