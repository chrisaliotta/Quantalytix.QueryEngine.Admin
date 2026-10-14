import type { DropdownTreeItem } from '../types';

/**
 * Find a node by id in the tree.
 */
export function findNodeById<T>(
  nodes: DropdownTreeItem<T>[],
  id: string
): DropdownTreeItem<T> | null {
  for (const node of nodes) {
    if (node.id === id) return node;
    if (node.children) {
      const found = findNodeById(node.children, id);
      if (found) return found;
    }
  }
  return null;
}
