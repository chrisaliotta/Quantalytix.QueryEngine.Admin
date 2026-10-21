import type { TreeItems } from '@registry/components/Nestable';
import type { LookupItem } from '@/shared/types';
import type { DesignRow, DesignRowInput } from './types';

/** Compares keys by UTF-16 code unit, as the engine's ordinal comparison does. */
function compareOrdinal(left: string, right: string) {
  if (left === right) {
    return 0;
  }
  return left < right ? -1 : 1;
}

/** Whether a row of a kind is a member of the report dimension (a data or group row) rather than an overlay row. */
export function isMemberRow(kind: string) {
  return kind === 'data' || kind === 'group';
}

/** Orders sibling rows as the engine lays them out: by sort order, members before overlay rows, then key. */
function bySiblingOrder(left: DesignRowInput, right: DesignRowInput) {
  return (
    left.sortOrder - right.sortOrder ||
    Number(!isMemberRow(left.kind)) - Number(!isMemberRow(right.kind)) ||
    compareOrdinal(left.key, right.key)
  );
}

/** A stored row as the rows body takes it back. */
export function toRowInput(row: DesignRow): DesignRowInput {
  return {
    key: row.key,
    label: row.label,
    kind: row.kind,
    parentKey: row.parentKey ?? null,
    sortOrder: row.sortOrder,
    isVisible: row.isVisible,
    showsValues: row.showsValues,
    calculatedMeasureKey: row.calculatedMeasureKey ?? null,
    valueColumnKey: row.valueColumnKey ?? null,
  };
}

/**
 * Nests a design's rows as the engine lays them out: each row under the row its parent key names, siblings by sort order,
 * members before overlay rows, then key. A row whose parent the list lacks shows at the top, so it stays editable; saving
 * reports it.
 */
export function buildRowTree(rows: DesignRowInput[], collapsedKeys: ReadonlySet<string>): TreeItems<DesignRowInput> {
  const keys = new Set(rows.map(row => row.key));

  function under(parentKey: string | null): TreeItems<DesignRowInput> {
    return rows
      .filter(row => (row.parentKey != null && keys.has(row.parentKey) ? row.parentKey : null) === parentKey)
      .sort(bySiblingOrder)
      .map(row => ({ id: row.key, data: row, collapsed: collapsedKeys.has(row.key), children: under(row.key) }));
  }

  return under(null);
}

/** The moved item's parent and siblings in a tree, or null when the tree does not hold it. */
function siblingsOf(
  items: TreeItems<DesignRowInput>,
  key: string,
  parentKey: string | null
): { parentKey: string | null; siblings: TreeItems<DesignRowInput> } | null {
  if (items.some(item => item.id === key)) {
    return { parentKey, siblings: items };
  }
  for (const item of items) {
    const found = siblingsOf(item.children, key, String(item.id));
    if (found) {
      return found;
    }
  }
  return null;
}

/**
 * The rows after a drag and drop: the moved row takes the parent it was dropped under, and its new sibling group is
 * renumbered by position (sort order 0, 1, 2, ...), which is the order the tree showed.
 */
export function rowsAfterMove(rows: DesignRowInput[], tree: TreeItems<DesignRowInput>, movedKey: string): DesignRowInput[] {
  const group = siblingsOf(tree, movedKey, null);
  if (!group) {
    return rows;
  }
  const positions = new Map(group.siblings.map((item, index) => [String(item.id), index]));
  return rows.map(row => {
    const position = positions.get(row.key);
    return position === undefined ? row : { ...row, parentKey: group.parentKey, sortOrder: position };
  });
}

/** The sort order that places a new row after the rows already under its parent, or at the top when it is null. */
export function nextSortOrder(rows: DesignRowInput[], parentKey: string | null) {
  return Math.max(-1, ...rows.filter(row => (row.parentKey ?? null) === parentKey).map(row => row.sortOrder)) + 1;
}

/** The rows with one row saved: a new row is added, and an edited row, which keeps its key, replaces the stored one. */
export function withRow(rows: DesignRowInput[], previousKey: string | null, row: DesignRowInput): DesignRowInput[] {
  if (previousKey === null) {
    return [...rows, row];
  }
  return rows.map(stored => (stored.key === previousKey ? row : stored));
}

/** The rows without one row; the rows under it move up to its parent. */
export function withoutRow(rows: DesignRowInput[], key: string): DesignRowInput[] {
  const parentKey = rows.find(row => row.key === key)?.parentKey ?? null;
  return rows.filter(row => row.key !== key).map(row => (row.parentKey === key ? { ...row, parentKey } : row));
}

/** A row and every row under it, none of which can become its parent. */
function selfAndBelow(rows: DesignRowInput[], key: string): string[] {
  return [key, ...rows.filter(row => row.parentKey === key).flatMap(row => selfAndBelow(rows, row.key))];
}

/**
 * Whether a row of one kind can sit under a row of another: a member row under a member row, because the report dimension's
 * hierarchy holds it, and an overlay row under any row but a formula row.
 */
function takesChild(parentKind: string, childKind: string) {
  return isMemberRow(childKind) ? isMemberRow(parentKind) : parentKind !== 'formula';
}

/** The rows a row of a kind can sit under, as picker items; the row itself and the rows under it are left out. */
export function parentsFor(rows: DesignRowInput[], kind: string, key: string | null): LookupItem[] {
  const excluded = new Set(key === null ? [] : selfAndBelow(rows, key));
  return rows
    .filter(row => !excluded.has(row.key) && takesChild(row.kind, kind))
    .map((row, index) => ({ id: index, key: row.key, label: row.label }));
}

/**
 * Whether a drag and drop may place a row under a parent, or at the top when it is null: under a row that takes its kind,
 * and at the top only an overlay row or the report's total, the one member row there.
 */
export function canPlace(rows: DesignRowInput[], key: string, parentKey: string | null): boolean {
  const row = rows.find(stored => stored.key === key)!;
  if (parentKey === null) {
    return !isMemberRow(row.kind) || row.parentKey == null;
  }
  return takesChild(rows.find(stored => stored.key === parentKey)!.kind, row.kind);
}

/**
 * A new row's key made from its label: the label's letters and digits in upper case, joined by '_', starting with a letter
 * and at most 120 characters, then numbered to differ from every key taken.
 */
export function keyFromLabel(label: string, taken: ReadonlySet<string>): string {
  const words = label.toUpperCase().match(/[A-Z0-9]+/g) ?? [];
  const joined = words.join('_').slice(0, 120);
  const base = /^[A-Z]/.test(joined) ? joined : ['ROW', joined].filter(Boolean).join('_');
  let key = base;
  for (let suffix = 2; taken.has(key); suffix++) {
    key = `${base}_${suffix}`;
  }
  return key;
}
