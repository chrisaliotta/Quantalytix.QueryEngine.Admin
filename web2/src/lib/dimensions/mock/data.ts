import type { Dimension, DimensionProfile, DimensionVersion, DimensionLevel, DimensionMember, DimensionPeriodicity, RangeBand, RangeDataType, DimensionRole, DimensionUsageReference, VersionStatus } from '../contract'

export interface Db {
  profiles: DimensionProfile[]
  dimensions: Dimension[]
  versions: DimensionVersion[]
  roles: DimensionRole[]
  levels: DimensionLevel[]
  members: DimensionMember[]
  /** Bands keyed by version id; bands save as a set. */
  bands: Record<string, RangeBand[]>
  /** References keyed by dimension id. */
  usage: Record<string, DimensionUsageReference[]>
}

/* ───────────────────────── profiles ───────────────────────── */

const PROFILES: DimensionProfile[] = [
  {
    key: 'branch',
    displayName: 'Branch',
    kind: 'hierarchical',
    owner: 'Core',
    editPolicy: 'extend',
    visibility: 'manager',
    requiredRoleKeys: ['branch'],
    requiredMemberKeys: ['BR01'],
    cardinality: 'one',
    memberPropertySchema: [
      { key: 'photoUrl', label: 'Photo', type: 'image', description: 'Shown on branch scorecards.' },
      { key: 'address', label: 'Address', type: 'text', required: true },
      { key: 'geo', label: 'Location', type: 'geo', description: 'Latitude and longitude of the branch entrance.' },
      { key: 'openedOn', label: 'Opened on', type: 'date' },
      { key: 'catchment', label: 'Catchment boundary', type: 'text', format: 'geojson', description: 'GeoJSON polygon for the branch catchment area.' },
    ],
  },
  {
    key: 'officer',
    displayName: 'Officer',
    kind: 'categorical',
    owner: 'Core',
    editPolicy: 'extend',
    visibility: 'manager',
    requiredRoleKeys: ['officer'],
    requiredMemberKeys: [],
    cardinality: 'one',
    memberPropertySchema: [
      { key: 'email', label: 'Email', type: 'text', required: true },
      { key: 'photoUrl', label: 'Photo', type: 'image' },
      { key: 'title', label: 'Title', type: 'text' },
    ],
  },
  {
    key: 'product',
    displayName: 'Product',
    kind: 'hierarchical',
    owner: 'Profitability',
    editPolicy: 'locked',
    visibility: 'manager',
    requiredRoleKeys: ['product'],
    requiredMemberKeys: ['MORTGAGE', 'AUTO', 'COMMERCIAL', 'DEPOSITS'],
    cardinality: 'one',
    memberPropertySchema: [
      { key: 'glPrefix', label: 'GL prefix', type: 'text' },
      { key: 'ftpCurve', label: 'FTP curve', type: 'select', options: ['SOFR', 'Treasury', 'Prime'] },
    ],
  },
  {
    key: 'planVersion',
    displayName: 'Scenario',
    kind: 'hierarchical',
    owner: 'Planning',
    editPolicy: 'extend',
    visibility: 'manager',
    requiredRoleKeys: ['planVersion'],
    requiredMemberKeys: [],
    cardinality: 'one',
    singleRole: true,
    memberPropertySchema: [
      { key: 'kind', label: 'Kind', type: 'select', options: ['budget', 'forecast'], required: true },
      { key: 'horizonStart', label: 'Horizon start', type: 'date' },
      { key: 'horizonEnd', label: 'Horizon end', type: 'date' },
      { key: 'asOf', label: 'As of', type: 'date' },
      { key: 'status', label: 'Status', type: 'select', options: ['working', 'submitted', 'approved', 'locked'] },
    ],
  },
  {
    key: 'channel',
    displayName: 'Channel',
    kind: 'categorical',
    owner: 'Digital',
    editPolicy: 'open',
    visibility: 'manager',
    requiredRoleKeys: ['channel'],
    requiredMemberKeys: ['BRANCH', 'ONLINE', 'MOBILE'],
    cardinality: 'one',
    seedNames: { channel: 'Channel', BRANCH: 'Branch', ONLINE: 'Online banking', MOBILE: 'Mobile app' },
    memberPropertySchema: [
      { key: 'isDigital', label: 'Digital channel', type: 'boolean' },
      { key: 'helpUrl', label: 'Help page', type: 'url' },
    ],
  },
  {
    key: 'costCentre',
    displayName: 'Cost centre',
    kind: 'hierarchical',
    owner: 'Cost Allocation',
    editPolicy: 'extend',
    visibility: 'manager',
    requiredRoleKeys: ['costCentre'],
    requiredMemberKeys: ['UNALLOCATED'],
    cardinality: 'one',
    seedNames: { costCentre: 'Cost centre', UNALLOCATED: 'Unallocated' },
    memberPropertySchema: [
      { key: 'manager', label: 'Manager', type: 'text' },
      { key: 'headcount', label: 'Headcount', type: 'number' },
    ],
  },
  {
    key: 'glAccountClass',
    displayName: 'GL account class',
    kind: 'categorical',
    owner: 'General Ledger',
    editPolicy: 'extend',
    visibility: 'module',
    requiredRoleKeys: ['glAccountClass'],
    requiredMemberKeys: ['ASSET', 'LIABILITY', 'EQUITY', 'INCOME', 'EXPENSE'],
    cardinality: 'one',
    memberPropertySchema: [{ key: 'normalBalance', label: 'Normal balance', type: 'select', options: ['debit', 'credit'] }],
  },
  {
    key: 'systemAccountClass',
    displayName: 'System account class',
    kind: 'categorical',
    owner: 'Platform',
    editPolicy: 'locked',
    visibility: 'hidden',
    requiredRoleKeys: [],
    requiredMemberKeys: [],
    cardinality: 'one',
    memberPropertySchema: [],
  },
]

