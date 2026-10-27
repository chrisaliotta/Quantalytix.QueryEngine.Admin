# 15 Calculation models

## Purpose and sources

A calculation model holds the variables, calculated measures, parameters and attributions that reports over it read.
Models are not versioned: every enabled report reads the model as it stands, so every write is checked against the
model's own rules and against every enabled report over it before it is made.

Sources: `web2/src/lib/model/` (`types.ts`, `store.tsx`, `rules.ts`, `data.ts`) and `components/models/*`; current
`CalculationModelsController`, `VariablesController`, `CalculatedMeasuresController`,
`MetricAttributionsController`, `ModelParametersController`, `A/CalculationModels/ModelChanges.cs`; schema
`calculation/`; engine README (formula language 1.0–1.2, attribution).

`web2` has no port for models; its React store holds the models, a draft per model, and runs the rules in the browser.
This file defines the contract.

## Domain rules

| # | Rule | Source |
|---|---|---|
| C1 | Variables and calculated measures share one key namespace per model. | Schema `qe_calculated_measure` |
| C2 | A formula reads only the model's own metric keys (`[m.KEY]`), `@periodDays`, `@yearDays` and (1.2) the model's parameters; never itself; no cycles. | Schema doc §18 rules 5, 9 |
| C3 | Formula language: 1.0 base; 1.1 adds `POWER`; 1.2 adds `@param`, text comparisons (`=`, `<>`, `IN`) and `YEARFRAC(convention)`. A formula using a feature needs that language. | Engine README A.5 |
| C4 | Text only in text comparisons or as the `YEARFRAC` argument; the argument is a convention literal or a text parameter whose allowed values are all conventions. | §18 rule 9 |
| C5 | A parameter sets exactly the default its type names; only text parameters have allowed values, and they include the default; a parameter that formulas or row values use is never deleted or re-keyed. | §18 rule 8 |
| C6 | An attribution names a metric of its model; its expression reads only model keys and day parameters; drivers (at most 8, never `residual`) cover each reference exactly once; its weight variable is a variable of the model; a variable needs an expression. | §18 rule 6; engine A.7 |
| C7 | Every write is applied in memory and refused, writing nothing, when it breaks a model rule or an enabled report over the model. | Schema doc §15–16; API model gate |
| C8 | A model is deleted only when no report reads it. | API |
| C9 | `model_key` is fixed. | Agreed (OI-45) |

## Frontend contract (Agreed, new `lib/models/contract.ts`)

Types follow `web2`'s `lib/model/types.ts`, renamed to the naming rule and given ids:

```ts
export type FormulaLanguage = '1.0' | '1.1' | '1.2'
export type AttributionMethod = 'shapley' | 'sequential'
export type MissingBehavior = 'null' | 'zero'
export type ParameterValueType = 'number' | 'text'
export type RoundingMode = 'half_even' | 'half_up' | 'down' | 'up' | 'truncate'

export interface CalcModel {
  id: Id
  key: string
  displayName: string
  currencyCode: string
  calendarKey: string
  formulaLanguage: FormulaLanguage
  attributionMethod: AttributionMethod
  isEnabled: boolean
}

export interface ModelVariable { key: string; displayName: string; displayFormat: string; unitKey: string | null; missingBehavior: MissingBehavior }
export interface ModelMeasure {
  key: string; displayName: string; formula: string
  outputDataType: 'decimal' | 'int64' | 'int32'; outputPrecision: number | null; outputScale: number | null
  roundingMode: RoundingMode | null; unitKey: string | null; displayFormat: string
}
export interface ModelParameter {
  key: string; displayName: string; valueType: ParameterValueType
  allowedValues: string[] | null; defaultNumber: number | null; defaultText: string | null; description: string | null
}
export interface Driver { key: string; displayName: string; inputs: string[] }   // sort order = position
export interface Attribution { metricKey: string; expression: string | null; weightVariableKey: string | null; drivers: Driver[] }

export interface ModelListItem extends CalcModel { variableCount: number; measureCount: number; usedBy: ReportUse[] }
export interface ReportUse { reportId: Id; reportKey: string; displayName: string; isEnabled: boolean }

export interface ModelDocument extends CalcModel {
  variables: ModelVariable[]
  measures: ModelMeasure[]
  parameters: ModelParameter[]
  /** Explicit attribution rows only; a metric without one uses default drivers (see effectiveDrivers). */
  attributions: Attribution[]
  /** Server-computed: drivers in effect per metric, default or explicit. */
  effectiveDrivers: Record<string, Driver[]>
  usedBy: ReportUse[]
}

export interface FormulaCheck {
  errors: { message: string; start: number; end: number }[]
  reads: string[]          // metric keys
  parameters: string[]     // @param keys
  requiresLanguage: FormulaLanguage
}
```

