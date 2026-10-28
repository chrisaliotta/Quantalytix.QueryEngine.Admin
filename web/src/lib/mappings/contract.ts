/**
 * Business contract for mapping sets and bridges (blueprint 13). Every type mirrors the schema of the same name in
 * api/openapi/contract/mappings.yaml. Screens depend only on these types, the `MappingsApi` port and the
 * `MappingsDirectory`; adapters replace either side without touching the UI.
 */

import type { ChangeSetPort } from '@/lib/api/changes'
import type { ApiResult, DeleteBlocker, DeleteCheck, DimensionKind, DimensionPurpose, FreezeResult, Id, Problem, SourceSelection, ValidationReport, VersionStatus, Violation } from '@/lib/api/contract'

export type { ApiResult, DeleteBlocker, DeleteCheck, DimensionKind, DimensionPurpose, FreezeResult, Id, Problem, SourceSelection, ValidationReport, VersionStatus, Violation }
export { newTempId } from '@/lib/api/contract'

export type MappingCardinality = 'many_to_one' | 'many_to_many'
export type MappingAllocationMode = 'none' | 'partition' | 'replicate'

/** A read-only slice of a dimension: enough to label it and to state which version keys resolve against. */
export interface MappingDimensionRef {
  id: Id
  key: string
  displayName: string
  kind: DimensionKind
  purpose: DimensionPurpose
  publishedVersionId: Id | null
  publishedVersionNumber: number | null
}

/** A member of a dimension's live version, as the pickers and labels show it. */
export interface MemberRef {
  key: string
  displayName: string
  parentKey: string | null
  isAssignable: boolean
}

/** Dimension refs and published members the editors resolve keys against; served by `TenantApi` (OI-13). */
export interface MappingsDirectory {
  listDimensionRefs(): Promise<MappingDimensionRef[]>
  /** Members of the dimension's live version; null when it has none. */
  getPublishedMembers(dimensionId: Id): Promise<MemberRef[] | null>
}

/** A dataset binding that names a mapping set or bridge by identity; drives Used by and delete blockers. */
export interface MappingBindingUsage {
  datasetId: Id
  datasetKey: string
  datasetVersionId: Id
  datasetVersionNumber: number
  status: VersionStatus
  fieldKey: string
  roleKey: string
  /** Bridge bindings: the role of the binding the bridge remaps. */
  sourceRoleKey: string | null
}

export interface MappingAssumptionPin {
  assumptionSetId: Id
  assumptionSetKey: string
  assumptionSetDisplayName: string
  isActive: boolean
  versionId: Id
  versionNumber: number
}

export type MappingViolationCode = 'source_key_unique' | 'dimension_version_published' | 'entry_member_in_version' | 'entry_member_assignable'
export type BridgeViolationCode =
  | 'endpoint_versions_published'
  | 'mapping_members_in_endpoint_versions'
  | 'cardinality_many_to_one'
  | 'allocation_none'
  | 'allocation_partition'
  | 'allocation_replicate'
  | 'pair_unique'
  | 'descendants_report_only'

/* ───────────────────────── mapping sets ───────────────────────── */

export interface MappingSet {
  id: Id
  key: string
  displayName: string
  sourceNamespace: string
  dimensionId: Id
}

/** `published` is the live version, `retired` a frozen snapshot. */
export interface MappingVersion {
  id: Id
  mappingSetId: Id
  versionNumber: number
  status: VersionStatus
  createdAt: string
  publishedAt: string | null
  retiredAt: string | null
  freezeNote: string | null
}

export interface MappingEntry {
  id: Id
  versionId: Id
  sourceKeyValue: string
  memberKey: string
}

export interface MappingVersionSummary {
  id: Id
  versionNumber: number
  status: VersionStatus
  createdAt: string
  publishedAt: string | null
  retiredAt: string | null
  freezeNote: string | null
  entryCount: number
}

