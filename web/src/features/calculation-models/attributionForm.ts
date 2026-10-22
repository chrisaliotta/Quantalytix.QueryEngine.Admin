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

/** A string literal, whose quotes are doubled inside it, as the engine's tokenizer reads it. */
const STRING_LITERAL = /'(?:[^']|'')*'/g;

/**
 * The inputs a driver can switch in an expression, as the API counts them: the metrics it reads in reading order, then both days
 * inputs when it reads either or calls YEARFRAC, which reads the period; the days it reads come first. The model's declared
 * parameters are never inputs, because a row's value is the same on both sides of a comparison.
 */
export function attributionInputs(expression: string): string[] {
  const code = expression.replace(STRING_LITERAL, "''");
  const metrics = [...code.matchAll(/\[m\.([^\]]+)\]/g)].map(match => match[1]);
  const days = [...code.matchAll(/@(periodDays|yearDays)\b/g)].map(match => `@${match[1]}`);
  const readsDays = days.length > 0 || /\bYEARFRAC\s*\(/i.test(code);
  return [...new Set([...metrics, ...(readsDays ? [...days, ...CONTEXT_PARAMETERS] : [])])];
}

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
