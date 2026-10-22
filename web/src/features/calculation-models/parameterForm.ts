import { emptyToNull } from '@/shared/text';
import type { ModelParameter, ModelParameterInput, ParameterValueType } from './types';

/** Values of the parameter sheet; the allowed values are one per line, and an optional text is empty for none. */
export interface ParameterValues {
  parameterKey: string;
  displayName: string;
  valueType: ParameterValueType | null;
  allowedValues: string;
  defaultNumber: number | null;
  defaultText: string;
  description: string;
}

/** A stored parameter as the sheet edits it, or a new number parameter. */
export function toParameterValues(parameter: ModelParameter | null): ParameterValues {
  return {
    parameterKey: parameter?.parameterKey ?? '',
    displayName: parameter?.displayName ?? '',
    valueType: (parameter?.valueType as ParameterValueType | undefined) ?? 'number',
    allowedValues: parameter?.allowedValues?.join('\n') ?? '',
    defaultNumber: parameter?.defaultNumber ?? null,
    defaultText: parameter?.defaultText ?? '',
    description: parameter?.description ?? '',
  };
}

/** The texts the allowed values' lines list, each trimmed, without blank lines; none means any text. */
export function allowedValueLines(text: string): string[] {
  return text
    .split('\n')
    .map(line => line.trim())
    .filter(line => line !== '');
}

/**
 * The body the API takes: a number parameter's number default alone, or a text parameter's text default with its allowed values,
 * or none for any text; the other type's fields are sent empty, as the API requires.
 */
export function toParameterInput(values: ParameterValues): ModelParameterInput {
  const common = { displayName: values.displayName, valueType: values.valueType ?? '', description: emptyToNull(values.description) };
  if (values.valueType === 'text') {
    const allowed = allowedValueLines(values.allowedValues);
    return { ...common, allowedValues: allowed.length === 0 ? null : allowed, defaultText: values.defaultText, defaultNumber: null };
  }
  return { ...common, allowedValues: null, defaultNumber: values.defaultNumber, defaultText: null };
}
