import type { ReportState, RowNode, RowKind } from './types'

function row(
  key: string,
  label: string,
  kind: RowKind,
  children: RowNode[] = [],
  extra: Partial<RowNode> = {},
): RowNode {
  return { key, label, kind, hidden: false, children, ...extra }
}

export const initialReport: ReportState = {
  settings: {
    key: 'bank.margin_statement',
    name: 'Bank margin',
    type: 'margin_analysis',
    model: 'bank.margin_model',
    currency: 'USD',
  periodicity: 'Month',

    layout: 'Statement',
    subtotals: 'Above',
    defaultProfile: 'gl_monthly',
    defaultBreakout: [],
    defaultFilters: [],
  },
  enabled: true,
  rows: [
    row(
      'TOTAL',
      'Total',
      'subtotal',
      [
        row('ASSETS', 'Assets', 'heading', [
          row('CASH', 'Cash and equivalents', 'line'),
          row(
            'LOANS',
            'Loans',
            'line',
            [row('MORTGAGE', 'Mortgage loans', 'line'), row('AUTO', 'Auto loans', 'line'), row('COMMERCIAL', 'Commercial loans', 'line')],
            { styleKey: 'highlight' },
          ),
        ]),
        row('FUNDING', 'Funding', 'heading', [
          row('DEPOSITS', 'Deposits', 'line', [], { parameters: { dayCount: 'actual_360' } }),
          row('CAPITAL', 'Capital', 'line'),
        ]),
      ],
      { parameters: { dayCount: 'actual_actual' } },
    ),
    row('nim', 'Net interest margin', 'formula', [], { measureKey: 'NIM', columnKey: 'yield' }),
  ],
  variables: [
    { key: 'AVERAGE_BALANCE', name: 'Average balance' },
    { key: 'LOAN_BALANCE', name: 'Average loan balance' },
    { key: 'INTEREST_INCOME', name: 'Interest income' },
    { key: 'INTEREST_EXPENSE', name: 'Interest expense' },
  ],
  measures: [
    {
      key: 'YIELD',
      name: 'Yield',
      formula: '[m.INTEREST_INCOME] / [m.AVERAGE_BALANCE] * @yearDays / @periodDays',
    },
    {
      key: 'COST_OF_FUNDS',
      name: 'Cost of funds',
      formula: '[m.INTEREST_EXPENSE] / [m.AVERAGE_BALANCE] * @yearDays / @periodDays',
    },
    {
      key: 'NET_INTEREST_INCOME',
      name: 'Net interest income',
      formula: '[m.INTEREST_INCOME] - [m.INTEREST_EXPENSE]',
    },
    {
      key: 'NIM',
      name: 'Net interest margin',
      formula: '[m.NET_INTEREST_INCOME] / [m.LOAN_BALANCE] * @yearDays / @periodDays',
    },
  ],
  columns: [
    { key: 'avgBalance', label: 'Avg balance', kind: 'variable', metric: 'AVERAGE_BALANCE', format: 'currency', visible: true },
    { key: 'loanBalance', label: 'Avg loan balance', kind: 'variable', metric: 'LOAN_BALANCE', format: 'currency', visible: false },
    { key: 'interestIncome', label: 'Interest income', kind: 'variable', metric: 'INTEREST_INCOME', format: 'currency', visible: true },
    { key: 'interestExpense', label: 'Interest expense', kind: 'variable', metric: 'INTEREST_EXPENSE', format: 'currency', visible: true },
    { key: 'yield', label: 'Yield', kind: 'calculated', metric: 'YIELD', format: 'percent', visible: true },
    { key: 'nim', label: 'NIM', kind: 'calculated', metric: 'NIM', format: 'percent', visible: true },
  ],
  accounts: [
    {
      key: 'ASSETS',
      label: 'Assets',
      children: [
        { key: '1000', label: 'Cash' },
        { key: '1100', label: 'Loans' },
        { key: '1200', label: 'Securities' },
        { key: '1300', label: 'Premises' },
      ],
    },
    {
      key: 'LIABILITIES',
      label: 'Liabilities',
      children: [
        { key: '2000', label: 'Deposits' },
        { key: '2100', label: 'Borrowings' },
      ],
    },
    {
      key: 'EQUITY',
      label: 'Equity',
      children: [
        { key: '3000', label: 'Capital' },
        { key: '3100', label: 'Retained earnings' },
      ],
    },
    {
      key: 'REVENUE',
      label: 'Revenue',
      children: [
        { key: '4000', label: 'Interest income' },
        { key: '4100', label: 'Fee income' },
      ],
    },
    {
      key: 'EXPENSE',
      label: 'Expense',
      children: [
        { key: '5000', label: 'Interest expense' },
        { key: '5100', label: 'Salaries' },
      ],
    },
  ],
  datasets: [
    {
      key: 'bank.balance_monthly',
      name: 'Monthly balances',
      measures: ['periodAverage', 'endingBalance', 'openingBalance'],
      fields: ['accountKey', 'branch', 'officer', 'currency', 'segment'],
      timeFields: ['reportingPeriod', 'postedDate'],
      roles: ['account', 'branch', 'officer'],
    },
    {
      key: 'bank.balance_daily',
      name: 'Daily balances',
      measures: ['periodAverage', 'endingBalance'],
      fields: ['accountKey', 'branch', 'officer', 'currency'],
      timeFields: ['asOfDate'],
      roles: ['account', 'branch', 'officer'],
    },
    {
      key: 'bank.income_monthly',
      name: 'Monthly income',
      measures: ['totalValue', 'periodAverage'],
      fields: ['accountKey', 'branch', 'officer', 'currency'],
      timeFields: ['reportingPeriod'],
      roles: ['account', 'branch', 'officer'],
    },
    {
      key: 'mortgage.loan_daily',
      name: 'Loan-level daily',
      measures: ['periodAverage', 'endingBalance', 'accruedInterest'],
      fields: ['product', 'branch', 'officer', 'rateType', 'vintage'],
      timeFields: ['asOfDate'],
      fieldValues: { product: ['MORTGAGE', 'AUTO', 'COMMERCIAL'] },
      roles: ['branch', 'officer'],
    },
    {
      key: 'bank.plan_monthly',
      name: 'Monthly plan',
      measures: ['plannedBalance', 'plannedIncome', 'plannedExpense'],
      fields: ['line', 'branch', 'planVersion'],
      timeFields: ['planPeriod'],
      fieldValues: { line: ['CASH', 'LOANS', 'MORTGAGE', 'AUTO', 'COMMERCIAL', 'DEPOSITS', 'CAPITAL'] },
      roles: ['branch', 'planVersion'],
    },
    {
      key: 'bank.nii_simulation',
      name: 'NII simulation',
      measures: ['projectedBalance', 'projectedIncome', 'projectedExpense'],
      fields: ['line', 'branch', 'planVersion', 'rateShock'],
      timeFields: ['projectionPeriod'],
      fieldValues: { line: ['CASH', 'LOANS', 'MORTGAGE', 'AUTO', 'COMMERCIAL', 'DEPOSITS', 'CAPITAL'] },
      roles: ['branch', 'planVersion', 'rateShock'],
    },
    {
      key: 'bank.fx_positions',
      name: 'FX positions',
      measures: ['notional', 'markToMarket'],
      fields: ['accountKey', 'currencyPair', 'desk'],
      timeFields: ['asOfDate'],
      roles: ['account'],
    },
  ],
  sources: [
    {
      key: 'gl_monthly',
      name: 'GL monthly balances',
      dataset: 'bank.balance_monthly',
      method: 'mapped',
      dimension: 'account',
      timeField: 'reportingPeriod',
      bindings: [
        { id: 'b1', variable: 'AVERAGE_BALANCE', measure: 'periodAverage', filters: [] },
        { id: 'b2', variable: 'LOAN_BALANCE', measure: 'periodAverage', filters: [] },
      ],
      mappings: [
        { id: 'm1', rowKey: 'LOANS', memberKey: 'REVENUE', factor: 1 },
        { id: 'm2', rowKey: 'LOANS', memberKey: '1100', factor: 1 },
        { id: 'm3', rowKey: 'CASH', memberKey: '1000', factor: 1 },
        { id: 'm4', rowKey: 'DEPOSITS', memberKey: 'LIABILITIES', factor: 1 },
        { id: 'm4b', rowKey: 'DEPOSITS', memberKey: 'EXPENSE', factor: 1 },
        { id: 'm4c', rowKey: 'CAPITAL', memberKey: 'EQUITY', factor: 1 },
      ],
      leftOut: [],
    },
    {
      key: 'gl_daily',
      name: 'GL daily balances',
      dataset: 'bank.balance_daily',
      method: 'mapped',
      dimension: 'account',
      timeField: 'asOfDate',
      bindings: [
        { id: 'b3', variable: 'AVERAGE_BALANCE', measure: 'periodAverage', filters: [] },
        { id: 'b4', variable: 'LOAN_BALANCE', measure: 'periodAverage', filters: [] },
      ],
      mappings: [
        { id: 'm5', rowKey: 'LOANS', memberKey: 'ASSETS', factor: 1 },
        { id: 'm6', rowKey: 'LOANS', memberKey: '1000', factor: -1 },
        { id: 'm7', rowKey: 'CASH', memberKey: '1000', factor: 1 },
        { id: 'm8', rowKey: 'DEPOSITS', memberKey: '2000', factor: 1 },
        { id: 'm9', rowKey: 'CAPITAL', memberKey: '3000', factor: 1 },
      ],
      leftOut: ['4000', '4100', '5000', '5100'],
    },
    {
      key: 'gl_income',
      name: 'GL income',
      dataset: 'bank.income_monthly',
      method: 'mapped',
      dimension: 'account',
      timeField: 'reportingPeriod',
      bindings: [
        {
          id: 'b5',
          variable: 'INTEREST_INCOME',
          measure: 'totalValue',
          filters: [{ field: 'accountKey', op: '=', value: '4000' }],
        },
        {
          id: 'b6',
          variable: 'INTEREST_EXPENSE',
          measure: 'totalValue',
          filters: [{ field: 'accountKey', op: '=', value: '5000' }],
        },
      ],
      mappings: [
        { id: 'm10', rowKey: 'LOANS', memberKey: '4000', factor: 1 },
        { id: 'm11', rowKey: 'DEPOSITS', memberKey: '5000', factor: 1 },
      ],
      leftOut: ['1000', '1100', '1200', '1300', '2000', '2100', '3000', '3100', '4100', '5100'],
    },
    {
      key: 'loan_level',
      name: 'Loan level',
      dataset: 'mortgage.loan_daily',
      method: 'matched',
      rowKeyField: 'product',
      timeField: 'asOfDate',
      bindings: [
        { id: 'b7', variable: 'AVERAGE_BALANCE', measure: 'periodAverage', filters: [] },
        { id: 'b8', variable: 'LOAN_BALANCE', measure: 'periodAverage', filters: [] },
      ],
      mappings: [],
      leftOut: [],
    },
    {
      key: 'plan',
      name: 'Budget and forecast plans',
      dataset: 'bank.plan_monthly',
      method: 'matched',
      rowKeyField: 'line',
      timeField: 'planPeriod',
      bindings: [
        { id: 'b9', variable: 'AVERAGE_BALANCE', measure: 'plannedBalance', filters: [], missing: 'null' },
        { id: 'b10', variable: 'INTEREST_INCOME', measure: 'plannedIncome', filters: [], missing: 'null' },
        { id: 'b11', variable: 'INTEREST_EXPENSE', measure: 'plannedExpense', filters: [], missing: 'null' },
      ],
      mappings: [],
      leftOut: [],
    },
    {
      key: 'simulation',
      name: 'NII simulation',
      dataset: 'bank.nii_simulation',
      method: 'matched',
      rowKeyField: 'line',
      timeField: 'projectionPeriod',
      bindings: [
        { id: 'b12', variable: 'AVERAGE_BALANCE', measure: 'projectedBalance', filters: [], missing: 'null' },
        { id: 'b13', variable: 'LOAN_BALANCE', measure: 'projectedBalance', filters: [], missing: 'null' },
        { id: 'b14', variable: 'INTEREST_INCOME', measure: 'projectedIncome', filters: [], missing: 'null' },
        { id: 'b15', variable: 'INTEREST_EXPENSE', measure: 'projectedExpense', filters: [], missing: 'null' },
      ],
      mappings: [],
      leftOut: [],
    },
  ],
  profiles: [
    {
      key: 'gl_monthly',
      name: 'GL monthly',
      enabled: true,
      picks: {
        AVERAGE_BALANCE: 'gl_monthly',
        LOAN_BALANCE: 'gl_monthly',
        INTEREST_INCOME: 'gl_income',
        INTEREST_EXPENSE: 'gl_income',
      },
    },
    {
      key: 'gl_daily',
      name: 'GL daily',
      enabled: true,
      picks: {
        AVERAGE_BALANCE: 'gl_daily',
        LOAN_BALANCE: 'gl_daily',
        INTEREST_INCOME: 'gl_income',
        INTEREST_EXPENSE: 'gl_income',
      },
    },
    {
      key: 'loan_based',
      name: 'Loan based',
      enabled: true,
      picks: {
        AVERAGE_BALANCE: 'loan_level',
        LOAN_BALANCE: 'loan_level',
        INTEREST_INCOME: 'gl_income',
        INTEREST_EXPENSE: 'gl_income',
      },
    },
    {
      key: 'plan',
      name: 'Budgets and forecasts',
      enabled: true,
      picks: {
        AVERAGE_BALANCE: 'plan',
        LOAN_BALANCE: 'gl_monthly',
        INTEREST_INCOME: 'plan',
        INTEREST_EXPENSE: 'plan',
      },
    },
    {
      key: 'simulation',
      name: 'NII simulation',
      enabled: true,
      picks: {
        AVERAGE_BALANCE: 'simulation',
        LOAN_BALANCE: 'simulation',
        INTEREST_INCOME: 'simulation',
        INTEREST_EXPENSE: 'simulation',
      },
    },
  ],
  breakoutDims: [
    { key: 'officer', name: 'Loan officer', aliasKey: 'officer', role: 'officer', grouping: { kind: 'member' }, groupable: true, filterable: true, enabled: true },
    { key: 'branch', name: 'Branch', aliasKey: 'branch', role: 'branch', grouping: { kind: 'member' }, groupable: true, filterable: true, enabled: true },
    { key: 'region', name: 'Region', aliasKey: 'region', role: 'branch', grouping: { kind: 'level', level: 'region' }, groupable: true, filterable: true, enabled: true },
    { key: 'account', name: 'Account', aliasKey: 'account', role: 'account', grouping: { kind: 'member' }, groupable: true, filterable: true, enabled: true },
  ],
  rowScopes: [
    { id: 's1', variable: 'LOAN_BALANCE', rowKey: 'LOANS', scope: 'subtree' },
  ],
  reportScenarios: [
    { role: 'planVersion', name: 'Plan version' },
    { role: 'rateShock', name: 'Rate shock' },
  ],
  scenarios: [
    {
      key: 'ALL',
      label: 'All scenarios',
      children: [
        {
          key: 'BUDGETS',
          label: 'Budgets',
          children: [
            {
              key: 'FY24_BUDGETS',
              label: 'FY2024 budgets',
              children: [
                { key: 'BUD_FY24_V1', label: 'FY2024 budget, version 1', status: 'superseded', asOf: '2023-10-31', horizon: 'Jan–Dec 2024' },
                { key: 'BUD_FY24_V2', label: 'FY2024 budget, version 2', status: 'approved', asOf: '2023-12-15', horizon: 'Jan–Dec 2024' },
              ],
            },
          ],
        },
        {
          key: 'FORECASTS',
          label: 'Forecasts',
          children: [
            {
              key: 'FY24_FORECASTS',
              label: 'FY2024 forecasts',
              children: [
                { key: 'FC_2024_06', label: 'June 2024 forecast', status: 'superseded', asOf: '2024-06-30', horizon: 'Jan–Dec 2024' },
                { key: 'FC_2024_09', label: 'September 2024 forecast', status: 'approved', asOf: '2024-09-30', horizon: 'Oct–Dec 2024' },
              ],
            },
          ],
        },
      ],
    },
  ],
  styles: [
    { key: 'header', name: 'Header', defaultRole: 'header', fontWeight: 'bold', italic: false },
    { key: 'subtotal', name: 'Subtotal', defaultRole: 'subtotal', fontWeight: 'bold', borderTop: 'thin', italic: false },
    { key: 'total', name: 'Total', defaultRole: 'total', fontWeight: 'bold', borderTop: 'thin', borderBottom: 'double', italic: false },
    { key: 'detail', name: 'Detail', defaultRole: 'detail', fontWeight: 'normal', italic: false },
    { key: 'formula', name: 'Formula', defaultRole: 'formula', italic: true },
    { key: 'highlight', name: 'Highlight', defaultRole: null, textColor: '#9C5700', backgroundColor: '#FFEB9C', italic: false },
  ],
}

