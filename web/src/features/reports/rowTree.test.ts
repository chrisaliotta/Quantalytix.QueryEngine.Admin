/**
 * Why these tests exist: a report's rows are the members of its report dimension with its overlay rows among them, and the
 * Rows & mappings tab must show them in the order the engine lays them out: siblings by sort order, members
 * before overlay rows, then key. The tree is saved in one replace of every row, so they also pin down what a drag and drop
 * sends (the dropped row's new parent and its sibling group renumbered as shown), which drops the tree refuses because the
 * API would (nothing under a formula row, member rows only under member rows, one member row at the top), that deleting a
 * row hands its children to its parent, that each kind is offered only the parents it can have, and that a new row's key is
 * a well-formed key unique in the report.
 */
import { describe, expect, it } from 'vitest';
import type { TreeItems } from '@registry/components/Nestable';
import { buildRowTree, canPlace, keyFromLabel, nextSortOrder, parentsFor, rowsAfterMove, withoutRow } from './rowTree';
import type { ReportRowInput } from './types';

function row(key: string, kind: string, parentKey: string | null, sortOrder: number): ReportRowInput {
  return { key, label: key, kind, parentKey, sortOrder, isVisible: true, showsValues: true, calculatedMeasureKey: null, valueColumnKey: null };
}

/** The tree as nested [key, children] pairs. */
function shape(tree: TreeItems<ReportRowInput>): unknown[] {
  return tree.map(item => [item.id, shape(item.children)]);
}

const rows = [
  row('TOTAL', 'group', null, 0),
  row('LOANS', 'group', 'TOTAL', 2),
  row('CASH', 'data', 'TOTAL', 1),
  row('AUTO', 'data', 'LOANS', 0),
  row('liquidity', 'section', 'TOTAL', 1),
  row('nim', 'formula', null, 1),
];

describe('buildRowTree', () => {
  it('nests rows under their parents in the engine order, members before overlay rows on a tie', () => {
    expect(shape(buildRowTree(rows, new Set()))).toEqual([
      ['TOTAL', [['CASH', []], ['liquidity', []], ['LOANS', [['AUTO', []]]]]],
      ['nim', []],
    ]);
  });

  it('shows a row whose parent the report lacks at the top', () => {
    expect(buildRowTree([row('TOTAL', 'group', null, 0), row('orphan', 'section', 'GONE', 0)], new Set()).map(item => item.id)).toEqual([
      'TOTAL',
      'orphan',
    ]);
  });
});

describe('rowsAfterMove', () => {
  it('gives the dropped row its new parent and renumbers its new siblings as the tree shows them', () => {
    const tree = buildRowTree(rows, new Set());
    const total = tree[0];
    const loans = total.children[2];
    const dropped: TreeItems<ReportRowInput> = [
      { ...total, children: [total.children[0], { ...loans, children: [...loans.children, total.children[1]] }] },
      tree[1],
    ];

    const moved = rowsAfterMove(rows, dropped, 'liquidity');

    expect(moved.filter(item => item.parentKey === 'LOANS').map(item => [item.key, item.sortOrder])).toEqual([
      ['AUTO', 0],
      ['liquidity', 1],
    ]);
    expect(moved.find(item => item.key === 'CASH')).toEqual(rows.find(item => item.key === 'CASH'));
  });
});

describe('canPlace', () => {
  it('keeps rows out of formula rows, member rows under member rows, and only overlay rows and the total at the top', () => {
    expect(canPlace(rows, 'liquidity', 'nim')).toBe(false);
    expect(canPlace(rows, 'AUTO', 'liquidity')).toBe(false);
    expect(canPlace(rows, 'CASH', null)).toBe(false);
    expect(canPlace(rows, 'AUTO', 'TOTAL')).toBe(true);
    expect(canPlace(rows, 'AUTO', 'CASH')).toBe(true);
    expect(canPlace(rows, 'liquidity', 'CASH')).toBe(true);
    expect(canPlace(rows, 'TOTAL', null)).toBe(true);
    expect(canPlace(rows, 'nim', null)).toBe(true);
  });
});

describe('withoutRow', () => {
  it('hands the rows under a deleted row to its parent', () => {
    expect(withoutRow(rows, 'LOANS').find(item => item.key === 'AUTO')?.parentKey).toBe('TOTAL');
  });
});

describe('parentsFor', () => {
  it('offers member rows only member rows, and overlay rows any row but a formula, never the row itself or below it', () => {
    expect(parentsFor(rows, 'data', 'AUTO').map(item => item.key)).toEqual(['TOTAL', 'LOANS', 'CASH']);
    expect(parentsFor(rows, 'group', 'LOANS').map(item => item.key)).toEqual(['TOTAL', 'CASH']);
    expect(parentsFor(rows, 'section', null).map(item => item.key)).toEqual(['TOTAL', 'LOANS', 'CASH', 'AUTO', 'liquidity']);
  });
});

describe('nextSortOrder', () => {
  it('places a new row after the rows already under its parent', () => {
    expect(nextSortOrder(rows, 'TOTAL')).toBe(3);
    expect(nextSortOrder(rows, 'AUTO')).toBe(0);
  });
});

describe('keyFromLabel', () => {
  it('joins the label\'s words in upper case, starts with a letter and numbers a taken key', () => {
    expect(keyFromLabel('Commercial real-estate loans', new Set())).toBe('COMMERCIAL_REAL_ESTATE_LOANS');
    expect(keyFromLabel('2024 adjustments', new Set())).toBe('ROW_2024_ADJUSTMENTS');
    expect(keyFromLabel('Cash', new Set(['CASH', 'CASH_2']))).toBe('CASH_3');
    expect(keyFromLabel('  ', new Set())).toBe('ROW');
  });
});
