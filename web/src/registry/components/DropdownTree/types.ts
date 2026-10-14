import type { ReactNode } from 'react';
import type { TreeNode } from '../TreeView';
import type { TreeViewRenderProps } from '../TreeView/types';

/**
 * Item shape for DropdownTree – same as TreeView's `data` prop (TreeNode).
 * Each node: { id, label, children?, data?, disabled? }.
 */
export type DropdownTreeItem<T = unknown> = TreeNode<T>;

/** Single mode: value is one id; multiple mode: value is array of ids */
export type DropdownTreeValue = string | string[] | null;

export interface DropdownTreeRenderProps<T = unknown> extends TreeViewRenderProps<T> {
  /** Show checkbox in multiple mode */
  showCheckbox?: boolean;
}

/**
 * Design tokens for dropdown tree layout CSS variables.
 * Override via `tokens` or upstream CSS.
 */
export interface DropdownTreeTokens {
  /**
   * Popover content width CSS value.
   */
  popoverWidth?: string;
  /**
   * Popover content max-width CSS value.
   */
  popoverMaxWidth?: string;
}

export interface DropdownTreeHandle {
  /** Open the dropdown */
  open: () => void;
  /** Close the dropdown */
  close: () => void;
  /** Toggle open state */
  toggle: () => void;
  /** Expand all tree nodes */
  expandAll: () => void;
  /** Collapse all tree nodes */
  collapseAll: () => void;
}

export interface DropdownTreeTriggerRenderProps {
  /** Whether the popover is open */
  open: boolean;
  /** Set open state */
  onOpenChange: (open: boolean) => void;
  /** Disabled state */
  disabled: boolean;
  /** Whether a value is selected */
  hasSelection: boolean;
  /** Formatted trigger label */
  triggerLabel: string;
  /** Placeholder when nothing selected */
  placeholder: string;
  /** Toggle open state */
  toggle: () => void;
  /** Clear selection handler (only when cleanable and has selection) */
  clear?: (event: React.MouseEvent) => void;
}

