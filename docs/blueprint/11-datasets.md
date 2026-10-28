# 11 Datasets

## Purpose and sources

The dataset manager describes a tenant table or view so the engine can read it: fields, grains, dimension bindings
(with time bindings) and measures (with components and time policies), versioned strictly and released through
semantic releases.

Sources: [`web2/src/lib/datasets/contract.ts`](../../web2/src/lib/datasets/contract.ts) and its mock
(`mock/adapter.ts`, `mock/rules.ts`); current `DatasetsController`, `DatasetVersionsController`, `FieldsController`,
`GrainsController`, `BindingsController`, `MeasuresController`, `SourceObjectsController`; schema `dataset/`,
`binding/`, `measure/`; schema doc §18 rules 1–3, 17, 22, 26–35.

## Domain rules

| # | Rule | Source |
|---|---|---|
| D1 | Dataset versions are strictly versioned: draft, validated, published, retired; validated can return to draft. Published versions never change. Several may be published at once. | Agreed; schema README 2.17.0 item 1; API `VersionTransitions` |
| D2 | A dataset version reaches queries only when the active release pins it. | Agreed |
| D3 | Content writes require a draft. | Agreed; API `[RequireDraft]` |
| D4 | `dataset_key`, `field_key`, `grain_key`, `measure_key` are fixed after creation and unique in scope. | Agreed (OI-36 for `datasetKey`) |
| D5 | `field_source_kind`: `column` needs `source_column`; `json_path` needs `source_column` and `json_path`; `expression` needs `expression_text`. | Schema doc |
| D6 | A version binds each role at most once, and a bound role's key names no other field of the version. | Schema doc §18 rule 22 |
| D7 | Measure component roles fit the measure kind; measure and expression dependency graphs are acyclic. | §18 rules 28–29 |
| D8 | A time policy's selection grain is a `series` grain of the same version. | §18 rule 30 |
| D9 | A time binding allows a periodicity finer than its native one only when its interval measures use `uniform_estimate`. | §18 rule 31 |
| D10 | A dataset can be deleted only when every version is a draft or validated and no release lists it; a version only while draft or validated and unpinned. | API delete rules; OI-17 |
| D14 | Deleting a dataset drops each registered managed table its versions read that no version of another dataset reads, with the table's materialized view and registry row ([below](#managed-tables-on-dataset-delete-agreed-oi-33)). | OI-33, decided 2026-10-27 |
| D12 | A dataset has at most one open (draft or validated) version at a time. | OI-23 |
| D11 | The UI never hard-codes an enumeration; vocabularies come from the server. | `datasets/contract.ts` header |
| D13 | The version the active release pins cannot be retired (`release_pins_retired_version`); activate a release that pins another version first. | Agreed (follows from D2); API `ReleasePinsRetiredVersionRule` |

## Frontend contract

`datasets/contract.ts` is adopted almost unchanged. Deltas:

