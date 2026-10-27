# 14 Releases and assumption sets

## Purpose and sources

A request asks two questions: which dataset versions (the active **release**) and which dimension, mapping and bridge
versions (the **assumption set**, active by default). This module authors releases, activates them, captures
historical assumption sets and compares them.

Sources: [`web2/src/lib/releases/contract.ts`](../../web2/src/lib/releases/contract.ts), `rules.ts`,
`mock/adapter.ts`; [releases and assumption sets brief](../requirements/releases-and-assumption-sets-prototype.md);
current `ReleasesController`, `AssumptionSetsController`; schema `release/`, `assumption/`.

## Domain rules

| # | Rule | Source |
|---|---|---|
| R1 | A release pins one published dataset version per dataset; unpinned datasets do not exist for queries while it is active. | Brief A2 |
| R2 | Strict lifecycle: draft, validated, published, retired; validated can return to draft. Only a draft's manifest changes. | Brief A2 |
| R3 | Several releases may be published; publishing activates and retires nothing. | Brief A2 |
| R4 | Only a published release can be activated; activation sets `active_semantic_release_id`, writes an activation log row and advances `semantic_revision`. Activating an older release is a rollback. | Brief A2; OI-40 |
| R5 | The active release cannot be retired (`release_not_active`); a retired release cannot be activated. | Brief A2 |
| R6 | Only a draft release is deleted. | Brief A2 |
| R7 | Release key: `^[A-Za-z0-9][A-Za-z0-9_.\-/]{0,127}$`, unique. A new release copies a manifest (active by default) or starts empty. | Brief A2 |
| R8 | Exactly one active set (key `active`), never deleted; it pins every live (published) version and reads the active release. | Brief A3; schema doc §18 rule 12 |
| R13 | Publishing computes `content_hash` over the sorted manifest (`dataset_id`, `dataset_version_id` pairs). | OI-41 |
| R14 | A release has no display name; screens show its key. | OI-39 |
| R9 | A historical set is captured from the active set's **analytical** pins (report dimensions and bridges into them left out), records `captured_at` and the active release; its pins and release never change. | Brief A3; §18 rules 13, 36 |
| R10 | A historical set can be relabelled (name, description; key fixed) and deleted any time. Capture and delete advance `semantic_revision`; relabel does not. | Brief A3 |
| R11 | Set key: `^[A-Za-z][A-Za-z0-9_]{0,127}$`, unique. | Brief A3 |
| R12 | Release activation re-checks every enabled report. | Schema doc §15–16 |

## Frontend contract

Adopted with these deltas:

| Item | Change | Issue |
|---|---|---|
| `ProblemDetails`, `ApiResult`, `ReleaseViolation`, `CheckTarget` | Kernel types. `ReleaseViolation` becomes `Violation` with `rule` → `code`. `CheckTarget` maps onto `ViolationTarget`: dataset → `{ area: 'dataset', itemId: datasetId }`; identity → `{ area: kind, itemId: identityId }`; report → `{ area: 'report', itemId: reportId, itemKey: sourceKey }`. `ReleaseViolation extends Violation { code: ReleaseRuleCode; datasetId: Id \| null; target?: ReleaseViolationTarget }` keeps the manifest row for inline issue icons. | |
| `getCatalog`, `Catalog` | Replaced by `TenantApi.getCatalog`; `Identity` and `ReportRef` become `PinIdentity` and `ReleaseReportRef`, projections served by `listIdentities()` and `listReportReads()`; the manifest editor's datasets come from `listManifestDatasets()` | OI-13 |
| `ReleaseReportRef.sources[].usesFactors`, `reads` | Computed by the server from report sources and bridges | |
| `checkRelease(id, manifest?)` | Becomes the release change-set preview | OI-37 |
| `saveManifest` | Becomes the release's single edit operation | OI-01 |
| `diffReleases`, `compareSets`, `capturePreview`, `getSet().readiness`, `SetListItem.livePins`, `changedSinceCapture` | New server read models | OI-38 |
| `TenantInfo` | `TenantInfo` from [10](10-tenant-session-catalog.md) (`tenant.yaml`); also the answer of `activateRelease` | |
| `ReleasesApi` | Extends `ChangeSetPort` (manifest) | OI-01 |
| Release activation date and history | Stored in `qe_release_activation` (SG-09); `ReleaseListItem` gains `activatedAt` and `activatedBy` for the active release, and `ReleaseDetail` gains `activations[]` | OI-40 |
| `Release.contentHash` | New: computed on publish, shown on the release page | OI-41 |

