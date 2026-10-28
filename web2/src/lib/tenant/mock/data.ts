import type { PublishedMember, TenantCatalog, Vocabularies } from '../contract'

/* ───────────────────────── Vocabularies ───────────────────────── */

const t = <V extends string>(value: V, label: string, description?: string) => ({ value, label, description })
const same = <V extends string>(...values: V[]) => values.map((v) => t(v, v))

const dataTypes = same('boolean', 'int32', 'int64', 'decimal', 'string', 'date', 'datetime', 'timestamp')

export const vocabularies: Vocabularies = {
  versionStatus: [
    t('draft', 'Draft', 'Editable. The engine does not read it.'),
    t('validated', 'Validated', 'Passed publication rules; ready to publish.'),
    t('published', 'Published', 'Immutable. A release can pin it.'),
    t('retired', 'Retired', 'No longer offered to releases.'),
  ],
  assumptionSetStatus: [t('active', 'Active', 'The set queries resolve against.'), t('historical', 'Historical', 'A captured earlier state.')],
  dataType: [...dataTypes, t('json', 'json')],
  measureOutputDataType: dataTypes,
  sourceObjectKind: [t('table', 'Table'), t('view', 'View')],
  fieldSourceKind: [
    t('column', 'Column', 'Reads a physical column of the source object.'),
    t('json_path', 'JSON path', 'Reads a property inside a JSON column.'),
    t('expression', 'Expression', 'A row expression over other fields.'),
  ],
  grainKind: [
    t('row', 'Row', 'Identifies one logical detail row.'),
    t('series', 'Series', 'Identifies one trajectory over time, excluding observation time.'),
  ],
  dimensionKind: [
    t('categorical', 'Categorical', 'A flat list of members.'),
    t('hierarchical', 'Hierarchical', 'Members arranged in a tree with levels.'),
    t('range', 'Range', 'Bands that place a scalar value.'),
    t('time', 'Time', 'A calendar axis.'),
  ],
  dimensionPurpose: [t('analytical', 'Analytical'), t('report', 'Report', 'Owned by one report; reachable only from it.')],
  definitionOrigin: [t('system', 'System'), t('user', 'User')],
  rangeDataType: [t('decimal', 'Decimal'), t('date', 'Date'), t('datetime', 'Date and time')],
  editPolicy: [
    t('open', 'Open', 'Users edit members freely.'),
    t('extend', 'Extend', 'Users add members; managed members are fixed.'),
    t('locked', 'Locked', 'Read-only; managed by its profile.'),
  ],
  bindingKind: [
    t('direct', 'Direct', 'The field value already is the member key.'),
    t('mapped', 'Mapped', 'Translates the value through a mapping set.'),
    t('range', 'Range', 'Places a scalar value into range bands.'),
    t('bridge', 'Bridge', "Remaps another binding's members through a bridge."),
    t('time', 'Time', 'Makes the field a time axis.'),
  ],
  groupingMode: [t('member', 'Member'), t('level', 'Level'), t('ancestor', 'Ancestor'), t('depth', 'Depth')],
  raggedBehavior: [
    t('null', 'Null', 'Group under a null member.'),
    t('self', 'Self', 'Group under the member itself.'),
    t('exclude', 'Exclude', 'Leave the row out.'),
  ],
  periodAlignment: [
    t('observation', 'Observation', 'Resolve attributes at the observation date.'),
    t('start', 'Period start', 'Resolve attributes at the start of the period.'),
    t('end', 'Period end', 'Resolve attributes at the end of the period.'),
  ],
  unmappedBehavior: [
    t('null', 'Null', 'Keep rows whose value has no member under a null member.'),
    t('exclude', 'Exclude', 'Drop rows whose value has no member.'),
    t('unknown', 'Unknown member', 'Group them under a named member.'),
  ],
  timeValueType: same('date', 'timestamp'),
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
  aggregationFunction: [...same('sum', 'avg', 'min', 'max', 'count'), t('count_distinct', 'count distinct')],
  componentRole: [t('value', 'Value'), t('weight', 'Weight'), t('period_days', 'Period days'), t('numerator', 'Numerator'), t('denominator', 'Denominator')],
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
  roundingMode: [t('half_even', 'Half even'), t('half_up', 'Half up'), t('down', 'Down'), t('up', 'Up'), t('truncate', 'Truncate')],
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
  expressionLanguageVersion: [t('1.0', '1.0', 'Current grammar')],
  timeZone: same('UTC', 'America/New_York', 'America/Chicago', 'America/Los_Angeles', 'Europe/London', 'Europe/Frankfurt', 'Asia/Singapore', 'Asia/Tokyo'),
  cardinality: [t('many_to_one', 'Many to one'), t('many_to_many', 'Many to many')],
  allocationMode: [t('none', 'None'), t('partition', 'Partition'), t('replicate', 'Replicate')],
  sourceSelection: [t('member', 'Member'), t('descendants', 'Descendants')],
  formulaLanguageVersion: [t('1.0', '1.0'), t('1.1', '1.1', 'Adds POWER(x, y).'), t('1.2', '1.2', 'Adds parameters, text comparisons and YEARFRAC.')],
  attributionMethod: [t('shapley', 'Shapley'), t('sequential', 'Sequential')],
  parameterValueType: [t('number', 'Number'), t('text', 'Text')],
  variableMissingBehavior: [t('null', 'Null'), t('zero', 'Zero')],
  dayCountConvention: [
    t('actual_actual', 'Actual/Actual'),
    t('actual_365', 'Actual/365'),
    t('actual_360', 'Actual/360'),
    t('thirty_360_us', '30/360 US'),
    t('thirty_e_360', '30E/360'),
  ],
  displayFormat: [t('number', 'Number'), t('currency', 'Currency'), t('percent', 'Percent'), t('basis_points', 'Basis points')],
  mappingMethod: [t('bridge', 'Bridge'), t('direct', 'Direct')],
  selectionScope: [t('exact', 'Exact'), t('descendants', 'Descendants')],
  columnValueKind: [t('variable', 'Variable'), t('calculated', 'Calculated')],
  subtotalPosition: [t('above', 'Above'), t('below', 'Below')],
  styleRole: [t('header', 'Header'), t('subtotal', 'Subtotal'), t('total', 'Total'), t('detail', 'Detail'), t('formula', 'Formula')],
  fontWeight: [t('normal', 'Normal'), t('medium', 'Medium'), t('semibold', 'Semibold'), t('bold', 'Bold')],
  borderStyle: [t('none', 'None'), t('thin', 'Thin'), t('medium', 'Medium'), t('thick', 'Thick'), t('double', 'Double')],
  layout: [t('statement', 'Statement'), t('list', 'List')],
  provider: [t('starrocks', 'StarRocks')],
}

