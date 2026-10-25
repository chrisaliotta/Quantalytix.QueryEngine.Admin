# Releases & Assumption Sets — Concepts and Frontend Prototype Requirements

Brief for a v0 prototype. Part A explains what semantic releases and assumption sets are and how the platform uses them.
Part B specifies the prototype. It assumes mock data and no API; everything the real system validates server-side is
described as "show inline" so the prototype has a place for it.

---

# Part A — How releases and assumption sets work

## A1. The two questions every request answers

Every query and report run reads metadata that comes in versions. Before it reads any data, it settles two questions:

| Question | Answered by | Pins | Changes when | Default |
|---|---|---|---|---|
| Which **dataset versions**: which table or view, which fields, measures and bindings? | A **semantic release** | One dataset version per dataset | A new release is activated | The **active release** |
| Which versions of the **dimensions, mapping sets and bridges** those bindings name? | An **assumption set** | One version per dimension, mapping set and bridge | A version is published (active set only) | The **active assumption set** |

The two are independent on purpose. Dataset versions are strict and structural: changing a dataset's source table, a
field's definition, a binding or a measure's logic needs a new dataset version and a new release. Dimensions, mapping
sets and bridges are "living": adding members, regrouping a hierarchy or refreshing a mapping is an edit to the
published version, or a new published version, and needs no release at all. Calculation models and reports are
versioned by neither; each is edited in place and checked before every write.

A request that names nothing runs the active release with the active set. A request can name a **historical**
assumption set to rerun the versions and the release it captured, which is how a backtest or an as-reported comparison
reproduces an earlier result.

## A2. Semantic releases

A **release** is a manifest: a key, such as `prod/2026-q3`, and one pinned **dataset version** for each dataset it
exposes. A dataset the manifest does not name does not exist for queries while the release is active. A release pins
nothing else: dimensions, mapping sets and bridges are picked by the assumption set, and reports are not pinned at all;
a report runs against whichever release is active.

**Lifecycle.** Releases are a **strict** kind, unlike dimensions:

- Statuses `draft → validated → published → retired`, plus `validated → draft` to go back and edit.
- Only a **draft's** manifest changes. A validated, published or retired release never changes; to change datasets,
  clone it into a new draft.
- **Several releases may be published at once.** Publishing does not make a release live and retires nothing.
- **Activation** makes one published release the one runtime queries use (`active_semantic_release_id`). It is the only
  way a dataset version reaches queries. Activating advances the tenant's **semantic revision**, so the query engine
  reloads its metadata on the next request. Activating an older published release is a rollback.
- The **active release cannot be retired**: activate another release first. A retired release cannot be activated.
- Only a **draft** can be deleted. A published release is retired, never deleted, so the release a historical
  assumption set captured always exists.

**Creating.** A new release starts as a draft. Its key starts with a letter or digit and holds letters, digits, `_`,
`.`, `-` and `/` (128 at most), and is unique. It may copy another release's manifest (by default the active release's,
so the usual flow is "copy the active release, move a few datasets to their new versions"), or start empty.

**Validation on validate/publish.** The release rules, in the order the API runs them:

| Rule | Checks | Message shape |
|---|---|---|
| `manifest_not_empty` | The release pins at least one dataset version | "The release pins no dataset versions." |
| `pinned_version_belongs_to_dataset` | Each pinned version is a version of its dataset | "Dataset 'loans.details' pins dataset version 812, which is not one of its versions." |
| `pinned_version_published` | Each pinned version is published | "Dataset 'loans.details' pins v4, which is draft; a release pins only published versions." |
| `bound_dimensions_resolvable` | Each binding of a pinned version binds a dimension that has a published version | "Binding 'servicingBranch' of dataset 'loans.details' binds dimension 'Branch', which has no published version." |
| `mapped_bindings_resolvable` | Each mapped binding's mapping set has a published version | "Binding … maps through mapping set 'loan.branch', which has no published version." |
| `bridge_bindings_resolvable` | Each bridge binding's bridge has a published version | "Binding … maps through bridge 'gl_account_to_account_group', which has no published version." |
| `report_sources_resolve` | Every **enabled report**'s sources still resolve in the release's dataset versions: the dataset is pinned, the role, field, time field, measures and filter fields exist and are enabled | "Report 'bank.margin_statement' source 'simulation' reads dataset 'bank.nii_simulation_monthly', which the release does not pin." |
| `report_factors_apply` | A report source whose rules use factors other than 1 reads only measures that apply bridge factors | "Report … source 'gl_monthly' adds items into rows with factors other than 1, but variable 'INTEREST_INCOME' reads 'interestIncome' of 'bank.income_monthly', which ignores bridge factors." |

