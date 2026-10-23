import { emptyToNull } from '@/shared/text';
import type { MeasureDetail, MeasureInput, MeasureTimePolicy, MeasureTimePolicyInput, MeasureUpdateInput } from './types';

/** One row of the components editor: a role and the field that supplies it. */
export interface ComponentFormValues {
  componentRole: string;
  fieldId: number | null;
}

/** Values of the measure sheet's time policy section. */
export interface TimePolicyFormValues {
  requiredTimeBindingId: number | null;
  selectionDatasetGrainId: number | null;
  observationSelection: string | null;
  valueTimeRole: string | null;
  boundaryMatch: string | null;
  maxLookbackSeconds: number | null;
  durationUnit: string | null;
  partialIntervalBehavior: string | null;
  missingPeriodBehavior: string | null;
}

/** Values of the measure sheet; text inputs hold '' where the API takes null. */
export interface MeasureFormValues {
  measureKey: string;
  displayName: string;
  measureKind: string | null;
  aggregationFunction: string | null;
  expressionText: string;
  rowFilterExpression: string;
  valueNullBehavior: string | null;
  negativeWeightBehavior: string | null;
  emptySetBehavior: string | null;
  bridgeFactorBehavior: string | null;
  bridgeAllocationBehavior: string | null;
  totalBehavior: string | null;
  outputDataType: string | null;
  outputPrecision: number | null;
  outputScale: number | null;
  accumulatorPrecision: number | null;
  accumulatorScale: number | null;
  roundingMode: string | null;
  unitKey: string;
  implementationKey: string;
  implementationVersion: string;
  configurationJson: string;
  isEnabled: boolean;
  components: ComponentFormValues[];
  /** Whether a measure of a kind other than period_average has a time policy; period averages always have one. */
  hasTimePolicy: boolean;
  timePolicy: TimePolicyFormValues;
}

/**
 * The component roles each measure kind reads, in the order the editor offers them: what the API's
 * `component_roles_match_kind` rule allows, so the editor never offers a role the kind rejects.
 */
const COMPONENT_ROLES: Record<string, string[]> = {
  aggregate: ['value'],
  weighted_sum: ['value', 'weight'],
  weighted_average: ['value', 'weight'],
  period_average: ['value', 'period_days'],
  formula: [],
  specialized: ['value', 'weight', 'period_days', 'numerator', 'denominator'],
};

/** The component roles a measure kind reads; none for formulas and unknown kinds. */
export function componentRolesFor(kind: string | null): string[] {
  return COMPONENT_ROLES[kind ?? ''] ?? [];
}

/** The components a measure keeps when its kind changes: those whose role the new kind reads. */
export function keepRolesOf(kind: string | null, components: ComponentFormValues[]): ComponentFormValues[] {
  const roles = componentRolesFor(kind);
  return components.filter(component => roles.includes(component.componentRole));
}

/** Whether a measure kind takes an aggregation function: aggregates need one, and specialized measures may use one. */
export function usesAggregationFunction(kind: string | null) {
  return kind === 'aggregate' || kind === 'specialized';
}

/** Whether the sheet shows and sends a time policy: always for period averages, otherwise when switched on. */
export function sendsTimePolicy(values: Pick<MeasureFormValues, 'measureKind' | 'hasTimePolicy'>) {
  return values.measureKind === 'period_average' || values.hasTimePolicy;
}

/** Sheet values of a measure, or of a new decimal sum over a value field with the schema's default behaviors. */
export function measureFormValues(detail?: MeasureDetail): MeasureFormValues {
  const measure = detail?.measure;
  return {
    measureKey: measure?.measureKey ?? '',
    displayName: measure?.displayName ?? '',
    measureKind: measure?.measureKind ?? 'aggregate',
    aggregationFunction: aggregationFunctionOf(detail),
    expressionText: measure?.expressionText ?? '',
    rowFilterExpression: measure?.rowFilterExpression ?? '',
    valueNullBehavior: measure?.valueNullBehavior ?? 'exclude',
    negativeWeightBehavior: measure?.negativeWeightBehavior ?? 'allow',
    emptySetBehavior: measure?.emptySetBehavior ?? 'null',
    bridgeFactorBehavior: measure?.bridgeFactorBehavior ?? 'ignore',
    bridgeAllocationBehavior: measure?.bridgeAllocationBehavior ?? 'apply',
    totalBehavior: measure?.totalBehavior ?? 'recompute',
    outputDataType: measure?.outputDataType ?? 'decimal',
    outputPrecision: outputSizeOf(detail, measure?.outputPrecision, 20),
    outputScale: outputSizeOf(detail, measure?.outputScale, 2),
    accumulatorPrecision: measure?.accumulatorPrecision ?? 38,
    accumulatorScale: measure?.accumulatorScale ?? 12,
    roundingMode: measure?.roundingMode ?? null,
    unitKey: measure?.unitKey ?? '',
    implementationKey: measure?.implementationKey ?? '',
    implementationVersion: measure?.implementationVersion ?? '',
    configurationJson: measure?.configurationJson ?? '',
    isEnabled: measure?.isEnabled ?? true,
    components: componentsOf(detail),
    hasTimePolicy: Boolean(detail?.timePolicy),
    timePolicy: timePolicyFormValues(detail?.timePolicy),
  };
}