**Deltas from `web2` `lib/model/types.ts`:**

| `web2` | Contract | Note |
|---|---|---|
| `name`, `currency`, `calendar`, `language`, `method`, `enabled` | `displayName`, `currencyCode`, `calendarKey`, `formulaLanguage`, `attributionMethod`, `isEnabled` | Naming rule (OI-10) |
| `ModelVariable.attribution`, `ModelMeasure.attribution` | Top-level `attributions[]` keyed by `metricKey` | Matches `qe_metric_attribution` |
| `Attribution.useDefault` | Absence of an attribution row | Default drivers are computed, not stored |
| `ModelMeasure.precision`, `scale`, `rounding`, `format`, `unit` | `output*`, `roundingMode`, `displayFormat`, `unitKey` | Schema names; `outputDataType` added (schema default `decimal`) |
| `ModelParameter.name`, `type`, `allowedValues: string[]` (empty = any) | `displayName`, `valueType`, `allowedValues: string[] \| null` (null = any) | Schema `allowed_values_json` NULL means any |
| `ReportRef` static list, `usedBy(key)` | `usedBy: ReportUse[]` computed by the server | OI-44 |
| `RuleViolation { rule, message }` | Kernel `Violation { code, message, target }` | |

### Port

```ts
export interface ModelsApi extends ChangeSetPort {
  // reads
  listModels(): Promise<ModelListItem[]>
  getModel(id: Id): Promise<ApiResult<ModelDocument>>
  checkFormula(id: Id, formula: string, selfKey: string | null): Promise<FormulaCheck>      // OI-43
  checkDeleteModel(id: Id): Promise<DeleteCheck>

  // edits (staged per model)
  updateModel(id: Id, patch: Partial<Pick<CalcModel, 'displayName' | 'currencyCode' | 'calendarKey' | 'formulaLanguage' | 'attributionMethod' | 'isEnabled'>>): Promise<ApiResult<CalcModel>>
  saveVariable(id: Id, input: ModelVariable & { originalKey?: string }): Promise<ApiResult<ModelVariable>>
  deleteVariable(id: Id, key: string): Promise<ApiResult<null>>
  saveMeasure(id: Id, input: ModelMeasure & { originalKey?: string }): Promise<ApiResult<ModelMeasure>>
  deleteMeasure(id: Id, key: string): Promise<ApiResult<null>>
  saveParameter(id: Id, input: ModelParameter & { originalKey?: string }): Promise<ApiResult<ModelParameter>>
  deleteParameter(id: Id, key: string): Promise<ApiResult<null>>
  setAttribution(id: Id, input: Attribution): Promise<ApiResult<Attribution>>
  clearAttribution(id: Id, metricKey: string): Promise<ApiResult<null>>

  // commands
  createModel(input: { key: string; displayName: string; currencyCode: string; formulaLanguage: FormulaLanguage; attributionMethod: AttributionMethod }): Promise<ApiResult<CalcModel>>
  deleteModel(id: Id): Promise<ApiResult<null>>
}
```

`originalKey` is present when an existing metric or parameter is edited; metric and parameter keys are fixed once
saved (C5, C9), so a changed key in an update is refused. `web2`'s editors allow typing a key only on new rows, which
already matches.

## Refusal semantics (Agreed, OI-42)

`web2` refuses an edit at once, before it is staged, when it **introduces** a violation (the store compares rule
results before and after the edit) and shows "This change was refused". Saving the draft is then always accepted. The
current API refuses each individual write that breaks a rule.

Preview implements the refusal. Each edit posts the operations so far to
`calculation-models/{id}/changes/preview`; the server applies them and answers `422` with `violations` when the last
operation introduces a violation of a model rule or a report rule of any enabled report over the model; the staged port
drops the operation and the UI shows `RefusalAlert`. Commit re-runs the same gate. Violations that already existed
before the change set opened do not refuse new edits (the `web2` behavior), but are listed in preview `violations`.

This makes the two report-side refusals `web2` fakes against the open report (`parameter_filter_clash`,
`parameter_allowed_values`) real, and checks every enabled report, not just the open one.

## HTTP API

