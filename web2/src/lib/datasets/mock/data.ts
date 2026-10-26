import type {
  Dataset,
  DatasetBinding,
  DatasetField,
  DatasetMeasure,
  DatasetVersionSettings,
  DatasetGrain,
  SourceColumn,
  SourceObject,
  TenantCatalog,
  TimeBindingExtension,
  TimePolicy,
  Vocabularies,
  VersionStatus,
  DataType,
} from '../contract'

/* ───────────────────────── Vocabularies ───────────────────────── */

const t = <V extends string>(value: V, label: string, description?: string) => ({ value, label, description })

export const vocabularies: Vocabularies = {
  versionStatus: [
    t('draft', 'Draft', 'Editable. The engine does not read it.'),
    t('validated', 'Validated', 'Passed publication rules; ready to publish.'),
    t('published', 'Published', 'Immutable. A release can pin it.'),
    t('retired', 'Retired', 'No longer offered to releases.'),
  ],
  fieldSourceKind: [
    t('column', 'Column', 'Reads a physical column of the source object.'),
    t('json_path', 'JSON path', 'Reads a property inside a JSON column.'),
    t('expression', 'Expression', 'A row expression over other fields.'),
  ],
  dataType: [
    t('boolean', 'boolean'),
    t('int32', 'int32'),
    t('int64', 'int64'),
    t('decimal', 'decimal'),
    t('string', 'string'),
    t('date', 'date'),
    t('datetime', 'datetime'),
    t('timestamp', 'timestamp'),
    t('json', 'json'),
  ],
  grainKind: [
    t('row', 'Row', 'Identifies one logical detail row.'),
    t('series', 'Series', 'Identifies one trajectory over time, excluding observation time.'),
  ],
  bindingKind: [
    t('direct', 'Direct', 'The field value already is the member key.'),
    t('mapped', 'Mapped', 'Translates the value through a mapping set.'),
    t('range', 'Range', 'Places a scalar value into range bands.'),
    t('bridge', 'Bridge', "Remaps another binding's members through a bridge."),
    t('time', 'Time', 'Makes the field a time axis.'),
  ],
  raggedBehavior: [
    t('self', 'Self', 'Group under the member itself.'),
    t('exclude', 'Exclude', 'Leave the row out.'),
  ],
  periodAlignment: [
    t('observation', 'Observation', 'Resolve attributes at the observation date.'),
    t('start', 'Period start', 'Resolve attributes at the start of the period.'),
    t('end', 'Period end', 'Resolve attributes at the end of the period.'),
  ],
  unmappedBehavior: [
    t('exclude', 'Exclude', 'Drop rows whose value has no member.'),
    t('unknown', 'Unknown member', 'Group them under a named member.'),
  ],
  timeValueType: [t('date', 'date'), t('timestamp', 'timestamp')],
  observationKind: [
    t('instant', 'Instant', 'An event at one point in time.'),
    t('snapshot', 'Snapshot', 'A state observed as of a date.'),
    t('interval', 'Interval', 'Covers a span between two times.'),
  ],
  periodicity: [t('day', 'Day'), t('month', 'Month'), t('quarter', 'Quarter'), t('year', 'Year')],
  cumulativeOrigin: [
    t('output_range_start', 'Output range start', 'Cumulate from the first period the query returns.'),
    t('fixed_start', 'Fixed start', 'Cumulate from a fixed date.'),
  ],
  measureKind: [
    t('aggregate', 'Aggregate', 'One aggregation over a value field.'),
    t('weighted_sum', 'Weighted sum', 'Sum of value × weight.'),
    t('weighted_average', 'Weighted average', 'Sum of value × weight over sum of weight.'),
    t('period_average', 'Period average', 'Average of a value across the days of a period.'),
    t('formula', 'Formula', 'Composes other measures of this version.'),
    t('specialized', 'Specialized', 'A registered implementation with its own configuration.'),
  ],
  aggregationFunction: [
    t('sum', 'sum'),
    t('avg', 'avg'),
    t('min', 'min'),
    t('max', 'max'),
    t('count', 'count'),
    t('count_distinct', 'count distinct'),
  ],
  componentRole: [
    t('value', 'Value'),
    t('weight', 'Weight'),
    t('period_days', 'Period days'),
    t('numerator', 'Numerator'),
    t('denominator', 'Denominator'),
  ],
  componentRolesByKind: {
    aggregate: [{ role: 'value', required: true }],
    weighted_sum: [
      { role: 'value', required: true },
      { role: 'weight', required: true },
    ],
    weighted_average: [
      { role: 'value', required: true },
      { role: 'weight', required: true },
    ],
    period_average: [
      { role: 'value', required: true },
      { role: 'period_days', required: false },
    ],
    formula: [],
    specialized: [
      { role: 'value', required: false },
      { role: 'weight', required: false },
      { role: 'period_days', required: false },
      { role: 'numerator', required: false },
      { role: 'denominator', required: false },
    ],
  },
  valueNullBehavior: [t('exclude', 'Exclude'), t('zero', 'Treat as zero')],
  negativeWeightBehavior: [t('allow', 'Allow'), t('exclude', 'Exclude row')],
  emptySetBehavior: [t('null', 'Null'), t('zero', 'Zero')],
  bridgeFactorBehavior: [
    t('ignore', 'Ignore', 'The bridge factor does not change the contribution.'),
    t('value', 'Multiply value', "The bridge's signed factor multiplies the contribution."),
  ],
  bridgeAllocationBehavior: [t('apply', 'Apply'), t('ignore', 'Ignore')],
  totalBehavior: [
    t('recompute', 'Recompute', 'Totals recompute from rows. Use for ratios, weighted and nonlinear measures.'),
    t('sum_children', 'Sum children', 'Totals add the child values. Additive measures only.'),
  ],
  roundingMode: [t('half_up', 'Half up'), t('half_even', 'Half even'), t('down', 'Down'), t('up', 'Up')],
  observationSelection: [
    t('all', 'All observations'),
    t('first_in_period', 'First in period'),
    t('last_in_period', 'Last in period'),
    t('at_start', 'At period start'),
    t('at_end', 'At period end'),
  ],
  valueTimeRole: [t('point', 'Point'), t('interval_open', 'Interval open'), t('interval_close', 'Interval close')],
  boundaryMatch: [t('exact', 'Exact'), t('on_or_before', 'On or before'), t('strictly_before', 'Strictly before')],
  durationUnit: [t('calendar_day', 'Calendar day'), t('elapsed_second', 'Elapsed second')],
  partialIntervalBehavior: [
    t('exact_only', 'Exact only', 'Only whole native intervals contribute.'),
    t('uniform_estimate', 'Uniform estimate', 'Spread an interval evenly across finer periods.'),
  ],
  missingPeriodBehavior: [t('null', 'Null'), t('zero', 'Zero')],
  sourceObjectKind: [t('table', 'Table'), t('view', 'View')],
  expressionLanguageVersion: [t('1.0', '1.0', 'Current grammar')],
  timeZone: ['UTC', 'America/New_York', 'America/Chicago', 'America/Los_Angeles', 'Europe/London', 'Europe/Frankfurt', 'Asia/Singapore', 'Asia/Tokyo'].map(
    (z) => t(z, z),
  ),
}