### Port (Agreed)

```ts
export interface ReleasesApi extends ChangeSetPort {
  // reads
  listReleases(): Promise<ReleaseListItem[]>
  getRelease(id: Id): Promise<ApiResult<ReleaseDetail>>
  diffReleases(fromId: Id, toId: Id): Promise<ReleaseDiff>
  checkRelease(id: Id): Promise<ReleaseCheckReport>                  // saved manifest; pending manifest via preview
  listManifestDatasets(): Promise<ManifestDataset[]>                 // datasets with versions, bindings, measures
  listIdentities(): Promise<PinIdentity[]>                           // dimensions, mapping sets, bridges with versions
  listReportReads(): Promise<ReleaseReportRef[]>
  listSets(): Promise<AssumptionSetList>                             // { active, historical }
  getSet(id: Id): Promise<ApiResult<SetDetail>>
  compareSets(fromSetId: Id, toSetId: Id): Promise<SetComparison>
  capturePreview(): Promise<SetCapturePreview>

  // edit (staged per draft release)
  saveManifest(id: Id, manifest: ManifestEntry[]): Promise<ApiResult<ReleaseDetail>>

  // commands
  createRelease(input: CreateReleaseInput): Promise<ApiResult<Release>>
  validateRelease(id: Id): Promise<ApiResult<ReleaseTransitionResult>>  // { release, report }
  publishRelease(id: Id): Promise<ApiResult<ReleaseTransitionResult>>
  backToDraft(id: Id): Promise<ApiResult<Release>>
  retireRelease(id: Id): Promise<ApiResult<Release>>
  activateRelease(id: Id): Promise<ApiResult<TenantInfo>>
  deleteRelease(id: Id): Promise<ApiResult<null>>
  captureSet(input: CaptureSetInput): Promise<ApiResult<AssumptionSet>>
  relabelSet(id: Id, input: RelabelSetInput): Promise<ApiResult<AssumptionSet>>
  deleteSet(id: Id): Promise<ApiResult<null>>
}
```

`ReleaseCheckReport.unsaved` stays: the preview response sets it when the report ran over pending operations.

