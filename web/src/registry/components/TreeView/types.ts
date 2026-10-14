import type { CSSProperties, ComponentPropsWithoutRef, ReactNode } from 'react';
import type { SelectionMode, UseTreeMachineOptions } from './hooks/useTreeMachine';
import type { FlattenedNode } from './hooks/useTreeMachine';

export interface TreeViewRenderProps<T = unknown> {
  node: FlattenedNode<T>;
  toggle: () => void | Promise<void>;
  select: () => void;
  isExpanded: boolean;
  isSelected: boolean;
  isFocused: boolean;
  isDisabled: boolean;
  hasChildren: boolean;
  depth: number;
  /** Path of ancestor ids including the node (root-first) */
  path: string[];
  /** Zero-based index among siblings */
  siblingIndex: number;
  /** Multi-select mode indicator */
  isMultiSelect: boolean;
  /** Whether the node is currently loading children */
  isLoading?: boolean;
  /** Async function to load children for this node (provided via renderItem) */
  onLoadChildren?: (
    nodeId: string,
    node: import('./hooks/useTreeMachine').TreeNode<T>
  ) => Promise<import('./hooks/useTreeMachine').TreeNode<T>[] | undefined>;
}

export interface TreeViewSlots<T = unknown> {
  /** Custom item renderer - replaces entire row content */
  renderItem?: (props: TreeViewRenderProps<T>) => ReactNode;
  /** Custom toggle/chevron renderer */
  renderToggle?: (props: TreeViewRenderProps<T>) => ReactNode;
  /** Custom icon renderer */
  renderIcon?: (props: TreeViewRenderProps<T>) => ReactNode;
  /** Custom container wrapper for rows */
  renderContainer?: (props: { children: ReactNode; style: CSSProperties }) => ReactNode;
}

export interface TreeViewProps<T = unknown>
  extends
    Omit<ComponentPropsWithoutRef<'div'>, 'onSelect' | 'onFocus' | 'onToggle'>,
    TreeViewSlots<T>,
    Pick<
      UseTreeMachineOptions<T>,
      | 'openIds'
      | 'selectedId'
      | 'selectedIds'
      | 'defaultOpenIds'
      | 'defaultSelectedId'
      | 'defaultSelectedIds'
      | 'onToggle'
      | 'onSelect'
      | 'onSelectionChange'
      | 'onFocus'
    > {
  /** Tree data to render */
  data: import('./hooks/useTreeMachine').TreeNode<T>[];
  /** Selection mode: 'single', 'multiple', or 'none' */
  selectionMode?: SelectionMode;
  /** Enable virtualization for large trees */
  virtualized?: boolean;
  /** Estimated row height for virtualization (px) */
  estimatedRowHeight?: number;
  /** Overscan count for virtualization */
  overscan?: number;
  /** Custom class for tree items */
  itemClassName?: string;
  /** Indent per depth level (px) */
  indentSize?: number;
  /** Whether to select on focus via keyboard (single-select only) */
  selectOnFocus?: boolean;
  /** When true, folders (nodes with children) cannot be selected – only leaf items */
  disableFolderSelection?: boolean;
  /**
   * Load children on demand when a lazy parent (`children: []`) is expanded.
   * Returned children are merged into the tree; use `children: []` on parents and omit `children` on leaves.
   */
  onLoadChildren?: (
    nodeId: string,
    node: import('./hooks/useTreeMachine').TreeNode<T>
  ) => Promise<import('./hooks/useTreeMachine').TreeNode<T>[] | undefined>;
  /** When true, expands ancestors and scrolls the selected node into view (single-select). */
  autoScrollToSelected?: boolean;
  /** When false, opening/focusing the tree container does not auto-focus the first visible item. */
  autoFocusFirstItem?: boolean;
  /** When true, renders a search input and filters tree nodes by label. */
  allowSearch?: boolean;
}

export interface TreeViewHandle {
  /** Expand a node by id */
  expand: (id: string) => void;
  /** Collapse a node by id */
  collapse: (id: string) => void;
  /** Toggle a node's expanded state */
  toggle: (id: string) => void;
  /** Expand to reveal a specific node (expands all ancestors) */
  expandTo: (id: string) => void;
  /** Expand all nodes */
  expandAll: () => void;
  /** Collapse all nodes */
  collapseAll: () => void;
  /** Select a node (single-select mode) */
  select: (id: string | null) => void;
  /** Select multiple nodes (multi-select mode) */
  selectMulti: (id: string, append?: boolean) => void;
  /** Toggle selection (multi-select mode) */
  toggleSelect: (id: string) => void;
  /** Select all visible nodes (multi-select mode) */
  selectAll: () => void;
  /** Clear all selections */
  clearSelection: () => void;
  /** Get array of selected ids */
  getSelectedIds: () => string[];
  /** Focus a node */
  focus: (id: string | null) => void;
  /** Get the container element */
  getContainer: () => HTMLDivElement | null;
  /** Scroll a node into view */
  scrollToNode: (id: string) => void;
}

export interface TreeDSLItemProps<T = unknown> {
  id: string;
  label: string;
  data?: T;
  disabled?: boolean;
  children?: ReactNode;
}

export interface TreeDSLProps<T = unknown> extends Omit<TreeViewProps<T>, 'data' | 'children'> {
  children: ReactNode;
}

export interface WindowingState {
  startIndex: number;
  endIndex: number;
  offsetTop: number;
  totalHeight: number;
}

/** Alias for TreeViewHandle */
export type TreeViewRef = TreeViewHandle;