| Item | Change | Why |
|---|---|---|
| `ApiResult`, `ValidationProblemDetails`, `PublicationViolation`, `ValidationReport` | Replaced by kernel `ApiResult`, `Problem`, and `DatasetViolation` / `DatasetValidationReport`, which narrow the kernel `Violation` and `ValidationReport` to `target.area` = `DatasetTab` | [02](02-frontend-contract-layer.md#shared-kernel-libapicontractts-agreed) |
| `ValidationProblemDetails.dependents` | Becomes `Problem.violations` with `target` for refused deletes | One error shape |
| `getTenantCatalog`, `TenantCatalog`, `Dimension.publishedMembers` | Moved to `TenantApi` ([10](10-tenant-session-catalog.md)) | OI-13 |
| `DimensionKind = 'standard' \| 'range' \| 'time'` | Removed; use the schema's `categorical \| hierarchical \| range \| time` | OI-11 |
| `Release.displayName`, `Release.pins` | `displayName` dropped (no column, OI-39); `pins` comes from the release module | |
| Field names | `datasetKey`, `fieldKey`, `grainKey`, `measureKey` become `key`; references stay `<entity>Id` | OI-10 |
| `createDataset` | Moves from the edit list to the command list | It creates an aggregate; it cannot be staged inside another one's change set |
| `updateDataset` patch | Drops `datasetKey` | D4; OI-36 |
| `DatasetFieldMaterialization` | Kept: materialized view per source object, async refresh ([below](#json-field-materialization-agreed-oi-32)) | OI-32 |
| `DatasetSourceObject.isManaged`, `createColumn`, `DatasetNewColumnInput`, `renameSourceTable`, `DatasetCreateInput.mode = 'new'` | Kept; backed by the managed-object registry (SG-08); DDL only on registered objects; rename only while no published version reads the table | OI-33 |
| `DatasetSourceObject.stats`, `DatasetListItem.storage` | Kept; read live from `information_schema` with a short cache | OI-34 |
| `DatasetVersion.dependents` | Computed by the server from the delete-blocker graph | OI-35 |
| `RoundingMode` | Add `truncate` (schema allows it); shared kernel type with models | Schema `qe_measure.rounding_mode` |
| `MeasureKind`, `ComponentRole`, `BindingKind`, `Periodicity` | Shared kernel types (`common.yaml`) instead of `Dataset*` copies | Read by tenant, releases, dimensions and reports too |
| `DatasetBinding.raggedBehavior`, `unmappedBehavior` | Required, with a `null` term, instead of nullable | The schema stores the string `'null'` (NOT NULL, default `'null'`); values are the schema's ([01](01-api-conventions.md#ids-keys-and-names)) |
| `Dataset.key` | `DottedKey` | The seed writes dotted dataset keys (OI-29) |

### Port (Agreed)

```ts
export interface DatasetsApi extends ChangeSetPort {
  // reads
  listSourceObjects(): Promise<DatasetSourceObject[]>
  getSourceColumns(kind: DatasetSourceObjectKind, name: string): Promise<ApiResult<DatasetSourceColumn[]>>
  listDatasets(): Promise<DatasetListItem[]>
  getDataset(datasetId: Id): Promise<ApiResult<DatasetListItem>>
  getVersion(versionId: Id): Promise<ApiResult<DatasetVersion>>
  checkVersion(versionId: Id): Promise<ApiResult<DatasetValidationReport>>   // new: read-only rules run

  // edits (staged per version; see "Change-set operations")
  updateDataset(datasetId: Id, patch: DatasetPatch): Promise<ApiResult<Dataset>>
  saveSettings(versionId: Id, settings: DatasetVersionSettings): Promise<ApiResult<DatasetSettingsSaveResult>>
  saveField(versionId: Id, input: DatasetFieldInput): Promise<ApiResult<DatasetField>>
  deleteField(versionId: Id, fieldId: Id): Promise<ApiResult<null>>
  addFieldsFromColumns(versionId: Id, columns: string[]): Promise<ApiResult<DatasetField[]>>
  saveGrain(versionId: Id, input: DatasetGrainInput): Promise<ApiResult<DatasetGrain>>
  deleteGrain(versionId: Id, grainId: Id): Promise<ApiResult<null>>
  saveBinding(versionId: Id, input: DatasetBindingInput): Promise<ApiResult<DatasetBinding>>
  deleteBinding(versionId: Id, bindingId: Id): Promise<ApiResult<null>>
  saveMeasure(versionId: Id, input: DatasetMeasureInput): Promise<ApiResult<DatasetMeasure>>
  deleteMeasure(versionId: Id, measureId: Id): Promise<ApiResult<null>>
  renameSourceTable(versionId: Id, tableName: string): Promise<ApiResult<DatasetVersionSettings>>   // OI-33

  // commands
  createDataset(input: DatasetCreateInput): Promise<ApiResult<DatasetListItem>>
  deleteDataset(datasetId: Id): Promise<ApiResult<null>>
  checkDeleteDataset(datasetId: Id): Promise<DeleteCheck>                     // new
  checkDeleteVersion(versionId: Id): Promise<DeleteCheck>                     // new
  createDraft(datasetId: Id, input: DatasetCreateDraftInput): Promise<ApiResult<DatasetVersionSummary>>
  deleteDraft(versionId: Id): Promise<ApiResult<null>>
  validateVersion(versionId: Id): Promise<ApiResult<DatasetValidationReport>>
  publishVersion(versionId: Id): Promise<ApiResult<DatasetValidationReport>>
  reopenVersion(versionId: Id): Promise<ApiResult<DatasetVersionSummary>>
  retireVersion(versionId: Id): Promise<ApiResult<DatasetVersionSummary>>
  materializeField(versionId: Id, fieldId: Id): Promise<ApiResult<DatasetField>>        // OI-32
  refreshMaterialization(versionId: Id, fieldId: Id): Promise<ApiResult<DatasetField>>  // OI-32
  dematerializeField(versionId: Id, fieldId: Id): Promise<ApiResult<DatasetField>>      // OI-32
}
```

`getVocabularies` moves to `TenantApi`. The screens read the tenant lookups through `TenantLookups`
(`lib/datasets/lookups.ts`), a `Pick` of `TenantApi`, `TenantCatalog` and `Vocabularies`, so a `TenantApi` serves them
unchanged.

**Dataset identity edits.** `updateDataset` edits `qe_dataset`, which is shared by every version. It is staged in the
open version's change set (as today) and committed with it; on a version that is not a draft only `updateDataset`
operations are accepted.

## HTTP API

Prefix `/api/tenants/{t}`. Open to any tenant user (OI-09).

| Method | Route | Request | Response | Errors | Port |
|---|---|---|---|---|---|
| GET | `source-objects` | | `DatasetSourceObject[]` | | `listSourceObjects` |
| GET | `source-objects/{kind}/{name}/columns` | | `DatasetSourceColumn[]` | 404 | `getSourceColumns` |
| GET | `datasets` | | `DatasetListItem[]` | | `listDatasets` |
| POST | `datasets` | `DatasetCreateInput` | `201 DatasetListItem` (creates draft v1) | 400, 409 key taken | `createDataset` |
| GET | `datasets/{id}` | | `DatasetListItem` | 404 | `getDataset` |
| GET | `datasets/{id}/delete-check` | | `DeleteCheck` | 404 | `checkDeleteDataset` |
| DELETE | `datasets/{id}` | | `204` (drops managed tables only it reads, D14) | 404, 409 blocked (`has_published_versions`, `referenced_by_release`) | `deleteDataset` |
| POST | `datasets/{id}/versions` | `DatasetCreateDraftInput` | `201 DatasetVersionSummary` | 400, 409 an open version exists (OI-23) | `createDraft` |
| GET | `dataset-versions/{id}` | | `DatasetVersion` (settings, fields, grains, bindings, measures, dependents) | 404 | `getVersion` |
| GET | `dataset-versions/{id}/delete-check` | | `DeleteCheck` | 404 | `checkDeleteVersion` |
| DELETE | `dataset-versions/{id}` | | `204` | 409 blocked (`published_version`, `referenced_by_release`) | `deleteDraft` |
| POST | `dataset-versions/{id}/changes/preview` | `ChangeSetRequest<DatasetOperation>` | `ChangeSetPreview<DatasetVersion>` | 400, 409, 422 | edits |
| POST | `dataset-versions/{id}/changes` | `ChangeSetRequest<DatasetOperation>` | `ChangeSetCommit<DatasetVersion>` | 400, 409 not draft, 422 | `commitChanges` |
| POST | `dataset-versions/{id}/check` | | `DatasetValidationReport` | | `checkVersion` |
| POST | `dataset-versions/{id}/validate` | | `DatasetVersionSummary` | 409 transition, 422 | `validateVersion` |
| POST | `dataset-versions/{id}/publish` | | `DatasetVersionSummary` | 409, 422 | `publishVersion` |
| POST | `dataset-versions/{id}/reopen` | | `DatasetVersionSummary` | 409 | `reopenVersion` |
| POST | `dataset-versions/{id}/retire` | | `DatasetVersionSummary` | 409, 422 `release_pins_retired_version` (D13) | `retireVersion` |
| POST | `dataset-versions/{id}/fields/{fieldId}/materialization` | | `DatasetField` (`state: building`) | 409 cannot be materialized | `materializeField` |
| POST | `dataset-versions/{id}/fields/{fieldId}/materialization/refresh` | | `DatasetField` | 409 | `refreshMaterialization` |
| DELETE | `dataset-versions/{id}/fields/{fieldId}/materialization` | | `DatasetField` | 409 | `dematerializeField` |

`publishVersion` validates first when called on a draft (`web2` offers Publish from draft); a failed run answers 422
and the adapter returns the report as data.

## JSON field materialization (Agreed, OI-32)

A JSON path field is materialized into a StarRocks asynchronous materialized view per source object:

- `materializeField` adds the field's extracted value as a column of the source object's materialized view (created on
  first use, named after the source object) and records the column in `qe_field.materialized_column`. The field's
  identity never changes; the planner reads the view when it is ready and the JSON path otherwise.
- `DatasetFieldMaterialization.state`, `refreshedAt` and `error` come from the view's refresh status in StarRocks
  (`information_schema.materialized_views` and its task runs). A state table (SG-07) is added only if StarRocks cannot
  report a column-level state.
- `refreshMaterialization` triggers a refresh; `dematerializeField` removes the column from the view (and drops the
  view when no column is left).
- These are commands: storage is physical and allowed on any non-retired version, as the `web2` contract says.

## Managed tables on dataset delete (Agreed, OI-33)

A managed table exists for the datasets over it. When `deleteDataset` passes its delete check (D10), the delete also takes
every table registered in `qe_managed_source_object` that the dataset's versions read and no version of another dataset
reads:

- The materialized view the API defined over the table (OI-32) is dropped, then the table, as schema statements of the
  delete's write plan, so they run before any row is deleted; then the version content, the versions, the table's
  registry row and the dataset row are deleted.
- The drops are planned from what StarRocks still holds, so a retry after a delete that stopped midway drops nothing
  twice and finishes the row deletes. StarRocks keeps a dropped table in its recycle bin for the catalog's retention,
  where `RECOVER TABLE` restores it.
- The delete check is unchanged: its `message` is a summary of what blocks a delete. `web2`'s confirmation names the
  tables the delete drops (`lib/datasets/managed-tables.ts`), from the source objects' `isManaged` and the versions'
  `sourceObject`.

## Change-set operations

```ts
type DatasetOperation =
  | { op: 'updateDataset'; patch: DatasetPatch }
  | { op: 'saveSettings'; settings: DatasetVersionSettings }
  | { op: 'renameSourceTable'; tableName: string }
  | { op: 'saveField'; input: DatasetFieldInput }
  | { op: 'deleteField'; fieldId: Id }
  | { op: 'addFieldsFromColumns'; columns: string[] }
  | { op: 'saveGrain'; input: DatasetGrainInput }
  | { op: 'deleteGrain'; grainId: Id }
  | { op: 'saveBinding'; input: DatasetBindingInput }
  | { op: 'deleteBinding'; bindingId: Id }
  | { op: 'saveMeasure'; input: DatasetMeasureInput }
  | { op: 'deleteMeasure'; measureId: Id }
```

`DatasetFieldInput.createColumn` runs DDL (`ALTER TABLE ADD COLUMN`) at commit, before rows are written, and only on managed
tables (OI-33). DDL cannot be rolled back; a commit that fails after DDL leaves the column, which is harmless because
no field reads it.

## Rule codes

Kept from the current API (`dataset_version` rules): `source_object_exists`, `version_supports_a_mode`,
`default_time_binding_in_version`, `field_key_unique`, `field_columns_exist`, `expression_field_references`,
`expression_field_acyclic`, `grain_key_unique`, `grain_fields_in_version`, `series_grain_excludes_time_field`,
`binding_fields_in_version`, `binding_source_field_groupable`, `bound_roles_distinct`,
`mapped_binding_mapping_set_dimension`, `range_binding_dimension_kind`, `time_binding_dimension_kind`,
`bridge_binding_route`, `unknown_members_in_version`, `interval_estimates_for_finer_periods`, `measure_key_unique`,
`component_fields_in_version`, `component_roles_match_kind`, `formula_references_known_measures`, `formula_acyclic`,
`time_policy_series_grain`, `time_policy_time_binding_in_version`. Each violation's `target.area` is the
`DatasetTab` the rule belongs to.

Retirement (D13): `release_pins_retired_version`, without a target, because the fix is in the releases module.

## Schema mapping

| Contract | Table / column |
|---|---|
| `Dataset` | `qe_dataset` |
| `DatasetVersionSummary`, `DatasetVersionSettings` | `qe_dataset_version` (`properties` JSON as text) |
| `DatasetVersionSummary.createdAt`, `clonedFromVersionId` | `qe_dataset_version.created_at`, `cloned_from_dataset_version_id` (new, SG-04, OI-12) |
| `DatasetVersionSummary.releases` | `qe_release_dataset` joined to `qe_semantic_release`, `qe_database_info` |
| `DatasetField` | `qe_field`; `materialization` from `materialized_column` plus the materialized view's refresh status (OI-32, SG-07) |
| `DatasetGrain` | `qe_dataset_grain` + `qe_dataset_grain_field` (ordinal = position) |
| `DatasetBinding` | `qe_dimension_binding`; `time` = `qe_time_binding` (one-to-one) |
| `DatasetMeasure` | `qe_measure` + `qe_measure_component` + `qe_measure_dependency` (derived from formula) + `qe_measure_time_policy` |
| `DatasetSourceObject`, `DatasetSourceColumn` | StarRocks `information_schema` introspection |

## Deltas from the current API

| Change | Item |
|---|---|
| Keep | Source-object introspection; version create/clone; draft delete; delete blockers |
| Change | Per-entity field, grain, binding, measure `POST`/`PUT`/`DELETE` become change-set operations |
| Change | `PUT dataset-versions/{id}` (settings) becomes the `saveSettings` operation |
| Change | `PUT versions/dataset_version/{id}/status` becomes `validate`, `publish`, `reopen`, `retire` |
| Change | `POST versions/dataset_version/{id}/validation` becomes `check` |
| Change | `POST datasets` accepts `DatasetCreateInput` (both modes) and returns the list item |
| Change | `materialize` / `materialized-column` become the materialization sub-resource (OI-32) |
| New | `delete-check` on datasets and dataset versions, dependents in the version read model, managed-table creation and drop on dataset delete (OI-33), source stats (OI-34) |
| Remove | `PUT datasets/{id}` (identity edits move into the change set) |

## Decisions applied

[OI-01](90-open-issues.md#oi-01), [OI-09](90-open-issues.md#oi-09), [OI-10](90-open-issues.md#oi-10), [OI-11](90-open-issues.md#oi-11),
[OI-12](90-open-issues.md#oi-12), [OI-13](90-open-issues.md#oi-13), [OI-17](90-open-issues.md#oi-17), [OI-23](90-open-issues.md#oi-23),
[OI-32](90-open-issues.md#oi-32), [OI-33](90-open-issues.md#oi-33), [OI-34](90-open-issues.md#oi-34),
[OI-35](90-open-issues.md#oi-35), [OI-36](90-open-issues.md#oi-36), [OI-39](90-open-issues.md#oi-39).
