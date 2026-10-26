import type { AllocationMode, AssumptionPin, BindingUsage, Bridge, BridgeMapping, BridgeVersion, Cardinality, MappingEntry, MappingSet, MappingVersion, SourceSelection, VersionStatus } from '../contract'

export interface AssumptionSet {
  key: string
  name: string
  isActive: boolean
  /** Object id (mapping set or bridge) → pinned version id. */
  pins: Record<string, string>
}

export interface Db {
  mappingSets: MappingSet[]
  mappingVersions: MappingVersion[]
  entries: MappingEntry[]
  bridges: Bridge[]
  bridgeVersions: BridgeVersion[]
  bridgeMappings: BridgeMapping[]
  /** Object id → dataset bindings that name it. */
  bindings: Record<string, BindingUsage[]>
  assumptionSets: AssumptionSet[]
}

const dim = (key: string) => `dim-${key}`
const pad = (n: number, w = 3) => String(n).padStart(w, '0')
const br = (n: number) => `BR${pad(n, 2)}`

interface MsVersionSpec {
  n: number
  status: VersionStatus
  created: string
  published?: string
  retired?: string
  entries: [source: string, member: string][]
}

interface MsSpec {
  key: string
  displayName: string
  namespace: string
  dimension: string
  versions: MsVersionSpec[]
  bindings?: BindingUsage[]
}

type MappingSpec = [source: string, target: string, factor?: number, weight?: number, selection?: SourceSelection]

interface BrVersionSpec {
  n: number
  status: VersionStatus
  cardinality: Cardinality
  mode: AllocationMode
  created: string
  published?: string
  retired?: string
  mappings: MappingSpec[]
}

interface BrSpec {
  key: string
  displayName: string
  source: string
  target: string
  isEnabled?: boolean
  owner?: Bridge['owner']
  versions: BrVersionSpec[]
  bindings?: BindingUsage[]
}

function build(sets: MsSpec[], bridges: BrSpec[], baseline: Record<string, number>): Db {
  const db: Db = { mappingSets: [], mappingVersions: [], entries: [], bridges: [], bridgeVersions: [], bridgeMappings: [], bindings: {}, assumptionSets: [] }
  const active: AssumptionSet = { key: 'active', name: 'Active', isActive: true, pins: {} }
  const historical: AssumptionSet = { key: 'baseline_2024', name: '2024 baseline', isActive: false, pins: {} }

  for (const s of sets) {
    const id = `ms-${s.key}`
    db.mappingSets.push({ id, key: s.key, displayName: s.displayName, sourceNamespace: s.namespace, dimensionId: dim(s.dimension) })
    db.bindings[id] = s.bindings ?? []
    for (const v of s.versions) {
      const vid = `${id}-v${v.n}`
      db.mappingVersions.push({ id: vid, mappingSetId: id, versionNumber: v.n, status: v.status, createdAt: v.created, publishedAt: v.published ?? null, retiredAt: v.retired ?? null })
      v.entries.forEach(([sourceKeyValue, memberKey], i) => db.entries.push({ id: `${vid}-e${i + 1}`, versionId: vid, sourceKeyValue, memberKey }))
      if (v.status === 'published') active.pins[id] = vid
      if (baseline[s.key] === v.n) historical.pins[id] = vid
    }
  }

  for (const b of bridges) {
    const id = `bridge-${b.key}`
    db.bridges.push({ id, key: b.key, displayName: b.displayName, sourceDimensionId: dim(b.source), targetDimensionId: dim(b.target), isEnabled: b.isEnabled ?? true, owner: b.owner ?? null })
    db.bindings[id] = b.bindings ?? []
    for (const v of b.versions) {
      const vid = `${id}-v${v.n}`
      db.bridgeVersions.push({ id: vid, bridgeId: id, versionNumber: v.n, status: v.status, cardinality: v.cardinality, allocationMode: v.mode, createdAt: v.created, publishedAt: v.published ?? null, retiredAt: v.retired ?? null })
      v.mappings.forEach(([sourceMemberKey, targetMemberKey, factor, weight, selection], i) =>
        db.bridgeMappings.push({ id: `${vid}-m${i + 1}`, versionId: vid, sourceMemberKey, targetMemberKey, factor: factor ?? 1, allocationWeight: weight ?? 1, sourceSelection: selection ?? 'member' }),
      )
      if (v.status === 'published') active.pins[id] = vid
      if (baseline[b.key] === v.n) historical.pins[id] = vid
    }
  }

  db.assumptionSets = [active, historical]
  return db
}