/* ───────────────────────── Catalog ───────────────────────── */

export const catalog: TenantCatalog = {
  tenant: { tenantKey: 'demo_bank', metadataSchemaVersion: 18, semanticRevision: 412, activeReleaseId: 'rel_2026_10' },
  datasets: [
    { id: 'ds_loan_daily', key: 'loan_daily', displayName: 'Loan daily balances', isEnabled: true, activeVersionId: 'dsv_loan_daily_3' },
    { id: 'ds_gl_balance', key: 'gl_balance', displayName: 'GL balances', isEnabled: true, activeVersionId: null },
  ],
  dimensions: [
    { id: 'dim_branch', key: 'branch', displayName: 'Branch', kind: 'categorical', purpose: 'analytical', origin: 'user', isEnabled: true, publishedVersionId: 'dimv_branch_2', publishedVersionNumber: 2, ownerReportId: null },
    { id: 'dim_region', key: 'region', displayName: 'Region', kind: 'hierarchical', purpose: 'analytical', origin: 'user', isEnabled: true, publishedVersionId: 'dimv_region_1', publishedVersionNumber: 1, ownerReportId: null },
    { id: 'dim_dpd_bucket', key: 'dpd_bucket', displayName: 'Days past due band', kind: 'range', purpose: 'analytical', origin: 'user', isEnabled: true, publishedVersionId: 'dimv_dpd_bucket_1', publishedVersionNumber: 1, ownerReportId: null },
    { id: 'dim_business_date', key: 'business_date', displayName: 'Business date', kind: 'time', purpose: 'analytical', origin: 'system', isEnabled: true, publishedVersionId: null, publishedVersionNumber: null, ownerReportId: null },
    { id: 'dim_rpt_income', key: 'rpt_income_statement', displayName: 'Income statement rows', kind: 'hierarchical', purpose: 'report', origin: 'user', isEnabled: true, publishedVersionId: 'dimv_rpt_income_1', publishedVersionNumber: 1, ownerReportId: 'rep_income_statement' },
  ],
  roles: [
    { id: 'role_servicing_branch', key: 'servicingBranch', displayName: 'Servicing branch', dimensionId: 'dim_branch' },
    { id: 'role_region', key: 'region', displayName: 'Region', dimensionId: 'dim_region' },
    { id: 'role_dpd_band', key: 'dpdBand', displayName: 'Days past due band', dimensionId: 'dim_dpd_bucket' },
  ],
  mappingSets: [{ id: 'ms_branch_codes', key: 'branch_codes', displayName: 'Core branch codes', dimensionId: 'dim_branch' }],
  bridges: [
    { id: 'br_branch_region', key: 'branch_to_region', displayName: 'Branch → region', sourceDimensionId: 'dim_branch', targetDimensionId: 'dim_region', ownerReportId: null },
    { id: 'br_rpt_income_branch', key: 'rpt_income_statement.branch', displayName: 'Branch → income statement rows', sourceDimensionId: 'dim_branch', targetDimensionId: 'dim_rpt_income', ownerReportId: 'rep_income_statement' },
  ],
  models: [{ id: 'model_nii', key: 'net_interest_income', displayName: 'Net interest income', isEnabled: true }],
  reports: [{ id: 'rep_income_statement', key: 'income_statement', displayName: 'Income statement', isEnabled: true, modelId: 'model_nii', reportDimensionId: 'dim_rpt_income' }],
  releases: [
    { id: 'rel_2026_10', key: '2026.10', status: 'published', isActive: true },
    { id: 'rel_2026_11', key: '2026.11', status: 'draft', isActive: false },
  ],
  assumptionSets: [{ id: 'as_current', key: 'current', displayName: 'Current', status: 'active' }],
}

