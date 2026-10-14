/**
 * Why these tests exist: in allocation mode `partition`, each source member's weights must sum to exactly 1, and the
 * page shows every source's sum so authors can fix a partition before validating. The API compares the sums in
 * decimal; the page must too, or weights such as 0.086 + 0.35 + 0.564 would show 0.9999999999999999 and look wrong.
 * Factors and weights are also written back as paste lines, which must be plain decimals that read again.
 */
import { describe, expect, it } from 'vitest';
import type { BridgeMapping } from './types';
import { decimalText, sourceWeightSums } from './weights';

function mapping(sourceMemberId: number, targetMemberId: number, allocationWeight: number): BridgeMapping {
  return { bridgeVersionId: 50, sourceMemberId, targetMemberId, factor: 1, allocationWeight };
}

describe('sourceWeightSums', () => {
  it('sums each source member in decimal, so weights adding up to 1 are exactly 1', () => {
    const sums = sourceWeightSums([
      mapping(1, 11, 0.086),
      mapping(1, 12, 0.35),
      mapping(1, 13, 0.564),
      mapping(2, 12, 1),
    ]);

    expect(sums.get(1)).toEqual({ text: '1', isOne: true });
    expect(sums.get(2)).toEqual({ text: '1', isOne: true });
  });

  it('shows a sum other than 1 as it is', () => {
    const sums = sourceWeightSums([mapping(1, 11, 0.3), mapping(1, 12, 0.6)]);

    expect(sums.get(1)).toEqual({ text: '0.9', isOne: false });
  });
});

describe('decimalText', () => {
  it('writes plain decimals without trailing zeros or exponents', () => {
    expect([0.35, -1, 1e-7, 0.000000000001].map(decimalText)).toEqual(['0.35', '-1', '0.0000001', '0.000000000001']);
  });
});
