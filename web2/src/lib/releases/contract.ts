/**
 * Business contract for semantic releases and assumption sets (blueprint 14). Every type mirrors the schema of the
 * same name in api/openapi/contract/releases.yaml. The UI talks only to `ReleasesApi`; ./mock implements it in memory.
 */
import type { ChangeSetPort } from '@/lib/api/changes'
import type { ApiResult, AssumptionSetStatus, BindingKind, ChangeSetCommit, ChangeSetPreview, ChangeSetRequest, Id, VersionStatus, Violation, ViolationTarget } from '@/lib/api/contract'

export type { ApiResult, AssumptionSetStatus, BindingKind, Id, VersionStatus } from '@/lib/api/contract'

export type PinKind = 'dimension' | 'mapping_set' | 'bridge'

/* ───────────────────────── Releases ───────────────────────── */

export interface Release {
  id: Id
  key: string
  status: VersionStatus
  createdAt: string
  publishedAt: string | null
  /** Hash of the sorted manifest, computed on publish. */
  contentHash: string | null
}

export interface ReleaseActivation {
  releaseId: Id
  /** Null for the tenant's first activation. */
  previousReleaseId: Id | null
  activatedAt: string
  /** The token subject of the caller. */
  activatedBy: string
}

export interface ManifestEntry {
  datasetId: Id
  datasetVersionId: Id
}

export interface ReleaseDiffEntry {
  datasetId: Id
  versionId: Id
}

export interface ReleaseDiffRemoval extends ReleaseDiffEntry {
  /** Keys of the enabled reports with a source on the dataset. */
  readBy: string[]
}

export interface ReleaseDiffChange {
  datasetId: Id
  fromVersionId: Id
  toVersionId: Id
}

export interface ReleaseDiff {
  fromReleaseId: Id
  toReleaseId: Id
  added: ReleaseDiffEntry[]
  removed: ReleaseDiffRemoval[]
  changed: ReleaseDiffChange[]
  unchanged: number
}

export interface ReleaseListItem extends Release {
  isActive: boolean
  datasetCount: number
  /** Null for the active release itself. */
  diffVsActive: ReleaseDiff | null
  /** Set for the active release only. */
  activatedAt: string | null
  activatedBy: string | null
}

export interface ReleaseDetail {
  release: Release
  isActive: boolean
  manifest: ManifestEntry[]
  /** Newest first. */
  activations: ReleaseActivation[]
}

/* ───────────────────────── Release checks ───────────────────────── */

export const RELEASE_RULES = [
  'manifest_not_empty',
  'pinned_version_belongs_to_dataset',
  'pinned_version_published',
  'bound_dimensions_resolvable',
  'mapped_bindings_resolvable',
  'bridge_bindings_resolvable',
  'report_sources_resolve',
  'report_factors_apply',
] as const
export type ReleaseRuleCode = (typeof RELEASE_RULES)[number]

export const RULE_LABEL: Record<ReleaseRuleCode, string> = {
  manifest_not_empty: 'Manifest is not empty',
  pinned_version_belongs_to_dataset: 'Pinned versions belong to their datasets',
  pinned_version_published: 'Pinned versions are published',
  bound_dimensions_resolvable: 'Bound dimensions resolve',
  mapped_bindings_resolvable: 'Mapped bindings resolve',
  bridge_bindings_resolvable: 'Bridge bindings resolve',
  report_sources_resolve: 'Report sources resolve',
  report_factors_apply: 'Report factors apply',
}

export type ReleaseTargetArea = 'dataset' | PinKind | 'report'

/** A dataset, an identity (`area` is its kind) or a report source (`itemId` the report, `itemKey` the source key). */
export interface ReleaseViolationTarget extends ViolationTarget {
  area: ReleaseTargetArea
  itemId: Id
}

export interface ReleaseViolation extends Violation {
  code: ReleaseRuleCode
  /** The manifest row the violation belongs to, for inline issue icons. */
  datasetId: Id | null
  target?: ReleaseViolationTarget
}