function variant(base: ReportState, patch: (draft: ReportState) => void): ReportState {
  const draft = structuredClone(base)
  patch(draft)
  return draft
}

type MappingSeed = [rowKey: string, memberKey: string, factor?: number]

/** Each report owns its own rows, so mapped sources are re-pointed at this report's row keys. */
function remapSources(d: ReportState, prefix: string, bySource: Record<string, MappingSeed[]>) {
  for (const source of d.sources) {
    if (source.method !== 'mapped') continue
    source.mappings = (bySource[source.key] ?? []).map(([rowKey, memberKey, factor], i) => ({
      id: `${prefix}-${source.key}-${i + 1}`,
      rowKey,
      memberKey,
      factor: factor ?? 1,
    }))
    source.leftOut = []
  }
}

function setMatchedLines(d: ReportState, lines: string[]) {
  for (const ds of d.datasets) if (ds.fieldValues?.line) ds.fieldValues = { ...ds.fieldValues, line: lines }
}

/** One profile only, so the profile switcher stays hidden. */
export const depositPricingReport = variant(initialReport, (d) => {
  d.settings.key = 'bank.deposit_pricing'
  d.settings.name = 'Deposit pricing'
  d.settings.type = 'deposit_pricing'
  d.profiles = d.profiles.filter((p) => p.key === 'gl_monthly')
  d.settings.defaultProfile = 'gl_monthly'
  d.rows = [
    row('DP_TOTAL', 'Total deposits', 'subtotal', [
      row('DP_RETAIL', 'Retail deposits', 'heading', [
        row('DP_CHECKING', 'Checking accounts', 'line'),
        row('DP_SAVINGS', 'Savings accounts', 'line'),
        row('DP_MONEY_MARKET', 'Money market accounts', 'line', [], { styleKey: 'highlight' }),
      ]),
      row('DP_TIME', 'Time deposits', 'heading', [
        row('DP_CD_SHORT', 'CDs under 12 months', 'line'),
        row('DP_CD_LONG', 'CDs 12 months and over', 'line', [], { parameters: { dayCount: 'actual_365' } }),
      ]),
      row('DP_WHOLESALE', 'Wholesale funding', 'heading', [row('DP_BROKERED', 'Brokered deposits', 'line')]),
    ], { parameters: { dayCount: 'actual_360' } }),
    row('cof', 'Cost of funds', 'formula', [], { measureKey: 'COST_OF_FUNDS', columnKey: 'yield' }),
  ]
  d.rowScopes = []
  remapSources(d, 'dp', {
    gl_monthly: [['DP_CHECKING', '2000'], ['DP_SAVINGS', '2000'], ['DP_BROKERED', '2100']],
    gl_daily: [['DP_CHECKING', '2000'], ['DP_BROKERED', '2100']],
    gl_income: [['DP_CHECKING', '5000'], ['DP_CD_SHORT', '5000']],
  })
  setMatchedLines(d, ['DP_CHECKING', 'DP_SAVINGS', 'DP_MONEY_MARKET', 'DP_CD_SHORT', 'DP_CD_LONG', 'DP_BROKERED'])
})

