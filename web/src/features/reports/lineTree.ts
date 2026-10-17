import type { UniqueIdentifier } from '@dnd-kit/core';
import type { TreeItem, TreeItems } from '@registry/components/Nestable';
import type { ReportLine, ReportLineInput } from './types';

/** Orders sibling lines as the API lists them: by sort order, then key. */
function byPosition(left: ReportLine, right: ReportLine) {
  return left.sortOrder - right.sortOrder || left.lineKey.localeCompare(right.lineKey);
}

/**
 * Nests a version's stored lines by their parent links, siblings in display order, each as the replace body's line: its
 * calculated measure and value column named by key, its ID kept so a line that keeps its key keeps its ID.
 */
export function buildLineTree(
  lines: ReportLine[],
  measureKeys: ReadonlyMap<number, string>,
  columnKeys: ReadonlyMap<number, string>
): TreeItems<ReportLineInput> {
  function toItem(line: ReportLine): TreeItem<ReportLineInput> {
    return {
      id: line.reportLineId,
      data: {
        reportLineId: line.reportLineId,
        lineKey: line.lineKey,
        displayName: line.displayName,
        lineKind: line.lineKind,
        reportMemberKey: line.reportMemberKey ?? null,
        calculatedMeasureKey: line.calculatedMeasureId == null ? null : (measureKeys.get(line.calculatedMeasureId) ?? null),
        valueColumnKey: line.valueColumnId == null ? null : (columnKeys.get(line.valueColumnId) ?? null),
        isVisible: line.isVisible,
        payloadJson: line.payloadJson ?? null,
        settingsJson: line.settingsJson ?? null,
      },
      children: lines.filter(child => child.parentReportLineId === line.reportLineId).sort(byPosition).map(toItem),
    };
  }

  return lines.filter(line => line.parentReportLineId == null).sort(byPosition).map(toItem);
}

/**
 * The tree as the replace body takes it: every line in pre-order, so each parent comes before its children, with the
 * parent named by its key. The API numbers siblings by their position.
 */
export function toLineInputs(tree: TreeItems<ReportLineInput>, parentLineKey: string | null = null): ReportLineInput[] {
  return tree.flatMap(item => {
    const line = item.data as ReportLineInput;
    return [{ ...line, parentLineKey }, ...toLineInputs(item.children, line.lineKey)];
  });
}

/** The tree without one line; its children take its place among its siblings. */
export function withoutLine(tree: TreeItems<ReportLineInput>, id: UniqueIdentifier): TreeItems<ReportLineInput> {
  return tree.flatMap(item =>
    item.id === id ? withoutLine(item.children, id) : [{ ...item, children: withoutLine(item.children, id) }]
  );
}

/** The tree with a new line as the last child of a parent, or the last root line when the parent is null. */
export function withLine(
  tree: TreeItems<ReportLineInput>,
  parentId: UniqueIdentifier | null,
  line: TreeItem<ReportLineInput>
): TreeItems<ReportLineInput> {
  if (parentId === null) {
    return [...tree, line];
  }
  return tree.map(item =>
    item.id === parentId
      ? { ...item, collapsed: false, children: [...item.children, line] }
      : { ...item, children: withLine(item.children, parentId, line) }
  );
}

/** The tree with one line's values replaced, in place. */
export function withLineValues(
  tree: TreeItems<ReportLineInput>,
  id: UniqueIdentifier,
  values: ReportLineInput
): TreeItems<ReportLineInput> {
  return tree.map(item =>
    item.id === id ? { ...item, data: values } : { ...item, children: withLineValues(item.children, id, values) }
  );
}