Retiring runs one rule, `release_not_active`: "Release 'prod/2026-q3' is the active release; activate another release
before retiring it."

## A3. Assumption sets

An **assumption set** is a named set of version pins: one version of each dimension, mapping set and bridge. There are
exactly two kinds.

**The active set** (key `active`, status `active`). It exists from the moment the tenant database does and can never be
deleted. It always pins every identity's **published** version: publishing a version repoints the active set in the
same operation that retires the predecessor. It reads the **active release**. A request that names no set uses it, and
a report run always does.

**Historical sets** (status `historical`). A user **captures** one from the active set: a new key, a display name, an
optional description. The capture copies the active set's pins as they are now, records `capturedAt`, and records the
**active release** as the set's own release. From then on:

- Its pins never change. Later publishes repoint only the active set.
- A version it pins keeps changing **while it is still published**, because the published version is edited in place;
  it freezes once a successor is published and it retires. So a capture is fully frozen only once each of its versions
  has a successor. The prototype makes that visible (B5.4).
- It keeps reading the release it captured, whatever is activated later. Retiring that release does not change the set.
- An identity created after the capture has no pin in it, so a request that names the set cannot use that identity.
- A capture **leaves out** the pins of report dimensions and of the bridges into them: those belong to reports, which
  always read them from the active set, so an as-reported comparison lines its rows up with the run's by member key.
- It can be relabelled (display name, description; the key is fixed) and deleted at any time. Deleting removes the set
  and its pins; the versions stay.

Assumption set keys start with a letter and hold letters, digits and `_` (128 at most), and are unique. Capturing or
deleting a set advances the semantic revision, because the engine caches the set keys requests may name; relabelling
does not.

## A4. How requests use them

- A **query** may name `"assumptionSet": "fy2026_q2_close"`; it then reads that set's pins over the dataset versions of
  that set's release. Without it, the active set and active release.
- A **catalog** request may name a set too, to list what the set exposes.
- A **report run** always uses the active set and active release. A report **comparison** may name a historical set to
  compare with the values as reported then: the comparison reads the set's analytical dimensions, mapping sets and
  bridges and its release, while the report's own dimension and its sources' bridges come from the active set. The set
  must pin every analytical dimension, mapping set and bridge the report's sources, calculation dimensions and scenarios
  read, and its release must pin every source dataset; otherwise the comparison fails before any query runs.
- At request start the engine captures the release, the assumption set and every pinned version it reads, and keeps
  them for the whole request even if something is published or activated meanwhile.

## A5. Typical workflows

1. **Ship a dataset change.** Publish the new dataset version (dataset manager) → New release copying the active one →
   move the dataset to its new version → Validate (fix what the rules report) → Publish → Activate.
2. **Roll back.** Activate the previous published release. Nothing is deleted; the newer release stays published and
   can be activated again.
3. **Close a period.** After the month's dimension and mapping changes are published, Capture the active set as
   `fy2026_q3_close`. Reports compare against it "as reported"; analysts backtest with it.
4. **Clean up.** Delete abandoned draft releases; retire published releases nobody will activate again; delete
   historical sets nobody names any more.

---

# Part B — Frontend prototype requirements

## B1. Purpose and scope

Build the release and assumption set screens: the release list and release page with lifecycle actions, a manifest
editor, a diff against the active release, client-side release checks and activation; the assumption set list and set
page with pins, a comparison against the active set, an as-reported readiness view, capture, relabel and delete.

