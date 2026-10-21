/**
 * Why these tests exist: in allocation mode `partition`, each source member's weights must sum to exactly 1, and the
 * page shows every source's sum so authors can fix a partition before validating. The API compares the sums in
 * decimal; the page must too, or weights such as 0.086 + 0.35 + 0.564 would show 0.9999999999999999 and look wrong.
 * Factors and weights are also written back as paste lines, which must be plain decimals that read again.
 */
import { describe, expect, it } from 'vitest';
import type { BridgeMapping } from './types';
import { decimalText, sourceWeightSums } from './weights';

function mapping(sourceMemberKey: string, targetMemberKey: string, allocationWeight: number): BridgeMapping {
  return { bridgeMappingId: 0, bridgeVersionId: 50, sourceMemberKey, targetMemberKey, factor: 1, allocationWeight, sourceSelection: 'member' };
}

describe('sourceWeightSums', () => {
  it('sums each source member in decimal, so weights adding up to 1 are exactly 1', () => {
    const sums = sourceWeightSums([
      mapping('LOAN_A', 'EAST', 0.086),
      mapping('LOAN_A', 'WEST', 0.35),
      mapping('LOAN_A', 'NORTH', 0.564),
      mapping('LOAN_B', 'WEST', 1),
    ]);

    expect(sums.get('LOAN_A')).toEqual({ text: '1', isOne: true });
    expect(sums.get('LOAN_B')).toEqual({ text: '1', isOne: true });
  });

  it('shows a sum other than 1 as it is', () => {
    const sums = sourceWeightSums([mapping('LOAN_A', 'EAST', 0.3), mapping('LOAN_A', 'WEST', 0.6)]);

    expect(sums.get('LOAN_A')).toEqual({ text: '0.9', isOne: false });
  });
});

describe('decimalText', () => {
  it('writes plain decimals without trailing zeros or exponents', () => {
    expect([0.35, -1, 1e-7, 0.000000000001].map(decimalText)).toEqual(['0.35', '-1', '0.0000001', '0.000000000001']);
  });
});
