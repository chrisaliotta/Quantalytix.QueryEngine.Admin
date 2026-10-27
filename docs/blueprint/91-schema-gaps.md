# 91 Schema gaps

Schema changes the frontend implies, against design revision 2.26.0 (metadata schema version 17). The schema is
authoritative: an adopted gap takes effect when the seed kit's `schema/` and `tools/build_kit.py` are revised. Names follow the kit's
key naming convention (schema README 2.25.0).

| Gap | Issue | Needed for | Status |
|---|---|---|---|
| [SG-01](#sg-01-aggregate-revision) | [OI-02](90-open-issues.md#oi-02) | Optimistic concurrency | **Rejected**: last write wins |
| [SG-02](#sg-02-commit-journal) | [OI-03](90-open-issues.md#oi-03) | Recovering partial commits | **Deferred**: only if partial commits are observed |
| [SG-03](#sg-03-dimension-profile-key) | [OI-16](90-open-issues.md#oi-16) | System dimension profiles | **Adopted** |
| [SG-04](#sg-04-version-timestamps) | [OI-12](90-open-issues.md#oi-12) | Created and retired dates, clone origin | **Adopted** |
| [SG-05](#sg-05-freeze-note) | [OI-15](90-open-issues.md#oi-15) | Freeze notes (dimensions, mapping sets, bridges) | **Adopted** |
| [SG-06](#sg-06-report-start-from-dimension) | [OI-19](90-open-issues.md#oi-19) | Template usage | **Rejected**: usage row dropped |
| [SG-07](#sg-07-field-materialization) | [OI-32](90-open-issues.md#oi-32) | Materialization state | **Conditional**: only if StarRocks cannot report the materialized view's refresh state per column |
| [SG-08](#sg-08-managed-source-objects) | [OI-33](90-open-issues.md#oi-33) | Managed tables | **Adopted** |
| [SG-09](#sg-09-release-activation-log) | [OI-40](90-open-issues.md#oi-40) | Activation history | **Adopted** |
| [SG-10](#sg-10-report-source-left-out-items) | [OI-51](90-open-issues.md#oi-51) | Coverage "left out" items | **Adopted** |

The adopted gaps form one schema revision: a new design revision of the seed kit, a new `metadata_schema_version`,
`tools/build_kit.py` and the seed's NDJSON updated (including the SG-03 backfill of seeded system dimensions and
`created_at` values for existing versions).

Not gaps (derivable, no column needed): bridge `owner` and report-owned pins (from `qe_report_source.bridge_id` and
`dimension_purpose`), pin state `live` / `frozen`, `supersededBy`, dependents, usage, readiness, diffs, source-object
statistics (from `information_schema`).

## SG-01 Aggregate revision

**Rejected (OI-02).** Kept for the record.

Add `revision BIGINT NOT NULL DEFAULT 0` to each aggregate root: `qe_dataset_version`, `qe_dimension`,
`qe_mapping_version`, `qe_bridge_version`, `qe_semantic_release`, `qe_calculation_model`, `qe_report`. A commit
compares and increments it. A dimension commit increments `qe_dimension.revision` (its change set spans identity,
roles and the live version).

## SG-02 Commit journal

**Deferred (OI-03).**

```sql
CREATE TABLE qe_commit_journal (
  commit_journal_id BIGINT NOT NULL,
  aggregate_kind VARCHAR(32) NOT NULL,     -- dataset_version, dimension, mapping_version, bridge_version, semantic_release, calculation_model, report
  aggregate_id BIGINT NOT NULL,
  status VARCHAR(16) NOT NULL,             -- writing, complete, compensated
  plan_json JSON NOT NULL,                 -- the row writes, in order
  created_at DATETIME NOT NULL,
  completed_at DATETIME NULL
)
```

## SG-03 Dimension profile key

`qe_dimension.dimension_profile_key VARCHAR(64) NULL`, as concept §7.3 proposes. Null for user and report dimensions;
required for `definition_origin = system` after a backfill. The profile registry itself lives in code.

## SG-04 Version timestamps

`created_at DATETIME NOT NULL` and `retired_at DATETIME NULL` on `qe_dataset_version`, `qe_dimension_version`,
`qe_mapping_version`, `qe_bridge_version`; `cloned_from_dataset_version_id BIGINT NULL` on `qe_dataset_version`
(a qualified reference under the key naming convention).

## SG-05 Freeze note

`freeze_note VARCHAR(512) NULL` on `qe_dimension_version`, `qe_mapping_version` and `qe_bridge_version`, set when a
version is frozen (OI-14 extends freezing to mapping sets and bridges).

## SG-06 Report start-from dimension

**Rejected (OI-19).** `qe_report.start_from_dimension_id BIGINT NULL` would have recorded it.

## SG-07 Field materialization

**Conditional (OI-32).** Materialization uses one StarRocks asynchronous materialized view per source object. State comes
from StarRocks; this table is added only if StarRocks cannot report a column-level state:

```sql
CREATE TABLE qe_field_materialization (
  field_id BIGINT NOT NULL,
  view_name VARCHAR(128) NOT NULL,
  column_name VARCHAR(128) NOT NULL,
  state VARCHAR(16) NOT NULL,              -- building, ready, failed
  requested_at DATETIME NOT NULL,
  refreshed_at DATETIME NULL,
  error_message VARCHAR(1024) NULL
)
```

## SG-08 Managed source objects

```sql
CREATE TABLE qe_managed_source_object (
  managed_source_object_id BIGINT NOT NULL,
  source_object VARCHAR(128) NOT NULL,     -- unique
  source_object_kind VARCHAR(32) NOT NULL, -- table
  created_by_dataset_id BIGINT NOT NULL,
  created_at DATETIME NOT NULL
)
```

## SG-09 Release activation log

```sql
CREATE TABLE qe_release_activation (
  release_activation_id BIGINT NOT NULL,
  semantic_release_id BIGINT NOT NULL,
  previous_semantic_release_id BIGINT NULL,
  activated_at DATETIME NOT NULL,
  activated_by VARCHAR(256) NOT NULL       -- token subject
)
```

## SG-10 Report source left-out items

```sql
CREATE TABLE qe_report_source_left_out (
  report_source_id BIGINT NOT NULL,
  member_key VARCHAR(256) NOT NULL         -- an item of the source's role dimension excluded on purpose
)
```

Read only by coverage diagnostics; the engine never reads it.