/* ───────────────────────── Tenant catalog ───────────────────────── */

const members = (prefix: string, labels: string[]) => labels.map((label, i) => ({ key: `${prefix}${String(i + 1).padStart(2, '0')}`, label }))

export const catalog: TenantCatalog = {
  dimensions: [
    { id: 'dim_branch', dimensionKey: 'branch', displayName: 'Branch', dimensionKind: 'standard', publishedMembers: [...members('BR', ['Downtown', 'Northside', 'Harbor', 'Westfield', 'Airport']), { key: 'BR_UNK', label: 'Unknown branch' }] },
    { id: 'dim_region', dimensionKey: 'region', displayName: 'Region', dimensionKind: 'standard', publishedMembers: [...members('RG', ['North', 'South', 'Central']), { key: 'RG_UNK', label: 'Unassigned' }] },
    { id: 'dim_product', dimensionKey: 'product', displayName: 'Product', dimensionKind: 'standard', publishedMembers: [...members('PR', ['Mortgage', 'Auto loan', 'Personal loan', 'Credit card', 'Checking', 'Savings', 'Term deposit']), { key: 'PR_OTHER', label: 'Other product' }] },
    { id: 'dim_account', dimensionKey: 'gl_account', displayName: 'GL account', dimensionKind: 'standard', publishedMembers: [...members('GL', ['Cash', 'Loans', 'Deposits', 'Equity', 'Interest income', 'Interest expense']), { key: 'GL_SUSPENSE', label: 'Suspense' }] },
    { id: 'dim_cost_center', dimensionKey: 'cost_center', displayName: 'Cost center', dimensionKind: 'standard', publishedMembers: members('CC', ['Retail', 'Commercial', 'Treasury', 'Operations']) },
    { id: 'dim_dpd_bucket', dimensionKey: 'dpd_bucket', displayName: 'Days past due band', dimensionKind: 'range', publishedMembers: [{ key: 'CURRENT', label: 'Current' }, { key: 'DPD_1_30', label: '1–30' }, { key: 'DPD_31_90', label: '31–90' }, { key: 'DPD_90P', label: '90+' }] },
    { id: 'dim_balance_band', dimensionKey: 'balance_band', displayName: 'Balance band', dimensionKind: 'range', publishedMembers: [{ key: 'LT_10K', label: '< 10k' }, { key: '10K_100K', label: '10k–100k' }, { key: 'GT_100K', label: '> 100k' }] },
    { id: 'dim_business_date', dimensionKey: 'business_date', displayName: 'Business date', dimensionKind: 'time', publishedMembers: [] },
    { id: 'dim_maturity_date', dimensionKey: 'maturity_date', displayName: 'Maturity date', dimensionKind: 'time', publishedMembers: [] },
  ],
  roles: [
    { id: 'role_servicing_branch', roleKey: 'servicingBranch', displayName: 'Servicing branch', dimensionId: 'dim_branch' },
    { id: 'role_origination_branch', roleKey: 'originationBranch', displayName: 'Origination branch', dimensionId: 'dim_branch' },
    { id: 'role_branch', roleKey: 'branch', displayName: 'Branch', dimensionId: 'dim_branch' },
    { id: 'role_region', roleKey: 'region', displayName: 'Region', dimensionId: 'dim_region' },
    { id: 'role_product', roleKey: 'product', displayName: 'Product', dimensionId: 'dim_product' },
    { id: 'role_account', roleKey: 'account', displayName: 'GL account', dimensionId: 'dim_account' },
    { id: 'role_cost_center', roleKey: 'costCenter', displayName: 'Cost center', dimensionId: 'dim_cost_center' },
    { id: 'role_dpd_band', roleKey: 'dpdBand', displayName: 'Days past due band', dimensionId: 'dim_dpd_bucket' },
    { id: 'role_balance_band', roleKey: 'balanceBand', displayName: 'Balance band', dimensionId: 'dim_balance_band' },
  ],
  mappingSets: [
    { id: 'ms_branch_codes', mappingSetKey: 'branch_codes', displayName: 'Core branch codes', dimensionId: 'dim_branch' },
    { id: 'ms_legacy_branch', mappingSetKey: 'legacy_branch_codes', displayName: 'Legacy branch codes', dimensionId: 'dim_branch' },
    { id: 'ms_product_codes', mappingSetKey: 'product_codes', displayName: 'Product codes', dimensionId: 'dim_product' },
    { id: 'ms_legacy_accounts', mappingSetKey: 'legacy_gl', displayName: 'Legacy GL accounts', dimensionId: 'dim_account' },
  ],
  bridges: [
    { id: 'br_branch_region', bridgeKey: 'branch_to_region', displayName: 'Branch → region', fromDimensionId: 'dim_branch', toDimensionId: 'dim_region' },
    { id: 'br_account_cc', bridgeKey: 'account_to_cost_center', displayName: 'GL account → cost center', fromDimensionId: 'dim_account', toDimensionId: 'dim_cost_center' },
  ],
  releases: [
    { id: 'rel_2026_10', releaseKey: '2026.10', displayName: 'October 2026', isActive: true, pins: [] },
    { id: 'rel_2026_09', releaseKey: '2026.09', displayName: 'September 2026', isActive: false, pins: [] },
  ],
}