export interface MappingSetListItem extends MappingSet {
  dimension: MappingDimensionRef
  versions: MappingVersionSummary[]
  publishedEntryCount: number
  bindings: MappingBindingUsage[]
}

export interface MappingSetDetail extends MappingSetListItem {
  pins: MappingAssumptionPin[]
}

export interface MappingCoverage {
  assignableMemberCount: number
  uncoveredMemberKeys: string[]
}

export interface MappingVersionContent {
  version: MappingVersion
  entries: MappingEntry[]
  coverage: MappingCoverage
  /** Rule findings on this content; `target.itemId` is the entry at fault. Empty on a saved live version. */
  violations: Violation[]
}

export interface MappingSetEditState {
  set: MappingSetDetail
  content: MappingVersionContent
}

export interface CreateMappingSetInput {
  key: string
  displayName: string
  sourceNamespace: string
  dimensionId: Id
}

export interface MappingSetCreated {
  mappingSetId: Id
  versionId: Id
}

export type MappingSetPatch = Partial<Pick<MappingSet, 'displayName' | 'sourceNamespace'>>

export interface MappingEntryInput {
  /** The entry's id, or `newTempId()` for a new entry. */
  id: Id
  sourceKeyValue: string
  memberKey: string
}

export type MappingFreezeResult = FreezeResult<MappingVersionSummary>

export type MappingOperation =
  | { op: 'updateMappingSet'; patch: MappingSetPatch }
  | { op: 'saveEntry'; versionId: Id; input: MappingEntryInput }
  | { op: 'deleteEntry'; versionId: Id; entryId: Id }
  | { op: 'replaceEntries'; versionId: Id; entries: MappingEntryInput[] }

/* ───────────────────────── bridges ───────────────────────── */

export interface BridgeOwner {
  reportId: Id
  reportKey: string
  sourceKey: string
}

export interface Bridge {
  id: Id
  key: string
  displayName: string
  sourceDimensionId: Id
  targetDimensionId: Id
  isEnabled: boolean
  /** Set for report bridges, which are written only through their report's change set. */
  owner: BridgeOwner | null
}

/** `published` is the live version, `retired` a frozen snapshot. */
export interface BridgeVersion {
  id: Id
  bridgeId: Id
  versionNumber: number
  status: VersionStatus
  cardinality: MappingCardinality
  allocationMode: MappingAllocationMode
  createdAt: string
  publishedAt: string | null
  retiredAt: string | null
  freezeNote: string | null
}

export interface BridgeMapping {
  id: Id
  versionId: Id
  sourceMemberKey: string
  targetMemberKey: string
  factor: number
  allocationWeight: number
  sourceSelection: SourceSelection
}

export interface BridgeVersionSummary {
  id: Id
  versionNumber: number
  status: VersionStatus
  cardinality: MappingCardinality
  allocationMode: MappingAllocationMode
  createdAt: string
  publishedAt: string | null
  retiredAt: string | null
  freezeNote: string | null
  mappingCount: number
}

export interface BridgeListItem extends Bridge {
  source: MappingDimensionRef
  target: MappingDimensionRef
  versions: BridgeVersionSummary[]
  publishedMappingCount: number
  bindings: MappingBindingUsage[]
}

export interface BridgeDetail extends BridgeListItem {
  pins: MappingAssumptionPin[]
}

export interface BridgeSourceAllocation {
  sourceMemberKey: string
  targetCount: number
  weightSum: number
}

export interface BridgeAllocationSummary {
  sources: BridgeSourceAllocation[]
}

export interface BridgeVersionContent {
  version: BridgeVersion
  mappings: BridgeMapping[]
  allocation: BridgeAllocationSummary
  /** Rule findings; `target.itemId` is the mapping at fault, `target.itemKey` the source member of per-source rules. */
  violations: Violation[]
}

export interface BridgeEditState {
  bridge: BridgeDetail
  content: BridgeVersionContent
}

export interface CreateBridgeInput {
  key: string
  displayName: string
  sourceDimensionId: Id
  targetDimensionId: Id
  isEnabled: boolean
}

