import type {UniqueIdentifier} from '@dnd-kit/core';

import type {TreeItems} from '../types';

export function removeItem<T>(items: TreeItems<T>, id: UniqueIdentifier) {
  return items.reduce<TreeItems<T>>((acc, item) => {
    if (item.id === id) {
      return acc;
    }

    const nextChildren = item.children.length
      ? removeItem(item.children, id)
      : item.children;

    acc.push({
      ...item,
      children: nextChildren,
    });

    return acc;
  }, []);
}