/** Two profiles, switched off: a draft that is not yet available to run. */
export const branchProfitabilityReport = variant(initialReport, (d) => {
  d.settings.key = 'bank.branch_profitability'
  d.settings.name = 'Branch profitability'
  d.settings.type = 'branch_profitability'
  d.settings.periodicity = 'Quarter'
  d.enabled = false
  d.profiles = d.profiles.filter((p) => p.key === 'gl_monthly' || p.key === 'loan_based')
  d.settings.defaultProfile = 'loan_based'
  d.settings.defaultBreakout = ['branch']
  // Lending keeps the MORTGAGE/AUTO/COMMERCIAL leaves so the loan-level matched source still lands.
  d.rows = [
    row('BP_CONTRIBUTION', 'Branch contribution', 'subtotal', [
      row('BP_REVENUE', 'Revenue', 'heading', [
        row('BP_LENDING', 'Lending income', 'line', [
          row('MORTGAGE', 'Mortgage loans', 'line'),
          row('AUTO', 'Auto loans', 'line'),
          row('COMMERCIAL', 'Commercial loans', 'line'),
        ], { styleKey: 'highlight' }),
        row('BP_DEPOSIT_SPREAD', 'Deposit spread', 'line'),
        row('BP_FEES', 'Fee income', 'line'),
      ]),
      row('BP_COSTS', 'Operating costs', 'heading', [
        row('BP_STAFF', 'Staff costs', 'line'),
        row('BP_OCCUPANCY', 'Occupancy', 'line'),
      ]),
    ], { parameters: { dayCount: 'actual_actual' } }),
    row('nim', 'Net interest margin', 'formula', [], { measureKey: 'NIM', columnKey: 'yield' }),
  ]
  d.rowScopes = [{ id: 's1', variable: 'LOAN_BALANCE', rowKey: 'BP_LENDING', scope: 'subtree' }]
  remapSources(d, 'bp', {
    gl_monthly: [['BP_LENDING', '1100'], ['BP_DEPOSIT_SPREAD', '2000'], ['BP_FEES', '4100'], ['BP_STAFF', '5100', -1], ['BP_OCCUPANCY', '1300', -1]],
    gl_daily: [['BP_LENDING', '1100'], ['BP_DEPOSIT_SPREAD', '2000']],
    gl_income: [['BP_LENDING', '4000'], ['BP_FEES', '4100'], ['BP_STAFF', '5100', -1]],
  })
  setMatchedLines(d, ['MORTGAGE', 'AUTO', 'COMMERCIAL', 'BP_DEPOSIT_SPREAD', 'BP_FEES', 'BP_STAFF', 'BP_OCCUPANCY'])
})

export const sampleReports: ReportState[] = [initialReport, depositPricingReport, branchProfitabilityReport]