/* ───────────────────────── Source objects ───────────────────────── */

const c = (name: string, nativeType: string, mappedType: DataType | null, opts: Partial<SourceColumn> = {}): SourceColumn => ({
  name,
  nativeType,
  mappedType,
  numericPrecision: null,
  numericScale: null,
  isNullable: true,
  ...opts,
})
const dec = (name: string, p: number, s: number, opts: Partial<SourceColumn> = {}) =>
  c(name, `numeric(${p},${s})`, 'decimal', { numericPrecision: p, numericScale: s, ...opts })

export const sourceObjects: SourceObject[] = [
  { name: 'fact_loan_daily', kind: 'table', stats: { rowCount: 48_213_904, sizeBytes: 13_314_398_618, measuredAt: '2026-10-26T04:10:00Z' } },
  { name: 'vw_gl_balance_daily', kind: 'view', stats: { rowCount: 9_842_117, sizeBytes: 0, measuredAt: '2026-10-26T04:10:00Z' } },
  { name: 'fact_deposit_daily', kind: 'table', stats: { rowCount: 112_506_330, sizeBytes: 41_875_931_136, measuredAt: '2026-10-26T04:12:00Z' } },
  { name: 'vw_loan_snapshot', kind: 'view', stats: null },
]

export const sourceColumns: Record<string, SourceColumn[]> = {
  fact_loan_daily: [
    c('loan_id', 'varchar(32)', 'string', { isNullable: false }),
    c('as_of_date', 'date', 'date', { isNullable: false }),
    c('branch_code', 'varchar(8)', 'string'),
    c('origination_branch_code', 'varchar(8)', 'string'),
    c('product_code', 'varchar(12)', 'string'),
    dec('balance', 18, 2, { isNullable: false }),
    dec('interest_rate', 9, 6),
    dec('interest_accrued', 18, 2),
    c('days_past_due', 'integer', 'int32'),
    c('status', 'varchar(16)', 'string'),
    c('maturity_date', 'date', 'date'),
    c('attributes', 'jsonb', 'json'),
    c('updated_at', 'timestamptz', 'timestamp'),
    c('collateral_geom', 'geometry(Point,4326)', null),
    c('scan_blob', 'bytea', null),
    c('origination_channel', 'varchar(16)', 'string'),
    dec('original_amount', 18, 2),
  ],
  vw_gl_balance_daily: [
    c('as_of_date', 'date', 'date', { isNullable: false }),
    c('branch_key', 'varchar(8)', 'string', { isNullable: false }),
    c('account_key', 'varchar(16)', 'string', { isNullable: false }),
    c('cost_center', 'varchar(8)', 'string', { isNullable: false }),
    dec('amount', 20, 2, { isNullable: false }),
    c('currency', 'char(3)', 'string'),
    c('posting_count', 'bigint', 'int64'),
  ],
  fact_deposit_daily: [
    c('account_id', 'varchar(32)', 'string', { isNullable: false }),
    c('as_of_date', 'date', 'date', { isNullable: false }),
    c('branch_code', 'varchar(8)', 'string'),
    c('product_code', 'varchar(12)', 'string'),
    dec('balance', 18, 2),
    dec('rate', 9, 6),
    c('is_dormant', 'boolean', 'boolean'),
    c('meta', 'jsonb', 'json'),
  ],
  vw_loan_snapshot: [
    c('loan_id', 'varchar(32)', 'string', { isNullable: false }),
    c('snapshot_date', 'date', 'date'),
    dec('balance', 18, 2),
    c('branch_code', 'varchar(8)', 'string'),
  ],
}