`activateRelease` answers the kernel `TenantInfo` ([10](10-tenant-session-catalog.md#tenant-overview-and-catalog-libtenantcontractts)):
the new `activeReleaseId` and the advanced `semanticRevision`, so the active-release chip updates from the command's
result. The activation log row appears in `ReleaseDetail.activations`.

Component names carry a `Release`, `Pin` or `Set` prefix because contract names are global (`ReleaseCheckReport`,
`ReleaseReportRef`, `PinIdentity`, `SetReadiness`, `SetCapturePreview`).

`relabelSet` is a command, not a staged edit: the set page has a small sheet with its own Save, and relabelling does
not touch anything else.

## HTTP API

Prefix `/api/tenants/{t}`. Open to any tenant user, including activation, capture and set delete (OI-09).

| Method | Route | Request | Response | Errors | Port |
|---|---|---|---|---|---|
| GET | `releases` | | `ReleaseListItem[]` (with `isActive`, `datasetCount`, `diffVsActive`) | | `listReleases` |
| POST | `releases` | `CreateReleaseInput` | `201 Release` | 400, 409 key taken | `createRelease` |
| GET | `releases/{id}` | | `ReleaseDetail` (with `activations[]`) | 404 | `getRelease` |
| GET | `releases/diff?from=&to=` | | `ReleaseDiff` (with `readBy` from enabled reports) | 404 | `diffReleases` |
| POST | `releases/{id}/changes/preview` | `ChangeSetRequest<ReleaseOperation>` | `ChangeSetPreview<ReleaseDetail> & { report: ReleaseCheckReport; diffVsActive: ReleaseDiff }` | 409 not draft | `saveManifest` (staged), `checkRelease` over pending manifest |
| POST | `releases/{id}/changes` | `ChangeSetRequest<ReleaseOperation>` | `ChangeSetCommit<ReleaseDetail>` | 409 not draft, 422 pinned version not owned or not published | `commitChanges` |
| POST | `releases/{id}/check` | | `ReleaseCheckReport` | | `checkRelease` |
| POST | `releases/{id}/validate` | | `ReleaseTransitionResult` | 409, 422 | `validateRelease` |
| POST | `releases/{id}/publish` | | `ReleaseTransitionResult` (sets `contentHash`) | 409, 422 | `publishRelease` |
| POST | `releases/{id}/reopen` | | `Release` | 409 | `backToDraft` |
| POST | `releases/{id}/retire` | | `Release` | 409, 422 `release_not_active` | `retireRelease` |
| POST | `releases/{id}/activate` | | `TenantInfo` | 409 not published, 422 enabled report rules | `activateRelease` |
| DELETE | `releases/{id}` | | `204` | 409 not draft | `deleteRelease` |
| GET | `manifest-datasets` | | `ManifestDataset[]` | | `listManifestDatasets` |
| GET | `identities` | | `PinIdentity[]` | | `listIdentities` |
| GET | `report-reads` | | `ReleaseReportRef[]` | | `listReportReads` |
| GET | `assumption-sets` | | `AssumptionSetList` | | `listSets` |
| GET | `assumption-sets/capture-preview` | | `SetCapturePreview` | | `capturePreview` |
| POST | `assumption-sets` | `CaptureSetInput` | `201 AssumptionSet` | 400, 409 key taken | `captureSet` |
| GET | `assumption-sets/{id}` | | `SetDetail` (pins with state, readiness) | 404 | `getSet` |
| GET | `assumption-sets/compare?from=&to=` | | `SetComparison` | 404 | `compareSets` |
| PUT | `assumption-sets/{id}` | `RelabelSetInput` | `AssumptionSet` | 409 active set | `relabelSet` |
| DELETE | `assumption-sets/{id}` | | `204` | 409 active set | `deleteSet` |

## Change-set operations

```ts
type ReleaseOperation = { op: 'saveManifest'; manifest: ManifestEntry[] }
```

The release editor keeps the whole manifest as its pending state, so a single replace operation is enough; undo steps
are local snapshots of the manifest.

## Rule codes

Release (`semantic_release`): `manifest_not_empty`, `pinned_version_belongs_to_dataset`, `pinned_version_published`,
`bound_dimensions_resolvable`, `mapped_bindings_resolvable`, `bridge_bindings_resolvable`, `report_sources_resolve`,
`report_factors_apply`. Retirement: `release_not_active`. Activation runs the report rules of every enabled report
([16](16-reports.md#rule-codes)).

## Derived read models (Agreed, OI-38)

| Read model | Computation |
|---|---|
| `ReleaseDiff` | Manifest set difference; `removed[].readBy` = enabled reports with a source on that dataset |
| `PinRow.state` | `live` if the pinned version is live (published); `frozen` if frozen (retired); `report_owned` if the identity belongs to a report. Living kinds have no drafts (OI-14), so `unpublished` is removed |
| `SetListItem.livePins`, `changedSinceCapture` | Count of pins whose version is still published; count whose identity's published version differs |
| `SetComparison` | Pin set difference (report-owned excluded) plus `ReleaseDiff` of the two releases |
| `SetCapturePreview` | Counts of active analytical pins and of left-out report-owned pins; the active release |
| `SetReadiness` | Per enabled report: every analytical identity it reads is pinned and every source dataset is in the set's release |

## Schema mapping

| Contract | Table |
|---|---|
| `Release` | `qe_semantic_release` (`release_key`, `status`, `created_at`, `published_at`, `content_hash`) |
| activation history | `qe_release_activation` (new, [SG-09](91-schema-gaps.md#sg-09-release-activation-log)) |
| `ManifestEntry` | `qe_release_dataset` |
| active release, `TenantInfo` | `qe_database_info` |
| `AssumptionSet` | `qe_assumption_set` (`semantic_release_id` → `releaseId`) |
| `PinRow`, `SetPinEntry` | `qe_assumption_set_dimension`, `_mapping`, `_bridge` |

## Deltas from the current API

| Change | Item |
|---|---|
| Keep | Create with copy; activation endpoint; capture; relabel; delete rules |
| Change | `PUT releases/{id}/datasets` becomes the manifest change set |
| Change | `PUT versions/semantic_release/{id}/status` becomes `validate`, `publish`, `reopen`, `retire` |
| Change | `GET assumption-sets` returns `{ active, historical }` with derived counts |
| New | `diff`, `check`, preview over unsaved manifest, `manifest-datasets`, `identities`, `report-reads`, `capture-preview`, `compare`, readiness, pin state |

## Decisions applied

[OI-01](90-open-issues.md#oi-01), [OI-09](90-open-issues.md#oi-09), [OI-13](90-open-issues.md#oi-13), [OI-14](90-open-issues.md#oi-14),
[OI-37](90-open-issues.md#oi-37), [OI-38](90-open-issues.md#oi-38), [OI-39](90-open-issues.md#oi-39),
[OI-40](90-open-issues.md#oi-40), [OI-41](90-open-issues.md#oi-41).
