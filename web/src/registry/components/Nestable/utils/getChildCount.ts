import type {UniqueIdentifier} from '@dnd-kit/core';

import type {TreeItem, TreeItems} from '../types';
import {findItemDeep} from './findItemDeep';

function countChildren<T>(items: TreeItem<T>[], count = 0): number {
  return items.reduce((acc, {children}) => {
    if (children.length) {
      return countChildren(children, acc + 1);
    }

    return acc + 1;
  }, count);
}

export function getChildCount<T>(items: TreeItems<T>, id: UniqueIdentifier) {
  const item = findItemDeep(items, id);

  return item ? countChildren(item.children) : 0;
}
