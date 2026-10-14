/**
 * Why these tests exist: the Members tab edits a version's hierarchy by drag and drop, and the API stores exactly the
 * placements this module computes. They pin down that the tree mirrors the parent links (keeping orphans and cycle
 * members visible), that a drop sends only the members whose parent or sort order changed, and that searching keeps the
 * path to each match.
 */
import { describe, expect, it } from 'vitest';
import type { TreeItems } from '@registry/components/Nestable';
import { buildMemberTree, filterMemberTree, placementsAfterMove, selfAndDescendants } from './memberTree';
import type { DimensionMember } from './types';

function member(memberId: number, memberKey: string, parentMemberId: number | null, sortOrder = 0): DimensionMember {
  return {
    memberId,
    dimensionVersionId: 10,
    memberKey,
    displayName: `Member ${memberKey}`,
    parentMemberId,
    isAssignable: true,
    sortOrder,
  };
}

/** The tree as nested [key, children] pairs. */
function shape(tree: TreeItems<DimensionMember>): unknown[] {
  return tree.map(item => [item.data?.memberKey, shape(item.children)]);
}

const members = [
  member(1, 'US', null),
  member(2, 'NY', 1, 1),
  member(3, 'CA', 1, 0),
  member(4, 'LA', 3),
  member(5, 'EU', null, 1),
];

describe('buildMemberTree', () => {
  it('nests members by parent, siblings by sort order then key', () => {
    expect(shape(buildMemberTree(members))).toEqual([
      ['US', [['CA', [['LA', []]]], ['NY', []]]],
      ['EU', []],
    ]);
  });

  // A broken draft must stay editable, so nothing may disappear from the tree.
  it('shows members with a foreign parent or in a cycle at the root', () => {
    const tree = buildMemberTree([member(1, 'A', 2), member(2, 'B', 1), member(3, 'X', 999)]);

    expect(shape(tree)).toEqual([
      ['X', []],
      ['A', [['B', []]]],
    ]);
  });
});

describe('placementsAfterMove', () => {
  // Dropping NY under CA, after LA: only NY changes parent; LA keeps sort order 0.
  it('returns only the members of the new sibling group that changed', () => {
    const next = buildMemberTree([...members.filter(m => m.memberId !== 2), member(2, 'NY', 3, 1)]);

    expect(placementsAfterMove(members, next, 2)).toEqual([{ memberId: 2, parentMemberId: 3, sortOrder: 1 }]);
  });

  // Reordering roots renumbers the group by position.
  it('renumbers the siblings whose position changed', () => {
    const next = buildMemberTree(members);
    const reordered = [next[1], next[0]];

    expect(placementsAfterMove(members, reordered, 5)).toEqual([
      { memberId: 5, parentMemberId: null, sortOrder: 0 },
      { memberId: 1, parentMemberId: null, sortOrder: 1 },
    ]);
  });
});

describe('filterMemberTree', () => {
  it('keeps matches and the ancestors that lead to them', () => {
    expect(shape(filterMemberTree(buildMemberTree(members), 'la'))).toEqual([['US', [['CA', [['LA', []]]]]]]);
  });
});

describe('selfAndDescendants', () => {
  // The parent picker must not offer a member's own subtree.
  it('collects a member and everything below it', () => {
    expect([...selfAndDescendants(members, 1)].sort()).toEqual([1, 2, 3, 4]);
  });
});