export interface DropdownTreeProps<T = unknown> {
  /**
   * Tree data – same format as TreeView's `data` prop: TreeNode<T>[].
   * Each node: { id: string, label: string, children?: TreeNode<T>[], data?: T, disabled?: boolean }.
   */
  items: DropdownTreeItem<T>[];
  /** Selection mode */
  mode?: 'single' | 'multiple';
  /** Controlled value: single id or array of ids */
  value?: DropdownTreeValue;
  /** Default value when uncontrolled */
  defaultValue?: DropdownTreeValue;
  /** Called when selection changes. Single: (id, item). Multiple: (ids, items). Clear passes null/[] */
  onSelect?: (
    idOrIds: string | string[] | null,
    itemOrItems: DropdownTreeItem<T> | DropdownTreeItem<T>[] | null
  ) => void;
  /** Show search input */
  allowSearch?: boolean;
  /** Search input placeholder */
  searchPlaceholder?: string;
  /**
   * Custom filter function for search. Receives full items tree and current query;
   * returns filtered tree (same TreeNode[] shape). When not provided, default filter
   * matches node label against query and keeps parent chain for matching leaves.
   */
  onFilterItems?: (items: DropdownTreeItem<T>[], query: string) => DropdownTreeItem<T>[];
  /** Expand all nodes when dropdown opens */
  expandAllItems?: boolean;
  /**
   * Custom render for each tree row (Rsuite `renderTreeNode`). Replaces the entire row,
   * including chevron, checkbox, and selection chrome. Use `onItemRender` + `renderIcon`
   * when only the label or icon area should be customized.
   */
  renderTreeNode?: (props: DropdownTreeRenderProps<T>) => ReactNode;
  /** Custom render for each tree item label area (default row chrome is kept) */
  onItemRender?: (props: DropdownTreeRenderProps<T>) => ReactNode;
  /** Custom icon render for each tree item (between checkbox and label) */
  renderIcon?: (props: DropdownTreeRenderProps<T>) => ReactNode;
  /**
   * Custom render for the tree list inside the popover (Rsuite `renderTree`).
   * Receives the default tree content (TreeView or empty state).
   */
  renderTree?: (tree: ReactNode) => ReactNode;
  /** Format the trigger button text from selected item(s). Default: label or comma-separated labels */
  displayText?: (item: DropdownTreeItem<T> | DropdownTreeItem<T>[]) => string;
  /** Show clear button on trigger when a value is selected. Defaults to true in multiple mode, false in single. */
  cleanable?: boolean;
  /** Called when the clear button is clicked */
  onClean?: (event: React.MouseEvent) => void;
  /** Show selection count badge on trigger in multiple mode. Defaults to true in multiple mode. */
  countable?: boolean;
  /** Max comma-separated labels on trigger before collapsing (when countable is false). Default 3. */
  maxVisibleLabels?: number;
  /** Placeholder when nothing selected */
  placeholder?: string;
  /** Controlled open state */
  open?: boolean;
  /** Whether the popover should trap focus/interactions as a modal. */
  popoverModal?: boolean;
  /** Called when open state changes */
  onOpenChange?: (open: boolean) => void;
  /** Initial open state (uncontrolled) */
  autoOpen?: boolean;
  /** Enable virtualized list for large trees */
  virtualized?: boolean;
  /** Show checkboxes in multiple mode. When true, checking a parent selects all descendants; unchecking clears them. */
  checkable?: boolean;
  /** Disabled state */
  disabled?: boolean;
  /** Root wrapper class name (layout) */
  className?: string;
  /** Trigger button class name (visual states) */
  triggerClassName?: string;
  /** Design tokens for layout CSS variables. */
  tokens?: DropdownTreeTokens;
  /** Popover content max height (px) */
  contentHeight?: number;
  /** Optional content rendered inside the popover above the tree (e.g. action buttons). Use so that ref actions (e.g. expandAll) run without closing the dropdown. */
  headerActions?: ReactNode;
  /**
   * When true, loads child nodes on demand via `onLoadChildren` when a parent is expanded.
   * Default false — the full tree in `items` is rendered synchronously.
   */
  asyncChildLoad?: boolean;
  /**
   * Load children on demand when a lazy parent (`children: []`) is expanded.
   * Only active when `asyncChildLoad` is true.
   */
  onLoadChildren?: (
    nodeId: string,
    node: DropdownTreeItem<T>
  ) => Promise<DropdownTreeItem<T>[] | undefined>;
  /** Initial expanded node ids (used when expandAllItems is false) */
  defaultOpenIds?: Set<string>;
  /** Controlled expanded node ids */
  openIds?: Set<string>;
  /** Portal container for popover content (e.g. dialog element) */
  popoverContainer?: HTMLElement | null;
  /** Content shown when the filtered tree is empty */
  emptyContent?: ReactNode;
  /** Called when the search query changes */
  onSearchChange?: (query: string) => void;
  /** When true, folders (nodes with children) cannot be selected – only leaf items */
  disableFolderSelection?: boolean;
  /** When true and a value is set, expand ancestor nodes to reveal the selection */
  expandToSelected?: boolean;
  /**
   * Called when async-loaded children are merged into the tree.
   * Only used when `asyncChildLoad` is true.
   */
  onItemsChange?: (items: DropdownTreeItem<T>[]) => void;
  /**
   * Full tree used for selection metadata (descendants, parent map, compressed labels)
   * when `asyncChildLoad` is active. Defaults to the loaded `items` tree.
   */
  catalogItems?: DropdownTreeItem<T>[];
  /** When true, popover width grows to fit the longest visible item (min = trigger width). */
  fitContentWidth?: boolean;
  /**
   * When true, renders the tree panel directly without a trigger or popover wrapper.
   * Use for inline trees embedded in dialogs or other containers.
   */
  inline?: boolean;
  /**
   * Custom trigger renderer. Replaces the default combobox button.
   * The returned element is wrapped in `PopoverTrigger asChild`.
   */
  renderTrigger?: (props: DropdownTreeTriggerRenderProps) => ReactNode;
  /**
   * Sort tree nodes by label at each level.
   * Defaults to `'asc'`. Pass `'none'` to keep the original order.
   */
  sort?: 'asc' | 'desc' | 'none';
}

/**
 * Flat item with parentId for conversion to tree.
 */
export interface FlatDropdownItem<T = unknown> {
  id: string;
  parentId?: string | null;
  label: string;
  disabled?: boolean;
  data?: T;
  [key: string]: unknown;
}