/** The overview numbers the catalog cannot give: the active release's measures and the draft dataset versions. */
export const overviewRows = { activeMeasures: 14, draftDatasetVersions: 1 }

/* ───────────────────────── Published members ───────────────────────── */

const leaf = (key: string, displayName: string, parentKey: string | null = null, levelKey: string | null = null): PublishedMember => ({ key, displayName, parentKey, isAssignable: true, levelKey })

export const publishedMembers: Record<string, PublishedMember[]> = {
  dim_branch: [leaf('BR01', 'Downtown'), leaf('BR02', 'Northside'), leaf('BR03', 'Harbor'), leaf('BR_UNK', 'Unknown branch')],
  dim_region: [
    { key: 'ALL', displayName: 'All regions', parentKey: null, isAssignable: false, levelKey: 'total' },
    leaf('RG01', 'North', 'ALL', 'region'),
    leaf('RG02', 'South', 'ALL', 'region'),
  ],
  dim_dpd_bucket: [leaf('CURRENT', 'Current'), leaf('DPD_1_30', '1–30'), leaf('DPD_31_90', '31–90'), leaf('DPD_90P', '90+')],
  dim_rpt_income: [
    { key: 'NII', displayName: 'Net interest income', parentKey: null, isAssignable: false, levelKey: null },
    leaf('INTEREST_INCOME', 'Interest income', 'NII'),
    leaf('INTEREST_EXPENSE', 'Interest expense', 'NII'),
  ],
}
