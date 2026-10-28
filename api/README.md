# Query Engine Admin API

An ASP.NET Core Web API on .NET 10 that authors the semantic metadata the Quantalytix query engine reads from StarRocks:
datasets with their fields, grains, bindings and measures; dimensions with their roles, levels, members and range bands;
mapping sets, bridges, semantic releases and assumption sets; calculation models; reports; and the control plane's tenant
directory. The engine reads the metadata only through the tenant database's `vw_qe_*` views. The API never calls the engine.

The API is contract-first. The hand-authored OpenAPI contract in [`openapi/contract/`](openapi/contract/README.md) is the
source of truth, the C# request and response types are written by hand to match it, and a conformance test fails the build
when the two differ. The `web2` application is the API's client; the older `web` application was removed with the move to
the change-set API.

| Project | Holds |
|---|---|
| `src/Quantalytix.QueryEngine.Admin.WebApi` | Controllers, authentication and authorization, the error shape, the route existence filter, JSON conventions, OpenAPI generation, feature registration |
| `src/Quantalytix.QueryEngine.Admin.Application` | Read models, change-set aggregates and their operations, snapshot rules, lifecycle commands, the version workflow, and the interfaces Infrastructure implements |
| `src/Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks` | Table gateways (Dapper over MySqlConnector), id generation, source-object introspection, managed-table and materialized-view DDL, tenant connection checks |
| `tests/Quantalytix.QueryEngine.Admin.Tests` | xUnit v3 tests on Microsoft.Testing.Platform, including the contract conformance test |

The [architecture pages](docs/architecture/README.md) describe the layers, the request pipeline, the change-set pipeline,
the version lifecycle, each feature module and the main request flows. The design decisions behind them are in the
[blueprint](../docs/blueprint/README.md).

## Prerequisites

- The .NET 10 SDK, and Node with pnpm for the contract tooling.
- A checkout of the query engine, `Quantalytix.QueryEngine`. The Application project references its
  `Quantalytix.QueryEngine.Domain` project for the expression parser.
- The seed kit, `database/srdb_ebm`, at design revision 2.27.0. The tests read its schema build script,
  `ddl_srdb_bi_v2_27.sql`.
- A tenant database at metadata schema version 18 with exactly one active assumption set, as the seed kit's build script
  creates it. Tenant connection verification refuses any other schema version and any other number of active sets.
- Network access to the StarRocks cluster for every endpoint that reads or writes metadata.

## Configuration

- **`QueryEngineRoot` and `SeedKitRoot`:** MSBuild properties for the query engine checkout and the seed kit.
  `Directory.Build.props` finds both next to the main checkout of this repository, also from a git worktree. Set an
  environment variable of the same name (an absolute path) to use another checkout.
- **`ConnectionStrings:ControlPlane`:** the `query_control` database, whose `qe_tenant_directory` routes each tenant to its
  own database. Keep it in user secrets, never in `appsettings*.json`:

  ```bash
  dotnet user-secrets set ConnectionStrings:ControlPlane "<connection string>" --project src/Quantalytix.QueryEngine.Admin.WebApi
  ```

- **`StarRocks`:** connection options such as `CommandTimeoutSeconds`.
- **`Cors:AllowedOrigins`:** the browser origins that may call the API; none by default. `appsettings.Development.json`
  allows the `web2` dev server at `http://localhost:5174`. The `web2` dev server also
  proxies `/api` and `/development` to `http://localhost:5180`, so a browser on its origin does not need CORS.
- **`Authentication:Schemes:Bearer`:** the standard ASP.NET Core JWT bearer settings through which a production identity
  provider plugs in. A token carries `sub`, one `tenant_id` claim (the claim type is `AdminAuthentication:TenantClaimType`),
  and `role: SystemAdmin` for a caller who may reach every tenant. Development also trusts `dotnet user-jwts` tokens.

## Run

From `api/`:

```bash
dotnet run --project src/Quantalytix.QueryEngine.Admin.WebApi --launch-profile http
```

The `http` profile listens on `http://localhost:5180` in Development; add `-- --urls http://localhost:<port>` for another
port. Development hosts issue tokens for any tenant:

