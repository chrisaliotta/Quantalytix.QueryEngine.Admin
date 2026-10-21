/**
 * Why these tests exist: a data row takes the items of each bridge source by picking members of the source's item dimension in a
 * tree, and only members that take source values can be items. They pin down that picking a group picks every item under it (and
 * picking it again clears them), that a disabled leaf is one that takes no values, and what saving sends: the row's complete item
 * list for the source, keeping each kept item's factor, with the items that move here from other rows named so the sheet can say
 * so. A row also shows one section per source: a bridge source's items, or a direct source's field matched against the row key.
 */
import { describe, expect, it } from 'vitest';
import { itemsFrom, memberTree, movedItems, rowItems, sourceSections, toggledItems } from './itemMappings';
import type { DimensionMember, EditorRow, EditorSource, EditorView } from './types';

function member(memberId: number, memberKey: string, parentMemberId: number | null, isAssignable: boolean): DimensionMember {
  return { memberId, dimensionVersionId: 3, memberKey, displayName: memberKey, parentMemberId, isAssignable, sortOrder: memberId };
}

function row(key: string, items: EditorRow['items']): EditorRow {
  return { key, label: key, kind: 'data', sortOrder: 0, isVisible: true, showsValues: true, items };
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

describe('itemsFrom and movedItems', () => {
  it("keeps the factor of an item the row had, starts a moved item at 1, and names the row it moves from, one source at a time", () => {
    const cash = row('CASH', [
      { reportSourceId: 7, itemKey: '1000', factor: -1 },
      { reportSourceId: 8, itemKey: '1000', factor: 1 },
    ]);
    const loans = row('LOANS', [{ reportSourceId: 7, itemKey: '1100', factor: 1 }]);
    const picked = new Set(['1000', '1100']);

    expect(itemsFrom(cash, 7, picked)).toEqual([
      { itemKey: '1000', factor: -1 },
      { itemKey: '1100', factor: 1 },
    ]);
    expect(movedItems([cash, loans], cash, 7, picked)).toEqual([{ itemKey: '1100', from: loans }]);
    expect(movedItems([cash, loans], cash, 8, picked)).toEqual([]);
  });
});

describe('sourceSections', () => {
  it("shows a bridge source's items on the row and a direct source's field matched against the row key", () => {
    const cash = row('CASH', [{ reportSourceId: 7, itemKey: '1000', factor: -1 }]);
    const view = { sources: [source(7, 'bridge'), source(9, 'direct')] } as EditorView;

    expect(sourceSections(view, cash).map(section => (section.method === 'bridge' ? section.items : [section.fieldKey, section.rowKey]))).toEqual([
      rowItems(cash, 7),
      ['product', 'CASH'],
    ]);
  });
});