/* ───────────────────────── builders ───────────────────────── */

type MemberSpec = [key: string, displayName: string, parentKey?: string | null, opts?: { level?: string; folder?: boolean; props?: Record<string, unknown>; description?: string }]
type LevelSpec = [key: string, displayName: string, isLeaf?: boolean, periodicity?: DimensionPeriodicity]

interface VersionSpec {
  n: number
  status: VersionStatus
  created: string
  published?: string
  retired?: string
  supersededBy?: number
  rangeDataType?: RangeDataType
  levels?: LevelSpec[]
  members?: MemberSpec[]
  /** [memberKey, lower, upper, lowerInclusive?, upperInclusive?] */
  bands?: [string, string | number | null, string | number | null, boolean?, boolean?][]
  /** Raw parent overrides applied after building, used to seed a cycle. */
  parentOverrides?: Record<string, string>
}

interface DimSpec {
  key: string
  displayName: string
  kind: Dimension['kind']
  origin: Dimension['origin']
  purpose?: Dimension['purpose']
  ownerReport?: Dimension['ownerReport']
  profileKey?: string
  isEnabled?: boolean
  isRemovable?: boolean
  roles?: [key: string, displayName: string, description?: string][]
  versions: VersionSpec[]
  usage?: DimensionUsageReference[]
}

function build(specs: DimSpec[]): Db {
  const db: Db = { profiles: PROFILES, dimensions: [], versions: [], roles: [], levels: [], members: [], bands: {}, usage: {} }
  for (const s of specs) {
    const dimId = `dim-${s.key}`
    db.dimensions.push({
      id: dimId,
      key: s.key,
      displayName: s.displayName,
      kind: s.kind,
      origin: s.origin,
      purpose: s.purpose ?? 'analytical',
      ownerReport: s.ownerReport ?? null,
      profileKey: s.profileKey ?? null,
      isEnabled: s.isEnabled ?? true,
      isRemovable: s.origin === 'system' ? false : (s.isRemovable ?? true),
    })
    for (const [key, displayName, description] of s.roles ?? []) db.roles.push({ id: `role-${key}`, dimensionId: dimId, key, displayName, description: description ?? null })
    db.usage[dimId] = s.usage ?? []

    for (const v of s.versions) {
      const vid = `${dimId}-v${v.n}`
      db.versions.push({
        id: vid,
        dimensionId: dimId,
        versionNumber: v.n,
        status: v.status,
        rangeDataType: v.rangeDataType ?? null,
        createdAt: v.created,
        publishedAt: v.published ?? null,
        retiredAt: v.retired ?? null,
        supersededBy: v.supersededBy ?? null,
        freezeNote: null,
      })
      const levelId = (k: string) => `${vid}-l-${k}`
      ;(v.levels ?? []).forEach(([key, displayName, isLeaf, periodicity], i) =>
        db.levels.push({ id: levelId(key), versionId: vid, key, displayName, ordinal: i + 1, isLeaf: !!isLeaf, periodicity: periodicity ?? null }),
      )
      const memberId = (k: string) => `${vid}-m-${k}`
      const siblingCount: Record<string, number> = {}
      const seen = new Set<string>()
      for (const [key, displayName, parentKey, opts] of v.members ?? []) {
        const slot = parentKey ?? '__root__'
        siblingCount[slot] = (siblingCount[slot] ?? 0) + 1
        let id = memberId(key)
        if (seen.has(id)) id = `${id}-dup`
        seen.add(id)
        db.members.push({
          id,
          versionId: vid,
          key,
          displayName,
          description: opts?.description ?? null,
          parentId: parentKey ? memberId(parentKey) : null,
          levelId: opts?.level ? levelId(opts.level) : null,
          isAssignable: !opts?.folder,
          sortOrder: siblingCount[slot],
          properties: opts?.props ?? {},
        })
      }
      for (const [child, parent] of Object.entries(v.parentOverrides ?? {})) {
        const m = db.members.find((x) => x.id === memberId(child))
        if (m) m.parentId = memberId(parent)
      }
      if (v.bands) {
        db.bands[vid] = v.bands.map(([key, lower, upper, li, ui], i) => ({
          ordinal: i + 1,
          memberId: memberId(key),
          lower,
          upper,
          lowerInclusive: li ?? lower !== null,
          upperInclusive: ui ?? false,
        }))
      }
    }
  }
  return db
}

/* ───────────────────────── Branch ───────────────────────── */

const BRANCH_LEVELS: LevelSpec[] = [
  ['region', 'Region'],
  ['district', 'District'],
  ['branch', 'Branch', true],
]

