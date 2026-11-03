# 14 Snapshots

## Purpose and sources

A request asks one question: which **snapshot** it reads, the live one by default. A snapshot holds at most one version of
every dataset, dimension, mapping set, bridge, report and calculation model. The live snapshot is edited in place through
each module's change set and is valid after every commit. This module captures frozen snapshots from live, restores a frozen
snapshot into live, relabels and deletes frozen snapshots, and compares two snapshots.

Sources: `api/openapi/contract/snapshots.yaml`; `Application/Snapshots` (`SnapshotCommands`, `SnapshotQueries`,
`SnapshotIndex`, `LiveSnapshot`) and `WebApi/Snapshots/SnapshotsController`; `web/src/lib/snapshots/contract.ts`; schema
`versioning/` (`qe_snapshot`, `qe_snapshot_restore`) and the `snapshot_id` of every version table.

## Domain rules

| # | Rule | Source |
|---|---|---|
| R1 | Every version of the six kinds belongs to exactly one snapshot; an identity has at most one version per snapshot. | Schema doc §6 |
| R2 | Exactly one snapshot is live, with key `live`. It is never relabelled, deleted or restored (409). | Contract `Snapshot.isLive` |
| R3 | Live versions are edited in place, and every commit runs every gate and commit rule; disabled reports and models included. Live is always valid, so capture does not validate. | Schema doc §18 |
| R4 | Capture copies every live version of every kind, report dimensions and report bridges included, changed or not, into a new frozen snapshot under new ids, then writes the `qe_snapshot` row with `captured_at` and `captured_by`. Live ids never change. | `SnapshotCommands.CaptureAsync` |
| R5 | Capture reads `semantic_revision` before and after the copies; when it changed, capture deletes its copies and refuses with 409. A taken key is also 409. | `captureSnapshot` |
| R6 | A frozen snapshot's versions never change. A dataset change set or materialization aimed at a frozen version is refused with 409 "Version is frozen"; the other kinds' change sets are posted on the identity and always edit its live version, so their frozen versions have no edit route. | Contract; `DatasetRefusals.FrozenTitle` |
| R7 | Restore makes live equal a frozen snapshot. Every version the snapshot holds must first pass its kind's rules, resolved in the snapshot against the database as it is now; any violation refuses with 422 and nothing changes. Then each frozen version replaces its identity's live content, keeping the live version id, or joins live when live lacks the identity; the live version of an identity the snapshot lacks is deleted, with the identity when no frozen snapshot holds it. The restore is logged in `qe_snapshot_restore`. | `restoreSnapshot` |
| R8 | Relabel changes a frozen snapshot's display name and description only; the key and the versions never change. | `relabelSnapshot` |
| R9 | Delete removes a frozen snapshot, its restore log and every version it holds with their content, and each identity no other snapshot holds. | `deleteSnapshot` |
| R10 | Capture, restore and delete advance `semantic_revision`; relabel does not, because requests name a snapshot by its fixed key. | Contract |
| R11 | An identity cannot be deleted while a frozen snapshot holds a version of it (`referenced_by_snapshot`), for all six kinds. | `SnapshotHolders` |
| R12 | Renaming a managed table is refused with 409 while any other dataset version, live or frozen, reads it. | `DatasetSettingsEdits` |
| R13 | Snapshot key (`SnapshotKey`): `^[A-Za-z][A-Za-z0-9_.]{0,127}$` (dots allowed, as in the seed's `seed.assumptions.2024`), unique, fixed after capture; `live` is the live snapshot's. | Contract |
| R14 | A report comparison that names a snapshot computes with that snapshot's whole report version: sources, bridges, report dimension, model and datasets. | Engine README |

## Frontend contract

`web/src/lib/snapshots` holds the contract, the HTTP adapter and the mock adapter; `web/src/components/snapshots` the
Snapshots page (`/snapshots`, `/snapshots/:snapshotId`) with the list, capture and relabel sheets, the restore confirmation,
the comparison, read-only browsing of a frozen snapshot's entries, and the shell's live revision chip (`live · rev N`), which
opens the page.

| Item | Contract |
|---|---|
| `Snapshot` | `id`, `key`, `displayName`, `description`, `isLive`, `capturedAt`, `capturedBy`, `createdAt` |
| `SnapshotCounts` | Versions per kind: `datasets`, `dimensions`, `mappingSets`, `bridges`, `calculationModels`, `reports`; report dimensions and bridges counted |
| `SnapshotListItem` | `Snapshot` plus `counts` and `lastRestore` (`SnapshotRestore` or null) |
| `SnapshotList` | `{ live, frozen }`, frozen newest capture first |
| `SnapshotEntry` | `kind`, `identityId`, `key`, `displayName`, `versionId`, `dimensionId` (a mapping set's dimension, a bridge's source dimension) and `reportId` (the owning report of a report dimension or report bridge) |
| `SnapshotDetail` | `{ snapshot, entries, restores }`; entries kind by kind in capture order, each by key; restores newest first |
| `SnapshotPair` | `kind`, `identityId`, `key`, `fromVersionId`, `toVersionId`, `differs` (the engine-visible content differs, compared by keys rather than surrogate ids) |
| `SnapshotComparison` | `fromSnapshotId`, `toSnapshotId`, `added` (only `to` holds), `removed` (only `from` holds), `common` (`SnapshotPair[]`) |
| `SnapshotKind` | `dataset`, `dimension`, `mapping_set`, `bridge`, `calculation_model`, `report` |
| `TenantInfo` | From [10](10-tenant-session-catalog.md); the answer of a restore |

### Port (Agreed)

```ts
export interface SnapshotsApi {
  // reads
  listSnapshots(): Promise<SnapshotList>
  getSnapshot(id: Id): Promise<ApiResult<SnapshotDetail>>
  compareSnapshots(fromId: Id, toId: Id): Promise<SnapshotComparison>
  capturePreview(): Promise<SnapshotCounts>

  // commands
  captureSnapshot(input: CaptureSnapshotInput): Promise<ApiResult<Snapshot>>        // { key, displayName, description }
  restoreSnapshot(id: Id): Promise<ApiResult<TenantInfo>>
  relabelSnapshot(id: Id, input: RelabelSnapshotInput): Promise<ApiResult<Snapshot>>  // { displayName, description }
  deleteSnapshot(id: Id): Promise<ApiResult<null>>
}
```

Snapshots have no change set: every action is a command on stored state, and the client blocks capture and restore while any
change set is open. `restoreSnapshot` answers the kernel `TenantInfo` with the advanced `semanticRevision`, so open editors know
to reload. A frozen snapshot's content is browsed read-only through each kind's version read: `dataset-versions/{id}`,
`dimension-versions/{id}`, `mapping-versions/{id}`, `bridge-versions/{id}`, `calculation-model-versions/{id}` and
`report-versions/{id}`, with the `versionId` an entry names.

## HTTP API

Prefix `/api/tenants/{tenantId}`. Open to any tenant user, including capture, restore and delete (OI-09).

| Method | Route | Request | Response | Errors | Port |
|---|---|---|---|---|---|
| GET | `snapshots` | | `SnapshotList` | | `listSnapshots` |
| POST | `snapshots` | `CaptureSnapshotInput` | `201 Snapshot` | 400, 409 key taken or live changed during the capture | `captureSnapshot` |
| GET | `snapshots/capture-preview` | | `SnapshotCounts` | | `capturePreview` |
| GET | `snapshots/compare?from=&to=` | | `SnapshotComparison` | 404 | `compareSnapshots` |
| GET | `snapshots/{snapshotId}` | | `SnapshotDetail` | 404 | `getSnapshot` |
| PUT | `snapshots/{snapshotId}` | `RelabelSnapshotInput` | `Snapshot` | 400, 404, 409 live snapshot | `relabelSnapshot` |
| DELETE | `snapshots/{snapshotId}` | | `204` | 400, 404, 409 live snapshot | `deleteSnapshot` |
| POST | `snapshots/{snapshotId}/restore` | | `TenantInfo` | 400, 404, 409 live snapshot, 422 a version breaks a rule | `restoreSnapshot` |

## Rule codes

Restore runs every kind's `IVersionRules` ([11](11-datasets.md#rule-codes), [12](12-dimensions.md#rule-codes),
[13](13-mappings-and-bridges.md#rule-codes), [15](15-calculation-models.md#rule-codes), [16](16-reports.md#rule-codes)) on the
versions of the snapshot, resolving keys in the snapshot. Capture runs none.

## Derived read models

| Read model | Computation |
|---|---|
| `SnapshotCounts` | Versions per kind with the snapshot's `snapshot_id`, from `SnapshotIndex`; the capture preview counts live |
| `SnapshotEntry.dimensionId`, `reportId` | The mapping set's dimension or the bridge's source dimension; the report owning a report dimension or report bridge |
| `SnapshotComparison` | Identity set difference per kind (`added`, `removed`), and the identities both hold (`common`) |
| `SnapshotPair.differs` | Whether the two versions' engine-visible content differs, compared by keys rather than surrogate ids |

## Schema mapping

| Contract | Table |
|---|---|
| `Snapshot` | `qe_snapshot` |
| `SnapshotRestore` | `qe_snapshot_restore` |
| `SnapshotEntry` | `snapshot_id` of `qe_dataset_version`, `qe_dimension_version`, `qe_mapping_version`, `qe_bridge_version`, `qe_calculation_model_version`, `qe_report_version`, joined to their identities |
| `TenantInfo.semanticRevision` | `qe_database_info.semantic_revision` |

## Deltas from the current API

| Change | Item |
|---|---|
| Remove | `releases` and every release route: manifest change set, `diff`, `check`, `validate`, `publish`, `reopen`, `retire`, `activate`, `manifest-datasets`, `identities`, `report-reads` |
| Remove | `assumption-sets` and its pins |
| Remove | The per-kind `check`, `freeze`, `validate`, `publish`, `reopen`, `retire` and version delete routes ([11](11-datasets.md), [12](12-dimensions.md), [13](13-mappings-and-bridges.md)) |
| New | `snapshots`: list, capture, capture preview, detail, compare, restore, relabel, delete |
| New | `calculation-model-versions/{id}` and `report-versions/{id}` reads |
| Change | `TenantInfo` loses `activeReleaseId` |

## Decisions applied

[OI-09](90-open-issues.md#oi-09), [OI-14](90-open-issues.md#oi-14).
