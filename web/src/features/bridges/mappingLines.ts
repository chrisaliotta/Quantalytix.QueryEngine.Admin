import { readPastedLines, type PastedRows } from '@/shared/bulkLists';
import type { LookupItem } from '@/shared/types';
import type { BridgeMapping, BridgeMappingInput } from './types';
import { decimalText } from './weights';

/** The line format of pasted bridge mappings. */
export const MAPPING_LINE_FORMAT = 'source_key,target_key,factor,weight';

/** Plain decimal text: digits with an optional sign and fraction, such as `-1`, `0.35` or `.5`. */
const DECIMAL = /^[+-]?(\d+\.?\d*|\.\d+)$/;

/**
 * Reads pasted `source_key,target_key,factor,weight` lines into mappings, resolving each source key among the source
 * version's members and each target key among the target version's.
 */
export function readMappingLines(
  text: string,
  sourceMembers: LookupItem[],
  targetMembers: LookupItem[]
): PastedRows<BridgeMappingInput> {
  const sourceIds = new Map(sourceMembers.map(member => [member.key, member.id]));
  const targetIds = new Map(targetMembers.map(member => [member.key, member.id]));
  return readPastedLines<BridgeMappingInput>(text, values => {
    if (values.length !== 4) {
      return { reason: `Expected ${MAPPING_LINE_FORMAT}.` };
    }
    const [sourceKey, targetKey, factor, weight] = values;
    const sourceMemberId = sourceIds.get(sourceKey);
    if (sourceMemberId === undefined) {
      return { reason: `No member of the source version has the key '${sourceKey}'.` };
    }
    const targetMemberId = targetIds.get(targetKey);
    if (targetMemberId === undefined) {
      return { reason: `No member of the target version has the key '${targetKey}'.` };
    }
    if (!DECIMAL.test(factor)) {
      return { reason: `The factor '${factor}' is not a number.` };
    }
    if (!DECIMAL.test(weight)) {
      return { reason: `The weight '${weight}' is not a number.` };
    }
    return { row: { sourceMemberId, targetMemberId, factor: Number(factor), allocationWeight: Number(weight) } };
  });
}

/** Writes mappings as `source_key,target_key,factor,weight` lines; a mapping with a member not listed is left out. */
export function writeMappingLines(
  mappings: BridgeMapping[],
  sourceMembers: LookupItem[],
  targetMembers: LookupItem[]
): string {
  const sourceKeys = new Map(sourceMembers.map(member => [member.id, member.key]));
  const targetKeys = new Map(targetMembers.map(member => [member.id, member.key]));
  return mappings
    .flatMap(mapping => {
      const sourceKey = sourceKeys.get(mapping.sourceMemberId);
      const targetKey = targetKeys.get(mapping.targetMemberId);
      if (sourceKey === undefined || targetKey === undefined) {
        return [];
      }
      return [`${sourceKey},${targetKey},${decimalText(mapping.factor)},${decimalText(mapping.allocationWeight)}`];
    })
    .join('\n');
}
