import type { BridgeMapping } from './types';

/** The scale of the factor and weight columns, `DECIMAL(38, 12)`. */
const SCALE = 12;

/** 1 in units of 10^-12. */
const ONE = 10n ** BigInt(SCALE);

/** A source member's allocation weight sum. */
export interface WeightSum {
  /** The sum as plain decimal text, such as `0.9`. */
  text: string;
  /** Whether the sum is exactly 1, as allocation mode `partition` requires. */
  isOne: boolean;
}

/** A factor or weight as plain decimal text, such as `0.0000001` where `String` writes `1e-7`. */
export function decimalText(value: number): string {
  return unitsText(toUnits(value));
}

/**
 * Each source member's allocation weight sum, added in units of the column's scale so that 0.086 + 0.35 + 0.564 is
 * exactly 1, where floating point gives 0.9999999999999999.
 */
export function sourceWeightSums(mappings: BridgeMapping[]): Map<number, WeightSum> {
  const sums = new Map<number, bigint>();
  for (const mapping of mappings) {
    sums.set(mapping.sourceMemberId, (sums.get(mapping.sourceMemberId) ?? 0n) + toUnits(mapping.allocationWeight));
  }
  return new Map(
    [...sums].map(([sourceMemberId, units]) => [sourceMemberId, { text: unitsText(units), isOne: units === ONE }])
  );
}

/** A value in units of 10^-12; the API keeps at most 12 decimals, so rounding to them recovers the stored value. */
function toUnits(value: number): bigint {
  return BigInt(value.toFixed(SCALE).replace('.', ''));
}

/** Units as plain decimal text without trailing zeros, such as `0.9` or `-1`. */
function unitsText(units: bigint): string {
  const sign = units < 0n ? '-' : '';
  const magnitude = units < 0n ? -units : units;
  const whole = (magnitude / ONE).toString();
  const fraction = (magnitude % ONE).toString().padStart(SCALE, '0').replace(/0+$/, '');
  if (fraction === '') {
    return `${sign}${whole}`;
  }
  return `${sign}${whole}.${fraction}`;
}