Out of scope: API calls, auth, tenants, editing datasets, dimensions, mapping sets, bridges or reports (their pages come
from their own prototypes; link to them, and dead links are acceptable). Mock data and an in-memory store; mutations
update the store.

Reuse existing design patterns from current screens that have been created.

## B2. Domain model (for mock data)

```ts
type VersionStatus = 'draft' | 'validated' | 'published' | 'retired';
type PinKind = 'dimension' | 'mapping_set' | 'bridge';
type BindingKind = 'direct' | 'mapped' | 'range' | 'bridge' | 'time';

/** Tenant state the header and activation read and write. */
interface TenantInfo { tenantKey: string; activeReleaseId: string; semanticRevision: number; }

interface Dataset { id: string; key: string; displayName: string; isEnabled: boolean; }
interface DatasetVersion {
  id: string; datasetId: string; versionNumber: number; status: VersionStatus; publishedAt: string | null;
  bindings: { name: string; kind: BindingKind; dimensionId: string; mappingSetId?: string; bridgeId?: string }[];
}

interface Release { id: string; key: string; status: VersionStatus; createdAt: string; publishedAt: string | null; }
interface ReleaseDataset { releaseId: string; datasetId: string; datasetVersionId: string; }

interface AssumptionSet {
  id: string; key: string; displayName: string; description: string | null;
  status: 'active' | 'historical';
  releaseId: string | null;        // null for the active set, which reads the active release
  capturedAt: string | null; createdAt: string;
}
interface Pin { setId: string; kind: PinKind; identityId: string; versionId: string; }

/** Read-only slices of the dimension, mapping and bridge prototypes' data. */
interface Identity {
  kind: PinKind; id: string; key: string; displayName: string;
  dimensionId: string;              // a dimension itself, a mapping set's dimension, or a bridge's target
  reportId: string | null;          // set for a report dimension or a bridge into one
  versions: { id: string; versionNumber: number; status: VersionStatus; publishedAt: string | null }[];
}

/** What an enabled report reads, for release checks and as-reported readiness. */
interface ReportRef {
  id: string; key: string; displayName: string; isEnabled: boolean;
  sources: { key: string; datasetId: string; usesFactors: boolean; measureKeys: string[] }[];
  reads: { kind: PinKind; identityId: string }[];   // analytical dimensions, mapping sets and bridges it needs
}
```

### Mock data to seed

**Datasets and versions**

- `bank.balance_monthly` — v1 retired, v2 published, v3 published (newer than any release pins).
- `bank.income_monthly` — v1 published.
- `loans.details` — v2 retired, v3 published, v4 **draft** (binds `servicingBranch` through mapping set `loan.branch`).
- `bank.plan_monthly` — v1 published.
- `bank.nii_simulation_monthly` — v1 published.
- `ledger.balance_sheet` — v1 published, v2 published; v2 adds a mapped binding through `legacy.costcenter`, a mapping
  set with **no published version**.
- `mortgage.loan_daily` — v1 published.
- `bank.ledger_monthly` — v1 published, v2 validated.

**Releases** (tenant `development`, semantic revision `4182`)

- `prod/2025-q4` — retired; pins the v1/v2 era. Read by historical set `fy2025_close`.
- `prod/2026-q2` — published, not active (the rollback target); does **not** pin `bank.nii_simulation_monthly`.
- `prod/2026-q3` — published, **active**; pins 8 datasets, including `bank.balance_monthly` v2 and `loans.details` v3.
- `prod/2026-q4` — validated; copy of q3 with `bank.balance_monthly` v3. Publishing it then activating it shows the
  happy path.
- `prod/2027-q1` — draft copied from q3, seeded to fail checks: `loans.details` moved to v4 (draft →
  `pinned_version_published`), `ledger.balance_sheet` moved to v2 (`mapped_bindings_resolvable`), and
  `bank.nii_simulation_monthly` removed while enabled report `bank.margin_statement` reads it
  (`report_sources_resolve`).