export interface ReleaseCheckReport {
  releaseId: Id
  /** True when the report ran over staged, unsaved manifest edits. */
  unsaved: boolean
  checkedAt: string
  violations: ReleaseViolation[]
}

export interface ReleaseTransitionResult {
  release: Release
  report: ReleaseCheckReport
}

/* ───────────────────────── Release change set ───────────────────────── */

export type ReleaseOperation = { op: 'saveManifest'; manifest: ManifestEntry[] }
export type ReleaseChangeSetRequest = ChangeSetRequest<ReleaseOperation>
export interface ReleaseChangeSetPreview extends ChangeSetPreview<ReleaseDetail> {
  violations: ReleaseViolation[]
  report: ReleaseCheckReport
  diffVsActive: ReleaseDiff | null
}
export type ReleaseChangeSetCommit = ChangeSetCommit<ReleaseDetail>

/* ───────────────────────── Projections the screens render ───────────────────────── */

export interface ManifestBinding {
  displayName: string
  kind: BindingKind
  dimensionId: Id
  mappingSetId: Id | null
  bridgeId: Id | null
}

export interface ManifestMeasure {
  key: string
  appliesBridgeFactors: boolean
}

export interface ManifestDatasetVersion {
  id: Id
  versionNumber: number
  status: VersionStatus
  publishedAt: string | null
  bindings: ManifestBinding[]
  measures: ManifestMeasure[]
}

export interface ManifestDataset {
  id: Id
  key: string
  displayName: string
  isEnabled: boolean
  /** Newest first. */
  versions: ManifestDatasetVersion[]
}

/** Living kinds have no drafts (OI-14): a version is live (published) or frozen (retired). */
export interface PinIdentityVersion {
  id: Id
  versionNumber: number
  status: Extract<VersionStatus, 'published' | 'retired'>
  publishedAt: string
}

export interface PinIdentity {
  kind: PinKind
  id: Id
  key: string
  displayName: string
  /** A dimension itself, a mapping set's dimension, or a bridge's target. */
  dimensionId: Id
  /** Set for a report dimension or a bridge into one. */
  reportId: Id | null
  createdAt: string
  versions: PinIdentityVersion[]
}

export interface ReleaseReportSource {
  key: string
  datasetId: Id
  usesFactors: boolean
  measureKeys: string[]
}

export interface ReleaseReportRead {
  kind: PinKind
  identityId: Id
}

export interface ReleaseReportRef {
  id: Id
  key: string
  displayName: string
  isEnabled: boolean
  sources: ReleaseReportSource[]
  reads: ReleaseReportRead[]
}

/* ───────────────────────── Assumption sets ───────────────────────── */


export interface AssumptionSet {
  id: Id
  key: string
  displayName: string
  description: string | null
  status: AssumptionSetStatus
  /** Null for the active set, which reads the active release. */
  releaseId: Id | null
  capturedAt: string | null
  createdAt: string
}

export interface SetListItem extends AssumptionSet {
  pinCount: number
  livePins: number
  changedSinceCapture: number
}

export interface AssumptionSetList {
  active: SetListItem
  historical: SetListItem[]
}

export type PinState = 'live' | 'frozen' | 'report_owned'

export interface PinRow {
  kind: PinKind
  identityId: Id
  versionId: Id
  publishedVersionId: Id | null
  state: PinState
}

export interface SetReadinessReason {
  message: string
  target: ReleaseViolationTarget
}

export interface SetReadiness {
  reportId: Id
  ready: boolean
  reasons: SetReadinessReason[]
}

export interface SetDetail {
  set: AssumptionSet
  /** The release it reads: its own, or the active release for the active set. */
  readsReleaseId: Id
  pins: PinRow[]
  /** Null for the active set. */
  readiness: SetReadiness[] | null
}

export interface SetPinEntry {
  kind: PinKind
  identityId: Id
  versionId: Id
}

export interface SetPinChange {
  kind: PinKind
  identityId: Id
  fromVersionId: Id
  toVersionId: Id
}

