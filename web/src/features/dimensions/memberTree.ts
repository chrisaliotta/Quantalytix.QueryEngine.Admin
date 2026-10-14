import type { TreeItem, TreeItems } from '@registry/components/Nestable';
import type { DimensionMember, MemberPlacement } from './types';

/** Orders members as the API lists them: by sort order, then key ignoring case. */
function byPresentationOrder(left: DimensionMember, right: DimensionMember) {
  if (left.sortOrder !== right.sortOrder) {
    return left.sortOrder - right.sortOrder;
  }
  return left.memberKey.localeCompare(right.memberKey, 'en', { sensitivity: 'base' });
}

/**
 * Nests a version's members by their parent links, siblings in presentation order. A member whose parent is not in the
 * version, or that sits in a parent cycle, is shown at the root so it stays visible and editable.
 */
export function buildMemberTree(members: DimensionMember[]): TreeItems<DimensionMember> {
  const ids = new Set(members.map(member => member.memberId));
  const children = new Map<number, DimensionMember[]>();
  for (const member of members) {
    const parentId = member.parentMemberId;
    if (parentId != null && ids.has(parentId)) {
      children.set(parentId, [...(children.get(parentId) ?? []), member]);
    }
  }

  const placed = new Set<number>();
  function toItem(member: DimensionMember): TreeItem<DimensionMember> {
    placed.add(member.memberId);
    const unplaced = (children.get(member.memberId) ?? []).filter(child => !placed.has(child.memberId));
    return { id: member.memberId, data: member, children: unplaced.sort(byPresentationOrder).map(toItem) };
  }

  const roots = members
    .filter(member => member.parentMemberId == null || !ids.has(member.parentMemberId))
    .sort(byPresentationOrder)
    .map(toItem);
  for (const member of [...members].sort(byPresentationOrder)) {
    if (!placed.has(member.memberId)) {
      roots.push(toItem(member));
    }
  }
  return roots;
}

/** Sets each item's `collapsed` flag from the collapsed member ids. */
export function applyCollapsed(
  tree: TreeItems<DimensionMember>,
  collapsedIds: ReadonlySet<number>
): TreeItems<DimensionMember> {
  return tree.map(item => ({
    ...item,
    collapsed: collapsedIds.has(Number(item.id)),
    children: applyCollapsed(item.children, collapsedIds),
  }));
}

/** The moved item's parent and siblings in a tree, or null when the tree does not hold it. */
function siblingsOf(
  items: TreeItems<DimensionMember>,
  memberId: number,
  parentId: number | null
): { parentId: number | null; siblings: TreeItems<DimensionMember> } | null {
  if (items.some(item => Number(item.id) === memberId)) {
    return { parentId, siblings: items };
  }
  for (const item of items) {
    const found = siblingsOf(item.children, memberId, Number(item.id));
    if (found) {
      return found;
    }
  }
  return null;
}

/**
 * The placements a drag-and-drop changed. The moved member's new sibling group is renumbered by position (sort order 0,
 * 1, 2, ...), and only the members whose parent or sort order differ from the stored ones are returned, so the API
 * rewrites as few rows as possible.
 */
export function placementsAfterMove(
  members: DimensionMember[],
  tree: TreeItems<DimensionMember>,
  movedMemberId: number
): MemberPlacement[] {
  const group = siblingsOf(tree, movedMemberId, null);
  if (!group) {
    return [];
  }
  const stored = new Map(members.map(member => [member.memberId, member]));
  return group.siblings
    .map((item, index) => ({ memberId: Number(item.id), parentMemberId: group.parentId, sortOrder: index }))
    .filter(placement => {
      const member = stored.get(placement.memberId);
      return (member?.parentMemberId ?? null) !== placement.parentMemberId || member?.sortOrder !== placement.sortOrder;
    });
}

/**
 * The tree pruned to the members whose key or label contains the term, with the ancestors that lead to them, expanded;
 * the whole tree when the term is blank.
 */
export function filterMemberTree(tree: TreeItems<DimensionMember>, term: string): TreeItems<DimensionMember> {
  const needle = term.trim().toLowerCase();
  if (!needle) {
    return tree;
  }
  return tree.flatMap(item => {
    const children = filterMemberTree(item.children, needle);
    const member = item.data as DimensionMember;
    const matches =
      member.memberKey.toLowerCase().includes(needle) || member.displayName.toLowerCase().includes(needle);
    if (!matches && children.length === 0) {
      return [];
    }
    return [{ ...item, collapsed: false, children }];
  });
}

/** The sort order that puts a new member after its future siblings. */
export function nextSortOrder(members: DimensionMember[], parentId: number | null) {
  const siblings = members.filter(member => (member.parentMemberId ?? null) === parentId);
  return Math.max(-1, ...siblings.map(member => member.sortOrder)) + 1;
}

/** A member and every member below it, which cannot become its parent without a cycle. */
export function selfAndDescendants(members: DimensionMember[], memberId: number): Set<number> {
  const found = new Set([memberId]);
  let grew = true;
  while (grew) {
    grew = false;
    for (const member of members) {
      if (member.parentMemberId != null && found.has(member.parentMemberId) && !found.has(member.memberId)) {
        found.add(member.memberId);
        grew = true;
      }
    }
  }
  return found;
}
