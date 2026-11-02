# 14 Snapshots

## Purpose and sources

A request asks one question: which **snapshot** it reads, the live one by default. A snapshot holds at most one version of
every dataset, dimension, mapping set, bridge, report and calculation model. The live snapshot is edited in place through
each module's change set and is valid after every commit. This module captures frozen snapshots from live, restores a frozen
snapshot into live, relabels and deletes frozen snapshots, and compares two snapshots.

Sources: the snapshot versioning refactor plan; schema `versioning/` (`qe_snapshot`, `qe_snapshot_restore`) and the
`snapshot_id` of every version table; `vw_qe_snapshot*`; the engine's `snapshot` request property.

## Domain rules

| # | Rule | Source |
|---|---|---|
| R1 | Every version of the six kinds belongs to exactly one snapshot; an identity has at most one version per snapshot. | Schema doc §6 |
| R2 | Exactly one snapshot is live, with key `live`; it is neither relabelled nor deleted. | Schema doc §6 |
| R3 | Live versions are edited in place, and every commit runs every gate and commit rule. Live is always valid, so capture does not validate. | Schema doc §18 |
| R4 | Capture copies every live version of every kind, changed or not, into a new frozen snapshot under new ids, then writes the `qe_snapshot` row with `captured_at` and `captured_by`. Live ids never change. | Plan, Decisions |
| R5 | Capture reads `semantic_revision` at the start and the end; when it changed, capture deletes its copies and refuses with 409. | Plan, Decisions |
| R6 | A frozen snapshot's versions never change. | Schema doc §6 |
| R7 | Restore makes live equal a frozen snapshot: every commit rule runs on the snapshot's versions first and any violation refuses with 422; then each live version's content is replaced by a copy of the frozen version, keeping its id; an identity the snapshot lacks loses its live version, and its identity row when no other snapshot holds a version of it. The restore is logged in `qe_snapshot_restore`. | Plan, Decisions |
| R8 | Relabel changes a frozen snapshot's key, display name and description. Delete removes every version of the snapshot with its content. | Plan, §3 |
| R9 | Capture, restore, relabel and delete each advance `semantic_revision`. | Plan, Decisions |
| R10 | An identity cannot be deleted while a frozen snapshot holds a version of it (`referenced_by_snapshot`), for all six kinds. | Plan, §3 |
| R11 | Managed-table DDL refuses to rename or drop a table or column that any dataset version, live or frozen, reads. | Plan, Decisions |
| R12 | Snapshot key: a dotted key (`DottedKey`, as `seed.assumptions.2024` shows), unique; `live` is reserved. | Seed |
| R13 | A report comparison that names a snapshot computes with that snapshot's whole report version: sources, bridges, report dimension, model and datasets. | Plan, Decisions |

## Frontend contract

`web/src/lib/snapshots` holds the contract, the HTTP adapter and the mock adapter; `web/src/components/snapshots` the list,
capture, restore confirmation, comparison, and read-only browsing of a frozen snapshot.

| Item | Contract |
|---|---|
| `Snapshot` | `id`, `key`, `displayName`, `description`, `isLive`, `capturedAt`, `capturedBy`, `createdAt` |
| `SnapshotListItem` | `Snapshot` plus the count of versions per kind and the last restore, if any |
| `SnapshotDetail` | `Snapshot` plus one row per identity it holds: kind, identity id and key, version id, and whether the version differs from the identity's live version |
| `SnapshotComparison` | Per kind: identities only one side holds, and identities whose versions differ |
| `SnapshotCapturePreview` | The count of live versions per kind a capture would copy, and the live `semanticRevision` |
| `SnapshotKind` | `dataset`, `dimension`, `mapping_set`, `bridge`, `calculation_model`, `report` |
| `TenantInfo` | From [10](10-tenant-session-catalog.md); the tenant chip shows the live `semanticRevision` |

### Port (Proposed)

