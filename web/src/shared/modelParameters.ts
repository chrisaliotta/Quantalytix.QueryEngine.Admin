import type { components } from '@/api/schema';

/** A parameter a calculation model declares, as its parameters endpoint and a report's editor view return it. */
type ModelParameter = components['schemas']['ModelParameter'];

/** A parameter's default: the value of every report row that neither sets one nor sits under a row that does. */
export function parameterDefault(parameter: ModelParameter): number | string {
  return parameter.valueType === 'number' ? (parameter.defaultNumber as number) : (parameter.defaultText as string);
}
