import type {UniqueIdentifier} from '@dnd-kit/core';

import type {TreeItem, TreeItems} from '../types';

export function findItemDeep<T>(
  items: TreeItems<T>,
  itemId: UniqueIdentifier
): TreeItem<T> | undefined {
  for (const item of items) {
    const {id, children} = item;

    if (id === itemId) {
      return item;
    }

    if (children.length) {
      const child = findItemDeep(children, itemId);

      if (child) {
        return child;
      }
    }
  }

  return undefined;
}
