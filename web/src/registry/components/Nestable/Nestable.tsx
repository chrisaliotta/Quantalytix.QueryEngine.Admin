import React, { useEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import type { Announcements } from '@dnd-kit/core';
import {
  type CollisionDetection,
  DndContext,
  type DragCancelEvent,
  type DragEndEvent,
  type DragMoveEvent,
  type DragOverEvent,
  DragOverlay,
  type DragStartEvent,
  type DraggableAttributes,
  type DraggableSyntheticListeners,
  type DropAnimation,
  KeyboardSensor,
  type MeasuringConfiguration,
  MeasuringStrategy,
  type Modifier,
  type Modifiers,
  type PointerActivationConstraint,
  PointerSensor,
  type SensorDescriptor,
  TouchSensor,
  type UniqueIdentifier,
  closestCenter,
  useSensor,
  useSensors,
} from '@dnd-kit/core';
import {
  SortableContext,
  type SortingStrategy,
  arrayMove,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { NestableSortableItem } from './components/SortableTreeItem';
import { sortableTreeKeyboardCoordinates } from './keyboardCoordinates';
import type { FlattenedItem, TreeItems } from './types';
import {
  buildTree,
  flattenTree,
  getChildCount,
  getProjection,
  removeChildrenOf,
  removeItem,
  setProperty,
} from './utils';

const defaultDropAnimation: DropAnimation = {
  keyframes({ transform }) {
    return [
      { opacity: 1, transform: CSS.Transform.toString(transform.initial) },
      {
        opacity: 0,
        transform: CSS.Transform.toString({
          ...transform.final,
          x: transform.final.x + 5,
          y: transform.final.y + 5,
        }),
      },
    ];
  },
  easing: 'ease-out',
  sideEffects({ active }) {
    active.node.animate([{ opacity: 0 }, { opacity: 1 }], {
      duration: 200,
      easing: 'ease-out',
    });
  },
};

const defaultMeasuring: MeasuringConfiguration = {
  droppable: {
    strategy: MeasuringStrategy.Always,
  },
};

// Without a distance threshold, dnd-kit's PointerSensor/TouchSensor start a
// drag on virtually any pointer movement — including the few px of jitter an
// ordinary click/tap has — which swallows onClick and can misfire a
// zero-distance reorder. 8px matches dnd-kit's own recommended default.
// Module-level so the default has a stable reference across renders.
const defaultActivationConstraint: PointerActivationConstraint = { distance: 8 };

export type NestableProjection = {
  depth: number;
  maxDepth: number;
  minDepth: number;
  parentId: UniqueIdentifier | null;
};

export type NestableItemsChange<T = unknown> = {
  source: 'drag' | 'collapse' | 'remove';
  activeId: UniqueIdentifier;
  overId: UniqueIdentifier;
  activeIndex: number;
  overIndex: number;
  projected: NestableProjection | null;
  event?: DragEndEvent;
  nextItems: TreeItems<T>;
};

export interface NestableRootProps<T = unknown> {
  items?: TreeItems<T>;
  defaultItems?: TreeItems<T>;
  onItemsChange?: (nextItems: TreeItems<T>, details: NestableItemsChange<T>) => void;
  renderItem: (props: NestableRenderItemProps<T>) => React.ReactNode;
  indentationWidth?: number;
  indicator?: boolean;
  handle?: boolean;
  collapsible?: boolean;
  removable?: boolean;
  canDrop?: (args: {
    activeId: UniqueIdentifier;
    overId: UniqueIdentifier;
    projected: NestableProjection;
    items: TreeItems<T>;
  }) => boolean;
  activationConstraint?: PointerActivationConstraint;
  collisionDetection?: CollisionDetection;
  strategy?: SortingStrategy;
  modifiers?: Modifiers;
  sensors?: SensorDescriptor<any>[];
  enablePointer?: boolean;
  enableTouch?: boolean;
  enableKeyboard?: boolean;
  dropAnimation?: DropAnimation;
  measuring?: MeasuringConfiguration;
  renderOverlay?: (args: {
    activeId: UniqueIdentifier;
    activeItem: FlattenedItem<T>;
    childCount: number;
  }) => React.ReactNode;
  onDragStart?: (event: DragStartEvent) => void;
  onDragMove?: (event: DragMoveEvent) => void;
  onDragOver?: (event: DragOverEvent) => void;
  onDragCancel?: (event: DragCancelEvent) => void;
  onDragEnd?: (event: DragEndEvent) => void;
  announcements?: Announcements;
}

export interface NestableRenderItemProps<T = unknown> {
  item: FlattenedItem<T>;
  id: UniqueIdentifier;
  depth: number;
  indentationWidth: number;
  isDragging: boolean;
  isSorting: boolean;
  activeId: UniqueIdentifier | null;
  overId: UniqueIdentifier | null;
  projected: NestableProjection | null;
  childCount: number;
  collapsed: boolean;
  attributes: DraggableAttributes;
  listeners: DraggableSyntheticListeners | undefined;
  activatorListeners: DraggableSyntheticListeners | undefined;
  setNodeRef: (element: HTMLElement | null) => void;
  setActivatorNodeRef: (element: HTMLElement | null) => void;
  style: React.CSSProperties;
  onCollapse?: () => void;
  handle?: boolean;
  onRemove?: () => void;
}

export function NestableRoot<T = unknown>({
  items: controlledItems,
  defaultItems = [],
  onItemsChange,
  renderItem,
  indentationWidth = 32,
  indicator = false,
  handle = false,
  collapsible = false,
  removable = false,
  canDrop,
  activationConstraint = defaultActivationConstraint,
  collisionDetection = closestCenter,
  strategy = verticalListSortingStrategy,
  modifiers,
  sensors: customSensors,
  enablePointer = true,
  enableTouch = true,
  enableKeyboard = true,
  dropAnimation = defaultDropAnimation,
  measuring = defaultMeasuring,
  renderOverlay,
  onDragStart,
  onDragMove,
  onDragOver,
  onDragCancel,
  onDragEnd,
  announcements,
}: NestableRootProps<T>) {
  const isControlled = controlledItems !== undefined;
  const [uncontrolledItems, setUncontrolledItems] = useState<TreeItems<T>>(defaultItems);
  const items = isControlled ? controlledItems : uncontrolledItems;
  const [activeId, setActiveId] = useState<UniqueIdentifier | null>(null);
  const [overId, setOverId] = useState<UniqueIdentifier | null>(null);
  const [offsetLeft, setOffsetLeft] = useState(0);
  const currentPositionRef = useRef<{
    parentId: UniqueIdentifier | null;
    overId: UniqueIdentifier;
  } | null>(null);

  const allFlattenedItems = useMemo(() => flattenTree(items), [items]);
  const collapsedIds = useMemo(
    () =>
      allFlattenedItems.reduce<UniqueIdentifier[]>((acc, item) => {
        if (item.collapsed && item.children.length) {
          acc.push(item.id);
        }
        return acc;
      }, []),
    [allFlattenedItems]
  );
  const flattenedItems = useMemo(
    () =>
      removeChildrenOf(
        allFlattenedItems,
        activeId != null ? [activeId, ...collapsedIds] : collapsedIds
      ),
    [activeId, allFlattenedItems, collapsedIds]
  );
  const rawProjected =
    activeId && overId
      ? getProjection(flattenedItems, activeId, overId, offsetLeft, indentationWidth)
      : null;

  // Never nest into the hovered row itself (parentId === overId). That projection
  // appears when dropping "below" a folder that already has visible children:
  // nextItem is a child (minDepth=1) which forces nesting. Treat as sibling reorder
  // instead; nesting still works via indent relative to the previous item.
  const projected = useMemo(() => {
    if (!rawProjected || !overId) return rawProjected;
    const overItem = flattenedItems.find(({ id }) => id === overId);
    if (overItem && rawProjected.parentId === overId) {
      return {
        ...rawProjected,
        depth: overItem.depth,
        parentId: overItem.parentId,
      };
    }
    return rawProjected;
  }, [rawProjected, overId, flattenedItems]);

  const sensorContext = useRef({
    items: flattenedItems,
    offset: offsetLeft,
  });
  const coordinateGetter = useMemo(
    () => sortableTreeKeyboardCoordinates(sensorContext, indicator, indentationWidth),
    [indicator, indentationWidth]
  );
  const pointerSensor = useSensor(PointerSensor, { activationConstraint });
  const touchSensor = useSensor(TouchSensor, { activationConstraint });
  const keyboardSensor = useSensor(KeyboardSensor, {
    coordinateGetter,
  });
  const sensors = useSensors(
    ...(enablePointer ? ([pointerSensor] as SensorDescriptor<any>[]) : []),
    ...(enableTouch ? ([touchSensor] as SensorDescriptor<any>[]) : []),
    ...(enableKeyboard ? ([keyboardSensor] as SensorDescriptor<any>[]) : []),
    ...(customSensors ?? [])
  );

  const sortedIds = useMemo(() => flattenedItems.map(({ id }) => id), [flattenedItems]);
  const activeItem = activeId
    ? (allFlattenedItems.find(({ id }) => id === activeId) ?? null)
    : null;

  useEffect(() => {
    sensorContext.current = {
      items: flattenedItems,
      offset: offsetLeft,
    };
  }, [flattenedItems, offsetLeft]);

  const mergedAnnouncements = useMemo<Announcements>(
    () =>
      announcements ?? {
        onDragStart({ active }) {
          return `Picked up ${active.id}.`;
        },
        onDragMove({ active, over }) {
          return getMovementAnnouncement('onDragMove', active.id, over?.id);
        },
        onDragOver({ active, over }) {
          return getMovementAnnouncement('onDragOver', active.id, over?.id);
        },
        onDragEnd({ active, over }) {
          return getMovementAnnouncement('onDragEnd', active.id, over?.id);
        },
        onDragCancel({ active }) {
          return `Moving was cancelled. ${active.id} was dropped in its original position.`;
        },
      },
    [announcements, allFlattenedItems, projected]
  );

  const canUseDOM = typeof document !== 'undefined';
  const overlayModifiers: Modifiers | undefined = indicator
    ? [adjustTranslate, ...(modifiers ?? [])]
    : modifiers;
  const overlay =
    renderOverlay && activeId && activeItem
      ? renderOverlay({
          activeId,
          activeItem,
          childCount: getChildCount(items, activeId) + 1,
        })
      : null;

  return (
    <DndContext
      accessibility={{ announcements: mergedAnnouncements }}
      sensors={sensors}
      collisionDetection={collisionDetection}
      measuring={measuring}
      modifiers={modifiers}
      onDragStart={handleDragStart}
      onDragMove={handleDragMove}
      onDragOver={handleDragOver}
      onDragEnd={handleDragEnd}
      onDragCancel={handleDragCancel}
    >
      <SortableContext items={sortedIds} strategy={strategy}>
        {flattenedItems.map(item => (
          <NestableSortableItem
            key={item.id}
            item={item}
            depth={item.id === activeId && projected ? projected.depth : item.depth}
            indentationWidth={indentationWidth}
            activeId={activeId}
            overId={overId}
            projected={projected}
            handle={handle}
            childCount={getChildCount(items, item.id)}
            onCollapse={
              collapsible && item.children.length ? () => handleCollapse(item.id) : undefined
            }
            onRemove={removable ? () => handleRemove(item.id) : undefined}
            renderItem={renderItem}
          />
        ))}
      </SortableContext>

      {overlay && canUseDOM
        ? createPortal(
            <DragOverlay dropAnimation={dropAnimation} modifiers={overlayModifiers}>
              {overlay}
            </DragOverlay>,
            document.body
          )
        : null}
    </DndContext>
  );

  function handleDragStart(event: DragStartEvent) {
    setActiveId(event.active.id);
    setOverId(event.active.id);
    const current = flattenedItems.find(({ id }) => id === event.active.id);
    if (current) {
      currentPositionRef.current = {
        parentId: current.parentId,
        overId: current.id,
      };
    }
    onDragStart?.(event);
  }

  function handleDragMove(event: DragMoveEvent) {
    setOffsetLeft(event.delta.x);
    onDragMove?.(event);
  }

  function handleDragOver(event: DragOverEvent) {
    setOverId(event.over?.id ?? null);
    onDragOver?.(event);
  }

  function handleDragEnd(event: DragEndEvent) {
    const { active, over } = event;
    const projection = projected;
    if (projection && over) {
      const nextItems = applyProjection(items, {
        activeId: active.id,
        overId: over.id,
        projected: projection,
      });

      if (nextItems) {
        const activeIndex = allFlattenedItems.findIndex(({ id }) => id === active.id);
        const overIndex = allFlattenedItems.findIndex(({ id }) => id === over.id);

        if (
          canDrop?.({
            activeId: active.id,
            overId: over.id,
            projected: projection,
            items,
          }) !== false
        ) {
          updateItems(nextItems, {
            source: 'drag',
            activeId: active.id,
            overId: over.id,
            activeIndex,
            overIndex,
            projected: projection,
            event,
            nextItems,
          });
        }
      }
    }

    resetState();
    onDragEnd?.(event);
  }

  function handleDragCancel(event: DragCancelEvent) {
    resetState();
    onDragCancel?.(event);
  }

  function resetState() {
    setOverId(null);
    setActiveId(null);
    setOffsetLeft(0);
    currentPositionRef.current = null;
  }

  function updateItems(nextItems: TreeItems<T>, details: NestableItemsChange<T>) {
    if (!isControlled) {
      setUncontrolledItems(nextItems);
    }
    onItemsChange?.(nextItems, details);
  }

  function handleRemove(id: UniqueIdentifier) {
    const nextItems = removeItem(items, id);
    updateItems(nextItems, {
      source: 'remove',
      activeId: id,
      overId: id,
      activeIndex: -1,
      overIndex: -1,
      projected: null,
      nextItems,
    });
  }

  function handleCollapse(id: UniqueIdentifier) {
    const nextItems = setProperty(items, id, 'collapsed', value => !value);
    updateItems(nextItems, {
      source: 'collapse',
      activeId: id,
      overId: id,
      activeIndex: -1,
      overIndex: -1,
      projected: null,
      nextItems,
    });
  }

  function getMovementAnnouncement(
    eventName: 'onDragMove' | 'onDragOver' | 'onDragEnd',
    activeId: UniqueIdentifier,
    overId?: UniqueIdentifier
  ) {
    if (!overId || !projected) {
      return undefined;
    }

    if (eventName !== 'onDragEnd') {
      const current = currentPositionRef.current;
      if (current && projected.parentId === current.parentId && overId === current.overId) {
        return undefined;
      }

      currentPositionRef.current = {
        parentId: projected.parentId,
        overId,
      };
    }

    const clonedItems = allFlattenedItems.map(item => ({ ...item }));
    const overIndex = clonedItems.findIndex(({ id }) => id === overId);
    const activeIndex = clonedItems.findIndex(({ id }) => id === activeId);
    const sortedItems = arrayMove(clonedItems, activeIndex, overIndex);

    const previousItem = sortedItems[overIndex - 1];

    const movedVerb = eventName === 'onDragEnd' ? 'dropped' : 'moved';
    const nestedVerb = eventName === 'onDragEnd' ? 'dropped' : 'nested';

    if (!previousItem) {
      const nextItem = sortedItems[overIndex + 1];
      return `${activeId} was ${movedVerb} before ${nextItem.id}.`;
    }

    if (projected.depth > previousItem.depth) {
      return `${activeId} was ${nestedVerb} under ${previousItem.id}.`;
    }

    let previousSibling: FlattenedItem<T> | undefined = previousItem;
    while (previousSibling && projected.depth < previousSibling.depth) {
      const parentId: UniqueIdentifier | null = previousSibling.parentId;
      previousSibling = sortedItems.find(({ id }) => id === parentId);
    }

    if (previousSibling) {
      return `${activeId} was ${movedVerb} after ${previousSibling.id}.`;
    }

    return undefined;
  }
}

export const Nestable = NestableRoot;

function applyProjection<T>(
  items: TreeItems<T>,
  args: {
    activeId: UniqueIdentifier;
    overId: UniqueIdentifier;
    projected: NestableProjection;
  }
) {
  const flattenedItems = flattenTree(items);
  const activeIndex = flattenedItems.findIndex(({ id }) => id === args.activeId);
  const overIndex = flattenedItems.findIndex(({ id }) => id === args.overId);

  if (activeIndex === -1 || overIndex === -1) {
    return null;
  }

  const nextItems = flattenedItems.map(item =>
    item.id === args.activeId
      ? { ...item, depth: args.projected.depth, parentId: args.projected.parentId }
      : item
  );
  const sortedItems = arrayMove(nextItems, activeIndex, overIndex);

  return buildTree(sortedItems);
}

const adjustTranslate: Modifier = ({ transform }) => {
  if (!transform) return transform;
  return {
    ...transform,
    y: transform.y - 25,
  };
};