```bash
curl -s -X POST http://localhost:5180/development/token -H 'Content-Type: application/json' \
  -d '{"tenantId":"development","systemAdmin":true}'
```

The response's `accessToken` is a bearer token valid for eight hours. The signing key lives in memory, so tokens stop
working when the host restarts. `POST /development/token` exists only in Development and is not in the contract.
`GET /health` answers without a token.

To run `web2` against the API, start the API as above, then run `pnpm dev` in `web2/` with `VITE_API_MODE=http`
(`VITE_API_TENANT` selects the tenant, `development` by default). Without it, `web2` uses its in-memory mock adapters.

## Contract-first workflow

An API change starts in the contract:

1. Edit the module file under `openapi/contract/` (`datasets.yaml`, `dimensions.yaml`, `mappings.yaml`, `releases.yaml`,
   `models.yaml`, `reports.yaml`, `tenant.yaml`, or the shared `common.yaml`). The
   [contract README](openapi/contract/README.md) describes the files and the authoring rules.
2. From the repository root, lint and bundle it. This writes `openapi/contract/bundled.yaml`:

   ```bash
   pnpm contract:lint && pnpm contract:bundle
   ```

3. Change the C# DTOs and controllers to match, then build from `api/`. The build regenerates `openapi/admin-api.json`
   from the C# code and copies `bundled.yaml` and `admin-api.json` into the test output.
4. Run the tests. `ContractConformanceTests` fails on any difference between the bundled contract and the generated
   document.
5. Regenerate the `web2` types from `web2/`, which bundles the contract and writes `web2/src/lib/api/schema.d.ts`:

   ```bash
   pnpm contract:types
   ```

`bundled.yaml`, `admin-api.json` and `schema.d.ts` are committed with the change that produced them. Bundle before you
build: the test project copies `bundled.yaml` at build time, so a build after a stale bundle compares a stale contract.

## Build

From `api/`:

```bash
MSBUILDDISABLENODEREUSE=1 dotnet build Quantalytix.QueryEngine.Admin.slnx -maxcpucount:4
```

Warnings are errors, and every type and member needs an XML `<summary>`.

## Test

From `api/`, after a build:

```bash
dotnet test --solution Quantalytix.QueryEngine.Admin.slnx --no-build
```

- Run it from `api/`: `global.json` selects Microsoft.Testing.Platform there; elsewhere `dotnet test` falls back to VSTest.
- Do not pass `-maxcpucount` to `dotnet test`. The platform forwards it to the test application, which then runs no tests.
- To run one class, use the test executable from the test project's directory:
  `./bin/Debug/net10.0/Quantalytix.QueryEngine.Admin.Tests -class <full class name>`.
- The live tests are skipped unless `QE_ADMIN_LIVE_GATEWAY=true`. They run against tenant `development` through the
  control-plane secret (or `ConnectionStrings__ControlPlane`), and write and then delete their own rows.

`web2` has its own checks (`pnpm build`, `pnpm lint`, `pnpm test` in `web2/`). Its parity suites run on the mock adapters by
default and also on the HTTP adapters against a running API with `QE_LIVE_API=1`.

## Live verification

[Live verification](../docs/blueprint/04-live-verification.md) lists the checks to run against a live tenant after a
change. `tools/acceptance/qe-acceptance.sh` proves end to end that metadata authored through the API reaches the engine: it
authors a dataset, a calculation model and a report, releases and activates the dataset version, and runs catalog and query
requests on the engine. Its header documents its modes, environment variables and the objects it leaves behind. Run it
outside any shell sandbox with the API on `http://localhost:5186` (or set `ADMIN`), `QueryEngineRoot` set, and a new
`PREFIX` per run:

```bash
dotnet run --project src/Quantalytix.QueryEngine.Admin.WebApi --launch-profile http -- --urls http://localhost:5186 &
PREFIX=zz_try_ tools/acceptance/qe-acceptance.sh
```

To keep its objects out of the development tenant, run `tools/CreateSandboxTenant.cs` first, which copies the development
tenant's database into tenant `sandbox`, then run the script with `TENANT=sandbox`.
