import type { ChangeSetPort } from '@/lib/api/changes'
import type {
  ApiResult,
  ChangeSetCommit,
  ChangeSetPreview,
  ChangeSetRequest,
  DefinitionOrigin,
  DeleteCheck,
  DimensionKind,
  DimensionPurpose,
  FreezeResult,
  Id,
  Periodicity,
  Problem,
  ValidationReport,
  VersionStatus,
  Violation,
  ViolationTarget,
} from '@/lib/api/contract'

/**
 * Business contract for the dimension manager (blueprint 12). Every type mirrors the schema of the same name in
 * api/openapi/contract/dimensions.yaml. Screens depend only on these types and the `DimensionsApi` port; the mock
 * adapter in ./mock implements it in memory, and an HTTP adapter can replace it without touching the UI.
 */

export type { ApiResult, DefinitionOrigin, DeleteBlocker, DeleteBlockerCode, DeleteCheck, DimensionKind, DimensionPurpose, FreezeInput, FreezeResult, Id, Periodicity, Problem, ValidationReport, VersionStatus, Violation } from '@/lib/api/contract'

/* ───────────────────────── vocabularies ───────────────────────── */

export type DimensionEditPolicy = 'open' | 'extend' | 'locked'
export type RangeDataType = 'decimal' | 'date' | 'datetime'

export type DimensionTab = 'properties' | 'members' | 'levels' | 'bands' | 'roles' | 'mappings' | 'bridges' | 'usage'

export type DimensionViolationCode =
  | 'range_data_type_matches_kind'
  | 'level_key_unique'
  | 'member_key_unique'
  | 'member_parent_in_version'
  | 'member_level_in_version'
  | 'hierarchy_acyclic'
  | 'band_boundary_family'
  | 'band_bounds_ordered'
  | 'bands_non_overlapping'
  | 'band_open_bounds_at_ends'
  | 'band_member_in_version'
  | 'dimension_consumers_resolve'
  | 'profile_required_keys_present'
  | 'profile_properties_valid'

export interface DimensionViolation extends Violation {
  code: DimensionViolationCode
  /** `itemId` is a member, level or role id, or a band ordinal as a string. */
  target?: ViolationTarget & { area: DimensionTab }
}

export interface DimensionValidationReport extends ValidationReport {
  violations: DimensionViolation[]
}

export type DimensionUsageKind = 'dataset_binding' | 'mapping_set' | 'bridge' | 'calculation_dimension' | 'report_scenario' | 'report_source' | 'assumption_set'

/* ───────────────────────── profiles ───────────────────────── */

export interface DimensionPropertyField {
  key: string
  label: string
  type: 'text' | 'number' | 'date' | 'url' | 'image' | 'select' | 'geo' | 'boolean'
  options?: string[]
  required?: boolean
  description?: string
  /** A text field holding GeoJSON; edited as a JSON textarea with a validity check. */
  format?: 'geojson'
}

/** A system-defined dimension type. A system dimension always has a profile; a user dimension never does. */
export interface DimensionProfile {
  key: string
  displayName: string
  kind: DimensionKind
  owner: string
  editPolicy: DimensionEditPolicy
  visibility: 'manager' | 'module' | 'hidden'
  requiredRoleKeys: string[]
  requiredMemberKeys: string[]
  memberPropertySchema: DimensionPropertyField[]
  cardinality: 'one' | 'many'
  /** Display names for required members and roles, used when the profile is defined. */
  seedNames?: Record<string, string>
  /** Refuses a second role unconditionally. */
  singleRole?: boolean
}

export interface AvailableDimensionProfile {
  profile: DimensionProfile
  dimensionId: Id | null
}

/* ───────────────────────── entities ───────────────────────── */

export interface Dimension {
  id: Id
  key: string
  displayName: string
  kind: DimensionKind
  origin: DefinitionOrigin
  purpose: DimensionPurpose
  /** Set only when purpose is `report`. Links go to this report in the editor, never to the dimension. */
  ownerReport: DimensionOwnerReport | null
  profileKey: string | null
  isEnabled: boolean
  isRemovable: boolean
}

export interface DimensionOwnerReport {
  reportKey: string
  reportName: string
}

export interface DimensionVersion {
  id: Id
  dimensionId: Id
  versionNumber: number
  status: VersionStatus
  rangeDataType: RangeDataType | null
  createdAt: string
  publishedAt: string | null
  retiredAt: string | null
  supersededBy: number | null
  /** Why the version was frozen, e.g. "FY26 close". */
  freezeNote: string | null
}