/** What `toSetId` pins differently from `fromSetId`. Report-owned identities are left out (captures never pin them). */
export interface SetComparison {
  fromSetId: Id
  toSetId: Id
  added: SetPinEntry[]
  removed: SetPinEntry[]
  changed: SetPinChange[]
  unchanged: number
  /** Null when both sets read the same release. */
  release: ReleaseDiff | null
}

export interface SetCapturePreview {
  dimensions: number
  mappingSets: number
  bridges: number
  leftOutDimensions: number
  leftOutBridges: number
  releaseId: Id
}

/* ───────────────────────── Inputs ───────────────────────── */

export interface CreateReleaseInput {
  key: string
  copyFromReleaseId: Id | null
}

export interface CaptureSetInput {
  key: string
  displayName: string
  description: string | null
}

export interface RelabelSetInput {
  displayName: string
  description: string | null
}

export const RELEASE_KEY_PATTERN = /^[A-Za-z0-9][A-Za-z0-9_.\-/]{0,127}$/
export const SET_KEY_PATTERN = /^[A-Za-z][A-Za-z0-9_]{0,127}$/

/* ───────────────────────── Port ───────────────────────── */

/** One change set per draft release: its manifest. */
export interface ReleasesApi extends ChangeSetPort {
  // reads
  listReleases(): Promise<ReleaseListItem[]>
  getRelease(id: Id): Promise<ApiResult<ReleaseDetail>>
  diffReleases(fromId: Id, toId: Id): Promise<ReleaseDiff>
  /** The saved manifest's report; while a manifest change set is open, the pending manifest's (`unsaved`). */
  checkRelease(id: Id): Promise<ReleaseCheckReport>
  listManifestDatasets(): Promise<ManifestDataset[]>
  listIdentities(): Promise<PinIdentity[]>
  listReportReads(): Promise<ReleaseReportRef[]>
  listSets(): Promise<AssumptionSetList>
  getSet(id: Id): Promise<ApiResult<SetDetail>>
  compareSets(fromSetId: Id, toSetId: Id): Promise<SetComparison>
  capturePreview(): Promise<SetCapturePreview>

  // edits (staged per draft release)
  saveManifest(id: Id, manifest: ManifestEntry[]): Promise<ApiResult<ReleaseDetail>>

  // commands
  createRelease(input: CreateReleaseInput): Promise<ApiResult<Release>>
  /** Validate and Publish return a failed rule run as data (the HTTP adapter converts the 422). */
  validateRelease(id: Id): Promise<ApiResult<ReleaseTransitionResult>>
  publishRelease(id: Id): Promise<ApiResult<ReleaseTransitionResult>>
  backToDraft(id: Id): Promise<ApiResult<Release>>
  retireRelease(id: Id): Promise<ApiResult<Release>>
  activateRelease(id: Id): Promise<ApiResult<ReleaseActivation>>
  deleteRelease(id: Id): Promise<ApiResult<null>>
  captureSet(input: CaptureSetInput): Promise<ApiResult<AssumptionSet>>
  relabelSet(id: Id, input: RelabelSetInput): Promise<ApiResult<AssumptionSet>>
  deleteSet(id: Id): Promise<ApiResult<null>>
}

export const RELEASE_EDITS = ['saveManifest'] as const satisfies readonly (keyof ReleasesApi)[]
export const RELEASE_COMMANDS = [
  'createRelease',
  'validateRelease',
  'publishRelease',
  'backToDraft',
  'retireRelease',
  'activateRelease',
  'deleteRelease',
  'captureSet',
  'relabelSet',
  'deleteSet',
] as const satisfies readonly (keyof ReleasesApi)[]

export const RELEASE_TABS = ['manifest', 'changes', 'checks'] as const
export type ReleaseTab = (typeof RELEASE_TABS)[number]
export const SET_TABS = ['pins', 'compare'] as const
export type SetTab = (typeof SET_TABS)[number]
