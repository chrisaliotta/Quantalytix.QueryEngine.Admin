import type { TreeItems } from '@registry/components/Nestable';
import type { ReportDimensionMember, ReportLine, ReportLineInput, ReportMemberOption, ReportMemberOptionInput } from './types';

/** A row of a report version: a member of the report dimension's version with its option, or one of the version's overlay rows. */
export interface ReportRowNode {
  key: string;
  /** The member's own name, or the overlay row's. */
  name: string;
  sortOrder: number;
  /** The member's option; null for a member shown as it is, and for an overlay row. */
  option: ReportMemberOptionInput | null;
  /** The overlay row; null for a member. */
  line: ReportLineInput | null;
}

/** Compares keys by UTF-16 code unit, as the engine's ordinal comparison does. */
function compareOrdinal(left: string, right: string) {
  if (left === right) {
    return 0;
  }
  return left < right ? -1 : 1;
}

/** Orders sibling rows as the engine lays them out: by sort order, members before overlay rows, then key. */
function bySiblingOrder(left: ReportRowNode, right: ReportRowNode) {
  return (
    left.sortOrder - right.sortOrder || Number(left.line !== null) - Number(right.line !== null) || compareOrdinal(left.key, right.key)
  );
}

/**
 * Nests a report version's rows as the engine lays them out: the members of the report dimension's version under their
 * parents, and each overlay row under the member or overlay row its parent key names, or at the top when it names none.
 * An overlay row whose parent the version lacks also shows at the top, so it stays editable; publishing reports it.
 */
export function buildRowTree(
  members: ReportDimensionMember[],
  lines: ReportLineInput[],
  options: ReportMemberOptionInput[],
  collapsedKeys: ReadonlySet<string>
): TreeItems<ReportRowNode> {
  const memberKeys = new Map(members.map(member => [member.memberId, member.memberKey]));
  const optionsByKey = new Map(options.map(option => [option.memberKey, option]));
  const placed = [
    ...members.map(member => ({
      parentKey: member.parentMemberId == null ? null : (memberKeys.get(member.parentMemberId) ?? null),
      node: {
        key: member.memberKey,
        name: member.displayName,
        sortOrder: member.sortOrder,
        option: optionsByKey.get(member.memberKey) ?? null,
        line: null,
      },
    })),
    ...lines.map(line => ({
      parentKey: line.parentKey ?? null,
      node: { key: line.lineKey, name: line.displayName, sortOrder: line.sortOrder, option: null, line },
    })),
  ];
  const keys = new Set(placed.map(entry => entry.node.key));

  function under(parentKey: string | null): TreeItems<ReportRowNode> {
    return placed
      .filter(entry => (entry.parentKey !== null && keys.has(entry.parentKey) ? entry.parentKey : null) === parentKey)
      .map(entry => entry.node)
      .sort(bySiblingOrder)
      .map(node => ({ id: node.key, data: node, collapsed: collapsedKeys.has(node.key), children: under(node.key) }));
  }

  return under(null);
}

/** The rows under a parent key, or the top rows when it is null. */
function rowsUnder(tree: TreeItems<ReportRowNode>, parentKey: string | null): TreeItems<ReportRowNode> {
  if (parentKey === null) {
    return tree;
  }
  return tree.flatMap(item => (item.id === parentKey ? item.children : rowsUnder(item.children, parentKey)));
}

/** The sort order that places a new row after the rows already under its parent. */
export function nextSortOrder(tree: TreeItems<ReportRowNode>, parentKey: string | null) {
  return Math.max(-1, ...rowsUnder(tree, parentKey).map(item => (item.data as ReportRowNode).sortOrder)) + 1;
}

/** A stored overlay row as the replace body takes it: its value column named by key, its ID kept. */
export function toLineInput(line: ReportLine, columnKeys: ReadonlyMap<number, string>): ReportLineInput {
  return {
    reportLineId: line.reportLineId,
    lineKey: line.lineKey,
    displayName: line.displayName,
    lineKind: line.lineKind,
    parentKey: line.parentKey ?? null,
    calculatedMeasureKey: line.calculatedMeasureKey ?? null,
    valueColumnKey: line.valueColumnId == null ? null : (columnKeys.get(line.valueColumnId) ?? null),
    sortOrder: line.sortOrder,
    isVisible: line.isVisible,
    payloadJson: line.payloadJson ?? null,
    settingsJson: line.settingsJson ?? null,
  };
}

/** A stored member option as the replace body takes it. */
export function toMemberOptionInput(option: ReportMemberOption): ReportMemberOptionInput {
  return {
    memberKey: option.memberKey,
    displayName: option.displayName ?? null,
    isVisible: option.isVisible,
    showsValues: option.showsValues,
  };
}

/** Orders overlay rows so each comes after the overlay row it sits under, as the replace body requires; siblings keep their order. */
function parentsFirst(lines: ReportLineInput[]): ReportLineInput[] {
  const overlayKeys = new Set(lines.map(line => line.lineKey));
  function under(parentKey: string | null): ReportLineInput[] {
    return lines
      .filter(line => (line.parentKey != null && overlayKeys.has(line.parentKey) ? line.parentKey : null) === parentKey)
      .flatMap(line => [line, ...under(line.lineKey)]);
  }
  return under(null);
}

/**
 * The overlay rows with one row saved, as the replace body takes them. A new row is added; an edited row is replaced, and
 * the rows under it follow a new key.
 */
export function withOverlayRow(lines: ReportLineInput[], previousKey: string | null, row: ReportLineInput): ReportLineInput[] {
  if (previousKey === null) {
    return parentsFirst([...lines, row]);
  }
  const follow = (line: ReportLineInput) => (line.parentKey === previousKey ? { ...line, parentKey: row.lineKey } : line);
  return parentsFirst(lines.map(line => (line.lineKey === previousKey ? row : follow(line))));
}

/** The overlay rows without one row; the rows under it move up to its parent. */
export function withoutOverlayRow(lines: ReportLineInput[], key: string): ReportLineInput[] {
  const parentKey = lines.find(line => line.lineKey === key)?.parentKey ?? null;
  return lines.filter(line => line.lineKey !== key).map(line => (line.parentKey === key ? { ...line, parentKey } : line));
}

/** An overlay row and every overlay row under it, none of which can become its parent. */
export function selfAndBelow(lines: ReportLineInput[], key: string): string[] {
  return [key, ...lines.filter(line => line.parentKey === key).flatMap(line => selfAndBelow(lines, line.lineKey))];
}

/** The member options with one member's option saved; an option that changes nothing is dropped, because a member without one shows the same. */
export function withMemberOption(options: ReportMemberOptionInput[], option: ReportMemberOptionInput): ReportMemberOptionInput[] {
  const others = options.filter(other => other.memberKey !== option.memberKey);
  const changesNothing = option.displayName == null && option.isVisible && option.showsValues;
  return changesNothing ? others : [...others, option];
}
