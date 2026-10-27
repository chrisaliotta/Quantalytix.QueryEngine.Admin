# 10 Tenant, session and catalog

## Purpose and sources

The cross-module surface every screen needs: who the caller is, which tenants they reach, the tenant's state, the
enumerations selects render, and one catalog of names and ids for pickers and cross-module links.

Sources: current `SessionController`, `TenantsController`, `SystemTenantsController`, `VocabulariesController`,
`OverviewController`, `LookupsController`; `web2` `lib/session/viewer.ts`, `ReleasesApi.getCatalog`,
`DatasetsApi.getVocabularies` / `getTenantCatalog`, `MappingsApi.listDimensionRefs` / `getPublishedMembers`,
`DimensionsApi.listAllDimensions`.

## Domain rules

- **Agreed:** exactly one active assumption set per tenant; the API refuses a tenant database without one
  ([schema README](../../../database/srdb_ebm/README.md), "Applying the schema").
- **Agreed:** the tenant's active release is `qe_database_info.active_semantic_release_id`; its engine cache key is
  `semantic_revision`.
- **Agreed:** tenant connection strings live in `query_control.qe_tenant_directory` and are visible to system admins
  only.

## Frontend contract (Agreed)

### Session (`lib/session/contract.ts`)

```ts
export interface Session { subject: string; displayName: string | null; tenantId: string | null; isSystemAdmin: boolean }
export interface AccessibleTenant { tenantId: string; isEnabled: boolean }

export interface SessionApi {
  getSession(): Promise<Session>
  listTenants(): Promise<AccessibleTenant[]>
}
```

### Tenant overview and catalog (`lib/tenant/contract.ts`)

Three `web2` modules each load an overlapping catalog: `ReleasesApi.getCatalog` (datasets, dataset versions,
identities, reports, releases), `DatasetsApi.getTenantCatalog` (dimensions with published members, roles, mapping sets,
bridges, releases) and `MappingsApi.listDimensionRefs` (dimension refs). `DimensionsApi.listAllDimensions` is a fourth.
**Agreed (OI-13):** one tenant catalog with the union of what they read, loaded once and cached on
`semanticRevision`.

```ts
export interface TenantInfo {
  tenantKey: string
  metadataSchemaVersion: number
  semanticRevision: number
  activeReleaseId: Id | null
}

export interface TenantOverview extends TenantInfo {
  counts: TenantOverviewCounts // datasets, analytical dimensions, mapping sets, analytical bridges, releases, sets, models, reports, activeMeasures
  drafts: TenantOverviewDrafts // datasetVersions, releases
}

// Dataset, model, report, mapping set and bridge keys are DottedKey (OI-29).
export interface TenantCatalogDataset { id: Id; key: string; displayName: string; isEnabled: boolean; activeVersionId: Id | null }
export interface TenantCatalogDimension {
  id: Id; key: string; displayName: string; kind: DimensionKind; purpose: DimensionPurpose; origin: DefinitionOrigin
  isEnabled: boolean; publishedVersionId: Id | null; publishedVersionNumber: number | null; ownerReportId: Id | null
}
export interface TenantCatalogRole { id: Id; key: string; displayName: string; dimensionId: Id }
export interface TenantCatalogMappingSet { id: Id; key: string; displayName: string; dimensionId: Id }
export interface TenantCatalogBridge { id: Id; key: string; displayName: string; sourceDimensionId: Id; targetDimensionId: Id; ownerReportId: Id | null }
export interface TenantCatalogModel { id: Id; key: string; displayName: string; isEnabled: boolean }
export interface TenantCatalogReport { id: Id; key: string; displayName: string; isEnabled: boolean; modelId: Id; reportDimensionId: Id }
export interface TenantCatalogRelease { id: Id; key: string; status: VersionStatus; isActive: boolean }
export interface TenantCatalogAssumptionSet { id: Id; key: string; displayName: string; status: AssumptionSetStatus }

export interface TenantCatalog {
  tenant: TenantInfo
  datasets: TenantCatalogDataset[]
  dimensions: TenantCatalogDimension[]
  roles: TenantCatalogRole[]
  mappingSets: TenantCatalogMappingSet[]
  bridges: TenantCatalogBridge[]
  models: TenantCatalogModel[]
  reports: TenantCatalogReport[]
  releases: TenantCatalogRelease[]
  assumptionSets: TenantCatalogAssumptionSet[]
}

export interface PublishedMember { key: string; displayName: string; parentKey: string | null; isAssignable: boolean; levelKey: string | null }
export interface PublishedMembers { dimensionId: Id; versionId: Id; members: PublishedMember[] }

export interface TenantApi {
  getOverview(): Promise<TenantOverview>
  getCatalog(): Promise<TenantCatalog>
  /** Members of a dimension's published version, or null when none is published. */
  getPublishedMembers(dimensionId: Id): Promise<PublishedMembers | null>
  getVocabularies(): Promise<Vocabularies>
}
```