export interface BridgeCreated {
  bridgeId: Id
  versionId: Id
}

export type BridgePatch = Partial<Pick<Bridge, 'displayName' | 'isEnabled'>>
export type BridgeVersionPatch = Pick<BridgeVersion, 'cardinality' | 'allocationMode'>

export interface BridgeMappingInput {
  /** The mapping's id, or `newTempId()` for a new mapping. */
  id: Id
  sourceMemberKey: string
  targetMemberKey: string
  factor: number
  allocationWeight: number
  /** Report bridges only, written by the report editor. */
  sourceSelection?: SourceSelection
}

export type BridgeFreezeResult = FreezeResult<BridgeVersionSummary>

export type BridgeOperation =
  | { op: 'updateBridge'; patch: BridgePatch }
  | { op: 'updateBridgeVersion'; versionId: Id; patch: BridgeVersionPatch }
  | { op: 'saveBridgeMapping'; versionId: Id; input: BridgeMappingInput }
  | { op: 'deleteBridgeMapping'; versionId: Id; mappingId: Id }
  | { op: 'replaceBridgeMappings'; versionId: Id; rows: BridgeMappingInput[] }

/* ───────────────────────── the port ───────────────────────── */

/**
 * Edits are staged per mapping set or analytical bridge (identity plus its live version) and saved by `commitChanges`.
 * Report bridges are read here and written only through their report's change set.
 */
export interface MappingsApi extends ChangeSetPort {
  // reads
  listMappingSets(filter?: { dimensionId?: Id }): Promise<MappingSetListItem[]>
  getMappingSet(id: Id): Promise<ApiResult<MappingSetDetail>>
  getMappingVersion(versionId: Id): Promise<ApiResult<MappingVersionContent>>
  checkMappingVersion(versionId: Id): Promise<ApiResult<ValidationReport>>
  checkDeleteMappingSet(id: Id): Promise<DeleteCheck>
  checkDeleteMappingVersion(versionId: Id): Promise<DeleteCheck>
  listBridges(filter?: { dimensionId?: Id }): Promise<BridgeListItem[]>
  getBridge(id: Id): Promise<ApiResult<BridgeDetail>>
  /** The report bridge a report source writes its row mappings to. */
  findReportBridge(reportId: Id, sourceKey: string): Promise<BridgeListItem | null>
  getBridgeVersion(versionId: Id): Promise<ApiResult<BridgeVersionContent>>
  checkBridgeVersion(versionId: Id): Promise<ApiResult<ValidationReport>>
  checkDeleteBridge(id: Id): Promise<DeleteCheck>
  checkDeleteBridgeVersion(versionId: Id): Promise<DeleteCheck>

  // edits: a mapping set's identity and its live version
  updateMappingSet(id: Id, patch: MappingSetPatch): Promise<ApiResult<MappingSet>>
  saveEntry(versionId: Id, input: MappingEntryInput): Promise<ApiResult<MappingEntry>>
  deleteEntry(versionId: Id, entryId: Id): Promise<ApiResult<null>>
  /** Replaces every entry of the version; used by bulk paste. */
  replaceEntries(versionId: Id, entries: MappingEntryInput[]): Promise<ApiResult<MappingEntry[]>>

  // edits: an analytical bridge's identity and its live version
  updateBridge(id: Id, patch: BridgePatch): Promise<ApiResult<Bridge>>
  updateBridgeVersion(versionId: Id, patch: BridgeVersionPatch): Promise<ApiResult<BridgeVersion>>
  saveBridgeMapping(versionId: Id, input: BridgeMappingInput): Promise<ApiResult<BridgeMapping>>
  deleteBridgeMapping(versionId: Id, mappingId: Id): Promise<ApiResult<null>>
  replaceBridgeMappings(versionId: Id, rows: BridgeMappingInput[]): Promise<ApiResult<BridgeMapping[]>>