Prefix `/api/tenants/{t}`. Open to any tenant user (OI-09).

| Method | Route | Request | Response | Errors | Port |
|---|---|---|---|---|---|
| GET | `calculation-models` | | `ModelListItem[]` | | `listModels` |
| POST | `calculation-models` | create input | `201 CalcModel` (disabled, empty) | 400, 409 key taken | `createModel` |
| GET | `calculation-models/{id}` | | `ModelDocument` | 404 | `getModel` |
| POST | `calculation-models/{id}/formula-check` | `{ formula, selfKey }` | `FormulaCheck` | 404 | `checkFormula` |
| GET | `calculation-models/{id}/delete-check` | | `DeleteCheck` | | `checkDeleteModel` |
| DELETE | `calculation-models/{id}` | | `204` | 409 `referenced_by_report` | `deleteModel` |
| POST | `calculation-models/{id}/changes/preview` | `ChangeSetRequest<ModelOperation>` | `ChangeSetPreview<ModelDocument>` | 400, 422 refused | edits |
| POST | `calculation-models/{id}/changes` | `ChangeSetRequest<ModelOperation>` | `ChangeSetCommit<ModelDocument>` | 400, 422 | `commitChanges` |

`formula-check` uses `Quantalytix.QueryEngine.Domain.ExpressionParser` for syntax (with the character position it
reports) and the model's keys, parameters and language for references and typing. It reads saved model state; the
formula editor passes pending keys through preview instead when it needs them.

## Change-set operations

```ts
type ModelOperation =
  | { op: 'updateModel'; patch: Partial<Pick<CalcModel, 'displayName' | 'currencyCode' | 'calendarKey' | 'formulaLanguage' | 'attributionMethod' | 'isEnabled'>> }
  | { op: 'saveVariable'; input: ModelVariable; originalKey?: string }
  | { op: 'deleteVariable'; key: string }
  | { op: 'saveMeasure'; input: ModelMeasure; originalKey?: string }
  | { op: 'deleteMeasure'; key: string }
  | { op: 'saveParameter'; input: ModelParameter; originalKey?: string }
  | { op: 'deleteParameter'; key: string }
  | { op: 'setAttribution'; input: Attribution }
  | { op: 'clearAttribution'; metricKey: string }
```

A commit that changes a model read by an enabled report advances `semantic_revision`.

## Rule codes

Model rules (current API): `formula_references_resolve`, `formulas_acyclic`, `formulas_fit_language`,
`formula_parameters_resolve`, `attribution_references_resolve`, `attribution_drivers`,
`attribution_weight_variable`. Report rules re-run for every enabled report over the model
([16](16-reports.md#rule-codes)). Delete blockers: `referenced_by_report` ([01](01-api-conventions.md#delete-checks-agreed-oi-07)).

Mapping from `web2` (confirm each against the API's rule implementations during the refactor): `parameters_resolve` → `formula_parameters_resolve`; `parameter_filter_clash` →
`report_defaults_resolve` (a parameter key colliding with a default filter field); `parameter_allowed_values` →
`report_row_parameters_resolve`; `model_exists` → `404`.

## Schema mapping

| Contract | Table |
|---|---|
| `CalcModel` | `qe_calculation_model` |
| `ModelVariable` | `qe_variable` |
| `ModelMeasure` | `qe_calculated_measure` (`expression_text` → `formula`) |
| `ModelParameter` | `qe_model_parameter` |
| `Attribution` | `qe_metric_attribution` + `qe_metric_attribution_driver` (`sort_order` = position, `input_keys_json` = `inputs`) |
| `usedBy` | `qe_report.calculation_model_id` |

## Deltas from the current API

| Change | Item |
|---|---|
| Keep | The model gate (in-memory apply + model rules + report rules); delete blocked while read |
| Change | Per-item variable, measure, parameter, attribution writes become change-set operations |
| Change | `PUT calculation-models/{id}` becomes the `updateModel` operation |
| New | `ModelDocument` read model (one call), `effectiveDrivers`, `usedBy`, `formula-check`, `delete-check`, preview |

## Decisions applied

[OI-01](90-open-issues.md#oi-01), [OI-05](90-open-issues.md#oi-05), [OI-10](90-open-issues.md#oi-10),
[OI-42](90-open-issues.md#oi-42), [OI-43](90-open-issues.md#oi-43), [OI-44](90-open-issues.md#oi-44),
[OI-45](90-open-issues.md#oi-45).
