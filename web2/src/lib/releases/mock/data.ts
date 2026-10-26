import type {
  AssumptionSet,
  Binding,
  Dataset,
  DatasetVersion,
  Identity,
  IdentityVersion,
  Pin,
  PinKind,
  Release,
  ReleaseDataset,
  ReportRef,
  TenantInfo,
  VersionStatus,
} from '../contract'

export interface Db {
  tenant: TenantInfo
  datasets: Dataset[]
  versions: DatasetVersion[]
  releases: Release[]
  manifests: ReleaseDataset[]
  identities: Identity[]
  sets: AssumptionSet[]
  /** Historical sets only; the active set always pins each identity's published version. */
  pins: Pin[]
  reports: ReportRef[]
}

type V = [n: number, status: VersionStatus, publishedAt: string | null]

const dimId = (key: string) => `dim-${key}`
const msId = (key: string) => `ms-${key}`
const brId = (key: string) => `bridge-${key}`
const dsId = (key: string) => `ds-${key}`
const dsv = (key: string, n: number) => `${dsId(key)}-v${n}`

function identity(kind: PinKind, id: string, key: string, displayName: string, dimensionId: string, versions: V[], opts: { reportId?: string; createdAt?: string } = {}): Identity {
  return {
    kind,
    id,
    key,
    displayName,
    dimensionId,
    reportId: opts.reportId ?? null,
    createdAt: opts.createdAt ?? '2023-06-01T09:00:00Z',
    versions: versions.map(([n, status, publishedAt]): IdentityVersion => ({ id: `${id}-v${n}`, versionNumber: n, status, publishedAt })),
  }
}

const P = (n: number, at: string): V => [n, 'published', at]
const R = (n: number, at: string): V => [n, 'retired', at]

const DIMENSIONS: [key: string, name: string, versions: V[], createdAt?: string][] = [
  ['Account', 'Account', [R(1, '2024-01-10T09:00:00Z'), P(2, '2026-01-15T09:00:00Z')]],
  ['AccountGroup', 'Account group', [P(1, '2024-02-01T09:00:00Z')]],
  ['BalanceBand', 'Balance band', [P(1, '2024-02-12T09:00:00Z')]],
  ['Branch', 'Branch', [R(1, '2023-07-01T09:00:00Z'), R(2, '2025-03-01T09:00:00Z'), R(3, '2026-02-01T09:00:00Z'), P(4, '2026-08-15T09:00:00Z')]],
  ['Collateral', 'Collateral', [P(1, '2024-04-03T09:00:00Z')]],
  ['CostCenter', 'Cost center', [P(1, '2024-01-20T09:00:00Z')]],
  ['Department', 'Department', [P(1, '2024-01-20T09:00:00Z')]],
  ['FiscalCalendar', 'Fiscal calendar', [P(1, '2023-06-01T09:00:00Z')]],
  ['GlAccount', 'GL account', [R(1, '2023-09-01T09:00:00Z'), P(2, '2025-11-01T09:00:00Z')]],
  ['GlAccountClass', 'GL account class', [P(1, '2023-09-01T09:00:00Z')]],
  ['MaturityBucket', 'Maturity bucket', [P(1, '2024-05-10T09:00:00Z')]],
  ['Officer', 'Loan officer', [P(1, '2024-06-01T09:00:00Z'), [2, 'draft', null]]],
  ['Product', 'Product', [R(1, '2024-03-01T09:00:00Z'), P(2, '2026-03-01T09:00:00Z')]],
  ['Purpose', 'Loan purpose', [P(1, '2024-03-01T09:00:00Z')]],
  ['RiskClass', 'Risk class', [P(1, '2024-03-15T09:00:00Z')]],
  ['RiskGrade', 'Risk grade', [P(1, '2024-03-15T09:00:00Z')]],
  ['Scenario', 'Scenario', [P(1, '2024-07-01T09:00:00Z')]],
  ['SystemAccountClass', 'System account class', [P(1, '2023-06-01T09:00:00Z')]],
  ['Territory', 'Territory', [P(1, '2024-01-05T09:00:00Z')]],
  ['PlanVersion', 'Plan version', [P(1, '2025-11-20T09:00:00Z')], '2025-11-18T09:00:00Z'],
  ['RateShock', 'Rate shock', [P(1, '2026-08-05T09:00:00Z')], '2026-08-02T09:00:00Z'],
  ['EvolvingBranch', 'Evolving branch', [R(1, '2025-06-01T09:00:00Z'), P(2, '2026-01-20T09:00:00Z')]],
]