/* ───────────────────────── mapping sets ───────────────────────── */

function loanBranchEntries(upTo: number): [string, string][] {
  const out: [string, string][] = []
  for (let i = 1; i <= upTo; i++) out.push([pad(i), br(i)])
  for (let i = 1; i <= upTo; i++) out.push([`C${pad(i, 2)}`, br(i)])
  return out
}

const LOANS_V3 = (fieldKey: string, roleKey: string): BindingUsage => ({ datasetKey: 'loans.details', datasetVersion: 3, fieldKey, roleKey, status: 'published' })

const PRODUCT_V1: [string, string][] = [
  ['MTG', 'MORTGAGE'],
  ['AUT', 'AUTO'],
  ['CML', 'COMMERCIAL'],
  ['DDA', 'DEPOSITS'],
  ['SAV', 'DEPOSITS'],
]

const MAPPING_SETS: MsSpec[] = [
  {
    key: 'loan.branch',
    displayName: 'Loan servicing branches',
    namespace: 'loan_servicing',
    dimension: 'Branch',
    versions: [
      { n: 1, status: 'retired', created: '2023-01-20', published: '2023-01-23', retired: '2024-07-02', entries: loanBranchEntries(15) },
      {
        n: 2,
        status: 'published',
        created: '2024-06-28',
        published: '2024-07-02',
        entries: [...loanBranchEntries(18), ['0100', 'BR01'], [' 007', 'BR07'], ['999', 'BR21']],
      },
    ],
    bindings: [LOANS_V3('servicing_branch_code', 'servicingBranch'), LOANS_V3('origination_branch_code', 'originationBranch')],
  },
  {
    key: 'finance.branch',
    displayName: 'GL branch codes',
    namespace: 'gl',
    dimension: 'Branch',
    versions: [
      {
        n: 1,
        status: 'published',
        created: '2024-01-08',
        published: '2024-01-10',
        entries: [['001', 'BR02'], ['002', 'BR01'], ...Array.from({ length: 8 }, (_, i): [string, string] => [pad(i + 3), br(i + 3)])],
      },
    ],
    bindings: [{ datasetKey: 'gl.balances', datasetVersion: 5, fieldKey: 'cost_centre_branch', roleKey: 'branch', status: 'published' }],
  },
  {
    key: 'loan.product',
    displayName: 'Loan product codes',
    namespace: 'loan_servicing',
    dimension: 'Product',
    versions: [
      { n: 1, status: 'published', created: '2024-02-01', published: '2024-02-02', entries: PRODUCT_V1 },
      { n: 2, status: 'draft', created: '2026-10-19', entries: [...PRODUCT_V1, ['MTG', 'AUTO'], ['HELOC', 'RETAIL_LENDING']] },
    ],
    bindings: [LOANS_V3('product_code', 'product')],
  },
  {
    key: 'collateral.codes',
    displayName: 'Collateral codes',
    namespace: 'collateral_mgmt',
    dimension: 'Collateral',
    versions: [
      {
        n: 1,
        status: 'draft',
        created: '2026-10-21',
        entries: [
          ['RE', 'REAL_ESTATE'],
          ['VH', 'VEHICLE'],
          ['CS', 'CASH'],
          ['SC', 'SECURITIES'],
          ['UN', 'UNSECURED'],
        ],
      },
    ],
  },
  {
    key: 'legacy.riskgrade',
    displayName: 'Legacy risk grades',
    namespace: 'legacy_core',
    dimension: 'RiskClass',
    versions: [
      {
        n: 1,
        status: 'draft',
        created: '2026-09-30',
        entries: [
          ['A', 'RC_LOW'],
          ['B', 'RC_MEDIUM'],
          ['C', 'RC_HIGH'],
        ],
      },
    ],
  },
]

