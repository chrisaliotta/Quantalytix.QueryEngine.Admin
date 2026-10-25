# Mapping Sets & Bridges — Concepts and Frontend Prototype Requirements

Brief for a v0 prototype. Part A explains what mapping sets and bridges are and how the platform uses them. Part B
specifies the prototype. It assumes mock data and no API; everything the real system validates server-side is described
as "show inline" so the prototype has a place for it.

---

# Part A — How mappings and bridges work

## A1. The problem they solve

A dimension holds the canonical members a business groups by: `Branch` has `BR01`, `BR02`, …; `Account` has a chart of
accounts. Datasets rarely store those keys. A loan system codes branches as `001`, the general ledger codes them as
`0100`, and a loan-level dataset knows its product but not which report line the product belongs on. Two mechanisms
turn raw or already-classified values into the members a query needs:

| Mechanism | Translates | From | To | Lives on |
|---|---|---|---|---|
| **Mapping set** | A raw source value | A dataset field's value, such as `001` | One member of **one** dimension | The dimension it targets |
| **Bridge** | An already-canonical member | A member of a **source** dimension | One or more members of a **target** dimension, with a factor and an allocation weight | The target dimension it leads into; its source dimension lists it read-only |

A mapping set answers "what does code `001` mean in *this* source system?" A bridge answers "how do accounts roll into
margin statement lines?" or "how do products regroup into risk classes?"

## A2. Mapping sets

A **mapping set** is a named source vocabulary for one dimension: key, display name, `sourceNamespace` (a label for the
system the codes come from, such as `loan_servicing` or `gl`), and the target dimension. It exists because the same raw
code means different things in different systems: the seed maps GL branch `001` to `BR02` through `finance.branch` and
loan branch `001` to `BR01` through `loan.branch`. Several datasets that share a source system share one mapping set.
A mapping set has exactly one dimension, so it is created and listed on that dimension's page, on its Mappings tab.

A mapping set has **versions**. A version holds **entries**, each pairing one exact `sourceKeyValue` with one
`memberKey` of the dimension. Rules:

- A source value appears in at most one entry of a version; many source values may map to the same member.
- Values compare exactly as text. `003000` and `3000` are different source values; the platform never coerces.
- An entry's member must be an **assignable** member of the dimension's **published** version. Folder-only nodes
  (`isAssignable = false`) may not receive source values.
- The member is named by key, not by ID, so the entry survives a new dimension version as long as the key does.

A dataset uses a mapping set through a **mapped binding**: "field `branch_code` of `loans.details` v3 classifies rows
into role `servicingBranch` through mapping set `loan.branch`." Rows whose value has no entry follow the binding's
unmapped behavior: group under null, be excluded, or route to an unknown member.

## A3. Bridges

A **bridge** connects two dimensions: key, display name, source dimension, target dimension, enabled flag. Both
dimensions are fixed at creation. A bridge has **versions**; a version carries two settings and a list of mappings.

**Version settings**

| Setting | Values | Meaning |
|---|---|---|
| `cardinality` | `many_to_one`, `many_to_many` | Whether a source member may map to more than one target |
| `allocationMode` | `none`, `partition`, `replicate` | What a source member's value does when it reaches several targets |

| Allocation | Rule | Use |
|---|---|---|
| `none` | Each source maps to at most one target; weight is exactly 1 | Pure regrouping |
| `partition` | A source may map to several targets; weights are ≥ 0 and **sum to exactly 1** per source | Splitting a cost center across departments |
| `replicate` | A source counts **fully** in every target; weights ≥ 0, no sum constraint; results are intentionally non-additive | Report rows where an item belongs on several lines |

**Mappings**

Each mapping is `sourceMemberKey → targetMemberKey` with:

- `factor` — a signed coefficient applied to the source's value, such as `-1` to flip the sign of liability balances,
  or `0.5` to take half. It changes only measures that opt in (`bridge_factor_behavior = value`).
- `allocationWeight` — the ownership share under `partition`; 1 under `none`; usually 1 under `replicate`.
- `sourceSelection` — `member` (default: this one member) or `descendants` (this member and everything under it in
  the source hierarchy, including members added later). `descendants` is only used by **report bridges**.

Each `(source, target)` pair appears once per version. Member keys resolve in the published versions of both
dimensions.

A dataset uses an analytical bridge through a **bridge binding**: "the `account` binding of `bank.balance_monthly`
remaps through bridge `gl_account_to_account_group` into role `accountGroup`." No second mapping is needed because the
dataset is already classified into `Account`.

