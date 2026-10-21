/**
 * Why these tests exist: sequential attribution switches a metric's drivers in sort order and the API refuses repeated sort
 * orders, so the sheet orders drivers by their place in the list and numbers them on save; an empty expression means the
 * calculated measure's own formula, which the API stores as null. The tables summarize each metric's attribution so an
 * administrator sees which metrics still need one: a variable has no formula, so without an attribution it has no drivers.
 */
import { describe, expect, it } from 'vitest';
import { attributionSummary, toAttributionInput, toAttributionValues } from './attributionForm';
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
