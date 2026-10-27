# 13 Mapping sets and bridges

## Purpose and sources

Mapping sets translate a source system's key values into members of one dimension. Bridges translate members of one
dimension into members of another, with cardinality, allocation, factors and (for report bridges) descendant rules.
Both are managed inside the dimension editor (`web2` rev. 2) and follow the same live-and-frozen version model as
dimensions.

Sources: [`web2/src/lib/mappings/contract.ts`](../../web2/src/lib/mappings/contract.ts), `rules.ts`, `mock/adapter.ts`;
[mappings and bridges brief rev. 1](../requirements/mappings-and-bridges-prototype.md) and
[rev. 2](../../web2/docs/mappings-and-bridges.md); current `MappingSetsController`, `MappingVersionsController`,
`MappingEntriesController`, `BridgesController`, `BridgeVersionsController`, `BridgeMappingsController`; schema
`mapping/`, `bridge/`.

## Domain rules

| # | Rule | Source |
|---|---|---|
| B1 | A mapping set targets exactly one analytical categorical or hierarchical dimension; never a report dimension. | Brief A2; schema README 2.19.0 |
| B2 | A source value appears at most once per version, compared exactly as text (never trimmed or coerced). | Brief A2; schema UQ |
| B3 | An entry's member is an assignable member of the dimension's published version, named by key. | Brief A2 |
| B4 | A bridge's endpoints are fixed at creation, differ, and the source is not `time`; a report dimension is never a bridge's source. | Brief A3; schema doc §18 rule 23 |
| B5 | `many_to_one` allows `none` only; `many_to_many` allows `partition` or `replicate`. `none`: one target, weight 1. `partition`: weights ≥ 0 summing to 1 per source. `replicate`: weights ≥ 0. | Brief A3 |
| B6 | Each (source, target) pair appears once per version. | Schema UQ |
| B7 | A factor affects only measures with `bridge_factor_behavior = value`. | Brief A3 |
| B8 | `descendants` source selection is used only by report bridges; analytical bridges map each member alone. | Schema README 2.20.0; OI-28 |
| B9 | A report bridge belongs to exactly one report source, is `many_to_many` + `replicate`, has one live version, and is edited only through its report. | Schema README 2.19.0, 2.26.0 |
| B10 | Live-and-frozen: exactly one live version (stored `published`) edited in place; `freeze` makes it a frozen snapshot and continues in a new live clone; creating a mapping set or bridge creates an empty live v1 (`many_to_one` / `none` for bridges). | OI-14, OI-27 |
| B11 | Cardinality and allocation are edited in place on the live version like any other edit; mappings that no longer comply are flagged by the rules and refuse the commit while violations exist. | OI-26 |
| B12 | A mapping set or bridge is deleted only when no dataset binding uses it and no historical assumption set pins one of its versions; a frozen version is deleted when no historical set pins it. Report bridges are deleted with their report source. | Brief A5, B7; OI-17 |
| B13 | Bindings name mapping sets and bridges by identity, never by version. | Brief A5 |
| B14 | Keys match `^[A-Za-z][A-Za-z0-9_.]*$` (dots allowed, as in the seed); `sourceNamespace` is free text. | OI-29 |
| B15 | Analytical bridges are edited from either endpoint's Bridges section (Outgoing and Incoming). | Map rev2; OI-25 |