`TenantInfo` is also the answer of release activation ([14](14-releases-and-assumption-sets.md#http-api)), so a client
updates its revision display from the command's own result.

Report-owned dimensions and bridges are in the catalog only so cross-module references resolve (a report source's
bridge, an assumption set pin); no picker or list offers them (OI-22).

Module contracts keep their own narrow types (`MappingDimensionRef`, `PinIdentity`) as projections of the catalog where
it helps a screen, but they read from `TenantApi`, not from a module-specific catalog endpoint. The dataset screens'
`TenantLookups` is a `Pick` of `TenantApi`, so a `TenantApi` serves them unchanged.

**Deltas from `web2`:**

- `datasets/contract.ts` `Dimension.publishedMembers` (members inline for every dimension) moves to
  `getPublishedMembers`, fetched per picker.
- `datasets/contract.ts` `Release.displayName` has no column ([OI-39](90-open-issues.md#oi-39)); dropped.
- `datasets/contract.ts` `DimensionKind = 'standard' | 'range' | 'time'` is replaced by the schema's four kinds
  ([OI-11](90-open-issues.md#oi-11)).

### Vocabularies

`GET /api/vocabularies` returns every enumeration a select renders, keyed by name, with `componentRolesByKind` and
the time zone list. Each vocabulary's values reference the schema that owns them: the shared kernel for the values more
than one module reads (`DimensionKind`, `DimensionPurpose`, `DefinitionOrigin`, `AssumptionSetStatus`, `BindingKind`,
`Periodicity`, `MeasureKind`, `ComponentRole`, `RoundingMode`, `SourceSelection`, `VariableMissingBehavior`), the module
file otherwise (`DatasetDataType`, `MappingCardinality`, `ReportLayout`). The dataset contract's `Vocabularies` interface is the starting point and grows with the other
modules' unions: `dimensionKind`, `rangeDataType`, `editPolicy`, `cardinality`, `allocationMode`, `sourceSelection`,
`mappingMethod`, `groupingMode`, `selectionScope`, `formulaLanguageVersion`, `attributionMethod`, `parameterValueType`,
`displayFormat`, `subtotalPosition`, `styleRole`, `fontWeight`, `borderStyle`, `layout`, `periodicity`,
`dayCountConvention`.

## HTTP API

| Method | Route | Response | Requires | Port |
|---|---|---|---|---|
| GET | `/api/me` | `Session` | any | `SessionApi.getSession` |
| GET | `/api/tenants` | `AccessibleTenant[]` | any | `SessionApi.listTenants` |
| GET | `/api/vocabularies` | `Vocabularies` | any | `TenantApi.getVocabularies` |
| GET | `/api/tenants/{t}/overview` | `TenantOverview` | any | `TenantApi.getOverview` |
| GET | `/api/tenants/{t}/catalog` | `TenantCatalog` | any | `TenantApi.getCatalog` (new) |
| GET | `/api/tenants/{t}/dimensions/{id}/published-members` | `PublishedMembers` or `404` when none published | any | `TenantApi.getPublishedMembers` (new) |
| GET | `/api/tenants/{t}/source-objects` | `DatasetSourceObject[]` | any | [11 Datasets](11-datasets.md) |
| GET | `/api/system/tenants` | `TenantDirectoryEntry[]` | SystemAdmin | system screen |
| POST | `/api/system/tenants` | `201 TenantDirectoryEntry` | SystemAdmin | |
| GET / PUT | `/api/system/tenants/{t}` | `TenantDirectoryEntry` | SystemAdmin | |
| PUT | `/api/system/tenants/{t}/enabled` | `TenantDirectoryEntry` | SystemAdmin | |
| POST | `/api/system/tenants/verify` | `TenantConnectionCheck` | SystemAdmin | |
| POST | `/development/token` | dev token | anonymous, Development only | dev sign-in |
| GET | `/health` | `200` | anonymous | |

The tenant directory endpoints have no `web2` screen yet; they are kept as they are.

## Deltas from the current API

| Change | Item |
|---|---|
| Keep | `/api/me`, `/api/tenants`, system tenants, `/development/token`, `/health`, `overview` |
| Change | `/api/vocabularies` returns labels and a keyed object instead of `{name, values[{value, description}]}[]` |
| Change | `SessionInfo` gains `displayName` |
| New | `catalog`, `dimensions/{id}/published-members` |
| Remove | `lookups/{kind}` (27 kinds). Its pickers are served by the catalog and by module read models. Keep it only if a screen needs a kind the catalog lacks (OI-13). |

## Decisions applied

[OI-04](90-open-issues.md#oi-04), [OI-09](90-open-issues.md#oi-09), [OI-11](90-open-issues.md#oi-11),
[OI-13](90-open-issues.md#oi-13), [OI-22](90-open-issues.md#oi-22), [OI-39](90-open-issues.md#oi-39).
