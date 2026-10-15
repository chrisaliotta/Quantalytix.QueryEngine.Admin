# Query Engine Admin API

An ASP.NET Core Web API on .NET 10 that authors the semantic metadata the Quantalytix query engine reads from StarRocks:
datasets, fields, grains, dimensions, members, mappings, bridges, bindings, measures, their versions, releases and the
tenant directory. Drafts never reach queries: the engine loads published metadata through the `vw_qe_*` views, and
dataset versions reach it only through an activated release.

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
  `Quantalytix.QueryEngine.Domain` project for the expression parser, and the tests read `schema/ddl_srdb_bi.sql`
  beside it.
- Network access to the StarRocks cluster, for every endpoint that reads or writes metadata.

## Configuration

- **`QueryEngineRoot`:** the path of the query engine checkout. MSBuild defaults to `../../Quantalytix.QueryEngine`
  from `api/`. Anywhere else, such as a git worktree, set an environment variable of the same name:
  `export QueryEngineRoot=/path/to/Quantalytix.QueryEngine/`.
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
- The live gateway round trip is skipped unless you opt in with `QE_ADMIN_LIVE_SANDBOX=true`. It writes and then deletes
  its own rows in tenant `sandbox`, so it needs the control-plane secret and the cluster.

## Run

```bash
dotnet run --project src/Quantalytix.QueryEngine.Admin.Api --launch-profile http
```

The `http` profile listens on `http://localhost:5180` in Development; add `-- --urls http://localhost:<port>` for
another port. Development hosts issue tokens for any tenant:

```bash
curl -s -X POST http://localhost:5180/development/token -H 'Content-Type: application/json' \
  -d '{"tenantId":"sandbox","systemAdmin":true}'
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

`tools/acceptance/qe-acceptance.sh` proves that metadata authored here reaches the engine. In tenant `sandbox` it:

1. authors dataset `${PREFIX}loans` and publishes its v1;
2. creates release `${PREFIX}acceptance` as a copy of the active release plus that version;
3. validates, publishes and activates the release;
4. runs `GetCatalog` and `Execute` on the engine;
5. re-activates the release that was active before.

Before you run it:

- Start the admin API on `http://localhost:5186` (or set `ADMIN`).
- Set `QueryEngineRoot`. When the engine is not running, the script starts its built host from there and stops it at
  the end.
- Run it outside any shell sandbox, because it calls localhost and StarRocks.
- Use a new `PREFIX` for every run. Published objects are immutable, so each run leaves `${PREFIX}loans` and
  `${PREFIX}acceptance` behind.

```bash
dotnet run --project src/Quantalytix.QueryEngine.Admin.Api --launch-profile http -- --urls http://localhost:5186 &
PREFIX=zz_try_ tools/acceptance/qe-acceptance.sh
```

The script's header describes its other modes: releasing an existing dataset, or checking the authoring path without
leaving objects. `tools/CreateSandboxTenant.cs` is the one-shot tool that rebuilt the sandbox database as a copy of the
development tenant's. It drops `srdb_admin_sandbox` first.