  // commands
  createMappingSet(input: CreateMappingSetInput): Promise<ApiResult<MappingSetCreated>>
  deleteMappingSet(id: Id): Promise<ApiResult<null>>
  freezeMappingVersion(versionId: Id, note: string | null): Promise<ApiResult<MappingFreezeResult>>
  /** Frozen versions only. */
  deleteMappingVersion(versionId: Id): Promise<ApiResult<null>>
  createBridge(input: CreateBridgeInput): Promise<ApiResult<BridgeCreated>>
  deleteBridge(id: Id): Promise<ApiResult<null>>
  freezeBridgeVersion(versionId: Id, note: string | null): Promise<ApiResult<BridgeFreezeResult>>
  /** Frozen versions only. */
  deleteBridgeVersion(versionId: Id): Promise<ApiResult<null>>
}

export const MAPPING_SET_EDITS = ['updateMappingSet', 'saveEntry', 'deleteEntry', 'replaceEntries'] as const satisfies readonly (MappingOperation['op'] & keyof MappingsApi)[]
export const BRIDGE_EDITS = ['updateBridge', 'updateBridgeVersion', 'saveBridgeMapping', 'deleteBridgeMapping', 'replaceBridgeMappings'] as const satisfies readonly (BridgeOperation['op'] & keyof MappingsApi)[]
export const MAPPINGS_COMMANDS = [
  'createMappingSet',
  'deleteMappingSet',
  'freezeMappingVersion',
  'deleteMappingVersion',
  'createBridge',
  'deleteBridge',
  'freezeBridgeVersion',
  'deleteBridgeVersion',
] as const satisfies readonly (keyof MappingsApi)[]

/* ───────────────────────── shared module rules ───────────────────────── */

export const CARDINALITY_LABEL: Record<MappingCardinality, string> = { many_to_one: 'Many to one', many_to_many: 'Many to many' }
export const CARDINALITY_HELP: Record<MappingCardinality, string> = {
  many_to_one: 'Each source member maps to at most one target.',
  many_to_many: 'A source member may map to several targets.',
}
export const ALLOCATION_LABEL: Record<MappingAllocationMode, string> = { none: 'None', partition: 'Partition', replicate: 'Replicate' }
export const ALLOCATION_HELP: Record<MappingAllocationMode, string> = {
  none: 'Each source maps to at most one target; weight is exactly 1. Pure regrouping.',
  partition: 'A source may map to several targets; weights are ≥ 0 and sum to exactly 1 per source.',
  replicate: 'A source counts fully in every target; weights ≥ 0 with no sum constraint. Totals are not additive.',
}

/** Rule codes a cardinality or allocation change can raise on existing mappings. */
export const SETTINGS_VIOLATION_CODES: readonly BridgeViolationCode[] = ['cardinality_many_to_one', 'allocation_none', 'allocation_partition', 'allocation_replicate']

/** Allocation modes a cardinality permits. */
export function allowedModes(c: MappingCardinality): MappingAllocationMode[] {
  return c === 'many_to_one' ? ['none'] : ['partition', 'replicate']
}

/** Mapping sets can target analytical categorical or hierarchical dimensions. */
export function canHaveMappingSets(d: Pick<MappingDimensionRef, 'kind' | 'purpose'>) {
  return d.purpose === 'analytical' && (d.kind === 'categorical' || d.kind === 'hierarchical')
}
/** Bridges can connect any non-time dimension; report dimensions only receive report bridges. */
export function canHaveBridges(d: Pick<MappingDimensionRef, 'kind'>) {
  return d.kind !== 'time'
}

/** Only the live version accepts edits. */
export function isEditableStatus(s: VersionStatus) {
  return s === 'published'
}

export function liveVersionOf<T extends { status: VersionStatus }>(versions: T[]): T {
  return versions.find((v) => v.status === 'published')!
}

export function versionLabel(s: VersionStatus) {
  return s === 'published' ? 'live' : 'frozen'
}