```ts
export interface SnapshotsApi {
  // reads
  listSnapshots(): Promise<SnapshotListItem[]>
  getSnapshot(id: Id): Promise<ApiResult<SnapshotDetail>>
  compareSnapshots(fromId: Id, toId: Id): Promise<SnapshotComparison>
  capturePreview(): Promise<SnapshotCapturePreview>

  // commands
  captureSnapshot(input: CaptureSnapshotInput): Promise<ApiResult<Snapshot>>    // { key, displayName, description }
  restoreSnapshot(id: Id): Promise<ApiResult<TenantInfo>>
  relabelSnapshot(id: Id, input: RelabelSnapshotInput): Promise<ApiResult<Snapshot>>
  deleteSnapshot(id: Id): Promise<ApiResult<null>>
}
```

Snapshots have no change set: every action is a command on stored state, and the client blocks capture and restore while any
change set is open. `restoreSnapshot` answers the kernel `TenantInfo` with the advanced `semanticRevision`, so open editors know
to reload. A frozen snapshot's content is browsed read-only through each module's version reads.

## HTTP API

Prefix `/api/tenants/{t}`. Open to any tenant user, including capture, restore and delete (OI-09). The routes below are the
blueprint's proposal; `api/openapi/contract/snapshots.yaml` is authoritative for paths and schemas.

| Method | Route | Request | Response | Errors | Port |
|---|---|---|---|---|---|
| GET | `snapshots` | | `SnapshotListItem[]` | | `listSnapshots` |
| GET | `snapshots/capture-preview` | | `SnapshotCapturePreview` | | `capturePreview` |
| POST | `snapshots` | `CaptureSnapshotInput` | `201 Snapshot` | 400, 409 key taken, 409 revision changed during capture | `captureSnapshot` |
| GET | `snapshots/{id}` | | `SnapshotDetail` | 404 | `getSnapshot` |
| GET | `snapshots/compare?from=&to=` | | `SnapshotComparison` | 404 | `compareSnapshots` |
| POST | `snapshots/{id}/restore` | | `TenantInfo` | 409 live snapshot, 422 commit rules | `restoreSnapshot` |
| PUT | `snapshots/{id}` | `RelabelSnapshotInput` | `Snapshot` | 400, 409 live snapshot or key taken | `relabelSnapshot` |
| DELETE | `snapshots/{id}` | | `204` | 409 live snapshot | `deleteSnapshot` |

## Rule codes

Restore runs the commit rules of every kind ([11](11-datasets.md#rule-codes), [12](12-dimensions.md#rule-codes),
[13](13-mappings-and-bridges.md#rule-codes), [15](15-calculation-models.md#rule-codes), [16](16-reports.md#rule-codes)) on the versions
of the snapshot, resolving keys in the snapshot. Capture runs none.

## Derived read models

| Read model | Computation |
|---|---|
| `SnapshotListItem` counts | Versions per kind with the snapshot's `snapshot_id` |
| `SnapshotDetail` rows | One per version of the snapshot; `differsFromLive` compares the version's content with the identity's live version |
| `SnapshotComparison` | Identity set difference per kind, plus the identities both hold whose content differs |
| `SnapshotCapturePreview` | Live versions per kind and the current `semantic_revision` |

## Schema mapping

| Contract | Table |
|---|---|
| `Snapshot` | `qe_snapshot` |
| last restore | `qe_snapshot_restore` |
| `SnapshotDetail` rows | `snapshot_id` of `qe_dataset_version`, `qe_dimension_version`, `qe_mapping_version`, `qe_bridge_version`, `qe_report_version`, `qe_calculation_model_version` |
| `TenantInfo.semanticRevision` | `qe_database_info.semantic_revision` |

## Deltas from the current API

| Change | Item |
|---|---|
| Remove | `releases` and every release route: manifest change set, `diff`, `check`, `validate`, `publish`, `reopen`, `retire`, `activate`, `manifest-datasets`, `identities`, `report-reads` |
| Remove | `assumption-sets` and its pins |
| Remove | The per-kind `freeze` and `check` routes of dimension, mapping and bridge versions, and the dataset version lifecycle routes ([11](11-datasets.md), [12](12-dimensions.md), [13](13-mappings-and-bridges.md)) |
| New | `snapshots`: capture, capture preview, detail, compare, restore, relabel, delete |
| Change | `TenantInfo` loses `activeReleaseId` |

## Decisions applied

[OI-09](90-open-issues.md#oi-09), [OI-14](90-open-issues.md#oi-14), [OI-29](90-open-issues.md#oi-29).
