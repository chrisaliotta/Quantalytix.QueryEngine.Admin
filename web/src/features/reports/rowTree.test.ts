/**
 * Why these tests exist: a report's rows are the members of its dimension's version, with the version's overlay rows placed
 * among them, and the Rows tab must show them in the order the engine lays them out: siblings by sort order, members before
 * overlay rows, then key. Overlay rows are saved in one replace that the API accepts only with every overlay parent before
 * its children, so they also pin down that a saved list keeps that order, that a renamed row takes the rows under it along,
 * that deleting a row hands its children to its parent, and that an option that changes nothing is not stored.
 */
import { describe, expect, it } from 'vitest';
import type { TreeItems } from '@registry/components/Nestable';
import {
  buildRowTree,
  nextSortOrder,
  selfAndBelow,
  withMemberOption,
  withOverlayRow,
  withoutOverlayRow,
  type ReportRowNode,
} from './rowTree';
import type { ReportDimensionMember, ReportLineInput } from './types';

function member(memberId: number, memberKey: string, parentMemberId: number | null, sortOrder: number): ReportDimensionMember {
  return { memberId, dimensionVersionId: 7, memberKey, displayName: memberKey, parentMemberId, isAssignable: true, sortOrder };
}

function overlay(lineKey: string, parentKey: string | null, sortOrder: number): ReportLineInput {
  return { reportLineId: null, lineKey, displayName: lineKey, lineKind: 'section', parentKey, sortOrder, isVisible: true };
}

/** The tree as nested [key, children] pairs. */
function shape(tree: TreeItems<ReportRowNode>): unknown[] {
  return tree.map(item => [item.id, shape(item.children)]);
}

const members = [member(1, 'TOTAL', null, 0), member(2, 'LOANS', 1, 2), member(3, 'CASH', 1, 1), member(4, 'AUTO', 2, 0)];

describe('buildRowTree', () => {
  it('nests overlay rows under their parents among the members, in the engine order', () => {
    const lines = [overlay('liquidity', 'TOTAL', 1), { ...overlay('nim', null, 1), lineKind: 'formula' }, overlay('detail', 'liquidity', 0)];

    expect(shape(buildRowTree(members, lines, [], new Set()))).toEqual([
      ['TOTAL', [['CASH', []], ['liquidity', [['detail', []]]], ['LOANS', [['AUTO', []]]]]],
      ['nim', []],
    ]);
  });

  it("shows an overlay row whose parent the version lacks at the top, and carries each member's option", () => {
    const tree = buildRowTree(members, [overlay('orphan', 'GONE', 0)], [{ memberKey: 'LOANS', isVisible: true, showsValues: false }], new Set());

    expect(tree.map(item => item.id)).toEqual(['TOTAL', 'orphan']);
    expect(tree[0].children[1].data?.option).toEqual({ memberKey: 'LOANS', isVisible: true, showsValues: false });
  });
});

describe('nextSortOrder', () => {
  it('places a new row after the rows already under its parent', () => {
    const tree = buildRowTree(members, [overlay('liquidity', 'TOTAL', 4)], [], new Set());

    expect(nextSortOrder(tree, 'TOTAL')).toBe(5);
    expect(nextSortOrder(tree, 'AUTO')).toBe(0);
  });
});

describe('withOverlayRow', () => {
  it('saves a row after the overlay row it sits under, and moves the rows under a renamed row along', () => {
    const lines = [overlay('child', 'later', 0), overlay('later', 'TOTAL', 0)];

    expect(withOverlayRow([overlay('later', 'TOTAL', 0)], null, overlay('child', 'later', 0)).map(line => line.lineKey)).toEqual([
      'later',
      'child',
    ]);
    expect(withOverlayRow(lines, 'later', overlay('renamed', 'TOTAL', 0)).map(line => [line.lineKey, line.parentKey])).toEqual([
      ['renamed', 'TOTAL'],
      ['child', 'renamed'],
    ]);
  });
});

describe('withoutOverlayRow', () => {
  it('hands the rows under a deleted row to its parent', () => {
    const lines = [overlay('section', 'TOTAL', 0), overlay('child', 'section', 0)];

    expect(withoutOverlayRow(lines, 'section')).toEqual([overlay('child', 'TOTAL', 0)]);
  });
});

describe('selfAndBelow', () => {
  it('lists a row and every overlay row under it, which cannot become its parent', () => {
    const lines = [overlay('a', null, 0), overlay('b', 'a', 0), overlay('c', 'b', 0), overlay('d', null, 1)];

    expect(selfAndBelow(lines, 'a')).toEqual(['a', 'b', 'c']);
  });
});

describe('withMemberOption', () => {
  it('stores an option that changes how the member shows, and drops one that changes nothing', () => {
    const header = { memberKey: 'LOANS', displayName: null, isVisible: true, showsValues: false };

    expect(withMemberOption([], header)).toEqual([header]);
    expect(withMemberOption([header], { ...header, showsValues: true })).toEqual([]);
  });
});
