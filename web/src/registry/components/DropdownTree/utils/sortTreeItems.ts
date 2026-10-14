import type { DropdownTreeItem } from '../types';

/**
 * Recursively sort tree nodes by label at each level.
 */
export function sortTreeItems<T>(
  nodes: DropdownTreeItem<T>[],
  sort: 'asc' | 'desc'
): DropdownTreeItem<T>[] {
  return [...nodes]
    .map(node => ({
      ...node,
      children: node.children?.length
        ? sortTreeItems(node.children, sort)
        : node.children,
    }))
    .sort((a, b) => {
      const cmp = (a.label ?? '').localeCompare(b.label ?? '');
      return sort === 'asc' ? cmp : -cmp;
    });
}