## A4. Analytical versus report bridges

Two kinds of bridges share the tables but are managed differently:

- **Analytical bridges** connect two analytical dimensions and are created and edited on the Bridges tab of their
  **target** dimension, each on its own page under that dimension.
- **Report bridges** lead from an analytical dimension into a **report's own row dimension**. Each belongs to exactly
  one report source, is always `many_to_many` + `replicate`, may use `descendants` rules, and is created and edited
  only in the report editor. A report bridge has no page and no list of its own: it appears read-only in the "From"
  section of its **source** dimension's Bridges tab, with a link to the report editor.

A bridge's home is its target, because a dataset reaches the target's role through the bridge, and report bridges
already live with their target, the report. The source dimension lists its bridges read-only so that whoever edits its
members sees which bridges read them.

## A5. Versions, publishing and the assumption set

Mapping sets and bridges are "living" objects, exactly like dimensions:

- Statuses `draft → validated → published → retired`. At most one version is published.
- The **published version is editable in place** and every edit is live on the next query.
- Publishing a new version retires the previous one and repoints the **active assumption set**, the bundle of pins
  every query reads through unless it names a historical snapshot.
- No dataset version or release is touched when a mapping or bridge changes. Bindings name the mapping set or bridge
  by identity, never by version.
- A historical assumption set keeps the version it captured, so a backtest can rerun under last year's mapping.

**Validation on validate/publish**

| Mapping set | Bridge |
|---|---|
| `source_key_unique` — each source value once | `endpoint_versions_published` — both dimensions have a published version |
| `dimension_version_published` — the dimension has a published version | `mapping_members_in_endpoint_versions` — every key exists in those versions |
| `entry_member_in_version` — every member key exists in it | `cardinality_many_to_one` — at most one target per source |
| `entry_member_assignable` — every member is assignable | `allocation_none` — one target, weight exactly 1 |
| | `allocation_partition` — weights ≥ 0 and sum to 1 per source |
| | `allocation_replicate` — weights ≥ 0 |

**Deleting**

A mapping set or bridge can be deleted only when every version is a draft and no dataset binding uses it. Drafts are
deleted freely. The published version is never deleted by hand.

## A6. How a query uses them

When a query groups by a role, the engine finds each row's member: by member key for a direct binding, through the
pinned mapping version's entries for a mapped binding, through the pinned bridge version's entries (after resolving the
source binding) for a bridge binding. Unmapped rows follow the binding's unmapped behavior. A bridge's factor and
allocation weight ride along into the measure computation. Filtering by a dimension that a bridge can send to several
members is refused, because the row would not have one answer.

---

# Part B — Frontend prototype requirements

## B1. Purpose and scope

Build the mapping set and bridge screens, which live under the dimensions: a dimension's Mappings and Bridges tabs,
create flows, detail pages with version switching, entry and mapping editors with bulk paste, live allocation checks,
usage views, and the read-only rows of the bridges that start from a dimension, report bridges included.

Out of scope: API calls, auth, tenants, dimension editing (the dimension page and its other tabs come from the
dimension manager prototype), report editor.
Mock data and an in-memory store; mutations update the store.

Reuse existing design patterns from current screens that have been created.

## B2. Domain model (for mock data)

```ts
type VersionStatus = 'draft' | 'validated' | 'published' | 'retired';
type Cardinality = 'many_to_one' | 'many_to_many';
type AllocationMode = 'none' | 'partition' | 'replicate';
type SourceSelection = 'member' | 'descendants';

/** A read-only slice of the dimension manager's data, enough to label and pick members. */
interface DimensionRef { id: string; key: string; displayName: string; kind: 'categorical'|'hierarchical'|'range'|'time'; publishedVersionId: string | null; }
interface MemberRef { dimensionId: string; key: string; displayName: string; parentKey: string | null; isAssignable: boolean; }

interface MappingSet { id: string; key: string; displayName: string; sourceNamespace: string; dimensionId: string; }
interface MappingVersion { id: string; mappingSetId: string; versionNumber: number; status: VersionStatus; publishedAt: string | null; }
interface MappingEntry { id: string; versionId: string; sourceKeyValue: string; memberKey: string; }

interface Bridge {
  id: string; key: string; displayName: string;
  sourceDimensionId: string; targetDimensionId: string; isEnabled: boolean;
  owner: { reportId: string; reportKey: string; reportSourceId: string; sourceKey: string } | null;   // set for report bridges
}
interface BridgeVersion { id: string; bridgeId: string; versionNumber: number; status: VersionStatus; cardinality: Cardinality; allocationMode: AllocationMode; publishedAt: string | null; }
interface BridgeMapping { id: string; versionId: string; sourceMemberKey: string; targetMemberKey: string; factor: number; allocationWeight: number; sourceSelection: SourceSelection; }

/** Bindings that use a mapping set or bridge; drives Usage and delete blockers. */
interface BindingUsage { datasetKey: string; datasetVersion: number; fieldKey: string; roleKey: string; status: VersionStatus; }

/** Which assumption sets pin which version. */
interface AssumptionPin { assumptionSetKey: string; assumptionSetName: string; isActive: boolean; versionId: string; }
```