/* ───────────────────────── Builders ───────────────────────── */

type FieldOpts = Partial<Omit<DatasetField, 'id' | 'fieldKey' | 'displayName'>>

function field(id: string, fieldKey: string, displayName: string, opts: FieldOpts): DatasetField {
  return {
    id,
    fieldKey,
    displayName,
    fieldSourceKind: 'column',
    sourceColumn: null,
    jsonPath: null,
    expressionText: null,
    materialization: null,
    dataType: 'string',
    numericPrecision: null,
    numericScale: null,
    isNullable: true,
    unitKey: null,
    isSelectable: true,
    isFilterable: true,
    isSortable: true,
    isGroupable: false,
    isEnabled: true,
    ...opts,
  }
}

export function emptyBinding(): Omit<DatasetBinding, 'id'> {
  return {
    bindingKind: 'direct',
    dimensionRoleId: null,
    dimensionId: null,
    sourceFieldId: null,
    mappingSetId: null,
    bridgeId: null,
    sourceDimensionBindingId: null,
    raggedBehavior: null,
    defaultPeriodAlignment: 'observation',
    unmappedBehavior: null,
    unknownMemberKey: null,
    isRequired: false,
    isGroupable: true,
    isFilterable: true,
    isEnabled: true,
    time: null,
  }
}

export function emptyTimeExtension(): TimeBindingExtension {
  return {
    timeValueType: 'date',
    sourceTimeZone: null,
    defaultQueryTimeZone: 'UTC',
    observationKind: 'snapshot',
    nativePeriodicity: 'day',
    intervalEndFieldId: null,
    allowedPeriodicities: ['day', 'month', 'quarter', 'year'],
    cumulativeOrigin: 'output_range_start',
    cumulativeFixedStart: null,
  }
}

export function emptyMeasure(): Omit<DatasetMeasure, 'id'> {
  return {
    measureKey: '',
    displayName: '',
    isEnabled: true,
    measureKind: 'aggregate',
    aggregationFunction: 'sum',
    expressionText: null,
    implementationKey: null,
    implementationVersion: null,
    configurationJson: null,
    components: [],
    rowFilterExpression: null,
    valueNullBehavior: 'exclude',
    negativeWeightBehavior: 'allow',
    emptySetBehavior: 'null',
    bridgeFactorBehavior: 'ignore',
    bridgeAllocationBehavior: 'apply',
    totalBehavior: 'recompute',
    outputDataType: 'decimal',
    outputPrecision: 18,
    outputScale: 2,
    roundingMode: 'half_even',
    unitKey: null,
    accumulatorPrecision: 38,
    accumulatorScale: 12,
    timePolicy: null,
  }
}

