import type { CalcModel, ReportRef } from './types'

const DAYS = ['@periodDays', '@yearDays']

export const initialModels: CalcModel[] = [
  {
    key: 'bank.margin_model',
    name: 'Bank margin',
    currency: 'USD',
    calendar: 'gregorian',
    language: '1.2',
    method: 'shapley',
    enabled: true,
    parameters: [
      {
        key: 'dayCount',
        name: 'Day count convention',
        type: 'text',
        allowedValues: ['actual_actual', 'actual_365', 'actual_360', 'thirty_360_us', 'thirty_e_360'],
        defaultNumber: null,
        defaultText: 'actual_actual',
        description: 'How the yield, cost of funds and net interest margin formulas count the days of a period when they annualize it.',
      },
    ],
    variables: [
      { key: 'AVERAGE_BALANCE', name: 'Average balance', format: 'currency', unit: 'USD', missing: 'null' },
      {
        key: 'INTEREST_INCOME',
        name: 'Interest income',
       
        format: 'currency',
        unit: 'USD',
        missing: 'null',
        attribution: {
          expression: '[m.AVERAGE_BALANCE] * [m.YIELD] * YEARFRAC(@dayCount)',
          useDefault: false,
          drivers: [
            { key: 'volume', name: 'Volume', inputs: ['AVERAGE_BALANCE'] },
            { key: 'rate', name: 'Rate', inputs: ['YIELD'] },
            { key: 'days', name: 'Days', inputs: DAYS },
          ],
        },
      },
      { key: 'INTEREST_EXPENSE', name: 'Interest expense', format: 'currency', unit: 'USD', missing: 'zero' },
      { key: 'LOAN_BALANCE', name: 'Average loan balance', format: 'currency', unit: 'USD', missing: 'null' },
    ],
    measures: [
      {
        key: 'YIELD',
        name: 'Yield',
        formula: '[m.INTEREST_INCOME] / [m.AVERAGE_BALANCE] / YEARFRAC(@dayCount)',
        precision: 28,
        scale: 6,
        rounding: 'half_even',
        unit: '',
        format: 'percent',
        attribution: {
          weight: 'AVERAGE_BALANCE',
          useDefault: false,
          drivers: [
            { key: 'income', name: 'Income', inputs: ['INTEREST_INCOME'] },
            { key: 'volume', name: 'Volume', inputs: ['AVERAGE_BALANCE'] },
            { key: 'days', name: 'Days', inputs: DAYS },
          ],
        },
      },
      {
        key: 'COST_OF_FUNDS',
        name: 'Cost of funds',
        formula: '[m.INTEREST_EXPENSE] / [m.AVERAGE_BALANCE] / YEARFRAC(@dayCount)',
        precision: 28,
        scale: 6,
        rounding: 'half_even',
        unit: '',
        format: 'percent',
      },
      {
        key: 'NET_INTEREST_INCOME',
        name: 'Net interest income',
        formula: '[m.INTEREST_INCOME] - [m.INTEREST_EXPENSE]',
        precision: 28,
        scale: 2,
        rounding: 'half_even',
        unit: 'USD',
        format: 'currency',
      },
      {
        key: 'NIM',
        name: 'Net interest margin',
        formula: '[m.NET_INTEREST_INCOME] / [m.LOAN_BALANCE] / YEARFRAC(@dayCount)',
        precision: 28,
        scale: 6,
        rounding: 'half_even',
        unit: '',
        format: 'percent',
        attribution: {
          expression: '([m.INTEREST_INCOME] - [m.INTEREST_EXPENSE]) / [m.LOAN_BALANCE] / YEARFRAC(@dayCount)',
          weight: 'LOAN_BALANCE',
          useDefault: false,
          drivers: [
            { key: 'asset_income', name: 'Asset income', inputs: ['INTEREST_INCOME'] },
            { key: 'funding_cost', name: 'Funding cost', inputs: ['INTEREST_EXPENSE'] },
            { key: 'earning_assets', name: 'Earning assets', inputs: ['LOAN_BALANCE'] },
            { key: 'days', name: 'Days', inputs: DAYS },
          ],
        },
      },
    ],
  },
  {
    key: 'consolidated.balances',
    name: 'Consolidated balances',
    currency: 'USD',
    calendar: 'gregorian',
    language: '1.1',
    method: 'shapley',
    enabled: true,
    parameters: [],
    variables: [
      { key: 'ENDING_BALANCE', name: 'Ending balance', format: 'currency', unit: 'USD', missing: 'zero' },
      { key: 'OPENING_BALANCE', name: 'Opening balance', format: 'currency', unit: 'USD', missing: 'zero' },
      { key: 'DEBITS', name: 'Debits', format: 'currency', unit: 'USD', missing: 'zero' },
      { key: 'CREDITS', name: 'Credits', format: 'currency', unit: 'USD', missing: 'zero' },
      { key: 'FX_ADJUSTMENT', name: 'FX adjustment', format: 'currency', unit: 'USD', missing: 'zero' },
      { key: 'ELIMINATIONS', name: 'Eliminations', format: 'currency', unit: 'USD', missing: 'zero' },
    ],
    measures: [
      { key: 'NET_MOVEMENT', name: 'Net movement', formula: '[m.DEBITS] - [m.CREDITS]', precision: 28, scale: 2, rounding: 'half_even', unit: 'USD', format: 'currency' },
      { key: 'CONSOLIDATED', name: 'Consolidated balance', formula: '[m.ENDING_BALANCE] + [m.FX_ADJUSTMENT] - [m.ELIMINATIONS]', precision: 28, scale: 2, rounding: 'half_even', unit: 'USD', format: 'currency' },
      { key: 'CHANGE', name: 'Change in balance', formula: '[m.ENDING_BALANCE] - [m.OPENING_BALANCE]', precision: 28, scale: 2, rounding: 'half_even', unit: 'USD', format: 'currency' },
      { key: 'ANNUAL_GROWTH', name: 'Annualised growth', formula: 'POWER([m.ENDING_BALANCE] / [m.OPENING_BALANCE], @yearDays / @periodDays) - 1', precision: 28, scale: 6, rounding: 'half_even', unit: '', format: 'percent' },
      { key: 'FX_SHARE', name: 'FX share', formula: 'NULLIF([m.FX_ADJUSTMENT], 0) / ABS([m.CONSOLIDATED])', precision: 28, scale: 6, rounding: 'half_even', unit: '', format: 'percent' },
    ],
  },
]

export const reportRefs: ReportRef[] = [
  { key: 'bank.margin_statement', name: 'Bank margin', type: 'margin_analysis', model: 'bank.margin_model', layout: 'statement', defaultProfile: 'gl_monthly', enabled: true },
  { key: 'bank.deposit_pricing', name: 'Deposit pricing', type: 'deposit_pricing', model: 'bank.margin_model', layout: 'statement', defaultProfile: 'gl_monthly', enabled: true },
  { key: 'bank.branch_profitability', name: 'Branch profitability', type: 'branch_profitability', model: 'bank.margin_model', layout: 'statement', defaultProfile: 'loan_based', enabled: false },
  { key: 'consolidated.balance_sheet', name: 'Consolidated balance sheet', type: 'balance_sheet', model: 'consolidated.balances', layout: 'statement', defaultProfile: 'gl_monthly', enabled: true },
]
