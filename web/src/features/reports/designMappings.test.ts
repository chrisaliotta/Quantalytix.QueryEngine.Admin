/**
 * Why these tests exist: a design row takes the items of its sources by picking members of each source's bound dimension in a
 * tree, and only members that take source values can be items. They pin down that picking a group picks every item under it
 * (and picking it again clears them), that a disabled leaf is one that takes no values, and what saving sends: the row's
 * complete list, keeping each kept item's factor, with the items that move here from other rows named so the sheet can say so.
 */
import { describe, expect, it } from 'vitest';
import { memberTree, movedItems, rowMappingsFrom, rowSelections, toggledItems } from './designMappings';
import type { DesignRow, DimensionMember, ReportDesign } from './types';

function member(memberId: number, memberKey: string, parentMemberId: number | null, isAssignable: boolean): DimensionMember {
  return { memberId, dimensionVersionId: 3, memberKey, displayName: memberKey, parentMemberId, isAssignable, sortOrder: memberId };
}

function row(key: string, mappings: DesignRow['mappings']): DesignRow {
  return { key, label: key, kind: 'data', sortOrder: 0, isVisible: true, showsValues: true, mappings, mappedCodes: [] };
}

const members = [
  member(1, 'ASSETS', null, false),
  member(2, 'LOANS', 1, false),
  member(3, '1100', 2, true),
  member(4, '1200', 2, true),
  member(5, '1000', 1, true),
  member(6, 'MEMO', 1, false),
];

describe('memberTree', () => {
  it('nests members by sort order and disables a leaf that takes no source values', () => {
    const [assets] = memberTree(members);

    expect(assets.children?.map(node => [node.id, node.disabled, node.children?.map(child => child.id)])).toEqual([
      ['LOANS', false, ['1100', '1200']],
      ['1000', false, undefined],
      ['MEMO', true, undefined],
    ]);
  });
});

describe('toggledItems', () => {
  it('picks the items under a group, clears them when the group is picked again, and clears one item alone', () => {
    const picked = toggledItems(new Set(), new Set(['ASSETS']), members);
    expect([...picked].sort()).toEqual(['1000', '1100', '1200']);

    expect(toggledItems(picked, new Set([...picked, 'LOANS']), members)).toEqual(new Set(['1000']));
    expect(toggledItems(picked, new Set(['1000', '1200']), members)).toEqual(new Set(['1000', '1200']));
  });

  it('completes a partly picked group rather than clearing it', () => {
    expect([...toggledItems(new Set(['1100']), new Set(['1100', 'LOANS']), members)].sort()).toEqual(['1100', '1200']);
  });
});

describe('rowMappingsFrom and movedItems', () => {
  it('keeps the factor of an item the row had, starts a moved item at 1, and names the row it moves from', () => {
    const cash = row('CASH', [{ bridgeId: 7, itemKey: '1000', factor: -1 }]);
    const loans = row('LOANS', [{ bridgeId: 7, itemKey: '1100', factor: 1 }]);
    const design = { rows: [cash, loans] } as ReportDesign;
    const selections = rowSelections(cash).set(7, new Set(['1000', '1100']));

    expect(rowMappingsFrom(cash, selections)).toEqual([
      { bridgeId: 7, itemKey: '1000', factor: -1 },
      { bridgeId: 7, itemKey: '1100', factor: 1 },
    ]);
    expect(movedItems(design, cash, selections)).toEqual([{ bridgeId: 7, itemKey: '1100', from: loans }]);
  });
});
