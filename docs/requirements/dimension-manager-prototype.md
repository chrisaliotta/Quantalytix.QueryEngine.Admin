# Dimension Manager & Editor — Frontend Prototype Requirements

Brief for a v0 prototype. It assumes mock data and no API; everything the real system validates server-side is
described as "show inline" so the prototype has a place for it.

## 1. Purpose and scope

Build a prototype of the dimension manager for a financial analytics administration app. A **dimension** is anything a
query can group or filter by: branches, products, risk grades, balance bands, dates, plan scenarios. Users create
dimensions, define their members (flat lists or hierarchies), assign range bands, and name roles. Some dimensions are
**system dimensions**, required by product modules and locked to varying degrees; they are managed in a separate area
from user dimensions.

Dimensions come in four kinds, which are the kinds the platform already defines:

| Kind | Meaning | Content |
|---|---|---|
| `categorical` | A flat list of named members | Members, optional levels |
| `hierarchical` | A tree of members; parent/child grouping and drill-down are first-class | Members with parents, levels |
| `range` | Members assigned from ordered, non-overlapping bands over a decimal, date or date-time value | Members, bands |
| `time` | A time axis; periods are derived from a dataset's date field, so members are rarely needed | Levels (day, month, quarter, year); members only for custom calendars |

Literal and calculated groupings are not dimensions: they are groupable fields defined on a dataset. The manager
points users there and does not create them.

In scope: list page split into user and system dimensions, create flow, detail page with tabs (Members, Levels, Range
bands, Roles, Usage, Settings, Mappings, Bridges), member editor with typed metadata, version switching and lifecycle
actions, locked and system states, delete-blocker display.

Out of scope: API calls, auth, tenants, reports, and the content of the Mappings and Bridges tabs and of the mapping set
and bridge pages under a dimension, which the mapping sets and bridges brief specifies. Use in-memory mock data and
local state; all mutations update the mock state so the prototype feels live.

Reuse existing design patterns from current screens that have been created.

## 2. Domain model (for mock data)

```ts
type DimensionKind = 'categorical' | 'hierarchical' | 'range' | 'time';
type VersionStatus = 'draft' | 'validated' | 'published' | 'retired';
type RangeDataType = 'decimal' | 'date' | 'datetime';
type EditPolicy = 'open' | 'extend' | 'locked';

/** A system-defined dimension type. A system dimension always has a profile; a user dimension never does. */
interface DimensionProfile {
  key: string;                           // 'branch', 'officer', 'product', 'planVersion'
  displayName: string;
  kind: DimensionKind;                   // the kind a dimension of this profile must have
  owner: string;                         // module that depends on it, e.g. 'Profitability'
  editPolicy: EditPolicy;
  visibility: 'manager' | 'module' | 'hidden';
  requiredRoleKeys: string[];
  requiredMemberKeys: string[];
  memberPropertySchema: PropertyField[]; // typed metadata rendered on the member sheet
  cardinality: 'one' | 'many';
}

interface PropertyField {
  key: string; label: string;
  type: 'text' | 'number' | 'date' | 'url' | 'image' | 'select' | 'geo' | 'boolean';
  options?: string[]; required?: boolean; description?: string;
}

interface Dimension {
  id: string; key: string; displayName: string;
  kind: DimensionKind;
  origin: 'system' | 'user';
  purpose: 'analytical' | 'report';      // 'report' dimensions never appear in the manager
  profileKey: string | null;             // set when origin is 'system'
  isEnabled: boolean; isRemovable: boolean;
}

interface DimensionVersion {
  id: string; dimensionId: string; versionNumber: number;
  status: VersionStatus; rangeDataType: RangeDataType | null; publishedAt: string | null;
}

interface Role { id: string; dimensionId: string; key: string; displayName: string; description: string | null; }

interface Level {
  id: string; versionId: string; key: string; displayName: string; ordinal: number; isLeaf: boolean;
  periodicity: 'day' | 'month' | 'quarter' | 'year' | null;
}

interface Member {
  id: string; versionId: string; key: string; displayName: string; description: string | null;
  parentId: string | null; levelId: string | null;
  isAssignable: boolean; sortOrder: number;
  properties: Record<string, unknown>;
}

interface RangeBand {
  ordinal: number; memberId: string;
  lower: string | number | null; upper: string | number | null;  // typed by the version's rangeDataType
  lowerInclusive: boolean; upperInclusive: boolean;
}

/** What references a dimension or role. */
interface UsageReference {
  kind: 'dataset binding' | 'mapping set' | 'bridge' | 'calculation dimension' | 'report scenario' | 'report';
  name: string; detail: string;          // e.g. "loans.details v3 · field servicing_branch_code · mapped"
  roleKey?: string;
}
```

