import type {UniqueIdentifier} from '@dnd-kit/core';

import type {FlattenedItem, TreeItems} from '../types';

function flatten<T>(
  items: TreeItems<T>,
  parentId: UniqueIdentifier | null = null,
  depth = 0
): FlattenedItem<T>[] {
  return items.reduce<FlattenedItem<T>[]>((acc, item, index) => {
    return [
      ...acc,
      {...item, parentId, depth, index},
      ...flatten(item.children, item.id, depth + 1),
    ];
  }, []);
}

export function flattenTree<T>(items: TreeItems<T>): FlattenedItem<T>[] {
  return flatten(items);
}
