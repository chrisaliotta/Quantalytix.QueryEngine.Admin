# Mapping Sets & Bridges — Concepts and Frontend Prototype Requirements (rev. 2)

Brief for the v0 prototype. Part A explains what mapping sets and bridges are and how the platform uses them. Part B
specifies the prototype. It assumes mock data and no API; everything the real system validates server-side is described
as "show inline" so the prototype has a place for it.

**Revision 2 changes**

1. **No standalone Mappings or Bridges pages.** Mapping sets and bridges are managed *inside the dimension they belong
   to*, as two new sections of the dimension editor: **Mappings** and **Bridges**. A mapping set lives on its target
   dimension. A bridge appears on both of its endpoint dimensions, grouped as *Outgoing* (this dimension is the source)
   and *Incoming* (this dimension is the target).
2. **Report bridges are exposed through the report editor's Rows page**, where a mapped row source is configured. They
   have no separate page or list. In the dimension editor they appear read-only, with a link into the report.

---

# Part A — How mappings and bridges work

## A1. The problem they solve

A dimension holds the canonical members a business groups by: `Branch` has `BR01`, `BR02`, …; `Account` has a chart of
accounts. Datasets rarely store those keys. A loan system codes branches as `001`, the general ledger codes them as
`0100`, and a loan-level dataset knows its product but not which report line the product belongs on. Two mechanisms
turn raw or already-classified values into the members a query needs:

| Mechanism | Translates | From | To | Managed in |
|---|---|---|---|---|
| **Mapping set** | A raw source value | A dataset field's value, such as `001` | One member of **one** dimension | The target dimension's **Mappings** section |
| **Bridge** | An already-canonical member | A member of a **source** dimension | One or more members of a **target** dimension, with a factor and an allocation weight | The **Bridges** section of either endpoint dimension (analytical), or the report's **Rows** page (report bridges) |

A mapping set answers "what does code `001` mean in *this* source system?" A bridge answers "how do accounts roll into
margin statement lines?" or "how do products regroup into risk classes?"

## A2. Mapping sets

A **mapping set** is a named source vocabulary for one dimension: key, display name, `sourceNamespace` (a label for the
system the codes come from, such as `loan_servicing` or `gl`), and the target dimension. It exists because the same raw
code means different things in different systems: the seed maps GL branch `001` to `BR02` through `finance.branch` and
loan branch `001` to `BR01` through `loan.branch`. Several datasets that share a source system share one mapping set.

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

- **Analytical bridges** connect two analytical dimensions. They are created and edited in the **Bridges** section of
  either endpoint dimension. The same bridge shows on both dimensions.
- **Report bridges** lead from an analytical dimension into a **report's own row dimension**. Each belongs to exactly
  one report source, is always `many_to_many` + `replicate`, may use `descendants` rules, and is created and edited
  only in the **report editor's Rows page**, as part of configuring a mapped row source. The source dimension's
  Bridges section lists them read-only with "Open in report editor".

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

Add mapping set and bridge management to the **dimension editor**, and show report bridges on the **report editor's
Rows page**. Covered: per-dimension lists, create flows, drill-in editors with version switching, entry and mapping
editors with bulk paste, live allocation checks, usage views, and the read-only report-bridge state.

Out of scope: API calls, auth, tenants, and full report-bridge *authoring* in the report editor (the Rows page shows the
bridge and links to it; editing its rules is a follow-up).

Architecture follows the Datasets and Dimensions modules: a business contract (`MappingsApi` port in
`lib/mappings/contract.ts`), pure rules shared by the UI and the mock server (`lib/mappings/rules.ts`), an in-memory
mock adapter (`lib/mappings/mock/*`) that resolves members through the dimensions adapter, and SWR hooks plus a provider
(`lib/mappings/client.tsx`). To use a real backend, swap the adapter.

Reuse the existing patterns: the dimension editor's header row and left section sidebar, the version bar, sheets for
editors, dialogs for bulk paste and confirms.

## B2. Domain model (for mock data)

