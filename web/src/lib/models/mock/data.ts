import type { Attribution, AttributionDriver, CalcModel, Id, ModelMeasure, ModelParameter, ModelReportUse, ModelVariable } from '../contract'

/** A model as the mock stores it: the document without its server-computed parts. */
export interface StoredModel extends CalcModel {
  variables: ModelVariable[]
  measures: ModelMeasure[]
  parameters: ModelParameter[]
  attributions: Attribution[]
}

export interface StoredReportUse {
  modelId: Id
  use: ModelReportUse
}

export interface Db {
  models: StoredModel[]
  reports: StoredReportUse[]
}

const DAYS = ['@periodDays', '@yearDays']
const CONVENTIONS = ['actual_actual', 'actual_365', 'actual_360', 'thirty_360_us', 'thirty_e_360']

const variable = (key: string, displayName: string, missingBehavior: ModelVariable['missingBehavior']): ModelVariable => ({
  key,
  displayName,
  displayFormat: 'currency',
  unitKey: 'USD',
  missingBehavior,
})

const measure = (key: string, displayName: string, formula: string, scale: number, displayFormat: string, unitKey: string | null = null): ModelMeasure => ({
  key,
  displayName,
  formula,
  outputDataType: 'decimal',
  outputPrecision: 28,
  outputScale: scale,
  roundingMode: 'half_even',
  unitKey,
  displayFormat,
})

const driver = (key: string, displayName: string, ...inputs: string[]): AttributionDriver => ({ key, displayName, inputs })

/** The seed's bank margin and consolidated balances models, and the reports that read them. */
export function createSeed(): Db {
  const models: StoredModel[] = [
    {
      id: 'model-1',
      key: 'bank.margin_model',
      displayName: 'Bank margin',
      currencyCode: 'USD',
      calendarKey: 'gregorian',
      formulaLanguage: '1.2',
      attributionMethod: 'shapley',
      isEnabled: true,
      parameters: [
        {
          key: 'dayCount',
          displayName: 'Day count convention',
          valueType: 'text',
          allowedValues: CONVENTIONS,
          defaultNumber: null,
          defaultText: 'actual_actual',
          description: 'How the yield, cost of funds and net interest margin formulas count the days of a period when they annualize it.',
        },
      ],
      variables: [
        variable('AVERAGE_BALANCE', 'Average balance', 'null'),
        variable('INTEREST_INCOME', 'Interest income', 'null'),
        variable('INTEREST_EXPENSE', 'Interest expense', 'zero'),
        variable('LOAN_BALANCE', 'Average loan balance', 'null'),
      ],
      measures: [
        measure('YIELD', 'Yield', '[m.INTEREST_INCOME] / [m.AVERAGE_BALANCE] / YEARFRAC(@dayCount)', 6, 'percent'),
        measure('COST_OF_FUNDS', 'Cost of funds', '[m.INTEREST_EXPENSE] / [m.AVERAGE_BALANCE] / YEARFRAC(@dayCount)', 6, 'percent'),
        measure('NET_INTEREST_INCOME', 'Net interest income', '[m.INTEREST_INCOME] - [m.INTEREST_EXPENSE]', 2, 'currency', 'USD'),
        measure('NIM', 'Net interest margin', '[m.NET_INTEREST_INCOME] / [m.LOAN_BALANCE] / YEARFRAC(@dayCount)', 6, 'percent'),
      ],
      attributions: [
        {
          metricKey: 'INTEREST_INCOME',
          expression: '[m.AVERAGE_BALANCE] * [m.YIELD] * YEARFRAC(@dayCount)',
          weightVariableKey: null,
          drivers: [driver('volume', 'Volume', 'AVERAGE_BALANCE'), driver('rate', 'Rate', 'YIELD'), driver('days', 'Days', ...DAYS)],
        },
        {
          metricKey: 'NIM',
          expression: '([m.INTEREST_INCOME] - [m.INTEREST_EXPENSE]) / [m.LOAN_BALANCE] / YEARFRAC(@dayCount)',
          weightVariableKey: 'LOAN_BALANCE',
          drivers: [
            driver('asset_income', 'Asset income', 'INTEREST_INCOME'),
            driver('funding_cost', 'Funding cost', 'INTEREST_EXPENSE'),
            driver('earning_assets', 'Earning assets', 'LOAN_BALANCE'),
            driver('days', 'Days', ...DAYS),
          ],
        },
        {
          metricKey: 'YIELD',
          expression: null,
          weightVariableKey: 'AVERAGE_BALANCE',
          drivers: [driver('income', 'Income', 'INTEREST_INCOME'), driver('volume', 'Volume', 'AVERAGE_BALANCE'), driver('days', 'Days', ...DAYS)],
        },
      ],
    },
    {
      id: 'model-2',
      key: 'consolidated.balances',
      displayName: 'Consolidated balances',
      currencyCode: 'USD',
      calendarKey: 'gregorian',
      formulaLanguage: '1.1',
      attributionMethod: 'shapley',
      isEnabled: true,
      parameters: [],
      variables: [
        variable('ENDING_BALANCE', 'Ending balance', 'zero'),
        variable('OPENING_BALANCE', 'Opening balance', 'zero'),
        variable('DEBITS', 'Debits', 'zero'),
        variable('CREDITS', 'Credits', 'zero'),
        variable('FX_ADJUSTMENT', 'FX adjustment', 'zero'),
        variable('ELIMINATIONS', 'Eliminations', 'zero'),
      ],
      measures: [
        measure('NET_MOVEMENT', 'Net movement', '[m.DEBITS] - [m.CREDITS]', 2, 'currency', 'USD'),
        measure('CONSOLIDATED', 'Consolidated balance', '[m.ENDING_BALANCE] + [m.FX_ADJUSTMENT] - [m.ELIMINATIONS]', 2, 'currency', 'USD'),
        measure('CHANGE', 'Change in balance', '[m.ENDING_BALANCE] - [m.OPENING_BALANCE]', 2, 'currency', 'USD'),
        measure('ANNUAL_GROWTH', 'Annualised growth', 'POWER([m.ENDING_BALANCE] / [m.OPENING_BALANCE], @yearDays / @periodDays) - 1', 6, 'percent'),
        measure('FX_SHARE', 'FX share', 'NULLIF([m.FX_ADJUSTMENT], 0) / ABS([m.CONSOLIDATED])', 6, 'percent'),
      ],
      attributions: [],
    },
  ]
  const reader = (modelId: Id, reportId: Id, reportKey: string, displayName: string, isEnabled: boolean): StoredReportUse => ({
    modelId,
    use: { reportId, reportKey, displayName, isEnabled },
  })
  const reports: StoredReportUse[] = [
    reader('model-1', 'report-1', 'bank.margin_statement', 'Bank margin', true),
    reader('model-1', 'report-2', 'bank.deposit_pricing', 'Deposit pricing', true),
    reader('model-1', 'report-3', 'bank.branch_profitability', 'Branch profitability', false),
    reader('model-2', 'report-4', 'consolidated.balance_sheet', 'Consolidated balance sheet', true),
  ]
  return { models, reports }
}