export function defaultTimePolicy(): TimePolicy {
  return {
    requiredTimeBindingId: null,
    selectionDatasetGrainId: null,
    observationSelection: 'all',
    valueTimeRole: 'point',
    boundaryMatch: 'exact',
    maxLookbackSeconds: null,
    durationUnit: 'calendar_day',
    partialIntervalBehavior: 'exact_only',
    missingPeriodBehavior: 'null',
  }
}

const binding = (id: string, opts: Partial<DatasetBinding>): DatasetBinding => ({ id, ...emptyBinding(), ...opts })
const measure = (id: string, measureKey: string, displayName: string, opts: Partial<DatasetMeasure>): DatasetMeasure => ({
  id,
  ...emptyMeasure(),
  measureKey,
  displayName,
  ...opts,
})

/* ───────────────────────── Seed datasets ───────────────────────── */

export interface StoredVersion {
  id: string
  datasetId: string
  versionNumber: number
  status: VersionStatus
  createdAt: string
  publishedAt: string | null
  clonedFromVersionId: string | null
  settings: DatasetVersionSettings
  fields: DatasetField[]
  grains: DatasetGrain[]
  bindings: DatasetBinding[]
  measures: DatasetMeasure[]
}

function loanVersionBody(withDraftChanges: boolean): Omit<StoredVersion, 'id' | 'datasetId' | 'versionNumber' | 'status' | 'createdAt' | 'publishedAt' | 'clonedFromVersionId'> {
  const fields: DatasetField[] = [
    field('f_loan_id', 'loanId', 'Loan ID', { sourceColumn: 'loan_id', isNullable: false, isGroupable: true }),
    field('f_as_of', 'asOfDate', 'As-of date', { sourceColumn: 'as_of_date', dataType: 'date', isNullable: false, isGroupable: true }),
    field('f_branch', 'branchCode', 'Branch code', { sourceColumn: 'branch_code', isGroupable: true }),
    field('f_orig_branch', 'originationBranchCode', 'Origination branch code', { sourceColumn: 'origination_branch_code', isGroupable: true }),
    field('f_product', 'productCode', 'Product code', { sourceColumn: 'product_code', isGroupable: true }),
    field('f_balance', 'balance', 'Balance', { sourceColumn: 'balance', dataType: 'decimal', numericPrecision: 18, numericScale: 2, isNullable: false, unitKey: 'USD' }),
    field('f_rate', 'interestRate', 'Interest rate', { sourceColumn: 'interest_rate', dataType: 'decimal', numericPrecision: 9, numericScale: 6, unitKey: 'percent' }),
    field('f_accrued', 'interestAccrued', 'Interest accrued', { sourceColumn: 'interest_accrued', dataType: 'decimal', numericPrecision: 18, numericScale: 2, unitKey: 'USD' }),
    field('f_dpd', 'daysPastDue', 'Days past due', { sourceColumn: 'days_past_due', dataType: 'int32', unitKey: 'days' }),
    field('f_status', 'status', 'Status', { sourceColumn: 'status', isGroupable: true }),
    field('f_maturity', 'maturityDate', 'Maturity date', { sourceColumn: 'maturity_date', dataType: 'date' }),
    field('f_risk_grade', 'riskGrade', 'Risk grade', {
      fieldSourceKind: 'json_path',
      sourceColumn: 'attributes',
      jsonPath: '$.risk.grade',
      isGroupable: true,
      materialization: { viewName: 'mv_fact_loan_daily', columnName: 'attributes_risk_grade', state: 'ready', requestedAt: '2026-08-20T13:00:00Z', refreshedAt: '2026-10-25T05:00:00Z', error: null },
    }),
    field('f_ltv', 'loanToValue', 'Loan to value', { fieldSourceKind: 'json_path', sourceColumn: 'attributes', jsonPath: '$.collateral.ltv', dataType: 'decimal', numericPrecision: 9, numericScale: 4, unitKey: 'percent', isSortable: false }),
    field('f_rate_x_bal', 'rateTimesBalance', 'Rate × balance', { fieldSourceKind: 'expression', expressionText: '[f.interestRate] * [f.balance]', dataType: 'decimal', numericPrecision: 28, numericScale: 8, isSelectable: false, isSortable: false }),
  ]
  if (withDraftChanges) {
    fields.push(
      field('f_channel', 'originationChannel', 'Origination channel', { sourceColumn: 'origination_channel', isGroupable: true }),
      field('f_is_open', 'isOpen', 'Is open', { fieldSourceKind: 'expression', expressionText: "[f.status] = 'open'", dataType: 'boolean', isSortable: false }),
    )
  }

  const grains: DatasetGrain[] = [
    { id: 'g_row', grainKey: 'rowIdentity', grainKind: 'row', fieldIds: ['f_loan_id', 'f_as_of'] },
    { id: 'g_series', grainKey: 'temporalSeries', grainKind: 'series', fieldIds: withDraftChanges ? ['f_loan_id', 'f_as_of'] : ['f_loan_id'] },
  ]

  const bindings: DatasetBinding[] = [
    binding('b_time', {
      bindingKind: 'time',
      dimensionId: 'dim_business_date',
      sourceFieldId: 'f_as_of',
      isRequired: true,
      time: emptyTimeExtension(),
    }),
    binding('b_servicing', { bindingKind: 'mapped', dimensionRoleId: 'role_servicing_branch', sourceFieldId: 'f_branch', mappingSetId: 'ms_branch_codes', unmappedBehavior: 'unknown', unknownMemberKey: 'BR_UNK' }),
    binding('b_origination', { bindingKind: 'mapped', dimensionRoleId: 'role_origination_branch', sourceFieldId: 'f_orig_branch', mappingSetId: 'ms_branch_codes', unmappedBehavior: 'exclude', defaultPeriodAlignment: 'start' }),
    binding('b_region', { bindingKind: 'bridge', dimensionRoleId: 'role_region', sourceDimensionBindingId: 'b_servicing', bridgeId: 'br_branch_region', unmappedBehavior: 'unknown', unknownMemberKey: 'RG_UNK' }),
    binding('b_product', { bindingKind: 'mapped', dimensionRoleId: 'role_product', sourceFieldId: 'f_product', mappingSetId: 'ms_product_codes', unmappedBehavior: 'unknown', unknownMemberKey: 'PR_OTHER' }),
    binding('b_dpd', { bindingKind: 'range', dimensionRoleId: 'role_dpd_band', sourceFieldId: 'f_dpd', isGroupable: false }),
  ]

  const endingBalancePolicy: TimePolicy = {
    ...defaultTimePolicy(),
    requiredTimeBindingId: 'b_time',
    selectionDatasetGrainId: 'g_series',
    observationSelection: 'at_end',
    boundaryMatch: 'on_or_before',
    maxLookbackSeconds: 5 * 86400,
  }

  const measures: DatasetMeasure[] = [
    measure('m_end_bal', 'endingBalance', 'Ending balance', { components: [{ role: 'value', fieldId: 'f_balance' }], unitKey: 'USD', totalBehavior: 'sum_children', timePolicy: endingBalancePolicy }),
    measure('m_avg_bal', 'averageBalance', 'Average balance', {
      measureKind: 'period_average',
      aggregationFunction: null,
      components: [{ role: 'value', fieldId: 'f_balance' }],
      unitKey: 'USD',
      timePolicy: { ...defaultTimePolicy(), requiredTimeBindingId: 'b_time' },
    }),
    measure('m_wavg_rate', 'weightedRate', 'Weighted average rate', {
      measureKind: 'weighted_average',
      aggregationFunction: null,
      components: [
        { role: 'value', fieldId: 'f_rate' },
        { role: 'weight', fieldId: 'f_balance' },
      ],
      negativeWeightBehavior: 'exclude',
      outputPrecision: 9,
      outputScale: 6,
      unitKey: 'percent',
    }),
    measure('m_interest', 'interestIncome', 'Interest income', { components: [{ role: 'value', fieldId: 'f_accrued' }], valueNullBehavior: 'zero', emptySetBehavior: 'zero', totalBehavior: 'sum_children', unitKey: 'USD' }),
    measure('m_loan_count', 'loanCount', 'Loan count', {
      aggregationFunction: 'count_distinct',
      components: [{ role: 'value', fieldId: 'f_loan_id' }],
      outputDataType: 'int64',
      outputPrecision: null,
      outputScale: null,
      emptySetBehavior: 'zero',
    }),
    measure('m_npl_bal', 'nonPerformingBalance', 'Non-performing balance', {
      components: [{ role: 'value', fieldId: 'f_balance' }],
      rowFilterExpression: '[f.daysPastDue] > 90',
      unitKey: 'USD',
      totalBehavior: 'sum_children',
      timePolicy: endingBalancePolicy,
    }),
    measure('m_yield', 'netYield', 'Net yield', { measureKind: 'formula', aggregationFunction: null, expressionText: '[m.interestIncome] / [m.averageBalance]', outputPrecision: 9, outputScale: 6, unitKey: 'percent' }),
    measure('m_npl_ratio', 'nplRatio', 'NPL ratio', { measureKind: 'formula', aggregationFunction: null, expressionText: '[m.nonPerformingBalance] / [m.endingBalance]', outputPrecision: 9, outputScale: 6, unitKey: 'percent' }),
  ]
  if (withDraftChanges) {
    measures.push(
      measure('m_open_bal', 'openBalance', 'Open balance', { components: [{ role: 'value', fieldId: 'f_balance' }], rowFilterExpression: '[f.isOpen]', unitKey: 'USD', totalBehavior: 'sum_children', timePolicy: endingBalancePolicy }),
      measure('m_coverage', 'provisionCoverage', 'Provision coverage', { measureKind: 'formula', aggregationFunction: null, expressionText: '[m.provisionBalance] / [m.nonPerformingBalance]', outputPrecision: 9, outputScale: 6, unitKey: 'percent' }),
    )
  }

  return {
    settings: {
      sourceObjectKind: 'table',
      sourceObject: 'fact_loan_daily',
      supportsAggregate: true,
      supportsDetail: true,
      defaultTimeBindingId: 'b_time',
      expressionLanguageVersion: '1.0',
      securityPolicyKey: 'loans.branch_scoped',
      properties: JSON.stringify({ owner: 'credit-analytics', refresh: 'daily 05:00 ET' }, null, 2),
    },
    fields,
    grains,
    bindings,
    measures,
  }
}

