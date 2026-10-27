# 00 Architecture

## Layers

```mermaid
flowchart LR
  Screens --> Port["Module port, contract.ts"]
  Port --> HttpAdapter["HTTP adapter, lib/module/http"]
  Port --> MockAdapter["Mock adapter, lib/module/mock"]
  HttpAdapter --> Rest["Admin Web API, REST"]
  Rest --> App["Application: gates, rules, change-set apply"]
  App --> StarRocks["StarRocks tenant DB, schema 2.26.0"]
  App --> Control["query_control tenant directory"]
  StarRocks --> Engine["Query engine, reads views"]
```

| Layer | Owns | Never does |
|---|---|---|
| Screens (`web2/src/components`) | Rendering, local UI state, URL state | Calls `fetch`, imports a DTO, decides whether input is valid |
| Module port (`web2/src/lib/<module>/contract.ts`) | The frontend contract: read models, inputs, results, the port interface | Knows about HTTP or the mock |
| Adapters (`lib/<module>/http`, `lib/<module>/mock`) | Translating the port to a transport | Holds business rules the server does not also hold |
| Admin Web API (controllers) | Routing, authentication, tenant access, binding, problem responses | Business rules |
| Application | Feature services, input validators, publication rules, gates, change-set application | SQL |
| Infrastructure (StarRocks) | Table gateways, introspection, DDL for managed objects, tenant routing | Rules |

## The contract-first rule (Agreed)

1. The TypeScript port in `contract.ts` is the frontend contract. A screen depends only on its module's port and on
   the shared kernel ([02](02-frontend-contract-layer.md)).
2. Backend request and response DTOs are shaped to the port's read models and inputs, not to the tables. A read model
   may join several tables (a dimension list item carries its roles, version summaries and profile); the Application
   composes it.
3. The OpenAPI document stays generated from the C# build. The generated `schema.d.ts` is imported only inside the HTTP
   adapters, which map DTOs to port types. A DTO that differs from its port type only in naming is a defect: rename the
   DTO.
4. Each adapter must pass the same port-level test suite, so the mock and HTTP adapters cannot drift (parity rule).

Today's `web` client re-exports generated schema types into screens. `web2` must not; its prototype already isolates
screens behind ports, and the blueprint keeps that boundary.

## Where validation lives (Agreed, OI-05)

- **The server is authoritative.** Every input check, publication rule, gate and cascade runs in the Application. The
  engine trusts the metadata it reads and re-checks none of it (engine README "Checks before any source runs";
  schema doc §18), so a rule the API skips is a rule nobody enforces.
- **The client renders server results.** Field errors, rule violations, delete blockers, readiness and coverage come
  back from the API in the shapes [01](01-api-conventions.md) defines. A screen may disable a control from a read
  model flag (for example `isVersionEditable`) but does not compute validity.
- **Client-side helpers stay presentational.** Parsing a pasted table into rows, highlighting a formula, or grouping a
  list is presentation; deciding that a row is invalid is not. `web2` currently runs real rules in the browser:
  `lib/mappings/rules.ts`, `lib/model/rules.ts` and the report's `coverage.ts`, `profiles.ts` and `scenarios.ts`. Those
  move behind check endpoints and read-model diagnostics; formulas are checked by `formula-check` (OI-43).
- **Validate once, at the boundary.** Input validators run when a request enters the Application; publication rules
  run when a version is validated or an aggregate is committed. Deeper layers assume valid input.

## Aggregates

The API is organized around the aggregates a user edits and saves as one unit. Each has one change set
([01, change-set protocol](01-api-conventions.md#change-set-protocol-agreed-oi-01)).

| Aggregate | Root | Edited content | Lifecycle |
|---|---|---|---|
| Dataset version | `qe_dataset_version` | settings, fields, grains, bindings (+ time binding), measures (+ components, time policy) | strict: draft, validated, published, retired |
| Dimension | `qe_dimension` + its live version | identity flags, roles, the live version's levels, members, range bands | living, live-and-frozen: one live version edited in place, frozen snapshots (OI-14) |
| Mapping set | `qe_mapping_set` + its live version | identity, entries | living, live-and-frozen (OI-14) |
| Bridge | `qe_bridge` + its live version | identity, cardinality, allocation, mappings (analytical bridges only) | living, live-and-frozen (OI-14) |
| Release | `qe_semantic_release` | manifest | strict |
| Assumption set | `qe_assumption_set` | label and description (historical only) | captured, never edited |
| Calculation model | `qe_calculation_model` | settings, variables, calculated measures, parameters, attributions | not versioned; every write gated |
| Report | `qe_report` | settings, rows, sources, profiles, calculation dimensions, scenarios, scopes, columns, styles, plus its report dimension and report bridges | not versioned; enabled reports gated |

## The engine boundary (Agreed)

- The engine reads metadata only through `vw_qe_*` views and caches snapshots by tenant, release,
  `semantic_revision`, assumption set and dataset. Every committed change the engine can see must advance
  `qe_database_info.semantic_revision` exactly once per commit ([01](01-api-conventions.md#semantic-revision-agreed)).
- The engine lists and runs **enabled** reports only, on the active assumption set. Report preview and run from the
  admin are out of scope for this refactor (OI-53).
- The engine has no formula-validation endpoint. The Admin API references `Quantalytix.QueryEngine.Domain` for
  `ExpressionParser` and must type-check formulas itself (OI-43).
- The engine is reached over SignalR (`/hubs/query`). The Admin API does not call it.

## Out of scope

- The engine's request contracts (query 1.4, report 2.1) and report preview or run (OI-53).
- Identity provider integration beyond the claims the API reads.
- Physical fact data.
