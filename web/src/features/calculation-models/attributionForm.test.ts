/**
 * Why these tests exist: sequential attribution switches a metric's drivers in sort order and the API refuses repeated sort
 * orders, so the sheet orders drivers by their place in the list and numbers them on save; an empty expression means the
 * calculated measure's own formula, which the API stores as null. The tables summarize each metric's attribution so an
 * administrator sees which metrics still need one: a variable has no formula, so without an attribution it has no drivers.
 * The drivers must split exactly the inputs the API counts in the expression, so the sheet offers those: YEARFRAC reads the
 * period, so it reads both days inputs, and a declared parameter is never an input.
 */
import { describe, expect, it } from 'vitest';
import { attributionInputs, attributionSummary, toAttributionInput, toAttributionValues } from './attributionForm';
import type { MetricAttribution } from './types';

/** The seed's attribution of interest income: an identity expression with volume, rate and days drivers. */
const interestIncome: MetricAttribution = {
  metricKey: 'INTEREST_INCOME',
  expressionText: '[m.AVERAGE_BALANCE] * [m.YIELD] * @periodDays / @yearDays',
  weightVariableKey: null,
  drivers: [
    { driverKey: 'volume', displayName: 'Volume', sortOrder: 10, inputKeys: ['AVERAGE_BALANCE'] },
    { driverKey: 'rate', displayName: 'Rate', sortOrder: 20, inputKeys: ['YIELD'] },
    { driverKey: 'days', displayName: 'Days', sortOrder: 30, inputKeys: ['@periodDays', '@yearDays'] },
  ],
};

describe('toAttributionInput', () => {
  it('numbers the drivers in list order, whatever sort orders they were stored with', () => {
    const values = toAttributionValues(interestIncome);
    values.drivers.reverse();

    expect(toAttributionInput(values).drivers.map(driver => [driver.driverKey, driver.sortOrder])).toEqual([
      ['days', 1],
      ['rate', 2],
      ['volume', 3],
    ]);
  });

  it('sends an empty expression as null and keeps a stored one', () => {
    expect(toAttributionInput(toAttributionValues(undefined)).expressionText).toBeNull();
    expect(toAttributionInput(toAttributionValues(interestIncome)).expressionText).toBe(interestIncome.expressionText);
  });
});

describe('attributionInputs', () => {
  it('counts YEARFRAC as reading both days inputs, and never a declared parameter', () => {
    expect(attributionInputs('[m.INTEREST_INCOME] / [m.AVERAGE_BALANCE] / YEARFRAC(@dayCount)')).toEqual([
      'INTEREST_INCOME',
      'AVERAGE_BALANCE',
      '@periodDays',
      '@yearDays',
    ]);
  });

  it('reads both days inputs when the expression reads either, the ones it reads first', () => {
    expect(attributionInputs('[m.V] * @yearDays / @periodDays')).toEqual(['V', '@yearDays', '@periodDays']);
    expect(attributionInputs('[m.V] / @periodDays')).toEqual(['V', '@periodDays', '@yearDays']);
  });

  it('lists each metric once, and reads nothing inside string literals', () => {
    expect(attributionInputs("CASE WHEN @basis = 'YEARFRAC(@periodDays) it''s [m.X]' THEN [m.V] ELSE [m.V] * 2 END")).toEqual(['V']);
  });
});

describe('attributionSummary', () => {
  it('tells default drivers from none, and names the weight variable', () => {
    expect(attributionSummary(undefined, false)).toBe('Default drivers');
    expect(attributionSummary(undefined, true)).toBe('None');
    expect(attributionSummary({ ...interestIncome, drivers: [], weightVariableKey: 'AVERAGE_BALANCE' }, false)).toBe(
      'Default drivers, weighted by AVERAGE_BALANCE'
    );
    expect(attributionSummary(interestIncome, true)).toBe('3 drivers');
  });
});