const REPORT_DIMENSIONS: [key: string, name: string, report: string][] = [
  ['MarginStatement', 'Margin statement rows', 'bank.margin_statement'],
  ['DepositPricingStatement', 'Deposit pricing rows', 'bank.deposit_pricing'],
  ['BranchProfitabilityStatement', 'Branch profitability rows', 'bank.branch_profitability'],
]

const MAPPING_SETS: [key: string, name: string, dimension: string, versions: V[], createdAt?: string][] = [
  ['loan.branch', 'Loan system branches', 'Branch', [P(1, '2025-09-01T09:00:00Z')]],
  ['loan.product', 'Loan system products', 'Product', [R(1, '2024-03-01T09:00:00Z'), P(2, '2026-03-01T09:00:00Z')]],
  ['collateral.codes', 'Collateral codes', 'Collateral', [P(1, '2024-04-03T09:00:00Z')]],
  ['finance.branch', 'Finance branches', 'Branch', [P(1, '2024-01-05T09:00:00Z')]],
  ['evolving.branch', 'Evolving branch codes', 'EvolvingBranch', [R(1, '2025-06-01T09:00:00Z'), P(2, '2026-01-20T09:00:00Z')]],
  ['legacy.costcenter', 'Legacy cost centers', 'CostCenter', [[1, 'draft', null]], '2026-09-01T09:00:00Z'],
]

const BRIDGES: [key: string, name: string, target: string, versions: V[], report?: string][] = [
  ['branch_to_territory', 'Branch → territory', 'Territory', [R(1, '2024-01-05T09:00:00Z'), P(2, '2025-10-01T09:00:00Z')]],
  ['gl_account_to_account_group', 'GL account → account group', 'AccountGroup', [P(1, '2024-02-01T09:00:00Z')]],
  ['margin.gl_monthly', 'Margin · GL monthly', 'MarginStatement', [P(1, '2025-12-01T09:00:00Z')], 'bank.margin_statement'],
  ['margin.balance_monthly', 'Margin · balances', 'MarginStatement', [P(1, '2025-12-01T09:00:00Z')], 'bank.margin_statement'],
  ['margin.income_monthly', 'Margin · income', 'MarginStatement', [P(1, '2025-12-01T09:00:00Z')], 'bank.margin_statement'],
  ['deposit_pricing.gl_monthly', 'Deposit pricing · GL monthly', 'DepositPricingStatement', [P(1, '2025-10-01T09:00:00Z')], 'bank.deposit_pricing'],
  ['deposit_pricing.balance_monthly', 'Deposit pricing · balances', 'DepositPricingStatement', [P(1, '2025-10-01T09:00:00Z')], 'bank.deposit_pricing'],
  ['branch_profitability.gl_monthly', 'Branch profitability · GL monthly', 'BranchProfitabilityStatement', [P(1, '2025-10-01T09:00:00Z')], 'bank.branch_profitability'],
  ['branch_profitability.income_monthly', 'Branch profitability · income', 'BranchProfitabilityStatement', [P(1, '2025-10-01T09:00:00Z')], 'bank.branch_profitability'],
]

export const reportIdOf = (key: string) => `rep-${key}`
export const reportKeyOf = (id: string) => id.replace(/^rep-/, '')

const b = (name: string, kind: Binding['kind'], dimension: string, via?: { ms?: string; bridge?: string }): Binding => ({
  name,
  kind,
  dimensionId: dimId(dimension),
  ...(via?.ms ? { mappingSetId: msId(via.ms) } : {}),
  ...(via?.bridge ? { bridgeId: brId(via.bridge) } : {}),
})
const m = (key: string, appliesBridgeFactors = true) => ({ key, appliesBridgeFactors })

