import type {UniqueIdentifier} from '@dnd-kit/core';

import type {TreeItem, TreeItems} from '../types';

export function setProperty<T, K extends keyof TreeItem<T>>(
  items: TreeItems<T>,
  id: UniqueIdentifier,
  property: K,
  setter: (value: TreeItem<T>[K]) => TreeItem<T>[K]
): TreeItems<T> {
  return items.map((item) => {
    if (item.id === id) {
      return {...item, [property]: setter(item[property])};
    }

    if (item.children.length) {
      return {
        ...item,
        children: setProperty(item.children, id, property, setter),
      };
    }

    return item;
  });
}
