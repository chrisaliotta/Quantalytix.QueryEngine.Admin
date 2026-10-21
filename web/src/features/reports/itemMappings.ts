import type { TreeNode } from '@registry/components/TreeView';
import type { DimensionMember, EditorRow, EditorSource, EditorView, SourceItemInput } from './types';

/** Compares keys by UTF-16 code unit, as the API's ordinal comparison does. */
function compareOrdinal(left: string, right: string) {
  if (left === right) {
    return 0;
  }
  return left < right ? -1 : 1;
}

/**
 * Nests an item dimension's members for the item picker: each under its parent, siblings by sort order, then key. A leaf that
 * takes no source values can't be picked, so it is disabled.
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
export function itemRows(rows: EditorRow[], sourceId: number): Map<string, EditorRow> {
  return new Map(
    rows.flatMap(row => row.items.filter(item => item.reportSourceId === sourceId).map(item => [item.itemKey, row] as const))
  );
}

/** A row's items of one source, as the items body takes them back. */
export function rowItems(row: EditorRow, sourceId: number): SourceItemInput[] {
  return row.items.filter(item => item.reportSourceId === sourceId).map(({ itemKey, factor }) => ({ itemKey, factor }));
}

/**
 * A row's complete item list of one source from the items picked, as the items body takes it: an item the row already had keeps
 * its factor, and any other item, including one that moves here from another row, starts with factor 1.
 */
export function itemsFrom(row: EditorRow, sourceId: number, picked: ReadonlySet<string>): SourceItemInput[] {
  const factors = new Map(rowItems(row, sourceId).map(item => [item.itemKey, item.factor]));
  return [...picked].sort(compareOrdinal).map(itemKey => ({ itemKey, factor: factors.get(itemKey) ?? 1 }));
}

/** The picked items of one source that map to another row now, with that row, which saving moves to this one. */
export function movedItems(rows: EditorRow[], row: EditorRow, sourceId: number, picked: ReadonlySet<string>): { itemKey: string; from: EditorRow }[] {
  const placed = itemRows(rows, sourceId);
  return [...picked]
    .filter(itemKey => placed.has(itemKey) && placed.get(itemKey)!.key !== row.key)
    .map(itemKey => ({ itemKey, from: placed.get(itemKey)! }));
}

/** What one source contributes to a data row: a bridge source's items, or the field a direct source matches the row's key in. */
export type SourceSection =
  | { method: 'bridge'; source: EditorSource; items: SourceItemInput[] }
  | { method: 'direct'; source: EditorSource; fieldKey: string; rowKey: string };

/**
 * The sections a data row shows, one per source in the report's source order: a bridge source lists the items its bridge maps to
 * the row, and a direct source shows that its field's values equal to the row's key are the row's rows.
 */
export function sourceSections(view: EditorView, row: EditorRow): SourceSection[] {
  return view.sources.map(source =>
    source.source.mappingMethod === 'bridge'
      ? { method: 'bridge', source, items: rowItems(row, source.source.reportSourceId) }
      : { method: 'direct', source, fieldKey: source.source.fieldKey ?? '', rowKey: row.key }
  );
}
