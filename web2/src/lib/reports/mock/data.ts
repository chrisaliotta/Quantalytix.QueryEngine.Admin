import { catalog, publishedMembers } from '@/lib/tenant/mock/data'
import type { Id, ReportDocument, ReportRow, ReportSource } from '../contract'

/** An item of a role's dimension that a bridge rule can name. */
export interface ItemNode {
  key: string
  children?: ItemNode[]
}

export interface Db {
  reports: ReportDocument[]
  /** The models mock's models, with the keys of their variables and calculated measures. */
  models: { id: Id; key: string; metricKeys: string[] }[]
  /** Members of each role's dimension, by role id. */
  items: Record<Id, ItemNode[]>
  /** Values of each dataset field, by dataset id then field key. */
  fieldValues: Record<Id, Record<string, string[]>>
}

export function flattenItems(items: ItemNode[]): ItemNode[] {
  return items.flatMap((i) => [i, ...flattenItems(i.children ?? [])])
}

/** The published members of each role's dimension in the tenant mock, as an item tree. */
function itemsByRole(): Record<Id, ItemNode[]> {
  const tree = (members: { key: string; parentKey: string | null }[], parentKey: string | null): ItemNode[] =>
    members.filter((m) => m.parentKey === parentKey).map((m) => ({ key: m.key, children: tree(members, m.key) }))
  return Object.fromEntries(catalog.roles.map((role) => [role.id, tree(publishedMembers[role.dimensionId] ?? [], null)]))
}

const row = (key: string, label: string, kind: ReportRow['kind'], parentKey: string | null, extra: Partial<ReportRow> = {}): ReportRow => ({
  key,
  label,
  kind,
  parentKey,
  isVisible: true,
  showsValues: true,
  calculatedMeasureKey: null,
  valueColumnKey: null,
  styleKey: null,
  parameters: {},
  ...extra,
})

const loansByBranch: ReportSource = {
  key: 'loans_by_branch',
  displayName: 'Loans by branch',
  datasetId: 'ds_loan_daily',
  timeFieldKey: 'as_of_date',
  method: 'bridge',
  roleId: 'role_servicing_branch',
  fieldKey: null,
  bridgeId: 'report-1_bridge_loans_by_branch',
  variables: [
    { variableKey: 'AVERAGE_BALANCE', measureKey: 'balance', filter: null, missingBehavior: null },
    { variableKey: 'INTEREST_INCOME', measureKey: 'interest_income', filter: null, missingBehavior: null },
    { variableKey: 'LOAN_BALANCE', measureKey: 'balance', filter: null, missingBehavior: null },
  ],
  rules: {
    LOANS: [
      { itemKey: 'BR01', selection: 'member', factor: 1 },
      { itemKey: 'BR02', selection: 'member', factor: 1 },
    ],
    DEPOSITS: [{ itemKey: 'BR03', selection: 'member', factor: 1 }],
  },
  leftOut: ['BR_UNK'],
}

const glDirect: ReportSource = {
  key: 'gl_direct',
  displayName: 'GL by product',
  datasetId: 'ds_gl_balance',
  timeFieldKey: 'posting_date',
  method: 'direct',
  roleId: null,
  fieldKey: 'product_code',
  bridgeId: null,
  variables: [{ variableKey: 'INTEREST_EXPENSE', measureKey: 'interest_expense', filter: null, missingBehavior: 'zero' }],
  rules: {},
  leftOut: [],
}