### Mock data to seed

Provide enough variety to exercise every state.

**System dimensions**

- **Branch** — profile `branch`, hierarchical, policy `extend`, owner "Core". ~25 members in Region > District >
  Branch; member schema: `photoUrl` (image), `address` (text), `geo` (geo lat/lng), `openedOn` (date). Roles: `branch`
  (required), `servicingBranch`, `originationBranch`. Versions: v1 retired, v2 published. Heavy usage list.
- **Officer** — profile `officer`, categorical, policy `extend`. Flat members; schema: `email` (text), `photoUrl`
  (image), `title` (text). One role `officer`.
- **Product** — profile `product`, hierarchical, policy `locked`, owner "Profitability". Required members `MORTGAGE`,
  `AUTO`, `COMMERCIAL`, `DEPOSITS` under folder nodes. v1 published.
- **Scenario** — profile `planVersion`, hierarchical, policy `extend`, owner "Planning". Folders Budgets/Forecasts
  (not assignable) with leaves; schema: `kind` (select budget|forecast), `horizonStart`, `horizonEnd`, `asOf` (dates),
  `status` (select). Exactly one role `planVersion`; adding a role is refused ("a scenario's dimension has exactly one
  role").
- A profile with `visibility: 'module'` (e.g. `glAccountClass`, owner "General Ledger") that is deep-linkable but not
  listed.
- A profile with `visibility: 'hidden'` (e.g. `systemAccountClass`) that must never be listed or linkable.

**User dimensions**

- **RiskGrade** — categorical, flat list of 8, v1 published and a v2 draft (clone) with one added member.
- **BalanceBand** — range `decimal`, 6 bands, first open-ended below, last open-ended above; seed one overlap in a
  draft to demonstrate validation.
- **MaturityBucket** — range `date`, 4 bands.
- **FiscalCalendar** — time, no roles, no members, levels day/month/quarter/year with periodicities.
- **Territory** — hierarchical, jagged (some branches directly under a region with no district); custom properties
  `boundaryGeoJson` and `color`.
- **Collateral** — categorical, v1 draft only, never published (deletable).
- **Purpose** — categorical, v1 published, referenced by one mapping set (not deletable).

**Never listed**

- At least one `purpose: 'report'` dimension in the data.

## 3. Navigation

- `/dimensions` — user dimensions list.
- `/dimensions/system` — system dimensions list.
- `/dimensions/new` — create flow for user dimensions (may be a sheet over the list).
- `/dimensions/:id?version=:versionId&tab=:tab` — detail page for either origin. Version and tab persist in the URL
  so a refresh returns to the same place. Default version: the published one, else the latest draft. Default tab:
  Members. `?tab=mappings` and `?tab=bridges` open the Mappings and Bridges tabs, which the pages nested under the
  dimension link back to.
- `/dimensions/:id/mapping-sets/:mappingSetId` and `/dimensions/:id/bridges/:bridgeId` — the pages of the dimension's
  mapping sets and of the analytical bridges that lead into it, nested under the dimension. There are no top-level
  mapping set or bridge pages.

Breadcrumb: Dimensions › {User | System} › {displayName} › v{n}. A nested page's breadcrumb ends in its mapping set or
bridge instead of the version.

## 4. List pages

Two top-level tabs at the top of `/dimensions`: **User dimensions** and **System dimensions**. They share the table
layout and differ in toolbar actions and columns.

### 4.1 Shared toolbar

- Search by key or display name.
- Kind filter chips: All · Categorical · Hierarchical · Range · Time.
- Status filter: Has draft · Published · Disabled.

### 4.2 Shared table columns

Name (display name, key beneath in muted mono) · Kind (badge with kind icon) · Roles (count; hover lists keys) ·
Members (count in the published version) · Published (v{n} · date; "—" when none) · Draft (v{n} badge when a draft
exists) · Enabled (dot) · row actions menu.

Row click opens the detail page. Empty states explain the tab and, on the user tab, offer the New dimension CTA.

### 4.3 User dimensions tab

- Primary button "New dimension".
- Row actions: Open, New draft, Disable/Enable, Delete.
- Delete opens a confirm dialog; when blocked (non-draft versions, references, not removable) the dialog shows the
  blocker list and no confirm button (see §10).

### 4.4 System dimensions tab

- No "New dimension" button. Instead, a secondary section or button "Available system types" that lists profiles with
  `visibility: 'manager'` and `cardinality: 'one'` that have no dimension yet, each with a "Define" action that creates
  it with its required roles and members and opens it. Profiles already defined link to their dimension.
- Extra columns: Type (profile display name with a `ShieldCheck` icon), Required by (owner module), Policy (open /
  extend / locked badge).
- Row actions: Open, New draft (not under `locked`), Disable/Enable (not under `locked`). No Delete.
- Lists only profiles with `visibility: 'manager'`. `module` profiles are reachable by deep link only and render a
  banner "Managed from the {owner} module"; `hidden` profiles and `purpose: 'report'` dimensions appear nowhere.

## 5. Create flow (user dimensions)

A two-step sheet or dialog opened from the user tab.

**Step 1 — Choose a kind.** A card grid of the four kinds:

- **Categorical** — "A flat list of named members, grouped by key." Icon `List`.
- **Hierarchical** — "A tree of members. Queries group at any level and roll up." Icon `Network`.
- **Range** — "Buckets a numeric or date value into ordered bands." Sub-option: range data type (Decimal / Date /
  Date-time). Icon `Ruler`.
- **Time** — "A time axis. Periods come from the data; members only for custom calendars." Icon `Calendar`.

Below the grid, a muted note: "Grouping by a raw or calculated field value does not need a dimension. Define a
groupable field on the dataset instead." with a link to Datasets › Fields.

**Step 2 — Details.**

- Key (mono; helper "A letter, then letters, digits or underscores. Fixed once created."), Display name.
- Range data type (Range only; prefilled from step 1).
- First role: key and name, prefilled from the dimension key and name and following them until the user edits them.
  Hidden for Time, which has no roles.
- Enabled (default on), Removable (default on).
- Summary line: "Creates {name} ({kind}) with draft version 1."
- Create → navigate to the detail page on the new draft, Members tab.

Inline validation to simulate: key pattern, key uniqueness across all dimensions, role key uniqueness across all
dimensions' roles.

## 6. Detail page

Shared by user and system dimensions; system state changes what is editable (§7).

### 6.1 Identity header

- Display name as title; beneath: `key · kind · origin` in muted text.
- Badges: kind; "System · {profile name}" with `ShieldCheck` for system dimensions; "Disabled" when not enabled;
  "Not removable".
- System dimensions show a line "Required by {owner} · {policy}" where the policy text is:
  - open: "You can edit everything; it cannot be deleted."
  - extend: "You can add members and roles and rename things. Required members and roles cannot be removed or
    re-keyed."
  - locked: "Read-only. Managed by {owner}."
- Actions: Edit (identity sheet: display name, enabled, removable; key and kind read-only; removable hidden for
  system), Delete (user dimensions only; blocked per §10). Under `locked`, Edit is hidden.

### 6.2 Version bar

A horizontal strip below the header.

- Version pills: `v1 retired · v2 published · v3 draft`, selected pill highlighted. Status colors: draft amber,
  validated blue, published green, retired gray.
- Actions for the selected version, by status:
  - draft: Validate, Publish, Delete draft.
  - validated: Publish, Delete.
  - published: note "Live — edits apply immediately"; New draft (clone).
  - retired: note "Retired on {date}, superseded by v{n}"; read-only.
- "New version" split button: Empty draft / Clone v{selected}. Only one draft may exist at a time; disable with a
  tooltip when one does. Hidden under `locked`.
- Publish confirm: "Publish v3? v2 will be retired and every query, mapping and report will read v3 at once." Run the
  simulated validation (§9) first and show violations instead of publishing when any exist.

Editability rule used by every tab: a tab is editable when the selected version is `draft` or `published` and the
dimension's policy permits it. `validated` and `retired` are read-only. Under `locked`, nothing is editable. Under
`extend`, required members and roles are locked individually.

### 6.3 Tabs

Tabs by kind: **Members**, **Levels**, **Range bands** (range only), **Roles** (not time), **Usage**, **Settings**,
**Mappings**, **Bridges**.

The Mappings tab lists the dimension's mapping sets, and the Bridges tab the bridges into it and, read-only, the
bridges from it; the mapping sets and bridges brief specifies both. They do not follow the version bar: mapping sets
and bridges have versions of their own, which assumption sets pin separately, so the two tabs show the same rows
whichever dimension version is selected.

---

### Members tab

Toolbar, then a tree (hierarchical) or list (categorical, range, time).

**Toolbar:** search (filters to matches plus their ancestors; drag disabled while active), member count, Expand all /
Collapse all (hierarchical), view toggle Tree / Table, and when editable: "New member" (root) and "Import…" (paste
CSV/TSV of key, name, parent key with a preview before adding; nice-to-have).

**Tree view (hierarchical):**

- Drag handle, caret, display name, key in mono, level badge when assigned, "folder" badge when `isAssignable` is
  false, `Lock` icon for required members, a thumbnail when the profile schema has an image property.
- Drag and drop reparents and reorders among siblings; the move is saved as one action with an undo toast.
- Row hover actions: Open, Add child, Delete. Under `extend`, a required member offers Open and Add child but not
  Delete. Under `locked`, only Open.
- Jagged trees must render correctly (a leaf directly under a root beside a subtree).

**List view (categorical, range, time):** same rows without carets or Add child; drag only reorders. Time dimensions
show a note "Time dimensions derive periods from dates; members are needed only for custom calendars."

**Table view:** key, name, parent key (hierarchical), level, assignable, sort order, plus the first two schema
properties as columns. Sortable and filterable, same row actions.

**Member sheet (create/edit):** right-side sheet, ~36rem.

- Key (mono; fixed after create; read-only for required members).
- Display name, Description (textarea).
- Parent (hierarchical only; searchable combobox over the version's members excluding the member and its
  descendants; "None (root)").
- Level (select over the version's levels, with "None").
- Assignable toggle, helper "Off for folder nodes that only group other members."
- Sort order (number; default last among siblings).
- **Properties:** for a system dimension, render each `memberPropertySchema` field by type: text, number, date picker,
  URL with preview link, image (URL plus thumbnail), select, boolean switch, geo (lat/lng inputs with a preview box; a
  GeoJSON textarea with a "valid JSON" check for boundary fields). Required fields marked. For a user dimension, a
  collapsible "Custom properties" key/value editor that round-trips to JSON, with a "View JSON" toggle.
- Footer: Cancel / Save. Read-only contexts render values as text, not disabled inputs.

**Delete member:** confirm dialog. When the member has children, is assigned by a range band, or is named by another
object (seed a few: `BR01` is a binding's unknown member, `NORTH` is a report's ancestor grouping), show the blockers
and no confirm button.

---

### Levels tab

Table: ordinal (drag to reorder), key, display name, leaf flag, periodicity (time only), members using it (count).
Add/edit level sheet with the same fields. Deleting a level used by members is blocked with the count. Helper text:
"Levels are labels for hierarchy tiers. The structure itself comes from member parentage."

---

### Range bands tab (range only)

- If the version has no range data type, show "Set the range data type in Settings before adding bands."
- Note: "Bands are evaluated in order and must not overlap. An empty bound is unbounded. Only the first band may be
  open below and only the last open above."
- Sortable list of band cards: ordinal, Member (combobox), Lower bound, Upper bound (inputs typed by the range data
  type: number, date picker, date-time picker), "Include lower" (default on), "Include upper" (default off). A
  textual preview per band, e.g. `[0, 10,000)` or `(−∞, 2024-01-01)`.
- A **band strip** above the list: a horizontal line showing each band as a labeled segment, highlighting overlaps and
  gaps in red/amber live as the user edits.
- Add band (prefills lower = previous upper, assigns the first unused member), remove band, drag to reorder.
  Save/Discard for the whole list (bands save as a set).

---

### Roles tab (not time)

Explanation: "A role is a named use of this dimension. Datasets bind fields to a role, and queries group by the role's
key. Example: Servicing branch and Origination branch are both roles of Branch."

Table: key (mono), display name, description, bound by (count of dataset bindings from usage), used by reports (count),
`Lock` for required roles. Add/edit role sheet: key (fixed after create), display name, description. Delete blocked
when referenced (show the list). For the Scenario mock, Add role is disabled with tooltip "A scenario's dimension has
exactly one role."

---

### Usage tab

Read-only groups of reference cards:

- Dataset bindings (dataset, version, field, binding kind, role).
- Mapping sets: a count, linking to the Mappings tab, which lists them.
- Bridges: counts into and from this dimension, linking to the Bridges tab, whose "From" section lists the bridges
  that read this dimension's members, report bridges included, so a member edit shows its impact there.
- Reports: calculation dimensions (report, grouping), scenarios (report), report rows when a report used it as a
  template.
- Assumption sets: which sets pin which version (e.g. "active → v2", "2024 baseline → v1").

Each card links out (dead links are fine). Empty state: "Nothing references this dimension yet — it can be deleted."

---

### Settings tab

Per version: range data type (range only; select, with warning when bands exist: "Changing the type clears the bands'
bounds"). Read-only facts: version number, status, created and published dates.

## 7. User versus system — a checklist the prototype must demonstrate

- User and system dimensions live on separate list tabs; the create flow exists only on the user tab; system
  dimensions are defined from "Available system types", never created freely.
- Report-purpose dimensions and `hidden` profiles appear nowhere.
- `module` profiles are not listed but deep-link to a detail page with the banner "Managed from the {owner} module."
- `locked`: every tab read-only, Edit hidden, no New version, no Delete.
- `extend`: required members and roles show a `Lock`, cannot be deleted or re-keyed, can be renamed and given
  properties; new members and roles can be added anywhere.
- `open` system dimension: fully editable, never deletable.
- Removable toggle hidden for system dimensions; Delete never offered for them.
- Typed member properties appear only on system dimensions; user dimensions get the custom key/value editor.

## 8. Interaction details

- Every destructive action uses a confirm dialog; every mutation shows a toast; drag moves show an undo toast.
- Sheets close on Escape and overlay click only when the form is not dirty; dirty forms prompt.
- Read-only rendering is text, not disabled inputs, so published and retired versions are readable.
- Keyboard: tree rows focusable, Enter opens, arrow keys navigate, Alt+arrows move (nice-to-have).
- Long trees (500+ members): virtualize or paginate the table view; collapse by default below depth 2.

## 9. Simulated validation (validate / publish)

On Validate or Publish of a draft, run client-side checks against the mock data and show a results panel listing
violations, each linking to the item:

- Duplicate member keys or level keys.
- A cycle in parent links (seed one in a draft to demonstrate).
- Range: missing range data type; overlapping bands; lower above upper; a band assigned to a member not in this
  version; an open lower bound on a band other than the first or an open upper bound on one other than the last.
- Required member or role missing (system dimensions).
- "Keys other objects name are missing from this draft": compare the draft's member keys with the keys the usage list
  names (e.g. a mapping set names `BR09` and the draft removed it).

On success, transition the status and show a toast.

## 10. Delete blockers

Deleting a user dimension is allowed only when it is removable, every version is a draft, and nothing references it or
its roles. The confirm dialog shows the reasons when blocked, in this phrasing:

> Dimension 'Purpose' cannot be deleted: it has non-draft versions (v1 published); it is bound by dataset versions
> ('loans.details' v3); referenced by mapping sets ('loan.purpose').

Same pattern for roles, levels and members. System dimensions never offer Delete.

## 11. Visual conventions

- shadcn components: Sheet for editors, Dialog for confirms, Tabs, Badge, Table, Command/Combobox for pickers, Tooltip
  for disabled reasons, Sonner toasts.
- Keys always in a mono font, muted. Display names regular.
- Status colors as in §6.2. `Lock` for required items, `ShieldCheck` for system dimensions.
- Kind icons: categorical `List`, hierarchical `Network`, range `Ruler`, time `Calendar`.

## 12. Deliverables from v0

- Routable pages as described, with a mock data module and an in-memory store.
- All sheets and dialogs wired to the store.
- A dev menu (or query flags) to jump to: Branch v2 (extend), Product (locked), the `module` profile by deep link,
  Collateral (deletable draft), Purpose (blocked delete), BalanceBand draft (band overlap), RiskGrade v2 draft (publish
  violation), FiscalCalendar (time).