The lifecycle and the `freeze` command are defined once in
[12 Dimensions, "The version model"](12-dimensions.md#the-version-model-agreed-oi-14).

**On B11:** editing settings in place changes how live queries allocate at once. The confirmation `web2` shows ("N
mappings won't satisfy ...") stays; because the version is live, a commit that leaves non-compliant mappings is refused
(422) rather than kept as a flagged draft. Freeze first if the old semantics must remain available to historical sets.

## Frontend contract

Adopted with these deltas:

| Item | Change | Issue |
|---|---|---|
| `MappingsApi` | Extends `ChangeSetPort`; edits are staged per mapping set or analytical bridge (identity plus live version) | OI-01 |
| `createMappingVersion`, `validate*Version`, `publish*Version`, `reopen*Version`, `createBridgeVersion`, `BridgeVersionInput` | Removed; replaced by `freezeMappingVersion` and `freezeBridgeVersion` | OI-14, OI-30 |
| `isEditableStatus`, `defaultVersionOf` | Editable = live (`published`); default = the live version | OI-14 |
| `createMappingSet`, `createBridge` | Return `{ ...Id, versionId }` of the live v1 | OI-27 |
| `ApiResult`, `ProblemDetails`, `DeleteCheck`, `Violation`, `ValidationReport` | Kernel types; `Violation.itemId`/`filter` move into `target` | OI-06 |
| `listDimensionRefs`, `getPublishedMembers` | Move to `TenantApi` | OI-13 |
| `BridgeOwner` | `{ reportId, reportKey, sourceKey }` | OI-31 |
| `findReportBridge(reportKey, sourceKey)` | `findReportBridge(reportId, sourceKey)` | OI-46, OI-47 |
| `MappingVersion.createdAt`, `retiredAt`; `BridgeVersion.createdAt`, `retiredAt`; `freezeNote` | Stored (SG-04, SG-05) | OI-12, OI-15 |
| `BridgeMappingInput.sourceSelection` | Only accepted on report bridges (written by the report editor) | OI-28 |
| Field names (`key`, `displayName`, `sourceKeyValue`, `memberKey`) | Already follow the naming rule | OI-10 |

### Port (Agreed)

```ts
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
  findReportBridge(reportId: Id, sourceKey: string): Promise<BridgeListItem | null>
  getBridgeVersion(versionId: Id): Promise<ApiResult<BridgeVersionContent>>
  checkBridgeVersion(versionId: Id): Promise<ApiResult<ValidationReport>>
  checkDeleteBridge(id: Id): Promise<DeleteCheck>
  checkDeleteBridgeVersion(versionId: Id): Promise<DeleteCheck>

  // edits: a mapping set's identity and its live version
  updateMappingSet(id: Id, patch: Partial<Pick<MappingSet, 'displayName' | 'sourceNamespace'>>): Promise<ApiResult<MappingSet>>
  saveEntry(versionId: Id, input: EntryInput): Promise<ApiResult<MappingEntry>>
  deleteEntry(versionId: Id, entryId: Id): Promise<ApiResult<null>>
  replaceEntries(versionId: Id, entries: EntryInput[]): Promise<ApiResult<MappingEntry[]>>

  // edits: an analytical bridge's identity and its live version
  updateBridge(id: Id, patch: Partial<Pick<Bridge, 'displayName' | 'isEnabled'>>): Promise<ApiResult<Bridge>>
  updateBridgeVersion(versionId: Id, patch: Pick<BridgeVersion, 'cardinality' | 'allocationMode'>): Promise<ApiResult<BridgeVersion>>
  saveBridgeMapping(versionId: Id, input: BridgeMappingInput): Promise<ApiResult<BridgeMapping>>
  deleteBridgeMapping(versionId: Id, mappingId: Id): Promise<ApiResult<null>>
  replaceBridgeMappings(versionId: Id, rows: BridgeMappingInput[]): Promise<ApiResult<BridgeMapping[]>>

  // commands
  createMappingSet(input: CreateMappingSetInput): Promise<ApiResult<{ mappingSetId: Id; versionId: Id }>>
  deleteMappingSet(id: Id): Promise<ApiResult<null>>
  freezeMappingVersion(versionId: Id, note: string | null): Promise<ApiResult<FreezeResult<MappingVersionSummary>>>
  deleteMappingVersion(versionId: Id): Promise<ApiResult<null>>      // frozen versions only
  createBridge(input: CreateBridgeInput): Promise<ApiResult<{ bridgeId: Id; versionId: Id }>>
  deleteBridge(id: Id): Promise<ApiResult<null>>
  freezeBridgeVersion(versionId: Id, note: string | null): Promise<ApiResult<FreezeResult<BridgeVersionSummary>>>
  deleteBridgeVersion(versionId: Id): Promise<ApiResult<null>>       // frozen versions only
}

/** Shared with dimensions: the rule report, and the new live version when the rules passed. */
export interface FreezeResult<V> { report: ValidationReport; live: V | null }
```

**Report bridges** are read through this port (Bridges sections, Usage, `ReportBridgeLine`) but every write to them
goes through the report's change set ([16](16-reports.md#sources-and-report-bridges)). The server refuses a mappings
change set or freeze that targets a report bridge with `409 Owned by a report`.

## HTTP API

Prefix `/api/tenants/{t}`. Open to any tenant user (OI-09).

| Method | Route | Request | Response | Errors | Port |
|---|---|---|---|---|---|
| GET | `mapping-sets?dimensionId=` | | `MappingSetListItem[]` | | `listMappingSets` |
| POST | `mapping-sets` | `CreateMappingSetInput` | `201 { mappingSetId, versionId }` (empty live v1) | 400, 409 key taken / report dimension | `createMappingSet` |
| GET | `mapping-sets/{id}` | | `MappingSetDetail` (bindings, pins) | 404 | `getMappingSet` |
| GET | `mapping-sets/{id}/delete-check` | | `DeleteCheck` | | `checkDeleteMappingSet` |
| DELETE | `mapping-sets/{id}` | | `204` | 409 blocked | `deleteMappingSet` |
| POST | `mapping-sets/{id}/changes/preview` | `ChangeSetRequest<MappingOperation>` | `ChangeSetPreview<MappingSetEditState>` | 400, 409, 422 | edits |
| POST | `mapping-sets/{id}/changes` | `ChangeSetRequest<MappingOperation>` | `ChangeSetCommit<MappingSetEditState>` | 400, 409, 422 | `commitChanges` |
| GET | `mapping-versions/{id}` | | `MappingVersionContent` | 404 | `getMappingVersion` |
| POST | `mapping-versions/{id}/check` | | `ValidationReport` | | `checkMappingVersion` |
| POST | `mapping-versions/{id}/freeze` | `{ note }` | `FreezeResult` | 409 not live, 422 | `freezeMappingVersion` |
| GET | `mapping-versions/{id}/delete-check` | | `DeleteCheck` | | `checkDeleteMappingVersion` |
| DELETE | `mapping-versions/{id}` | | `204` | 409 live / pinned | `deleteMappingVersion` |
| GET | `bridges?dimensionId=` | | `BridgeListItem[]` (both directions, analytical and report) | | `listBridges` |
| GET | `bridges?reportId=&sourceKey=` | | `BridgeListItem[]` (0 or 1) | | `findReportBridge` |
| POST | `bridges` | `CreateBridgeInput` | `201 { bridgeId, versionId }` (empty live v1, `many_to_one` / `none`) | 400, 409 | `createBridge` |
| GET | `bridges/{id}` | | `BridgeDetail` | 404 | `getBridge` |
| GET | `bridges/{id}/delete-check` | | `DeleteCheck` | | `checkDeleteBridge` |
| DELETE | `bridges/{id}` | | `204` | 409 blocked / report-owned | `deleteBridge` |
| POST | `bridges/{id}/changes/preview` | `ChangeSetRequest<BridgeOperation>` | `ChangeSetPreview<BridgeEditState>` | 400, 409 report-owned, 422 | edits |
| POST | `bridges/{id}/changes` | `ChangeSetRequest<BridgeOperation>` | `ChangeSetCommit<BridgeEditState>` | 400, 409 report-owned, 422 | `commitChanges` |
| GET | `bridge-versions/{id}` | | `BridgeVersionContent` | 404 | `getBridgeVersion` |
| POST | `bridge-versions/{id}/check` | | `ValidationReport` | | `checkBridgeVersion` |
| POST | `bridge-versions/{id}/freeze` | `{ note }` | `FreezeResult` | 409 not live / report-owned, 422 | `freezeBridgeVersion` |
| GET | `bridge-versions/{id}/delete-check` | | `DeleteCheck` | | `checkDeleteBridgeVersion` |
| DELETE | `bridge-versions/{id}` | | `204` | 409 live / pinned | `deleteBridgeVersion` |

`MappingSetEditState` = `{ set: MappingSetDetail; content: MappingVersionContent }`; `BridgeEditState` =
`{ bridge: BridgeDetail; content: BridgeVersionContent }`. As with dimensions, the change set targets the identity and
covers its live version.

## Change-set operations

```ts
type MappingOperation =
  | { op: 'updateMappingSet'; patch: { displayName?: string; sourceNamespace?: string } }
  | { op: 'saveEntry'; versionId: Id; input: EntryInput }
  | { op: 'deleteEntry'; versionId: Id; entryId: Id }
  | { op: 'replaceEntries'; versionId: Id; entries: EntryInput[] }

type BridgeOperation =
  | { op: 'updateBridge'; patch: { displayName?: string; isEnabled?: boolean } }
  | { op: 'updateBridgeVersion'; versionId: Id; patch: { cardinality: Cardinality; allocationMode: AllocationMode } }
  | { op: 'saveBridgeMapping'; versionId: Id; input: BridgeMappingInput }
  | { op: 'deleteBridgeMapping'; versionId: Id; mappingId: Id }
  | { op: 'replaceBridgeMappings'; versionId: Id; rows: BridgeMappingInput[] }
```

Preview reports non-compliant mappings as `violations` so the editor can flag them while the user fixes them; commit
refuses while any remain (B11).

## Rule codes

Mapping versions: `source_key_unique`, `dimension_version_published`, `entry_member_in_version`,
`entry_member_assignable`. Bridge versions: `endpoint_versions_published`, `mapping_members_in_endpoint_versions`,
`cardinality_many_to_one`, `allocation_none`, `allocation_partition`, `allocation_replicate`, `pair_unique`,
`descendants_report_only` (B8).

`web2`'s `lib/mappings/rules.ts` decided entry and mapping validity in the browser. With the server authoritative
(OI-05), issue badges, uncovered-member counts and the allocation summary come from preview `violations` and from
read-model fields the server computes (`coverage: { uncoveredMemberKeys }`, `allocation: AllocationSummary`). Paste
parsing and the paste preview's New/Unchanged/Changed/Removed grouping stay in the browser.

## Schema mapping

| Contract | Table |
|---|---|
| `MappingSet` | `qe_mapping_set` |
| `MappingVersion` | `qe_mapping_version` (`published` = live, `retired` = frozen; `created_at`, `retired_at`, `freeze_note` new) |
| `MappingEntry` | `qe_dimension_mapping` |
| `Bridge` | `qe_bridge`; `owner` from `qe_report_source.bridge_id` → `qe_report` |
| `BridgeVersion` | `qe_bridge_version` (same status mapping and new columns) |
| `BridgeMapping` | `qe_bridge_mapping` |
| `BindingUsage` | `qe_dimension_binding` (`mapping_set_id`, `bridge_id`) joined to dataset versions |
| `AssumptionPin` | `qe_assumption_set_mapping`, `qe_assumption_set_bridge` |

## Deltas from the current API

| Change | Item |
|---|---|
| Keep | Replace-all semantics (now `replaceEntries` / `replaceBridgeMappings` operations); report-owned refusal |
| Change | Per-entry and replace-all `PUT`s become change-set operations on the mapping set or bridge |
| Change | `POST mapping-sets/{id}/versions`, `POST bridges/{id}/versions` and the status endpoint become `freeze` |
| Change | Validation becomes `check` |
| Change | `POST mapping-sets` and `POST bridges` create an empty live v1 (today: no version) |
| Change | `GET bridges?dimensionId` returns both directions with `source`, `target`, `versions`, `bindings` (list item) |
| New | `delete-check` per identity and version, `bridges?reportId&sourceKey`, coverage and allocation summaries, pins and bindings in detail read models |
| Remove | Drafts, validated versions and manual publish for mapping and bridge versions |

## Decisions applied

[OI-01](90-open-issues.md#oi-01), [OI-05](90-open-issues.md#oi-05), [OI-06](90-open-issues.md#oi-06),
[OI-09](90-open-issues.md#oi-09), [OI-10](90-open-issues.md#oi-10), [OI-12](90-open-issues.md#oi-12),
[OI-13](90-open-issues.md#oi-13), [OI-14](90-open-issues.md#oi-14), [OI-15](90-open-issues.md#oi-15),
[OI-17](90-open-issues.md#oi-17), [OI-25](90-open-issues.md#oi-25), [OI-26](90-open-issues.md#oi-26),
[OI-27](90-open-issues.md#oi-27), [OI-28](90-open-issues.md#oi-28), [OI-29](90-open-issues.md#oi-29),
[OI-30](90-open-issues.md#oi-30), [OI-31](90-open-issues.md#oi-31), [OI-46](90-open-issues.md#oi-46),
[OI-47](90-open-issues.md#oi-47).