/** Bank margin, enabled, over the models mock's bank margin model, and an empty draft of branch profitability over the same model. */
export function createSeed(): Db {
  return {
    models: [
      {
        id: 'model-1',
        key: 'bank.margin_model',
        metricKeys: ['AVERAGE_BALANCE', 'INTEREST_INCOME', 'INTEREST_EXPENSE', 'LOAN_BALANCE', 'YIELD', 'COST_OF_FUNDS', 'NET_INTEREST_INCOME', 'NIM'],
      },
      {
        id: 'model-2',
        key: 'consolidated.balances',
        metricKeys: [
          'ENDING_BALANCE', 'OPENING_BALANCE', 'DEBITS', 'CREDITS', 'FX_ADJUSTMENT', 'ELIMINATIONS',
          'NET_MOVEMENT', 'CONSOLIDATED', 'CHANGE', 'ANNUAL_GROWTH', 'FX_SHARE',
        ],
      },
    ],
    items: itemsByRole(),
    fieldValues: { ds_gl_balance: { product_code: ['LOANS', 'DEPOSITS', 'FEES'] } },
    reports: [
      {
        id: 'report-1',
        key: 'bank.margin_statement',
        isEnabled: true,
        modelId: 'model-1',
        reportDimensionId: 'report-1_dimension',
        reportDimensionVersionId: 'report-1_dimension_v1',
        settings: {
          displayName: 'Bank margin',
          reportType: 'financial',
          subtotalPosition: 'above',
          defaultProfileKey: 'base',
          periodicity: 'month',
          layout: 'statement',
          defaultBreakout: ['region'],
          defaultFilter: null,
        },
        rows: [
          row('total', 'Net interest margin', 'group', null, { styleKey: 'total' }),
          row('LOANS', 'Loans', 'data', 'total'),
          row('DEPOSITS', 'Deposits', 'data', 'total'),
          row('NIM_LINE', 'Net interest margin %', 'formula', 'total', { calculatedMeasureKey: 'NIM', valueColumnKey: 'nim' }),
        ],
        sources: [glDirect, loansByBranch],
        profiles: [
          {
            key: 'base',
            displayName: 'Base',
            isEnabled: true,
            picks: { AVERAGE_BALANCE: 'loans_by_branch', INTEREST_INCOME: 'loans_by_branch', LOAN_BALANCE: 'loans_by_branch', INTEREST_EXPENSE: 'gl_direct' },
          },
        ],
        calculationDimensions: [
          { key: 'region', displayName: 'Region', roleId: 'role_region', grouping: { mode: 'level', levelKey: 'region' }, isGroupable: true, isFilterable: true, isEnabled: true },
        ],
        scenarios: [],
        variableScopes: [],
        columns: [
          { key: 'income', displayName: 'Interest income', valueKind: 'variable', metricKey: 'INTEREST_INCOME', displayFormat: null, isVisible: true },
          { key: 'expense', displayName: 'Interest expense', valueKind: 'variable', metricKey: 'INTEREST_EXPENSE', displayFormat: null, isVisible: true },
          { key: 'balance', displayName: null, valueKind: 'variable', metricKey: 'AVERAGE_BALANCE', displayFormat: null, isVisible: true },
          { key: 'nim', displayName: 'NIM', valueKind: 'calculated', metricKey: 'NIM', displayFormat: 'percent', isVisible: true },
        ],
        styles: [
          {
            key: 'total',
            displayName: 'Total',
            defaultRole: 'total',
            fontFamily: null,
            fontSize: null,
            fontWeight: 'bold',
            isItalic: false,
            textColor: null,
            backgroundColor: null,
            borderTop: 'thin',
            borderBottom: 'double',
            indent: null,
          },
        ],
      },
      {
        id: 'report-3',
        key: 'bank.branch_profitability',
        isEnabled: false,
        modelId: 'model-1',
        reportDimensionId: 'report-3_dimension',
        reportDimensionVersionId: 'report-3_dimension_v1',
        settings: {
          displayName: 'Branch profitability',
          reportType: 'financial',
          subtotalPosition: 'above',
          defaultProfileKey: null,
          periodicity: 'month',
          layout: 'statement',
          defaultBreakout: [],
          defaultFilter: null,
        },
        rows: [row('total', 'Branch profitability', 'group', null)],
        sources: [],
        profiles: [],
        calculationDimensions: [],
        scenarios: [],
        variableScopes: [],
        columns: [],
        styles: [],
      },
    ],
  }
}
