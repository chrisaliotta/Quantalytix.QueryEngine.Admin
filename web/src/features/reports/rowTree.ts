import type { TreeItems } from '@registry/components/Nestable';
import { ApiProblem } from '@/api/problemDetails';
import { parameterDefault } from '@/shared/modelParameters';
import type { LookupItem } from '@/shared/types';
import type { EditorRow, ModelParameter, ReportRowInput, RowParameterValue } from './types';

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
function bySiblingOrder(left: ReportRowInput, right: ReportRowInput) {
  return (
    left.sortOrder - right.sortOrder ||
    Number(!isMemberRow(left.kind)) - Number(!isMemberRow(right.kind)) ||
    compareOrdinal(left.key, right.key)
  );
}

/** A stored row as the rows body takes it back, with its style and own parameter values, which every replace of the tree must keep. */
export function toRowInput(row: EditorRow): ReportRowInput {
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
    styleKey: row.styleKey ?? null,
    parameters: row.parameters,
  };
}

/**
 * The value a row of a kind under a parent takes for a parameter it sets none of, as the engine resolves it: the own value of its
 * nearest ancestor that sets one, through hidden rows and headers too, or else the model's default. The engine evaluates a formula
 * row at the report's total wherever it sits, so a formula row takes the total's value, and a section at the top continues to it.
 */
export function inheritedValue(rows: ReportRowInput[], kind: string, parentKey: string | null, parameter: ModelParameter): RowParameterValue {
  const byKey = new Map(rows.map(row => [row.key, row]));
  const total = rows.find(row => isMemberRow(row.kind) && row.parentKey == null)?.key ?? null;
  const above = (rowKind: string, rowParentKey: string | null) =>
    rowKind === 'formula' ? total : (rowParentKey ?? (isMemberRow(rowKind) ? null : total));
  let key = above(kind, parentKey);
  while (key !== null) {
    const ancestor = byKey.get(key)!;
    const value = ancestor.parameters?.[parameter.parameterKey];
    if (value !== undefined) {
      return value;
    }
    key = above(ancestor.kind, ancestor.parentKey ?? null);
  }
  return parameterDefault(parameter);
}

/**
 * A failed save of the tree as one row's sheet shows it: the validation errors of the row at `index` in the rows sent, keyed
 * `rows[index].field`, are keyed by the sheet's own field names so they show on its fields; every other error stays in its alert.
 */
export function rowProblem(error: unknown, index: number): unknown {
  if (!(error instanceof ApiProblem) || !error.errors) {
    return error;
  }
  const prefix = `rows[${index}].`;
  const errors = Object.fromEntries(
    Object.entries(error.errors).map(([key, messages]) => [key.startsWith(prefix) ? key.slice(prefix.length) : key, messages])
  );
  return new ApiProblem(error.status, { title: error.title, detail: error.detail, errors, violations: error.violations });
}

/**
 * Nests a report's rows as the engine lays them out: each row under the row its parent key names, siblings by sort order,
 * members before overlay rows, then key. A row whose parent the list lacks shows at the top, so it stays editable; saving
 * reports it.
 */
export function buildRowTree(rows: ReportRowInput[], collapsedKeys: ReadonlySet<string>): TreeItems<ReportRowInput> {
  const keys = new Set(rows.map(row => row.key));

  function under(parentKey: string | null): TreeItems<ReportRowInput> {
    return rows
      .filter(row => (row.parentKey != null && keys.has(row.parentKey) ? row.parentKey : null) === parentKey)
      .sort(bySiblingOrder)
      .map(row => ({ id: row.key, data: row, collapsed: collapsedKeys.has(row.key), children: under(row.key) }));
  }

  return under(null);
}

/** The moved item's parent and siblings in a tree, or null when the tree does not hold it. */
function siblingsOf(
  items: TreeItems<ReportRowInput>,
  key: string,
  parentKey: string | null
): { parentKey: string | null; siblings: TreeItems<ReportRowInput> } | null {
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
export function rowsAfterMove(rows: ReportRowInput[], tree: TreeItems<ReportRowInput>, movedKey: string): ReportRowInput[] {
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
export function nextSortOrder(rows: ReportRowInput[], parentKey: string | null) {
  return Math.max(-1, ...rows.filter(row => (row.parentKey ?? null) === parentKey).map(row => row.sortOrder)) + 1;
}

/** The rows with one row saved: a new row is added, and an edited row, which keeps its key, replaces the stored one. */
export function withRow(rows: ReportRowInput[], previousKey: string | null, row: ReportRowInput): ReportRowInput[] {
  if (previousKey === null) {
    return [...rows, row];
  }
  return rows.map(stored => (stored.key === previousKey ? row : stored));
}

/** The rows without one row; the rows under it move up to its parent. */
export function withoutRow(rows: ReportRowInput[], key: string): ReportRowInput[] {
  const parentKey = rows.find(row => row.key === key)?.parentKey ?? null;
  return rows.filter(row => row.key !== key).map(row => (row.parentKey === key ? { ...row, parentKey } : row));
}

/** A row and every row under it, none of which can become its parent. */
function selfAndBelow(rows: ReportRowInput[], key: string): string[] {
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
export function parentsFor(rows: ReportRowInput[], kind: string, key: string | null): LookupItem[] {
  const excluded = new Set(key === null ? [] : selfAndBelow(rows, key));
  return rows
    .filter(row => !excluded.has(row.key) && takesChild(row.kind, kind))
    .map((row, index) => ({ id: index, key: row.key, label: row.label }));
}

/**
 * Whether a drag and drop may place a row under a parent, or at the top when it is null: under a row that takes its kind,
 * and at the top only an overlay row or the report's total, the one member row there.
 */
export function canPlace(rows: ReportRowInput[], key: string, parentKey: string | null): boolean {
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
