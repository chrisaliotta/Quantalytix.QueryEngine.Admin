import { readPastedLines, type PastedRows } from '@/shared/bulkLists';
import type { BridgeMapping, BridgeMappingInput } from './types';
import { decimalText } from './weights';

/** The line format of pasted bridge mappings. */
export const MAPPING_LINE_FORMAT = 'source_key,target_key,factor,weight';

/** Plain decimal text: digits with an optional sign and fraction, such as `-1`, `0.35` or `.5`. */
const DECIMAL = /^[+-]?(\d+\.?\d*|\.\d+)$/;

/**
 * Reads pasted `source_key,target_key,factor,weight` lines into mappings. Member keys are kept as typed: requests resolve
 * them in the published versions of the bridge's dimensions, which the publication rules check.
 */
export function readMappingLines(text: string): PastedRows<BridgeMappingInput> {
  return readPastedLines<BridgeMappingInput>(text, values => {
    if (values.length !== 4) {
      return { reason: `Expected ${MAPPING_LINE_FORMAT}.` };
    }
    const [sourceMemberKey, targetMemberKey, factor, weight] = values;
    if (!DECIMAL.test(factor)) {
      return { reason: `The factor '${factor}' is not a number.` };
    }
    if (!DECIMAL.test(weight)) {
      return { reason: `The weight '${weight}' is not a number.` };
    }
    return { row: { sourceMemberKey, targetMemberKey, factor: Number(factor), allocationWeight: Number(weight) } };
  });
}

/** Writes mappings as `source_key,target_key,factor,weight` lines. */
export function writeMappingLines(mappings: BridgeMapping[]): string {
  return mappings
    .map(
      mapping =>
        `${mapping.sourceMemberKey},${mapping.targetMemberKey},${decimalText(mapping.factor)},${decimalText(mapping.allocationWeight)}`
    )
    .join('\n');
}
