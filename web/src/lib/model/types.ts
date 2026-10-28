import type { DisplayFormat } from '@/lib/report/types'

export type MissingValue = 'null' | 'zero'
export type AttributionMethod = 'shapley' | 'sequential'
export type FormulaLanguage = '1.0' | '1.1' | '1.2'
export type ParameterType = 'number' | 'text'
export type ParameterValue = string | number

export interface ModelParameter {
  key: string
  name: string
  type: ParameterType
  /** Text only; empty means any text is allowed. */
  allowedValues: string[]
  defaultNumber: number | null
  defaultText: string | null
  description: string
}
export type Rounding = 'half_even' | 'half_up' | 'down'

export interface Driver {
  key: string
  name: string
  /** Metric keys of the expression, plus @periodDays / @yearDays. */
  inputs: string[]
}

export interface Attribution {
  /** Optional for a calculated measure (its own formula splits), required for a variable. */
  expression?: string
  weight?: string
  /** Generated drivers: one per metric read, plus a Days driver. */
  useDefault: boolean
  drivers: Driver[]
}

export interface ModelVariable {
  key: string
  name: string
  format: DisplayFormat
  unit: string
  missing: MissingValue
  attribution?: Attribution
}

export interface ModelMeasure {
  key: string
  name: string
  formula: string
  precision: number
  scale: number
  rounding: Rounding
  unit: string
  format: DisplayFormat
  attribution?: Attribution
}

export interface CalcModel {
  key: string
  name: string
  currency: string
  calendar: string
  language: FormulaLanguage
  method: AttributionMethod
  enabled: boolean
  parameters: ModelParameter[]
  variables: ModelVariable[]
  measures: ModelMeasure[]
}

export interface ReportRef {
  key: string
  name: string
  type: string
  model: string
  layout: 'statement' | 'list'
  defaultProfile: string
  enabled: boolean
}

export interface RuleViolation {
  rule: string
  message: string
}