- `scratch/empty` — draft with an empty manifest (`manifest_not_empty`; deletable).

**Identities and assumption sets** (about 25 dimensions, 6 mapping sets, 9 bridges, of which 3 report dimensions and 7
report bridges)

- `active` — pins every published version; reads the active release.
- `fy2025_close` — captured 2025-12-31 23:59, reads `prod/2025-q4` (a **retired** release). Pins `EvolvingBranch` v1,
  `evolving.branch` v1 and `Account` v1, all retired since (frozen).
- `fy2026_q2_close` — captured 2026-06-30 18:00, reads `prod/2026-q2`. Most pins are still the published versions
  (live); `Branch` v3 has been superseded by v4. Dimension `RateShock` was created after the capture, so the set has no
  pin of it.

**Reports:** `bank.margin_statement` (reads `Account`, `Branch`, `PlanVersion`, `RateShock`, mapping set `loan.branch`;
datasets balance, income, plan and NII simulation), `consolidated.balance_sheet` (reads `GlAccount`, `Branch`; datasets
`ledger.balance_sheet`, `loans.details`), `bank.key_ratios` (balance and income). Against `fy2026_q2_close`, the margin
statement is **not** ready (no `RateShock` pin; its release lacks the NII simulation); the other two are.

## B3. Navigation

- Sidebar, Semantic model group: … Dimensions, **Releases**, **Assumption sets**.
- `/releases` — release list (B4.1); "New release" opens a sheet.
- `/releases/:releaseId?tab=` — a release's page (B4.3). Tabs: `manifest` (default), `changes`, `checks`, `usage`.
- `/assumption-sets` — set list (B5.1); "Capture active set" opens a sheet.
- `/assumption-sets/:setId?tab=` — a set's page (B5.3). Tabs: `pins` (default), `compare`, `reports`.
- Tab persists in the URL. Breadcrumbs: Releases › {release key}; Assumption sets › {set key}.
- Wherever a dataset, dimension, mapping set, bridge or report is named, link to its page: a dataset version to
  `/datasets/:id?version=`, a dimension to `/dimensions/:id?version=`, a mapping set to
  `/dimensions/:dimensionId/mapping-sets/:id?version=`, an analytical bridge to `/dimensions/:targetId/bridges/:id?version=`,
  a report dimension or report bridge to `/reports/:reportId`. Dead links are acceptable.
- **Active release chip** (nice-to-have): a small chip in the app header, "prod/2026-q3 · rev 4182", linking to the
  active release, so every page shows what queries run on.

## B4. Releases

### B4.1 List page

**Active release card** above the table: "Queries and reports run on **prod/2026-q3**: 8 datasets, published
2026-07-01. Semantic revision 4182." Links: Open · Compare with… (opens B4.5 on another release). The platform does not
record when a release was activated, so the card shows no activation date.

Toolbar: search (key); Status filter chips (Draft · Validated · Published · Retired; Retired off by default); count;
primary "New release".

Columns: Release (key in mono) · Status (badge) · Active (solid "Active" badge on one row) · Datasets (count) · Changes
vs active (`+1 ~2 −0` as small colored counts; "—" for the active one; hover lists them) · Created · Published · row
actions: Open, Clone, Activate (published and not active), Delete (draft). Newest first. A row opens the release page.

Empty state: "A release pins one dataset version per dataset. Create one to choose which dataset versions queries read."

### B4.2 Create flow

A sheet, also opened by Clone (prefilled with the cloned release):