const BRANCHES: [key: string, name: string, district: string, street: string, lat: number, lng: number, opened: string][] = [
  ['BR01', 'Head office', 'N_METRO', '100 Market St', 41.881, -87.628, '1962-03-01'],
  ['BR02', 'Riverside', 'N_METRO', '22 River Rd', 41.889, -87.637, '1978-06-12'],
  ['BR03', 'Union Square', 'N_METRO', '5 Union Sq', 41.874, -87.62, '1985-09-30'],
  ['BR04', 'Old Union Square', 'N_METRO', '9 Union Sq', 41.873, -87.621, '1971-01-15'],
  ['BR05', 'Lakeview', 'N_LAKES', '810 Shore Dr', 41.94, -87.65, '1990-04-02'],
  ['BR06', 'Pine Harbor', 'N_LAKES', '41 Harbor Ln', 42.03, -87.68, '1996-11-20'],
  ['BR07', 'Northfield', 'N_LAKES', '1200 Elm Ave', 42.1, -87.76, '2004-05-17'],
  ['BR08', 'Bayfront', 'S_COAST', '3 Bay St', 29.951, -90.071, '1983-07-08'],
  ['BR09', 'Gulf Point', 'S_COAST', '77 Gulf Blvd', 30.4, -89.06, '1999-02-26'],
  ['BR10', 'Seabrook', 'S_COAST', '15 Pier Way', 29.56, -95.02, '2008-10-03'],
  ['BR11', 'Magnolia', 'S_VALLEY', '640 Magnolia St', 32.3, -90.18, '1988-08-14'],
  ['BR12', 'Cedar Ridge', 'S_VALLEY', '2 Ridge Rd', 32.51, -92.12, '2001-03-22'],
  ['BR13', 'Willow Creek', 'S_VALLEY', '58 Creek Pkwy', 33.2, -91.8, '2012-06-01'],
  ['BR14', 'Prairie Gate', 'W_PLAINS', '900 Gate Ave', 39.74, -104.99, '1993-09-09'],
  ['BR15', 'Mesa Verde', 'W_PLAINS', '13 Mesa Rd', 35.08, -106.65, '2006-12-11'],
  ['BR16', 'High Plains', 'W_PLAINS', '400 Plains Hwy', 41.14, -104.82, '2019-04-29'],
  ['BR17', 'Canyon Springs', 'W_PLAINS', '7 Canyon Ct', 36.17, -115.14, '2021-08-16'],
  ['BR18', 'Silver Lake', 'W_PLAINS', '260 Silver St', 39.53, -119.81, '2023-02-06'],
]

function branchMembers(upTo: number): MemberSpec[] {
  const out: MemberSpec[] = [
    ['NORTH', 'North region', null, { level: 'region', folder: true }],
    ['N_METRO', 'Metro district', 'NORTH', { level: 'district', folder: true }],
    ['N_LAKES', 'Lakes district', 'NORTH', { level: 'district', folder: true }],
    ['SOUTH', 'South region', null, { level: 'region', folder: true }],
    ['S_COAST', 'Coast district', 'SOUTH', { level: 'district', folder: true }],
    ['S_VALLEY', 'Valley district', 'SOUTH', { level: 'district', folder: true }],
    ['WEST', 'West region', null, { level: 'region', folder: true }],
    ['W_PLAINS', 'Plains district', 'WEST', { level: 'district', folder: true }],
  ]
  BRANCHES.slice(0, upTo).forEach(([key, name, district, street, lat, lng, opened], i) =>
    out.push([
      key,
      name,
      district,
      {
        level: 'branch',
        props: {
          ...(i % 3 !== 2 ? { photoUrl: i % 2 ? '/images/dimensions/branch-2.png' : '/images/dimensions/branch-1.png' } : {}),
          address: street,
          geo: { lat, lng },
          openedOn: opened,
        },
      },
    ]),
  )
  return out
}

/* ───────────────────────── seed ───────────────────────── */

