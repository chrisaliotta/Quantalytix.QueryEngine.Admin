/**
 * A time policy stores its lookback as whole seconds (`maxLookbackSeconds`); editors enter it as an amount of seconds, days or
 * months. A month is a fixed 31 days, so a one-month lookback always reaches past a full calendar month.
 */
export type LookbackUnit = 'seconds' | 'days' | 'months'

export const LOOKBACK_UNIT_SECONDS: Record<LookbackUnit, number> = { seconds: 1, days: 86_400, months: 31 * 86_400 }

export const LOOKBACK_UNIT_OPTIONS: { value: LookbackUnit; label: string }[] = [
  { value: 'seconds', label: 'Seconds' },
  { value: 'days', label: 'Days' },
  { value: 'months', label: 'Months' },
]

/** The largest unit the stored seconds are a whole number of. */
export function lookbackUnitOf(seconds: number): LookbackUnit {
  if (seconds % LOOKBACK_UNIT_SECONDS.months === 0) return 'months'
  if (seconds % LOOKBACK_UNIT_SECONDS.days === 0) return 'days'
  return 'seconds'
}

export const toSeconds = (amount: number, unit: LookbackUnit) => amount * LOOKBACK_UNIT_SECONDS[unit]

export const fromSeconds = (seconds: number, unit: LookbackUnit) => seconds / LOOKBACK_UNIT_SECONDS[unit]
