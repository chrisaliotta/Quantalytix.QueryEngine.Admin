# Query Engine Admin API

An ASP.NET Core Web API on .NET 10 that authors the semantic metadata the Quantalytix query engine reads from StarRocks:
datasets, fields, grains, dimensions and their roles, members, mappings, bridges, bindings, measures, calculation models,
reports, releases, assumption sets and the tenant directory. A role is a named use of a dimension, such as
`servicingBranch` of `branch`: a binding binds a field to a role, and requests name the binding by the role's key.
Drafts never reach queries. Dataset versions reach them only through an activated release. Dimension, mapping and bridge
versions are living: the published one is the default every request reads through the active assumption set, edits to it
are live, and publishing the next version retires it. A query can name a captured historical assumption set to backtest
against the versions of its time, over the dataset versions of the release that was active when it was captured.
Calculation models and reports are not versioned. The report editor builds a report's rows and sources in place, the
engine runs a report while it is enabled, always on the active assumption set and with its model as it stands, and every
write to an enabled report, or to a model, its metrics, their attributions or its parameters, is checked before it is
made. A model's attributions say how each metric's change between two values, such as actuals and a budget, splits into
the effects of its drivers. A model declares number and text parameters, such as the day count convention its formulas
pass to `YEARFRAC` on formula language 1.2; a report sets their values on its rows, which the rows under them inherit,
and names the styles its rows show. A report's scenarios are roles whose members are alternatives a run reads one at a
time, such as plan versions or interest rate shocks: a run or comparison names one member of each by the role's key, so
alternatives are never added together. A scenario's dimension has exactly one role, a business rule the API keeps on both
the scenario list and new roles, so a scenario and its dimension are one.

| Project | Holds |
|---|---|
| `src/Quantalytix.QueryEngine.Admin.Api` | Controllers, authentication and authorization, the validation filter, OpenAPI |
| `src/Quantalytix.QueryEngine.Admin.Application` | Feature services, input validators, publication and retirement rules, the version workflow |
| `src/Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks` | Table gateways (Dapper), source-object introspection, schema changes, tenant connection checks |
| `tests/Quantalytix.QueryEngine.Admin.Tests` | xUnit v3 tests on Microsoft.Testing.Platform |

## Architecture

The [architecture pages](docs/architecture/README.md) describe the API with Mermaid diagrams: its runtime components and
composition root, the request pipeline and error responses, the core and per-feature class diagrams, the version
lifecycle, and the sequence of each key write.

## Prerequisites

- The .NET 10 SDK.
- A checkout of the query engine, `Quantalytix.QueryEngine`. The Application project references its
  `Quantalytix.QueryEngine.Domain` project for the expression parser.
- The seed kit, `database/srdb_ebm`, at design revision 2.26.0. The tests read its generated schema build script,
  `ddl_srdb_bi_v2_26.sql`.
- A tenant database at metadata schema version 17 with its active assumption set, as the seed kit's build script creates
  it. Tenant verification refuses a database without exactly one active set.
- Network access to the StarRocks cluster, for every endpoint that reads or writes metadata.

## Configuration

- **`QueryEngineRoot`:** the path of the query engine checkout. MSBuild defaults to `../../Quantalytix.QueryEngine`
  from `api/`. Anywhere else, such as a git worktree, set an environment variable of the same name:
  `export QueryEngineRoot=/path/to/Quantalytix.QueryEngine/`.
- **`SeedKitRoot`:** the path of the seed kit. MSBuild defaults to `../../database/srdb_ebm` from `api/`; an
  environment variable of the same name overrides it.
- **`ConnectionStrings:ControlPlane`:** the `query_control` database, whose `qe_tenant_directory` routes each tenant
  to its own database. Keep it in user secrets, never in `appsettings*.json`:

  ```bash
  dotnet user-secrets set ConnectionStrings:ControlPlane "<connection string>" --project src/Quantalytix.QueryEngine.Admin.Api
  ```

- **`Cors:AllowedOrigins`:** the browser origins that may call the API; none by default. `appsettings.Development.json`
  allows the Vite dev server, `http://localhost:5173`.
- **`Authentication:Schemes:Bearer`:** the standard ASP.NET Core JWT bearer settings (`ValidIssuer`, `ValidAudiences`,
  `Authority` or signing keys) through which a production identity provider plugs in. A token carries `sub`, one
  `tenant_id` claim, and `role: SystemAdmin` for a caller who may reach every tenant. Development also trusts
  `dotnet user-jwts` tokens.

## Build

From `api/`:

```bash
MSBUILDDISABLENODEREUSE=1 dotnet build Quantalytix.QueryEngine.Admin.slnx -maxcpucount:4
```

