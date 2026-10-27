import type { ApiResult, ChangeSetCommit, ChangeSetPreview, ChangeSetRequest, Id, Problem } from './contract'
import type { HttpClient } from './http'

/**
 * Universal editing rule: every edit stays pending until the user saves the batch.
 *
 * Each module port implements this change set. The first edit opens it; later edits and reads see the pending state;
 * `commitChanges` applies everything as one batch and `discardChanges` drops it. Lifecycle commands (publish, freeze,
 * delete, activate…) never run while a change set is open.
 */
export interface ChangeSetPort {
  beginChanges(): void
  /** `idMap` maps each temporary id created in the batch to its real id. */
  commitChanges(): Promise<ApiResult<{ idMap: Record<string, Id> }>>
  discardChanges(): Promise<void>
  hasPendingChanges(): boolean
  /** Opaque copy of the pending state, taken around each edit for undo/redo. */
  captureState(): unknown
  /** Puts a state from `captureState` back. Only called while a change set is open. */
  restoreState(state: unknown): void
}

/** In-memory change set for mock adapters: snapshot on begin, restore on discard. */
export function createSnapshotChangeSet<S>(take: () => S, restore: (snapshot: S) => void): ChangeSetPort {
  let snapshot: S | null = null
  return {
    captureState: () => structuredClone(take()),
    restoreState: (state) => restore(structuredClone(state as S)),
    beginChanges() {
      if (snapshot === null) snapshot = structuredClone(take())
    },
    async commitChanges() {
      await new Promise((r) => setTimeout(r, 160))
      snapshot = null
      return { ok: true as const, data: { idMap: {} } }
    },
    async discardChanges() {
      if (snapshot !== null) restore(snapshot)
      snapshot = null
    },
    hasPendingChanges: () => snapshot !== null,
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
