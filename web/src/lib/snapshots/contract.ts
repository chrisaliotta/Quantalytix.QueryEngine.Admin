/**
 * Business contract for snapshots (blueprint 14). Every type mirrors the schema of the same name in
 * api/openapi/contract/snapshots.yaml. Screens depend only on these types and the `SnapshotsApi` port; ./mock implements it in
 * memory and ./http over the Admin API.
 *
 * Snapshots have no change set: every method is a command or a read on stored state. A frozen snapshot's content is browsed
 * read-only through each module's version read.
 */
import type { TenantInfo } from '@/lib/tenant/contract'
import type { ApiResult, Id } from '@/lib/api/contract'

export type { ApiResult, Id, Problem } from '@/lib/api/contract'
export type { TenantInfo } from '@/lib/tenant/contract'

/** The identities a snapshot holds one version of, in capture order. */
export type SnapshotKind = 'dataset' | 'dimension' | 'mapping_set' | 'bridge' | 'calculation_model' | 'report'
export const SNAPSHOT_KINDS: readonly SnapshotKind[] = ['dataset', 'dimension', 'mapping_set', 'bridge', 'calculation_model', 'report']

/** `SnapshotKey`: fixed after capture; requests name the snapshot by it. */
export const SNAPSHOT_KEY_PATTERN = /^[A-Za-z][A-Za-z0-9_]{0,127}$/
export const LIVE_SNAPSHOT_KEY = 'live'

/** One row of `qe_snapshot`. */
export interface Snapshot {
  id: Id
  key: string
  displayName: string
  description: string | null
  /** The live snapshot is edited in place; it is never relabelled, deleted or restored. */
  isLive: boolean
  /** Null for the live snapshot. */
  capturedAt: string | null
  /** The token subject of whoever captured a frozen snapshot; null for the live snapshot. */
  capturedBy: string | null
  createdAt: string
}

/** How many versions a snapshot holds, or a capture would copy, of each kind; report dimensions and bridges included. */
export interface SnapshotCounts {
  datasets: number
  dimensions: number
  mappingSets: number
  bridges: number
  calculationModels: number
  reports: number
}

/** One row of the restore log. */
export interface SnapshotRestore {
  snapshotId: Id
  restoredAt: string
  restoredBy: string
}

export interface SnapshotListItem extends Snapshot {
  counts: SnapshotCounts
  /** Null when never restored, and always for the live snapshot. */
  lastRestore: SnapshotRestore | null
}

export interface SnapshotList {
  live: SnapshotListItem
  /** Newest capture first. */
  frozen: SnapshotListItem[]
}

/** The version a snapshot holds of one identity; the kind's version read shows it. */
export interface SnapshotEntry {
  kind: SnapshotKind
  identityId: Id
  key: string
  /** A model's or report's as its version names it. */
  displayName: string
  versionId: Id
}

export interface SnapshotDetail {
  snapshot: Snapshot
  /** Kind by kind in capture order, each by key. */
  entries: SnapshotEntry[]
  /** Restores into live, newest first; none for the live snapshot. */
  restores: SnapshotRestore[]
}

/** An identity both compared snapshots hold, with each one's version. */
export interface SnapshotPair {
  kind: SnapshotKind
  identityId: Id
  key: string
  fromVersionId: Id
  toVersionId: Id
}

/** What `to` holds that `from` does not (`added`), the reverse (`removed`), and what both hold (`common`). */
export interface SnapshotComparison {
  fromSnapshotId: Id
  toSnapshotId: Id
  added: SnapshotEntry[]
  removed: SnapshotEntry[]
  common: SnapshotPair[]
}

export interface CaptureSnapshotInput {
  key: string
  displayName: string
  description: string | null
}

export interface RelabelSnapshotInput {
  displayName: string
  description: string | null
}

export interface SnapshotsApi {
  // reads
  listSnapshots(): Promise<SnapshotList>
  getSnapshot(id: Id): Promise<ApiResult<SnapshotDetail>>
  compareSnapshots(fromId: Id, toId: Id): Promise<SnapshotComparison>
  /** The live versions of each kind a capture would copy now. */
  capturePreview(): Promise<SnapshotCounts>

  // commands
  /** Refused with 409 when the key is taken or live changed during the capture. */
  captureSnapshot(input: CaptureSnapshotInput): Promise<ApiResult<Snapshot>>
  /** Makes live equal the snapshot; refused with 422 and the violations when a version breaks a rule. */
  restoreSnapshot(id: Id): Promise<ApiResult<TenantInfo>>
  relabelSnapshot(id: Id, input: RelabelSnapshotInput): Promise<ApiResult<Snapshot>>
  deleteSnapshot(id: Id): Promise<ApiResult<null>>
}

export const KIND_LABEL: Record<SnapshotKind, string> = {
  dataset: 'Datasets',
  dimension: 'Dimensions',
  mapping_set: 'Mapping sets',
  bridge: 'Bridges',
  calculation_model: 'Calculation models',
  report: 'Reports',
}

export const KIND_ONE: Record<SnapshotKind, string> = {
  dataset: 'dataset',
  dimension: 'dimension',
  mapping_set: 'mapping set',
  bridge: 'bridge',
  calculation_model: 'calculation model',
  report: 'report',
}

/** The `SnapshotCounts` field of each kind. */
export const COUNT_OF: Record<SnapshotKind, keyof SnapshotCounts> = {
  dataset: 'datasets',
  dimension: 'dimensions',
  mapping_set: 'mappingSets',
  bridge: 'bridges',
  calculation_model: 'calculationModels',
  report: 'reports',
}

export const totalVersions = (counts: SnapshotCounts) => SNAPSHOT_KINDS.reduce((n, kind) => n + counts[COUNT_OF[kind]], 0)

export const SNAPSHOT_TABS = ['contents', 'compare', 'restores'] as const
export type SnapshotTab = (typeof SNAPSHOT_TABS)[number]