```ts
type VersionStatus = 'draft' | 'validated' | 'published' | 'retired';
type Cardinality = 'many_to_one' | 'many_to_many';
type AllocationMode = 'none' | 'partition' | 'replicate';
type SourceSelection = 'member' | 'descendants';

interface MappingSet { id: string; key: string; displayName: string; sourceNamespace: string; dimensionId: string; }
interface MappingVersion { id: string; mappingSetId: string; versionNumber: number; status: VersionStatus; publishedAt: string | null; }
interface MappingEntry { id: string; versionId: string; sourceKeyValue: string; memberKey: string; }

interface Bridge {
  id: string; key: string; displayName: string;
  sourceDimensionId: string; targetDimensionId: string; isEnabled: boolean;
  owner: { reportId: string; reportKey: string; sourceKey: string } | null;   // set for report bridges
}
interface BridgeVersion { id: string; bridgeId: string; versionNumber: number; status: VersionStatus; cardinality: Cardinality; allocationMode: AllocationMode; publishedAt: string | null; }
interface BridgeMapping { id: string; versionId: string; sourceMemberKey: string; targetMemberKey: string; factor: number; allocationWeight: number; sourceSelection: SourceSelection; }

/** Bindings that use a mapping set or bridge; drives Usage and delete blockers. */
interface BindingUsage { datasetKey: string; datasetVersion: number; fieldKey: string; roleKey: string; status: VersionStatus; }

/** Which assumption sets pin which version. */
interface AssumptionPin { assumptionSetKey: string; assumptionSetName: string; isActive: boolean; versionId: string; }
```

Dimensions and members come from the existing Dimensions module, not from a separate read-only slice. Report row
dimensions (`purpose: 'report'`) are hidden from the Dimensions list by default but can be resolved by ID, so report
bridges can name them.

### Mock data to seed

**Dimensions** (added to the Dimensions seed where missing): `Branch` (hierarchical, published), `Account`
(hierarchical with non-assignable folder nodes `ASSETS`, `LIABILITIES`, `REVENUE`, `EXPENSE`, `EQUITY`),
`AccountGroup`, `Product` (hierarchical), `RiskClass`, `CostCenter`, `Department` (categorical), `MarginStatement`
(`purpose: 'report'`), `Collateral` (categorical, **no published version**).

**Mapping sets**

- `loan.branch` — namespace `loan_servicing`, → `Branch`. v1 retired, v2 published (~40 entries), used by two dataset
  bindings. One entry's member key no longer exists in the published Branch version.
- `finance.branch` — namespace `gl`, → `Branch`. v1 published; maps `001` to a different branch than `loan.branch`.
  Used by one binding.
- `loan.product` — `loan_servicing` → `Product`. v1 published, v2 draft with one duplicate source key and one entry
  pointing at a folder member.
- `collateral.codes` — → `Collateral`. v1 draft; publish fails because the dimension has no published version.
- `legacy.riskgrade` — → `RiskClass`. v1 draft only, no bindings (deletable).

**Bridges**

- `gl_account_to_account_group` — `Account → AccountGroup`, v1 published, `many_to_one` / `none`, ~30 mappings, used
  by one bridge binding.
- `cost_center_to_department` — `CostCenter → Department`, v1 published `many_to_many` / `partition` with one source
  summing to 0.9 and one to 1.1; v2 draft with the sums fixed.
- `product_to_risk_class` — `Product → RiskClass`, v1 draft, `many_to_one` / `none`, one source with two targets; no
  bindings (deletable).
- `margin.gl_monthly` — `Account → MarginStatement`, **report bridge** owned by `bank.margin_statement` · source
  `gl_monthly`, `many_to_many` / `replicate`; `REVENUE` with `descendants` into Interest income, and a `-1` factor rule
  taking a cash account back out of Loans.
- A disabled analytical bridge (`isEnabled: false`).

**Assumption sets:** `active` pins every published version; `2024 baseline` (historical) pins `loan.branch` v1 and
`gl_account_to_account_group` v1.

## B3. Navigation

- **No `/mappings` or `/bridges` routes.** Both live in the dimension editor, which gets two new sections in its left
  sidebar: **Mappings** (icon `ArrowRightLeft`) and **Bridges** (icon `Split`/`GitMerge`), each with a count.
