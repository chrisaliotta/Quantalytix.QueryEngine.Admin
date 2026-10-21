/**
 * Why these tests exist: a data row takes each bridge source's values through rules, each an item or a folder of items with a
 * factor, and the rules of every row add up. They pin down how the picker takes a folder, as one rule that covers the items under
 * it (and later ones) or as its current items, that a covered item takes no rule of its own, and what saving sends: the row's
 * complete rule list, keeping each kept rule's factor. They also pin down how far each item reaches across rows, so the editor can
 * say how many times the report's total counts it, as when a folder's row takes an item back out at -1 and another row takes it
 * whole, and how a factor reads. A row also shows one section per source: a bridge source's rules, or a direct source's field.
 */
import { describe, expect, it } from 'vitest';
import {
  factorText,
  itemReach,
  memberTree,
  netWeight,
  otherRows,
  rowItems,
  rulesFrom,
  sourceSections,
  toggledRules,
  unevenItems,
} from './itemMappings';
import type { DimensionMember, EditorRow, EditorSource, EditorView, SourceSelection } from './types';

function member(memberId: number, memberKey: string, parentMemberId: number | null, isAssignable: boolean): DimensionMember {
  return { memberId, dimensionVersionId: 3, memberKey, displayName: memberKey, parentMemberId, isAssignable, sortOrder: memberId };
}

function row(key: string, items: EditorRow['items']): EditorRow {
  return { key, label: key, kind: 'data', sortOrder: 0, isVisible: true, showsValues: true, items };
}

function rule(itemKey: string, factor: number, selection: SourceSelection = 'member', reportSourceId = 7): EditorRow['items'][number] {
  return { reportSourceId, itemKey, selection, factor };
}

function source(reportSourceId: number, mappingMethod: 'bridge' | 'direct'): EditorSource {
  return {
    source: {
      reportSourceId,
      reportId: 60,
      sourceKey: `source_${reportSourceId}`,
      displayName: `Source ${reportSourceId}`,
      datasetId: 1,
      mappingMethod,
      fieldKey: mappingMethod === 'direct' ? 'product' : null,
      timeFieldKey: 'as_of_date',
    },
    datasetKey: 'gl',
    variables: [],
  };
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

describe('toggledRules', () => {
  it('takes a folder as one rule that replaces the rules under it, and ignores an item the folder covers', () => {
    const picked = toggledRules(new Map([['1100', 'member']]), new Set(['1100', 'ASSETS']), 'folder', members);
    expect(picked).toEqual(new Map([['ASSETS', 'descendants']]));

    expect(toggledRules(picked, new Set(['ASSETS', '1000']), 'folder', members)).toEqual(picked);
    expect(toggledRules(picked, new Set(), 'folder', members)).toEqual(new Map());
  });

  it("takes a folder's current items alone, clears them when picked again, and completes a partly picked folder", () => {
    const picked = toggledRules(new Map(), new Set(['ASSETS']), 'items', members);
    expect([...picked].sort()).toEqual([
      ['1000', 'member'],
      ['1100', 'member'],
      ['1200', 'member'],
    ]);

    expect(toggledRules(picked, new Set([...picked.keys(), 'LOANS']), 'items', members)).toEqual(new Map([['1000', 'member']]));
    expect([...toggledRules(new Map([['1100', 'member']]), new Set(['1100', 'LOANS']), 'items', members).keys()].sort()).toEqual([
      '1100',
      '1200',
    ]);
  });

  it('keeps items a folder rule leaves uncovered when it takes the current items of a folder above', () => {
    const picked = toggledRules(new Map([['LOANS', 'descendants']]), new Set(['LOANS', 'ASSETS']), 'items', members);

    expect(picked).toEqual(
      new Map<string, SourceSelection>([
        ['LOANS', 'descendants'],
        ['1000', 'member'],
      ])
    );
  });
});

describe('rulesFrom', () => {
  it('keeps the factor of a rule the row had with the same selection, and starts new rules whole, by item key', () => {
    const cash = row('CASH', [rule('1000', -1), rule('LOANS', 0.5, 'descendants'), rule('1000', 1, 'member', 8)]);
    const picked = new Map<string, SourceSelection>([
      ['LOANS', 'descendants'],
      ['1000', 'member'],
      ['ASSETS', 'descendants'],
    ]);

    expect(rulesFrom(cash, 7, picked)).toEqual([
      { itemKey: '1000', selection: 'member', factor: -1 },
      { itemKey: 'ASSETS', selection: 'descendants', factor: 1 },
      { itemKey: 'LOANS', selection: 'descendants', factor: 0.5 },
    ]);
  });
});

describe('itemReach and unevenItems', () => {
  it('counts an item taken back out of a folder row and added whole elsewhere once, and an item added into two rows twice', () => {
    const rows = [
      row('LOANS', [rule('ASSETS', 1, 'descendants'), rule('1000', -1)]),
      row('CASH', [rule('1000', 1), rule('1100', 1)]),
      row('OTHER', [rule('1200', 0.5, 'member', 8)]),
    ];

    const reach = itemReach(rows, 7, members);

    expect(netWeight(reach.get('1000')!)).toBe(1);
    expect(reach.get('1200')!.map(reached => [reached.row.key, reached.factor, reached.folder])).toEqual([['LOANS', 1, 'ASSETS']]);
    expect(unevenItems(reach).map(item => [item.itemKey, item.weight])).toEqual([['1100', 2]]);
  });
});

describe('otherRows', () => {
  it('names the other rows that take a member, by its own rule or a folder rule above it', () => {
    const loans = row('LOANS', [rule('ASSETS', 1, 'descendants')]);
    const cash = row('CASH', [rule('1100', 1)]);

    expect(otherRows([loans, cash], cash, 7, members, '1100')).toEqual(['LOANS']);
    expect(otherRows([loans, cash], loans, 7, members, '1100')).toEqual(['CASH']);
    expect(otherRows([loans, cash], cash, 7, members, 'MEMO')).toEqual(['LOANS']);
  });
});

describe('factorText', () => {
  it('reads the usual factors by name and any other as it is', () => {
    expect([1, -1, 0.5, -0.25].map(factorText)).toEqual(['×1', '×−1', '×½', '×−0.25']);
  });
});

describe('sourceSections', () => {
  it("shows a bridge source's rules on the row and a direct source's field matched against the row key", () => {
    const cash = row('CASH', [rule('1000', -1), rule('LOANS', 1, 'descendants')]);
    const view = { sources: [source(7, 'bridge'), source(9, 'direct')] } as EditorView;

    expect(sourceSections(view, cash).map(section => (section.method === 'bridge' ? section.items : [section.fieldKey, section.rowKey]))).toEqual([
      rowItems(cash, 7),
      ['product', 'CASH'],
    ]);
  });
});
