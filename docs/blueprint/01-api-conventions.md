# 01 API conventions

These rules apply to every module file. A module file states only its exceptions.

## Routing and tenancy (Agreed)

- Base path `/api`. Tenant data lives under `/api/tenants/{tenantId}/...`; `tenantId` is the tenant directory key, a
  string.
- Routes outside tenant scope: `/api/me`, `/api/tenants`, `/api/vocabularies`, `/api/system/tenants`, `/health`, and
  `POST /development/token` in Development only.
- Resource paths are plural, kebab-case nouns. A child of a version hangs off the version
  (`dimension-versions/{versionId}/...`); version lists, one version per snapshot, hang off the identity
  (`datasets/{datasetId}/versions`).
- Versions have no lifecycle actions: a live version is edited through its identity's change set and validated on every
  commit, and a frozen version never changes. Snapshot actions are sub-resources named by the action, such as
  `POST snapshots/{id}/restore` ([14](14-snapshots.md#http-api)).
- Delete checks are `GET .../delete-check` ([below](#delete-checks-agreed-oi-07)).

## Authentication and authorization (Agreed, OI-09)

- JWT bearer. Claims: `sub`, exactly one `tenant_id`, and `role: SystemAdmin` for a caller who may reach any tenant.
  `TenantAccess` admits a caller whose tenant claim equals the route tenant, or a system admin.
- Every tenant route is open to any caller with tenant access. There are no tenant-level roles: capture, restore,
  system dimension definition and every editor are available to every tenant user.
- `/api/system/*` (the tenant directory) requires `SystemAdmin`.
- `web`'s `report_author` role (`lib/session/viewer.ts`) has no counterpart and is removed.

## Ids, keys and names

- **Ids are strings (Agreed, OI-04).** Every id in a URL or JSON body is a string. Tables use BIGINT ids below 2^53;
  the API serializes them as strings so ids stay opaque and `web`'s `string` ids are unchanged.
- **Keys are fixed after creation (Agreed)** for datasets (OI-36), fields, grains, measures, dimensions, roles,
  members, levels, mapping sets, bridges, models and metrics and parameters (OI-45),
  reports (OI-47), rows (OI-48), sources, profiles, columns and styles.
- **Key patterns (Agreed, OI-29):** `^[A-Za-z][A-Za-z0-9_]*$` (`Key`) for most keys; dataset, calculation model,
  report, mapping set and bridge keys also allow `.` (`^[A-Za-z][A-Za-z0-9_.]*$`, `DottedKey`), matching the seed
  (`bank.balance_monthly`, `mortgage.loan_daily`, `bank.margin_model`, `bank.margin_statement`) and report-derived
  bridge keys; snapshot keys follow [14](14-snapshots.md#domain-rules).
- **Field naming (Agreed, OI-10):** an entity's own identifier and natural key are `id` and `key`
  (`Dimension.id`, `Dimension.key`). A reference to another entity is `<entity>Id` or `<entity>Key`
  (`dimensionId`, `roleKey`). Display text is `displayName`. This renames `datasets/contract.ts`'s `datasetKey`,
  `fieldKey`, `grainKey`, `measureKey`, and the current API's `dimensionKey`, `dimensionRoleId`.
- **JSON:** `application/json` only, camelCase properties, enum values as the schema's snake_case strings
  (`many_to_one`, `calculation_model`). Nullable properties are present with `null`, never omitted. Timestamps are ISO 8601 UTC
  strings. Decimals that the schema stores as `DECIMAL(38,12)` (factors, weights, band bounds, parameter defaults) are
  JSON numbers in the frontend contract; the adapter rejects values a JS number cannot represent exactly.

## Results and errors

### Port result (Agreed, shared kernel)

```ts
export type ApiResult<T> = { ok: true; data: T } | { ok: false; problem: Problem }
```

Ports return `ApiResult` for writes and for reads that can fail for business reasons (not found). List reads that
cannot fail return the value directly, as today.

### Problem body (Agreed, OI-06)

One shape for every error, `application/problem+json`, replacing `ProblemDetails`, `ValidationProblemDetails` and
`RuleViolationProblemDetails`, and the three divergent TypeScript copies:

```ts
export interface Problem {
  type: string              // stable URI-ish code, e.g. "urn:qe:problem:rules-violated"
  title: string             // short, user-facing
  status: 400 | 401 | 403 | 404 | 409 | 422 | 500
  detail?: string
  /** Input path -> messages. '' is the form-level key. Paths are camelCase, dotted, indexed: components[0].fieldId */
  errors: Record<string, string[]>
  /** Rule violations (422) and delete blockers (409). */
  violations?: Violation[]
  traceId?: string
}

export interface Violation {
  code: string              // the rule code, snake_case, e.g. "member_key_unique"
  message: string           // phrased for display
  target?: ViolationTarget  // where to take the user
}

export interface ViolationTarget {
  /** Module-specific area: a dataset tab, a dimension tab, a snapshot kind, a report section. */
  area: string
  itemId?: string
  itemKey?: string
  /** Search text that narrows a table to the offending rows. */
  filter?: string
}
```

| Status | When | Body |
|---|---|---|
| 400 | Body, route or query does not bind, or an input validator fails | `errors` |
| 401 / 403 | No valid token / no tenant access; `SystemAdmin` missing on `/api/system/*` | title only |
| 404 | An id in the route names no row in the tenant; a shared existence check runs before every action | title only |
| 409 | A precondition fails: not editable (locked), owned by a report, delete blocked, the live snapshot relabelled or deleted, the revision changed during a capture | `violations` for delete blockers; `detail` otherwise |
| 422 | The change would break a rule: gate and commit rules, report gate, model gate, a restore's rules | `violations` |
| 500 | Unhandled | `traceId` |

Rule reports: a change-set preview answers the commit rules' findings in `violations` and never writes. A commit or a
restore answers `422` with `violations` when a rule fails, writing nothing.

### Delete checks (Agreed, OI-07)

```ts
export interface DeleteCheck {
  allowed: boolean
  /** One-sentence summary, e.g. "Dimension 'Purpose' cannot be deleted: ..." */
  message: string | null
  reasons: DeleteBlocker[]
}

export interface DeleteBlocker extends Violation {
  code: DeleteBlockerCode
}
```

- `GET <resource>/delete-check` returns `DeleteCheck` and never writes.
- `DELETE <resource>` re-runs the same check and answers `409` with the same `violations` when blocked.
- Every module draws its reasons from one snake_case list, `DeleteBlockerCode` in `common.yaml`. A code names either a
  state of the target or, as `referenced_by_<kind>`, the kind of object that still reads it; the message names the
  objects.

| Code | Blocks deleting |
|---|---|
| `not_removable` | A dimension marked not removable. |
| `system_dimension` | A system dimension. |
| `has_children` | A member with child members. |
| `required_by_profile` | A member or role the dimension's profile requires. |
| `assigned_by_bands` | A member that range bands assign. |
| `named_by_consumers` | A live member that a consumer names by key (mapping entry, bridge mapping, report rule). |
| `owned_by_report` | A report-owned bridge; the report source owns it. |
| `referenced_by_member` | A level that members use. |
| `referenced_by_dataset_binding` | A dimension, role, mapping set or bridge that a dataset binding reads. |
| `referenced_by_mapping_set` | A dimension that a mapping set targets. |
| `referenced_by_bridge` | A dimension that a bridge joins. |
| `referenced_by_calculation_dimension` | A dimension or role that a report calculation dimension groups by. |
| `referenced_by_report_scenario` | A dimension or role a report uses as a scenario. |
| `referenced_by_report_source` | A dimension or role a report source reads. |
| `referenced_by_snapshot` | An identity of any of the six kinds that a frozen snapshot holds a version of. |
| `referenced_by_report` | A calculation model that a report reads. |

## Vocabularies (Agreed, OI-11)

`GET /api/vocabularies` returns every enumeration the UI renders, each term with `value`, `label` and optional
`description`, plus relationships the UI needs (for example `componentRolesByKind`). Values are the schema's; rule
codes are the API's snake_case codes everywhere. TypeScript unions remain in the contracts for type safety; labels and
option lists come from the server.

## Read models (Agreed, OI-08)

- A list endpoint returns the module's list item read model in one call (a dimension list item includes its roles,
  version summaries, profile and live member count). No N+1 calls from the browser.
- A detail endpoint returns the detail read model (list item plus usage, snapshots holding a version, dependents).
- A version content endpoint returns the whole editable content of one version.
- Fine-grained list endpoints no screen uses are removed.
- No pagination: tenant metadata is small. Lists accept filters the screens use (`origin`, `dimensionId`).

## Concurrency (Agreed, OI-02)

Last write wins. There is no revision token: read models carry none and commits send none. Two authors saving the same
aggregate overwrite each other, as today; each commit is still internally consistent because it is applied and checked
as a whole.

## Change-set protocol (Agreed, OI-01)

`web`'s universal editing rule (`lib/changes.ts`): every edit stays pending until the user saves; reads see pending
state; commit applies the batch at once; discard drops it; commands never run while a change set is open.
Every module implements it with this protocol.

1. **The client holds the change set.** The staged port records each edit as an operation and keeps the pending state
   locally, which `captureState`/`restoreState` (undo/redo) already require.
2. **Each edit is checked by the server as it is made.** The HTTP adapter posts the operations so far to the
   aggregate's preview endpoint and receives the resulting read model or a problem:

   `POST <aggregate>/changes/preview` with `{ operations: Operation[] }`
   - `200 { state: <AggregateContent>, violations: Violation[] }`: the operations apply; `state` is what the editor
     renders; `violations` are commit-rule findings, which the commit refuses until they are resolved.
   - `400` / `409` / `422` with a `Problem` whose `errors` keys are prefixed by the failing operation index
     (`operations[3].memberKey`): the last operation is refused and is not recorded.
3. **Save commits once.** `POST <aggregate>/changes` with the same body applies all operations in memory to the
   aggregate as currently stored, runs input validators, gates and cascades, writes, advances `semantic_revision` once
   if the engine can see the change, and returns the new read model. Any failure writes nothing.
4. **Discard is local.** Nothing was written.
5. **Commands** (create, delete, enable, capture, restore) are separate endpoints; the client
   blocks them while a change set is open, as `useStagedApi` does today.

```ts
export interface ChangeSetRequest<Op> {
  operations: Op[]
}
export interface ChangeSetPreview<State> {
  state: State
  violations: Violation[]
}
export interface ChangeSetCommit<State> {
  state: State
  idMap: Record<string, string>
}
```

Operations are a discriminated union per aggregate, named after the port's edit methods, with the same inputs:

```ts
type DimensionOperation =
  | { op: 'updateDimension'; patch: { displayName?: string; isEnabled?: boolean; isRemovable?: boolean } }
  | { op: 'saveMember'; versionId: string; input: MemberInput }
  | { op: 'deleteMember'; versionId: string; memberId: string }
  | { op: 'moveMember'; versionId: string; memberId: string; to: MovePosition }
  // ... one per edit method of the port
```

New items created inside a change set carry a client-generated temporary id (`tmp:<uuid>`); later operations may
reference it; the commit response maps temporary ids to real ones in `idMap`.

Because concurrency is last-write-wins (OI-02), a commit replays its operations onto whatever is stored at commit
time. An operation that no longer applies (its target was deleted by someone else) fails the commit with the usual
`400`/`409`/`422`, and the user re-opens the editor.

Saving several aggregates (the navigation guard's "Save and leave") commits each in turn; each commit is atomic, the
sequence is not, and the guard stops at the first failure and keeps the user on the page (OI-57).

### Writing without transactions (Agreed, OI-03)

StarRocks rejects `UPDATE`/`DELETE` inside transactions. The commit computes the full set of row inserts, updates and
deletes from the in-memory result, then writes them in an order that leaves a harmless residue if it stops midway:
deletes children-first, then updates, then inserts parents-first, then the root row. No journal table is added; one is
reconsidered only if partial commits are observed ([91](91-schema-gaps.md#sg-02-commit-journal)).

## Semantic revision (Agreed)

`qe_database_info.semantic_revision` advances once per commit or command that changes what the engine reads (schema
doc §18):

- any write to a **live** version of a dataset, dimension, mapping set or bridge;
- any write to an **enabled** report, enabling or disabling a report;
- any write to a calculation model that an enabled report reads;
- capturing, restoring, relabelling or deleting a snapshot;
- identity flags the catalog reads (`isEnabled`, display names) on datasets, dimensions and bridges.

Writes to disabled reports do not advance it.

## Caching hints

Every read model that the engine also reads carries `semanticRevision` from the tenant so the client can tell when its
cache is stale after another author's commit. The tenant overview returns the current value.