- Key (mono; helper "Letters, digits, `_ . - /`, starting with a letter or digit, such as prod/2026-q4. Fixed once
  created."). Inline: pattern and uniqueness.
- Copy manifest from (combobox over releases with status badges; defaults to the active release; clearable; helper
  "Starts from this release's pinned dataset versions; clear it to start empty").
- Summary: "Creates draft **prod/2026-q4** pinning the 8 dataset versions of prod/2026-q3." or "…with an empty
  manifest."
- Create → the release page, Manifest tab, in edit mode.

### B4.3 Release page

**Header:** key (mono, large); status badge; "Active" badge when active. Beneath: "Created {date} · Published {date}".

**Lifecycle strip:** a four-step indicator, Draft → Validated → Published → Active, with the current step highlighted
and the next action as the primary button:

| Status | Primary | Secondary | Notes |
|---|---|---|---|
| draft | Validate | Delete | Manifest editable |
| validated | Publish | Back to draft | Read-only; Publish confirm: "Publish prod/2026-q4? A published release never changes; it can only be retired. Publishing does not activate it." |
| published, not active | Activate | Retire | Activate opens B4.8 |
| published, active | — | Retire (disabled; tooltip "Activate another release before retiring this one") | "Queries run on this release" |
| retired | — | — | Read-only; "Retired releases cannot be activated" |

Clone is always available ("New release from this manifest"). Validate and Publish run the checks first (B6); on
failure the Checks tab opens with the violations and the status stays.

**Tabs:** Manifest · Changes · Checks · Usage.

### B4.4 Manifest tab

The pinned dataset versions, one row per dataset.

Columns: Dataset (display name, key beneath in mono; disabled datasets get a muted "Disabled" badge) · Pinned version
(`v3` with status badge) · Latest published (`v3`; when newer than the pinned one, an amber "Newer: v4" chip) ·
Bindings (count; hover lists each binding with its kind and what it binds, such as "servicingBranch · mapped via
loan.branch → Branch") · Issues (inline icons for this row's check failures, B6) · actions (Remove; draft only).

**On a draft** the manifest is edited locally and saved as a whole, because the release stores a complete manifest:

- The Pinned version cell is a select listing the dataset's versions newest first with status badges; non-published
  versions are selectable but flagged inline ("v4 is draft; a release pins only published versions").
- "Add dataset" (combobox over datasets not in the manifest; picks the latest published version; datasets with no
  published version are listed but flagged).
- "Use latest published" bulk action: a preview dialog listing each row it would move ("bank.balance_monthly v2 → v3"),
  then Apply.
- A sticky bar while there are changes: "3 unsaved changes · Discard · Save manifest". Leaving with unsaved changes
  prompts.

**On any other status** the table is read-only, with the note "Only a draft release's manifest changes. Clone this
release to change its datasets."

Toolbar: search (dataset key or name); filter "Newer version available"; count.

### B4.5 Changes tab

A diff of this release against the active release (selector to compare against any other release instead):

- **Added** — datasets this release pins and the other does not.
- **Removed** — datasets the other pins and this does not; when an enabled report reads one, flag it ("read by
  bank.margin_statement").
- **Version changes** — `bank.balance_monthly v2 → v3`, each side linking to the dataset version.
- **Unchanged** — a collapsed count.

For the active release itself, the selector defaults to the newest other published release, since the platform keeps no
history of which release was active before. Against the active release, the diff is what activation will switch, so
B4.8 reuses it.

### B4.6 Checks tab

"Run checks" runs the rules of A2 against the current manifest (including unsaved draft edits, labelled "unsaved").
Results are grouped by rule, each violation with the message shape from A2 and a link: a manifest row (scrolls to and
highlights it), a binding's dimension, mapping set or bridge page, or a report's source in the report editor
(`/reports/:reportId?source=`). A passing run shows "All 8 checks pass" with the rule list ticked. Validate and Publish
show the same panel when they fail.

### B4.7 Usage tab

- Whether the release is active.
- **Assumption sets that read it**: the active set when it is active, and every historical set that captured it, each
  linking to the set. Note: "Historical sets keep reading the release they captured, even after another release is
  activated or this one is retired."
- **Reports**: the enabled reports whose sources read datasets this release pins (from the checks' scope).

### B4.8 Activation dialog

Confirm dialog titled "Activate prod/2026-q4?":

- Impact summary from B4.5: "Queries and reports switch from prod/2026-q3: 1 dataset changes version, 0 added,
  0 removed." with the list collapsed beneath.
- When the release was created before the active one, a notice "This rolls back to an older release."
- Fixed text: "The semantic revision advances, so the query engine reloads its metadata on the next request.
  Historical assumption sets keep reading the releases they captured."
- Activate → updates the tenant's active release and revision in the store, toasts "prod/2026-q4 is now the active
  release.", and the Active badge, header chip and list card move.

### B4.9 Delete

Only a draft has Delete; confirm "Delete prod/2027-q1? The draft and its manifest are deleted." Non-draft releases show
no Delete; the overflow menu's disabled item explains "Only a draft can be deleted. Retire a published release instead."

## B5. Assumption sets

### B5.1 List page

**Active set card** at top: "**active** · Active — pins every published version (214 pins) and reads the active release
**prod/2026-q3**. Every publish repoints it." Actions: Open · Capture.

**Historical sets** table. Toolbar: search (key, name, description); count; primary "Capture active set".

Columns: Set (display name, key beneath in mono) · Release (key, linked; a gray "Retired" badge when that release is
retired) · Captured (date; relative time on hover) · Pins (count) · Live pins (count of pins whose version is still
published, with a tooltip "Edits to these versions still change what this set reads") · Changed since capture (count of
identities whose published version differs now) · row actions: Open, Delete. Newest capture first.

Empty state: "No historical sets yet. Capture the active set at a period close so reports can compare as reported and
analysts can backtest."

### B5.2 Capture flow

Sheet "Capture the active set":

- Key (mono; helper "The name requests use, such as fy2026_q3_close: a letter, then letters, digits or underscores.
  Fixed once captured."). Inline: pattern and uniqueness.
- Display name (required). Description (optional, 1,024 characters; placeholder "What this snapshot is for").
- **Preview** (read-only): "Pins 61 dimensions, 6 mapping sets and 9 bridges as published now, and release
  **prod/2026-q3**. Leaves out 3 report dimensions and 7 report bridges, which reports always read from the active set."
- Capture → the new set's page; toast "Captured fy2026_q3_close."

### B5.3 Set page

**Header:** display name; beneath `key · status · reads {release}`. Actions: Edit (sheet: display name, description; key
read-only), Capture (active set only), Delete (historical only).

**Facts card:** Key · Status · Release (linked, status badge; for the active set "The active release, prod/2026-q3") ·
Captured · Created · Description · **How to name it** (historical only, copyable snippets):

```json
{ "assumptionSet": "fy2026_q2_close" }
```

with the captions "In a query" and "In a report comparison".

**Tabs:** Pins · Compare (historical only) · Reports (historical only).

### B5.4 Pins tab

Grouped by kind with counts (Dimensions 61 · Mapping sets 6 · Bridges 9); filter chips for kind and state; search.

Columns: Identity (key in mono, display name) · Pinned version (`v3` with status badge; links to the identity's page with
that version selected, per B3) · Published now (`v4`; historical only) · State:

- **Live** (green dot) — the pinned version is still published, so edits to it still change what the set reads.
- **Frozen** (gray, snowflake) — the pinned version is retired and never changes again.
- **Report-owned** (active set only; `FileText`) — a report's dimension or a bridge into one; links to the report editor.
  Captures leave these out.

For a historical set with live pins, a banner above the table: "12 pins still point at published versions. Until each
gets a successor, edits to it also change what fy2026_q2_close reads."

### B5.5 Compare tab (historical only)

The set against the active set, in three sections:

- **Changed since capture** — identities whose pinned version differs from the published one: "Branch v3 → v4", each
  side linking to its version.
- **Not in this set** — identities created after the capture (`RateShock`), with the note "Requests that name this set
  cannot use them."
- **Release** — the set's release against the active release, reusing the B4.5 diff ("prod/2026-q2 → prod/2026-q3:
  1 dataset added").

### B5.6 Reports tab (historical only)

As-reported readiness: for each enabled report, whether a comparison can name this set (A4).

- Ready: a green check, "Comparisons can name fy2026_q2_close."
- Not ready: a red cross and the reasons, each linking where it can be fixed or understood: "Does not pin dimension
  RateShock (created after the capture)"; "Release prod/2026-q2 does not pin dataset bank.nii_simulation_monthly, which
  source 'simulation' reads."

### B5.7 Delete

Historical sets only. Confirm "Delete fy2026_q2_close? The set and its pins are deleted, so requests and report
comparisons can no longer name it. The versions it pins stay." The active set shows no Delete; its overflow menu's
disabled item explains "The active set holds the default versions and cannot be deleted."

## B6. Simulated lifecycle and validation

**Validate / Publish a release:** run the A2 rules client-side over the store; on violations show B4.6 and keep the
status. On success move the status (`publishedAt` set on publish) and toast.

**Back to draft:** validated → draft, manifest editable again.

**Retire:** refused for the active release with the `release_not_active` message; otherwise confirm (destructive,
"Retiring cannot be undone; a retired release cannot be activated.") and retire.

**Activate:** published only; update `TenantInfo.activeReleaseId`, increment `semanticRevision`, toast.

**Capture:** copy the active set's pins minus report-owned ones into a new historical set with `releaseId` = the active
release and `capturedAt` = now; increment `semanticRevision`.

**Delete set:** remove the set and its pins; increment `semanticRevision`. **Relabel:** no revision change.

**Cross-prototype hook:** when the dimension, mapping or bridge prototype publishes a version, the active set's pin moves
to it; if the stores are shared, the Pins tab and the live/frozen states update.

## B7. Refusals and their messages

| Action | When | Message |
|---|---|---|
| Edit manifest | Release is not a draft | "The release is not a draft." (UI: read-only, B4.4) |
| Retire release | It is active | "Release 'prod/2026-q3' is the active release; activate another release before retiring it." |
| Activate release | It is not published | "Release is not published": "prod/2026-q4 is validated; only published releases can be activated." |
| Delete release | It is not a draft | No action offered (B4.9) |
| Create release | Key taken | "A release with key 'prod/2026-q3' already exists." |
| Capture set | Key taken | "An assumption set with key 'fy2026_q2_close' already exists." |
| Delete set | It is the active set | "Assumption set 'active' is the active set, which holds the default versions; it cannot be deleted." |

## B8. Interaction details

- Destructive actions confirm; every mutation toasts.
- Sheets and the manifest editor prompt on leave when dirty.
- Read-only contexts render text, not disabled inputs.
- Dates show absolute on hover, relative in tables ("3 days ago").
- Keys, release keys and version numbers in mono.
- Diffs use consistent colors: added green, removed red, changed amber.
- Tables handle 200 datasets and 500 pins: virtualize or paginate; search is debounced.

## B9. Visual conventions

- shadcn: Sheet for create, clone, capture and edit; Dialog for activation, bulk "Use latest published" and confirms;
  Tabs, Badge, Table, Card, Command/Combobox, Tooltip, Sonner.
- Status colors: draft amber, validated blue, published green, retired gray. "Active" is a solid primary badge.
- Icons: release `Rocket`; active `Radio`; assumption set `SlidersHorizontal`; historical set `History`; capture
  `Camera`; live pin `Activity`; frozen pin `Snowflake`; report-owned `FileText`; diff `GitCompare`; warning
  `TriangleAlert`.

## B10. Deliverables from v0

- Routable release and assumption set list and detail pages with a shared mock data module (tenant info, datasets and
  versions, releases and manifests, identities, assumption sets and pins, reports) and an in-memory store.
- All sheets, dialogs and confirms wired to the store, including activation, capture and the manifest editor.
- A dev menu to jump to: `prod/2026-q3` (active), `prod/2026-q4` (validated, ready to publish and activate),
  `prod/2027-q1` (draft failing three checks), `scratch/empty` (empty, deletable), `prod/2026-q2` (rollback target),
  `fy2025_close` (frozen, reads a retired release), `fy2026_q2_close` (live pins, a missing identity, one report not
  ready), and the active set.
