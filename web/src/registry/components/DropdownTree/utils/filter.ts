import type { DropdownTreeItem } from '../types';

/**
 * Filter tree nodes by search term (label match). Keeps parent chain for matching leaves.
 */
export function filterTreeBySearch<T>(
  nodes: DropdownTreeItem<T>[],
  search: string
): DropdownTreeItem<T>[] {
  const term = search.trim().toLowerCase();
  if (!term) return nodes;

  const go = (list: DropdownTreeItem<T>[]): DropdownTreeItem<T>[] =>
    list
      .map((node) => {
        const matches = (node.label ?? '').toLowerCase().includes(term);
        const filteredChildren = node.children ? go(node.children) : undefined;
        const hasMatchingChild = filteredChildren && filteredChildren.length > 0;
        if (matches || hasMatchingChild) {
          return {
            ...node,
            children: filteredChildren,
          } as DropdownTreeItem<T>;
        }
        return null;
      })
      .filter((n): n is DropdownTreeItem<T> => n != null);

  return go(nodes);
}
