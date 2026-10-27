# 02 Frontend contract layer

How `web2` talks to the API. The prototype already has the right shape for four modules (a port per module, a mock
adapter behind it, SWR hooks in a `client.tsx`); this file makes it uniform, adds the HTTP adapters and brings reports
and models into the same pattern.

## Source of truth (Agreed)

The hand-authored OpenAPI contract in [`api/openapi/contract/`](../../api/openapi/contract/README.md) defines every
read model, input, operation union and error the frontend sees ([00](00-architecture.md#the-contract-first-rule-agreed)).
The `web2` side follows it in two ways:

- **Ports mirror it by hand.** Each `lib/<module>/contract.ts` declares the contract's schemas under the same names
  and the port methods that use them. A port type that differs from its schema in more than presentation-only
  additions is a defect in the port or a change the contract makes first.
- **HTTP adapters use generated types.** `pnpm contract:types` bundles the contract and generates
  `lib/api/schema.d.ts` with `openapi-typescript`. Only `lib/<module>/http/*.ts` imports it; the adapter maps the
  generated types onto the port types, so a contract change that the port does not follow fails `pnpm build`.

`lib/api/contract.ts` mirrors `common.yaml` the same way.

## Folder layout (Agreed)

```
web2/src/lib/
  api/
    contract.ts        shared kernel: ApiResult, Problem, Violation, DeleteCheck, VersionStatus, Vocabulary, ids
    http.ts            fetch wrapper: base URL, tenant prefix, bearer token, problem parsing, 401 handling
    schema.d.ts        generated from api/openapi/contract/bundled.yaml; imported only by */http/*.ts
    changes.ts         ChangeSetPort, the mock snapshot change set, the HTTP change set (moved from lib/changes.ts)
  session/
    contract.ts        Session, role, tenant list
    client.tsx         SessionProvider, useSession, useTenant
  <module>/            datasets, dimensions, mappings, releases, models, reports
    contract.ts        read models, inputs, operations, the port interface, shared module rules (labels, tab lists)
    http/adapter.ts    implements the port over HTTP
    mock/adapter.ts    implements the port in memory (kept for tests and offline prototyping)
    client.tsx         ApiProvider, SWR hooks, the module's EditScope
```

`lib/<module>/rules.ts` files that compute validity (`mappings/rules.ts`, `model/rules.ts`, `report/coverage.ts`,
`report/profiles.ts`, `report/scenarios.ts`) are retired as decision makers (OI-05). Their presentational helpers
(paste parsing, `entriesToText`, `descendantKeys`, `roleOf` for display) move to the module's `contract.ts` or to the
component that uses them.

## Shared kernel: `lib/api/contract.ts` (Agreed)

Replaces the separate `ApiResult`/`ProblemDetails` declarations in `datasets/contract.ts`,
`dimensions/contract.ts` and `releases/contract.ts` (they disagree on `status` and on extra members such as
`dependents`).

```ts
export type Id = string
export const newTempId = (): Id => `tmp:${crypto.randomUUID()}`  // items created inside a change set
export const KEY_PATTERN: RegExp                                     // Key
export const DOTTED_KEY_PATTERN: RegExp                              // DottedKey: datasets, models, reports, mapping sets, bridges

export type VersionStatus = 'draft' | 'validated' | 'published' | 'retired'

// Vocabularies more than one module reads
export type DimensionKind, DimensionPurpose, DefinitionOrigin, AssumptionSetStatus, BindingKind, Periodicity,
  MeasureKind, ComponentRole, RoundingMode, SourceSelection, VariableMissingBehavior

export interface Problem { /* see 01-api-conventions.md#problem-body */ }
export interface Violation { code: string; message: string; target?: ViolationTarget }
export interface ViolationTarget { area: string; itemId?: string; itemKey?: string; filter?: string }
export type ApiResult<T> = { ok: true; data: T } | { ok: false; problem: Problem }

export interface ValidationReport { versionId: Id; checkedAt: string; violations: Violation[] }
export interface DeleteCheck { allowed: boolean; message: string | null; reasons: DeleteBlocker[] }
export interface DeleteBlocker extends Violation { code: DeleteBlockerCode }   // one list, 01 "Delete checks"

export interface FreezeInput { note: string | null }
export interface FreezeResult<V> { report: ValidationReport; live: V | null }  // dimensions, mapping sets, bridges
export type Grouping = { mode: 'member' } | { mode: 'level'; levelKey: string } | { mode: 'ancestor'; memberKey: string } | { mode: 'depth'; depth: number }
export type FilterNode = /* the engine's structured filterNode over FilterOperand */

export interface VocabularyTerm<V extends string = string> { value: V; label: string; description?: string }

export interface ChangeSetRequest<Op> { operations: Op[] }
export interface ChangeSetPreview<State> { state: State; violations: Violation[] }
export interface ChangeSetCommit<State> { state: State; idMap: Record<string, Id> }

export class ProblemError extends Error { problem: Problem }          // thrown by unwrap
export function unwrap<T>(result: Promise<ApiResult<T>>): Promise<T>  // SWR fetchers of reads that return ApiResult
```

Each type mirrors the `common.yaml` schema of the same name. The contract expresses the three `ChangeSet*` generics as
base schemas that each aggregate specializes with `allOf` (see the contract README).

### HTTP client: `lib/api/http.ts` (Agreed)

`createHttpClient({ baseUrl, token, onUnauthorized })` returns a client whose `get`, `post`, `put`, `patch` and
`delete` resolve to `ApiResult`: 2xx bodies are the data (`204` is `null`), error bodies are the `Problem`.
`forTenant(tenantId)` returns a client rooted at `/api/tenants/{tenantId}`. Every `401` calls `onUnauthorized` before
the problem is returned. The session layer creates the client once; HTTP adapters receive it.

Module contracts re-export what they use (`export type { ApiResult, VersionStatus } from '@/lib/api/contract'`) and
extend `Violation` codes with module unions where useful:

```ts
export type DimensionViolationCode = 'member_key_unique' | 'level_key_unique' | 'hierarchy_acyclic' | ...
export interface DimensionViolation extends Violation { code: DimensionViolationCode }
```

**Delta:** `web2` violation codes are camelCase in dimensions (`duplicateMemberKey`, `bandOverlap`) and snake_case in
mappings and releases. The API's rule codes are snake_case. Contracts adopt the API codes (OI-11).

## Ports

A port is an interface of async methods grouped as reads, edits and commands.

| Group | Behavior | Transport |
|---|---|---|
| Reads | Return read models; see pending state when called through the staged port | `GET` |
| Edits | Stage an operation; validated by preview; applied on Save | `POST <aggregate>/changes/preview`, then `POST <aggregate>/changes` |
| Commands | Run on saved state immediately; refused while edits are pending | `POST`/`DELETE` lifecycle endpoints |

The edit and command lists are part of the contract, declared next to the port, as `dimensions/client.tsx` and
`datasets/client.tsx` do today (`DIMENSION_EDITS`, `DIMENSION_COMMANDS`). They move into
`contract.ts` so the HTTP adapter and the staged proxy share them.

### `ChangeSetPort` over HTTP (Agreed, OI-01)

```ts
export interface ChangeSetPort {
  beginChanges(): void
  commitChanges(): Promise<ApiResult<{ idMap: Record<string, Id> }>>
  discardChanges(): Promise<void>
  hasPendingChanges(): boolean
  captureState(): unknown
  restoreState(state: unknown): void
}
```

The HTTP implementation per aggregate:

| Method | Implementation |
|---|---|
| `beginChanges` | Start an empty operation list; until the first preview, reads still answer from the server. |
| edit method `m(args)` | Append `{ op: 'm', ...args }`; post all operations to `changes/preview`; on success replace the local pending state with `state` and return the edited item from it; on failure pop the operation and return the problem. |
| reads while pending | Answer from the last preview `state`. |
| `captureState` | Copy of `{ operations, state }`. |
| `restoreState` | Put that copy back (no request). |
| `commitChanges` | Post operations to `changes`; on success clear, return `idMap`; the staged proxy's `onSettled` revalidates SWR keys. Last write wins (OI-02): there is no stale-revision check; an operation that no longer applies fails the commit and the user reloads. |
| `discardChanges` | Clear locally. |

`commitChanges` returns the kernel `ApiResult` (it was `{ ok, problem: { title, detail } }`) so the save bar can show
field-level problems.

`lib/api/changes.ts` implements this table once, as `createHttpChangeSet<Op, State>(http)`. An HTTP adapter spreads
it into its port, implements each edit method as `stage(aggregatePath, { op: '<method>', ...args })` and answers
reads from `pending()` while it is not null. `stage` strips the `operations[n].` prefix from the refused operation's
`errors`. Mock adapters use `createSnapshotChangeSet`, whose commit returns an empty `idMap`.

### Commit scope

One change set per aggregate root (one dataset version, one dimension, one mapping set, one analytical bridge, one
draft release, one model, one report). A dimension, mapping set or bridge change set covers its identity and its live
version; a frozen version accepts none (OI-14). `useStagedApi` already scopes by `dimension:<id>` and `dataset:<id>`.

The navigation guard (`ChangesProvider.resolve`) saves every dirty scope in sequence. Saves across aggregates are not
atomic and the blueprint does not make them so; a failed save stops the sequence and keeps the user on the page
(OI-57).

## Adapting each module

| Module | Today in `web2` | Target |
|---|---|---|
| Datasets | Port + `ChangeSetPort` + staged proxy | Keep; add HTTP adapter; move `createDataset` from edits to commands (it creates an aggregate, it does not edit one) |
| Dimensions | Port + `ChangeSetPort` + staged proxy | Keep; HTTP adapter; `freezeVersion` on the living lifecycle; `listHiddenDimensions` removed (OI-14, OI-22) |
| Mapping sets, bridges | Port with drafts; edits write immediately | `MappingsApi` extends `ChangeSetPort`; edits staged per mapping set or bridge; drafts, validate, publish and reopen replaced by `freeze` like dimensions (OI-01, OI-14) |
| Releases | Port; manifest kept in component state, saved with `saveManifest` | `saveManifest` becomes the release aggregate's single edit; `checkRelease(id, manifest)` becomes a preview |
| Calculation models | React store with per-model drafts, rules in the browser | New `ModelsApi` port extending `ChangeSetPort`; refusals come from preview (OI-42); formulas checked by `checkFormula` (OI-43) |
| Reports | React store holding whole `ReportState` documents | New `ReportsApi` port extending `ChangeSetPort`, over the `ReportDocument` read model ([16](16-reports.md)) |

## Caching and SWR keys (Agreed)

- Keys are arrays beginning with the module and the tenant: `['dim', tenantId, 'list', origin]`,
  `['dim', tenantId, 'version', versionId]`. Today's keys omit the tenant (`['dim', ...]`, `['ds', ...]`); adding it
  makes a tenant switch safe.
- A successful commit or command revalidates the module's keys for that aggregate plus the keys the response lists in
  `affects?: string[][]` (optional field) for cross-module effects, such as freezing a dimension version changing
  assumption set pins.
- Pending state is never written into SWR's cache; the staged port serves it.

## Error mapping

- `Problem.errors` keys are input paths. Form helpers map `components[0].fieldId` to the field name. Inside a change
  set the adapter strips the `operations[n].` prefix.
- `Problem.violations[].target` drives "go to" links: `area` names a tab or section, `itemId` opens the item, `filter`
  narrows a table. This replaces `Violation.tab` (dimensions), `PublicationViolation.tab` (datasets),
  `Violation.filter` (mappings) and `ReleaseViolation.target` (releases) with one shape.
- `401` clears the session and routes to sign-in; `403` (wrong tenant, or `/api/system/*` without `SystemAdmin`)
  shows a not-allowed page; `404` on a detail route shows the
  module's "doesn't exist or isn't available" message.

## Session

`lib/session/viewer.ts` is a prototype stand-in, replaced by `lib/session/contract.ts`:

```ts
export interface Session {
  subject: string
  displayName: string | null
  tenantId: string | null
  isSystemAdmin: boolean
}
```

served by `GET /api/me` ([10](10-tenant-session-catalog.md)). There are no tenant roles (OI-09): `ViewerRole`,
`report_author`, `setViewerRole` and `useViewer().isSystemAdmin` gating in tenant screens are removed;
`isSystemAdmin` only gates the system tenant directory.

## Testing

- One port test suite per module, run against the mock adapter and, behind a flag, against a live tenant through the
  HTTP adapter (parity rule, [00](00-architecture.md#the-contract-first-rule-agreed)).
- Tests exist to confirm documented behavior; each states the behavior and why it matters.
