/**
 * Business contract for semantic releases and assumption sets. The UI talks only to `ReleasesApi`;
 * the mock adapter in ./mock implements it in memory. Swap the adapter for the real API.
 */

export type VersionStatus = 'draft' | 'validated' | 'published' | 'retired'
export type PinKind = 'dimension' | 'mapping_set' | 'bridge'
export type BindingKind = 'direct' | 'mapped' | 'range' | 'bridge' | 'time'

export interface TenantInfo {
  tenantKey: string
  activeReleaseId: string
  semanticRevision: number
}

export interface Dataset {
  id: string
  key: string
  displayName: string
  isEnabled: boolean
}

export interface Binding {
  name: string
  kind: BindingKind
  dimensionId: string
  mappingSetId?: string
  bridgeId?: string
}

export interface DatasetVersion {
  id: string
  datasetId: string
  versionNumber: number
  status: VersionStatus
  publishedAt: string | null
  bindings: Binding[]
  /** Measures and whether each applies bridge factors. */
  measures: { key: string; appliesBridgeFactors: boolean }[]
}

export interface Release {
  id: string
  key: string
  status: VersionStatus
  createdAt: string
  publishedAt: string | null
}

export interface ReleaseDataset {
  releaseId: string
  datasetId: string
  datasetVersionId: string
}

export interface AssumptionSet {
  id: string
  key: string
  displayName: string
  description: string | null
  status: 'active' | 'historical'
  /** Null for the active set, which reads the active release. */
  releaseId: string | null
  capturedAt: string | null
  createdAt: string
}

export interface Pin {
  setId: string
  kind: PinKind
  identityId: string
  versionId: string
}

export interface IdentityVersion {
  id: string
  versionNumber: number
  status: VersionStatus
  publishedAt: string | null
}

/** Read-only slices of the dimension, mapping and bridge prototypes' data. */
export interface Identity {
  kind: PinKind
  id: string
  key: string
  displayName: string
  /** A dimension itself, a mapping set's dimension, or a bridge's target. */
  dimensionId: string
  /** Set for a report dimension or a bridge into one. */
  reportId: string | null
  createdAt: string
  versions: IdentityVersion[]
}

export interface ReportRef {
  id: string
  key: string
  displayName: string
  isEnabled: boolean
  sources: { key: string; datasetId: string; usesFactors: boolean; measureKeys: string[]; variable?: string }[]
  reads: { kind: PinKind; identityId: string }[]
}

/* ───────────────────────── Results ───────────────────────── */

export interface ProblemDetails {
  title: string
  detail?: string
  /** Field path → messages; '' for form-level messages. */
  errors: Record<string, string[]>
}

export type ApiResult<T> = { ok: true; data: T } | { ok: false; problem: ProblemDetails }

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
export type ReleaseRule = (typeof RELEASE_RULES)[number]

export const RULE_LABEL: Record<ReleaseRule, string> = {
  manifest_not_empty: 'Manifest is not empty',
  pinned_version_belongs_to_dataset: 'Pinned versions belong to their datasets',
  pinned_version_published: 'Pinned versions are published',
  bound_dimensions_resolvable: 'Bound dimensions resolve',
  mapped_bindings_resolvable: 'Mapped bindings resolve',
  bridge_bindings_resolvable: 'Bridge bindings resolve',
  report_sources_resolve: 'Report sources resolve',
  report_factors_apply: 'Report factors apply',
}

export type CheckTarget =
  | { type: 'dataset'; datasetId: string }
  | { type: 'identity'; kind: PinKind; identityId: string }
  | { type: 'report'; reportId: string; sourceKey: string }

export interface ReleaseViolation {
  rule: ReleaseRule
  message: string
  /** The manifest row the violation belongs to, for inline issue icons. */
  datasetId: string | null
  target: CheckTarget | null
}

export interface CheckReport {
  releaseId: string
  /** True when the report ran over unsaved draft edits. */
  unsaved: boolean
  violations: ReleaseViolation[]
  ranAt: string
}

/* ───────────────────────── Release read models ───────────────────────── */

export interface ReleaseDiff {
  fromReleaseId: string
  toReleaseId: string
  added: { datasetId: string; versionId: string }[]
  removed: { datasetId: string; versionId: string; readBy: string[] }[]
  changed: { datasetId: string; fromVersionId: string; toVersionId: string }[]
  unchanged: number
}

