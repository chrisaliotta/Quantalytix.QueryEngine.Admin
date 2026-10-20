/**
 * Why these tests exist: the Lines tab edits a report version's line tree locally and saves it in one replace, which
 * the API accepts only as a list in pre-order with every parent named before its children. They pin down that the stored
 * lines nest by their parents with their value columns named by key, that the saved list is in pre-order with each
 * line's parent taken from where it sits in the tree, and that deleting a line hands its children to its parent.
 */
import { describe, expect, it } from 'vitest';
import type { TreeItems } from '@registry/components/Nestable';
import { buildLineTree, toLineInputs, withLine, withoutLine } from './lineTree';
import type { ReportLine, ReportLineInput } from './types';

function line(reportLineId: number, lineKey: string, parentReportLineId: number | null, sortOrder: number): ReportLine {
  return {
    reportLineId,
    reportVersionId: 61,
    lineKey,
    displayName: lineKey,
    lineKind: 'member',
    parentReportLineId,
    reportMemberKey: lineKey.toUpperCase(),
    sortOrder,
    isVisible: true,
  };
}

/** The tree as nested [key, children] pairs. */
function shape(tree: TreeItems<ReportLineInput>): unknown[] {
  return tree.map(item => [item.data?.lineKey, shape(item.children)]);
}

const lines = [
  line(1, 'assets', null, 1),
  line(2, 'loans', 1, 2),
  line(3, 'cash', 1, 1),
  line(4, 'mortgage', 2, 1),
  { ...line(5, 'nim', null, 2), lineKind: 'formula', reportMemberKey: null, calculatedMeasureKey: 'NIM', valueColumnId: 7 },
];
const tree = buildLineTree(lines, new Map([[7, 'yield']]));

describe('buildLineTree', () => {
  it('nests lines by parent, siblings by sort order, and names value columns by key', () => {
    expect(shape(tree)).toEqual([
      ['assets', [['cash', []], ['loans', [['mortgage', []]]]]],
      ['nim', []],
    ]);
    expect(tree[1].data).toMatchObject({ reportLineId: 5, calculatedMeasureKey: 'NIM', valueColumnKey: 'yield' });
  });
});

describe('toLineInputs', () => {
  it('lists lines in pre-order with the parent each sits under', () => {
    expect(toLineInputs(tree).map(input => [input.lineKey, input.parentLineKey])).toEqual([
      ['assets', null],
      ['cash', 'assets'],
      ['loans', 'assets'],
      ['mortgage', 'loans'],
      ['nim', null],
    ]);
  });

  // A drag moves an item in the tree; the saved parent must follow the tree, not the line's stored parent.
  it('takes each parent from the tree after a move', () => {
    const moved = withLine(withoutLine(tree, 4), null, { id: 4, data: tree[0].children[1].children[0].data, children: [] });

    expect(toLineInputs(moved).find(input => input.lineKey === 'mortgage')?.parentLineKey).toBeNull();
  });
});

describe('withoutLine', () => {
  it('hands a deleted line its children to its parent, in its place', () => {
    expect(shape(withoutLine(tree, 2))).toEqual([
      ['assets', [['cash', []], ['mortgage', []]]],
      ['nim', []],
    ]);
  });
});
