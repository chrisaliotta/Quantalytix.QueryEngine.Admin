# Sortable (Headless)

Headless vertical sortable primitives built on **dnd-kit**. Render your own list markup while getting pointer/touch/keyboard sorting, optional drag handles, and optional drag overlays.

## Features

- Headless: you own all DOM and styling via render props.
- Vertical sorting strategy by default (with a built-in vertical-axis modifier).
- Pointer + touch + keyboard sensors enabled by default.
- Optional drag handle support (activator listeners).
- Optional drag overlay rendering.
- Screen-reader instructions baked in (Space to pick up, Arrows to move).

## Quick Start

```tsx
import { useState } from 'react';
import { SortableItem, SortableRoot } from '@registry/components/Sortable';

const initial = ['alpha', 'bravo', 'charlie'];

export function SimpleSortable() {
  const [items, setItems] = useState(initial);

  return (
    <SortableRoot
      items={items}
      onItemsChange={next => setItems(next)}
      activationConstraint={{ distance: 4 }}
    >
      <div className="flex flex-col gap-2">
        {items.map((id, index) => (
          <SortableItem key={id} id={id} index={index}>
            {({ setNodeRef, attributes, listeners, style, isDragging }) => (
              <div
                ref={setNodeRef}
                {...attributes}
                {...listeners}
                style={style}
                className={`rounded border px-3 py-2 ${isDragging ? 'opacity-70' : ''}`}
              >
                {id}
              </div>
            )}
          </SortableItem>
        ))}
      </div>
    </SortableRoot>
  );
}
```

## API

### `SortableRoot`

Wraps your list and wires `DndContext` + `SortableContext`.

| Prop                                     | Type                                     | Default                       | Notes                                                                     |
| ---------------------------------------- | ---------------------------------------- | ----------------------------- | ------------------------------------------------------------------------- |
| `items`                                  | `UniqueIdentifier[]`                     | required                      | Order of items.                                                           |
| `onItemsChange`                          | `(next, details) => void`                | -                             | Called after a successful reorder. Provide to update your state.          |
| `children`                               | `ReactNode`                              | required                      | Your list/body markup.                                                    |
| `activationConstraint`                   | `PointerActivationConstraint`            | -                             | e.g. `{ distance: 4 }`.                                                   |
| `collisionDetection`                     | `CollisionDetection`                     | `closestCenter`               | From dnd-kit.                                                             |
| `strategy`                               | `SortingStrategy`                        | `verticalListSortingStrategy` | Change if you need a different layout.                                    |
| `modifiers`                              | `Modifiers`                              | vertical-axis only            | Override to customize transforms.                                         |
| `sensors`                                | `SensorDescriptor[]`                     | -                             | Provide custom sensors; otherwise pointer + touch + keyboard are created. |
| `enablePointer`                          | `boolean`                                | `true`                        | Toggle pointer sensor.                                                    |
| `enableTouch`                            | `boolean`                                | `true`                        | Toggle touch sensor.                                                      |
| `enableKeyboard`                         | `boolean`                                | `true`                        | Toggle keyboard sensor (uses `sortableKeyboardCoordinates`).              |
| `renderOverlay`                          | `({activeId, activeIndex}) => ReactNode` | -                             | When provided, renders a `DragOverlay`.                                   |
| `onDragStart / onDragCancel / onDragEnd` | dnd-kit events                           | -                             | Passthrough hooks.                                                        |
| `screenReaderInstructions`               | `ScreenReaderInstructions`               | built-in                      | Override if needed.                                                       |

`onItemsChange` receives `details: { activeId, overId, activeIndex, overIndex, event }`.

### `SortableItem`

Headless item with a render prop.

| Prop       | Type                         | Default  | Notes                                                                                       |
| ---------- | ---------------------------- | -------- | ------------------------------------------------------------------------------------------- |
| `id`       | `UniqueIdentifier`           | required | Item id from `items`.                                                                       |
| `index`    | `number`                     | required | Convenience index for your render.                                                          |
| `disabled` | `boolean`                    | `false`  | Disable dragging for this item.                                                             |
| `handle`   | `boolean`                    | `false`  | When `true`, attach listeners to a handle via `activatorListeners` + `setActivatorNodeRef`. |
| `children` | `(renderProps) => ReactNode` | required | Render prop with sortable state + helpers.                                                  |

`renderProps` includes:

- `setNodeRef`, `setActivatorNodeRef`
- `attributes`
- `listeners` (attach to the whole row) and `activatorListeners` (attach to a handle)
- `isDragging`, `isSorting`, `overIndex`, `index`
- `transform`, `transition`, `style` (already includes transform/transition + `touchAction: 'manipulation'`)

## Patterns

### Drag handle

```tsx
<SortableItem id={item.id} index={index} handle>
  {({ setNodeRef, setActivatorNodeRef, attributes, activatorListeners, style }) => (
    <div ref={setNodeRef} {...attributes} style={style}>
      <button ref={setActivatorNodeRef} {...activatorListeners} aria-label="Drag">
        ☰
      </button>
      {item.title}
    </div>
  )}
</SortableItem>
```

### Drag overlay

Pass `renderOverlay` to `SortableRoot` and return any React node. The overlay receives `{ activeId, activeIndex }`.

## Notes

- Vertical-only transforms by default; override `modifiers` if you need other axes.
- Keyboard: Space to pick up, Arrow keys to move, Space to drop, Escape to cancel.
- Keep `items` stable across renders; re-create state from `onItemsChange` output.
