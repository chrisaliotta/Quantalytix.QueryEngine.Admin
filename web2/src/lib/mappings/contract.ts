/**
 * Business contract for mapping sets and bridges. Screens depend only on these types and the `MappingsApi` port.
 * The mock adapter in ./mock implements it in memory and resolves member keys through the dimensions port,
 * so an HTTP adapter can replace either side without touching the UI.
 */

import type { ApiResult, DeleteCheck, DimensionKind, ProblemDetails, VersionStatus } from '@/lib/dimensions/contract'

export type { ApiResult, DeleteCheck, ProblemDetails, VersionStatus }

export type Cardinality = 'many_to_one' | 'many_to_many'
export type AllocationMode = 'none' | 'partition' | 'replicate'
export type SourceSelection = 'member' | 'descendants'

/** A read-only slice of a dimension: enough to label it and resolve member keys. */
export interface DimensionRef {
  id: string
  key: string
  displayName: string
  kind: DimensionKind
  purpose: 'analytical' | 'report'
  publishedVersionId: string | null
  publishedVersionNumber: number | null
}

export interface MemberRef {
  key: string
  displayName: string
  parentKey: string | null
  isAssignable: boolean
}

export interface MappingSet {
  id: string
  key: string
  displayName: string
  sourceNamespace: string
  dimensionId: string
}

export interface MappingVersion {
  id: string
  mappingSetId: string
  versionNumber: number
  status: VersionStatus
  createdAt: string
  publishedAt: string | null
  retiredAt: string | null
}

export interface MappingEntry {
  id: string
  versionId: string
  sourceKeyValue: string
  memberKey: string
}

export interface BridgeOwner {
  reportId: string
  reportKey: string
  sourceKey: string
}

export interface Bridge {
  id: string
  key: string
  displayName: string
  sourceDimensionId: string
  targetDimensionId: string
  isEnabled: boolean
  /** Set for report bridges, which are edited only in the owning report's rows page. */
  owner: BridgeOwner | null
}

export interface BridgeVersion {
  id: string
  bridgeId: string
  versionNumber: number
  status: VersionStatus
  cardinality: Cardinality
  allocationMode: AllocationMode
  createdAt: string
  publishedAt: string | null
  retiredAt: string | null
}

export interface BridgeMapping {
  id: string
  versionId: string
  sourceMemberKey: string
  targetMemberKey: string
  factor: number
  allocationWeight: number
  sourceSelection: SourceSelection
}

/** A dataset binding that names a mapping set or bridge; drives Usage and delete blockers. */
export interface BindingUsage {
  datasetKey: string
  datasetVersion: number
  fieldKey: string
  roleKey: string
  status: VersionStatus
  /** Bridge bindings: the role of the binding the bridge remaps. */
  sourceRoleKey?: string
}

export interface AssumptionPin {
  assumptionSetKey: string
  assumptionSetName: string
  isActive: boolean
  versionId: string
  versionNumber: number
}

/* ───────────────────────── read models ───────────────────────── */

export interface MappingVersionSummary {
  id: string
  versionNumber: number
  status: VersionStatus
  publishedAt: string | null
  retiredAt: string | null
  entryCount: number
}

export interface MappingSetListItem extends MappingSet {
  dimension: DimensionRef
  versions: MappingVersionSummary[]
  publishedEntryCount: number | null
  bindings: BindingUsage[]
}

export interface MappingSetDetail extends MappingSetListItem {
  pins: AssumptionPin[]
}

export interface MappingVersionContent {
  version: MappingVersion
  entries: MappingEntry[]
}

export interface BridgeVersionSummary {
  id: string
  versionNumber: number
  status: VersionStatus
  cardinality: Cardinality
  allocationMode: AllocationMode
  publishedAt: string | null
  retiredAt: string | null
  mappingCount: number
}

export interface BridgeListItem extends Bridge {
  source: DimensionRef
  target: DimensionRef
  versions: BridgeVersionSummary[]
  publishedMappingCount: number | null
  bindings: BindingUsage[]
}

export interface BridgeDetail extends BridgeListItem {
  pins: AssumptionPin[]
}

export interface BridgeVersionContent {
  version: BridgeVersion
  mappings: BridgeMapping[]
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

export interface Violation {
  code: MappingViolationCode | BridgeViolationCode
  message: string
  /** Entry or mapping id to open, when one item is at fault. */
  itemId?: string
  /** Search text that narrows the table to the offending rows. */
  filter?: string
}

export interface ValidationReport {
  versionId: string
  checkedAt: string
  violations: Violation[]
}

/* ───────────────────────── inputs ───────────────────────── */

export interface CreateMappingSetInput {
  key: string
  displayName: string
  sourceNamespace: string
  dimensionId: string
}

export interface EntryInput {
  id?: string
  sourceKeyValue: string
  memberKey: string
}

export interface CreateBridgeInput {
  key: string
  displayName: string
  sourceDimensionId: string
  targetDimensionId: string
  isEnabled: boolean
}

export interface BridgeVersionInput {
  cardinality: Cardinality
  allocationMode: AllocationMode
  cloneFromVersionId: string | null
}

export interface BridgeMappingInput {
  id?: string
  sourceMemberKey: string
  targetMemberKey: string
  factor: number
  allocationWeight: number
  sourceSelection?: SourceSelection
}

/* ───────────────────────── the port ───────────────────────── */

export interface MappingsApi {
  listDimensionRefs(): Promise<DimensionRef[]>
  /** Members of the dimension's published version; null when it has none. */
  getPublishedMembers(dimensionId: string): Promise<MemberRef[] | null>

