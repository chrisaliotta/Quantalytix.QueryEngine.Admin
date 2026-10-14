import type { DropdownTreeItem } from '../types';

/**
 * Collect all nodes into a flat map id -> node.
 */
export function buildItemMap<T>(nodes: DropdownTreeItem<T>[]): Map<string, DropdownTreeItem<T>> {
  const map = new Map<string, DropdownTreeItem<T>>();
  function walk(list: DropdownTreeItem<T>[]) {
    for (const node of list) {
      map.set(node.id, node);
      if (node.children?.length) walk(node.children);
    }
  }
  walk(nodes);
  return map;
}

/**
 * Build a map from each node id to all its descendant ids (not including the node itself).
 * Used for cascade selection: when checking a parent, check all descendants; when unchecking, uncheck all.
 */
/**
 * Return the first selected id in depth-first tree order.
 */
export function findFirstSelectedInTreeOrder<T>(
  nodes: DropdownTreeItem<T>[],
  selectedIds: Set<string>
): string | null {
  for (const node of nodes) {
    if (selectedIds.has(node.id)) return node.id;
    if (node.children?.length) {
      const found = findFirstSelectedInTreeOrder(node.children, selectedIds);
      if (found) return found;
    }
  }
  return null;
}

export function buildDescendantsMap<T>(nodes: DropdownTreeItem<T>[]): Map<string, string[]> {
  const map = new Map<string, string[]>();
  function collectDescendants(node: DropdownTreeItem<T>): string[] {
    const ids: string[] = [];
    if (node.children?.length) {
      for (const child of node.children) {
        ids.push(child.id);
        ids.push(...collectDescendants(child));
      }
    }
    return ids;
  }
  function walk(list: DropdownTreeItem<T>[]) {
    for (const node of list) {
      map.set(node.id, collectDescendants(node));
      if (node.children?.length) walk(node.children);
    }
  }
  walk(nodes);
  return map;
}

/**
 * Like buildDescendantsMap but excludes disabled nodes from each subtree list.
 * Used for cascade selection and parent checkbox state so disabled items are never
 * selected when checking a parent.
 */
export function buildSelectableDescendantsMap<T>(
  nodes: DropdownTreeItem<T>[]
): Map<string, string[]> {
  const map = new Map<string, string[]>();
  function collectSelectableDescendants(node: DropdownTreeItem<T>): string[] {
    const ids: string[] = [];
    if (node.children?.length) {
      for (const child of node.children) {
        if (!child.disabled) {
          ids.push(child.id);
        }
        ids.push(...collectSelectableDescendants(child));
      }
    }
    return ids;
  }
  function walk(list: DropdownTreeItem<T>[]) {
    for (const node of list) {
      map.set(node.id, collectSelectableDescendants(node));
      if (node.children?.length) walk(node.children);
    }
  }
  walk(nodes);
  return map;
}

/**
 * Build a map from each node id to all leaf descendant ids in its subtree.
 * Used for "(All)" label compression and parent checkbox state so intermediate
 * folder nodes are not required in the controlled value.
 */
export function buildLeafDescendantsMap<T>(nodes: DropdownTreeItem<T>[]): Map<string, string[]> {
  const map = new Map<string, string[]>();

  function collectLeafIds(node: DropdownTreeItem<T>): string[] {
    if (!node.children?.length) {
      return node.disabled ? [] : [node.id];
    }
    const ids: string[] = [];
    for (const child of node.children) {
      ids.push(...collectLeafIds(child));
    }
    return ids;
  }

  function walk(list: DropdownTreeItem<T>[]) {
    for (const node of list) {
      const hasChildren = Boolean(node.children?.length);
      map.set(node.id, hasChildren ? collectLeafIds(node) : []);
      if (hasChildren) walk(node.children!);
    }
  }

  walk(nodes);
  return map;
}
