import type { Snapshot, SnapshotEntry, SnapshotKind, SnapshotRestore } from '../contract'

/** Snapshot ids every module mock files its versions under, so version pickers resolve their labels from the tenant catalog. */
export const LIVE_SNAPSHOT_ID = 'snap_live'

const frozen = (key: string, displayName: string, capturedAt: string, description: string | null = null): Snapshot => ({
  id: `snap_${key}`,
  key,
  displayName,
  description,
  isLive: false,
  capturedAt,
  capturedBy: 'controller@bank.example',
  createdAt: capturedAt,
})

export const seedSnapshots: Snapshot[] = [
  {
    id: LIVE_SNAPSHOT_ID,
    key: 'live',
    displayName: 'Live',
    description: null,
    isLive: true,
    capturedAt: null,
    capturedBy: null,
    createdAt: '2023-01-02T09:00:00Z',
  },
  frozen('fy2023_close', 'FY2023 close', '2024-01-12T18:00:00Z', 'Year-end close as reported to the board.'),
  frozen('fy2024_close', 'FY2024 close', '2025-01-15T18:00:00Z', 'Year-end close as reported to the board.'),
  frozen('fy2025_close', 'FY2025 close', '2026-01-20T18:00:00Z', 'Year-end close; restored once after the branch remap was rolled back.'),
  frozen('q2_2026_close', 'Q2 2026 close', '2026-08-01T18:00:00Z'),
]

/**
 * The frozen snapshot that holds a version which was live from `liveFrom` until `liveUntil`: the latest one captured in that
 * window. Null when no capture fell inside it, so no snapshot holds the version.
 */
export function frozenSnapshotOf(liveFrom: string, liveUntil: string): string | null {
  const held = seedSnapshots.filter((s) => !s.isLive && s.capturedAt! >= liveFrom && s.capturedAt! < liveUntil)
  return held.at(-1)?.id ?? null
}

type IdentitySpec = [kind: SnapshotKind, id: string, key: string, displayName: string, createdAt: string]

const IDENTITIES: IdentitySpec[] = [
  ['dataset', 'ds_loans', 'loans.details', 'Loan details', '2023-03-01T09:00:00Z'],
  ['dataset', 'ds_gl', 'gl.balances', 'GL balances', '2023-02-01T09:00:00Z'],
  ['dataset', 'ds_deposits', 'deposits.daily', 'Deposit daily balances', '2024-05-01T09:00:00Z'],
  ['dimension', 'dim-Branch', 'Branch', 'Branch', '2023-01-09T09:00:00Z'],
  ['dimension', 'dim-Product', 'Product', 'Product', '2023-02-01T09:00:00Z'],
  ['dimension', 'dim-RiskGrade', 'RiskGrade', 'Risk grade', '2024-02-10T09:00:00Z'],
  ['dimension', 'dim-BalanceBand', 'BalanceBand', 'Balance band', '2024-03-01T09:00:00Z'],
  ['dimension', 'dim-Territory', 'Territory', 'Territory', '2025-04-01T09:00:00Z'],
  ['dimension', 'dim-MarginStatement', 'MarginStatement', 'Margin statement rows', '2023-06-01T09:00:00Z'],
  ['mapping_set', 'ms-loan.branch', 'loan.branch', 'Loan system branches', '2023-06-01T09:00:00Z'],
  ['mapping_set', 'ms-loan.product', 'loan.product', 'Loan system products', '2024-03-01T09:00:00Z'],
  ['bridge', 'bridge-branch_to_territory', 'branch_to_territory', 'Branch → territory', '2025-04-01T09:00:00Z'],
  ['bridge', 'bridge-margin.gl_monthly', 'margin.gl_monthly', 'Margin · GL monthly', '2023-06-01T09:00:00Z'],
  ['calculation_model', 'model_nii', 'bank.net_interest_income', 'Net interest income', '2023-06-01T09:00:00Z'],
  ['report', 'rep-bank.margin_statement', 'bank.margin_statement', 'Margin statement', '2023-06-01T09:00:00Z'],
]

/** What each mapping set and bridge routes under (its dimension), and the report that owns a report dimension or bridge. */
const ROUTES: Record<string, Pick<SnapshotEntry, 'dimensionId' | 'reportId'>> = {
  'dim-MarginStatement': { dimensionId: null, reportId: 'report-1' },
  'ms-loan.branch': { dimensionId: 'dim-Branch', reportId: null },
  'ms-loan.product': { dimensionId: 'dim-Product', reportId: null },
  'bridge-branch_to_territory': { dimensionId: 'dim-Branch', reportId: null },
  'bridge-margin.gl_monthly': { dimensionId: 'dim-Account', reportId: 'report-1' },
}

/** When live edited an identity's content after creating it; a snapshot captured between two edits holds the content of the first. */
const EDITS: Record<string, string[]> = {
  ds_loans: ['2024-06-01T09:00:00Z', '2026-03-01T09:00:00Z'],
  'dim-Branch': ['2025-09-01T09:00:00Z'],
  'ms-loan.branch': ['2024-02-01T09:00:00Z', '2026-05-01T09:00:00Z'],
  model_nii: ['2026-09-01T09:00:00Z'],
  'rep-bank.margin_statement': ['2025-03-01T09:00:00Z', '2026-09-15T09:00:00Z'],
}

const heldBy = (snapshot: Snapshot) => IDENTITIES.filter(([, , , , createdAt]) => snapshot.isLive || createdAt < snapshot.capturedAt!)

/** Every snapshot holds a version of each identity created before it was captured; live holds them all. */
function entriesOf(snapshot: Snapshot): SnapshotEntry[] {
  return heldBy(snapshot).map(([kind, identityId, key, displayName]) => ({
    kind,
    identityId,
    key,
    displayName,
    versionId: `${identityId}@${snapshot.key}`,
    ...(ROUTES[identityId] ?? { dimensionId: null, reportId: null }),
  }))
}

/** A tag of the content each version holds: the identity and how many edits it had seen; equal tags mean equal content. */
function contentOf(snapshot: Snapshot): Record<string, string> {
  return Object.fromEntries(
    heldBy(snapshot).map(([, identityId]) => [identityId, `${identityId}#${(EDITS[identityId] ?? []).filter((at) => snapshot.isLive || at < snapshot.capturedAt!).length}`]),
  )
}

export const seedEntries: Record<string, SnapshotEntry[]> = Object.fromEntries(seedSnapshots.map((s) => [s.id, entriesOf(s)]))

/** The content tag of every version, by snapshot and identity. */
export const seedContent: Record<string, Record<string, string>> = Object.fromEntries(seedSnapshots.map((s) => [s.id, contentOf(s)]))

export const seedRestores: SnapshotRestore[] = [{ snapshotId: 'snap_fy2025_close', restoredAt: '2026-02-03T08:30:00Z', restoredBy: 'controller@bank.example' }]