/* ───────────────────────── bridges ───────────────────────── */

const ACCOUNT_GROUPS: MappingSpec[] = [
  ['CASH_1010', 'AG_CASH'],
  ['CASH_1020', 'AG_CASH'],
  ['LOAN_1100', 'AG_LOANS'],
  ['LOAN_1110', 'AG_LOANS'],
  ['LOAN_1120', 'AG_LOANS'],
  ['SEC_1200', 'AG_SECURITIES'],
  ['PREM_1300', 'AG_FIXED'],
  ['DEP_2000', 'AG_DEPOSITS', -1],
  ['DEP_2010', 'AG_DEPOSITS', -1],
  ['DEP_2020', 'AG_DEPOSITS', -1],
  ['BOR_2100', 'AG_BORROWINGS', -1],
  ['CAP_3000', 'AG_EQUITY', -1],
  ['RE_3100', 'AG_EQUITY', -1],
  ['INT_4000', 'AG_INT_INCOME', -1],
  ['INT_4010', 'AG_INT_INCOME', -1],
  ['FEE_4100', 'AG_FEES', -1],
  ['INT_5000', 'AG_INT_EXPENSE'],
  ['INT_5010', 'AG_INT_EXPENSE'],
  ['OPX_5100', 'AG_OPEX'],
  ['OPX_5200', 'AG_OPEX'],
]

