/**
 * Business contract for mapping sets and bridges (blueprint 13). Every type mirrors the schema of the same name in
 * api/openapi/contract/mappings.yaml. Screens depend only on these types, the `MappingsApi` port and the
 * `MappingsDirectory`; adapters replace either side without touching the UI.
 */

import type { ChangeSetPort } from '@/lib/api/changes'
import type { ApiResult, DeleteBlocker, DeleteCheck, DimensionKind, DimensionPurpose, Id, Problem, SnapshotMembership, SourceSelection, Violation } from '@/lib/api/contract'

export type { ApiResult, DeleteBlocker, DeleteCheck, DimensionKind, DimensionPurpose, Id, Problem, SourceSelection, Violation }
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
  /** The dimension's version in the same snapshot, whose members entry and mapping keys resolve against; null when it holds none. */
  versionId: Id | null
}

/** A member of a dimension's live version, as the pickers and labels show it. */
export interface MemberRef {
  key: string
  displayName: string
  parentKey: string | null
  isAssignable: boolean
}

/** Dimension refs and live members the editors resolve keys against; served by `TenantApi` (OI-13). */
export interface MappingsDirectory {
  listDimensionRefs(): Promise<MappingDimensionRef[]>
  /** Members of the dimension's live version; null when the dimension does not exist. */
  getPublishedMembers(dimensionId: Id): Promise<MemberRef[] | null>
}

/** A dataset binding that names a mapping set or bridge by identity; drives Used by and delete blockers. */
export interface MappingBindingUsage {
  datasetId: Id
  datasetKey: string
  /** The dataset version that holds the binding, in the same snapshot. */
  datasetVersionId: Id
  fieldKey: string
  roleKey: string
  /** Bridge bindings: the role of the binding the bridge remaps. */
  sourceRoleKey: string | null
}

export type MappingViolationCode = 'source_key_unique' | 'dimension_version_in_snapshot' | 'entry_member_in_version' | 'entry_member_assignable'
export type BridgeViolationCode =
  | 'endpoint_versions_in_snapshot'
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

/** The live version (edited in place) or a frozen snapshot's version (read-only). */
export interface MappingVersion extends SnapshotMembership {
  id: Id
  mappingSetId: Id
  createdAt: string
}

export interface MappingEntry {
  id: Id
  versionId: Id
  sourceKeyValue: string
  memberKey: string
}

export interface MappingVersionSummary extends SnapshotMembership {
  id: Id
  createdAt: string
  entryCount: number
}

export interface MappingSetListItem extends MappingSet {
  dimension: MappingDimensionRef
  /** One per snapshot holding the mapping set: the live version first, then frozen ones newest first. */
  versions: MappingVersionSummary[]
  /** The live version, which change sets edit. */
  liveVersionId: Id
  liveEntryCount: number
  bindings: MappingBindingUsage[]
}

export interface MappingCoverage {
  assignableMemberCount: number
  uncoveredMemberKeys: string[]
}

export interface MappingVersionContent {
  version: MappingVersion
  entries: MappingEntry[]
  coverage: MappingCoverage
  /** Commit-rule findings on this content; `target.itemId` is the entry at fault. Empty on a saved live version. */
  violations: Violation[]
}

export interface MappingSetEditState {
  set: MappingSetListItem
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

/** The live version (edited in place) or a frozen snapshot's version (read-only). */
export interface BridgeVersion extends SnapshotMembership {
  id: Id
  bridgeId: Id
  cardinality: MappingCardinality
  allocationMode: MappingAllocationMode
  /** Whether the engine offers the bridge in the version's snapshot. */
  isEnabled: boolean
  createdAt: string
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

export interface BridgeVersionSummary extends SnapshotMembership {
  id: Id
  cardinality: MappingCardinality
  allocationMode: MappingAllocationMode
  createdAt: string
  mappingCount: number
}

export interface BridgeListItem extends Bridge {
  source: MappingDimensionRef
  target: MappingDimensionRef
  /** One per snapshot holding the bridge: the live version first, then frozen ones newest first. */
  versions: BridgeVersionSummary[]
  /** The live version, which change sets edit. */
  liveVersionId: Id
  liveMappingCount: number
  bindings: MappingBindingUsage[]
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
  bridge: BridgeListItem
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

export type BridgeOperation =
  | { op: 'updateBridge'; patch: BridgePatch }
  | { op: 'updateBridgeVersion'; versionId: Id; patch: BridgeVersionPatch }
  | { op: 'saveBridgeMapping'; versionId: Id; input: BridgeMappingInput }
  | { op: 'deleteBridgeMapping'; versionId: Id; mappingId: Id }
  | { op: 'replaceBridgeMappings'; versionId: Id; rows: BridgeMappingInput[] }

/* ───────────────────────── the port ───────────────────────── */

/**
 * Edits are staged per mapping set or analytical bridge (identity plus its live version) and saved by `commitChanges`, which the
 * commit rules refuse while any finding remains. Frozen versions are read-only. Report bridges are read here and written only
 * through their report's change set.
 */
export interface MappingsApi extends ChangeSetPort {
  // reads
  listMappingSets(filter?: { dimensionId?: Id }): Promise<MappingSetListItem[]>
  getMappingSet(id: Id): Promise<ApiResult<MappingSetListItem>>
  /** Any version, live or frozen. */
  getMappingVersion(versionId: Id): Promise<ApiResult<MappingVersionContent>>
  checkDeleteMappingSet(id: Id): Promise<DeleteCheck>
  listBridges(filter?: { dimensionId?: Id }): Promise<BridgeListItem[]>
  getBridge(id: Id): Promise<ApiResult<BridgeListItem>>
  /** The report bridge a report source writes its row mappings to. */
  findReportBridge(reportId: Id, sourceKey: string): Promise<BridgeListItem | null>
  /** Any version, live or frozen. */
  getBridgeVersion(versionId: Id): Promise<ApiResult<BridgeVersionContent>>
  checkDeleteBridge(id: Id): Promise<DeleteCheck>

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
  /** Allowed only while no live binding uses it and no frozen snapshot holds it. */
  deleteMappingSet(id: Id): Promise<ApiResult<null>>
  createBridge(input: CreateBridgeInput): Promise<ApiResult<BridgeCreated>>
  /** Allowed only while no live binding uses it and no frozen snapshot holds it. */
  deleteBridge(id: Id): Promise<ApiResult<null>>
}

export const MAPPING_SET_EDITS = ['updateMappingSet', 'saveEntry', 'deleteEntry', 'replaceEntries'] as const satisfies readonly (MappingOperation['op'] & keyof MappingsApi)[]
export const BRIDGE_EDITS = ['updateBridge', 'updateBridgeVersion', 'saveBridgeMapping', 'deleteBridgeMapping', 'replaceBridgeMappings'] as const satisfies readonly (BridgeOperation['op'] & keyof MappingsApi)[]
export const MAPPINGS_COMMANDS = ['createMappingSet', 'deleteMappingSet', 'createBridge', 'deleteBridge'] as const satisfies readonly (keyof MappingsApi)[]

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