- Sections appear on analytical dimensions of every kind except `time`. Report row dimensions show no Mappings section;
  their bridges are reached from the report.
- Drilling into a mapping set or bridge stays inside the same section, following the editor convention: a back link
  returns to the section's list and no extra crumb bar is added. The selected set or bridge and its version persist in
  the URL alongside the existing `dim`, `version` and `tab` params, so links and reloads land on the same view.
- Default version: published, else latest draft.
- Wherever another dimension is named (a bridge's other endpoint), it links to that dimension's editor.
- **Report bridges** are shown on the report editor's **Rows** page, in each mapped row source card ("Report bridge
  `margin.gl_monthly` · Account → MarginStatement · n rules"). That line opens the bridge read-only in the source
  dimension's Bridges section.

## B4. Mappings section (dimension editor)

### B4.1 List

Lists the mapping sets that target this dimension. Header "Mapping sets", count, primary "New mapping set" (hidden on
`system`-origin dimensions where editing is locked).

Toolbar: search (key, name, namespace); Namespace filter (from the data); Status filter: Has draft · Published · No
published version.

Columns: Name (display name, key beneath in mono) · Namespace (mono badge) · Entries (count in the published version) ·
Published (v{n} · date; "—") · Draft (v{n} badge) · Bound by (count; tooltip lists the bindings) · row actions: Open,
New draft, Delete.

No Dimension column or Group-by-dimension toggle is needed: the section is already scoped to one dimension, which
replaces the original "see every vocabulary feeding Branch together" grouping.

Empty state: "A mapping set translates one source system's codes into the members of this dimension."

### B4.2 Create flow

Dialog or sheet with Key (mono; "A letter, then letters, digits or underscores. Fixed once created."), Display name,
and Source namespace (text with suggestions from existing namespaces). The dimension is implied by context. If it has
no published version, warn inline: "Entries cannot be validated until {dimension} publishes a version". Summary:
"Creates {name} with draft version 1 (empty)." Create drills into the new set's Entries.

Simulated validation: key pattern and uniqueness.

### B4.3 Mapping set editor (drill-in)

**Header:** back link to the list; display name; beneath it `key · namespace → {dimension}`. Actions: Edit (display
name, namespace; key and dimension read-only) and Delete (blocked per B7).

**Version bar:** the same component and rules as the dimension version bar. Pills use status colors. Actions depend on
status: a draft offers Validate/Publish/Delete; the published version shows "Live — edits apply immediately" plus New
draft; a retired version is read-only. A New version menu offers Empty or Clone v{n}, and only one draft can exist at a
time. Publish confirm: "Publish v2? v1 will be retired and every binding of {key} reads v2 at once."

**Resolution banner:** "Member keys resolve in **{dimension} v{n}** (published)". If the dimension has no published
version, an amber banner says so and notes the set cannot be published.

**Content:** Entries, Coverage and Usage, in that order on one scrolling surface (or as inner tabs).

### B4.4 Entries

Identical to rev. 1: search, count, and, when the version is editable, Bulk paste, New entry and Export
(`source_key,member_key`). The table columns are Source key (mono, whitespace preserved), Member key, Member label (a
destructive "Not a member of the published version" marker, or a "folder — not assignable" warning) and a delete
action. Clicking a row opens the entry sheet. Source keys are never trimmed. The member combobox covers assignable
members, grouped under their parents, with folders shown disabled.

Inline checks: duplicate source key ("already maps to {member}"), member not in the published version, member not
assignable.

**Bulk paste:** a textarea prefilled with the current entries, accepting comma or tab separators and `#` comments. A
live preview shows New / Unchanged / Changed / Removed / Error rows with summary counts. A "Keep existing entries not
in the paste" toggle switches between merge and replace. Apply is disabled while any errors remain.

**Coverage:** "Members without any source value" lists published assignable members that no entry maps to.

### B4.5 Usage

Two cards: **Dataset bindings** (dataset, version, status, field, role) and **Assumption sets** ("active → v2",
"2024 baseline → v1"). Empty state: "No binding uses this mapping set yet — it can be deleted."

## B5. Bridges section (dimension editor)

### B5.1 List

Two tables:

- **Outgoing** — bridges whose *source* is this dimension. The "To" column links to the target dimension.
- **Incoming** — bridges whose *target* is this dimension. The "From" column links to the source dimension.

The same analytical bridge appears on both of its endpoints. Columns: Name (display name, key beneath) · Other endpoint
(link) · Type (Analytical / Report badge; on hover, the report badge names the owning report) · Settings of the
published version (`many_to_one · none` badges) · Mappings (count) · Published · Draft · Enabled dot · actions (Open;
New draft and Delete for analytical bridges only).

Report bridges appear in **Outgoing** on their source dimension, with a muted row style and an "Open in report editor"
affordance. They never appear as Incoming on the report row dimension, because that dimension has no editor section
for them.

Primary action "New bridge" (analytical only).

### B5.2 Create flow

A dialog whose direction toggle reads "From this dimension" or "Into this dimension". This dimension fills one endpoint,
and the user picks the other with a combobox over analytical dimensions, excluding `time`, `report` and this dimension
itself, and showing whether each has a published version. The dialog also takes Key, Display name and Enabled (on by
default). Create makes the bridge with an **empty `many_to_one` / `none` draft v1** and drills into it. Cardinality and
allocation are then set on that draft's Settings card (B5.5). There, `none` is disabled under many-to-many, and
`partition` and `replicate` are disabled under many-to-one, each with an explanation.

(Rev. 1 created the bridge without a version and opened a separate New version dialog. Rev. 2 always starts with
draft v1, so there is no extra step.)

### B5.3 Bridge editor (drill-in)

**Header:** back link; display name; beneath it `key · Source → Target`, where each endpoint links to its dimension.
An analytical bridge shows an Enabled badge, Edit (display name, enabled) and Delete. A report bridge shows the badge
"Report bridge of {reportKey} · {sourceKey}" and an "Open in report editor" button, with no Edit, Delete or version
actions.

**Version bar:** as in B4.3; the pills also show the settings (`v1 · many_to_one · none`).

**Resolution banner:** "Source keys resolve in **Account v3**, target keys in **AccountGroup v1**". The banner turns
amber when either endpoint has no published version.

**Content:** Mappings, Settings and Usage.

### B5.4 Mappings

**Allocation summary strip** as in rev. 1. For `none` it reads "one target, weight 1" with a violation count. For
`partition` it is green when every weight set sums to 1; otherwise it is red, listing each offending source (e.g.
"CC_100 (0.9)") as a link that filters the table. For `replicate` it reads "Totals are not additive". Under
many-to-one it lists every source that has more than one target.

**Toolbar:** search, count and, when editable, Bulk paste, New mapping and Export. A **Group by source** toggle (one
row per source, with its targets and weight sum) is a nice-to-have.

**Table:** Source member (gets an "and every member under it" badge for `descendants`) · Target member · Factor ·
Allocation weight · Source weight sum (partition only; red when it is not 1) · delete. Missing keys show the
destructive marker.

**Mapping sheet:** Source member and Target member (pickers over each endpoint's published members). Factor defaults
to 1 ("Signed coefficient, such as -1 for sign normalization"). Allocation weight defaults to 1 and is locked to 1
under `none`. An "Include descendants" switch (`sourceSelection = descendants`) appears only when the source member
is a folder node; report bridges show it read-only. Inline checks: duplicate pair, a second target under many-to-one,
a weight other than 1 under none, a negative weight.

**Bulk paste:** `source_key,target_key[,factor[,weight]]`, with the same preview as B4.4. Allocation and cardinality
are checked across the whole pasted result.

**Report bridge view:** read-only, with the note "Items are edited in the report editor. A `descendants` rule maps the
member and every member under it, including members added later." `descendants` rows expand to show the members they
currently cover.

**Factor visualization (nice-to-have):** select a target to list each contributing source with its factor and weight,
e.g. `CASH_1010 × -1`.

### B5.5 Settings

Cardinality and allocation of the selected version. They can be edited **only on a draft**. On the published version,
the card points the user to "Start a new draft to change them", because changing the mode in place could invalidate
live queries. A change re-runs the allocation summary and flags mappings that no longer comply. Version facts are
shown below.

### B5.6 Usage

Two cards: **Dataset bindings** (dataset, version, source role → target role) and **Assumption sets**. A report bridge
shows its owning report and source instead. Empty state: "No binding uses this bridge yet — it can be deleted."

## B6. Report bridges on the Rows page

- In the report editor's Rows tab, every row source card that maps through a report bridge shows a compact line:
  "Report bridge `{key}` · {Source} → {Target} · n rules · v{n}". The line shows regardless of advanced mode, because
  this is the only place report bridges surface in the report flow.
- Clicking the line opens the bridge read-only in the source dimension's Bridges section.
- Creating and editing report-bridge rules happens while configuring a row's mapping in the report editor. That
  authoring UI is out of scope for this prototype; the data model and read path are in place for it.

## B7. Simulated validation (validate / publish)

Validate or Publish runs the A5 checks client-side and lists every violation, each linking to (or filtering to) the
offending entry or mapping.

Mapping set: duplicate source keys; dimension has no published version; member key not in it; member not assignable.

Bridge: an endpoint has no published version; a key not in its version; many-to-one with a second target; `none` with
a second target or a weight other than 1; `partition` sums other than 1 or a negative weight; `replicate` with a
negative weight.

On success: transition the status, retire the previous published version, repoint the `active` pin, and show a toast.

## B8. Delete blockers

A mapping set or bridge can be deleted only when every version is a draft and no binding uses it. Report bridges never
offer Delete. When deletion is blocked, the confirm dialog gives the reasons:

> Mapping set 'loan.branch' cannot be deleted: it has non-draft versions (v1 retired, v2 published); it is used by
> bindings ('loans.details' v3 · servicingBranch, 'loans.details' v3 · originationBranch).

> Bridge 'gl_account_to_account_group' cannot be deleted: it has non-draft versions (v1 published); it is used by
> bindings ('bank.balance_monthly' v2 · accountGroup).

Deleting an entry or mapping is always allowed on an editable version and uses a plain confirm.

## B9. Interaction details

- Destructive actions confirm; every mutation toasts; applying a bulk paste toasts the counts.
- Sheets prompt before closing when they have unsaved changes.
- Read-only contexts render text, not disabled inputs.
- Tables handle 5,000 rows (paginate or virtualize); search is debounced.
- Source key inputs never trim or normalize, and a subtle marker flags leading or trailing whitespace and leading
  zeros.
- Member pickers show both key and label, search both, and group hierarchical members under their parents.

## B10. Visual conventions

- Sheets for editors and Dialogs for bulk paste and confirms; Badge, Table, Combobox, Tooltip and Sonner toasts.
- Keys and source values in mono. Status colors: draft amber, validated blue, published green, retired gray.
- Icons: mapping set `ArrowRightLeft`; bridge `Split`/`GitMerge`; report bridge `FileText`; allocation `PieChart`
  (partition), `Copy` (replicate), `ArrowRight` (none); warning `TriangleAlert`.

## B11. Deliverables

- Mappings and Bridges sections in the dimension editor, backed by the `MappingsApi` contract, the mock adapter and
  SWR hooks.
- All sheets, bulk paste dialogs and confirms wired to the store.
- The report-bridge line on the Rows page source cards.
- Demo paths, opened from the dimension editor (the Dimensions dev jump menu leads to each dimension):
  - **Branch → Mappings:** `loan.branch` v2 (missing-member entry), `finance.branch`.
  - **Product → Mappings:** `loan.product` v2 draft (publish violations).
  - **Collateral → Mappings:** `collateral.codes` (no published dimension).
  - **RiskClass → Mappings:** `legacy.riskgrade` (deletable).
  - **CostCenter → Bridges:** `cost_center_to_department` v1 (partition sums wrong) and v2 draft (fixed).
  - **Product → Bridges:** `product_to_risk_class` (many-to-one violation, deletable).
  - **Account → Bridges:** `gl_account_to_account_group`, and `margin.gl_monthly` (report bridge, read-only,
    descendants and −1 factor). The last is also reachable from the Bank margin report's Rows page.
