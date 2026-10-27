import type { ChangeSetPort } from '@/lib/api/changes'

/**
 * Business contract for the dimension manager. Screens depend only on these types and the `DimensionsApi` port;
 * the mock adapter in ./mock implements it in memory, and an HTTP adapter can replace it without touching the UI.
 */

export type DimensionKind = 'categorical' | 'hierarchical' | 'range' | 'time'
export type VersionStatus = 'draft' | 'validated' | 'published' | 'retired'
export type RangeDataType = 'decimal' | 'date'
export type EditPolicy = 'open' | 'extend' | 'locked'
export type Origin = 'system' | 'user'
export type Periodicity = 'day' | 'month' | 'quarter' | 'year'

export type DimensionTab = 'properties' | 'members' | 'levels' | 'bands' | 'roles' | 'mappings' | 'bridges' | 'usage'

export interface PropertyField {
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
  editPolicy: EditPolicy
  visibility: 'manager' | 'module' | 'hidden'
  requiredRoleKeys: string[]
  requiredMemberKeys: string[]
  memberPropertySchema: PropertyField[]
  cardinality: 'one' | 'many'
  /** Display names for required members and roles, used when the profile is defined. */
  seedNames?: Record<string, string>
}

export interface Dimension {
  id: string
  key: string
  displayName: string
  kind: DimensionKind
  origin: Origin
  /** `report` dimensions are hidden: discrete hierarchies that are a report's Rows tree, bound to exactly one report. */
  purpose: 'analytical' | 'report'
  /** Set only when purpose is `report`. Links go to this report in the editor, never to the dimension. */
  ownerReport: OwnerReport | null
  profileKey: string | null
  isEnabled: boolean
  isRemovable: boolean
}

export interface OwnerReport {
  reportKey: string
  reportName: string
}

export interface DimensionVersion {
  id: string
  dimensionId: string
  versionNumber: number
  status: VersionStatus
  rangeDataType: RangeDataType | null
  createdAt: string
  publishedAt: string | null
  retiredAt: string | null
  supersededBy: number | null
  /** Why the version was frozen, e.g. "FY26 close". Frozen versions only. */
  freezeNote?: string | null
}

export interface Role {
  id: string
  dimensionId: string
  key: string
  displayName: string
  description: string | null
}

export interface Level {
  id: string
  versionId: string
  key: string
  displayName: string
  ordinal: number
  isLeaf: boolean
  periodicity: Periodicity | null
}

export interface Member {
  id: string
  versionId: string
  key: string
  displayName: string
  description: string | null
  parentId: string | null
  levelId: string | null
  isAssignable: boolean
  sortOrder: number
  properties: Record<string, unknown>
}

export type Bound = string | number | null

export interface RangeBand {
  ordinal: number
  memberId: string
  lower: Bound
  upper: Bound
  lowerInclusive: boolean
  upperInclusive: boolean
}

export type UsageKind = 'dataset binding' | 'mapping set' | 'bridge' | 'calculation dimension' | 'report scenario' | 'report' | 'assumption set'

/** Something outside the dimension that references it, one of its roles, or some of its member keys. */
export interface UsageReference {
  kind: UsageKind
  name: string
  detail: string
  roleKey?: string
  /** Member keys this object names; deleting them is blocked and a draft missing them fails validation. */
  memberKeys?: string[]
  /** For assumption sets: the version number pinned. */
  pinsVersion?: number
  /** Dataset bindings: dataset version label for blocker phrasing, e.g. "'loans.details' v3". */
  label?: string
  href?: string
}

/* ───────────────────────── read models ───────────────────────── */

export interface VersionSummary {
  id: string
  versionNumber: number
  status: VersionStatus
  publishedAt: string | null
  retiredAt: string | null
  supersededBy: number | null
  freezeNote: string | null
  memberCount: number
}

export interface FreezeResult {
  report: ValidationReport
  /** The new live version, or null when checks found violations. */
  live: VersionSummary | null
}

