# 90 Open issues

Every disagreement or uncertainty of intent found while writing the blueprint. Each entry lists the sources that
disagreed, the options, the recommendation, and the decision (all decided 2026-10-26 and applied across the blueprint). Decisions are recorded here and in the [README decision log](README.md#decision-log).

Path shorthand: `web2/` = `Quantalytix.QueryEngine.Admin/web2/src/`, `api/` = `Quantalytix.QueryEngine.Admin/api/`,
`concept` = [dimensions concept](../concepts/dimensions.md), `dim brief` =
[dimension manager brief](../requirements/dimension-manager-prototype.md), `map brief` =
[mappings and bridges brief](../requirements/mappings-and-bridges-prototype.md), `map rev2` =
[`web2/docs/mappings-and-bridges.md`](../../web2/docs/mappings-and-bridges.md), `rel brief` =
[releases brief](../requirements/releases-and-assumption-sets-prototype.md), `schema doc` =
`database/srdb_ebm/docs/schema/Query_Engine_Database_Schema.md`.

## Index

| Area | Issues |
|---|---|
| Cross-cutting | [01](#oi-01) [02](#oi-02) [03](#oi-03) [04](#oi-04) [05](#oi-05) [06](#oi-06) [07](#oi-07) [08](#oi-08) [09](#oi-09) [10](#oi-10) [11](#oi-11) [12](#oi-12) [13](#oi-13) [57](#oi-57) |
| Dimensions | [14](#oi-14) [15](#oi-15) [16](#oi-16) [17](#oi-17) [18](#oi-18) [19](#oi-19) [20](#oi-20) [21](#oi-21) [22](#oi-22) [23](#oi-23) [24](#oi-24) |
| Mapping sets and bridges | [25](#oi-25) [26](#oi-26) [27](#oi-27) [28](#oi-28) [29](#oi-29) [30](#oi-30) [31](#oi-31) |
| Datasets | [32](#oi-32) [33](#oi-33) [34](#oi-34) [35](#oi-35) [36](#oi-36) |
| Releases and assumption sets | [37](#oi-37) [38](#oi-38) [39](#oi-39) [40](#oi-40) [41](#oi-41) |
| Calculation models | [42](#oi-42) [43](#oi-43) [44](#oi-44) [45](#oi-45) |
| Reports | [46](#oi-46) [47](#oi-47) [48](#oi-48) [49](#oi-49) [50](#oi-50) [51](#oi-51) [52](#oi-52) [53](#oi-53) [54](#oi-54) [55](#oi-55) [56](#oi-56) |

---

## Cross-cutting

<a id="oi-01"></a>
### OI-01 How edits travel to the server

- **Statement:** `web2/lib/changes.ts` states one rule: every edit stays pending until the user saves the batch. The
  modules implement it four ways: datasets and dimensions stage through `ChangeSetPort`; mapping and bridge editors
  write immediately; the release manifest lives in component state until `saveManifest`; models keep a draft per model;
  reports keep a whole-document copy. The current API has fine-grained per-entity writes and no batch endpoint.
- **Sources:** `web2/lib/changes.ts`; `web2/lib/dimensions/client.tsx` 21–36; `web2/lib/datasets/client.tsx` 21–46;
  `web2/lib/model/store.tsx` 62–77; `web2/lib/report/store.tsx` 188–195; api inventory §5.
- **Options:**
  1. Client-held change set; per-edit server preview; one commit request per aggregate.
  2. Server-side draft workspaces (`changesets` resource) that edits write into, committed or discarded later.
  3. Keep immediate fine-grained writes; drop the universal rule.
- **Recommendation:** option 1 ([01](01-api-conventions.md#change-set-protocol-agreed-oi-01)). It keeps undo local, matches the
  in-memory gate the API already uses for reports and models, and needs no new tables beyond OI-02 and OI-03.
- **Status:** Decided 2026-10-26. Option 1: client-held change set, server preview per edit, one commit per aggregate.

<a id="oi-02"></a>
### OI-02 Optimistic concurrency

- **Statement:** Nothing stops two authors overwriting each other; with batched commits the loss is larger. No table has
  a revision or `updated_at` column per aggregate (only `qe_database_info.updated_at` and the tenant-wide
  `semantic_revision`).
- **Sources:** api inventory §1 ("no concurrency control"); schema `01_tables`.
- **Options:**
  1. Per-aggregate revision column ([SG-01](91-schema-gaps.md#sg-01-aggregate-revision)).
  2. Use tenant `semantic_revision` as the token (any engine-visible change anywhere makes every open editor stale; drafts
     never advance it, so drafts get no protection).
  3. Hash of the aggregate's content computed on read and on commit (no schema change; costs a full read per commit).
  4. Accept last-write-wins.
- **Recommendation:** option 1; option 3 as a stopgap if the schema cannot change in this refactor.
- **Status:** Decided 2026-10-26 (differs from the recommendation). Option 4: accept last-write-wins; no revision token, no `baseRevision`.

<a id="oi-03"></a>
### OI-03 Atomic commits without transactions

- **Statement:** StarRocks rejects `UPDATE`/`DELETE` inside transactions and the gateway writes statement by statement.
  A batch commit that fails midway leaves a partial change.
- **Sources:** `api/docs/architecture/02-request-pipeline.md`; api inventory §5.
- **Options:**
  1. Commit journal table; write the journal first, then rows, then mark complete; replay or compensate incomplete
     journals ([SG-02](91-schema-gaps.md#sg-02-commit-journal)).
  2. Order writes so a partial write is harmless (deletes children-first, inserts parents-first) and accept the residue.
  3. Shadow copy: write a new version or aggregate copy and switch a pointer.
- **Recommendation:** option 2 now, option 1 if partial commits are observed in practice.
- **Status:** Decided 2026-10-26. Option 2: order writes so a partial write is harmless; commit journal only if partial commits are observed.

<a id="oi-04"></a>
### OI-04 Id encoding

- **Statement:** The API serializes BIGINT ids as JSON numbers with strict number handling; `web2` types every id as
  `string`.
- **Sources:** api inventory §1 "JSON"; every `web2/lib/*/contract.ts`.
- **Options:** strings in JSON; numbers in JSON with adapters converting.
- **Recommendation:** strings. Ids are opaque to the client, and ids below 2^53 parse either way.
- **Status:** Decided 2026-10-26. Strings in JSON.

<a id="oi-05"></a>
### OI-05 Who validates

- **Statement:** `web2/lib/datasets/contract.ts` says the adapter owns every validation decision and the UI never checks
  input. Other modules run real rules in the browser: `web2/lib/mappings/rules.ts` (entry and bridge issues, allocation
  summaries), `web2/lib/model/rules.ts` (formula parsing, language features, attribution checks) and the report's
  `coverage.ts`, `profiles.ts`, `scenarios.ts` (readiness, coverage, profile resolution, enable gate). The engine
  trusts the metadata.
- **Options:**
  1. Server authoritative; browser rules retired as decision makers; server returns diagnostics in read models and
     previews.
  2. Shared rule code (compiled for both), as `mappings/rules.ts` claims for the mock.
  3. Client rules for UX, server rules for enforcement, kept in sync by hand.
- **Recommendation:** option 1 ([00](00-architecture.md#where-validation-lives-agreed-oi-05)). The report rules
  in the browser already disagree with the model (they walk the report's stale copy of the model).
- **Status:** Decided 2026-10-26. Option 1: server authoritative; browser rule code retired as decision maker.

<a id="oi-06"></a>
### OI-06 One problem shape, and 404 for unknown ids

- **Statement:** The API answers with three problem shapes (`ProblemDetails`, `ValidationProblemDetails`,
  `RuleViolationProblemDetails`) and returns 500 for an id that names no row. `web2` declares three incompatible
  `ProblemDetails` types (dimensions has `status`; releases does not; datasets adds `dependents`).
- **Sources:** api inventory §2; `web2/lib/dimensions/contract.ts` 210–218; `web2/lib/releases/contract.ts` 107–115;
  `web2/lib/datasets/contract.ts` 373–381.
- **Recommendation:** the single `Problem` of [01](01-api-conventions.md#problem-body-agreed-oi-06) and a 404 from a
  shared existence check on every route id.
- **Status:** Decided 2026-10-26. Single `Problem` shape; 404 from a shared existence check.

<a id="oi-07"></a>
### OI-07 Delete checks before delete

- **Statement:** `web2` asks whether something can be deleted before offering Delete and lists reasons
  (`DeleteCheck { allowed, message, reasons: string[] }`). The API only refuses with a 409 whose `detail` is a sentence.
- **Sources:** `web2/lib/dimensions/contract.ts` 194–206; `web2/lib/mappings/contract.ts` `checkDelete*`; api
  `DeleteBlockers.cs`.
- **Options:** `GET .../delete-check` per resource; or a `?dryRun=true` on `DELETE`.
- **Recommendation:** `GET .../delete-check` returning `reasons: Violation[]` (structured, linkable); `DELETE` returns
  the same violations on 409.
- **Status:** Decided 2026-10-26. `GET .../delete-check` with structured `Violation` reasons; `DELETE` returns the same on 409.

<a id="oi-08"></a>
### OI-08 Composite read models versus fine-grained reads

- **Statement:** `web2` read models join several tables (a dimension list item with roles, version summaries, profile,
  member count; a report document with everything). The API mostly returns one table per endpoint, and the old client
  made many calls per screen.
- **Recommendation:** composite read models per screen, as each module file defines; fine-grained list endpoints that
  no screen uses are removed.
- **Status:** Decided 2026-10-26. Composite read models per screen; remove fine-grained lists no screen uses.

<a id="oi-09"></a>
### OI-09 Permissions: system admin versus report author

- **Statement:** `web2/lib/session/viewer.ts` has `system_admin` and `report_author`, used only to hide the hidden
  dimensions tab. The API knows `SystemAdmin` (cross-tenant) and tenant access. No brief defines permissions (all put
  auth out of scope). Unanswered: who may define system dimensions, activate releases, capture or delete assumption
  sets, see report-owned dimensions, delete reports.
- **Options:**
  1. Two tenant roles: `admin` (everything) and `author` (no activation, no system dimension definition, no capture or
     set delete, no hidden list); claim `role: TenantAdmin` added.
  2. Everything open to any tenant user; only `SystemAdmin` (cross-tenant) is special.
  3. Fine-grained permissions per action.
- **Recommendation:** option 1, with the list confirmed by product.
- **Status:** Decided 2026-10-26 (differs from the recommendation). Option 2: everything open to any tenant user; only cross-tenant `SystemAdmin` is special. `web2`'s `report_author` role is removed.

<a id="oi-10"></a>
### OI-10 Field naming

- **Statement:** `datasets/contract.ts` uses `datasetKey`, `fieldKey`, `grainKey`, `measureKey`, `dimensionKey`;
  `dimensions` and `mappings` use `key`; the API uses `dimensionKey`, `dimensionRoleId`, `dimensionMemberId`; model and
  report stores use `name`, `language`, `method`.
- **Recommendation:** own identifiers `id`/`key`, references `<entity>Id`/`<entity>Key`, display text `displayName`
  ([01](01-api-conventions.md#ids-keys-and-names)).
- **Status:** Decided 2026-10-26. `id`/`key` for own identifiers, `<entity>Id`/`<entity>Key` for references, `displayName` for display text.

<a id="oi-11"></a>
### OI-11 Vocabularies and enumerations

- **Statement:** `datasets/contract.ts` requires server-supplied vocabularies; other modules hard-code unions and labels.
  The datasets contract defines `DimensionKind = 'standard' | 'range' | 'time'`; the schema and `dimensions/contract.ts`
  have `categorical | hierarchical | range | time`. Violation codes are camelCase in `dimensions` and snake_case
  elsewhere.
- **Recommendation:** one `GET /api/vocabularies` for every enumeration; the schema's values; the API's snake_case rule
  codes everywhere.
- **Status:** Decided 2026-10-26. One `GET /api/vocabularies`; schema values; snake_case rule codes everywhere.

<a id="oi-12"></a>
### OI-12 Version timestamps that are not stored

- **Statement:** `web2` shows `createdAt`, `retiredAt` and `supersededBy` for dimension, mapping and bridge versions,
  and `createdAt`, `clonedFromVersionId` for dataset versions; the dim brief shows "created and published dates" and
  "Retired on {date}, superseded by v{n}". The schema stores only `published_at` (and `created_at` on releases and
  assumption sets).
- **Options:** add `created_at`, `retired_at` (and `cloned_from_version_id`) columns
  ([SG-04](91-schema-gaps.md#sg-04-version-timestamps)); derive `supersededBy` (next version number) and approximate
  `retiredAt` with the successor's `published_at`; drop from the UI.
- **Recommendation:** derive `supersededBy` and `retiredAt` now; add `created_at` in the next schema revision.
- **Status:** Decided 2026-10-26 (differs from the recommendation). Add `created_at`, `retired_at` and `cloned_from_dataset_version_id` columns now (SG-04).

<a id="oi-13"></a>
### OI-13 One tenant catalog

- **Statement:** `ReleasesApi.getCatalog`, `DatasetsApi.getTenantCatalog`, `MappingsApi.listDimensionRefs`,
  `DimensionsApi.listAllDimensions` and the API's 27-kind `lookups/{kind}` overlap.
- **Recommendation:** `GET catalog` plus `published-members` ([10](10-tenant-session-catalog.md)); remove `lookups`
  unless a screen needs a kind the catalog lacks.
- **Status:** Decided 2026-10-26. `GET catalog` plus `published-members`; remove `lookups` unless a screen needs a kind the catalog lacks.

<a id="oi-57"></a>
### OI-57 Saving several editors at once is not atomic

- **Statement:** The navigation guard's "Save and leave" saves every dirty scope in sequence
  (`web2/components/shell/changes.tsx` `resolve`), but `lib/changes.ts` promises atomic commits.
- **Options:** accept per-aggregate atomicity and stop on the first failure; or a cross-aggregate commit endpoint.
- **Recommendation:** accept per-aggregate atomicity; the guard stops on the first failure and keeps the user on the
  page with the failing editor's problem shown.
- **Status:** Decided 2026-10-26. Accept per-aggregate atomicity; the guard stops at the first failure and keeps the user on the page.

## Dimensions

<a id="oi-14"></a>
### OI-14 Live-and-frozen versus draft-and-publish

- **Statement:** `web2/lib/dimensions/contract.ts` 364–375: one live version edited in place plus frozen snapshots, "no
  drafts"; `freezeVersion` clones the live version into a new live one. The concept doc §6.4, the dim brief §6.2 (draft
  / validated / published bar, "only one draft") and the API (draft → validated → published, publish retires) keep
  drafts. The mappings prototype in `web2` keeps drafts too, so dimensions and mappings now behave differently.
- **Options:**
  1. `web2` model on the living lifecycle: v1 created published; freeze = clone to n+1 published and retire n
     ([12](12-dimensions.md#the-version-model-agreed-oi-14)); mappings keep drafts.
  2. `web2` model for dimensions, mappings and bridges alike.
  3. Draft model everywhere (change the `web2` dimension screens back to the brief).
- **Recommendation:** option 1. It is what users of `web2` see, needs no schema change except the note (OI-15), and
  mapping sets genuinely need drafts for full re-mappings.
- **Status:** Decided 2026-10-26 (differs from the recommendation). Option 2: live-and-frozen model for dimensions, mapping sets and bridges alike; no drafts for living kinds.

<a id="oi-15"></a>
### OI-15 Freeze note

- **Statement:** `freezeVersion(versionId, note)` records why a version was frozen ("FY26 close"); no column holds it.
- **Options:** `qe_dimension_version.freeze_note` ([SG-05](91-schema-gaps.md#sg-05-freeze-note)); drop the note;
  steer users to capture an assumption set (which has a name and description) instead.
- **Recommendation:** add the column; freezing is often paired with a capture, but not always.
- **Status:** Decided 2026-10-26. Add `qe_dimension_version.freeze_note` (SG-05).

<a id="oi-16"></a>
### OI-16 Dimension profiles, edit policy and visibility

- **Statement:** `web2` and the dim brief model system dimensions through profiles (`editPolicy` open / extend / locked,
  `visibility` manager / module / hidden, required roles and members, member property schema, cardinality, owner) and
  offer `defineSystemDimension`. None of it is in the schema; the concept doc §7.3 proposes
  `qe_dimension.dimension_profile_key` and a code registry. The two disagree on examples (concept: Branch `open`, owner
  profitability; brief: Branch `extend`, owner Core, Product `locked`). Today's seed has `system` dimensions with no
  profile, against "a system dimension always has a profile".
- **Options:** code registry + `dimension_profile_key` column ([SG-03](91-schema-gaps.md#sg-03-dimension-profile-key));
  registry stored as tenant data; defer profiles (system dimensions read-only by origin).
- **Recommendation:** code registry + column, a backfill mapping today's seeded system dimensions to profiles, and the
  examples settled by product.
- **Status:** Decided 2026-10-26. Code registry plus `qe_dimension.dimension_profile_key` (SG-03), with a backfill of seeded system dimensions.

<a id="oi-17"></a>
### OI-17 Deleting validated versions, retiring by hand, reopening

- **Statement:** Concept §6.4/§6.6: only drafts are deleted; a published version is never retired by hand. Dim brief
  §6.2 offers "validated: Publish, Delete". Concept §6.1 lists `retired` as an accepted status value. Releases define
  validated → draft; living kinds do not say. `web2` mappings has `reopen*Version`.
- **Recommendation:** delete drafts and validated versions (a validated version was never live); never retire living
  versions by hand; allow validated → draft (`reopen`) for every kind with drafts.
- **Status:** Decided 2026-10-26. Delete drafts and validated versions; never retire living versions by hand; validated -> draft (reopen) for every kind with drafts (datasets and releases, given OI-14).

<a id="oi-18"></a>
### OI-18 Member property schema format

- **Statement:** Concept §7.3 proposes "a JSON schema"; the dim brief and `web2` use `PropertyField[]` with UI types
  (`image`, `geo`, `select`, `url`). User dimensions get a free key/value editor. Members store untyped
  `properties JSON`.
- **Recommendation:** `PropertyField[]` in the profile registry (it is what the UI renders); the server validates
  `properties` against it for system dimensions only.
- **Status:** Decided 2026-10-26. `PropertyField[]` in the profile registry; server validates `properties` for system dimensions only.

<a id="oi-19"></a>
### OI-19 "Used as a template" usage

- **Statement:** The dim brief's Usage tab lists "report rows when a report used it as a template" (the report's
  "start from" dimension). Nothing records which dimension a report started from.
- **Options:** `qe_report.start_from_dimension_id` ([SG-06](91-schema-gaps.md#sg-06-report-start-from-dimension));
  drop the usage row.
- **Recommendation:** drop it; the copy is independent once made, and the usage would mislead.
- **Status:** Decided 2026-10-26. Drop the usage row; no SG-06.

<a id="oi-20"></a>
### OI-20 Scope of the scenario-role rule

- **Statement:** Schema README 2.24.0 and concept §6.5: a role cannot be added while a report names the dimension's only
  role as a scenario. The dim brief refuses Add role on the Scenario profile unconditionally.
- **Recommendation:** the schema's rule (conditional), plus the profile's `requiredRoleKeys` and a profile flag
  `singleRole` for the Scenario profile if product wants the unconditional rule.
- **Status:** Decided 2026-10-26. Schema's conditional rule, plus an optional profile flag `singleRole`.

<a id="oi-21"></a>
### OI-21 Member move, import and clear

- **Statement:** `web2` has `moveMember` (returns the previous position for undo), `importMembers` (merge or replace,
  failing when a needed member would go) and `clearMembers`. The API has only `PUT members/order`.
- **Recommendation:** change-set operations for all three ([12](12-dimensions.md#change-set-operations)); import parsing
  stays in the browser.
- **Status:** Decided 2026-10-26. Change-set operations for move, import and clear; parsing stays in the browser.

<a id="oi-22"></a>
### OI-22 Listing report-owned dimensions

- **Statement:** Concept §1: report dimensions are excluded from lists and catalogs but visible in the "start from"
  picker and active set pins. `web2` adds a Hidden tab for system admins (`listHiddenDimensions`, 403 otherwise).
- **Recommendation:** `GET dimensions?purpose=report` restricted by OI-09; links from it open the owning report.
- **Status:** Decided 2026-10-26 (differs from the recommendation). Nobody lists report-owned dimensions; they are reachable only from their report. `listHiddenDimensions` and the Hidden tab are removed.

<a id="oi-23"></a>
### OI-23 One draft at a time

- **Statement:** The dim brief, map brief and map rev2 allow one draft per identity; the schema and API do not enforce
  it. `web2` datasets do not state it.
- **Recommendation:** enforce one open (draft or validated) version per identity for mapping sets, bridges and dataset
  versions; 409 on a second `createDraft`.
- **Status:** Decided 2026-10-26. Enforce one open (draft or validated) dataset version per dataset; releases unrestricted.

<a id="oi-24"></a>
### OI-24 How a locked system dimension changes

- **Statement:** Under `locked`, nothing is editable and no new version can be made (dim brief §6.1, §6.2). The owning
  module must still change it.
- **Options:** a provisioning path outside the Admin API; an `admin`-only override; the profile owner's own API.
- **Recommendation:** provisioning outside this API; document it with the profile registry.
- **Status:** Decided 2026-10-26. Provisioning path outside the Admin API, documented with the profile registry.

## Mapping sets and bridges

<a id="oi-25"></a>
### OI-25 Where analytical bridges are edited

- **Statement:** Concept §8, dim brief §6.3 and map brief A4: a bridge's home is its target dimension; the source side
  is read-only. Map rev2 and `web2`: editable from either endpoint (Outgoing and Incoming sections).
- **Recommendation:** either endpoint (rev2, what `web2` implements); the API is indifferent because writes address the
  bridge version.
- **Status:** Decided 2026-10-26. Editable from either endpoint (Outgoing and Incoming).

<a id="oi-26"></a>
### OI-26 Bridge settings on a published version

- **Statement:** Map brief B5.6: cardinality and allocation editable on a draft or the published version. Map rev2
  B5.5: draft only. The living rule says the published version is edited in place.
- **Recommendation:** draft only. Changing allocation semantics under live queries is a different bridge, so it
  should go through a new version and publish.
- **Status:** Decided 2026-10-26 (differs from the recommendation). Cardinality and allocation editable in place on the live version like any other edit.

<a id="oi-27"></a>
### OI-27 A new bridge's first version

- **Statement:** Map brief B5.2: a bridge starts without a version. Map rev2: an empty `many_to_one` / `none` draft v1
  is created at once. The API creates none.
- **Recommendation:** create draft v1 at once (rev2), and the same for mapping sets.
- **Status:** Decided 2026-10-26. Created at once as an empty live v1 (`many_to_one` / `none` for bridges); same for mapping sets.

<a id="oi-28"></a>
### OI-28 `descendants` rules on analytical bridges

- **Statement:** Schema README 2.20.0 and map brief: only report bridges have `descendants` rules. Map rev2 B5.4 shows
  "Include descendants" whenever the source member is a folder.
- **Recommendation:** report bridges only (schema rule); hide the switch on analytical bridges.
- **Status:** Decided 2026-10-26. Report bridges only; hide the switch on analytical bridges.

<a id="oi-29"></a>
### OI-29 Key patterns versus seeded keys

- **Statement:** Map brief and rev2 helper text: "A letter, then letters, digits or underscores". Seeded mapping sets
  and bridges use dotted keys (`loan.branch`, `bank.margin_statement_gl_monthly`). Seeded `source_namespace` equals the
  key, not a system name as the docs describe. `web2`'s mock report bridge key `margin.gl_monthly` differs from the
  seed.
- **Recommendation:** allow `.` in mapping set and bridge keys (`^[A-Za-z][A-Za-z0-9_.]*$`), matching the seed and
  report-derived keys; keep namespace free text.
- **Status:** Decided 2026-10-26. Allow dots: `^[A-Za-z][A-Za-z0-9_.]*$`; namespace free text.
- **Note (2026-10-27, G1):** the dotted pattern (`DottedKey`) extends to dataset, calculation model and report keys,
  which the seed also writes dotted (`bank.balance_monthly`, `mortgage.loan_daily`, `bank.margin_model`,
  `bank.margin_statement`). Other keys keep `^[A-Za-z][A-Za-z0-9_]*$`.

<a id="oi-30"></a>
### OI-30 Check versus validate

- **Statement:** `web2` mappings has both `check*Version` (read-only) and `validate*Version` (transition); dimensions
  has only `checkVersion`; the API has `POST versions/{kind}/{id}/validation` (read-only) and the status endpoint.
- **Recommendation:** `check` = read-only rule run on any version; `validate` = draft → validated transition, for kinds
  with drafts.
- **Status:** Decided 2026-10-26. `check` = read-only rule run on any version; `validate` = draft -> validated, datasets and releases only.

<a id="oi-31"></a>
### OI-31 Report bridge owner shape

- **Statement:** Map brief B2 and the API's `BridgeSummary.owner` carry `reportSourceId`; map rev2 and `web2` drop it
  (`{ reportId, reportKey, sourceKey }`).
- **Recommendation:** `{ reportId, reportKey, sourceKey }`; source keys are unique per report and fixed.
- **Status:** Decided 2026-10-26. `{ reportId, reportKey, sourceKey }`.

## Datasets

<a id="oi-32"></a>
### OI-32 Materialization of JSON path fields

- **Statement:** `web2` models a materialization request with `viewName`, `columnName`, `state` (building / ready /
  failed), `requestedAt`, `refreshedAt`, `error`, polled until ready. The schema has only `qe_field.materialized_column`;
  the API's `materialize` runs `ALTER TABLE` synchronously on the source table and returns 204.
- **Options:** add materialization state columns or table ([SG-07](91-schema-gaps.md#sg-07-field-materialization));
  read state from StarRocks (materialized view refresh status); keep synchronous column materialization and drop the
  async model.
- **Recommendation:** decide the physical mechanism first (materialized view versus generated column); then store state
  only if StarRocks cannot report it.
- **Status:** Decided 2026-10-26. Mechanism: StarRocks materialized view per source object (async refresh). Store state (SG-07) only where StarRocks cannot report refresh status.

<a id="oi-33"></a>
### OI-33 Managed tables

- **Statement:** `web2` creates tables ("New dataset" with columns), adds columns for new fields (`createColumn`), marks
  sources `isManaged`, and renames a managed table while a draft is its sole reader. The API introspects only existing
  objects; nothing records which tables the API created.
- **Options:** a managed-object registry ([SG-08](91-schema-gaps.md#sg-08-managed-source-objects)); naming convention
  (prefix) instead of a registry; drop managed tables from scope.
- **Recommendation:** registry table; DDL only on registered objects; rename allowed only while no published version
  reads it.
- **Status:** Decided 2026-10-26. Managed-object registry (SG-08); DDL only on registered objects; rename only while no published version reads it.

<a id="oi-34"></a>
### OI-34 Source-object statistics

- **Statement:** `web2` shows row count, size on disk and when measured per source object and per dataset. Not stored;
  StarRocks `information_schema` exposes approximate values.
- **Recommendation:** read live from `information_schema` with a short cache; no column.
- **Status:** Decided 2026-10-26. Read live from `information_schema` with a short cache.

<a id="oi-35"></a>
### OI-35 Server-computed dependents

- **Statement:** `DatasetVersion.dependents` (item id → what reads it, and whether it blocks delete) is computed by the
  mock. The API checks blockers at delete time only.
- **Recommendation:** compute in the version read model from the same graph the delete blockers use.
- **Status:** Decided 2026-10-26. Computed by the server in the version read model.

<a id="oi-36"></a>
### OI-36 Editing `datasetKey`

- **Statement:** `updateDataset` accepts `datasetKey` in its patch; keys are fixed elsewhere and the engine's requests
  name datasets by key.
- **Recommendation:** fixed; remove from the patch.
- **Status:** Decided 2026-10-26. Fixed after creation; removed from the patch.

## Releases and assumption sets

<a id="oi-37"></a>
### OI-37 Checking an unsaved manifest

- **Statement:** `checkRelease(id, manifest?)` and `diffReleases(from, to, toManifest?)` run over a draft's unsaved
  manifest (`CheckReport.unsaved`). The API checks saved state only.
- **Recommendation:** release change-set preview returns the check report and the diff for the pending manifest.
- **Status:** Decided 2026-10-26. Release change-set preview returns the check report and diff for the pending manifest.

<a id="oi-38"></a>
### OI-38 Derived release and set read models

- **Statement:** `web2` needs `ReleaseDiff` with `readBy`, `diffVsActive`, `SetComparison`, `CapturePreview`, pin
  `state`, `livePins`, `changedSinceCapture` and per-report `Readiness`. None exist in the API.
- **Recommendation:** compute them server-side as specified in
  [14](14-releases-and-assumption-sets.md#derived-read-models-agreed-oi-38).
- **Status:** Decided 2026-10-26. Compute all derived read models server-side as specified in doc 14.

<a id="oi-39"></a>
### OI-39 Release display name

- **Statement:** `web2/lib/datasets/contract.ts` `Release.displayName`; no column (`qe_semantic_release` has a key only);
  the releases contract has none.
- **Recommendation:** drop it; show the key.
- **Status:** Decided 2026-10-26. Dropped; show the key.

<a id="oi-40"></a>
### OI-40 Activation history

- **Statement:** The rel brief shows no activation date because none is recorded; rollback is "activate an older
  release". Nothing records who activated what, when.
- **Options:** activation log table ([SG-09](91-schema-gaps.md#sg-09-release-activation-log)); none.
- **Recommendation:** add the log; it answers "what was live on date X", which historical sets also rely on.
- **Status:** Decided 2026-10-26. Add an activation log table (SG-09).

<a id="oi-41"></a>
### OI-41 Release content hash

- **Statement:** `qe_semantic_release.content_hash` exists; no document says who computes it or what it covers.
- **Recommendation:** compute on publish over the sorted manifest; show it on the release page.
- **Status:** Decided 2026-10-26. Compute on publish over the sorted manifest; show on the release page.

## Calculation models

<a id="oi-42"></a>
### OI-42 Refusing an edit versus staging a draft

- **Statement:** `web2` refuses a model edit at once if it introduces a violation, then lets the user save the draft.
  The API refuses individual writes. `web2` checks two report-side refusals only against the open report.
- **Recommendation:** preview refuses the operation with the model gate and every enabled report's rules; commit
  re-runs it ([15](15-calculation-models.md#refusal-semantics-agreed-oi-42)).
- **Status:** Decided 2026-10-26. Preview refuses an operation that introduces a model-rule or enabled-report violation; commit re-runs the gate.

<a id="oi-43"></a>
### OI-43 Formula check endpoint

- **Statement:** `web2` parses formulas in the browser with regular expressions (two parsers that disagree on case:
  `metricGraph.ts` accepts mixed case, `rules.ts` uppercase only). The engine has no validation endpoint; the Admin API
  references the Domain library's `ExpressionParser`.
- **Recommendation:** `POST calculation-models/{id}/formula-check` returning errors with positions, reads, parameters and
  the required language.
- **Status:** Decided 2026-10-26. `POST calculation-models/{id}/formula-check` using the Domain `ExpressionParser`.

<a id="oi-44"></a>
### OI-44 Which reports use a model

- **Statement:** `web2` reads a static `reportRefs` list unrelated to the report library.
- **Recommendation:** `usedBy` computed from `qe_report.calculation_model_id` in model read models.
- **Status:** Decided 2026-10-26. `usedBy` computed from `qe_report.calculation_model_id`.

<a id="oi-45"></a>
### OI-45 Model creation and keys

- **Statement:** `web2` creates and deletes models immediately while other edits are staged, and checks key uniqueness
  only against the browser's list. Model keys and metric keys are fixed in the API.
- **Recommendation:** create and delete are commands; uniqueness is a server 409; keys fixed.
- **Status:** Decided 2026-10-26. Create and delete are commands; key uniqueness is a server 409; model and metric keys fixed.

## Reports

<a id="oi-46"></a>
### OI-46 A report contract

- **Statement:** Reports have no port; `ReportState` is one document in React state. The API has a report editor view
  (rows and sources only) plus a dozen sub-resource endpoints.
- **Recommendation:** `ReportsApi` with the `ReportDocument` read model and report change set ([16](16-reports.md)).
- **Status:** Decided 2026-10-26. `ReportsApi` port with a `ReportDocument` read model and a report change set.

<a id="oi-47"></a>
### OI-47 Report identity: id scheme, key rename and model change

- **Statement:** `web2` uses three report id schemes (`report_1` in the library, `rep-<key>` in releases,
  `reportId = key` in bridge owners). It lets the user rename the report key and change the model. The schema fixes
  `calculation_model_id` and `report_dimension_id`; the API fixes `report_key` and the model ("it never changes"), and
  the report dimension takes the report's key.
- **Options:** keep keys and model fixed (create a new report to change them); allow key rename with cascades to the
  report dimension and bridge keys; allow model change with the cascade `web2` performs.
- **Recommendation:** ids from the server everywhere; key and model fixed; "change model" becomes "duplicate onto
  another model" (create with `copyFromReportId` and a different `modelId`, dropping what does not resolve).
- **Status:** Decided 2026-10-26. Key and model fixed; "change model" becomes "duplicate onto another model".

<a id="oi-48"></a>
### OI-48 Where the row tree lives

- **Statement:** `web2` stores rows in `ReportState.rows` (kinds `line`, `subtotal`, `heading`, `formula`) and lets the
  user rename row keys. The schema stores rows as report dimension members plus member options and overlay lines; the
  API's kinds are `data`, `group`, `section`, `formula` and row keys are fixed. `heading` maps to either a `group` that
  shows no values (a member) or a `section` (an overlay line). The dimensions module expects the row tree to be the
  hidden report dimension; the editor never writes it.
- **Recommendation:** the schema is the store; kinds `data`, `group`, `section`, `formula`; `heading` becomes `group`
  with `showsValues = false` when it has member children and `section` otherwise; row keys fixed.
- **Status:** Decided 2026-10-26. Schema is the store; kinds `data`, `group`, `section`, `formula`; heading mapping as recommended; row keys fixed.

<a id="oi-49"></a>
### OI-49 Where report mappings live

- **Statement:** `web2` stores mappings in `Source.mappings` inside the report while the mappings module shows the same
  report bridge with static versions and counts.
- **Recommendation:** the report bridge's `qe_bridge_mapping` rows, written by the report change set
  ([16](16-reports.md#sources-and-report-bridges)).
- **Status:** Decided 2026-10-26. Report bridge `qe_bridge_mapping` rows, written by the report change set.

<a id="oi-50"></a>
### OI-50 Breakouts, scenarios and scopes

- **Statement:** `BreakoutDimension` (alias key, name) maps to `qe_calculation_dimension`. `ReportScenario` has a `name`
  the schema does not store (it takes the role's). `RowScope` allows several scopes per variable with `row` / `subtree`;
  the schema keys `qe_report_variable_scope` by `(report_id, variable_key)` with `exact` / `descendants`, and `exact`
  needs a leaf.
- **Recommendation:** scenario name = role name (read-only); one scope per variable with the schema's vocabulary; the UI
  stops offering a second scope.
- **Status:** Decided 2026-10-26. Scenario name = role name (read-only); one scope per variable with `exact` / `descendants`.

<a id="oi-51"></a>
### OI-51 Left-out items and unfit members

- **Statement:** `Source.leftOut` (items intentionally excluded, not counted as coverage gaps) and `unfitMembers` have no
  table.
- **Options:** a table ([SG-10](91-schema-gaps.md#sg-10-report-source-left-out-items)); store in `qe_report.payload_json`
  (opaque application metadata); drop.
- **Recommendation:** a table for `leftOut` (coverage is a server computation and needs it); drop `unfitMembers` (no UI
  uses it).
- **Status:** Decided 2026-10-26. Table for `leftOut` (SG-10); drop `unfitMembers`.

<a id="oi-52"></a>
### OI-52 Multiplier versus factor

- **Statement:** `web2` keeps `factor` (+1 / −1) and an optional `multiplier`, "kept apart so exclusions still net to
  zero". The schema has one `factor` per bridge rule.
- **Recommendation:** store `factor × multiplier`; exclusions stay separate −1 rules on the same item; the read model
  splits a factor into sign and multiplier for display.
- **Status:** Decided 2026-10-26 (differs from the recommendation). Drop the multiplier; the factor is the only number users edit.

<a id="oi-53"></a>
### OI-53 Report preview and run

- **Statement:** The Preview tab is static and unreachable. The engine runs enabled reports only, over SignalR. The
  Admin API does not call the engine.
- **Options:**
  1. The browser connects to the engine hub directly with the user's token (only enabled reports).
  2. The Admin API proxies `ExecuteReport` for enabled reports (`POST reports/{id}/preview-run`).
  3. Engine support for running a submitted, unsaved report definition (new engine feature).
- **Recommendation:** option 2 for enabled reports now; raise option 3 with the engine team for disabled drafts.
- **Status:** Decided 2026-10-26 (differs from the recommendation). Preview and run are out of scope for this refactor; `POST reports/{id}/preview-run` is not built.

<a id="oi-54"></a>
### OI-54 Report defaults in JSON columns

- **Statement:** `periodicity`, `layout`, `defaultBreakout` and `defaultFilters` live in `default_settings_json`,
  `default_pivot_json` and `default_filter_json`, whose shapes the schema documents; `web2` uses capitalized values
  (`'Month'`, `'Statement'`) and its own `Filter[]`.
- **Recommendation:** typed fields in `ReportSettings`, serialized by the server into the documented JSON shapes; the
  schema's lowercase vocabularies.
- **Status:** Decided 2026-10-26. Typed fields in `ReportSettings`; server serializes to the documented JSON shapes; lowercase schema vocabularies.

<a id="oi-55"></a>
### OI-55 Data copied into reports, and comparisons

- **Statement:** `ReportState` carries copies of accounts, datasets, scenario members, assumption sets and the model's
  variables and measures; `dimensions.ts` hard-codes three dimensions and the role catalogue. Readiness walks the copies.
  `comparisons` are stored in the report but the schema has no table; comparisons are run-time inputs in report
  contract 2.1.
- **Recommendation:** remove the copies and read from the owning modules; comparisons are preview-run inputs, not
  report data.
- **Status:** Decided 2026-10-26. Remove copies; read from owning modules; comparisons are run-time inputs, not stored.

<a id="oi-56"></a>
### OI-56 Cascades belong to the server

- **Statement:** `web2` cascades some deletes and renames in the browser and misses others (removing a source leaves its
  profile picks; removing a dimension leaves it in default filters; a model change leaves calculated columns and formula
  rows dangling).
- **Recommendation:** the server performs every cascade listed in [16](16-reports.md#change-set-operations) and refuses
  what it cannot cascade.
- **Status:** Decided 2026-10-26. Server performs every cascade listed in doc 16 and refuses what it cannot cascade.
