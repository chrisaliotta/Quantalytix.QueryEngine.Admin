import type {MutableRefObject} from 'react';
import type {UniqueIdentifier} from '@dnd-kit/core';

export interface TreeItem<T = unknown> {
  id: UniqueIdentifier;
  children: TreeItem<T>[];
  collapsed?: boolean;
  disabled?: boolean;
  data?: T;
}

export type TreeItems<T = unknown> = TreeItem<T>[];

export interface FlattenedItem<T = unknown> extends TreeItem<T> {
  parentId: UniqueIdentifier | null;
  depth: number;
  index: number;
}

export type SensorContext<T = unknown> = MutableRefObject<{
  items: FlattenedItem<T>[];
  offset: number;
}>;