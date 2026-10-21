import type { TreeNode } from '@registry/components/TreeView';
import type { DesignRow, DimensionMember, ReportDesign, RowMappingInput } from './types';

/** Compares keys by UTF-16 code unit, as the API's ordinal comparison does. */
function compareOrdinal(left: string, right: string) {
  if (left === right) {
    return 0;
  }
  return left < right ? -1 : 1;
}

/**
 * Nests a bound dimension's members for the item picker: each under its parent, siblings by sort order, then key. A leaf
 * that takes no source values can't be picked, so it is disabled.
 */
export function memberTree(members: DimensionMember[]): TreeNode<DimensionMember>[] {
  function under(parentMemberId: number | null): TreeNode<DimensionMember>[] {
    return members
      .filter(member => (member.parentMemberId ?? null) === parentMemberId)
      .sort((left, right) => left.sortOrder - right.sortOrder || compareOrdinal(left.memberKey, right.memberKey))
      .map(member => {
        const children = under(member.memberId);
        return {
          id: member.memberKey,
          label: `${member.memberKey} — ${member.displayName}`,
          data: member,
          disabled: children.length === 0 && !member.isAssignable,
          ...(children.length > 0 && { children }),
        };
      });
  }
  return under(null);
}

/** The items of a member's branch: the member itself when it takes source values, and every such member under it. */
function branchItems(members: DimensionMember[], key: string): string[] {
  const member = members.find(candidate => candidate.memberKey === key)!;
  const below = members.filter(child => child.parentMemberId === member.memberId).flatMap(child => branchItems(members, child.memberKey));
  return member.isAssignable ? [key, ...below] : below;
}

/**
 * The selected items after the picker's selection changed from `selected` to `next`. Picking a member picks the items of its
 * branch, so picking a group picks the items under it; picking one group whose items are all picked already clears them, and
 * clearing a member clears its branch.
 */
export function toggledItems(selected: ReadonlySet<string>, next: ReadonlySet<string>, members: DimensionMember[]): Set<string> {
  const added = [...next].filter(key => !selected.has(key));
  const result = new Set(selected);
  for (const key of [...selected].filter(key => !next.has(key))) {
    branchItems(members, key).forEach(item => result.delete(item));
  }
  const clearsGroup = added.length === 1 && branchItems(members, added[0]).every(item => result.has(item));
  for (const key of added) {
    branchItems(members, key).forEach(item => (clearsGroup ? result.delete(item) : result.add(item)));
  }
  return result;
}

/** The row each item of one source maps to, by item key. */
export function itemRows(design: ReportDesign, bridgeId: number): Map<string, DesignRow> {
  return new Map(
    design.rows.flatMap(row => row.mappings.filter(mapping => mapping.bridgeId === bridgeId).map(mapping => [mapping.itemKey, row] as const))
  );
}

/** A row's items, by source. */
export function rowSelections(row: DesignRow): Map<number, Set<string>> {
  const selections = new Map<number, Set<string>>();
  for (const mapping of row.mappings) {
    selections.set(mapping.bridgeId, (selections.get(mapping.bridgeId) ?? new Set()).add(mapping.itemKey));
  }
  return selections;
}

/**
 * A row's complete mapping list from the items picked per source, as the row-mappings body takes it: an item the row already
 * had keeps its factor, and any other item, including one that moves here from another row, starts with factor 1.
 */
export function rowMappingsFrom(row: DesignRow, selections: ReadonlyMap<number, ReadonlySet<string>>): RowMappingInput[] {
  const factors = new Map(row.mappings.map(mapping => [`${mapping.bridgeId}:${mapping.itemKey}`, mapping.factor]));
  return [...selections].flatMap(([bridgeId, itemKeys]) =>
    [...itemKeys].sort(compareOrdinal).map(itemKey => ({ bridgeId, itemKey, factor: factors.get(`${bridgeId}:${itemKey}`) ?? 1 }))
  );
}

/** The picked items that map to another row now, with that row, which saving moves to this one. */
export function movedItems(
  design: ReportDesign,
  row: DesignRow,
  selections: ReadonlyMap<number, ReadonlySet<string>>
): { bridgeId: number; itemKey: string; from: DesignRow }[] {
  return [...selections].flatMap(([bridgeId, itemKeys]) => {
    const rows = itemRows(design, bridgeId);
    return [...itemKeys]
      .filter(itemKey => rows.has(itemKey) && rows.get(itemKey)!.key !== row.key)
      .map(itemKey => ({ bridgeId, itemKey, from: rows.get(itemKey)! }));
  });
}