const DATASETS: { key: string; name: string; enabled?: boolean; versions: { n: number; status: VersionStatus; at: string | null; bindings: Binding[]; measures: DatasetVersion['measures'] }[] }[] = [
  {
    key: 'bank.balance_monthly',
    name: 'Monthly balances',
    versions: [
      { n: 1, status: 'retired', at: '2025-02-01T09:00:00Z', bindings: [b('account', 'direct', 'Account'), b('branch', 'direct', 'Branch'), b('period', 'time', 'FiscalCalendar')], measures: [m('averageBalance')] },
      { n: 2, status: 'published', at: '2026-02-10T09:00:00Z', bindings: [b('account', 'direct', 'Account'), b('branch', 'direct', 'Branch'), b('balanceBand', 'range', 'BalanceBand'), b('period', 'time', 'FiscalCalendar')], measures: [m('averageBalance'), m('endingBalance')] },
      { n: 3, status: 'published', at: '2026-09-18T09:00:00Z', bindings: [b('account', 'direct', 'Account'), b('branch', 'direct', 'Branch'), b('balanceBand', 'range', 'BalanceBand'), b('maturity', 'range', 'MaturityBucket'), b('period', 'time', 'FiscalCalendar')], measures: [m('averageBalance'), m('endingBalance')] },
    ],
  },
  {
    key: 'bank.income_monthly',
    name: 'Monthly income',
    versions: [{ n: 1, status: 'published', at: '2025-02-01T09:00:00Z', bindings: [b('account', 'direct', 'Account'), b('branch', 'direct', 'Branch'), b('period', 'time', 'FiscalCalendar')], measures: [m('interestIncome', false), m('interestExpense')] }],
  },
  {
    key: 'loans.details',
    name: 'Loan details',
    versions: [
      { n: 2, status: 'retired', at: '2025-04-01T09:00:00Z', bindings: [b('servicingBranch', 'direct', 'Branch'), b('product', 'mapped', 'Product', { ms: 'loan.product' })], measures: [m('principal')] },
      { n: 3, status: 'published', at: '2026-03-05T09:00:00Z', bindings: [b('servicingBranch', 'direct', 'Branch'), b('product', 'mapped', 'Product', { ms: 'loan.product' }), b('riskGrade', 'direct', 'RiskGrade')], measures: [m('principal'), m('accruedInterest')] },
      { n: 4, status: 'draft', at: null, bindings: [b('servicingBranch', 'mapped', 'Branch', { ms: 'loan.branch' }), b('product', 'mapped', 'Product', { ms: 'loan.product' }), b('riskGrade', 'direct', 'RiskGrade')], measures: [m('principal'), m('accruedInterest')] },
    ],
  },
  {
    key: 'bank.plan_monthly',
    name: 'Monthly plan',
    versions: [{ n: 1, status: 'published', at: '2025-12-01T09:00:00Z', bindings: [b('planVersion', 'direct', 'PlanVersion'), b('account', 'direct', 'Account'), b('period', 'time', 'FiscalCalendar')], measures: [m('planAmount')] }],
  },
  {
    key: 'bank.nii_simulation_monthly',
    name: 'NII simulation',
    versions: [{ n: 1, status: 'published', at: '2026-06-20T09:00:00Z', bindings: [b('scenario', 'direct', 'Scenario'), b('account', 'direct', 'Account'), b('period', 'time', 'FiscalCalendar')], measures: [m('niiDelta')] }],
  },
  {
    key: 'ledger.balance_sheet',
    name: 'Ledger balance sheet',
    versions: [
      { n: 1, status: 'published', at: '2025-01-15T09:00:00Z', bindings: [b('glAccount', 'bridge', 'AccountGroup', { bridge: 'gl_account_to_account_group' }), b('period', 'time', 'FiscalCalendar')], measures: [m('balance')] },
      { n: 2, status: 'published', at: '2026-09-02T09:00:00Z', bindings: [b('glAccount', 'bridge', 'AccountGroup', { bridge: 'gl_account_to_account_group' }), b('costCenter', 'mapped', 'CostCenter', { ms: 'legacy.costcenter' }), b('period', 'time', 'FiscalCalendar')], measures: [m('balance')] },
    ],
  },
  {
    key: 'mortgage.loan_daily',
    name: 'Daily mortgages',
    versions: [{ n: 1, status: 'published', at: '2025-05-01T09:00:00Z', bindings: [b('collateral', 'mapped', 'Collateral', { ms: 'collateral.codes' }), b('branch', 'direct', 'Branch'), b('asOf', 'time', 'FiscalCalendar')], measures: [m('outstanding')] }],
  },
  {
    key: 'bank.ledger_monthly',
    name: 'Monthly ledger',
    versions: [
      { n: 1, status: 'published', at: '2025-03-01T09:00:00Z', bindings: [b('glAccount', 'direct', 'GlAccount'), b('territory', 'bridge', 'Territory', { bridge: 'branch_to_territory' }), b('period', 'time', 'FiscalCalendar')], measures: [m('amount')] },
      { n: 2, status: 'validated', at: null, bindings: [b('glAccount', 'direct', 'GlAccount'), b('territory', 'bridge', 'Territory', { bridge: 'branch_to_territory' }), b('department', 'direct', 'Department'), b('period', 'time', 'FiscalCalendar')], measures: [m('amount')] },
    ],
  },
]

