import { emptyToNull } from '@/shared/text';
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

/**
 * Values of the binding sheet: the role a breakout binding binds, or the dimension a time binding names. The unknown member is a
 * key, which requests resolve in the version of the dimension their assumption set pins.
 */
export interface BindingFormValues {
  bindingKind: string | null;
  roleId: number | null;
  dimensionId: number | null;
  sourceFieldId: number | null;
  mappingSetId: number | null;
  sourceBindingId: number | null;
  bridgeId: number | null;
  raggedBehavior: string | null;
  defaultPeriodAlignment: string | null;
  unmappedBehavior: string | null;
  unknownMemberKey: string | null;
  isRequired: boolean;
  isGroupable: boolean;
  isFilterable: boolean;
  isEnabled: boolean;
  timeBinding: TimeBindingFormValues;
}

/** Every periodicity, which a new time binding allows by default. */
const ALL_PERIODICITIES = ['day', 'month', 'quarter', 'year'];

/**
 * Sheet values of a binding, or of a new direct binding with the schema's defaults. A role binding's stored dimension is its
 * role's template, which the sheet reads from the role, so only a time binding loads it.
 */
export function bindingFormValues(detail?: BindingDetail): BindingFormValues {
  const binding = detail?.binding;
  return {
    bindingKind: binding?.bindingKind ?? 'direct',
    roleId: binding?.roleId ?? null,
    dimensionId: binding?.bindingKind === 'time' ? binding.dimensionId : null,
    sourceFieldId: binding?.sourceFieldId ?? null,
    mappingSetId: binding?.mappingSetId ?? null,
    sourceBindingId: binding?.sourceBindingId ?? null,
    bridgeId: binding?.bridgeId ?? null,
    raggedBehavior: binding?.raggedBehavior ?? 'null',
    defaultPeriodAlignment: binding?.defaultPeriodAlignment ?? 'observation',
    unmappedBehavior: binding?.unmappedBehavior ?? 'null',
    unknownMemberKey: binding?.unknownMemberKey ?? null,
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
 * The body of the sheet's values. The sheet shows only the references of the chosen kind, a role for a breakout binding or a
 * dimension for a time binding, and the unknown member of the unknown behavior, so values left behind in hidden inputs are
 * not sent; the time section is sent only for time bindings.
 */
export function toBindingInput(values: BindingFormValues): BindingInput {
  const kind = values.bindingKind;
  return {
    roleId: kind === 'time' ? null : values.roleId,
    dimensionId: kind === 'time' ? values.dimensionId : null,
    bindingKind: kind ?? '',
    sourceFieldId: values.sourceFieldId,
    mappingSetId: kind === 'mapped' ? values.mappingSetId : null,
    sourceBindingId: kind === 'bridge' ? values.sourceBindingId : null,
    bridgeId: kind === 'bridge' ? values.bridgeId : null,
    raggedBehavior: values.raggedBehavior ?? '',
    defaultPeriodAlignment: values.defaultPeriodAlignment ?? '',
    unmappedBehavior: values.unmappedBehavior ?? '',
    unknownMemberKey: values.unmappedBehavior === 'unknown' ? values.unknownMemberKey : null,
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
