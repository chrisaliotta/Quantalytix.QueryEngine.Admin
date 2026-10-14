import type { DropdownTreeItem } from '../types';

/** Whether a node has loaded or lazy-loadable children */
function hasChildBranch<T>(node: DropdownTreeItem<T>): boolean {
  return Array.isArray(node.children);
}

/** Whether a node is a lazy-load placeholder (`children: []`). */
export function isLazyPlaceholder<T>(node: DropdownTreeItem<T> | undefined): boolean {
  return Boolean(node && Array.isArray(node.children) && node.children.length === 0);
}

/**
 * Prepare a tree for on-demand loading: expandable parents get `children: []` as
 * placeholders; leaves omit `children`. Actual child data is fetched asynchronously
 * via `onLoadChildren` when the user expands a node. Mirrors Rsuite `createInitialData`.
 */
export function prepareTreeItems<T>(items: DropdownTreeItem<T>[]): DropdownTreeItem<T>[] {
  return items.map(node => ({
    ...node,
    children: hasChildBranch(node) ? [] : undefined,
  }));
}

/**
 * Find the path of ids from root to the target node (inclusive).
 */
export function findPathToNodeId<T>(
  items: DropdownTreeItem<T>[],
  targetId: string,
  path: string[] = []
): string[] | null {
  for (const node of items) {
    const nextPath = [...path, node.id];
    if (node.id === targetId) return nextPath;
    if (node.children) {
      const childPath = findPathToNodeId(node.children, targetId, nextPath);
      if (childPath) return childPath;
    }
  }
  return null;
}

/** Parent ids on the path to a node (excludes the target itself). */
export function collectAncestorIds<T>(items: DropdownTreeItem<T>[], targetId: string): Set<string> {
  const path = findPathToNodeId(items, targetId);
  if (!path || path.length <= 1) return new Set();
  return new Set(path.slice(0, -1));
}

/**
 * Inject all paths to the given selected node ids into a lazy tree so branches
 * containing pre-selected values are loaded without expanding every sibling.
 */
export function injectPathsForSelectedIds<T>(
  items: DropdownTreeItem<T>[],
  selectedIds: string[]
): DropdownTreeItem<T>[] {
  const loadedNodeIds = new Set<string>();
  for (const id of selectedIds) {
    const path = findPathToNodeId(items, id);
    if (path) path.forEach(nodeId => loadedNodeIds.add(nodeId));
  }
  if (loadedNodeIds.size === 0) return prepareTreeItems(items);

  function buildWithLoadedPaths(list: DropdownTreeItem<T>[]): DropdownTreeItem<T>[] {
    return list.map(node => {
      if (!hasChildBranch(node)) {
        return { ...node, children: undefined };
      }
      if (!loadedNodeIds.has(node.id)) {
        return { ...node, children: [] };
      }
      return {
        ...node,
        children: buildWithLoadedPaths(node.children ?? []),
      };
    });
  }

  return buildWithLoadedPaths(items);
}

/**
 * Inject a selected path into a lazy tree so ancestors are pre-expanded with loaded children.
 * Siblings at each level keep `children: []` for continued lazy loading.
 */
export function injectPathIntoLazyTree<T>(
  items: DropdownTreeItem<T>[],
  path: string[]
): DropdownTreeItem<T>[] {
  if (path.length === 0) return prepareTreeItems(items);

  const [currentId, ...rest] = path;

  return items.map(node => {
    if (node.id !== currentId) {
      return {
        ...node,
        children: hasChildBranch(node) ? [] : undefined,
      };
    }

    if (rest.length === 0) {
      return {
        ...node,
        children: hasChildBranch(node) ? [] : undefined,
      };
    }

    return {
      ...node,
      children: injectPathIntoLazyTree(node.children ?? [], rest),
    };
  });
}