export interface DimensionVersionSummary {
  id: Id
  versionNumber: number
  status: VersionStatus
  publishedAt: string | null
  retiredAt: string | null
  supersededBy: number | null
  freezeNote: string | null
  memberCount: number
}

export interface DimensionRole {
  id: Id
  dimensionId: Id
  key: string
  displayName: string
  description: string | null
}

export interface DimensionLevel {
  id: Id
  versionId: Id
  key: string
  displayName: string
  ordinal: number
  isLeaf: boolean
  periodicity: Periodicity | null
}

export interface DimensionMember {
  id: Id
  versionId: Id
  key: string
  displayName: string
  description: string | null
  parentId: Id | null
  levelId: Id | null
  isAssignable: boolean
  sortOrder: number
  properties: Record<string, unknown>
}

/** A number for `decimal`, an ISO date for `date`, an ISO date-time for `datetime`; null is open. */
export type RangeBound = string | number | null

export interface RangeBand {
  ordinal: number
  memberId: Id
  lower: RangeBound
  upper: RangeBound
  lowerInclusive: boolean
  upperInclusive: boolean
}

/** Something outside the dimension that references it, one of its roles, or some of its member keys. */
export interface DimensionUsageReference {
  kind: DimensionUsageKind
  name: string
  detail: string
  roleKey?: string
  /** Member keys this object names; deleting them is blocked and the live version must keep them. */
  memberKeys?: string[]
  /** For assumption sets: the version number pinned. */
  pinsVersion?: number
  /** Dataset bindings: dataset version label for blocker phrasing, e.g. "'loans.details' v3". */
  label?: string
}

/* ───────────────────────── read models ───────────────────────── */

export interface DimensionListItem extends Dimension {
  profile: DimensionProfile | null
  roles: DimensionRole[]
  versions: DimensionVersionSummary[]
  /** Members in the live version. */
  publishedMemberCount: number | null
}

export interface DimensionDetail extends DimensionListItem {
  usage: DimensionUsageReference[]
}

export interface DimensionVersionContent {
  version: DimensionVersion
  levels: DimensionLevel[]
  members: DimensionMember[]
  bands: RangeBand[]
}

/** The change-set aggregate content: everything the dimension editor shows. */
export interface DimensionEditState {
  dimension: DimensionDetail
  content: DimensionVersionContent
}

export interface DimensionCreated {
  dimensionId: Id
  versionId: Id
}

/* ───────────────────────── live-and-frozen versions ───────────────────────── */

export interface DimensionFreezeResult extends FreezeResult<DimensionVersionSummary> {
  report: DimensionValidationReport
}

/* ───────────────────────── inputs ───────────────────────── */

export interface CreateDimensionInput {
  key: string
  displayName: string
  kind: DimensionKind
  rangeDataType: RangeDataType | null
  firstRole: DimensionFirstRoleInput | null
  isEnabled: boolean
  isRemovable: boolean
}

export interface DimensionFirstRoleInput {
  key: string
  displayName: string
}

export type DimensionPatch = Partial<Pick<Dimension, 'displayName' | 'isEnabled' | 'isRemovable'>>

export interface DimensionVersionPatch {
  rangeDataType: RangeDataType | null
}

/** A stored `id` updates; an absent or `tmp:<uuid>` id creates. Keys are fixed once created. */
export interface DimensionMemberInput {
  id?: Id
  key: string
  displayName: string
  description: string | null
  parentId: Id | null
  levelId: Id | null
  isAssignable: boolean
  sortOrder: number | null
  properties: Record<string, unknown>
}

export interface DimensionLevelInput {
  id?: Id
  key: string
  displayName: string
  isLeaf: boolean
  periodicity: Periodicity | null
}

export interface DimensionRoleInput {
  id?: Id
  key: string
  displayName: string
  description: string | null
}

export interface DimensionMemberPosition {
  parentId: Id | null
  /** Index among the new siblings after the move. */
  index: number
}

export interface DimensionMemberImportRow {
  key: string
  displayName: string
  parentKey: string | null
}

/** `merge` upserts by key and keeps everything else; `replace` makes the version's members exactly the imported rows. */
export type DimensionMemberImportMode = 'merge' | 'replace'

