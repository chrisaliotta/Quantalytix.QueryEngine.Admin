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

export interface TenantCatalog {
  tenant: TenantInfo
  datasets: { id: Id; key: string; displayName: string; isEnabled: boolean; activeVersionId: Id | null }[]
  dimensions: {
    id: Id; key: string; displayName: string; kind: DimensionKind; purpose: 'analytical' | 'report'
    origin: 'system' | 'user'; isEnabled: boolean
    publishedVersionId: Id | null; publishedVersionNumber: number | null
    ownerReportId: Id | null
  }[]
  roles: { id: Id; key: string; displayName: string; dimensionId: Id }[]
  mappingSets: { id: Id; key: string; displayName: string; dimensionId: Id }[]
  bridges: { id: Id; key: string; displayName: string; sourceDimensionId: Id; targetDimensionId: Id; ownerReportId: Id | null }[]
  models: { id: Id; key: string; displayName: string; isEnabled: boolean }[]
  reports: { id: Id; key: string; displayName: string; isEnabled: boolean; modelId: Id; reportDimensionId: Id }[]
  releases: { id: Id; key: string; status: VersionStatus; isActive: boolean }[]
  assumptionSets: { id: Id; key: string; displayName: string; status: 'active' | 'historical' }[]
}

export interface PublishedMembers { dimensionId: Id; versionId: Id; members: { key: string; displayName: string; parentKey: string | null; isAssignable: boolean; levelKey: string | null }[] }

export interface TenantApi {
  getCatalog(): Promise<TenantCatalog>
  /** Members of a dimension's published version, or null when none is published. */
  getPublishedMembers(dimensionId: Id): Promise<PublishedMembers | null>
  getVocabularies(): Promise<Vocabularies>
}
```

Report-owned dimensions and bridges are in the catalog only so cross-module references resolve (a report source's
bridge, an assumption set pin); no picker or list offers them (OI-22).

Module contracts keep their own narrow types (`DimensionRef`, `Identity`) as projections of the catalog where it helps
a screen, but they read from `TenantApi`, not from a module-specific catalog endpoint.

**Deltas from `web2`:**

- `datasets/contract.ts` `Dimension.publishedMembers` (members inline for every dimension) moves to
  `getPublishedMembers`, fetched per picker.
- `datasets/contract.ts` `Release.displayName` has no column ([OI-39](90-open-issues.md#oi-39)); dropped.
- `datasets/contract.ts` `DimensionKind = 'standard' | 'range' | 'time'` is replaced by the schema's four kinds
  ([OI-11](90-open-issues.md#oi-11)).

### Vocabularies

`GET /api/vocabularies` returns every enumeration a select renders, keyed by name, with `componentRolesByKind` and
the time zone list. The dataset contract's `Vocabularies` interface is the starting point and grows with the other
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
| GET | `/api/tenants/{t}/overview` | `TenantInfo & { counts, drafts }` | any | overview screen (not in `web2` yet) |
| GET | `/api/tenants/{t}/catalog` | `TenantCatalog` | any | `TenantApi.getCatalog` (new) |
| GET | `/api/tenants/{t}/dimensions/{id}/published-members` | `PublishedMembers` or `404` when none published | any | `TenantApi.getPublishedMembers` (new) |
| GET | `/api/tenants/{t}/source-objects` | `SourceObject[]` | any | [11 Datasets](11-datasets.md) |
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
