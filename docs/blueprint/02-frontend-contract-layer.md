# 02 Frontend contract layer

How `web2` talks to the API. The prototype already has the right shape for four modules (a port per module, a mock
adapter behind it, SWR hooks in a `client.tsx`); this file makes it uniform, adds the HTTP adapters and brings reports
and models into the same pattern.

## Folder layout (Agreed)

```
web2/src/lib/
  api/
    contract.ts        shared kernel: ApiResult, Problem, Violation, DeleteCheck, VersionStatus, Vocabulary, ids
    http.ts            fetch wrapper: base URL, tenant prefix, bearer token, problem parsing, 401 handling
    schema.d.ts        generated from api/openapi/admin-api.json; imported only by */http/*.ts
    changes.ts         ChangeSetPort, the staged-port helper, operation recording (moved from lib/changes.ts)
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

export type VersionStatus = 'draft' | 'validated' | 'published' | 'retired'

export interface Problem { /* see 01-api-conventions.md#problem-body */ }
export interface Violation { code: string; message: string; target?: ViolationTarget }
export interface ViolationTarget { area: string; itemId?: string; itemKey?: string; filter?: string }
export type ApiResult<T> = { ok: true; data: T } | { ok: false; problem: Problem }

export interface ValidationReport { versionId: Id; checkedAt: string; violations: Violation[] }
export interface DeleteCheck { allowed: boolean; message: string | null; reasons: Violation[] }

export interface VocabularyTerm<V extends string = string> { value: V; label: string; description?: string }

export interface ChangeSetRequest<Op> { operations: Op[] }
export interface ChangeSetPreview<State> { state: State; violations: Violation[] }
export interface ChangeSetCommit<State> { state: State; idMap: Record<string, Id> }
```

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
| `beginChanges` | Start an empty operation list over the aggregate read model as last loaded. |
| edit method `m(args)` | Append `{ op: 'm', ...args }`; post all operations to `changes/preview`; on success replace the local pending state with `state` and return the edited item from it; on failure pop the operation and return the problem. |
| reads while pending | Answer from the last preview `state`. |
| `captureState` | Copy of `{ operations, state }`. |
| `restoreState` | Put that copy back (no request). |
| `commitChanges` | Post operations to `changes`; on success clear, revalidate SWR keys, return `idMap`. Last write wins (OI-02): there is no stale-revision check; an operation that no longer applies fails the commit and the user reloads. |
| `discardChanges` | Clear locally. |

`commitChanges` changes from `{ ok, problem: { title, detail } }` to the kernel `ApiResult` so the save bar can show
field-level problems.

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
