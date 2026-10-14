import type {UniqueIdentifier} from '@dnd-kit/core';
import {arrayMove} from '@dnd-kit/sortable';

import type {FlattenedItem} from '../types';

function getDragDepth(offset: number, indentationWidth: number) {
  if (!indentationWidth) return 0;
  return Math.round(offset / indentationWidth);
}

function clamp(value: number, min: number, max: number) {
  return Math.max(min, Math.min(value, max));
}

function getMaxDepth<T>(previousItem?: FlattenedItem<T>) {
  if (previousItem) {
    return previousItem.depth + 1;
  }

  return 0;
}

function getMinDepth<T>(nextItem?: FlattenedItem<T>) {
  if (nextItem) {
    return nextItem.depth;
  }

  return 0;
}

export function getProjection<T>(
  items: FlattenedItem<T>[],
  activeId: UniqueIdentifier,
  overId: UniqueIdentifier,
  dragOffset: number,
  indentationWidth: number
) {
  const overItemIndex = items.findIndex(({id}) => id === overId);
  const activeItemIndex = items.findIndex(({id}) => id === activeId);

  if (overItemIndex === -1 || activeItemIndex === -1) {
    return null;
  }

  const activeItem = items[activeItemIndex];
  const newItems = arrayMove(items, activeItemIndex, overItemIndex);
  const previousItem = newItems[overItemIndex - 1];
  const nextItem = newItems[overItemIndex + 1];
  const dragDepth = getDragDepth(dragOffset, indentationWidth);
  const projectedDepth = activeItem.depth + dragDepth;
  const maxDepth = getMaxDepth(previousItem);
  const minDepth = getMinDepth(nextItem);
  const depth = clamp(projectedDepth, minDepth, maxDepth);

  return {depth, maxDepth, minDepth, parentId: getParentId()};

  function getParentId() {
    if (depth === 0 || !previousItem) {
      return null;
    }

    if (depth === previousItem.depth) {
      return previousItem.parentId;
    }

    if (depth > previousItem.depth) {
      return previousItem.id;
    }

    const newParent = newItems
      .slice(0, overItemIndex)
      .reverse()
      .find((item) => item.depth === depth)?.parentId;

    return newParent ?? null;
  }
}
