import type { ApiResult, ChangeSetCommit, ChangeSetPreview, ChangeSetRequest, Id, Problem, Violation } from './contract'
import type { HttpClient } from './http'

/**
 * Universal editing rule: every edit stays pending until the user saves the batch.
 *
 * Each module port implements this change set. The first edit opens it; later edits and reads see the pending state;
 * `commitChanges` applies everything as one batch and `discardChanges` drops it. Commands (create, delete, capture,
 * restore…) never run while a change set is open. Live is valid after every commit: a commit-rule finding on the pending
 * state refuses the commit.
 */
export interface ChangeSetPort {
  beginChanges(): void
  /** `idMap` maps each temporary id created in the batch to its real id. */
  commitChanges(): Promise<ApiResult<{ idMap: Record<string, Id> }>>
  discardChanges(): Promise<void>
  hasPendingChanges(): boolean
  /** Commit-rule findings on the pending state; saving is refused while any remains. Empty when nothing is pending. */
  pendingViolations(): Violation[]
  /** Opaque copy of the pending state, taken around each edit for undo/redo. */
  captureState(): unknown
  /** Puts a state from `captureState` back. Only called while a change set is open. */
  restoreState(state: unknown): void
}

const rulesViolated = (violations: Violation[]): Problem => ({
  type: 'urn:qe:problem:rules-violated',
  title: 'Fix the rule findings before saving',
  detail: violations[0].message,
  status: 422,
  errors: {},
  violations,
})

/**
 * In-memory change set for mock adapters: snapshot on begin, restore on discard. `violations` runs the module's commit rules
 * over the pending state, which the mock keeps in its store, given the state the batch began from; a found violation refuses
 * the commit as the API does.
 */
export function createSnapshotChangeSet<S>(take: () => S, restore: (snapshot: S) => void, violations: (before: S) => Violation[]): ChangeSetPort {
  let snapshot: S | null = null
  const pendingViolations = () => (snapshot === null ? [] : violations(snapshot))
  return {
    captureState: () => structuredClone(take()),
    restoreState: (state) => restore(structuredClone(state as S)),
    beginChanges() {
      if (snapshot === null) snapshot = structuredClone(take())
    },
    async commitChanges() {
      await new Promise((r) => setTimeout(r, 160))
      const found = pendingViolations()
      if (found.length) return { ok: false as const, problem: rulesViolated(found) }
      snapshot = null
      return { ok: true as const, data: { idMap: {} } }
    },
    async discardChanges() {
      if (snapshot !== null) restore(snapshot)
      snapshot = null
    },
    hasPendingChanges: () => snapshot !== null,
    pendingViolations,
  }
}

/** Replaces an array's contents in place so closures holding the array see the restored rows. */
export function refill<T>(target: T[], rows: T[]) {
  target.splice(0, target.length, ...rows)
}

/**
 * Change set of one aggregate over HTTP (01, Change-set protocol). The HTTP adapter spreads it into its port and
 * implements each edit method as `stage(aggregatePath, { op: '<method>', ...args })`.
 */
export interface HttpChangeSet<Op, State> extends ChangeSetPort {
  /**
   * Appends `op` and previews every staged operation at `<aggregatePath>/changes/preview`. On success the preview
   * becomes the pending state; on failure `op` is dropped and the problem's `operations[n].` prefixes are stripped
   * so form helpers see plain input paths.
   */
  stage(aggregatePath: string, op: Op): Promise<ApiResult<ChangeSetPreview<State>>>
  /** The last successful preview, which reads answer from while the change set is open; null before the first edit. */
  pending(): ChangeSetPreview<State> | null
}

interface Staged<Op, State> {
  /** Fixed by the first staged operation: one change set covers one aggregate root. */
  aggregatePath: string | null
  operations: Op[]
  preview: ChangeSetPreview<State> | null
}

/**
 * Last write wins (OI-02): the commit carries no revision, the server replays the operations onto what is stored, and
 * an operation that no longer applies fails the commit. Cache revalidation after a commit is the staged proxy's
 * `onSettled`.
 */
export function createHttpChangeSet<Op, State>(http: HttpClient): HttpChangeSet<Op, State> {
  let staged: Staged<Op, State> | null = null

  return {
    beginChanges() {
      staged ??= { aggregatePath: null, operations: [], preview: null }
    },
    async stage(aggregatePath, op) {
      const open = staged!
      const operations = [...open.operations, op]
      const result = await http.post<ChangeSetPreview<State>>(`${aggregatePath}/changes/preview`, { operations } satisfies ChangeSetRequest<Op>)
      if (!result.ok) return { ok: false, problem: withoutOperationPrefix(result.problem) }
      staged = { aggregatePath, operations, preview: result.data }
      return result
    },
    pending: () => staged?.preview ?? null,
    async commitChanges() {
      const { aggregatePath, operations } = staged!
      const result = await http.post<ChangeSetCommit<State>>(`${aggregatePath}/changes`, { operations } satisfies ChangeSetRequest<Op>)
      if (!result.ok) return result
      staged = null
      return { ok: true, data: { idMap: result.data.idMap } }
    },
    async discardChanges() {
      staged = null
    },
    hasPendingChanges: () => staged !== null,
    pendingViolations: () => staged?.preview?.violations ?? [],
    captureState: () => structuredClone(staged),
    restoreState(state) {
      staged = structuredClone(state as Staged<Op, State>)
    },
  }
}

const OPERATION_PREFIX = /^operations\[\d+\]\.?/

function withoutOperationPrefix(problem: Problem): Problem {
  const errors = Object.fromEntries(Object.entries(problem.errors).map(([path, messages]) => [path.replace(OPERATION_PREFIX, ''), messages]))
  return { ...problem, errors }
}
