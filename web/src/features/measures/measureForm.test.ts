/**
 * Why these tests exist: the measure sheet shows only the inputs of the chosen kind and output type, and a time policy
 * only when it is switched on or the measure is a period average. Values left in hidden inputs must not be sent, or the
 * API rejects inputs the user can no longer see. Switching kind also drops the components whose role the new kind does
 * not read, so the components editor always shows exactly the rows the body carries.
 */
import { describe, expect, it } from 'vitest';
import { keepRolesOf, measureFormValues, toMeasureInput, toMeasureUpdateInput, type MeasureFormValues } from './measureForm';

describe('toMeasureInput', () => {
  const values: MeasureFormValues = {
    ...measureFormValues(),
    measureKey: 'endingBalance',
    displayName: 'Ending balance',
    expressionText: '[m.interest] / [m.balance]',
    implementationKey: 'irr',
    implementationVersion: '1.0',
    configurationJson: '{"periods":12}',
  };

  it('sends the formula only for formulas and the aggregation function only where the kind takes one', () => {
    expect(toMeasureInput({ ...values, measureKind: 'aggregate' })).toMatchObject({ aggregationFunction: 'sum', expressionText: null });
    expect(toMeasureInput({ ...values, measureKind: 'formula' })).toMatchObject({
      aggregationFunction: null,
      expressionText: '[m.interest] / [m.balance]',
    });
    expect(toMeasureInput({ ...values, measureKind: 'weighted_average' }).aggregationFunction).toBeNull();
  });

  it('sends the implementation only for specialized measures', () => {
    expect(toMeasureInput({ ...values, measureKind: 'specialized' })).toMatchObject({
      implementationKey: 'irr',
      implementationVersion: '1.0',
      configurationJson: '{"periods":12}',
    });
    expect(toMeasureInput({ ...values, measureKind: 'aggregate' })).toMatchObject({
      implementationKey: null,
      implementationVersion: null,
      configurationJson: null,
    });
  });

  it('sends precision and scale only for decimal outputs', () => {
    expect(toMeasureInput({ ...values, outputDataType: 'decimal' })).toMatchObject({ outputPrecision: 20, outputScale: 2 });
    expect(toMeasureInput({ ...values, outputDataType: 'int64' })).toMatchObject({ outputPrecision: null, outputScale: null });
  });

  // Period averages always need a time policy, so the sheet shows it without the switch.
  it('sends the time policy when it is switched on, and always for period averages', () => {
    expect(toMeasureInput({ ...values, measureKind: 'aggregate', hasTimePolicy: false }).timePolicy).toBeNull();
    expect(toMeasureInput({ ...values, measureKind: 'aggregate', hasTimePolicy: true }).timePolicy).toMatchObject({ observationSelection: 'all' });
    expect(toMeasureInput({ ...values, measureKind: 'period_average', hasTimePolicy: false }).timePolicy).not.toBeNull();
  });

  it('sends the lookback only for boundary matches that search back', () => {
    const policy = { ...values.timePolicy, maxLookbackSeconds: 86400 };

    const exact = toMeasureInput({ ...values, hasTimePolicy: true, timePolicy: { ...policy, boundaryMatch: 'exact' } });
    const prior = toMeasureInput({ ...values, hasTimePolicy: true, timePolicy: { ...policy, boundaryMatch: 'on_or_before' } });

    expect(exact.timePolicy?.maxLookbackSeconds).toBeNull();
    expect(prior.timePolicy?.maxLookbackSeconds).toBe(86400);
  });

  it('sends empty optional text as null and leaves the key out of the update body', () => {
    const body = toMeasureUpdateInput({ ...values, rowFilterExpression: '  ', unitKey: '' });

    expect(body.rowFilterExpression).toBeNull();
    expect(body.unitKey).toBeNull();
    expect(body).not.toHaveProperty('measureKey');
  });
});

describe('keepRolesOf', () => {
  const components = [
    { componentRole: 'value', fieldId: 1 },
    { componentRole: 'weight', fieldId: 2 },
  ];

  it('keeps the components whose role the new kind reads', () => {
    expect(keepRolesOf('weighted_sum', components)).toEqual(components);
    expect(keepRolesOf('aggregate', components)).toEqual([{ componentRole: 'value', fieldId: 1 }]);
  });

  it('drops every component of a formula, which reads measures instead of fields', () => {
    expect(keepRolesOf('formula', components)).toEqual([]);
  });
});
