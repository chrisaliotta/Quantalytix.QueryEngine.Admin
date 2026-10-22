/**
 * Why these tests exist: the API requires a number parameter to carry only a number default, and a text parameter only a text
 * default with its allowed values, if any, so the sheet must send the other type's fields empty whatever was typed into them
 * before the type changed. Allowed values are edited one per line, so blank lines and stray spaces must not become values, and no
 * line at all must mean any text rather than none.
 */
import { describe, expect, it } from 'vitest';
import { allowedValueLines, toParameterInput, toParameterValues } from './parameterForm';
import type { ModelParameter } from './types';

/** The seed's day count parameter. */
const dayCount: ModelParameter = {
  parameterKey: 'dayCount',
  displayName: 'Day count',
  valueType: 'text',
  allowedValues: ['actual_actual', 'actual_360'],
  defaultText: 'actual_actual',
};

describe('allowedValueLines', () => {
  it('takes one trimmed value per non-blank line', () => {
    expect(allowedValueLines(' actual_actual \n\nactual_360\n  ')).toEqual(['actual_actual', 'actual_360']);
    expect(allowedValueLines('  \n')).toEqual([]);
  });
});

describe('toParameterInput', () => {
  it('sends a text parameter its text default and allowed values, and no number', () => {
    const values = { ...toParameterValues(dayCount), defaultNumber: 5 };

    expect(toParameterInput(values)).toEqual({
      displayName: 'Day count',
      valueType: 'text',
      description: null,
      allowedValues: ['actual_actual', 'actual_360'],
      defaultText: 'actual_actual',
      defaultNumber: null,
    });
  });

  it('sends no allowed values for a text parameter that takes any text', () => {
    expect(toParameterInput({ ...toParameterValues(dayCount), allowedValues: '\n' }).allowedValues).toBeNull();
  });

  it('sends a number parameter its number default alone, whatever text fields were filled before the type changed', () => {
    const values = { ...toParameterValues(dayCount), valueType: 'number' as const, defaultNumber: 0.05 };

    expect(toParameterInput(values)).toMatchObject({ valueType: 'number', defaultNumber: 0.05, defaultText: null, allowedValues: null });
  });
});
