/**
 * Why these tests exist: a report's rows are the members of its report dimension with its overlay rows among them, and the
 * Rows & mappings tab must show them in the order the engine lays them out: siblings by sort order, members
 * before overlay rows, then key. The tree is saved in one replace of every row, so they also pin down what a drag and drop
 * sends (the dropped row's new parent and its sibling group renumbered as shown), which drops the tree refuses because the
 * API would (nothing under a formula row, member rows only under member rows, one member row at the top), that deleting a
 * row hands its children to its parent, that each kind is offered only the parents it can have, and that a new row's key is
 * a well-formed key unique in the report. Because every save replaces the whole tree, a stored row must carry its style and own
 * parameter values back, or a drag would erase them. A row without a value of a parameter inherits as the engine resolves it, its
 * nearest ancestor's, a formula row the total's, else the model's default, which the row sheet shows; and the sheet shows the API's
 * errors about its row on its own fields.
 */
import { describe, expect, it } from 'vitest';
import type { TreeItems } from '@registry/components/Nestable';
import { ApiProblem } from '@/api/problemDetails';
import {
  buildRowTree,
  canPlace,
  inheritedValue,
  keyFromLabel,
  nextSortOrder,
  parentsFor,
  rowProblem,
  rowsAfterMove,
  toRowInput,
  withoutRow,
} from './rowTree';
import type { ModelParameter, ReportRowInput } from './types';

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

describe('toRowInput', () => {
  it('carries a stored row\'s style and own parameter values back into the tree', () => {
    const input = toRowInput({
      key: 'LOANS',
      label: 'Loans',
      kind: 'group',
      sortOrder: 2,
      isVisible: true,
      showsValues: false,
      styleKey: 'subtotals',
      parameters: { dayCount: 'actual_360', scale: 1000 },
      items: [],
    });

    expect([input.styleKey, input.parameters]).toEqual(['subtotals', { dayCount: 'actual_360', scale: 1000 }]);
  });
});

describe('inheritedValue', () => {
  const dayCount: ModelParameter = { parameterKey: 'dayCount', displayName: 'Day count', valueType: 'text', defaultText: 'actual_actual' };
  const tree = [
    { ...row('TOTAL', 'group', null, 0), parameters: { dayCount: 'actual_365' } },
    { ...row('LOANS', 'group', 'TOTAL', 0), showsValues: false, isVisible: false },
    { ...row('MORTGAGE', 'data', 'LOANS', 0), parameters: { dayCount: 'thirty_360_us' } },
    row('nim', 'formula', null, 1),
  ];

  it('takes the nearest ancestor\'s own value, through hidden rows and headers', () => {
    expect(inheritedValue(tree, 'data', 'LOANS', dayCount)).toBe('actual_365');
    expect(inheritedValue(tree, 'section', 'MORTGAGE', dayCount)).toBe('thirty_360_us');
  });

  // The engine evaluates a formula row at the report's total wherever it sits, so it reads the total's value, never its parent's.
  it('takes the total\'s value for a formula row wherever it sits, and for a section at the top', () => {
    expect(inheritedValue(tree, 'formula', 'MORTGAGE', dayCount)).toBe('actual_365');
    expect(inheritedValue(tree, 'formula', null, dayCount)).toBe('actual_365');
    expect(inheritedValue(tree, 'section', null, dayCount)).toBe('actual_365');
  });

  it('takes the model\'s default of the parameter\'s type when no ancestor sets a value, or for the total', () => {
    const scale: ModelParameter = { parameterKey: 'scale', displayName: 'Scale', valueType: 'number', defaultNumber: 1000 };

    expect(inheritedValue([row('TOTAL', 'group', null, 0)], 'data', 'TOTAL', dayCount)).toBe('actual_actual');
    expect(inheritedValue(tree, 'group', null, dayCount)).toBe('actual_actual');
    expect(inheritedValue(tree, 'data', 'MORTGAGE', scale)).toBe(1000);
  });
});

describe('rowProblem', () => {
  it('keys the errors of the sheet\'s row by its fields and keeps every other error as it is', () => {
    const problem = new ApiProblem(400, {
      title: 'One or more validation errors occurred.',
      errors: { 'rows[2].styleKey': ['No such style.'], 'rows[2].parameters.dayCount': ['Not allowed.'], 'rows[0].label': ['Empty.'], rows: ['Two roots.'] },
    });

    expect((rowProblem(problem, 2) as ApiProblem).errors).toEqual({
      styleKey: ['No such style.'],
      'parameters.dayCount': ['Not allowed.'],
      'rows[0].label': ['Empty.'],
      rows: ['Two roots.'],
    });
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
