import type {UniqueIdentifier} from '@dnd-kit/core';

import type {FlattenedItem, TreeItem, TreeItems} from '../types';

export function buildTree<T>(flattenedItems: FlattenedItem<T>[]): TreeItems<T> {
  const root: TreeItem<T> = {id: 'root', children: []};
  const nodes = new Map<UniqueIdentifier, TreeItem<T>>();
  nodes.set(root.id, root);

  for (const item of flattenedItems) {
    nodes.set(item.id, {...item, children: []});
  }

  const placed = new Set<UniqueIdentifier>();
  for (const item of flattenedItems) {
    if (placed.has(item.id)) continue;
    placed.add(item.id);
    const node = nodes.get(item.id);
    if (!node) continue;
    const parentId = item.parentId ?? root.id;
    const parent = nodes.get(parentId) ?? root;
    parent.children.push(node);
  }

  return root.children;
}
