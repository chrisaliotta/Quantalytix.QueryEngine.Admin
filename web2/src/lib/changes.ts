/**
 * Universal editing rule: every edit stays pending until the user saves the batch.
 *
 * Each module port implements this change set. The first edit opens it; later edits and reads see the pending state;
 * `commitChanges` applies everything as one batch and `discardChanges` drops it. Lifecycle commands (publish, freeze,
 * delete, activate…) never run while a change set is open.
 */
export interface ChangeSetPort {
  beginChanges(): void
  commitChanges(): Promise<{ ok: true } | { ok: false; problem: { title: string; detail?: string } }>
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
      return { ok: true as const }
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
