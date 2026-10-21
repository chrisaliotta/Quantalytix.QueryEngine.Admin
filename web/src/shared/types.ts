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
  | 'semantic_release';

/** Result of a version status change. */
export type VersionStatusResult = Schemas['VersionState'];

/** Signed-in identity from `GET /api/me`. */
export type Me = Schemas['SessionInfo'];

/** Tenant from `GET /api/tenants`. */
export type Tenant = Schemas['AccessibleTenant'];

/** Lifecycle states of versioned resources. */
export type VersionStatus = 'draft' | 'validated' | 'published' | 'retired';

/** What a dimension is for (the `dimension_purpose` vocabulary): datasets bind analytical ones; a report dimension holds one report's rows. */
export type DimensionPurpose = 'analytical' | 'report';

/** Lookup kinds served by the lookups endpoint (comments name the expected `parentId`). */
export type LookupKind =
  | 'datasets'
  | 'dataset-versions' // dataset
  | 'fields' // dataset version; classifier = data type
  | 'bound-roles' // dataset version; item parentId = the role's template; classifier = binding kind
  | 'grains'
  | 'bindings'
  | 'time-bindings'
  | 'measures'
  | 'dimensions' // optional purpose; classifier = kind
  | 'dimension-roles' // optional dimension; item parentId = the template; classifier = its kind
  | 'dimension-versions' // dimension
  | 'levels' // dimension version
  | 'members' // dimension version; item parentId = parent member
  | 'mapping-sets' // optional dimension
  | 'mapping-versions' // mapping set
  | 'bridges' // optional source dimension; item parentId = target dimension
  | 'bridge-versions' // bridge
  | 'releases'
  | 'assumption-sets'
  | 'calculation-models' // classifier = currency
  | 'variables' // calculation model; classifier = role
  | 'report-sources' // report; classifier = mapping method
  | 'source-profiles' // report
  | 'calculated-measures' // calculation model; classifier = display format
  | 'calculation-dimensions' // report; classifier = grouping mode
  | 'reports' // classifier = report type
  | 'report-columns'; // report; classifier = value kind