export interface ReleaseListItem extends Release {
  isActive: boolean
  datasetCount: number
  /** Null for the active release itself. */
  diffVsActive: ReleaseDiff | null
}

export interface ReleaseDetail {
  release: Release
  isActive: boolean
  manifest: ReleaseDataset[]
}

/** Everything the screens need to render names, versions and links without extra calls. */
export interface Catalog {
  tenant: TenantInfo
  datasets: Dataset[]
  versions: DatasetVersion[]
  identities: Identity[]
  reports: ReportRef[]
  releases: Release[]
}

/* ───────────────────────── Assumption set read models ───────────────────────── */

export type PinState = 'live' | 'frozen' | 'report_owned' | 'unpublished'

export interface PinRow {
  kind: PinKind
  identityId: string
  versionId: string
  publishedVersionId: string | null
  state: PinState
}

export interface SetListItem extends AssumptionSet {
  pinCount: number
  livePins: number
  changedSinceCapture: number
}

export interface Readiness {
  reportId: string
  ready: boolean
  reasons: { message: string; target: CheckTarget | null }[]
}

export interface SetDetail {
  set: AssumptionSet
  /** The release it reads: its own, or the active release for the active set. */
  readsReleaseId: string
  pins: PinRow[]
  readiness: Readiness[] | null
}

/** What `toSetId` pins differently from `fromSetId`. Report-owned identities are left out (captures never pin them). */
export interface SetComparison {
  fromSetId: string
  toSetId: string
  added: { kind: PinKind; identityId: string; versionId: string }[]
  removed: { kind: PinKind; identityId: string; versionId: string }[]
  changed: { kind: PinKind; identityId: string; fromVersionId: string; toVersionId: string }[]
  unchanged: number
  /** Null when both sets read the same release. */
  release: ReleaseDiff | null
}

export interface CapturePreview {
  dimensions: number
  mappingSets: number
  bridges: number
  leftOutDimensions: number
  leftOutBridges: number
  releaseId: string
}

/* ───────────────────────── Inputs ───────────────────────── */

export interface CreateReleaseInput {
  key: string
  copyFromReleaseId: string | null
}

export interface ManifestEntry {
  datasetId: string
  datasetVersionId: string
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

export interface ReleasesApi {
  getCatalog(): Promise<Catalog>
  listReleases(): Promise<ReleaseListItem[]>
  getRelease(id: string): Promise<ApiResult<ReleaseDetail>>
  diffReleases(fromId: string, toId: string, toManifest?: ManifestEntry[]): Promise<ReleaseDiff>
  /** Runs the release rules; a draft's unsaved manifest may be passed in. */
  checkRelease(id: string, manifest?: ManifestEntry[]): Promise<CheckReport>

  createRelease(input: CreateReleaseInput): Promise<ApiResult<Release>>
  saveManifest(id: string, manifest: ManifestEntry[]): Promise<ApiResult<Release>>
  /** Validate and Publish return the check report as data when it fails. */
  validateRelease(id: string): Promise<ApiResult<{ release: Release; report: CheckReport }>>
  publishRelease(id: string): Promise<ApiResult<{ release: Release; report: CheckReport }>>
  backToDraft(id: string): Promise<ApiResult<Release>>
  retireRelease(id: string): Promise<ApiResult<Release>>
  activateRelease(id: string): Promise<ApiResult<TenantInfo>>
  deleteRelease(id: string): Promise<ApiResult<null>>

  listSets(): Promise<{ active: SetListItem; historical: SetListItem[] }>
  getSet(id: string): Promise<ApiResult<SetDetail>>
  compareSets(fromSetId: string, toSetId: string): Promise<SetComparison>
  capturePreview(): Promise<CapturePreview>
  captureSet(input: CaptureSetInput): Promise<ApiResult<AssumptionSet>>
  relabelSet(id: string, input: RelabelSetInput): Promise<ApiResult<AssumptionSet>>
  deleteSet(id: string): Promise<ApiResult<null>>
}

export const RELEASE_TABS = ['manifest', 'changes', 'checks'] as const
export type ReleaseTab = (typeof RELEASE_TABS)[number]
export const SET_TABS = ['pins', 'compare'] as const
export type SetTab = (typeof SET_TABS)[number]
