import type { DropdownTreeItem, FlatDropdownItem } from '../types';

/**
 * Convert flat list (id, parentId, label) to nested TreeNode[] for DropdownTree/TreeView.
 */
export function flatItemsToTree<T>(
  items: FlatDropdownItem<T>[]
): DropdownTreeItem<T>[] {
  const nodeMap = new Map<string, DropdownTreeItem<T>>();
  const roots: DropdownTreeItem<T>[] = [];

  for (const item of items) {
    const node: DropdownTreeItem<T> = {
      id: item.id,
      label: item.label,
      disabled: item.disabled,
      data: item.data as T | undefined,
      children: undefined,
    };
    nodeMap.set(item.id, node);
  }

  for (const item of items) {
    const node = nodeMap.get(item.id)!;
    const parentId = item.parentId ?? null;

    if (!parentId) {
      roots.push(node);
    } else {
      const parent = nodeMap.get(parentId);
      if (parent) {
        if (!parent.children) parent.children = [];
        parent.children.push(node);
      } else {
        roots.push(node);
      }
    }
  }

  return roots;
}