export interface DimensionMemberImportResult {
  added: DimensionMember[]
  updated: DimensionMember[]
  removed: number
}

export type DimensionDeleteTarget =
  | { type: 'dimension'; dimensionId: Id }
  | { type: 'version'; versionId: Id }
  | { type: 'member'; versionId: Id; memberId: Id }
  | { type: 'level'; versionId: Id; levelId: Id }
  | { type: 'role'; roleId: Id }

/* ───────────────────────── change set ───────────────────────── */

export type DimensionOperation =
  | { op: 'updateDimension'; patch: DimensionPatch }
  | { op: 'updateVersion'; versionId: Id; patch: DimensionVersionPatch }
  | { op: 'saveMember'; versionId: Id; input: DimensionMemberInput }
  | { op: 'deleteMember'; versionId: Id; memberId: Id }
  | { op: 'moveMember'; versionId: Id; memberId: Id; to: DimensionMemberPosition }
  | { op: 'importMembers'; versionId: Id; rows: DimensionMemberImportRow[]; mode: DimensionMemberImportMode }
  | { op: 'clearMembers'; versionId: Id }
  | { op: 'saveLevel'; versionId: Id; input: DimensionLevelInput }
  | { op: 'deleteLevel'; versionId: Id; levelId: Id }
  | { op: 'reorderLevels'; versionId: Id; orderedIds: Id[] }
  | { op: 'saveBands'; versionId: Id; bands: RangeBand[] }
  | { op: 'saveRole'; input: DimensionRoleInput }
  | { op: 'deleteRole'; roleId: Id }

export type DimensionChangeSetRequest = ChangeSetRequest<DimensionOperation>
export type DimensionChangeSetPreview = ChangeSetPreview<DimensionEditState>
export type DimensionChangeSetCommit = ChangeSetCommit<DimensionEditState>

/* ───────────────────────── the port ───────────────────────── */

/** Edits stage into the dimension's change set and apply on `commitChanges`; see lib/api/changes.ts. */
export interface DimensionsApi extends ChangeSetPort {
  /** Analytical dimensions only; report-owned dimensions are reached from their report. */
  listDimensions(origin: DefinitionOrigin): Promise<DimensionListItem[]>
  /** @deprecated Moves to the tenant catalog (OI-13). Kept while lib/mappings resolves dimensions through it. */
  listAllDimensions(): Promise<DimensionListItem[]>
  listAvailableProfiles(): Promise<AvailableDimensionProfile[]>
  getDimension(id: Id): Promise<ApiResult<DimensionDetail>>
  getVersion(versionId: Id): Promise<ApiResult<DimensionVersionContent>>
  /** Runs the rules without changing anything. */
  checkVersion(versionId: Id): Promise<ApiResult<DimensionValidationReport>>
  checkDelete(target: DimensionDeleteTarget): Promise<DeleteCheck>

  updateDimension(id: Id, patch: DimensionPatch): Promise<ApiResult<Dimension>>
  /** Changing the range data type clears every band bound. */
  updateVersion(versionId: Id, patch: DimensionVersionPatch): Promise<ApiResult<DimensionVersion>>
  saveMember(versionId: Id, input: DimensionMemberInput): Promise<ApiResult<DimensionMember>>
  deleteMember(versionId: Id, memberId: Id): Promise<ApiResult<null>>
  /** Returns the member's previous position so callers can undo. */
  moveMember(versionId: Id, memberId: Id, to: DimensionMemberPosition): Promise<ApiResult<DimensionMemberPosition>>
  /** Defaults to `merge`. Replace fails (changing nothing) when a member it would remove is still needed. */
  importMembers(versionId: Id, rows: DimensionMemberImportRow[], mode?: DimensionMemberImportMode): Promise<ApiResult<DimensionMemberImportResult>>
  /** Removes every member. Fails (changing nothing) when any member is still needed. */
  clearMembers(versionId: Id): Promise<ApiResult<{ removed: number }>>
  saveLevel(versionId: Id, input: DimensionLevelInput): Promise<ApiResult<DimensionLevel>>
  deleteLevel(versionId: Id, levelId: Id): Promise<ApiResult<null>>
  reorderLevels(versionId: Id, orderedIds: Id[]): Promise<ApiResult<DimensionLevel[]>>
  saveBands(versionId: Id, bands: RangeBand[]): Promise<ApiResult<RangeBand[]>>
  saveRole(dimensionId: Id, input: DimensionRoleInput): Promise<ApiResult<DimensionRole>>
  deleteRole(roleId: Id): Promise<ApiResult<null>>