### Mock data to seed

**Dimensions (read-only refs):** `Branch` (hierarchical, published), `Account` (hierarchical with folder nodes
`ASSETS`, `LIABILITIES`, `REVENUE`, `EXPENSE`, `EQUITY` that are not assignable), `AccountGroup` (categorical),
`Product` (hierarchical), `RiskClass` (categorical), `CostCenter` (categorical), `Department` (categorical),
`MarginStatement` (a report row dimension: `purpose: 'report'`), `Collateral` (categorical, **no published version**).

**Mapping sets**

- `loan.branch` — namespace `loan_servicing`, → `Branch`. v1 retired, v2 published (~40 entries), used by two dataset
  bindings. Seed one entry whose member key no longer exists in the published Branch version, to show the "not a
  member" marker.
- `finance.branch` — namespace `gl`, → `Branch`. v1 published; maps `001` to a different branch than `loan.branch`
  does. Used by one binding.
- `loan.product` — namespace `loan_servicing`, → `Product`. v1 published, v2 draft with one duplicate source key and
  one entry pointing at a folder member, to exercise validation.
- `collateral.codes` — → `Collateral`. v1 draft; publishing must fail because the dimension has no published version.
- `legacy.riskgrade` — → `RiskClass`. v1 draft only, no bindings (deletable).

**Bridges**

- `gl_account_to_account_group` — `Account → AccountGroup`, analytical, v1 published, `many_to_one` / `none`, ~30
  mappings. Used by one bridge binding.
- `cost_center_to_department` — `CostCenter → Department`, analytical, v1 published `many_to_many` / `partition`;
  seed one source whose weights sum to 0.9 and one to 1.1. v2 draft cloned from v1 with the sums fixed.
- `product_to_risk_class` — `Product → RiskClass`, analytical, v1 draft only, `many_to_one` / `none`, no bindings
  (deletable). Seed a mapping with a second target to trip `cardinality_many_to_one`.
- `margin.gl_monthly` — `Account → MarginStatement`, **report bridge** owned by report `bank.margin_statement`, source
  `gl_monthly`, `many_to_many` / `replicate`; rules include `REVENUE` with `descendants` into the Interest income
  line, and a `-1` factor rule taking a cash account back out of Loans. It has no page: it shows read-only in the
  From section of `Account`'s Bridges tab.
- A disabled analytical bridge (`isEnabled: false`).

**Assumption sets:** `active` pins every published version; `2024 baseline` (historical) pins `loan.branch` v1 and
`gl_account_to_account_group` v1.

## B3. Navigation

Mapping sets and bridges have no top-level pages and no sidebar entries of their own: there is no standalone mapping
set or bridge list. They live under the dimension pages of the dimension manager prototype.

- `/dimensions/:dimensionId?version=&tab=mappings` — the dimension's Mappings tab (B4.1); "New mapping set" opens a
  sheet.
- `/dimensions/:dimensionId?version=&tab=bridges` — the dimension's Bridges tab (B5.1); "New bridge" opens a sheet.
- `/dimensions/:dimensionId/mapping-sets/:mappingSetId?version=&tab=` — a mapping set's page, under the dimension its
  codes map onto.
- `/dimensions/:dimensionId/bridges/:bridgeId?version=&tab=` — an analytical bridge's page, under the dimension it
  leads into, its target.
- Version and tab persist in the URL. Default version: published, else latest draft. On a mapping set or bridge page,
  `?version=` selects its own version. The Mappings and Bridges tabs do not follow the dimension's version picker:
  mapping sets and bridges have versions of their own, which assumption sets pin separately.
