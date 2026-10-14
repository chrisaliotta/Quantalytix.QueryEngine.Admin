import React from 'react';
import {useSortable} from '@dnd-kit/sortable';
import {CSS} from '@dnd-kit/utilities';
import type {UniqueIdentifier} from '@dnd-kit/core';

import type {FlattenedItem} from '../types';
import type {NestableRenderItemProps, NestableProjection} from '../Nestable';

export interface NestableSortableItemProps<T = unknown> {
  item: FlattenedItem<T>;
  depth: number;
  indentationWidth: number;
  activeId: UniqueIdentifier | null;
  overId: UniqueIdentifier | null;
  projected: NestableProjection | null;
  handle: boolean;
  childCount: number;
  onCollapse?: () => void;
  onRemove?: () => void;
  renderItem: (props: NestableRenderItemProps<T>) => React.ReactNode;
}

export function NestableSortableItem<T = unknown>({
  item,
  depth,
  indentationWidth,
  activeId,
  overId,
  projected,
  handle,
  childCount,
  onCollapse,
  onRemove,
  renderItem,
}: NestableSortableItemProps<T>) {
  const {
    setNodeRef,
    setActivatorNodeRef,
    attributes,
    listeners,
    transform,
    transition,
    isDragging,
    isSorting,
  } = useSortable({
    id: item.id,
    disabled: item.disabled,
  });

  const activatorListeners = handle ? listeners : undefined;
  const itemListeners = handle ? undefined : listeners;

  const style: React.CSSProperties = {
    transform: transform ? CSS.Transform.toString(transform) : undefined,
    transition,
    touchAction: 'manipulation',
  };

  return (
    <>
      {renderItem({
        item,
        id: item.id,
        depth,
        handle,
        indentationWidth,
        isDragging,
        isSorting,
        activeId,
        overId,
        projected,
        childCount,
        collapsed: Boolean(item.collapsed && item.children.length),
        attributes,
        listeners: itemListeners,
        activatorListeners,
        setNodeRef,
        setActivatorNodeRef,
        style,
        onCollapse,
        onRemove,
      })}
    </>
  );
}