export interface DimensionListItem extends Dimension {
  profile: DimensionProfile | null
  roles: Role[]
  versions: VersionSummary[]
  /** Members in the published version. */
  publishedMemberCount: number | null
}

export interface DimensionDetail extends DimensionListItem {
  usage: UsageReference[]
}

export interface VersionContent {
  version: DimensionVersion
  levels: Level[]
  members: Member[]
  bands: RangeBand[]
}

export interface AvailableProfile {
  profile: DimensionProfile
  dimensionId: string | null
}

export interface Violation {
  code: 'duplicateMemberKey' | 'duplicateLevelKey' | 'cycle' | 'rangeType' | 'bandOverlap' | 'bandOrder' | 'bandMember' | 'bandOpenBound' | 'requiredMember' | 'requiredRole' | 'namedKeyMissing'
  message: string
  tab: DimensionTab
  /** Member id, level id, role id or band ordinal as a string. */
  itemId?: string
}

export interface ValidationReport {
  versionId: string
  checkedAt: string
  violations: Violation[]
}

/** Reasons something cannot be deleted, already phrased for display. Empty `reasons` means deletable. */
export interface DeleteCheck {
  allowed: boolean
  message: string | null
  reasons: string[]
}

export type DeleteTarget =
  | { type: 'dimension'; dimensionId: string }
  | { type: 'version'; versionId: string }
  | { type: 'member'; versionId: string; memberId: string }
  | { type: 'level'; versionId: string; levelId: string }
  | { type: 'role'; roleId: string }

/* ───────────────────────── results & errors ───────────────────────── */

export interface ProblemDetails {
  title: string
  detail?: string
  status: number
  /** Field path → messages. */
  errors: Record<string, string[]>
}

export type ApiResult<T> = { ok: true; data: T } | { ok: false; problem: ProblemDetails }

/* ───────────────────────── inputs ───────────────────────── */

export interface CreateDimensionInput {
  key: string
  displayName: string
  kind: DimensionKind
  rangeDataType: RangeDataType | null
  firstRole: { key: string; displayName: string } | null
  isEnabled: boolean
  isRemovable: boolean
}

export interface MemberInput {
  id?: string
  key: string
  displayName: string
  description: string | null
  parentId: string | null
  levelId: string | null
  isAssignable: boolean
  sortOrder: number | null
  properties: Record<string, unknown>
}

export interface LevelInput {
  id?: string
  key: string
  displayName: string
  isLeaf: boolean
  periodicity: Periodicity | null
}

export interface RoleInput {
  id?: string
  key: string
  displayName: string
  description: string | null
}

export interface MovePosition {
  parentId: string | null
  /** Index among the new siblings after the move. */
  index: number
}

export interface ImportRow {
  key: string
  displayName: string
  parentKey: string | null
}

/** `merge` upserts by key and keeps everything else; `replace` makes the version's members exactly the imported rows. */
export type ImportMode = 'merge' | 'replace'

export interface ImportResult {
  added: Member[]
  updated: Member[]
  removed: number
}

/* ───────────────────────── the port ───────────────────────── */

/** Edits stage into the change set and apply on `commitChanges`; see lib/api/changes.ts. */
export interface DimensionsApi extends ChangeSetPort {
  listDimensions(origin: Origin): Promise<DimensionListItem[]>
  /** Every dimension of any purpose (analytical and report-owned), for cross-module resolution such as bridges. Not for the list screen. */
  listAllDimensions(): Promise<DimensionListItem[]>
  /** Report-owned (hidden) dimensions. System admins only; anyone else gets 403. */
  listHiddenDimensions(): Promise<ApiResult<DimensionListItem[]>>
  listAvailableProfiles(): Promise<AvailableProfile[]>
  getDimension(id: string): Promise<ApiResult<DimensionDetail>>
  getVersion(versionId: string): Promise<ApiResult<VersionContent>>

