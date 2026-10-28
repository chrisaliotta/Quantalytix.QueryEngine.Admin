import type {
  Bridge,
  BridgeMapping,
  BridgeOwner,
  BridgeVersion,
  MappingAllocationMode,
  MappingBindingUsage,
  MappingCardinality,
  MappingEntry,
  MappingSet,
  SourceSelection,
  MappingVersion,
} from '../contract'

export interface AssumptionSet {
  id: string
  key: string
  displayName: string
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
  bindings: Record<string, MappingBindingUsage[]>
  assumptionSets: AssumptionSet[]
}

const dim = (key: string) => `dim-${key}`
const pad = (n: number, w = 3) => String(n).padStart(w, '0')
const br = (n: number) => `BR${pad(n, 2)}`
const at = (date: string) => `${date}T09:00:00Z`

interface Frozen {
  on: string
  note: string | null
}

/** Versions are listed oldest first: every one but the last is frozen, the last is live. */
interface MsVersionSpec {
  created: string
  frozen?: Frozen
  entries: [source: string, member: string][]
}

interface MsSpec {
  key: string
  displayName: string
  namespace: string
  dimension: string
  versions: MsVersionSpec[]
  bindings?: MappingBindingUsage[]
}

type MappingSpec = [source: string, target: string, factor?: number, weight?: number, selection?: SourceSelection]

interface BrVersionSpec {
  cardinality: MappingCardinality
  mode: MappingAllocationMode
  created: string
  frozen?: Frozen
  mappings: MappingSpec[]
}

interface BrSpec {
  key: string
  displayName: string
  source: string
  target: string
  isEnabled?: boolean
  owner?: BridgeOwner
  versions: BrVersionSpec[]
  bindings?: MappingBindingUsage[]
}

const lifecycle = (v: { created: string; frozen?: Frozen }) => ({
  status: v.frozen ? ('retired' as const) : ('published' as const),
  createdAt: at(v.created),
  publishedAt: at(v.created),
  retiredAt: v.frozen ? at(v.frozen.on) : null,
  freezeNote: v.frozen?.note ?? null,
})

function build(sets: MsSpec[], bridges: BrSpec[], baseline: Record<string, number>): Db {
  const db: Db = { mappingSets: [], mappingVersions: [], entries: [], bridges: [], bridgeVersions: [], bridgeMappings: [], bindings: {}, assumptionSets: [] }
  const active: AssumptionSet = { id: 'as-active', key: 'active', displayName: 'Active', isActive: true, pins: {} }
  const historical: AssumptionSet = { id: 'as-baseline_2024', key: 'baseline_2024', displayName: '2024 baseline', isActive: false, pins: {} }

  for (const s of sets) {
    const id = `ms-${s.key}`
    db.mappingSets.push({ id, key: s.key, displayName: s.displayName, sourceNamespace: s.namespace, dimensionId: dim(s.dimension) })
    db.bindings[id] = s.bindings ?? []
    s.versions.forEach((v, i) => {
      const n = i + 1
      const vid = `${id}-v${n}`
      db.mappingVersions.push({ id: vid, mappingSetId: id, versionNumber: n, ...lifecycle(v) })
      v.entries.forEach(([sourceKeyValue, memberKey], j) => db.entries.push({ id: `${vid}-e${j + 1}`, versionId: vid, sourceKeyValue, memberKey }))
      if (!v.frozen) active.pins[id] = vid
      if (baseline[s.key] === n) historical.pins[id] = vid
    })
  }

  for (const b of bridges) {
    const id = `bridge-${b.key}`
    db.bridges.push({ id, key: b.key, displayName: b.displayName, sourceDimensionId: dim(b.source), targetDimensionId: dim(b.target), isEnabled: b.isEnabled ?? true, owner: b.owner ?? null })
    db.bindings[id] = b.bindings ?? []
    b.versions.forEach((v, i) => {
      const n = i + 1
      const vid = `${id}-v${n}`
      db.bridgeVersions.push({ id: vid, bridgeId: id, versionNumber: n, cardinality: v.cardinality, allocationMode: v.mode, ...lifecycle(v) })
      v.mappings.forEach(([sourceMemberKey, targetMemberKey, factor, weight, selection], j) =>
        db.bridgeMappings.push({ id: `${vid}-m${j + 1}`, versionId: vid, sourceMemberKey, targetMemberKey, factor: factor ?? 1, allocationWeight: weight ?? 1, sourceSelection: selection ?? 'member' }),
      )
      if (!v.frozen) active.pins[id] = vid
      if (baseline[b.key] === n) historical.pins[id] = vid
    })
  }

  db.assumptionSets = [active, historical]
  return db
}

