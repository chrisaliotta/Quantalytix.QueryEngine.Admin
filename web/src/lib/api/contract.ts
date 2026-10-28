/**
 * Shared kernel of the frontend contract (blueprint 02). Every type mirrors the schema of the same name in
 * api/openapi/contract/common.yaml. Module contracts re-export what they use.
 */

/** Opaque id. Inside a change set, items created by an earlier operation carry a temporary `tmp:<uuid>` id. */
export type Id = string

/** Temporary id of an item created inside a change set; the commit's `idMap` gives its real id. */
export const newTempId = (): Id => `tmp:${crypto.randomUUID()}`

/** `Key`: most natural keys (OI-29). */
export const KEY_PATTERN = /^[A-Za-z][A-Za-z0-9_]*$/
/** `DottedKey`: dataset, calculation model, report, mapping set and bridge keys (OI-29). */
export const DOTTED_KEY_PATTERN = /^[A-Za-z][A-Za-z0-9_.]*$/

export type VersionStatus = 'draft' | 'validated' | 'published' | 'retired'

/* ───────────────────────── Vocabularies read by more than one module ───────────────────────── */

export type DimensionKind = 'categorical' | 'hierarchical' | 'range' | 'time'
export type DimensionPurpose = 'analytical' | 'report'
export type DefinitionOrigin = 'system' | 'user'
export type AssumptionSetStatus = 'active' | 'historical'
export type BindingKind = 'direct' | 'mapped' | 'range' | 'bridge' | 'time'
export type Periodicity = 'day' | 'month' | 'quarter' | 'year'
export type MeasureKind = 'aggregate' | 'weighted_sum' | 'weighted_average' | 'period_average' | 'formula' | 'specialized'
export type ComponentRole = 'value' | 'weight' | 'period_days' | 'numerator' | 'denominator'
export type RoundingMode = 'half_even' | 'half_up' | 'down' | 'up' | 'truncate'
/** `descendants` is used only by report bridges and report rules (OI-28). */
export type SourceSelection = 'member' | 'descendants'
export type VariableMissingBehavior = 'null' | 'zero'

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
  reasons: DeleteBlocker[]
}

/** One list for every module; `referenced_by_<kind>` names the kind of object that still reads the target (01, Delete checks). */
export type DeleteBlockerCode =
  | 'not_removable'
  | 'system_dimension'
  | 'published_version'
  | 'live_version'
  | 'has_published_versions'
  | 'has_children'
  | 'required_by_profile'
  | 'assigned_by_bands'
  | 'named_by_consumers'
  | 'owned_by_report'
  | 'referenced_by_member'
  | 'referenced_by_dataset_binding'
  | 'referenced_by_mapping_set'
  | 'referenced_by_bridge'
  | 'referenced_by_calculation_dimension'
  | 'referenced_by_report_scenario'
  | 'referenced_by_report_source'
  | 'referenced_by_assumption_set'
  | 'referenced_by_release'
  | 'referenced_by_report'

export interface DeleteBlocker extends Violation {
  code: DeleteBlockerCode
}

/** Body of a freeze for dimensions, mapping sets and bridges (OI-14). */
export interface FreezeInput {
  note: string | null
}

/** The port's freeze answer for any living kind; `live` is null when a rule failed and nothing changed. */
export interface FreezeResult<V> {
  report: ValidationReport
  live: V | null
}

/** How a role's members group. */
export type Grouping =
  | { mode: 'member' }
  | { mode: 'level'; levelKey: string }
  | { mode: 'ancestor'; memberKey: string }
  | { mode: 'depth'; depth: number }

/** The query engine's structured `filterNode`; dimension operands name calculation dimension keys. */
export type FilterNode =
  | { operator: 'and' | 'or'; operands: FilterNode[] }
  | { operator: 'not'; operand: FilterNode }
  | { operator: 'eq' | 'ne' | 'lt' | 'le' | 'gt' | 'ge'; left: FilterOperand; right: FilterOperand }
  | { operator: 'in'; left: FilterOperand; values: FilterOperand[] }
  | { operator: 'isNull' | 'isNotNull'; operand: FilterOperand }

export type FilterOperand =
  | { type: 'field' | 'measure' | 'parameter'; id: string }
  | { type: 'dimension'; id: string; grouping?: Grouping }
  | { type: 'literal'; value: string | number | boolean | null }

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