  listMappingSets(filter?: { dimensionId?: string }): Promise<MappingSetListItem[]>
  getMappingSet(id: string): Promise<ApiResult<MappingSetDetail>>
  getMappingVersion(versionId: string): Promise<ApiResult<MappingVersionContent>>
  createMappingSet(input: CreateMappingSetInput): Promise<ApiResult<{ mappingSetId: string; versionId: string }>>
  updateMappingSet(id: string, patch: Partial<Pick<MappingSet, 'displayName' | 'sourceNamespace'>>): Promise<ApiResult<MappingSet>>
  checkDeleteMappingSet(id: string): Promise<DeleteCheck>
  deleteMappingSet(id: string): Promise<ApiResult<null>>
  createMappingVersion(setId: string, input: { mode: 'empty' } | { mode: 'clone'; fromVersionId: string }): Promise<ApiResult<MappingVersionSummary>>
  checkMappingVersion(versionId: string): Promise<ApiResult<ValidationReport>>
  validateMappingVersion(versionId: string): Promise<ApiResult<ValidationReport>>
  publishMappingVersion(versionId: string): Promise<ApiResult<ValidationReport>>
  reopenMappingVersion(versionId: string): Promise<ApiResult<MappingVersion>>
  deleteMappingVersion(versionId: string): Promise<ApiResult<null>>
  saveEntry(versionId: string, input: EntryInput): Promise<ApiResult<MappingEntry>>
  deleteEntry(versionId: string, entryId: string): Promise<ApiResult<null>>
  /** Replaces every entry of the version; used by bulk paste. */
  replaceEntries(versionId: string, entries: EntryInput[]): Promise<ApiResult<MappingEntry[]>>

  listBridges(filter?: { dimensionId?: string }): Promise<BridgeListItem[]>
  getBridge(id: string): Promise<ApiResult<BridgeDetail>>
  /** The report bridge a report source writes its row mappings to. */
  findReportBridge(reportKey: string, sourceKey: string): Promise<BridgeListItem | null>
  getBridgeVersion(versionId: string): Promise<ApiResult<BridgeVersionContent>>
  createBridge(input: CreateBridgeInput): Promise<ApiResult<{ bridgeId: string }>>
  updateBridge(id: string, patch: Partial<Pick<Bridge, 'displayName' | 'isEnabled'>>): Promise<ApiResult<Bridge>>
  checkDeleteBridge(id: string): Promise<DeleteCheck>
  deleteBridge(id: string): Promise<ApiResult<null>>
  createBridgeVersion(bridgeId: string, input: BridgeVersionInput): Promise<ApiResult<BridgeVersionSummary>>
  updateBridgeVersion(versionId: string, patch: Pick<BridgeVersion, 'cardinality' | 'allocationMode'>): Promise<ApiResult<BridgeVersion>>
  checkBridgeVersion(versionId: string): Promise<ApiResult<ValidationReport>>
  validateBridgeVersion(versionId: string): Promise<ApiResult<ValidationReport>>
  publishBridgeVersion(versionId: string): Promise<ApiResult<ValidationReport>>
  reopenBridgeVersion(versionId: string): Promise<ApiResult<BridgeVersion>>
  deleteBridgeVersion(versionId: string): Promise<ApiResult<null>>
  saveBridgeMapping(versionId: string, input: BridgeMappingInput): Promise<ApiResult<BridgeMapping>>
  deleteBridgeMapping(versionId: string, mappingId: string): Promise<ApiResult<null>>
  replaceBridgeMappings(versionId: string, rows: BridgeMappingInput[]): Promise<ApiResult<BridgeMapping[]>>
}

/* ───────────────────────── shared rules ───────────────────────── */

export const CARDINALITY_LABEL: Record<Cardinality, string> = { many_to_one: 'Many to one', many_to_many: 'Many to many' }
export const CARDINALITY_HELP: Record<Cardinality, string> = {
  many_to_one: 'Each source member maps to at most one target.',
  many_to_many: 'A source member may map to several targets.',
}
export const ALLOCATION_LABEL: Record<AllocationMode, string> = { none: 'None', partition: 'Partition', replicate: 'Replicate' }
export const ALLOCATION_HELP: Record<AllocationMode, string> = {
  none: 'Each source maps to at most one target; weight is exactly 1. Pure regrouping.',
  partition: 'A source may map to several targets; weights are ≥ 0 and sum to exactly 1 per source.',
  replicate: 'A source counts fully in every target; weights ≥ 0 with no sum constraint. Totals are not additive.',
}

/** Allocation modes a cardinality permits. */
export function allowedModes(c: Cardinality): AllocationMode[] {
  return c === 'many_to_one' ? ['none'] : ['partition', 'replicate']
}

/** Mapping sets can target analytical categorical or hierarchical dimensions. */
export function canHaveMappingSets(d: Pick<DimensionRef, 'kind' | 'purpose'>) {
  return d.purpose === 'analytical' && (d.kind === 'categorical' || d.kind === 'hierarchical')
}
/** Bridges can connect any non-time dimension; report dimensions only receive report bridges. */
export function canHaveBridges(d: Pick<DimensionRef, 'kind'>) {
  return d.kind !== 'time'
}

export function isEditableStatus(s: VersionStatus) {
  return s === 'draft' || s === 'published'
}

export function defaultVersionOf<T extends { id: string; status: VersionStatus; versionNumber: number }>(versions: T[]): T | undefined {
  return (
    versions.find((v) => v.status === 'published') ??
    [...versions].filter((v) => v.status === 'draft' || v.status === 'validated').sort((a, b) => b.versionNumber - a.versionNumber)[0] ??
    [...versions].sort((a, b) => b.versionNumber - a.versionNumber)[0]
  )
}
