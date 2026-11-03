import { describe, expect, it } from 'vitest'
import { fromSeconds, lookbackUnitOf, toSeconds } from './lookback'

/**
 * The measure panel shows a stored `maxLookbackSeconds` as an amount and a unit, and saves the amount and unit back as seconds.
 * These tests confirm a stored value opens in the unit an editor would expect and survives the round trip unchanged.
 */
describe('lookback units', () => {
  it.each([
    [2_678_400, 'months', 1],
    [86_400, 'days', 1],
    [90_000, 'seconds', 90_000],
  ] as const)('%i seconds opens as %s and saves back unchanged', (seconds, unit, amount) => {
    expect(lookbackUnitOf(seconds)).toBe(unit)
    expect(fromSeconds(seconds, unit)).toBe(amount)
    expect(toSeconds(amount, unit)).toBe(seconds)
  })
})