  createDimension(input: CreateDimensionInput): Promise<ApiResult<DimensionCreated>>
  defineSystemDimension(profileKey: string): Promise<ApiResult<DimensionCreated>>
  deleteDimension(id: Id): Promise<ApiResult<null>>
  /**
   * Freezes the live version as a read-only snapshot and continues in a new live version cloned from it.
   * Rules run first; on violations nothing changes and `live` is null.
   */
  freezeVersion(versionId: Id, note: string | null): Promise<ApiResult<DimensionFreezeResult>>
  /** Frozen versions only; the live version can't be deleted. */
  deleteVersion(versionId: Id): Promise<ApiResult<null>>
}

/** Port methods staged into the dimension change set; each is one `DimensionOperation`. */
export const DIMENSION_EDITS = [
  'updateDimension',
  'updateVersion',
  'saveMember',
  'deleteMember',
  'moveMember',
  'importMembers',
  'clearMembers',
  'saveLevel',
  'deleteLevel',
  'reorderLevels',
  'saveBands',
  'saveRole',
  'deleteRole',
] as const satisfies readonly (keyof DimensionsApi & DimensionOperation['op'])[]

/** Port methods that run on saved state immediately and are refused while edits are pending. */
export const DIMENSION_COMMANDS = ['createDimension', 'defineSystemDimension', 'deleteDimension', 'freezeVersion', 'deleteVersion'] as const satisfies readonly (keyof DimensionsApi)[]

/* ───────────────────────── shared rules ───────────────────────── */

export const KEY_PATTERN = /^[A-Za-z][A-Za-z0-9_]*$/
export const KEY_HELP = 'A letter, then letters, digits or underscores. Fixed once created.'

export const KIND_LABEL: Record<DimensionKind, string> = {
  categorical: 'Categorical',
  hierarchical: 'Hierarchical',
  range: 'Range',
  time: 'Time',
}

export const RANGE_TYPE_LABEL: Record<RangeDataType, string> = { decimal: 'Decimal', date: 'Date', datetime: 'Date and time' }

export const USAGE_KIND_LABEL: Record<DimensionUsageKind, string> = {
  dataset_binding: 'Dataset binding',
  mapping_set: 'Mapping set',
  bridge: 'Bridge',
  calculation_dimension: 'Calculation dimension',
  report_scenario: 'Report scenario',
  report_source: 'Report source',
  assumption_set: 'Assumption set',
}

export const POLICY_TEXT: Record<DimensionEditPolicy, (owner: string) => string> = {
  open: () => 'You can edit everything; it cannot be deleted.',
  extend: () => 'You can add members and roles and rename things. Required members and roles cannot be removed or re-keyed.',
  locked: (owner) => `Read-only. Managed by ${owner}.`,
}

export function tabsForKind(kind: DimensionKind, purpose: DimensionPurpose = 'analytical'): DimensionTab[] {
  const mappable = purpose === 'analytical' && (kind === 'categorical' || kind === 'hierarchical')
  return [
    'properties',
    'members',
    'levels',
    ...(kind === 'range' ? (['bands'] as const) : []),
    ...(kind === 'time' ? [] : (['roles'] as const)),
    ...(mappable ? (['mappings'] as const) : []),
    ...(kind === 'time' ? [] : (['bridges'] as const)),
    'usage',
  ]
}

/** Tabs whose content is versioned on its own, independent of the dimension version. */
export const SELF_VERSIONED_TABS: DimensionTab[] = ['mappings', 'bridges']

/**
 * Dimensions have one live version (stored as `published`) that is edited in place, plus frozen snapshots
 * (stored as `retired`). There are no drafts.
 */
export const VERSION_LABEL: Partial<Record<VersionStatus, string>> = { published: 'live', retired: 'frozen' }
export const versionLabel = (s: VersionStatus) => VERSION_LABEL[s] ?? s

/** Only the live version is editable, and only when the dimension isn't locked. */
export function isVersionEditable(status: VersionStatus, policy: DimensionEditPolicy | null) {
  return policy !== 'locked' && status === 'published'
}

export function defaultVersion(versions: DimensionVersionSummary[]) {
  return versions.find((v) => v.status === 'published') ?? [...versions].sort((a, b) => b.versionNumber - a.versionNumber)[0]
}