const BRIDGES: BrSpec[] = [
  {
    key: 'gl_account_to_account_group',
    displayName: 'GL account → account group',
    source: 'Account',
    target: 'AccountGroup',
    versions: [{ n: 1, status: 'published', cardinality: 'many_to_one', mode: 'none', created: '2025-06-10', published: '2025-06-11', mappings: ACCOUNT_GROUPS }],
    bindings: [{ datasetKey: 'bank.balance_monthly', datasetVersion: 2, fieldKey: 'account', roleKey: 'accountGroup', sourceRoleKey: 'account', status: 'published' }],
  },
  {
    key: 'cost_center_to_department',
    displayName: 'Cost center → department',
    source: 'CostCenter',
    target: 'Department',
    versions: [
      {
        n: 1,
        status: 'published',
        cardinality: 'many_to_many',
        mode: 'partition',
        created: '2025-03-05',
        published: '2025-03-06',
        mappings: [
          ['CC_100', 'DEPT_RETAIL', 1, 0.6],
          ['CC_100', 'DEPT_COMMERCIAL', 1, 0.3],
          ['CC_200', 'DEPT_RETAIL', 1, 0.5],
          ['CC_200', 'DEPT_COMMERCIAL', 1, 0.4],
          ['CC_200', 'DEPT_TREASURY', 1, 0.2],
          ['CC_300', 'DEPT_RETAIL', 1, 0.7],
          ['CC_300', 'DEPT_COMMERCIAL', 1, 0.3],
          ['CC_400', 'DEPT_CORPORATE', 1, 1],
        ],
      },
      {
        n: 2,
        status: 'draft',
        cardinality: 'many_to_many',
        mode: 'partition',
        created: '2026-10-22',
        mappings: [
          ['CC_100', 'DEPT_RETAIL', 1, 0.7],
          ['CC_100', 'DEPT_COMMERCIAL', 1, 0.3],
          ['CC_200', 'DEPT_RETAIL', 1, 0.5],
          ['CC_200', 'DEPT_COMMERCIAL', 1, 0.3],
          ['CC_200', 'DEPT_TREASURY', 1, 0.2],
          ['CC_300', 'DEPT_RETAIL', 1, 0.7],
          ['CC_300', 'DEPT_COMMERCIAL', 1, 0.3],
          ['CC_400', 'DEPT_CORPORATE', 1, 1],
        ],
      },
    ],
    bindings: [{ datasetKey: 'opex.monthly', datasetVersion: 1, fieldKey: 'cost_center', roleKey: 'department', sourceRoleKey: 'costCenter', status: 'published' }],
  },
  {
    key: 'product_to_risk_class',
    displayName: 'Product → risk class',
    source: 'Product',
    target: 'RiskClass',
    versions: [
      {
        n: 1,
        status: 'draft',
        cardinality: 'many_to_one',
        mode: 'none',
        created: '2026-10-12',
        mappings: [
          ['MORTGAGE', 'RC_LOW'],
          ['AUTO', 'RC_MEDIUM'],
          ['AUTO', 'RC_HIGH'],
          ['COMMERCIAL', 'RC_HIGH'],
          ['DEPOSITS', 'RC_LOW'],
        ],
      },
    ],
  },
  {
    key: 'margin.gl_monthly',
    displayName: 'Bank margin · GL monthly balances',
    source: 'Account',
    target: 'MarginStatement',
    owner: { reportId: 'bank.margin_statement', reportKey: 'bank.margin_statement', sourceKey: 'gl_monthly' },
    versions: [
      {
        n: 1,
        status: 'published',
        cardinality: 'many_to_many',
        mode: 'replicate',
        created: '2025-02-03',
        published: '2025-02-03',
        mappings: [
          ['CASH', 'CASH', 1, 1, 'descendants'],
          ['LOAN_1100', 'MORTGAGE', 1, 1],
          ['LOAN_1110', 'AUTO', 1, 1],
          ['LOAN_1120', 'COMMERCIAL', 1, 1],
          ['DEPOSITS', 'DEPOSITS', -1, 1, 'descendants'],
          ['EQUITY', 'CAPITAL', -1, 1, 'descendants'],
        ],
      },
    ],
  },
  {
    key: 'deposit_pricing.gl_monthly',
    displayName: 'Deposit pricing · GL monthly balances',
    source: 'Account',
    target: 'DepositPricingStatement',
    owner: { reportId: 'bank.deposit_pricing', reportKey: 'bank.deposit_pricing', sourceKey: 'gl_monthly' },
    versions: [
      {
        n: 1,
        status: 'published',
        cardinality: 'many_to_many',
        mode: 'replicate',
        created: '2025-03-11',
        published: '2025-03-11',
        mappings: [
          ['DEP_2000', 'DP_CHECKING', -1, 1],
          ['DEP_2010', 'DP_SAVINGS', -1, 1],
          ['DEP_2020', 'DP_MONEY_MARKET', -1, 1],
          ['BOR_2100', 'DP_BROKERED', -1, 1],
        ],
      },
    ],
  },
  {
    key: 'branch_profitability.gl_monthly',
    displayName: 'Branch profitability · GL monthly balances',
    source: 'Account',
    target: 'BranchProfitabilityStatement',
    owner: { reportId: 'bank.branch_profitability', reportKey: 'bank.branch_profitability', sourceKey: 'gl_monthly' },
    versions: [
      {
        n: 1,
        status: 'draft',
        cardinality: 'many_to_one',
        mode: 'none',
        created: '2026-09-18',
        mappings: [
          ['LOAN_1100', 'MORTGAGE'],
          ['LOAN_1110', 'AUTO'],
          ['LOAN_1120', 'COMMERCIAL'],
          ['DEP_2000', 'BP_DEPOSIT_SPREAD', -1],
          ['FEE_4100', 'BP_FEES', -1],
          ['OPX_5100', 'BP_STAFF'],
          ['PREM_1300', 'BP_OCCUPANCY'],
        ],
      },
    ],
  },
  {
    key: 'branch_to_territory',
    displayName: 'Branch → sales territory',
    source: 'Branch',
    target: 'Territory',
    isEnabled: false,
    versions: [
      {
        n: 1,
        status: 'published',
        cardinality: 'many_to_one',
        mode: 'none',
        created: '2025-04-04',
        published: '2025-04-05',
        mappings: [
          ['BR01', 'T_BR01'],
          ['BR02', 'T_BR02'],
          ['BR05', 'T_BR05'],
          ['BR08', 'T_BR08'],
          ['BR09', 'T_BR09'],
        ],
      },
    ],
  },
]

export function createSeed(): Db {
  return build(MAPPING_SETS, BRIDGES, { 'loan.branch': 1, gl_account_to_account_group: 1 })
}

export type { AssumptionPin }
