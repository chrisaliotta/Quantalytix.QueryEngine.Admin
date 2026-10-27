/**
 * Shared kernel of the frontend contract (blueprint 02). Every type mirrors the schema of the same name in
 * api/openapi/contract/common.yaml. Module contracts re-export what they use.
 */

/** Opaque id. Inside a change set, items created by an earlier operation carry a temporary `tmp:<uuid>` id. */
export type Id = string

export type VersionStatus = 'draft' | 'validated' | 'published' | 'retired'

/** The one error body of every failed call (01, Problem body). */
export interface Problem {
  /** Stable problem code, e.g. `urn:qe:problem:rules-violated`. */
  type: string
  /** Short, user-facing summary. */
  title: string
  status: 400 | 401 | 403 | 404 | 409 | 422 | 500
  detail?: string
  /** Input path → messages. `''` is the form-level key. Paths are camelCase, dotted, indexed: `components[0].fieldId`. */
  errors: Record<string, string[]>
  /** Rule violations (422) and delete blockers (409). */
  violations?: Violation[]
  traceId?: string
}

export interface Violation {
  /** The rule code, snake_case, e.g. `member_key_unique`. */
  code: string
  /** Phrased for display. */
  message: string
  /** Where to take the user. */
  target?: ViolationTarget
}

export interface ViolationTarget {
  /** Module-specific area: a dataset tab, a dimension tab, a release manifest row, a report section. */
  area: string
  itemId?: Id
  itemKey?: string
  /** Search text that narrows a table to the offending rows. */
  filter?: string
}

export type ApiResult<T> = { ok: true; data: T } | { ok: false; problem: Problem }

/** Answer of a read-only `check`. */
export interface ValidationReport {
  versionId: Id
  checkedAt: string
  violations: Violation[]
}

/** Answer of `GET <resource>/delete-check`; a blocked `DELETE` answers 409 with the same violations. */
export interface DeleteCheck {
  allowed: boolean
  /** One-sentence summary when blocked. */
  message: string | null
  reasons: Violation[]
}

export interface VocabularyTerm<V extends string = string> {
  value: V
  label: string
  description?: string
}

/** Body of `POST <aggregate>/changes/preview` and `POST <aggregate>/changes`. */
export interface ChangeSetRequest<Op> {
  operations: Op[]
}

/** 200 answer of a preview: the aggregate after the operations, and the rule findings that do not block saving. */
export interface ChangeSetPreview<State> {
  state: State
  violations: Violation[]
}

/** 200 answer of a commit: the stored aggregate and the real id of each temporary id. */
export interface ChangeSetCommit<State> {
  state: State
  idMap: Record<string, Id>
}

/** Thrown by `unwrap` so SWR surfaces a failed read as its error. */
export class ProblemError extends Error {
  constructor(public problem: Problem) {
    super(problem.title)
  }
}

/** The data of a successful result; throws `ProblemError` otherwise. */
export async function unwrap<T>(result: Promise<ApiResult<T>>): Promise<T> {
  const r = await result
  if (!r.ok) throw new ProblemError(r.problem)
  return r.data
}
