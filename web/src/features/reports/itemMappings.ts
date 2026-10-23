import type { TreeNode } from '@registry/components/TreeView';
import type { DimensionMember, EditorRow, EditorSource, EditorView, SourceItemInput, SourceSelection } from './types';

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
  function under(parentDimensionMemberId: number | null): TreeNode<DimensionMember>[] {
    return members
      .filter(member => (member.parentDimensionMemberId ?? null) === parentDimensionMemberId)
      .sort((left, right) => left.sortOrder - right.sortOrder || compareOrdinal(left.memberKey, right.memberKey))
      .map(member => {
        const children = under(member.dimensionMemberId);
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

function memberOf(members: DimensionMember[], key: string) {
  return members.find(member => member.memberKey === key)!;
}

/** Whether a member has members under it: a folder, whose rule can take every item under it. */
export function isFolder(members: DimensionMember[], key: string) {
  const folder = memberOf(members, key);
  return members.some(member => member.parentDimensionMemberId === folder.dimensionMemberId);
}

/** The items of a member's branch: the member itself when it takes source values, and every such member under it. */
export function branchItems(members: DimensionMember[], key: string): string[] {
  const member = memberOf(members, key);
  const below = members.filter(child => child.parentDimensionMemberId === member.dimensionMemberId).flatMap(child => branchItems(members, child.memberKey));
  return member.isAssignable ? [key, ...below] : below;
}

/** The keys of the members above a member, nearest first. */
function ancestors(members: DimensionMember[], key: string): string[] {
  const byId = new Map(members.map(member => [member.dimensionMemberId, member]));
  const keys: string[] = [];
  for (let parentId = memberOf(members, key).parentDimensionMemberId; parentId != null; parentId = byId.get(parentId)!.parentDimensionMemberId) {
    keys.push(byId.get(parentId)!.memberKey);
  }
  return keys;
}

/** How the item picker takes a folder: as one rule that follows the members added under it later, or as its current items. */
export type FolderPick = 'folder' | 'items';

/** A row's rules of one source, as the items body takes them back. */
export function rowItems(row: EditorRow, sourceId: number): SourceItemInput[] {
  return row.items
    .filter(item => item.reportSourceId === sourceId)
    .map(({ itemKey, selection, factor }) => ({ itemKey, selection, factor }));
}

/** A row's rules of one source as the picker edits them: the selection of each rule, by item key. */
export function pickedRules(row: EditorRow, sourceId: number): Map<string, SourceSelection> {
  return new Map(rowItems(row, sourceId).map(item => [item.itemKey, item.selection as SourceSelection]));
}

/** The folder whose rule among the picked rules takes an item from above; such an item takes no rule of its own. */
export function coveringFolder(rules: ReadonlyMap<string, SourceSelection>, key: string, members: DimensionMember[]) {
  return ancestors(members, key).find(ancestor => rules.get(ancestor) === 'descendants');
}

/**
 * The picked rules after the picker's selection changed from the rules' keys to `next`. Clearing a rule removes it. Picking a
 * folder takes it as one rule, which replaces the rules under it because it covers them, or, picking its current items, takes each
 * item under it alone, and clears them when they all were taken already. Picking an item a folder covers changes nothing.
 */
export function toggledRules(
  rules: ReadonlyMap<string, SourceSelection>,
  next: ReadonlySet<string>,
  pick: FolderPick,
  members: DimensionMember[]
): Map<string, SourceSelection> {
  const result = new Map([...rules].filter(([key]) => next.has(key)));
  const added = [...next].filter(key => !rules.has(key) && coveringFolder(rules, key, members) === undefined);
  for (const key of added) {
    if (!isFolder(members, key)) {
      result.set(key, 'member');
    } else if (pick === 'folder') {
      [...result.keys()].filter(other => ancestors(members, other).includes(key)).forEach(other => result.delete(other));
      result.set(key, 'descendants');
    } else {
      const items = branchItems(members, key).filter(item => coveringFolder(result, item, members) === undefined);
      const clears = added.length === 1 && items.every(item => result.has(item));
      items.forEach(item => (clears ? result.delete(item) : result.set(item, 'member')));
    }
  }
  return result;
}

/**
 * A row's complete rule list of one source from the rules picked, as the items body takes it, by item key: a rule the row
 * already had keeps its factor, and a new rule starts with factor 1.
 */
export function rulesFrom(row: EditorRow, sourceId: number, rules: ReadonlyMap<string, SourceSelection>): SourceItemInput[] {
  const factors = new Map(rowItems(row, sourceId).map(item => [`${item.selection}:${item.itemKey}`, item.factor]));
  return [...rules]
    .sort(([left], [right]) => compareOrdinal(left, right))
    .map(([itemKey, selection]) => ({ itemKey, selection, factor: factors.get(`${selection}:${itemKey}`) ?? 1 }));
}

/** The labels of the other rows a member adds into: rows with a rule naming it, or a rule taking a folder above it. */
export function otherRows(rows: EditorRow[], row: EditorRow, sourceId: number, members: DimensionMember[], key: string): string[] {
  const above = ancestors(members, key);
  return rows
    .filter(
      other =>
        other.key !== row.key &&
        rowItems(other, sourceId).some(item => item.itemKey === key || (item.selection === 'descendants' && above.includes(item.itemKey)))
    )
    .map(other => other.label);
}

/** A rule of a source that reaches an item: the row it adds into, its factor, and the folder it takes the item through, if any. */
export interface ItemRule {
  row: EditorRow;
  factor: number;
  folder: string | null;
}

/**
 * The rules of one source that reach each of its items: rules that take the item alone, and rules that take a folder above it.
 * Every rule adds its factor of the item's values into its row, so an item can add into several rows, whole or in part.
 */
export function itemReach(rows: EditorRow[], sourceId: number, members: DimensionMember[]): Map<string, ItemRule[]> {
  const reach = new Map<string, ItemRule[]>(members.filter(member => member.isAssignable).map(member => [member.memberKey, []]));
  for (const row of rows) {
    for (const item of rowItems(row, sourceId)) {
      const folder = item.selection === 'descendants' ? item.itemKey : null;
      const items = folder === null ? [item.itemKey] : branchItems(members, folder);
      items.forEach(itemKey => reach.get(itemKey)!.push({ row, factor: item.factor, folder }));
    }
  }
  return reach;
}

/** The sum of an item's factors: how many times the report's total counts its values, 1 once, 0 not at all, 2 twice. */
export function netWeight(rules: ItemRule[]) {
  return rules.reduce((sum, rule) => sum + rule.factor, 0);
}

/** The items some rule reaches whose factors don't add up to 1, so the report's total counts them other than once. */
export function unevenItems(reach: Map<string, ItemRule[]>): { itemKey: string; rules: ItemRule[]; weight: number }[] {
  return [...reach]
    .filter(([, rules]) => rules.length > 0 && Math.abs(netWeight(rules) - 1) > 1e-9)
    .map(([itemKey, rules]) => ({ itemKey, rules, weight: netWeight(rules) }))
    .sort((left, right) => compareOrdinal(left.itemKey, right.itemKey));
}

/** How a factor reads: ×1, ×−1, ×½, or the factor itself. */
export function factorText(factor: number) {
  if (factor === 0.5) {
    return '×½';
  }
  return `×${String(factor).replace('-', '−')}`;
}

/** What one source contributes to a data row: a bridge source's rules, or the field a direct source matches the row's key in. */
export type SourceSection =
  | { method: 'bridge'; source: EditorSource; items: SourceItemInput[] }
  | { method: 'direct'; source: EditorSource; fieldKey: string; rowKey: string };

/**
 * The sections a data row shows, one per source in the report's source order: a bridge source lists the rules its bridge adds
 * into the row, and a direct source shows that its field's values equal to the row's key are the row's rows.
 */
export function sourceSections(view: EditorView, row: EditorRow): SourceSection[] {
  return view.sources.map(source =>
    source.source.mappingMethod === 'bridge'
      ? { method: 'bridge', source, items: rowItems(row, source.source.reportSourceId) }
      : { method: 'direct', source, fieldKey: source.source.fieldKey ?? '', rowKey: row.key }
  );
}
