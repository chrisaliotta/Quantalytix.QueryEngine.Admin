# 04 Live verification

The refactor's phases are complete on `feature/web2-refactor`, and every check that runs without a cluster passes: the API
build and tests, the contract lint and bundle, the exact contract conformance test, and `web2`'s build, lint and tests on
the mock adapters. What follows needs a live StarRocks tenant and has not been run yet. Each item says how to run it and
what passing looks like; run them in order, because later items assume the earlier ones.

## Prerequisites

- The schema kit at design revision 2.27.0 (metadata schema version 18) applied to the cluster with `sql-run`, and the seed
  kit loaded into tenant `development` (gate G2 of the refactor plan). The API refuses a tenant at another schema version.
- The admin API running in Development (`dotnet run` in `api/src/Quantalytix.QueryEngine.Admin.WebApi`, see
  [the API README](../../api/README.md)) with the control-plane connection string in its user secrets.

## 1. Live test suites

```bash
cd api
QE_ADMIN_LIVE_GATEWAY=true dotnet test --solution Quantalytix.QueryEngine.Admin.slnx
```

Passing: the 3 tests skipped without the variable run and pass: the StarRocks table gateway round trip, reading the seeded
reports, and round-tripping a report through tenant `development`.

## 2. Acceptance script

[`api/tools/acceptance/qe-acceptance.sh`](../../api/tools/acceptance/qe-acceptance.sh) moved to the change-set endpoints and
only passed `bash -n`. Run it outside the shell sandbox with `QueryEngineRoot` set to the engine checkout, in its three modes,
each with a new `PREFIX`:

```bash
PREFIX=zz_p6a_ AUTHOR_CHECK_ONLY=1 ./qe-acceptance.sh
PREFIX=zz_p6b_ ./qe-acceptance.sh
PREFIX=zz_p6c_ DATASET_KEY=zz_p6b_loans MEASURES="balance avgRate balanceRate endingBalance" ./qe-acceptance.sh
```

Passing: every call answers the status the script expects, the authoring-only mode ends with `PASS (authoring only)`, and the
full modes end with the engine checks passing and the original release active again. These calls are the first to run
against StarRocks, so they verify behavior that the in-memory tests can't check:

- the write plan's statement order, given that StarRocks has no transactions (deletes deepest-first, then updates and
  inserts shallowest-first);
- `tmp:` ids that later operations in the same change set reference;
- report bridges created and written through the report change set;
- the semantic revision advancing so the engine reloads after a live edit.

## 3. web2 against the live API

Copy `web2/.env.example` to `web2/.env.local` (it sets `VITE_API_MODE=http` and `VITE_API_TENANT=development`), run
`pnpm dev` in `web2/` (`http://localhost:5174`, an allowed CORS origin in Development), and walk every module's screens:

| Module | Check |
|---|---|
| Tenant and catalog | The overview counts and drafts match the tenant; switching tenants as a system administrator works. |
| Datasets | Create a dataset over an existing table; edit fields, grains, bindings and measures in one change set with preview and commit; validate, publish, reopen, delete a draft; source object statistics and distinct values load. |
| Dimensions | Edit the live version's levels, members and range bands; freeze; a blocked delete shows its reasons. |
| Mapping sets and bridges | Edit entries and mappings on the live version; freeze; report-owned bridges read as owned. |
| Releases and assumption sets | Edit a draft manifest, validate, publish, activate and restore the original; capture an assumption set. |
| Calculation models | Edit variables and measures; the formula check reports errors in place. |
| Reports | Edit sources and rules, profiles, columns and rows in one change set; coverage lists unmapped items, including a direct source's values; enable and disable. |

Passing: every commit answers 200 and the screen shows the stored state; every refusal shows the API's problem on the
item it targets; no console errors.

## Open items

These came up during integration and are outside the refactor's scope. Each needs a decision or a follow-up change:

- **Dataset retirement has no rule.** `DatasetCommands.RetireAsync` promises to retire only versions that the active release
  doesn't pin, but no `IRetirementRule` covers dataset versions (releases have `ReleaseNotActiveRule`), so a pinned version
  can be retired. It needs a dataset retirement rule and a test.
- **Managed tables on dataset delete.** Deleting a dataset created over a new managed table leaves the table and its
  `qe_managed_source_object` row in place. Decide whether the delete drops them ([OI-33](90-open-issues.md#oi-33)).