Warnings are errors, and every type and member needs an XML `<summary>`. The build also writes the OpenAPI document,
`openapi/admin-api.json`.

## Test

From `api/`, after a build:

```bash
dotnet test --solution Quantalytix.QueryEngine.Admin.slnx --no-build
```

- Run it from `api/`: `global.json` selects Microsoft.Testing.Platform there, and anywhere else `dotnet test` falls
  back to VSTest.
- Don't pass `-maxcpucount` to `dotnet test`. The platform forwards it to the test app, which then runs no tests.
- To run one class, use the xUnit runner from the test project's directory:
  `./bin/Debug/net10.0/Quantalytix.QueryEngine.Admin.Tests -class <full class name>`.
- The live tests are skipped unless you opt in with `QE_ADMIN_LIVE_GATEWAY=true`: the gateway round trip, a check that
  every seeded report passes the report rules and opens in the editor, and a new report's round trip from creation to
  deletion. They write and then delete their own rows in tenant `development`, so they need the control-plane secret and
  the cluster.

## Run

```bash
dotnet run --project src/Quantalytix.QueryEngine.Admin.Api --launch-profile http
```

The `http` profile listens on `http://localhost:5180` in Development; add `-- --urls http://localhost:<port>` for
another port. Development hosts issue tokens for any tenant:

```bash
curl -s -X POST http://localhost:5180/development/token -H 'Content-Type: application/json' \
  -d '{"tenantId":"development","systemAdmin":true}'
```

The response's `accessToken` is a bearer token for eight hours. The signing key lives in memory, so tokens die when the
host restarts. `POST /development/token` exists only in Development and is not in the OpenAPI document. `GET /health`
answers without a token.

## From the OpenAPI document to `schema.d.ts`

Every build regenerates `openapi/admin-api.json`. The web app generates its client types from that file:

```bash
cd ../web && pnpm api:types   # writes web/src/api/schema.d.ts
```

Commit both files with the API change that produced them.

## End-to-end acceptance

`tools/acceptance/qe-acceptance.sh` proves that metadata authored here reaches the engine. In tenant `development`, or
the tenant `TENANT` names, it:

1. authors dataset `${PREFIX}loans`, whose branch code binds the seed `branch` role, and publishes its v1;
2. authors calculation model `${PREFIX}margin` in US dollars, which holds two variables and a yield formula, live as
   they are written, and builds report `${PREFIX}margin` over it through the report editor. The report's own report
   dimension starts as a copy of the seed `MarginStatement` dimension. Two GL monthly bridge sources on the seed
   `account` role map accounts to rows as the seed report's `gl_monthly` source does, the second as a copy of the first,
   and their variables make profile `default`; a branch calculation dimension on the `branch` role, the columns, and
   rows with a header and a formula row follow. Enabling the report checks it against every report rule;
3. creates release `${PREFIX}acceptance` as a copy of the active release plus the dataset version; the report runs
   against whichever release is active, so the release pins no report;
4. validates, publishes and activates the release;
5. renames the enabled report's yield column, a live edit;
6. runs `GetCatalog`, `Execute`, `GetReportCatalog` and `ExecuteReport` on the engine, and expects the report catalog
   to show the renamed column;
7. re-activates the release that was active before.

Before you run it:

- Start the admin API on `http://localhost:5186` (or set `ADMIN`).
- Set `QueryEngineRoot`. When the engine is not running, the script starts its built host from there and stops it at
  the end.
- Run it outside any shell sandbox, because it calls localhost and StarRocks.
- Load the seed kit, which registers tenant `development`: the report reads its `bank.balance_monthly` and
  `bank.income_monthly` datasets with their bindings of the `account` and `branch` roles, its `MarginStatement` and
  `Branch` dimensions, the account mappings of report `bank.margin_statement`'s `gl_monthly` source, and its 2024 data.
- Use a new `PREFIX` for every run. Each run leaves `${PREFIX}loans`, the `${PREFIX}margin` model and report with the
  report's dimension and bridges, and `${PREFIX}acceptance` behind until you reload the seed kit, and their keys stay
  taken.
- Don't run the engine's golden cases at the same time: while the script runs, its release is the tenant's active
  release.

```bash
dotnet run --project src/Quantalytix.QueryEngine.Admin.Api --launch-profile http -- --urls http://localhost:5186 &
PREFIX=zz_try_ tools/acceptance/qe-acceptance.sh
```

The script's header describes its other modes: releasing an existing dataset, or checking the authoring path without
leaving objects. To keep its objects out of the development tenant, run `tools/CreateSandboxTenant.cs` first. It copies
the development tenant's database into tenant `sandbox`, dropping `srdb_admin_sandbox` first. Then run the script with
`TENANT=sandbox`.