const Q3: [string, number][] = [
  ['bank.balance_monthly', 2],
  ['bank.income_monthly', 1],
  ['loans.details', 3],
  ['bank.plan_monthly', 1],
  ['bank.nii_simulation_monthly', 1],
  ['ledger.balance_sheet', 1],
  ['mortgage.loan_daily', 1],
  ['bank.ledger_monthly', 1],
]
const replace = (base: [string, number][], changes: Record<string, number | null>) =>
  base.flatMap(([k, n]): [string, number][] => (k in changes ? (changes[k] === null ? [] : [[k, changes[k] as number]]) : [[k, n]]))

const RELEASES: { key: string; status: VersionStatus; created: string; published: string | null; manifest: [string, number][] }[] = [
  {
    key: 'prod/2025-q4',
    status: 'retired',
    created: '2025-09-20T10:00:00Z',
    published: '2025-10-01T08:00:00Z',
    manifest: [['bank.balance_monthly', 1], ['bank.income_monthly', 1], ['loans.details', 2], ['ledger.balance_sheet', 1], ['bank.ledger_monthly', 1]],
  },
  { key: 'prod/2026-q2', status: 'published', created: '2026-03-20T10:00:00Z', published: '2026-04-01T08:00:00Z', manifest: replace(Q3, { 'bank.nii_simulation_monthly': null }) },
  { key: 'prod/2026-q3', status: 'published', created: '2026-06-15T10:00:00Z', published: '2026-07-01T08:00:00Z', manifest: Q3 },
  { key: 'prod/2026-q4', status: 'validated', created: '2026-09-28T10:00:00Z', published: null, manifest: replace(Q3, { 'bank.balance_monthly': 3 }) },
  {
    key: 'prod/2027-q1',
    status: 'draft',
    created: '2026-10-12T10:00:00Z',
    published: null,
    manifest: replace(Q3, { 'loans.details': 4, 'ledger.balance_sheet': 2, 'bank.nii_simulation_monthly': null }),
  },
  { key: 'scratch/empty', status: 'draft', created: '2026-10-20T10:00:00Z', published: null, manifest: [] },
]

export const releaseIdOf = (key: string) => `rel-${key.replace(/[/.]/g, '-')}`

const REPORTS: ReportRef[] = [
  {
    id: reportIdOf('bank.margin_statement'),
    key: 'bank.margin_statement',
    displayName: 'Bank margin',
    isEnabled: true,
    sources: [
      { key: 'balance', datasetId: dsId('bank.balance_monthly'), usesFactors: false, measureKeys: ['averageBalance'], variable: 'AVERAGE_BALANCE' },
      { key: 'income', datasetId: dsId('bank.income_monthly'), usesFactors: false, measureKeys: ['interestIncome', 'interestExpense'], variable: 'INTEREST_INCOME' },
      { key: 'plan', datasetId: dsId('bank.plan_monthly'), usesFactors: false, measureKeys: ['planAmount'] },
      { key: 'simulation', datasetId: dsId('bank.nii_simulation_monthly'), usesFactors: false, measureKeys: ['niiDelta'] },
    ],
    reads: [
      { kind: 'dimension', identityId: dimId('Account') },
      { kind: 'dimension', identityId: dimId('Branch') },
      { kind: 'dimension', identityId: dimId('PlanVersion') },
      { kind: 'dimension', identityId: dimId('RateShock') },
      { kind: 'mapping_set', identityId: msId('loan.branch') },
    ],
  },
  {
    id: reportIdOf('consolidated.balance_sheet'),
    key: 'consolidated.balance_sheet',
    displayName: 'Consolidated balance sheet',
    isEnabled: true,
    sources: [
      { key: 'ledger', datasetId: dsId('ledger.balance_sheet'), usesFactors: true, measureKeys: ['balance'], variable: 'BALANCE' },
      { key: 'loans', datasetId: dsId('loans.details'), usesFactors: false, measureKeys: ['principal'] },
    ],
    reads: [
      { kind: 'dimension', identityId: dimId('GlAccount') },
      { kind: 'dimension', identityId: dimId('Branch') },
    ],
  },
  {
    id: reportIdOf('bank.key_ratios'),
    key: 'bank.key_ratios',
    displayName: 'Key ratios',
    isEnabled: true,
    sources: [
      { key: 'balance', datasetId: dsId('bank.balance_monthly'), usesFactors: false, measureKeys: ['averageBalance', 'endingBalance'] },
      { key: 'income', datasetId: dsId('bank.income_monthly'), usesFactors: false, measureKeys: ['interestIncome'] },
    ],
    reads: [{ kind: 'dimension', identityId: dimId('Account') }],
  },
]

/** The version an identity had published at a moment: the newest one published by then. */
function publishedAsOf(i: Identity, at: string) {
  return [...i.versions].filter((v) => v.publishedAt && v.publishedAt <= at).sort((a, c) => c.versionNumber - a.versionNumber)[0] ?? null
}