function glVersionBody(): ReturnType<typeof loanVersionBody> {
  return {
    settings: {
      sourceObjectKind: 'view',
      sourceObject: 'vw_gl_balance_daily',
      supportsAggregate: true,
      supportsDetail: false,
      defaultTimeBindingId: 'gb_time',
      expressionLanguageVersion: '1.0',
      securityPolicyKey: null,
      properties: '{}',
    },
    fields: [
      field('gf_as_of', 'asOfDate', 'As-of date', { sourceColumn: 'as_of_date', dataType: 'date', isNullable: false, isGroupable: true }),
      field('gf_branch', 'branchKey', 'Branch', { sourceColumn: 'branch_key', isNullable: false, isGroupable: true }),
      field('gf_account', 'accountKey', 'GL account', { sourceColumn: 'account_key', isNullable: false, isGroupable: true }),
      field('gf_cc', 'costCenter', 'Cost center', { sourceColumn: 'cost_center', isNullable: false, isGroupable: true }),
      field('gf_amount', 'amount', 'Amount', { sourceColumn: 'amount', dataType: 'decimal', numericPrecision: 20, numericScale: 2, isNullable: false, unitKey: 'USD' }),
    ],
    grains: [
      { id: 'gg_row', grainKey: 'rowIdentity', grainKind: 'row', fieldIds: ['gf_as_of', 'gf_branch', 'gf_account', 'gf_cc'] },
      { id: 'gg_series', grainKey: 'temporalSeries', grainKind: 'series', fieldIds: ['gf_branch', 'gf_account', 'gf_cc'] },
    ],
    bindings: [
      binding('gb_time', { bindingKind: 'time', dimensionId: 'dim_business_date', sourceFieldId: 'gf_as_of', isRequired: true, time: emptyTimeExtension() }),
      binding('gb_branch', { bindingKind: 'direct', dimensionRoleId: 'role_branch', sourceFieldId: 'gf_branch' }),
      binding('gb_account', { bindingKind: 'mapped', dimensionRoleId: 'role_account', sourceFieldId: 'gf_account', mappingSetId: 'ms_legacy_accounts', unmappedBehavior: 'unknown', unknownMemberKey: 'GL_SUSPENSE' }),
      binding('gb_cc', { bindingKind: 'direct', dimensionRoleId: 'role_cost_center', sourceFieldId: 'gf_cc' }),
    ],
    measures: [
      measure('gm_closing', 'closingBalance', 'Closing balance', {
        components: [{ role: 'value', fieldId: 'gf_amount' }],
        totalBehavior: 'sum_children',
        unitKey: 'USD',
        timePolicy: { ...defaultTimePolicy(), requiredTimeBindingId: 'gb_time', selectionDatasetGrainId: 'gg_series', observationSelection: 'at_end', boundaryMatch: 'on_or_before', maxLookbackSeconds: 3 * 86400 },
      }),
      measure('gm_avg', 'averageBalance', 'Average balance', { measureKind: 'period_average', aggregationFunction: null, components: [{ role: 'value', fieldId: 'gf_amount' }], unitKey: 'USD', timePolicy: { ...defaultTimePolicy(), requiredTimeBindingId: 'gb_time' } }),
    ],
  }
}