export function createSeed(): Db {
  return build([
    {
      key: 'Branch',
      displayName: 'Branch',
      kind: 'hierarchical',
      origin: 'system',
      profileKey: 'branch',
      roles: [
        ['branch', 'Branch', 'The branch that owns the account.'],
        ['servicingBranch', 'Servicing branch', 'The branch that services the loan day to day.'],
        ['originationBranch', 'Origination branch', 'The branch where the loan was originated.'],
      ],
      versions: [
        { n: 1, status: 'retired', created: '2023-01-09', published: '2023-01-16', retired: '2024-07-01', supersededBy: 2, levels: BRANCH_LEVELS, members: branchMembers(15) },
        { n: 2, status: 'published', created: '2024-06-20', published: '2024-07-01', levels: BRANCH_LEVELS, members: branchMembers(18) },
      ],
      usage: [
        { kind: 'dataset_binding', name: 'loans.details', label: "'loans.details' v3", detail: 'loans.details v3 · field servicing_branch_code · mapped', roleKey: 'servicingBranch', memberKeys: ['BR01'] },
        { kind: 'dataset_binding', name: 'loans.details', label: "'loans.details' v3", detail: 'loans.details v3 · field origination_branch_code · direct', roleKey: 'originationBranch' },
        { kind: 'dataset_binding', name: 'deposits.daily', label: "'deposits.daily' v2", detail: 'deposits.daily v2 · field branch_code · direct', roleKey: 'branch' },
        { kind: 'dataset_binding', name: 'gl.balances', label: "'gl.balances' v5", detail: 'gl.balances v5 · field cost_centre_branch · mapped', roleKey: 'branch' },
        { kind: 'mapping_set', name: 'loan.branch', detail: 'loan_servicing codes to branch keys · see the Mappings section', memberKeys: ['BR09', 'BR12'] },
        { kind: 'mapping_set', name: 'finance.branch', detail: 'gl codes to branch keys · see the Mappings section', memberKeys: ['BR02'] },
        { kind: 'bridge', name: 'branch_to_territory', detail: 'from Branch to Territory · many-to-one · see the Bridges section' },
        { kind: 'calculation_dimension', name: 'Branch profitability', detail: 'grouping by Region', roleKey: 'branch' },
        { kind: 'report_source', name: 'Regional scorecard', detail: 'ancestor grouping NORTH', roleKey: 'branch', memberKeys: ['NORTH'] },
        { kind: 'assumption_set', name: 'active', detail: 'active → v2', pinsVersion: 2 },
        { kind: 'assumption_set', name: '2024 baseline', detail: '2024 baseline → v1', pinsVersion: 1 },
      ],
    },
    {
      key: 'Officer',
      displayName: 'Officer',
      kind: 'categorical',
      origin: 'system',
      profileKey: 'officer',
      roles: [['officer', 'Officer', 'The relationship officer responsible for the account.']],
      versions: [
        {
          n: 1,
          status: 'published',
          created: '2023-02-01',
          published: '2023-02-03',
          members: [
            ['OF_ADAMS', 'Rachel Adams', null, { props: { email: 'r.adams@meridian.bank', photoUrl: '/images/dimensions/officer-1.png', title: 'Senior loan officer' } }],
            ['OF_BAKER', 'Marcus Baker', null, { props: { email: 'm.baker@meridian.bank', photoUrl: '/images/dimensions/officer-2.png', title: 'Commercial banker' } }],
            ['OF_CHEN', 'Lily Chen', null, { props: { email: 'l.chen@meridian.bank', title: 'Mortgage advisor' } }],
            ['OF_DIAZ', 'Javier Diaz', null, { props: { email: 'j.diaz@meridian.bank', title: 'Relationship manager' } }],
            ['OF_EVANS', 'Priya Evans', null, { props: { email: 'p.evans@meridian.bank', photoUrl: '/images/dimensions/officer-1.png', title: 'Branch manager' } }],
            ['OF_FOSTER', 'Tom Foster', null, { props: { email: 't.foster@meridian.bank', title: 'Small business banker' } }],
            ['OF_UNASSIGNED', 'Unassigned', null, { props: { email: 'lending-ops@meridian.bank' }, description: 'Accounts without an officer of record.' }],
          ],
        },
      ],
      usage: [{ kind: 'dataset_binding', name: 'loans.details', label: "'loans.details' v3", detail: 'loans.details v3 · field officer_id · mapped', roleKey: 'officer', memberKeys: ['OF_UNASSIGNED'] }],
    },
    {
      key: 'Product',
      displayName: 'Product',
      kind: 'hierarchical',
      origin: 'system',
      profileKey: 'product',
      roles: [['product', 'Product']],
      versions: [
        {
          n: 1,
          status: 'published',
          created: '2023-01-05',
          published: '2023-01-06',
          levels: [
            ['family', 'Product family'],
            ['product', 'Product', true],
          ],
          members: [
            ['RETAIL_LENDING', 'Retail lending', null, { level: 'family', folder: true }],
            ['MORTGAGE', 'Residential mortgage', 'RETAIL_LENDING', { level: 'product', props: { glPrefix: '1410', ftpCurve: 'Treasury' } }],
            ['AUTO', 'Auto loan', 'RETAIL_LENDING', { level: 'product', props: { glPrefix: '1420', ftpCurve: 'SOFR' } }],
            ['COMMERCIAL_LENDING', 'Commercial lending', null, { level: 'family', folder: true }],
            ['COMMERCIAL', 'Commercial term loan', 'COMMERCIAL_LENDING', { level: 'product', props: { glPrefix: '1500', ftpCurve: 'SOFR' } }],
            ['FUNDING', 'Funding', null, { level: 'family', folder: true }],
            ['DEPOSITS', 'Deposits', 'FUNDING', { level: 'product', props: { glPrefix: '2100', ftpCurve: 'SOFR' } }],
          ],
        },
      ],
      usage: [
        { kind: 'dataset_binding', name: 'loans.details', label: "'loans.details' v3", detail: 'loans.details v3 · field product_code · mapped', roleKey: 'product' },
        { kind: 'calculation_dimension', name: 'Bank margin', detail: 'grouping by Product family', roleKey: 'product' },
      ],
    },
    {
      key: 'Scenario',
      displayName: 'Scenario',
      kind: 'hierarchical',
      origin: 'system',
      profileKey: 'planVersion',
      roles: [['planVersion', 'Plan version', 'The plan scenario a figure belongs to.']],
      versions: [
        {
          n: 1,
          status: 'published',
          created: '2024-09-01',
          published: '2024-09-02',
          members: [
            ['BUDGETS', 'Budgets', null, { folder: true }],
            ['BUD2025', 'Budget 2025', 'BUDGETS', { props: { kind: 'budget', horizonStart: '2025-01-01', horizonEnd: '2025-12-31', asOf: '2024-11-15', status: 'locked' } }],
            ['BUD2026', 'Budget 2026', 'BUDGETS', { props: { kind: 'budget', horizonStart: '2026-01-01', horizonEnd: '2026-12-31', asOf: '2025-11-14', status: 'approved' } }],
            ['FORECASTS', 'Forecasts', null, { folder: true }],
            ['FC2026Q2', 'Forecast Q2 2026', 'FORECASTS', { props: { kind: 'forecast', horizonStart: '2026-04-01', horizonEnd: '2026-12-31', asOf: '2026-03-31', status: 'approved' } }],
            ['FC2026Q3', 'Forecast Q3 2026', 'FORECASTS', { props: { kind: 'forecast', horizonStart: '2026-07-01', horizonEnd: '2026-12-31', asOf: '2026-06-30', status: 'submitted' } }],
            ['FC2026Q4', 'Forecast Q4 2026', 'FORECASTS', { props: { kind: 'forecast', horizonStart: '2026-10-01', horizonEnd: '2027-03-31', asOf: '2026-09-30', status: 'working' } }],
          ],
        },
      ],
      usage: [
        { kind: 'report_scenario', name: 'Budget vs actual', detail: 'compares BUD2026 with actuals', roleKey: 'planVersion', memberKeys: ['BUD2026'] },
        { kind: 'report_scenario', name: 'Rolling forecast', detail: 'latest forecast under FORECASTS', roleKey: 'planVersion' },
      ],
    },
    {
      key: 'GlAccountClass',
      displayName: 'GL account class',
      kind: 'categorical',
      origin: 'system',
      profileKey: 'glAccountClass',
      roles: [['glAccountClass', 'GL account class']],
      versions: [
        {
          n: 1,
          status: 'published',
          created: '2023-01-02',
          published: '2023-01-02',
          members: [
            ['ASSET', 'Asset', null, { props: { normalBalance: 'debit' } }],
            ['LIABILITY', 'Liability', null, { props: { normalBalance: 'credit' } }],
            ['EQUITY', 'Equity', null, { props: { normalBalance: 'credit' } }],
            ['INCOME', 'Income', null, { props: { normalBalance: 'credit' } }],
            ['EXPENSE', 'Expense', null, { props: { normalBalance: 'debit' } }],
          ],
        },
      ],
      usage: [{ kind: 'dataset_binding', name: 'gl.balances', label: "'gl.balances' v5", detail: 'gl.balances v5 · field account_class · direct', roleKey: 'glAccountClass' }],
    },
    {
      key: 'SystemAccountClass',
      displayName: 'System account class',
      kind: 'categorical',
      origin: 'system',
      profileKey: 'systemAccountClass',
      versions: [{ n: 1, status: 'published', created: '2023-01-01', published: '2023-01-01', members: [['SUSPENSE', 'Suspense']] }],
    },

    /* user dimensions */
    {
      key: 'RiskGrade',
      displayName: 'Risk grade',
      kind: 'categorical',
      origin: 'user',
      roles: [['riskGrade', 'Risk grade', 'Internal credit risk rating.']],
      versions: [
        { n: 1, status: 'published', created: '2024-02-10', published: '2024-02-12', members: riskGrades() },
        {
          n: 2,
          status: 'draft',
          created: '2026-10-14',
          members: [...riskGrades(), ['RG5', 'Special mention', null, { description: 'Imported from the credit file; its key collides with Watch.' }]],
        },
      ],
      usage: [{ kind: 'dataset_binding', name: 'loans.details', label: "'loans.details' v3", detail: 'loans.details v3 · field risk_grade · direct', roleKey: 'riskGrade' }],
    },
    {
      key: 'BalanceBand',
      displayName: 'Balance band',
      kind: 'range',
      origin: 'user',
      roles: [['balanceBand', 'Balance band']],
      versions: [
        { n: 1, status: 'published', created: '2024-03-01', published: '2024-03-04', rangeDataType: 'decimal', members: balanceMembers(), bands: balanceBands(100000) },
        { n: 2, status: 'draft', created: '2026-10-20', rangeDataType: 'decimal', members: balanceMembers(), bands: balanceBands(90000) },
      ],
      usage: [{ kind: 'dataset_binding', name: 'deposits.daily', label: "'deposits.daily' v2", detail: 'deposits.daily v2 · field ledger_balance · range', roleKey: 'balanceBand' }],
    },
    {
      key: 'MaturityBucket',
      displayName: 'Maturity bucket',
      kind: 'range',
      origin: 'user',
      roles: [['maturityBucket', 'Maturity bucket']],
      versions: [
        {
          n: 1,
          status: 'published',
          created: '2025-01-03',
          published: '2025-01-06',
          rangeDataType: 'date',
          members: [
            ['M_PAST', 'Matured'],
            ['M_2026', 'Matures 2026'],
            ['M_2027_28', 'Matures 2027–2028'],
            ['M_LATER', 'Matures 2029 or later'],
          ],
          bands: [
            ['M_PAST', null, '2026-01-01'],
            ['M_2026', '2026-01-01', '2027-01-01'],
            ['M_2027_28', '2027-01-01', '2029-01-01'],
            ['M_LATER', '2029-01-01', null],
          ],
        },
      ],
      usage: [{ kind: 'dataset_binding', name: 'loans.details', label: "'loans.details' v3", detail: 'loans.details v3 · field maturity_date · range', roleKey: 'maturityBucket' }],
    },
    {
      key: 'FiscalCalendar',
      displayName: 'Fiscal calendar',
      kind: 'time',
      origin: 'user',
      versions: [
        {
          n: 1,
          status: 'published',
          created: '2023-01-02',
          published: '2023-01-03',
          levels: [
            ['YEAR', 'Fiscal year', false, 'year'],
            ['QUARTER', 'Fiscal quarter', false, 'quarter'],
            ['MONTH', 'Fiscal month', false, 'month'],
            ['DAY', 'Day', true, 'day'],
          ],
        },
      ],
      usage: [{ kind: 'dataset_binding', name: 'gl.balances', label: "'gl.balances' v5", detail: 'gl.balances v5 · field posting_date · time' }],
    },
    {
      key: 'Territory',
      displayName: 'Territory',
      kind: 'hierarchical',
      origin: 'user',
      roles: [['territory', 'Sales territory']],
      versions: [
        { n: 1, status: 'published', created: '2025-04-01', published: '2025-04-03', levels: BRANCH_LEVELS, members: territoryMembers() },
        { n: 2, status: 'draft', created: '2026-10-18', levels: BRANCH_LEVELS, members: territoryMembers(), parentOverrides: { T_N_METRO: 'T_BR02' } },
      ],
      usage: [{ kind: 'bridge', name: 'branch_to_territory', detail: 'from Branch to Territory · many-to-one' }],
    },
    {
      key: 'Collateral',
      displayName: 'Collateral type',
      kind: 'categorical',
      origin: 'user',
      roles: [['collateral', 'Collateral type']],
      versions: [
        {
          n: 1,
          status: 'draft',
          created: '2026-10-21',
          members: [
            ['REAL_ESTATE', 'Real estate'],
            ['VEHICLE', 'Vehicle'],
            ['CASH', 'Cash and deposits'],
            ['SECURITIES', 'Marketable securities'],
            ['UNSECURED', 'Unsecured'],
          ],
        },
      ],
    },
    {
      key: 'Purpose',
      displayName: 'Purpose',
      kind: 'categorical',
      origin: 'user',
      roles: [['loanPurpose', 'Loan purpose']],
      versions: [
        {
          n: 1,
          status: 'published',
          created: '2024-05-06',
          published: '2024-05-07',
          members: [
            ['HOME_PURCHASE', 'Home purchase'],
            ['REFINANCE', 'Refinance'],
            ['VEHICLE', 'Vehicle'],
            ['WORKING_CAPITAL', 'Working capital'],
            ['EQUIPMENT', 'Equipment'],
            ['OTHER', 'Other'],
          ],
        },
      ],
      usage: [
        { kind: 'dataset_binding', name: 'loans.details', label: "'loans.details' v3", detail: 'loans.details v3 · field purpose_code · mapped', roleKey: 'loanPurpose' },
        { kind: 'mapping_set', name: 'loan.purpose', detail: '14 entries · origination system codes', memberKeys: ['REFINANCE', 'OTHER'] },
      ],
    },
    {
      key: 'Account',
      displayName: 'Account',
      kind: 'hierarchical',
      origin: 'user',
      roles: [['account', 'GL account', 'The general ledger account a balance posts to.']],
      versions: [
        {
          n: 3,
          status: 'published',
          created: '2025-06-02',
          published: '2025-06-09',
          levels: [
            ['class', 'Account class'],
            ['group', 'Account group'],
            ['account', 'Account', true],
          ],
          members: accountMembers(),
        },
      ],
    },
    {
      key: 'AccountGroup',
      displayName: 'Account group',
      kind: 'categorical',
      origin: 'user',
      roles: [['accountGroup', 'Account group']],
      versions: [
        {
          n: 1,
          status: 'published',
          created: '2025-06-02',
          published: '2025-06-09',
          members: [
            ['AG_CASH', 'Cash and equivalents'],
            ['AG_LOANS', 'Loans'],
            ['AG_SECURITIES', 'Securities'],
            ['AG_FIXED', 'Fixed assets'],
            ['AG_DEPOSITS', 'Deposits'],
            ['AG_BORROWINGS', 'Borrowings'],
            ['AG_EQUITY', 'Equity'],
            ['AG_INT_INCOME', 'Interest income'],
            ['AG_FEES', 'Fee income'],
            ['AG_INT_EXPENSE', 'Interest expense'],
            ['AG_OPEX', 'Operating expense'],
          ],
        },
      ],
    },
    {
      key: 'RiskClass',
      displayName: 'Risk class',
      kind: 'categorical',
      origin: 'user',
      roles: [['riskClass', 'Risk class']],
      versions: [
        {
          n: 1,
          status: 'published',
          created: '2025-08-11',
          published: '2025-08-12',
          members: [
            ['RC_LOW', 'Low risk'],
            ['RC_MEDIUM', 'Medium risk'],
            ['RC_HIGH', 'High risk'],
          ],
        },
      ],
    },
    {
      key: 'CostCenter',
      displayName: 'Cost center',
      kind: 'categorical',
      origin: 'user',
      roles: [['costCenter', 'Cost center']],
      versions: [
        {
          n: 1,
          status: 'published',
          created: '2025-03-03',
          published: '2025-03-04',
          members: [
            ['CC_100', 'Branch operations'],
            ['CC_200', 'Shared technology'],
            ['CC_300', 'Marketing'],
            ['CC_400', 'Finance'],
          ],
        },
      ],
    },
    {
      key: 'Department',
      displayName: 'Department',
      kind: 'categorical',
      origin: 'user',
      roles: [['department', 'Department']],
      versions: [
        {
          n: 1,
          status: 'published',
          created: '2025-03-03',
          published: '2025-03-04',
          members: [
            ['DEPT_RETAIL', 'Retail banking'],
            ['DEPT_COMMERCIAL', 'Commercial banking'],
            ['DEPT_TREASURY', 'Treasury'],
            ['DEPT_CORPORATE', 'Corporate center'],
          ],
        },
      ],
    },
    {
      key: 'MarginStatement',
      displayName: 'Bank margin · rows',
      kind: 'hierarchical',
      origin: 'user',
      purpose: 'report',
      ownerReport: { reportKey: 'bank.margin_statement', reportName: 'Bank margin' },
      isRemovable: false,
      // Mirrors the report's Rows tree (lib/report/mockData.ts); formula rows are not members.
      versions: [
        {
          n: 1,
          status: 'published',
          created: '2025-02-01',
          published: '2025-02-01',
          members: [
            ['TOTAL', 'Total', null, { folder: true }],
            ['ASSETS', 'Assets', 'TOTAL', { folder: true }],
            ['CASH', 'Cash and equivalents', 'ASSETS'],
            ['LOANS', 'Loans', 'ASSETS', { folder: true }],
            ['MORTGAGE', 'Mortgage loans', 'LOANS'],
            ['AUTO', 'Auto loans', 'LOANS'],
            ['COMMERCIAL', 'Commercial loans', 'LOANS'],
            ['FUNDING', 'Funding', 'TOTAL', { folder: true }],
            ['DEPOSITS', 'Deposits', 'FUNDING'],
            ['CAPITAL', 'Capital', 'FUNDING'],
          ],
        },
      ],
    },
    {
      key: 'DepositPricingStatement',
      displayName: 'Deposit pricing · rows',
      kind: 'hierarchical',
      origin: 'user',
      purpose: 'report',
      ownerReport: { reportKey: 'bank.deposit_pricing', reportName: 'Deposit pricing' },
      isRemovable: false,
      versions: [
        {
          n: 1,
          status: 'published',
          created: '2025-03-10',
          published: '2025-03-10',
          members: [
            ['DP_TOTAL', 'Total deposits', null, { folder: true }],
            ['DP_RETAIL', 'Retail deposits', 'DP_TOTAL', { folder: true }],
            ['DP_CHECKING', 'Checking accounts', 'DP_RETAIL'],
            ['DP_SAVINGS', 'Savings accounts', 'DP_RETAIL'],
            ['DP_MONEY_MARKET', 'Money market accounts', 'DP_RETAIL'],
            ['DP_TIME', 'Time deposits', 'DP_TOTAL', { folder: true }],
            ['DP_CD_SHORT', 'CDs under 12 months', 'DP_TIME'],
            ['DP_CD_LONG', 'CDs 12 months and over', 'DP_TIME'],
            ['DP_WHOLESALE', 'Wholesale funding', 'DP_TOTAL', { folder: true }],
            ['DP_BROKERED', 'Brokered deposits', 'DP_WHOLESALE'],
          ],
        },
      ],
    },
    {
      key: 'BranchProfitabilityStatement',
      displayName: 'Branch profitability · rows',
      kind: 'hierarchical',
      origin: 'user',
      purpose: 'report',
      ownerReport: { reportKey: 'bank.branch_profitability', reportName: 'Branch profitability' },
      isRemovable: false,
      versions: [
        {
          n: 1,
          status: 'draft',
          created: '2026-09-18',
          members: [
            ['BP_CONTRIBUTION', 'Branch contribution', null, { folder: true }],
            ['BP_REVENUE', 'Revenue', 'BP_CONTRIBUTION', { folder: true }],
            ['BP_LENDING', 'Lending income', 'BP_REVENUE', { folder: true }],
            ['MORTGAGE', 'Mortgage loans', 'BP_LENDING'],
            ['AUTO', 'Auto loans', 'BP_LENDING'],
            ['COMMERCIAL', 'Commercial loans', 'BP_LENDING'],
            ['BP_DEPOSIT_SPREAD', 'Deposit spread', 'BP_REVENUE'],
            ['BP_FEES', 'Fee income', 'BP_REVENUE'],
            ['BP_COSTS', 'Operating costs', 'BP_CONTRIBUTION', { folder: true }],
            ['BP_STAFF', 'Staff costs', 'BP_COSTS'],
            ['BP_OCCUPANCY', 'Occupancy', 'BP_COSTS'],
          ],
        },
      ],
    },
  ])
}

