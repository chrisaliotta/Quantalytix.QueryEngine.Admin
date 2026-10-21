import { emptyToNull } from '@/shared/text';
import type { MetricAttribution, MetricAttributionInput } from './types';

/** One driver as the attribution sheet edits it; its place in the list is its sort order. */
export interface DriverValues {
  driverKey: string;
  displayName: string;
  inputKeys: string[];
}

/** Values of the attribution sheet; an empty expression keeps a calculated measure's formula, and no weight means no member contributions. */
export interface AttributionValues {
  expressionText: string;
  weightVariableKey: string | null;
  drivers: DriverValues[];
}

/** The context parameters a driver may switch besides the model's metrics; the default drivers switch them together as `days`. */
export const CONTEXT_PARAMETERS = ['@periodDays', '@yearDays'] as const;

/** A metric's stored attribution as the sheet edits it, drivers in sort order, or an empty one for a metric without. */
export function toAttributionValues(attribution: MetricAttribution | undefined): AttributionValues {
  return {
    expressionText: attribution?.expressionText ?? '',
    weightVariableKey: attribution?.weightVariableKey ?? null,
    drivers: (attribution?.drivers ?? []).map(({ driverKey, displayName, inputKeys }) => ({ driverKey, displayName, inputKeys: [...inputKeys] })),
  };
}

/** The body the API takes: drivers numbered in list order, which is the order sequential attribution switches them in. */
export function toAttributionInput(values: AttributionValues): MetricAttributionInput {
  return {
    expressionText: emptyToNull(values.expressionText),
    weightVariableKey: values.weightVariableKey,
    drivers: values.drivers.map((driver, index) => ({ ...driver, sortOrder: index + 1 })),
  };
}

/**
 * What a metric's attribution amounts to, for a table cell: its own drivers, or the default ones a calculated measure takes from
 * its formula; a variable without an attribution has none, because it has no formula.
 */
export function attributionSummary(attribution: MetricAttribution | undefined, isVariable: boolean): string {
  if (attribution === undefined) {
    return isVariable ? 'None' : 'Default drivers';
  }
  const drivers = attribution.drivers.length === 0 ? 'Default drivers' : `${attribution.drivers.length} drivers`;
  return attribution.weightVariableKey ? `${drivers}, weighted by ${attribution.weightVariableKey}` : drivers;
}