/** Re-ids every child so clones never share identities with their source. */
export function reId<T extends ReturnType<typeof loanVersionBody>>(body: T, suffix: string): T {
  const map = new Map<string, string>()
  const id = (old: string) => {
    if (!map.has(old)) map.set(old, `${old}__${suffix}`)
    return map.get(old)!
  }
  ;[...body.fields, ...body.grains, ...body.bindings, ...body.measures].forEach((x) => id(x.id))
  const ref = (v: string | null) => (v && map.has(v) ? map.get(v)! : v)
  return {
    ...body,
    settings: { ...body.settings, defaultTimeBindingId: ref(body.settings.defaultTimeBindingId) },
    fields: body.fields.map((f) => ({ ...f, id: id(f.id) })),
    grains: body.grains.map((g) => ({ ...g, id: id(g.id), fieldIds: g.fieldIds.map((f) => ref(f)!) })),
    bindings: body.bindings.map((b) => ({
      ...b,
      id: id(b.id),
      sourceFieldId: ref(b.sourceFieldId),
      sourceDimensionBindingId: ref(b.sourceDimensionBindingId),
      time: b.time ? { ...b.time, intervalEndFieldId: ref(b.time.intervalEndFieldId) } : null,
    })),
    measures: body.measures.map((m) => ({
      ...m,
      id: id(m.id),
      components: m.components.map((cp) => ({ ...cp, fieldId: ref(cp.fieldId)! })),
      timePolicy: m.timePolicy
        ? { ...m.timePolicy, requiredTimeBindingId: ref(m.timePolicy.requiredTimeBindingId), selectionDatasetGrainId: ref(m.timePolicy.selectionDatasetGrainId) }
        : null,
    })),
  }
}

