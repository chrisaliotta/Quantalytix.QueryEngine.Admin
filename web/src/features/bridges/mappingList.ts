import type { LookupItem } from '@/shared/types';
import type { BridgeMapping, BridgeMappingInput } from './types';

/** A mapping with the labels of its members, which the table shows and searches. */
export interface BridgeMappingRow extends BridgeMapping {
  sourceLabel: string;
  targetLabel: string;
}

/**
 * The mappings with the labels their member keys have in the published versions of the bridge's dimensions, by source key,
 * then target key. A key the published version lacks is marked.
 */
export function toMappingRows(
  mappings: BridgeMapping[],
  sourceMembers: LookupItem[],
  targetMembers: LookupItem[]
): BridgeMappingRow[] {
  const sources = new Map(sourceMembers.map(member => [member.key, member.label]));
  const targets = new Map(targetMembers.map(member => [member.key, member.label]));
  return mappings
    .map(mapping => ({
      ...mapping,
      sourceLabel: sources.get(mapping.sourceMemberKey) ?? 'Not a member of the published source version',
      targetLabel: targets.get(mapping.targetMemberKey) ?? 'Not a member of the published target version',
    }))
    .sort(
      (left, right) =>
        left.sourceMemberKey.localeCompare(right.sourceMemberKey) || left.targetMemberKey.localeCompare(right.targetMemberKey)
    );
}

/**
 * The list the API stores without `removed`, identified by its source and target member keys; the whole list for null.
 * A bridge version's mappings are replaced as a whole, so every single edit sends this list.
 */
export function withoutMapping(mappings: BridgeMapping[], removed: BridgeMapping | null): BridgeMappingInput[] {
  return mappings
    .filter(
      mapping =>
        removed === null ||
        mapping.sourceMemberKey !== removed.sourceMemberKey ||
        mapping.targetMemberKey !== removed.targetMemberKey
    )
    .map(mapping => ({
      sourceMemberKey: mapping.sourceMemberKey,
      targetMemberKey: mapping.targetMemberKey,
      factor: mapping.factor,
      allocationWeight: mapping.allocationWeight,
    }));
}
