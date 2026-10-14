# Nestable (Headless)

Headless drag-and-drop tree nesting primitives built on **dnd-kit**. Use `NestableRoot` with a render prop to supply your own UI, or compose with the optional `NestableItem` helper component.

## Features

- Headless: you control all DOM and styling via `renderItem`.
- Drag to reorder and nest under siblings.
- Optional drag handle and overlay.
- Optional collapse/remove controls (handled by your UI).
- Keyboard support via dnd-kit (Space + Arrow keys).

## Quick Start

```tsx
import { useState } from 'react';
import { Nestable, NestableItem, type TreeItems } from '@registry/components/Nestable';

type NodeData = { label: string; description?: string };

const initial: TreeItems<NodeData> = [
  { id: 'Home', data: { label: 'Home' }, children: [] },
  {
    id: 'Collections',
    data: { label: 'Collections' },
    children: [
      { id: 'Spring', data: { label: 'Spring' }, children: [] },
      { id: 'Summer', data: { label: 'Summer' }, children: [] },
    ],
  },
];

export function BasicNestable() {
  const [items, setItems] = useState(initial);

  return (
    <Nestable
      items={items}
      onItemsChange={next => setItems(next)}
      handle
      collapsible
      removable
      renderItem={({ item, id, depth, indentationWidth, ...props }) => (
        <NestableItem
          id={id}
          label={item.data?.label ?? String(id)}
          depth={depth}
          indentationWidth={indentationWidth}
          {...props}
        />
      )}
    />
  );
}
```

## API Overview

### `NestableRoot` / `Nestable`

Main headless component. Provide `items` and `renderItem`, and update state via `onItemsChange`.

Key props:

- `items` / `defaultItems`: controlled or uncontrolled tree items.
- `renderItem`: render prop for each row.
- `indentationWidth`: indent in pixels per depth level.
- `handle`: use a drag handle (listeners attach to handle).
- `collapsible`, `removable`: wire collapse/remove callbacks for your UI.
- `renderOverlay`: custom drag overlay rendering.
- `activationConstraint`, `modifiers`, `sensors`: dnd-kit customization.
- `canDrop`: optional predicate to block invalid nesting.

### `NestableItem`

Optional helper UI component using Tailwind + `data-slot` attributes. You can replace this with your own UI.

`data-slot` hooks:

- `nestable-item`
- `nestable-collapse`
- `nestable-handle`
- `nestable-content`
- `nestable-actions`
- `nestable-remove`
- `nestable-count`

## Data Model

```ts
type TreeItem<T = unknown> = {
  id: UniqueIdentifier;
  children: TreeItem<T>[];
  collapsed?: boolean;
  disabled?: boolean;
  data?: T;
};
```

## Code Structure

```
Nestable/
  Nestable.tsx                # Headless root + drag logic
  index.ts                    # Public exports
  types.ts                    # Tree types + flattened item types
  keyboardCoordinates.ts      # Keyboard navigation helper
  utils/
    buildTree.ts              # Flattened -> tree
    flattenTree.ts            # Tree -> flattened
    getProjection.ts          # Drag projection math
    index.ts                  # Utils exports
  components/
    SortableTreeItem.tsx      # Hook wrapper for useSortable
    TreeItem.tsx              # Optional Tailwind UI helper
  stories/
    Nestable.stories.tsx      # Storybook demos
```

## LLM Guidance (for future edits)

- Prefer editing `Nestable.tsx` only for drag logic and state transitions. UI belongs in `TreeItem.tsx` or consumer render props.
- Keep utilities in `utils/` pure and immutable (no in-place mutation).
- `NestableSortableItem` should remain a thin wrapper around `useSortable`.
- Use `data-slot` attributes for any UI surface so themes can override styles.
- Favor controlled state in docs/examples; keep uncontrolled support for convenience.
- If adding new behaviors, surface them as callbacks or render props instead of hardcoding UI.

## Notes

- Drag overlay rendering is optional; `renderOverlay` receives the active item.
- For large trees, consider memoizing `renderItem` or using virtualization in your own UI layer.