export const seedDatasets: Dataset[] = [
  { id: 'ds_loans', datasetKey: 'loans.daily', displayName: 'Daily loans', isEnabled: true },
  { id: 'ds_gl', datasetKey: 'gl.balance_daily', displayName: 'Daily balance sheet', isEnabled: true },
  { id: 'ds_deposits', datasetKey: 'deposits.daily', displayName: 'Daily deposits', isEnabled: false },
]

export const seedVersions: StoredVersion[] = [
  { id: 'v_loans_1', datasetId: 'ds_loans', versionNumber: 1, status: 'published', createdAt: '2026-07-02T14:10:00Z', publishedAt: '2026-07-09T16:00:00Z', clonedFromVersionId: null, ...reId(loanVersionBody(false), 'v1') },
  { id: 'v_loans_2', datasetId: 'ds_loans', versionNumber: 2, status: 'published', createdAt: '2026-09-01T09:30:00Z', publishedAt: '2026-09-18T15:45:00Z', clonedFromVersionId: 'v_loans_1', ...reId(loanVersionBody(false), 'v2') },
  { id: 'v_loans_3', datasetId: 'ds_loans', versionNumber: 3, status: 'draft', createdAt: '2026-10-14T11:05:00Z', publishedAt: null, clonedFromVersionId: 'v_loans_2', ...loanVersionBody(true) },
  { id: 'v_gl_1', datasetId: 'ds_gl', versionNumber: 1, status: 'published', createdAt: '2026-06-11T10:00:00Z', publishedAt: '2026-06-20T12:00:00Z', clonedFromVersionId: null, ...glVersionBody() },
  {
    id: 'v_dep_1',
    datasetId: 'ds_deposits',
    versionNumber: 1,
    status: 'draft',
    createdAt: '2026-10-20T08:15:00Z',
    publishedAt: null,
    clonedFromVersionId: null,
    settings: { sourceObjectKind: 'table', sourceObject: 'fact_deposit_daily', supportsAggregate: true, supportsDetail: true, defaultTimeBindingId: null, expressionLanguageVersion: '1.0', securityPolicyKey: null, properties: '{}' },
    fields: [
      field('df_account', 'accountId', 'Account ID', { sourceColumn: 'account_id', isNullable: false, isGroupable: true }),
      field('df_as_of', 'asOfDate', 'As-of date', { sourceColumn: 'as_of_date', dataType: 'date', isNullable: false, isGroupable: true }),
      field('df_balance', 'balance', 'Balance', { sourceColumn: 'balance', dataType: 'decimal', numericPrecision: 18, numericScale: 2, unitKey: 'USD' }),
    ],
    grains: [],
    bindings: [],
    measures: [],
  },
]

export const seedReleasePins: Record<string, { datasetId: string; versionId: string }[]> = {
  rel_2026_10: [
    { datasetId: 'ds_loans', versionId: 'v_loans_2' },
    { datasetId: 'ds_gl', versionId: 'v_gl_1' },
  ],
  rel_2026_09: [
    { datasetId: 'ds_loans', versionId: 'v_loans_1' },
    { datasetId: 'ds_gl', versionId: 'v_gl_1' },
  ],
}