const binding = (datasetKey: string, datasetVersionNumber: number, fieldKey: string, roleKey: string, sourceRoleKey: string | null = null): MappingBindingUsage => ({
  datasetId: `ds-${datasetKey}`,
  datasetKey,
  datasetVersionId: `ds-${datasetKey}-v${datasetVersionNumber}`,
  datasetVersionNumber,
  status: 'published',
  fieldKey,
  roleKey,
  sourceRoleKey,
})

const reportOwner = (reportKey: string, sourceKey: string): BridgeOwner => ({ reportId: reportKey, reportKey, sourceKey })

/* ───────────────────────── mapping sets ───────────────────────── */

function loanBranchEntries(upTo: number): [string, string][] {
  const out: [string, string][] = []
  for (let i = 1; i <= upTo; i++) out.push([pad(i), br(i)])
  for (let i = 1; i <= upTo; i++) out.push([`C${pad(i, 2)}`, br(i)])
  return out
}

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
      { created: '2023-01-20', frozen: { on: '2024-07-02', note: 'Before the 2024 branch consolidation' }, entries: loanBranchEntries(15) },
      { created: '2024-07-02', entries: [...loanBranchEntries(18), ['0100', 'BR01'], [' 007', 'BR07']] },
    ],
    bindings: [binding('loans.details', 3, 'servicing_branch_code', 'servicingBranch'), binding('loans.details', 3, 'origination_branch_code', 'originationBranch')],
  },
  {
    key: 'finance.branch',
    displayName: 'GL branch codes',
    namespace: 'gl',
    dimension: 'Branch',
    versions: [{ created: '2024-01-08', entries: [['001', 'BR02'], ['002', 'BR01'], ...Array.from({ length: 8 }, (_, i): [string, string] => [pad(i + 3), br(i + 3)])] }],
    bindings: [binding('gl.balances', 5, 'cost_centre_branch', 'branch')],
  },
  {
    key: 'loan.product',
    displayName: 'Loan product codes',
    namespace: 'loan_servicing',
    dimension: 'Product',
    versions: [
      { created: '2024-02-01', frozen: { on: '2026-10-19', note: 'FY26 close' }, entries: PRODUCT_V1 },
      { created: '2026-10-19', entries: PRODUCT_V1 },
    ],
    bindings: [binding('loans.details', 3, 'product_code', 'product')],
  },
  {
    key: 'collateral.codes',
    displayName: 'Collateral codes',
    namespace: 'collateral_mgmt',
    dimension: 'Collateral',
    versions: [
      {
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
    versions: [{ cardinality: 'many_to_one', mode: 'none', created: '2025-06-10', mappings: ACCOUNT_GROUPS }],
    bindings: [binding('bank.balance_monthly', 2, 'account', 'accountGroup', 'account')],
  },
  {
    key: 'cost_center_to_department',
    displayName: 'Cost center → department',
    source: 'CostCenter',
    target: 'Department',
    versions: [
      {
        cardinality: 'many_to_many',
        mode: 'partition',
        created: '2025-03-05',
        frozen: { on: '2026-10-22', note: 'FY26 cost allocation' },
        mappings: [
          ['CC_100', 'DEPT_RETAIL', 1, 0.6],
          ['CC_100', 'DEPT_COMMERCIAL', 1, 0.4],
          ['CC_200', 'DEPT_RETAIL', 1, 0.5],
          ['CC_200', 'DEPT_COMMERCIAL', 1, 0.3],
          ['CC_200', 'DEPT_TREASURY', 1, 0.2],
          ['CC_300', 'DEPT_RETAIL', 1, 0.7],
          ['CC_300', 'DEPT_COMMERCIAL', 1, 0.3],
          ['CC_400', 'DEPT_CORPORATE', 1, 1],
        ],
      },
      {
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
    bindings: [binding('opex.monthly', 1, 'cost_center', 'department', 'costCenter')],
  },
  {
    key: 'product_to_risk_class',
    displayName: 'Product → risk class',
    source: 'Product',
    target: 'RiskClass',
    versions: [
      {
        cardinality: 'many_to_one',
        mode: 'none',
        created: '2026-10-12',
        mappings: [
          ['MORTGAGE', 'RC_LOW'],
          ['AUTO', 'RC_MEDIUM'],
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
    owner: reportOwner('bank.margin_statement', 'gl_monthly'),
    versions: [
      {
        cardinality: 'many_to_many',
        mode: 'replicate',
        created: '2025-02-03',
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
    owner: reportOwner('bank.deposit_pricing', 'gl_monthly'),
    versions: [
      {
        cardinality: 'many_to_many',
        mode: 'replicate',
        created: '2025-03-11',
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
    owner: reportOwner('bank.branch_profitability', 'gl_monthly'),
    versions: [
      {
        cardinality: 'many_to_many',
        mode: 'replicate',
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
        cardinality: 'many_to_one',
        mode: 'none',
        created: '2025-04-04',
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