export function createSeed(): Db {
  const identities: Identity[] = [
    ...DIMENSIONS.map(([key, name, versions, createdAt]) => identity('dimension', dimId(key), key, name, dimId(key), versions, { createdAt })),
    ...REPORT_DIMENSIONS.map(([key, name, report]) => identity('dimension', dimId(key), key, name, dimId(key), [P(1, '2025-10-01T09:00:00Z')], { reportId: reportIdOf(report) })),
    ...MAPPING_SETS.map(([key, name, dim, versions, createdAt]) => identity('mapping_set', msId(key), key, name, dimId(dim), versions, { createdAt })),
    ...BRIDGES.map(([key, name, target, versions, report]) => identity('bridge', brId(key), key, name, dimId(target), versions, { reportId: report ? reportIdOf(report) : undefined })),
  ]

  const datasets: Dataset[] = DATASETS.map((d) => ({ id: dsId(d.key), key: d.key, displayName: d.name, isEnabled: d.enabled ?? true }))
  const versions: DatasetVersion[] = DATASETS.flatMap((d) =>
    d.versions.map((v) => ({ id: dsv(d.key, v.n), datasetId: dsId(d.key), versionNumber: v.n, status: v.status, publishedAt: v.at, bindings: v.bindings, measures: v.measures })),
  )

  const releases: Release[] = RELEASES.map((r) => ({ id: releaseIdOf(r.key), key: r.key, status: r.status, createdAt: r.created, publishedAt: r.published }))
  const manifests: ReleaseDataset[] = RELEASES.flatMap((r) => r.manifest.map(([k, n]) => ({ releaseId: releaseIdOf(r.key), datasetId: dsId(k), datasetVersionId: dsv(k, n) })))

  const sets: AssumptionSet[] = [
    { id: 'set-active', key: 'active', displayName: 'Active', description: 'Pins every published version and reads the active release.', status: 'active', releaseId: null, capturedAt: null, createdAt: '2023-06-01T09:00:00Z' },
    {
      id: 'set-fy2025_close',
      key: 'fy2025_close',
      displayName: 'FY2025 close',
      description: 'Year-end 2025 close, as reported to the board.',
      status: 'historical',
      releaseId: releaseIdOf('prod/2025-q4'),
      capturedAt: '2025-12-31T23:59:00Z',
      createdAt: '2025-12-31T23:59:00Z',
    },
    {
      id: 'set-fy2026_q2_close',
      key: 'fy2026_q2_close',
      displayName: 'FY2026 Q2 close',
      description: 'Second-quarter close. Used for as-reported margin comparisons.',
      status: 'historical',
      releaseId: releaseIdOf('prod/2026-q2'),
      capturedAt: '2026-06-30T18:00:00Z',
      createdAt: '2026-06-30T18:00:00Z',
    },
  ]

  const pins: Pin[] = sets
    .filter((s) => s.status === 'historical')
    .flatMap((s) =>
      identities
        .filter((i) => !i.reportId && i.createdAt <= (s.capturedAt as string))
        .flatMap((i): Pin[] => {
          const v = publishedAsOf(i, s.capturedAt as string)
          return v ? [{ setId: s.id, kind: i.kind, identityId: i.id, versionId: v.id }] : []
        }),
    )

  return {
    tenant: { tenantKey: 'development', activeReleaseId: releaseIdOf('prod/2026-q3'), semanticRevision: 4182 },
    datasets,
    versions,
    releases,
    manifests,
    identities,
    sets,
    pins,
    reports: REPORTS,
  }
}

/** Jump targets for the dev menu. */
export const DEV_JUMPS = {
  releases: [
    { id: releaseIdOf('prod/2026-q3'), label: 'prod/2026-q3', hint: 'Active' },
    { id: releaseIdOf('prod/2026-q4'), label: 'prod/2026-q4', hint: 'Validated, ready to publish' },
    { id: releaseIdOf('prod/2027-q1'), label: 'prod/2027-q1', hint: 'Draft failing 3 checks' },
    { id: releaseIdOf('scratch/empty'), label: 'scratch/empty', hint: 'Empty, deletable' },
    { id: releaseIdOf('prod/2026-q2'), label: 'prod/2026-q2', hint: 'Rollback target' },
  ],
  sets: [
    { id: 'set-active', label: 'active', hint: 'The active set' },
    { id: 'set-fy2025_close', label: 'fy2025_close', hint: 'Frozen, retired release' },
    { id: 'set-fy2026_q2_close', label: 'fy2026_q2_close', hint: 'Live pins, one report not ready' },
  ],
}
