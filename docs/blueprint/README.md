# Admin Web API and frontend contract blueprint

This folder is the blueprint for rebuilding the Query Engine Admin Web API and for giving the new web application
(`web2`) a frontend contract layer. It replaces neither the concept docs nor the requirements briefs: it turns them,
the `web2` prototype and the authoritative database schema into one interface the frontend and the backend refactors
both build against.

The frontend drives the design. Where the prototype's contracts (`web2/src/lib/*/contract.ts`) describe what a screen
needs, the API is shaped to serve it. Where the prototype asks for something the schema cannot hold, or where two
sources disagree, the blueprint records an open issue instead of choosing silently.

Each module's frontend contract and HTTP API are transcribed into the hand-authored OpenAPI contract in
[`api/openapi/contract/`](../../api/openapi/contract/README.md). The contract is the source of truth for the wire
format; the blueprint remains the source for the rules behind it
([00, the contract-first rule](00-architecture.md#the-contract-first-rule-agreed)).

## Sources

| Source                                                                                                                                              | Role                                                                      |
| --------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| [Database schema, design revision 2.27.0](../../../database/srdb_ebm/README.md) (`schema/01_tables`, `docs/schema/Query_Engine_Database_Schema.md`) | **Authoritative.** Tables, columns, allowed values and publication rules. |
| [Query engine README](../../../Quantalytix.QueryEngine/README.md)                                                                                   | What the engine reads, trusts and refuses; report run contract 2.1.       |
| [Current Admin API README](../../api/README.md), `api/openapi/admin-api.json`, `api/docs/architecture/`                                             | The API being rebuilt: about 160 operations in 43 controllers.            |
| [Dimensions concept](../concepts/dimensions.md)                                                                                                     | Dimension model, lifecycle and proposed profiles.                         |
| [Dimension manager brief](../requirements/dimension-manager-prototype.md)                                                                           | Dimension manager behaviors.                                              |
| [Mappings and bridges brief](../requirements/mappings-and-bridges-prototype.md), [rev. 2 in web2](../../web2/docs/mappings-and-bridges.md)          | Mapping sets and bridges.                                                 |
| [Releases and assumption sets brief](../requirements/releases-and-assumption-sets-prototype.md)                                                     | Releases, activation, capture.                                            |
| `web2` [prototype](../../web2/src/lib)                                                                                                              | Module ports, read models, mock rules; report and model stores.           |

## Reading order

1. [00 Architecture](00-architecture.md): the layers, the contract-first rule, who validates, the engine boundary.
2. [01 API conventions](01-api-conventions.md): routing, ids, errors, concurrency, the change-set commit protocol.
3. [02 Frontend contract layer](02-frontend-contract-layer.md): the shared kernel, ports, adapters and caching.
4. [03 Module refactor checklist](03-module-refactor-checklist.md): the steps, files and gotchas of moving a module to
   the contract, from the dimensions pilot.
5. Module blueprints:
   - [10 Tenant, session and catalog](10-tenant-session-catalog.md)
   - [11 Datasets](11-datasets.md)
   - [12 Dimensions](12-dimensions.md)
   - [13 Mapping sets and bridges](13-mappings-and-bridges.md)
   - [14 Releases and assumption sets](14-releases-and-assumption-sets.md)
   - [15 Calculation models](15-calculation-models.md)
   - [16 Reports](16-reports.md)
6. [90 Open issues](90-open-issues.md) and [91 Schema gaps](91-schema-gaps.md).

## Status legend

Every decision in these files carries one of three labels.

| Label | Meaning |
|---|---|
| **Agreed** | Stated by the schema, the engine or every source; not up for discussion here. |
| **Proposed** | The blueprint's recommendation. Adopted unless an open issue changes it. |
| **Pending** | Needs a decision. The open issue lists the options and a recommendation. |

Open issues are numbered `OI-nn` and defined once, in [90 Open issues](90-open-issues.md). Module files link to them.

## Module template

Each module file follows the same outline so the two refactors can be planned side by side:

1. Purpose and sources
2. Domain rules (with the source that states each)
3. Frontend contract: read models, inputs and the port, with deltas from the current `web2` contract
4. HTTP API: method, route, request, response, errors and the port method it serves
5. Change-set operations: what an aggregate's commit accepts
6. Rule codes
7. Schema mapping
8. Deltas from the current API: keep, change, new, remove
9. Open issues

## Current API coverage

Every controller of the current API and the blueprint file that decides its fate (each module file's "Deltas from the
current API" table says keep, change, new or remove).

| Current controller | Blueprint |
|---|---|
| `SessionController`, `TenantsController`, `SystemTenantsController`, `VocabulariesController`, `OverviewController`, `LookupsController`, `DevelopmentTokenController` | [10](10-tenant-session-catalog.md) |
| `VersionStatusController` (`PUT versions/{kind}/{id}/status`, `POST versions/{kind}/{id}/validation`) | Replaced by per-kind `validate`, `publish`, `reopen`, `retire`, `freeze` and `check` endpoints ([01](01-api-conventions.md#routing-and-tenancy-agreed)) in 11–14 |
| `DatasetsController`, `DatasetVersionsController`, `FieldsController`, `GrainsController`, `BindingsController`, `MeasuresController`, `SourceObjectsController` | [11](11-datasets.md) |
| `DimensionsController`, `DimensionRolesController`, `DimensionVersionsController`, `DimensionLevelsController`, `DimensionMembersController`, `RangeBandsController` | [12](12-dimensions.md) |
| `MappingSetsController`, `MappingVersionsController`, `MappingEntriesController`, `BridgesController`, `BridgeVersionsController`, `BridgeMappingsController` | [13](13-mappings-and-bridges.md) |
| `ReleasesController`, `AssumptionSetsController` | [14](14-releases-and-assumption-sets.md) |
| `CalculationModelsController`, `VariablesController`, `CalculatedMeasuresController`, `MetricAttributionsController`, `ModelParametersController` | [15](15-calculation-models.md) |
| `ReportsController`, `ReportEditorController`, `ReportSourcesController`, `SourceProfilesController`, `CalculationDimensionsController`, `ReportScenariosController`, `ReportVariableScopesController`, `ReportColumnsController`, `ReportStylesController` | [16](16-reports.md) |

## Decision log

Record resolved open issues here, newest first, with the date, the issue, the chosen option and who decided.

| Date | Issue | Decision | By |
|---|---|---|---|
| 2026-10-27 | [OI-29](90-open-issues.md#oi-29) | The dotted key pattern (`DottedKey`) extends to dataset, calculation model and report keys, as the seed writes them. | Integrator, G1 |