function accountMembers(): MemberSpec[] {
  const folder = (key: string, name: string, parent: string | null, level: string): MemberSpec => [key, name, parent, { level, folder: true }]
  const leaf = (key: string, name: string, parent: string): MemberSpec => [key, name, parent, { level: 'account' }]
  return [
    folder('ASSETS', 'Assets', null, 'class'),
    folder('CASH', 'Cash', 'ASSETS', 'group'),
    leaf('CASH_1010', 'Cash on hand', 'CASH'),
    leaf('CASH_1020', 'Due from banks', 'CASH'),
    folder('LOANS', 'Loans', 'ASSETS', 'group'),
    leaf('LOAN_1100', 'Residential mortgages', 'LOANS'),
    leaf('LOAN_1110', 'Auto loans', 'LOANS'),
    leaf('LOAN_1120', 'Commercial loans', 'LOANS'),
    leaf('SEC_1200', 'Investment securities', 'ASSETS'),
    leaf('PREM_1300', 'Premises and equipment', 'ASSETS'),
    folder('LIABILITIES', 'Liabilities', null, 'class'),
    folder('DEPOSITS', 'Deposits', 'LIABILITIES', 'group'),
    leaf('DEP_2000', 'Demand deposits', 'DEPOSITS'),
    leaf('DEP_2010', 'Savings deposits', 'DEPOSITS'),
    leaf('DEP_2020', 'Time deposits', 'DEPOSITS'),
    leaf('BOR_2100', 'Borrowings', 'LIABILITIES'),
    folder('EQUITY', 'Equity', null, 'class'),
    leaf('CAP_3000', 'Share capital', 'EQUITY'),
    leaf('RE_3100', 'Retained earnings', 'EQUITY'),
    folder('REVENUE', 'Revenue', null, 'class'),
    leaf('INT_4000', 'Loan interest income', 'REVENUE'),
    leaf('INT_4010', 'Securities interest income', 'REVENUE'),
    leaf('FEE_4100', 'Fee income', 'REVENUE'),
    folder('EXPENSE', 'Expense', null, 'class'),
    leaf('INT_5000', 'Deposit interest expense', 'EXPENSE'),
    leaf('INT_5010', 'Borrowing interest expense', 'EXPENSE'),
    leaf('OPX_5100', 'Salaries and benefits', 'EXPENSE'),
    leaf('OPX_5200', 'Occupancy', 'EXPENSE'),
  ]
}

