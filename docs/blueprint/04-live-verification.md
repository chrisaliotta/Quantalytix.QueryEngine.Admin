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

## 4. Dataset retirement and managed-table drop

Two follow-ups decided after integration ([11 Datasets](11-datasets.md) D13 and D14) are covered by in-memory tests and the
mock parity suite. On a live tenant, first run the datasets parity suite over the HTTP adapter:

```bash
cd web2
QE_LIVE_API=1 pnpm vitest run src/lib/datasets
```

It refuses retiring the version the active release pins (so the tenant needs an active release that pins a dataset
version), and creates a dataset over a new managed table, deletes it, and expects the table to leave the source object
list. Then check StarRocks itself, with `PREFIX` a new prefix per run:

1. **Retire guard.** `POST dataset-versions/{id}/retire` on the version the active release pins answers 422 with one
   violation `release_pins_retired_version`, and `GET dataset-versions/{id}` still reads `published`.
2. **Table dropped.** Create a dataset with `mode: 'new'`, table `${PREFIX}drop_t`, and one `json` column, then
   `SHOW TABLES LIKE '${PREFIX}drop_t'` lists it and `qe_managed_source_object` has its row. Delete the dataset (204).
   `SHOW TABLES LIKE '${PREFIX}drop_t'` is empty, `SELECT * FROM qe_managed_source_object WHERE source_object =
   '${PREFIX}drop_t'` is empty, and `SHOW CATALOG RECYCLE BIN` lists the table.
3. **Materialized view dropped.** Repeat 2, but before deleting add a JSON path field over the column, commit it, and
   materialize it, so `qe_mv_${PREFIX}drop_t` appears in `information_schema.materialized_views`. After the delete (204)
   neither the view nor the table exists, and the API log shows no error from the drop order (view first, then table).
4. **Shared table kept.** Create a dataset over a new managed table, then a second dataset with `mode: 'existing'` over the
   same table. Deleting the first keeps the table and its registry row; deleting the second then drops both.

Passing: each step answers as described, and no `qe_` metadata row names a dropped table.

## 5. Ordinary-table materialized view after dataset delete

Decided 2026-10-27 by the user ([11 Datasets](11-datasets.md#json-field-materialization-agreed-oi-32),
[OI-32](90-open-issues.md#oi-32)): deleting a dataset over an ordinary (unmanaged) table neither redefines nor drops the
table's materialized view, so the view keeps the deleted fields' columns until the next materialize or dematerialize on
that table redefines it from the fields that remain. Check that cleanup, with `${TABLE}` an existing ordinary table with
a `json` column:

1. Create two datasets with `mode: 'existing'` over `${TABLE}`, add a JSON path field over the column to each (different
   paths, so they get different view columns), commit, and materialize both. `qe_mv_${TABLE}` in `information_schema.materialized_views` defines both columns.
2. Delete the first dataset (204). `${TABLE}` and `qe_mv_${TABLE}` remain, and the view still defines the deleted field's
   column.
3. Materialize a second field of the remaining dataset (or dematerialize its field). The view is redefined without the
   deleted field's column, and no `qe_field.materialized_column` names it.

Passing: each step answers as described, and the API log shows no error from redefining the view.
