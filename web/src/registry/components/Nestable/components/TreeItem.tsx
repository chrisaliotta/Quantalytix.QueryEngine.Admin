import React from 'react';
import type {
  DraggableAttributes,
  DraggableSyntheticListeners,
  UniqueIdentifier,
} from '@dnd-kit/core';
import { cn } from '@/lib/utils';

export interface NestableItemProps {
  id: UniqueIdentifier;
  label: React.ReactNode;
  depth: number;
  indentationWidth: number;
  style?: React.CSSProperties;
  isDragging?: boolean;
  isClone?: boolean;
  collapsed?: boolean;
  childCount?: number;
  handle?: boolean;
  attributes?: DraggableAttributes;
  listeners?: DraggableSyntheticListeners;
  activatorListeners?: DraggableSyntheticListeners;
  setNodeRef?: (element: HTMLElement | null) => void;
  setActivatorNodeRef?: (element: HTMLElement | null) => void;
  onCollapse?: () => void;
  onRemove?: () => void;
  className?: string;
  handleClassName?: string;
  contentClassName?: string;
  actionsClassName?: string;
  collapseClassName?: string;
  removeClassName?: string;
}

export function NestableItem({
  id,
  label,
  depth,
  indentationWidth,
  style,
  isDragging,
  isClone,
  collapsed,
  childCount = 0,
  handle = false,
  attributes,
  listeners,
  activatorListeners,
  setNodeRef,
  setActivatorNodeRef,
  onCollapse,
  onRemove,
  className,
  handleClassName,
  contentClassName,
  actionsClassName,
  collapseClassName,
  removeClassName,
}: NestableItemProps) {
  const rowListeners = handle ? undefined : listeners;
  const handleListeners = handle ? (activatorListeners ?? listeners) : undefined;
  const paddingLeft = depth * indentationWidth;

  return (
    <div
      ref={setNodeRef}
      data-slot="nestable-item"
      data-id={String(id)}
      data-dragging={isDragging ? 'true' : 'false'}
      data-clone={isClone ? 'true' : 'false'}
      style={{ paddingLeft, ...style }}
      className={cn(
        'group flex items-center gap-2 rounded-md border bg-background px-2 py-1.5 text-sm text-foreground shadow-sm',
        isDragging && 'opacity-60',
        className
      )}
      {...attributes}
      {...rowListeners}
    >
      {onCollapse ? (
        <button
          type="button"
          data-slot="nestable-collapse"
          aria-label={collapsed ? 'Expand' : 'Collapse'}
          onClick={onCollapse}
          className={cn(
            'flex h-6 w-6 items-center justify-center rounded-sm text-muted-foreground transition hover:text-foreground',
            collapseClassName
          )}
        >
          <svg
            className={cn('h-4 w-4 transition-transform', collapsed ? '-rotate-90' : 'rotate-0')}
            viewBox="0 0 16 16"
            aria-hidden="true"
          >
            <path
              d="M5.5 6l2.5 3 2.5-3"
              stroke="currentColor"
              strokeWidth="1.5"
              fill="none"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
        </button>
      ) : (
        <span data-slot="nestable-collapse-spacer" className="h-6 w-6" />
      )}

      {handle ? (
        <button
          type="button"
          ref={setActivatorNodeRef}
          data-slot="nestable-handle"
          aria-label="Drag"
          className={cn(
            'flex h-6 w-6 items-center justify-center rounded-sm text-muted-foreground transition hover:text-foreground',
            handleClassName
          )}
          {...handleListeners}
        >
          <svg className="h-4 w-4" viewBox="0 0 16 16" aria-hidden="true">
            <circle cx="6" cy="4" r="1" fill="currentColor" />
            <circle cx="10" cy="4" r="1" fill="currentColor" />
            <circle cx="6" cy="8" r="1" fill="currentColor" />
            <circle cx="10" cy="8" r="1" fill="currentColor" />
            <circle cx="6" cy="12" r="1" fill="currentColor" />
            <circle cx="10" cy="12" r="1" fill="currentColor" />
          </svg>
        </button>
      ) : null}

      <div
        data-slot="nestable-content"
        className={cn('flex min-w-0 flex-1 items-center gap-2', contentClassName)}
      >
        <span className="truncate">{label}</span>
        {childCount > 0 ? (
          <span
            data-slot="nestable-count"
            className="rounded-full bg-muted px-2 py-0.5 text-xs text-muted-foreground"
          >
            {childCount}
          </span>
        ) : null}
      </div>

      <div data-slot="nestable-actions" className={cn('flex items-center gap-1', actionsClassName)}>
        {onRemove ? (
          <button
            type="button"
            data-slot="nestable-remove"
            aria-label="Remove"
            onClick={onRemove}
            className={cn(
              'rounded-sm px-2 py-1 text-xs text-muted-foreground transition hover:text-foreground',
              removeClassName
            )}
          >
            Remove
          </button>
        ) : null}
      </div>
    </div>
  );
}
