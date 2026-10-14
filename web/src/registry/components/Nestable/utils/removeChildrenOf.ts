import type {UniqueIdentifier} from '@dnd-kit/core';

import type {FlattenedItem} from '../types';

export function removeChildrenOf<T>(
  items: FlattenedItem<T>[],
  ids: UniqueIdentifier[]
) {
  const excludeParentIds = new Set(ids);

  return items.filter((item) => {
    if (item.parentId && excludeParentIds.has(item.parentId)) {
      if (item.children.length) {
        excludeParentIds.add(item.id);
      }
      return false;
    }

    return true;
  });
}