function riskGrades(): MemberSpec[] {
  return [
    ['RG1', 'Prime'],
    ['RG2', 'Strong'],
    ['RG3', 'Satisfactory'],
    ['RG4', 'Fair'],
    ['RG5', 'Watch'],
    ['RG6', 'Substandard'],
    ['RG7', 'Doubtful'],
    ['RG8', 'Loss'],
  ]
}

function balanceMembers(): MemberSpec[] {
  return [
    ['B_UNDER_10K', 'Under 10k'],
    ['B_10K_50K', '10k – 50k'],
    ['B_50K_100K', '50k – 100k'],
    ['B_100K_250K', '100k – 250k'],
    ['B_250K_1M', '250k – 1m'],
    ['B_OVER_1M', '1m and over'],
  ]
}

function balanceBands(fourthLower: number): VersionSpec['bands'] {
  return [
    ['B_UNDER_10K', null, 10000],
    ['B_10K_50K', 10000, 50000],
    ['B_50K_100K', 50000, 100000],
    ['B_100K_250K', fourthLower, 250000],
    ['B_250K_1M', 250000, 1000000],
    ['B_OVER_1M', 1000000, null],
  ]
}

function territoryMembers(): MemberSpec[] {
  const poly = (x: number, y: number) => ({ type: 'Polygon', coordinates: [[[x, y], [x + 1, y], [x + 1, y + 1], [x, y + 1], [x, y]]] })
  return [
    ['T_NORTH', 'North territory', null, { level: 'region', folder: true, props: { color: '#2f6fb3', boundaryGeoJson: poly(-88, 41) } }],
    ['T_N_METRO', 'Metro', 'T_NORTH', { level: 'district', folder: true, props: { color: '#4c8bd0' } }],
    ['T_BR01', 'Downtown', 'T_N_METRO', { level: 'branch' }],
    ['T_BR02', 'Riverside', 'T_N_METRO', { level: 'branch' }],
    ['T_BR05', 'Lakeview', 'T_NORTH', { level: 'branch', description: 'Sits directly under the territory with no district.' }],
    ['T_SOUTH', 'South territory', null, { level: 'region', folder: true, props: { color: '#b3702f', boundaryGeoJson: poly(-91, 29) } }],
    ['T_S_COAST', 'Coast', 'T_SOUTH', { level: 'district', folder: true }],
    ['T_BR08', 'Bayfront', 'T_S_COAST', { level: 'branch' }],
    ['T_BR09', 'Gulf Point', 'T_SOUTH', { level: 'branch' }],
  ]
}
