# Admin API contract

The hand-authored OpenAPI 3.1 description of the Admin API. It is the source of truth (design-first): C# DTOs are
written to match it, `web2` types are generated from it, and `ContractConformanceTests` fails the build when the
generated `api/openapi/admin-api.json` drifts from it ([blueprint 00](../../../docs/blueprint/00-architecture.md#the-contract-first-rule-agreed)).

## Files

| File | Owner | Holds |
|---|---|---|
| `admin-api.yaml` | integrator | Root: info, servers, bearer security, the tag list. No paths or schemas. |
| `common.yaml` | integrator | Shared kernel from [blueprint 01](../../../docs/blueprint/01-api-conventions.md): `Id`, `Key`, `DottedKey`, `Timestamp`, `VersionStatus`, `Problem`, `Violation`, `ViolationTarget`, `DeleteCheck`, `DeleteBlocker`, `DeleteBlockerCode`, `ValidationReport`, the `ChangeSet*` envelopes, `FreezeInput`, `FreezeResult`, `Grouping`, `FilterNode`, the vocabularies more than one module reads (`DimensionKind`, `DimensionPurpose`, `DefinitionOrigin`, `AssumptionSetStatus`, `BindingKind`, `Periodicity`, `MeasureKind`, `ComponentRole`, `RoundingMode`, `SourceSelection`, `VariableMissingBehavior`), `VocabularyTerm`, the `TenantId` parameter and the 400/401/403/404/409/422 responses. |
| `tenant.yaml`, `datasets.yaml`, `dimensions.yaml`, `mappings.yaml`, `releases.yaml`, `models.yaml`, `reports.yaml` | that module's agent | The module's paths and components. |
| `redocly.yaml`, `plugins/qe.mjs` | integrator | Lint rules. |
| `compose.mjs` | integrator | Builds `.composed.yaml` (gitignored) from the root and the module files. |
| `bundled.yaml` | generated | One-file contract for typegen and the conformance test. Never edited by hand. |

## Composition

Each tag in `admin-api.yaml` names a module file: tag `dimensions` is `dimensions.yaml`. `compose.mjs` writes
`.composed.yaml`, the root plus a `$ref` to every path and every component of `common.yaml` and of each module file,
and Redocly lints and bundles that. So a module agent:

- adds paths under `paths:` and schemas, parameters or responses under `components:` of its own file only;
- never edits `admin-api.yaml`, `common.yaml`, `redocly.yaml` or another module's file (a missing shared type is a
  request to the integrator);
- gets every component into `bundled.yaml` and `schema.d.ts` even before an operation uses it.

Composition fails when two files declare the same path or the same component name, because the bundler would
otherwise rename one silently. Name components after the module's entities (`DimensionListItem`, `MappingSetDetail`),
never generically (`ListItem`, `VersionSummary`).

## Writing a module file

- **Paths** are full, from `/api`, as in [01 Routing](../../../docs/blueprint/01-api-conventions.md#routing-and-tenancy-agreed):
  `/api/tenants/{tenantId}/dimensions/{dimensionId}`. Declare `tenantId` once per path item with
  `$ref: ./common.yaml#/components/parameters/TenantId`.
- **Operations** carry `operationId` (camelCase, globally unique, named after the port method it serves), `summary`,
  and exactly one tag: the module's.
- **References:** own components as `#/components/schemas/X`; shared ones as `./common.yaml#/components/schemas/X`.
  A value list or shape that a second module needs moves to `common.yaml`; a single read of another module's
  schema (tenant's vocabularies, releases' `TenantInfo`) references that module's file directly.
- **Errors:** every 4xx response is `application/problem+json` with the common `Problem`. Reuse the common responses
  and override only the description: `'409': { $ref: './common.yaml#/components/responses/Conflict', description: Not a draft. }`.
  Every write (`post`, `put`, `patch`, `delete`) declares `400`.
- **Ids and nulls:** `id`, `<entity>Id` and `<entity>Ids` are strings (`$ref` the common `Id`). A nullable property is
  listed in `required` with `type: [string, 'null']`; a property that may be absent is left out of `required`.
- **Change sets:** the operation union is a `oneOf` of named schemas, one per edit method, each with
  `op: { const: <method> }`, plus `discriminator: { propertyName: op }`. The envelopes specialize the common generics
  with `allOf`, restating `required` so the generated type is not optional:

```yaml
DimensionChangeSetRequest:
  allOf:
    - $ref: './common.yaml#/components/schemas/ChangeSetRequest'
    - type: object
      required: [operations]
      properties:
        operations: { type: array, items: { $ref: '#/components/schemas/DimensionOperation' } }
DimensionChangeSetPreview:
  allOf:
    - $ref: './common.yaml#/components/schemas/ChangeSetPreview'
    - type: object
      required: [state]
      properties:
        state: { $ref: '#/components/schemas/DimensionContent' }
```

## Lint rules

`redocly.yaml` extends Redocly's `recommended` set and adds:

| Rule | Enforces |
|---|---|
| `rule/property-names-camel-case`, `rule/parameter-names-camel-case` | camelCase property and parameter names |
| `qe/string-ids` | `id`, `<entity>Id` and `<entity>Ids` are strings |
| `qe/problem-responses` | writes declare 400; every error response is the common `Problem` |
| `qe/operation-module-tag` | each operation is tagged exactly with the module whose file declares it |
| `operation-tag-defined`, `operation-operationId`, `operation-operationId-unique`, `operation-summary`, `operation-4xx-response` | tags from the root list, unique operation ids, summaries, at least one 4xx |

## Commands

From the Admin repo root (`pnpm install` once):

```sh
pnpm contract:lint      # compose, then lint
pnpm contract:bundle    # compose, then write api/openapi/contract/bundled.yaml
```

From `web2/`:

```sh
pnpm contract:types     # contract:bundle, then generate src/lib/api/schema.d.ts
pnpm build              # type-checks schema.d.ts with the rest of web2
```

`bundled.yaml` and `web2/src/lib/api/schema.d.ts` are committed, so the conformance test and the `web2` build run
without Node tooling. Regenerate both whenever a contract file changes; on a merge conflict in either, take any side
and regenerate.