- Breadcrumbs: Dimensions › {dimension} › {mapping set or bridge}.
- Wherever another dimension is named, such as a bridge's source, link to its page (`/dimensions/:id?version=`);
  dead links are acceptable.

## B4. Mapping sets

### B4.1 The dimension's Mappings tab

The dimension page's Mappings tab lists that dimension's mapping sets, so every vocabulary feeding `Branch` shows
together on `Branch`'s page. It shows the same sets whichever dimension version is selected, with the note "A mapping
set maps one source system's codes onto this dimension's members. Its versions are its own, and the assumption set
pins them apart from the dimension's."

Toolbar: search (key, name, namespace); Namespace filter (chips from the data); Status filter: Has draft · Published ·
No published version; count; primary "New mapping set".

Columns: Name (display name, key beneath in mono) · Namespace (mono badge) · Entries (count in published version) ·
Published (v{n} · date; "—") · Draft (v{n} badge) · Bound by (count of bindings; hover lists them) · row actions: Open,
New draft, Delete. A row opens the mapping set's page.

Empty state explains: "A mapping set translates one source system's codes into the members of this dimension."

### B4.2 Create flow

A sheet opened from the Mappings tab. The dimension is the page's and is not asked for:

- Key (mono; helper "A letter, then letters, digits or underscores. Fixed once created."), Display name.
- Source namespace (text with suggestions from existing namespaces; helper "The system the codes come from, such as
  loan_servicing or gl").
- When the dimension has no published version, warn inline: "Entries cannot be validated until {dimension} publishes
  a version".
- Summary: "Creates {name} for {dimension} with draft version 1 (empty)."
- Create → the mapping set's page, Entries tab.

Simulated validation: key pattern and uniqueness.

### B4.3 Detail page

The page sits under its dimension (`/dimensions/:dimensionId/mapping-sets/:mappingSetId`), with the breadcrumb
Dimensions › {dimension} › {mapping set}.

**Header:** display name; beneath `key · namespace → dimension`. Actions: Edit (identity sheet: display name,
namespace; key and dimension read-only), Delete (blocked per B7).

**Version bar:** same component and rules as the dimension manager: pills with status colors, actions by status
(draft: Validate/Publish/Delete; published: "Live — edits apply immediately" + New draft; retired: read-only), New
version split button (Empty / Clone v{n}), one draft at a time. Publish confirm: "Publish v2? v1 will be retired and
every binding of {key} reads v2 at once."

**Resolution banner** under the version bar: "Member keys resolve in **Branch v2** (published) · Open dimension". When
the dimension has no published version: an amber banner "Branch has no published version yet, so member keys resolve
in none. This set cannot be published."

**Tabs:** Entries · Usage · Settings (Settings is minimal: version facts; may be omitted).

### B4.4 Entries tab

Toolbar: search (source value, member key, member label), entry count, and when editable: **Bulk paste**, **New
entry**, **Export** (download/copy the entries as `source_key,member_key` lines).

Table columns: Source key (mono, whitespace preserved) · Member key (mono) · Member label (from the published
dimension version; when the key is missing there, show "Not a member of the published version" in destructive text
with a warning icon; when the member is a folder, show a "folder — not assignable" warning) · actions (Delete).

Row click opens the **entry sheet**: Source key (text; mono; trim nothing — leading zeros and spaces matter; helper
"Compared exactly as text"), Member (searchable combobox over the published version's **assignable** members, showing
key and label, grouped under their parent for hierarchical dimensions; folder members are visible but disabled with
the reason). Save / Cancel. Read-only contexts render text.

**Inline checks as the user types or pastes** (the prototype's visible validation):

- Duplicate source key in this version → row and sheet field flagged "already maps to {member}".
- Member key not in published version → flagged.
- Member not assignable → flagged.

**Bulk paste dialog:** a textarea prefilled with the current entries as `source_key,member_key` lines (one per line;
comma or tab separated; `#` comments ignored). Below it a live **preview table**: parsed rows with a status column
(New / Unchanged / Changed → shows old member / Removed / Error: duplicate, unknown member, folder member). Counts
summary ("12 new, 3 changed, 1 removed, 2 errors"). Apply replaces the whole version's entries; disabled while errors
exist. A "Keep existing entries not in the paste" toggle switches between replace and merge semantics.

**Coverage panel (nice-to-have):** a card "Members without any source value" listing published assignable members no
entry maps to, so a user sees gaps.

### B4.5 Usage tab

Cards: **Dataset bindings** (dataset, version, status, field, role; link), **Assumption sets** (which set pins which
version; "active → v2", "2024 baseline → v1"). Empty state: "No binding uses this mapping set yet — it can be deleted."

## B5. Bridges

### B5.1 The dimension's Bridges tab

The dimension page's Bridges tab has two sections, per A4. Like the Mappings tab, it shows the same bridges whichever
dimension version is selected.

**Into {dimension}** — the bridges whose target is this dimension, which are its own. Note: "Each bridge remaps another
dimension's members onto this one's, with factors and allocation weights, so a dataset classified by the other
dimension can be read by this one. Its versions are its own."

- Toolbar: search; Source dimension filter; Status filter; count; primary "New bridge".
- Columns: Name (display name, key beneath) · From (the source dimension, a link) · Settings of the published version
  (`many_to_one · none` as two small badges) · Mappings (count) · Published · Draft · Enabled dot · actions (Open, New
  draft, Delete). A row opens the bridge's page.

**From {dimension}** — read-only, the bridges whose source is this dimension, report bridges included. Note: "Bridges
that read this dimension's members, including the bridges of report sources. They change on their target's page or in
their report's editor, but moving or removing a member here changes what they read."

- Columns: Name (display name, key beneath) · Into (the target dimension; for a report bridge "Report {reportKey} ·
  {sourceKey}") · Enabled dot. No actions.
- An analytical bridge's row opens its page under its target dimension. A report bridge's row renders with a muted
  style and an `ExternalLink` icon on hover, and opens the report editor on its source
  (`/reports/:reportId?source=:reportSourceId`).

### B5.2 Create flow

A sheet opened from the Into section. The target is the page's dimension and is not asked for:

- Key, Display name.
- Source dimension (combobox over analytical dimensions, not `time`; must differ from the target; shows
  published-version state).
- Enabled (default on).
- Note: "The bridge starts without a version. Add v1 to choose cardinality and allocation and enter mappings."
- Create → the bridge's page, with the **New version** dialog opened immediately.

Report dimensions have no dimension page, so no sheet creates a bridge into one; a report's own bridges are created in
its editor.

### B5.3 New version dialog

Fields: Cardinality (radio: Many to one / Many to many with one-line explanations), Allocation mode (radio: None /
Partition / Replicate with the rule text from A3; `none` is disabled when cardinality is many-to-many, and `partition`
and `replicate` are disabled when many-to-one, with tooltips), Clone from (optional: a version to copy mappings from;
prefilled when opened via Clone). Create draft.

### B5.4 Detail page

The page serves analytical bridges only and sits under its target dimension
(`/dimensions/:targetDimensionId/bridges/:bridgeId`), with the breadcrumb Dimensions › {target} › {bridge}.

**Header:** display name; beneath `key · Source → Target`. Enabled badge, Edit (display name, enabled), Delete.

**Version bar:** as in B4.3; pills additionally show the version's settings (`v1 · many_to_one · none`).

**Resolution banner:** "Source keys resolve in **Account v3**, target keys in **AccountGroup v1**" with links; amber
when either lacks a published version.

**Tabs:** Mappings · Settings · Usage.

### B5.5 Mappings tab

**Allocation summary strip** at top, depending on the version's mode:

- `none`: "Each source maps to one target with weight 1." plus a count of violations if any.
- `partition`: "Every source member's weights sum to 1." in green, or in red "Partition needs each source's weights to
  sum to 1. These do not: CC_100 (0.9), CC_200 (1.1)." Each named source is a link that filters the table.
- `replicate`: "Each source counts fully in every target it maps to. Totals are not additive."
- Cardinality `many_to_one`: a violation list of sources with more than one target.

**Toolbar:** search, mapping count, and when editable: Bulk paste, New mapping, Export. A **Group by source** toggle
that collapses the table into one row per source member with its targets listed beneath and the weight sum shown.

**Table columns:** Source member (key mono + label) · Target member (key + label) · Factor (`1`, `-1`, `0.5`) · Allocation weight · Source weight sum (partition only; red when
≠ 1) · actions (Delete). Missing keys render the destructive "Not a member of the published version" marker.

**Mapping sheet:** Source member (combobox over the source dimension's published members; hierarchical dimensions
render a tree picker), Target member (same for the target), Factor (number, default 1; helper "Signed coefficient of
the source's values, such as -1 for sign normalization"), Allocation weight (number, default 1; helper text by mode as
in A3; read-only and fixed to 1 under `none`). An analytical bridge maps each source member alone, so the sheet has
no source selection. Inline checks: duplicate pair, second target under many-to-one, weight ≠ 1 under none, negative
weight.

**Bulk paste dialog:** lines `source_key,target_key[,factor[,weight]]`; defaults factor 1 and weight 1. Same live
preview and status column as B4.4, with the allocation and cardinality checks applied to the pasted result as a whole
(the preview's summary shows "3 sources whose weights do not sum to 1").

**Factor visualization (nice-to-have):** for a selected target, a side panel listing contributing sources with their
factors and weights and the resulting coefficient per source, e.g. `CASH_1010 × -1`, so the "take it back out"
pattern is visible.

### B5.6 Settings tab

Cardinality and allocation mode of the selected version (editable on a draft or the published version; changing the
mode re-runs the allocation summary and flags mappings that no longer comply, with a confirm "Switching to `none` will
flag 4 mappings"). Version facts.

### B5.7 Usage tab

Cards: **Dataset bindings** (bridge bindings that use it: dataset, version, source binding role → target role),
**Assumption sets** pins. Empty state: "No binding uses this bridge yet — it can be deleted."

## B6. Simulated validation (validate / publish)

On Validate or Publish, run the checks from A5 client-side and show a results panel listing every violation with a
link that opens the offending entry or mapping (or filters the table to it):

Mapping set: duplicate source keys; dimension has no published version; member key not in it; member not assignable.

Bridge: an endpoint has no published version; a source or target key not in its version; many-to-one with a second
target; `none` with a second target or a weight ≠ 1; `partition` sums ≠ 1 or a negative weight; `replicate` with a
negative weight.

On success, transition the status, retire the previous published version, repoint the `active` pin in the mock
assumption sets, and toast.

## B7. Delete blockers

A mapping set or bridge may be deleted only when every version is a draft and no binding uses it. After a delete, the
app returns to the dimension's Mappings tab for a mapping set, or to the target dimension's Bridges tab for a bridge.
Report bridges have no page here; they are deleted with their report source, in the report editor. The confirm dialog
shows reasons when blocked:

> Mapping set 'loan.branch' cannot be deleted: it has non-draft versions (v1 retired, v2 published); it is used by
> bindings ('loans.details' v3 · servicingBranch, 'loans.details' v3 · originationBranch).

> Bridge 'gl_account_to_account_group' cannot be deleted: it has non-draft versions (v1 published); it is used by
> bindings ('bank.balance_monthly' v2 · accountGroup).

Deleting an entry or mapping is always allowed on an editable version and shows a plain confirm.

## B8. Interaction details

- Destructive actions confirm; every mutation toasts; bulk paste apply toasts the counts.
- Sheets prompt on close when dirty.
- Read-only contexts render text, not disabled inputs.
- Tables handle 5,000 entries: virtualize or paginate; search is debounced.
- Source key inputs never trim or normalize; show a subtle marker for leading/trailing whitespace and leading zeros
  so users see what will be compared.
- Member pickers show key and label, search both, and group hierarchical members under their parents.

## B9. Visual conventions

- shadcn: Sheet for editors, Dialog for bulk paste and confirms, Tabs, Badge, Table, Command/Combobox, Tooltip, Sonner.
- Keys and source values in mono. Status colors: draft amber, validated blue, published green, retired gray.
- Icons: mapping set `ArrowRightLeft` or `BookA`; bridge `GitMerge`; report bridge `FileText`; allocation
  `PieChart` (partition), `Copy` (replicate), `ArrowRight` (none); warning `TriangleAlert`.

## B10. Deliverables from v0

- The dimension page's Mappings and Bridges tabs and the routable mapping set and bridge pages under it, with a shared
  mock data module (dimensions, members, assumption sets) and an in-memory store.
- All sheets, bulk paste dialogs and confirms wired to the store.
- A dev menu to jump to: `loan.branch` v2 (published, with a missing-member entry), `loan.product` v2 draft (publish
  violations), `collateral.codes` (no published dimension), `legacy.riskgrade` (deletable), `cost_center_to_department`
  v1 (partition sums wrong) and v2 draft (fixed), `product_to_risk_class` (many-to-one violation, deletable),
  `Account`'s Bridges tab (`margin.gl_monthly`, a report bridge, read-only in its From section).
