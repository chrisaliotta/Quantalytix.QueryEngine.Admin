import type { LookupItem } from '@/shared/types';
import type { BridgeMapping, BridgeMappingInput } from './types';

/** A mapping with the keys and labels of its members, which the table shows, sorts and searches. */
export interface BridgeMappingRow extends BridgeMapping {
  sourceKey: string;
  sourceLabel: string;
  targetKey: string;
  targetLabel: string;
}

/**
 * The mappings with their members' keys and labels, by source key, then target key. A member outside its endpoint
 * version shows its ID.
 */
export function toMappingRows(
  mappings: BridgeMapping[],
  sourceMembers: LookupItem[],
  targetMembers: LookupItem[]
): BridgeMappingRow[] {
  const sources = new Map(sourceMembers.map(member => [member.id, member]));
  const targets = new Map(targetMembers.map(member => [member.id, member]));
  return mappings
    .map(mapping => {
      const source = memberText(sources.get(mapping.sourceMemberId), mapping.sourceMemberId, 'source');
      const target = memberText(targets.get(mapping.targetMemberId), mapping.targetMemberId, 'target');
      return {
        ...mapping,
        sourceKey: source.key,
        sourceLabel: source.label,
        targetKey: target.key,
        targetLabel: target.label,
      };
    })
    .sort(
      (left, right) => left.sourceKey.localeCompare(right.sourceKey) || left.targetKey.localeCompare(right.targetKey)
    );
}

/**
 * The list the API stores without `removed`, identified by its source and target members; the whole list for null.
 * A bridge version's mappings are replaced as a whole, so every single edit sends this list.
 */
export function withoutMapping(mappings: BridgeMapping[], removed: BridgeMapping | null): BridgeMappingInput[] {
  return mappings
    .filter(
      mapping =>
        removed === null ||
        mapping.sourceMemberId !== removed.sourceMemberId ||
        mapping.targetMemberId !== removed.targetMemberId
    )
    .map(mapping => ({
      sourceMemberId: mapping.sourceMemberId,
      targetMemberId: mapping.targetMemberId,
      factor: mapping.factor,
      allocationWeight: mapping.allocationWeight,
    }));
}

/** A member's key and label, or its ID and a note when it is not a member of the endpoint version. */
function memberText(member: LookupItem | undefined, memberId: number, end: 'source' | 'target') {
  if (!member) {
    return { key: String(memberId), label: `Not a member of the ${end} version` };
  }
  return { key: member.key, label: member.label };
}
