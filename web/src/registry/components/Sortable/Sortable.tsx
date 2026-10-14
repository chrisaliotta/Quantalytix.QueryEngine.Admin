import React, { useState } from 'react';
import type { PointerActivationConstraint } from '@dnd-kit/core';
import {
  type CollisionDetection,
  DndContext,
  type DragCancelEvent,
  type DragEndEvent,
  DragOverlay,
  type DragStartEvent,
  KeyboardSensor,
  type Modifiers,
  PointerSensor,
  type ScreenReaderInstructions,
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
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable';
import { CSS, type Transform } from '@dnd-kit/utilities';

type SortableItems = UniqueIdentifier[];

export type SortableItemsChange = {
  activeId: UniqueIdentifier;
  overId: UniqueIdentifier;
  activeIndex: number;
  overIndex: number;
  event: DragEndEvent;
};

export interface SortableRootProps {
  items: SortableItems;
  onItemsChange?: (nextItems: SortableItems, details: SortableItemsChange) => void;
  children: React.ReactNode;
  activationConstraint?: PointerActivationConstraint;
  collisionDetection?: CollisionDetection;
  strategy?: SortingStrategy;
  modifiers?: Modifiers;
  sensors?: SensorDescriptor<any>[];
  enablePointer?: boolean;
  enableTouch?: boolean;
  enableKeyboard?: boolean;
  renderOverlay?: (args: { activeId: UniqueIdentifier; activeIndex: number }) => React.ReactNode;
  onDragStart?: (event: DragStartEvent) => void;
  onDragCancel?: (event: DragCancelEvent) => void;
  onDragEnd?: (event: DragEndEvent) => void;
  screenReaderInstructions?: ScreenReaderInstructions;
}

export interface SortableItemRenderProps {
  id: UniqueIdentifier;
  index: number;
  isDragging: boolean;
  isSorting: boolean;
  overIndex: number;
  transform: Transform | null;
  transition: string | undefined;
  attributes: Record<string, any>;
  listeners: Record<string, any> | undefined;
  activatorListeners: Record<string, any> | undefined;
  setNodeRef: (element: HTMLElement | null) => void;
  setActivatorNodeRef: (element: HTMLElement | null) => void;
  style: React.CSSProperties;
}

export interface SortableItemProps {
  id: UniqueIdentifier;
  index: number;
  disabled?: boolean;
  handle?: boolean;
  children: (props: SortableItemRenderProps) => React.ReactNode;
}

const defaultScreenReaderInstructions: ScreenReaderInstructions = {
  draggable: `
    To pick up an item, press the space bar.
    While sorting, use the arrow keys to move the item.
    Press space again to drop the item in its new position, or press escape to cancel.
  `,
};

const restrictToVerticalAxis: Modifiers = [
  ({ transform }) => {
    if (!transform) return transform;
    return { ...transform, x: 0 };
  },
];

export function SortableRoot({
  items,
  onItemsChange,
  children,
  activationConstraint,
  collisionDetection = closestCenter,
  strategy = verticalListSortingStrategy,
  modifiers,
  sensors: customSensors,
  enablePointer = true,
  enableTouch = true,
  enableKeyboard = true,
  renderOverlay,
  onDragStart,
  onDragCancel,
  onDragEnd,
  screenReaderInstructions = defaultScreenReaderInstructions,
}: SortableRootProps) {
  const [activeId, setActiveId] = useState<UniqueIdentifier | null>(null);
  const pointerSensor = useSensor(PointerSensor, { activationConstraint });
  const touchSensor = useSensor(TouchSensor, { activationConstraint });
  const keyboardSensor = useSensor(KeyboardSensor, {
    coordinateGetter: sortableKeyboardCoordinates,
  });
  const sensors = useSensors(
    ...(enablePointer ? ([pointerSensor] as SensorDescriptor<any>[]) : []),
    ...(enableTouch ? ([touchSensor] as SensorDescriptor<any>[]) : []),
    ...(enableKeyboard ? ([keyboardSensor] as SensorDescriptor<any>[]) : []),
    ...(customSensors ?? [])
  );

  const handleDragStart = (event: DragStartEvent) => {
    setActiveId(event.active.id);
    onDragStart?.(event);
  };

  const handleDragCancel = (event: DragCancelEvent) => {
    setActiveId(null);
    onDragCancel?.(event);
  };

  const handleDragEnd = (event: DragEndEvent) => {
    const { active, over } = event;
    setActiveId(null);
    onDragEnd?.(event);

    if (!over) return;

    const activeIndex = items.indexOf(active.id);
    const overIndex = items.indexOf(over.id);

    if (activeIndex === -1 || overIndex === -1 || activeIndex === overIndex) {
      return;
    }

    const nextItems = arrayMove(items, activeIndex, overIndex);
    onItemsChange?.(nextItems, {
      activeId: active.id,
      overId: over.id,
      activeIndex,
      overIndex,
      event,
    });
  };

  return (
    <DndContext
      accessibility={{ screenReaderInstructions }}
      sensors={sensors}
      collisionDetection={collisionDetection}
      modifiers={modifiers ?? restrictToVerticalAxis}
      onDragStart={handleDragStart}
      onDragCancel={handleDragCancel}
      onDragEnd={handleDragEnd}
    >
      <SortableContext items={items} strategy={strategy}>
        {children}
      </SortableContext>

      {renderOverlay ? (
        <DragOverlay>
          {activeId != null
            ? renderOverlay({ activeId, activeIndex: items.indexOf(activeId) })
            : null}
        </DragOverlay>
      ) : null}
    </DndContext>
  );
}

export function SortableItem({ id, index, disabled, handle = false, children }: SortableItemProps) {
  const {
    setNodeRef,
    setActivatorNodeRef,
    attributes,
    listeners,
    transform,
    transition,
    isDragging,
    isSorting,
    overIndex,
  } = useSortable({
    id,
    disabled,
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
      {children({
        id,
        index,
        isDragging,
        isSorting,
        overIndex,
        transform,
        transition,
        attributes,
        listeners: itemListeners,
        activatorListeners,
        setNodeRef,
        setActivatorNodeRef,
        style,
      })}
    </>
  );
}