  createDimension(input: CreateDimensionInput): Promise<ApiResult<{ dimensionId: string; versionId: string }>>
  defineSystemDimension(profileKey: string): Promise<ApiResult<{ dimensionId: string; versionId: string }>>
  updateDimension(id: string, patch: Partial<Pick<Dimension, 'displayName' | 'isEnabled' | 'isRemovable'>>): Promise<ApiResult<Dimension>>
  checkDelete(target: DeleteTarget): Promise<DeleteCheck>
  deleteDimension(id: string): Promise<ApiResult<null>>

  updateVersion(versionId: string, patch: { rangeDataType: RangeDataType | null }): Promise<ApiResult<DimensionVersion>>
  /** Runs the integrity checks without changing anything. */
  checkVersion(versionId: string): Promise<ApiResult<ValidationReport>>
  /**
   * Freezes the live version as a read-only snapshot and continues editing in a new live version cloned from it.
   * Checks run first; on violations nothing changes and `live` is null.
   */
  freezeVersion(versionId: string, note: string | null): Promise<ApiResult<FreezeResult>>
  /** Frozen versions only; the live version can't be deleted. */
  deleteVersion(versionId: string): Promise<ApiResult<null>>

  saveMember(versionId: string, input: MemberInput): Promise<ApiResult<Member>>
  deleteMember(versionId: string, memberId: string): Promise<ApiResult<null>>
  /** Returns the member's previous position so callers can undo. */
  moveMember(versionId: string, memberId: string, to: MovePosition): Promise<ApiResult<MovePosition>>
  /** Defaults to `merge`. Replace fails (changing nothing) when a member it would remove is still needed. */
  importMembers(versionId: string, rows: ImportRow[], mode?: ImportMode): Promise<ApiResult<ImportResult>>
  /** Removes every member. Fails (changing nothing) when any member is still needed. */
  clearMembers(versionId: string): Promise<ApiResult<{ removed: number }>>

  saveLevel(versionId: string, input: LevelInput): Promise<ApiResult<Level>>
  deleteLevel(versionId: string, levelId: string): Promise<ApiResult<null>>
  reorderLevels(versionId: string, orderedIds: string[]): Promise<ApiResult<Level[]>>

  saveBands(versionId: string, bands: RangeBand[]): Promise<ApiResult<RangeBand[]>>

  saveRole(dimensionId: string, input: RoleInput): Promise<ApiResult<Role>>
  deleteRole(roleId: string): Promise<ApiResult<null>>
}

/* ───────────────────────── shared rules ───────────────────────── */

export const KEY_PATTERN = /^[A-Za-z][A-Za-z0-9_]*$/
export const KEY_HELP = 'A letter, then letters, digits or underscores. Fixed once created.'

export const KIND_LABEL: Record<DimensionKind, string> = {
  categorical: 'Categorical',
  hierarchical: 'Hierarchical',
  range: 'Range',
  time: 'Time',
}

export const RANGE_TYPE_LABEL: Record<RangeDataType, string> = { decimal: 'Decimal', date: 'Date' }

export const POLICY_TEXT: Record<EditPolicy, (owner: string) => string> = {
  open: () => 'You can edit everything; it cannot be deleted.',
  extend: () => 'You can add members and roles and rename things. Required members and roles cannot be removed or re-keyed.',
  locked: (owner) => `Read-only. Managed by ${owner}.`,
}

export function tabsForKind(kind: DimensionKind, purpose: Dimension['purpose'] = 'analytical'): DimensionTab[] {
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
export function isVersionEditable(status: VersionStatus, policy: EditPolicy | null) {
  return policy !== 'locked' && status === 'published'
}

export function defaultVersion(versions: VersionSummary[]) {
  return versions.find((v) => v.status === 'published') ?? [...versions].sort((a, b) => b.versionNumber - a.versionNumber)[0]
}