/** A measure's aggregation function, or sum for a new measure. */
function aggregationFunctionOf(detail?: MeasureDetail) {
  if (!detail) {
    return 'sum';
  }
  return detail.measure.aggregationFunction ?? null;
}

/** A stored output precision or scale, or the default of a new measure. */
function outputSizeOf(detail: MeasureDetail | undefined, stored: number | null | undefined, fallback: number) {
  if (!detail) {
    return fallback;
  }
  return stored ?? null;
}

/** A measure's components as editor rows, or a single value row for a new measure. */
function componentsOf(detail?: MeasureDetail): ComponentFormValues[] {
  if (!detail) {
    return [{ componentRole: 'value', fieldId: null }];
  }
  return detail.components.map(component => ({ componentRole: component.componentRole, fieldId: component.fieldId }));
}

/** Time policy section values of a policy, or of one that uses every observation with the schema's defaults. */
function timePolicyFormValues(policy?: MeasureTimePolicy | null): TimePolicyFormValues {
  return {
    requiredTimeBindingId: policy?.requiredTimeBindingId ?? null,
    selectionDatasetGrainId: policy?.selectionDatasetGrainId ?? null,
    observationSelection: policy?.observationSelection ?? 'all',
    valueTimeRole: policy?.valueTimeRole ?? 'point',
    boundaryMatch: policy?.boundaryMatch ?? 'exact',
    maxLookbackSeconds: policy?.maxLookbackSeconds ?? null,
    durationUnit: policy?.durationUnit ?? 'calendar_day',
    partialIntervalBehavior: policy?.partialIntervalBehavior ?? 'exact_only',
    missingPeriodBehavior: policy?.missingPeriodBehavior ?? 'null',
  };
}

/**
 * The `PUT` body of the sheet's values. The sheet shows only the inputs of the chosen kind and output type, so values
 * left behind in hidden inputs are not sent: the aggregation function only for aggregates and specialized measures, the
 * formula only for formulas, the implementation only for specialized measures, the precision and scale only for decimal
 * outputs, and the time policy only when it is shown. Empty text is sent as null.
 */
export function toMeasureUpdateInput(values: MeasureFormValues): MeasureUpdateInput {
  const kind = values.measureKind;
  const isSpecialized = kind === 'specialized';
  const isDecimal = values.outputDataType === 'decimal';
  return {
    displayName: values.displayName,
    measureKind: kind ?? '',
    aggregationFunction: usesAggregationFunction(kind) ? values.aggregationFunction : null,
    expressionText: kind === 'formula' ? emptyToNull(values.expressionText) : null,
    rowFilterExpression: emptyToNull(values.rowFilterExpression),
    valueNullBehavior: values.valueNullBehavior ?? '',
    negativeWeightBehavior: values.negativeWeightBehavior ?? '',
    emptySetBehavior: values.emptySetBehavior ?? '',
    bridgeFactorBehavior: values.bridgeFactorBehavior ?? '',
    bridgeAllocationBehavior: values.bridgeAllocationBehavior ?? '',
    totalBehavior: values.totalBehavior ?? '',
    outputDataType: values.outputDataType ?? '',
    outputPrecision: isDecimal ? values.outputPrecision : null,
    outputScale: isDecimal ? values.outputScale : null,
    accumulatorPrecision: values.accumulatorPrecision,
    accumulatorScale: values.accumulatorScale,
    roundingMode: values.roundingMode,
    unitKey: emptyToNull(values.unitKey),
    implementationKey: isSpecialized ? emptyToNull(values.implementationKey) : null,
    implementationVersion: isSpecialized ? emptyToNull(values.implementationVersion) : null,
    configurationJson: isSpecialized ? emptyToNull(values.configurationJson) : null,
    isEnabled: values.isEnabled,
    components: values.components,
    timePolicy: sendsTimePolicy(values) ? toTimePolicyInput(values.timePolicy) : null,
  };
}

/** The `POST` body of the sheet's values: the update body plus the key. */
export function toMeasureInput(values: MeasureFormValues): MeasureInput {
  return { measureKey: values.measureKey, ...toMeasureUpdateInput(values) };
}

/** The time policy section of the body; the lookback is sent only for boundary matches that search back. */
function toTimePolicyInput(values: TimePolicyFormValues): MeasureTimePolicyInput {
  return {
    requiredTimeBindingId: values.requiredTimeBindingId,
    selectionDatasetGrainId: values.selectionDatasetGrainId,
    observationSelection: values.observationSelection ?? '',
    valueTimeRole: values.valueTimeRole ?? '',
    boundaryMatch: values.boundaryMatch ?? '',
    maxLookbackSeconds: searchesBack(values.boundaryMatch) ? values.maxLookbackSeconds : null,
    durationUnit: values.durationUnit ?? '',
    partialIntervalBehavior: values.partialIntervalBehavior ?? '',
    missingPeriodBehavior: values.missingPeriodBehavior ?? '',
  };
}

/** Whether a boundary match looks back for a prior observation, which bounds it by the lookback. */
export function searchesBack(boundaryMatch: string | null) {
  return boundaryMatch === 'on_or_before' || boundaryMatch === 'strictly_before';
}
