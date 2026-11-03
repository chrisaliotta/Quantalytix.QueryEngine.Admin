# 00 Architecture

## Layers

```mermaid
flowchart LR
  Contract["OpenAPI contract, api/openapi/contract"] -. mirrored by .-> Port
  Contract -. matched by DTOs .-> Rest
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
| Screens (`web/src/components`) | Rendering, local UI state, URL state | Calls `fetch`, imports a DTO, decides whether input is valid |
| Module port (`web/src/lib/<module>/contract.ts`) | The frontend contract: read models, inputs, results, the port interface, mirroring the OpenAPI contract | Knows about HTTP or the mock |
| Adapters (`lib/<module>/http`, `lib/<module>/mock`) | Translating the port to a transport | Holds business rules the server does not also hold |
| Admin Web API (controllers) | Routing, authentication, tenant access, binding, problem responses | Business rules |
| Application | Feature services, input validators, publication rules, gates, change-set application | SQL |
| Infrastructure (StarRocks) | Table gateways, introspection, DDL for managed objects, tenant routing | Rules |

## The contract-first rule (Agreed)

1. **The hand-authored OpenAPI contract is the source of truth (design-first).** It lives in
   [`api/openapi/contract/`](../../api/openapi/contract/README.md): a root `admin-api.yaml`, the shared kernel
   `common.yaml` (from [01](01-api-conventions.md)) and one file per module. Its read models and inputs are shaped to
   what the screens need, not to the tables. A read model may join several tables (a dimension list item carries its
   roles, version summaries and profile); the Application composes it.
2. The TypeScript port in `contract.ts` mirrors the contract's schemas by name and is what screens see. A screen depends
   only on its module's port and on the shared kernel ([02](02-frontend-contract-layer.md)).
3. **C# request and response DTOs are written by hand to match the contract;** no server code is generated. A DTO
   whose name, property names, types or nullability differ from its contract schema is a defect: fix the DTO, or change
   the contract first.
4. `api/openapi/admin-api.json` stays generated from the C# build, as the record of what the server implements.
   `ContractConformanceTests` (`api/tests`) loads the bundled contract and `admin-api.json` and fails on any difference
   in paths, methods, status codes, schema property names, types or nullability. Operations not yet implemented are
   listed in its allow-list, which only shrinks and is empty when the refactor ends.
5. `web/src/lib/api/schema.d.ts` is generated from the bundled contract and imported only inside the HTTP adapters,
   which map generated types to port types.
6. Each adapter must pass the same port-level test suite, so the mock and HTTP adapters cannot drift (parity rule).

The removed earlier client re-exported generated schema types into screens. `web` must not; its prototype already
isolates screens behind ports, and the blueprint keeps that boundary.

## Where validation lives (Agreed, OI-05)

- **The server is authoritative.** Every input check, publication rule, gate and cascade runs in the Application. The
  engine trusts the metadata it reads and re-checks none of it (engine README "Checks before any source runs";
  schema doc §18), so a rule the API skips is a rule nobody enforces.
- **The client renders server results.** Field errors, rule violations, delete blockers, readiness and coverage come
  back from the API in the shapes [01](01-api-conventions.md) defines. A screen may disable a control from a read
  model flag (for example `isVersionEditable`) but does not compute validity.
- **Client-side helpers stay presentational.** Parsing a pasted table into rows, highlighting a formula, or grouping a
  list is presentation; deciding that a row is invalid is not. `web` currently runs real rules in the browser:
  `lib/mappings/rules.ts`, `lib/model/rules.ts` and the report's `coverage.ts`, `profiles.ts` and `scenarios.ts`. Those
  move behind check endpoints and read-model diagnostics; formulas are checked by `formula-check` (OI-43).
- **Validate once, at the boundary.** Input validators run when a request enters the Application; gate and commit rules
  run when an aggregate is committed or a snapshot is restored. Deeper layers assume valid input.

## Aggregates

The API is organized around the aggregates a user edits and saves as one unit. Each has one change set
([01, change-set protocol](01-api-conventions.md#change-set-protocol-agreed-oi-01)).

Every aggregate is an identity with its version in the live snapshot. The live version is edited in place, every commit runs
the gate and commit rules, and frozen snapshots hold read-only copies ([14](14-snapshots.md)).

| Aggregate | Root | Edited content |
|---|---|---|
| Dataset | `qe_dataset` + its live version | identity, settings, enabled flag, fields, grains, bindings (+ time binding), measures (+ components, time policy) |
| Dimension | `qe_dimension` + its live version | identity flags, roles, the live version's levels, members, range bands |
| Mapping set | `qe_mapping_set` + its live version | identity, entries |
| Bridge | `qe_bridge` + its live version | identity, enabled flag, cardinality, allocation, mappings (analytical bridges only) |
| Calculation model | `qe_calculation_model` + its live version | settings, variables, calculated measures, parameters, attributions |
| Report | `qe_report` + its live version | settings, rows, sources, profiles, calculation dimensions, scenarios, scopes, columns, styles, plus its report dimension and report bridges |

Snapshots themselves have no change set: capture, restore, relabel and delete are commands.

## The engine boundary (Agreed)

- The engine reads metadata only through `vw_qe_*` views and caches metadata by tenant, snapshot,
  `semantic_revision` and dataset or report. Every commit and snapshot command that changes metadata must advance
  `qe_database_info.semantic_revision` exactly once per commit ([01](01-api-conventions.md#semantic-revision-agreed)).
- The engine lists and runs **enabled** reports only, on the live snapshot unless a comparison names a frozen one. Report preview and run from the
  admin are out of scope for this refactor (OI-53).
- The engine has no formula-validation endpoint. The Admin API references `Quantalytix.QueryEngine.Domain` for
  `ExpressionParser` and must type-check formulas itself (OI-43).
- The engine is reached over SignalR (`/hubs/query`). The Admin API does not call it.

## Out of scope

- The engine's request contracts (query 1.4, report 2.1) and report preview or run (OI-53).
- Identity provider integration beyond the claims the API reads.
- Physical fact data.
