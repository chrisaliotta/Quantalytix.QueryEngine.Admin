import { emptyToNull } from '@/features/datasets/text';
import type { BindingDetail, BindingInput, TimeBinding, TimeBindingInput } from './types';

/** Values of the binding sheet's time section; text inputs hold '' where the API takes null. */
export interface TimeBindingFormValues {
  timeValueType: string | null;
  observationKind: string | null;
  sourceTimeZone: string;
  nativePeriodicity: string | null;
  intervalEndFieldId: number | null;
  allowedPeriodicities: string[];
  defaultQueryTimeZone: string;
  cumulativeOrigin: string | null;
  cumulativeFixedStart: string | null;
}

/** Values of the binding sheet. `bridgeId` only narrows the bridge version picker; it is never sent. */
export interface BindingFormValues {
  displayName: string;
  bindingKind: string | null;
  dimensionId: number | null;
  pinnedDimensionVersionId: number | null;
  sourceFieldId: number | null;
  mappingSetId: number | null;
  sourceBindingId: number | null;
  bridgeId: number | null;
  bridgeVersionId: number | null;
  groupingMode: string | null;
  groupingLevelId: number | null;
  groupingAncestorMemberId: number | null;
  groupingDepth: number | null;
  raggedBehavior: string | null;
  defaultPeriodAlignment: string | null;
  unmappedBehavior: string | null;
  unknownMemberId: number | null;
  isRequired: boolean;
  isGroupable: boolean;
  isFilterable: boolean;
  isEnabled: boolean;
  timeBinding: TimeBindingFormValues;
}

/** Every periodicity, which a new time binding allows by default. */
const ALL_PERIODICITIES = ['day', 'month', 'quarter', 'year'];

/** Sheet values of a binding, or of a new direct binding with the schema's defaults. */
export function bindingFormValues(detail?: BindingDetail): BindingFormValues {
  const binding = detail?.binding;
  return {
    displayName: binding?.displayName ?? '',
    bindingKind: binding?.bindingKind ?? 'direct',
    dimensionId: binding?.dimensionId ?? null,
    pinnedDimensionVersionId: binding?.pinnedDimensionVersionId ?? null,
    sourceFieldId: binding?.sourceFieldId ?? null,
    mappingSetId: binding?.mappingSetId ?? null,
    sourceBindingId: binding?.sourceBindingId ?? null,
    bridgeId: detail?.bridgeId ?? null,
    bridgeVersionId: binding?.bridgeVersionId ?? null,
    groupingMode: binding?.groupingMode ?? 'member',
    groupingLevelId: binding?.groupingLevelId ?? null,
    groupingAncestorMemberId: binding?.groupingAncestorMemberId ?? null,
    groupingDepth: binding?.groupingDepth ?? null,
    raggedBehavior: binding?.raggedBehavior ?? 'null',
    defaultPeriodAlignment: binding?.defaultPeriodAlignment ?? 'observation',
    unmappedBehavior: binding?.unmappedBehavior ?? 'null',
    unknownMemberId: binding?.unknownMemberId ?? null,
    isRequired: binding?.isRequired ?? false,
    isGroupable: binding?.isGroupable ?? true,
    isFilterable: binding?.isFilterable ?? true,
    isEnabled: binding?.isEnabled ?? true,
    timeBinding: timeBindingFormValues(detail?.timeBinding),
  };
}

/** Time section values of a time extension, or of daily date snapshots that allow every periodicity. */
function timeBindingFormValues(timeBinding?: TimeBinding | null): TimeBindingFormValues {
  return {
    timeValueType: timeBinding?.timeValueType ?? 'date',
    observationKind: timeBinding?.observationKind ?? 'snapshot',
    sourceTimeZone: timeBinding?.sourceTimeZone ?? '',
    nativePeriodicity: timeBinding?.nativePeriodicity ?? 'day',
    intervalEndFieldId: timeBinding?.intervalEndFieldId ?? null,
    allowedPeriodicities: allowedPeriodicities(timeBinding),
    defaultQueryTimeZone: timeBinding?.defaultQueryTimeZone ?? 'UTC',
    cumulativeOrigin: timeBinding?.cumulativeOrigin ?? 'output_range_start',
    cumulativeFixedStart: timeBinding?.cumulativeFixedStart ?? null,
  };
}

/** The periodicities a time extension allows, read from its JSON array. */
function allowedPeriodicities(timeBinding?: TimeBinding | null): string[] {
  if (!timeBinding) {
    return ALL_PERIODICITIES;
  }
  return JSON.parse(timeBinding.allowedPeriodicitiesJson) as string[];
}

/**
 * The body of the sheet's values. The sheet shows only the references of the chosen kind, the grouping reference of the
 * chosen mode and the unknown member of the unknown behavior, so values left behind in hidden inputs are not sent; the
 * time section is sent only for time bindings.
 */
export function toBindingInput(values: BindingFormValues): BindingInput {
  const kind = values.bindingKind;
  return {
    displayName: values.displayName,
    dimensionId: values.dimensionId,
    pinnedDimensionVersionId: values.pinnedDimensionVersionId,
    bindingKind: kind ?? '',
    sourceFieldId: values.sourceFieldId,
    mappingSetId: kind === 'mapped' ? values.mappingSetId : null,
    sourceBindingId: kind === 'bridge' ? values.sourceBindingId : null,
    bridgeVersionId: kind === 'bridge' ? values.bridgeVersionId : null,
    groupingMode: values.groupingMode ?? '',
    groupingLevelId: values.groupingMode === 'level' ? values.groupingLevelId : null,
    groupingAncestorMemberId: values.groupingMode === 'ancestor' ? values.groupingAncestorMemberId : null,
    groupingDepth: values.groupingMode === 'depth' ? values.groupingDepth : null,
    raggedBehavior: values.raggedBehavior ?? '',
    defaultPeriodAlignment: values.defaultPeriodAlignment ?? '',
    unmappedBehavior: values.unmappedBehavior ?? '',
    unknownMemberId: values.unmappedBehavior === 'unknown' ? values.unknownMemberId : null,
    isRequired: values.isRequired,
    isGroupable: values.isGroupable,
    isFilterable: values.isFilterable,
    isEnabled: values.isEnabled,
    timeBinding: kind === 'time' ? toTimeBindingInput(values.timeBinding) : null,
  };
}

/** The time section of the body: the source zone only for timestamps, the fixed start only for that origin. */
function toTimeBindingInput(values: TimeBindingFormValues): TimeBindingInput {
  return {
    timeValueType: values.timeValueType ?? '',
    observationKind: values.observationKind ?? '',
    sourceTimeZone: values.timeValueType === 'timestamp' ? emptyToNull(values.sourceTimeZone) : null,
    nativePeriodicity: values.nativePeriodicity,
    intervalEndFieldId: values.intervalEndFieldId,
    allowedPeriodicities: values.allowedPeriodicities,
    defaultQueryTimeZone: values.defaultQueryTimeZone,
    cumulativeOrigin: values.cumulativeOrigin ?? '',
    cumulativeFixedStart: values.cumulativeOrigin === 'fixed_start' ? values.cumulativeFixedStart : null,
  };
}
