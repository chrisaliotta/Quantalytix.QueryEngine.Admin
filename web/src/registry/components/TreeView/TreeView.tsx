import {
  type KeyboardEvent,
  type MouseEvent,
  type RefObject,
  forwardRef,
  useCallback,
  useEffect,
  useImperativeHandle,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { cn } from '@/lib/utils';
import { DefaultIcon, DefaultToggle, TreeDSLItem } from './components';
import {
  type FlattenedNode,
  type TreeNode,
  updateNodeChildrenInTree,
  useTreeMachine,
} from './hooks/useTreeMachine';
import type {
  // TreeDSLItemProps,
  TreeDSLProps,
  TreeViewHandle,
  // TreeViewSlots,
  TreeViewProps,
  TreeViewRenderProps,
  WindowingState,
} from './types';
import { buildTreeNodesFromChildren, getTreeItemPaddingLeft } from './utils';

function needsScrollToRevealInScrollableAncestors(element: HTMLElement): boolean {
  let parent = element.parentElement;
  while (parent) {
    const { overflowY } = getComputedStyle(parent);
    if (/(auto|scroll|overlay)/.test(overflowY) && parent.scrollHeight > parent.clientHeight) {
      const elementRect = element.getBoundingClientRect();
      const containerRect = parent.getBoundingClientRect();
      if (elementRect.top < containerRect.top || elementRect.bottom > containerRect.bottom) {
        return true;
      }
    }
    parent = parent.parentElement;
  }
  return false;
}

function getMiddleChildId(children: { id: string }[] | undefined): string | undefined {
  if (!children?.length) return undefined;
  return children[Math.floor((children.length - 1) / 2)].id;
}

function filterTreeBySearchQuery<T>(nodes: TreeNode<T>[], query: string): TreeNode<T>[] {
  if (!query) return nodes;

  const walk = (list: TreeNode<T>[]): TreeNode<T>[] =>
    list
      .map(node => {
        const normalizedLabel = String(node.label ?? '').toLowerCase();
        const hasSelfMatch = normalizedLabel.includes(query);
        const filteredChildren = node.children ? walk(node.children) : undefined;
        const hasDescendantMatch = !!filteredChildren?.length;

        if (!hasSelfMatch && !hasDescendantMatch) return null;
        return {
          ...node,
          ...(node.children ? { children: filteredChildren } : {}),
        };
      })
      .filter(Boolean) as TreeNode<T>[];

  return walk(nodes);
}

function getExpandableNodeIds<T>(nodes: TreeNode<T>[]): Set<string> {
  const ids = new Set<string>();
  const walk = (list: TreeNode<T>[]) => {
    for (const node of list) {
      if (node.children && node.children.length > 0) {
        ids.add(node.id);
        walk(node.children);
      }
    }
  };
  walk(nodes);
  return ids;
}

// ─────────────────────────────────────────────────────────────────────────────
// Windowing Hook
// ─────────────────────────────────────────────────────────────────────────────

function useVirtualization<T>(
  containerRef: RefObject<HTMLDivElement | null>,
  flatNodes: FlattenedNode<T>[],
  estimatedRowHeight: number,
  overscan: number,
  enabled: boolean
): WindowingState & {
  measureRef: (el: HTMLDivElement | null, index: number) => void;
  heights: Map<number, number>;
} {
  const [scrollTop, setScrollTop] = useState(0);
  const [containerHeight, setContainerHeight] = useState(0);
  const heights = useRef(new Map<number, number>());
  const resizeObserver = useRef<ResizeObserver | null>(null);

  // Update scroll position and container height
  useLayoutEffect(() => {
    const container = containerRef.current;
    if (!container || !enabled) return;

    const handleScroll = () => {
      setScrollTop(container.scrollTop);
    };

    const handleResize = () => {
      setContainerHeight(container.clientHeight);
    };

    handleResize();
    container.addEventListener('scroll', handleScroll, { passive: true });
    resizeObserver.current = new ResizeObserver(handleResize);
    resizeObserver.current.observe(container);

    return () => {
      container.removeEventListener('scroll', handleScroll);
      resizeObserver.current?.disconnect();
    };
  }, [containerRef, enabled]);

  // Measure row heights eagerly
  const measureRef = useCallback((el: HTMLDivElement | null, index: number) => {
    if (el) {
      const height = el.getBoundingClientRect().height;
      if (heights.current.get(index) !== height) {
        heights.current.set(index, height);
      }
    }
  }, []);

  // Calculate visible range
  const getRowHeight = useCallback(
    (index: number) => heights.current.get(index) ?? estimatedRowHeight,
    [estimatedRowHeight]
  );

  if (!enabled) {
    return {
      startIndex: 0,
      endIndex: flatNodes.length,
      offsetTop: 0,
      totalHeight: flatNodes.length * estimatedRowHeight,
      measureRef,
      heights: heights.current,
    };
  }

  // Calculate offsets and visible range
  let accumulatedHeight = 0;
  let startIndex = 0;
  let startOffset = 0;

  // Find start index
  for (let i = 0; i < flatNodes.length; i++) {
    const rowHeight = getRowHeight(i);
    if (accumulatedHeight + rowHeight > scrollTop) {
      startIndex = Math.max(0, i - overscan);
      break;
    }
    accumulatedHeight += rowHeight;
  }

  // Calculate offset for start
  for (let i = 0; i < startIndex; i++) {
    startOffset += getRowHeight(i);
  }

  // Find end index
  let visibleHeight = 0;
  let endIndex = startIndex;

  for (let i = startIndex; i < flatNodes.length; i++) {
    visibleHeight += getRowHeight(i);
    endIndex = i + 1;
    if (visibleHeight > containerHeight + estimatedRowHeight * overscan * 2) {
      break;
    }
  }

  endIndex = Math.min(flatNodes.length, endIndex + overscan);

  // Calculate total height
  let totalHeight = 0;
  for (let i = 0; i < flatNodes.length; i++) {
    totalHeight += getRowHeight(i);
  }

  return {
    startIndex,
    endIndex,
    offsetTop: startOffset,
    totalHeight,
    measureRef,
    heights: heights.current,
  };
}

// ─────────────────────────────────────────────────────────────────────────────
// TreeView Component
// ─────────────────────────────────────────────────────────────────────────────

function TreeViewInner<T = unknown>(
  {
    data,
    selectionMode = 'single',
    disableFolderSelection = false,
    openIds,
    selectedId,
    selectedIds,
    defaultOpenIds,
    defaultSelectedId,
    defaultSelectedIds,
    onToggle,
    onSelect,
    onSelectionChange,
    onFocus,
    virtualized = false,
    estimatedRowHeight = 32,
    overscan = 5,
    indentSize = 16,
    selectOnFocus = false,
    renderItem,
    renderToggle,
    renderIcon,
    renderContainer,
    className,
    itemClassName,
    onLoadChildren,
    autoScrollToSelected = true,
    autoFocusFirstItem = true,
    allowSearch = false,
    ...props
  }: TreeViewProps<T>,
  ref: React.ForwardedRef<TreeViewHandle>
) {
  const containerRef = useRef<HTMLDivElement>(null);
  const itemRefs = useRef<Map<string, HTMLDivElement>>(new Map());
  const pendingExpandScrollParentIdRef = useRef<string | null>(null);
  const [treeData, setTreeData] = useState<TreeNode<T>[]>(data);
  const [loadingIds, setLoadingIds] = useState<Set<string>>(new Set());
  const [searchQuery, setSearchQuery] = useState('');
  const [searchOpenIds, setSearchOpenIds] = useState<Set<string>>(new Set());

  useEffect(() => {
    setTreeData(data);
  }, [data]);

  const resolvedData = onLoadChildren ? treeData : data;
  const normalizedSearchQuery = searchQuery.trim().toLowerCase();
  const isSearching = allowSearch && normalizedSearchQuery.length > 0;

  const filteredData = useMemo(
    () => filterTreeBySearchQuery(resolvedData, normalizedSearchQuery),
    [resolvedData, normalizedSearchQuery]
  );
  const machineData = isSearching ? filteredData : resolvedData;

  useEffect(() => {
    if (isSearching) {
      setSearchOpenIds(getExpandableNodeIds(filteredData));
    }
  }, [isSearching, filteredData]);

  const effectiveOpenIds = isSearching ? searchOpenIds : openIds;

  const handleToggle = useCallback(
    (id: string, isOpen: boolean) => {
      if (isSearching) {
        setSearchOpenIds(prev => {
          const next = new Set(prev);
          if (isOpen) {
            next.add(id);
          } else {
            next.delete(id);
          }
          return next;
        });
      }
      onToggle?.(id, isOpen);
    },
    [isSearching, onToggle]
  );

  const machine = useTreeMachine({
    data: machineData,
    selectionMode,
    openIds: effectiveOpenIds,
    selectedId,
    selectedIds,
    defaultOpenIds,
    defaultSelectedId,
    defaultSelectedIds,
    onToggle: handleToggle,
    onSelect,
    onSelectionChange,
    onFocus,
    lazyLoad: !!onLoadChildren,
  });

  const {
    flatNodes,
    state,
    toggle,
    expand,
    collapse,
    expandTo,
    expandAll,
    collapseAll,
    select,
    selectMulti,
    toggleSelect,
    selectRange,
    selectAll,
    clearSelection,
    getSelectedIds,
    focus,
    focusNext,
    focusPrev,
    focusFirst,
    focusLast,
  } = machine;

  const isMultiSelect = selectionMode === 'multiple';

  const loadChildrenIfNeeded = useCallback(
    async (id: string): Promise<boolean> => {
      if (!onLoadChildren) return false;

      const flatNode = flatNodes.find(n => n.id === id);
      const treeNode = flatNode?.node;
      const isExpanded = state.openIds.has(id);
      const needsLoad =
        treeNode &&
        Array.isArray(treeNode.children) &&
        treeNode.children.length === 0 &&
        !isExpanded;

      if (!needsLoad) return false;

      setLoadingIds(prev => new Set(prev).add(id));
      try {
        const children = await onLoadChildren(id, treeNode);
        if (children) {
          setTreeData(prev => updateNodeChildrenInTree(prev, id, children));
        }
        return true;
      } finally {
        setLoadingIds(prev => {
          const next = new Set(prev);
          next.delete(id);
          return next;
        });
      }
    },
    [onLoadChildren, flatNodes, state.openIds]
  );

  const expandWithLoad = useCallback(
    async (id: string) => {
      const wasExpanded = state.openIds.has(id);
      if (wasExpanded) return;

      await loadChildrenIfNeeded(id);
      expand(id);
      pendingExpandScrollParentIdRef.current = id;
    },
    [loadChildrenIfNeeded, state.openIds, expand]
  );

  const toggleWithLoad = useCallback(
    async (id: string) => {
      if (!onLoadChildren) {
        const wasExpanded = state.openIds.has(id);
        toggle(id);
        if (!wasExpanded) {
          pendingExpandScrollParentIdRef.current = id;
        }
        return;
      }

      const isExpanded = state.openIds.has(id);
      if (isExpanded) {
        toggle(id);
        return;
      }

      await expandWithLoad(id);
    },
    [onLoadChildren, state.openIds, toggle, expandWithLoad]
  );

  useLayoutEffect(() => {
    const parentId = pendingExpandScrollParentIdRef.current;
    if (!parentId) return;

    const parentNode = flatNodes.find(n => n.id === parentId);
    if (!parentNode?.isExpanded) {
      pendingExpandScrollParentIdRef.current = null;
      return;
    }

    const scrollChildId = getMiddleChildId(parentNode.node.children);
    if (!scrollChildId) {
      pendingExpandScrollParentIdRef.current = null;
      return;
    }

    const childEl = itemRefs.current.get(scrollChildId);
    if (!childEl) return;

    pendingExpandScrollParentIdRef.current = null;

    if (!needsScrollToRevealInScrollableAncestors(childEl)) return;

    childEl.scrollIntoView({ block: 'center', behavior: 'smooth' });
  }, [flatNodes]);

  const { startIndex, endIndex, offsetTop, totalHeight, measureRef } = useVirtualization(
    containerRef,
    flatNodes,
    estimatedRowHeight,
    overscan,
    virtualized
  );

  // Scroll focused node into view
  // useEffect(() => {
  //   if (state.focusedId) {
  //     const el = itemRefs.current.get(state.focusedId)
  //     el?.scrollIntoView({ block: 'nearest', behavior: 'smooth' })
  //   }
  // }, [state.focusedId])

  const expandToWithLoad = useCallback(
    async (id: string) => {
      const node = flatNodes.find(n => n.id === id);
      if (!node) return;
      for (const ancestorId of node.path.slice(0, -1)) {
        await expandWithLoad(ancestorId);
      }
      expandTo(id);
    },
    [flatNodes, expandWithLoad, expandTo]
  );
  const scrollToNode = useCallback((id: string) => {
    const el = itemRefs.current.get(id);
    el?.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
  }, []);

  useEffect(() => {
    if (!autoScrollToSelected || selectionMode !== 'single' || !selectedId) return;

    expandTo(selectedId);

    // Use requestAnimationFrame to ensure the node is visible after expansion before scrolling
    const frameId = window.requestAnimationFrame(() => {
      scrollToNode(selectedId);
    });

    return () => window.cancelAnimationFrame(frameId);
  }, [autoScrollToSelected, selectionMode, selectedId]);

  // Imperative handle
  useImperativeHandle(
    ref,
    () => ({
      expand: (id: string) => {
        void expandWithLoad(id);
      },
      collapse,
      toggle: (id: string) => {
        void toggleWithLoad(id);
      },
      expandTo: (id: string) => {
        void expandToWithLoad(id);
      },
      expandAll,
      collapseAll,
      select,
      selectMulti,
      toggleSelect,
      selectAll,
      clearSelection,
      getSelectedIds,
      focus,
      getContainer: () => containerRef.current,
      scrollToNode,
    }),
    [
      expandWithLoad,
      collapse,
      toggleWithLoad,
      expandToWithLoad,
      expandAll,
      collapseAll,
      select,
      selectMulti,
      toggleSelect,
      selectAll,
      clearSelection,
      getSelectedIds,
      focus,
      scrollToNode,
    ]
  );

  // Handle click with modifiers for multi-select
  const handleItemClick = useCallback(
    (node: FlattenedNode<T>, e: MouseEvent) => {
      if (node.isDisabled) return;

      // When folder selection is disabled, clicking on a folder row should be a no-op
      // (no focus, no selection). Expansion is still controlled via the toggle button/double-click.
      if (disableFolderSelection && node.hasChildren) return;

      focus(node.id);

      if (selectionMode === 'none') return;

      if (isMultiSelect) {
        if (e.shiftKey) {
          // Range select
          selectRange(node.id);
        } else {
          // Normal click (with or without Ctrl/Meta) toggles the item
          toggleSelect(node.id);
        }
      } else {
        select(node.id);
      }
    },
    [
      selectionMode,
      isMultiSelect,
      focus,
      select,
      selectMulti,
      toggleSelect,
      selectRange,
      disableFolderSelection,
    ]
  );

  // Keyboard navigation handler
  const handleKeyDown = useCallback(
    (e: KeyboardEvent<HTMLDivElement>) => {
      const { focusedId } = state;
      const focusedNode = flatNodes.find(n => n.id === focusedId);

      switch (e.key) {
        case 'ArrowDown':
          e.preventDefault();
          focusNext();
          if (isMultiSelect && e.shiftKey && focusedId) {
            // Extend selection down
            const nextNode = flatNodes.find(
              (n, i) => i > (focusedNode?.index ?? -1) && !n.isDisabled
            );
            if (nextNode) {
              selectRange(nextNode.id);
              focus(nextNode.id);
            }
          } else if (selectOnFocus && !isMultiSelect) {
            const nextNode = flatNodes.find(
              (n, i) => i > (focusedNode?.index ?? -1) && !n.isDisabled
            );
            if (nextNode && !(disableFolderSelection && nextNode.hasChildren)) {
              select(nextNode.id);
            }
          }
          break;

        case 'ArrowUp':
          e.preventDefault();
          focusPrev();
          if (isMultiSelect && e.shiftKey && focusedId) {
            // Extend selection up
            const prevNode = flatNodes
              .slice(0, focusedNode?.index ?? flatNodes.length)
              .reverse()
              .find(n => !n.isDisabled);
            if (prevNode) {
              selectRange(prevNode.id);
              focus(prevNode.id);
            }
          } else if (selectOnFocus && !isMultiSelect) {
            const prevNode = flatNodes
              .slice(0, focusedNode?.index ?? flatNodes.length)
              .reverse()
              .find(n => !n.isDisabled);
            if (prevNode && !(disableFolderSelection && prevNode.hasChildren)) {
              select(prevNode.id);
            }
          }
          break;

        case 'ArrowRight':
          e.preventDefault();
          if (focusedNode) {
            if (focusedNode.hasChildren && !focusedNode.isExpanded) {
              void expandWithLoad(focusedNode.id);
            } else if (focusedNode.hasChildren && focusedNode.isExpanded) {
              // Move to first child
              focusNext();
            }
          }
          break;

        case 'ArrowLeft':
          e.preventDefault();
          if (focusedNode) {
            if (focusedNode.hasChildren && focusedNode.isExpanded) {
              collapse(focusedNode.id);
            } else if (focusedNode.parentId) {
              // Move to parent
              focus(focusedNode.parentId);
              if (selectOnFocus && !isMultiSelect) select(focusedNode.parentId);
            }
          }
          break;

        case 'Home':
          e.preventDefault();
          focusFirst();
          if (isMultiSelect && e.shiftKey && focusedId) {
            // Select from current to first
            const firstNode = flatNodes.find(n => !n.isDisabled);
            if (firstNode) selectRange(firstNode.id);
          } else if (selectOnFocus && !isMultiSelect) {
            const firstNode = flatNodes.find(n => !n.isDisabled);
            if (firstNode && !(disableFolderSelection && firstNode.hasChildren)) {
              select(firstNode.id);
            }
          }
          break;

        case 'End':
          e.preventDefault();
          focusLast();
          if (isMultiSelect && e.shiftKey && focusedId) {
            // Select from current to last
            const lastNode = [...flatNodes].reverse().find(n => !n.isDisabled);
            if (lastNode) selectRange(lastNode.id);
          } else if (selectOnFocus && !isMultiSelect) {
            const lastNode = [...flatNodes].reverse().find(n => !n.isDisabled);
            if (lastNode && !(disableFolderSelection && lastNode.hasChildren)) {
              select(lastNode.id);
            }
          }
          break;

        case 'Enter':
          e.preventDefault();
          if (focusedNode && !focusedNode.isDisabled) {
            if (isMultiSelect) {
              if (e.ctrlKey || e.metaKey) {
                if (!(disableFolderSelection && focusedNode.hasChildren)) {
                  toggleSelect(focusedNode.id);
                }
              } else {
                if (!(disableFolderSelection && focusedNode.hasChildren)) {
                  selectMulti(focusedNode.id, false);
                }
              }
            } else {
              if (!(disableFolderSelection && focusedNode.hasChildren)) {
                select(focusedNode.id);
              }
            }
            if (focusedNode.hasChildren) {
              void toggleWithLoad(focusedNode.id);
            }
          }
          break;

        case ' ':
          e.preventDefault();
          if (focusedNode && !focusedNode.isDisabled) {
            if (isMultiSelect) {
              if (!(disableFolderSelection && focusedNode.hasChildren)) {
                toggleSelect(focusedNode.id);
              }
            } else {
              if (!(disableFolderSelection && focusedNode.hasChildren)) {
                select(focusedNode.id);
              }
            }
          }
          break;

        case 'a':
        case 'A':
          // Select all (Ctrl/Cmd + A)
          if ((e.ctrlKey || e.metaKey) && isMultiSelect) {
            e.preventDefault();
            selectAll();
          }
          break;

        case 'Escape':
          // Clear selection
          if (isMultiSelect) {
            e.preventDefault();
            clearSelection();
          }
          break;

        case '*':
          // Expand all siblings at current level
          e.preventDefault();
          if (focusedNode) {
            const siblings = flatNodes.filter(
              n => n.parentId === focusedNode.parentId && n.hasChildren
            );
            siblings.forEach(s => {
              void expandWithLoad(s.id);
            });
          }
          break;
      }
    },
    [
      state,
      flatNodes,
      focusNext,
      focusPrev,
      focusFirst,
      focusLast,
      expandWithLoad,
      collapse,
      toggleWithLoad,
      select,
      selectMulti,
      toggleSelect,
      selectRange,
      selectAll,
      clearSelection,
      focus,
      selectOnFocus,
      isMultiSelect,
      disableFolderSelection,
    ]
  );

  // Handle focus when clicking container
  const handleContainerFocus = useCallback(() => {
    if (!autoFocusFirstItem) return;
    if (!state.focusedId && flatNodes.length > 0) {
      focus(flatNodes[0].id);
    }
  }, [autoFocusFirstItem, state.focusedId, flatNodes, focus]);

  // Render a single row
  const renderRow = useCallback(
    (node: FlattenedNode<T>, _virtualIndex: number) => {
      const renderProps: TreeViewRenderProps<T> = {
        node,
        toggle: () => toggleWithLoad(node.id),
        select: () => {
          if (disableFolderSelection && node.hasChildren) return;
          if (isMultiSelect) {
            // In multi-select mode, custom renderers calling `select()` should behave
            // like a toggle (add/remove) instead of replacing the whole selection set.
            // This matches dropdown/tag-style UX (e.g. MultiSelect) and is more intuitive
            // for consumers that don't want to deal with modifier keys.
            toggleSelect(node.id);
          } else {
            select(node.id);
          }
        },
        isExpanded: node.isExpanded,
        isSelected: node.isSelected,
        isFocused: node.isFocused,
        isDisabled: node.isDisabled,
        hasChildren: node.hasChildren,
        depth: node.depth,
        path: node.path,
        siblingIndex: node.siblingIndex,
        isMultiSelect,
        isLoading: loadingIds.has(node.id),
        onLoadChildren,
      };

      // Custom item renderer
      if (renderItem) {
        return (
          <div
            key={node.id}
            ref={el => {
              if (el) {
                itemRefs.current.set(node.id, el);
                measureRef(el, node.index);
              }
            }}
            role="treeitem"
            aria-expanded={node.hasChildren ? node.isExpanded : undefined}
            aria-selected={node.isSelected}
            aria-disabled={node.isDisabled}
            aria-level={node.depth + 1}
            aria-setsize={flatNodes.filter(n => n.parentId === node.parentId).length}
            aria-posinset={
              flatNodes.filter(n => n.parentId === node.parentId).findIndex(n => n.id === node.id) +
              1
            }
            tabIndex={node.isFocused ? 0 : -1}
            data-tree-id={node.id}
            data-selected={node.isSelected}
            className={cn(
              'outline-none rounded-md transition-colors',
              (!disableFolderSelection || !node.hasChildren) && 'hover:bg-accent/50',
              node.isSelected && 'bg-accent text-accent-foreground',
              node.isFocused && 'ring-1 ring-ring ring-offset-0',
              node.isDisabled && 'opacity-50 cursor-not-allowed',
              itemClassName
            )}
            style={{
              paddingLeft: getTreeItemPaddingLeft(node.depth, indentSize),
            }}
            onClick={e => handleItemClick(node, e)}
          >
            {renderItem(renderProps)}
          </div>
        );
      }

      // Default rendering
      const toggleEl = renderToggle ? (
        renderToggle(renderProps)
      ) : (
        <DefaultToggle {...renderProps} />
      );
      const iconEl = renderIcon ? renderIcon(renderProps) : <DefaultIcon {...renderProps} />;

      return (
        <div
          key={node.id}
          ref={el => {
            if (el) {
              itemRefs.current.set(node.id, el);
              measureRef(el, node.index);
            }
          }}
          role="treeitem"
          aria-expanded={node.hasChildren ? node.isExpanded : undefined}
          aria-selected={node.isSelected}
          aria-disabled={node.isDisabled}
          aria-level={node.depth + 1}
          aria-setsize={flatNodes.filter(n => n.parentId === node.parentId).length}
          aria-posinset={
            flatNodes.filter(n => n.parentId === node.parentId).findIndex(n => n.id === node.id) + 1
          }
          tabIndex={node.isFocused ? 0 : -1}
          data-tree-id={node.id}
          data-selected={node.isSelected}
          className={cn(
            'flex items-center gap-1 px-2 py-1 rounded-md cursor-pointer select-none outline-none transition-colors',
            // Only show hover background when selection is allowed for this node
            (!disableFolderSelection || !node.hasChildren) && 'hover:bg-accent/50',
            node.isSelected && 'bg-accent text-accent-foreground',
            node.isFocused && 'ring-1 ring-ring ring-offset-0',
            node.isDisabled && 'opacity-50 cursor-not-allowed hover:bg-transparent',
            itemClassName
          )}
          style={{
            paddingLeft: getTreeItemPaddingLeft(node.depth, indentSize),
          }}
          onClick={e => handleItemClick(node, e)}
          onDoubleClick={() => {
            if (!node.isDisabled && node.hasChildren) {
              void toggleWithLoad(node.id);
            }
          }}
        >
          <button
            type="button"
            className={cn(
              'p-0.5 -ml-0.5 rounded border-none bg-transparent cursor-pointer hover:bg-muted focus:outline-none',
              !node.hasChildren && 'pointer-events-none'
            )}
            onClick={e => {
              e.stopPropagation();
              if (node.hasChildren && !node.isDisabled) {
                void toggleWithLoad(node.id);
              }
            }}
            tabIndex={-1}
            aria-hidden="true"
          >
            {toggleEl}
          </button>
          {iconEl}
          <span className="overflow-hidden text-ellipsis whitespace-nowrap text-sm leading-5">
            {node.label}
          </span>
        </div>
      );
    },
    [
      renderItem,
      renderToggle,
      renderIcon,
      toggleWithLoad,
      select,
      toggleSelect,
      handleItemClick,
      flatNodes,
      indentSize,
      itemClassName,
      measureRef,
      isMultiSelect,
      disableFolderSelection,
      loadingIds,
      onLoadChildren,
    ]
  );

  // Get visible slice for rendering
  const visibleNodes = flatNodes.slice(startIndex, endIndex);

  const content = (
    <div
      className="relative"
      style={{
        height: virtualized ? totalHeight : 'auto',
      }}
    >
      <div
        className={cn('relative', virtualized && 'absolute left-0 right-0')}
        style={{
          top: virtualized ? offsetTop : 0,
        }}
      >
        {visibleNodes.map((node, i) => renderRow(node, startIndex + i))}
      </div>
    </div>
  );

  const wrappedContent = renderContainer
    ? renderContainer({ children: content, style: {} })
    : content;

  const treeContainer = (
    <div
      ref={containerRef}
      role="tree"
      aria-label="Tree navigation"
      aria-multiselectable={isMultiSelect}
      tabIndex={0}
      className={cn('overflow-auto outline-none', virtualized && 'relative', className)}
      onKeyDown={handleKeyDown}
      onFocus={handleContainerFocus}
      {...props}
    >
      {wrappedContent}
    </div>
  );

  if (!allowSearch) return treeContainer;

  return (
    <div className="flex h-full min-h-0 flex-col gap-2">
      <input
        type="text"
        value={searchQuery}
        onChange={e => setSearchQuery(e.target.value)}
        placeholder="Search..."
        aria-label="Search tree nodes"
        className="h-8 w-full rounded-md border border-input bg-background px-3 text-sm outline-none ring-offset-background placeholder:text-muted-foreground focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
      />
      <div className="min-h-0 flex-1">{treeContainer}</div>
    </div>
  );
}

// Forward ref with generic support
export const TreeView = forwardRef(TreeViewInner) as <T = unknown>(
  props: TreeViewProps<T> & { ref?: React.ForwardedRef<TreeViewHandle> }
) => ReturnType<typeof TreeViewInner>;

// ─────────────────────────────────────────────────────────────────────────────
// Optional DSL wrapper
// ─────────────────────────────────────────────────────────────────────────────

type TreeDSLPropsInternal<T> = TreeDSLProps<T> & { data?: TreeNode<T>[] };

function TreeDSLInner<T = unknown>(
  props: TreeDSLPropsInternal<T>,
  ref: React.ForwardedRef<TreeViewHandle>
) {
  const { children, data, ...rest } = props;

  if (process.env.NODE_ENV !== 'production' && data) {
    // Guard: prefer either `data` or DSL children, not both.
    // DSL always wins to keep behavior predictable.
    console.warn('[TreeView] DSL mode ignores `data`; use one or the other.');
  }

  const nodes = useMemo(() => buildTreeNodesFromChildren<T>(children), [children]);

  return <TreeView ref={ref} {...(rest as Omit<TreeViewProps<T>, 'data'>)} data={nodes} />;
}

const TreeDSLBase = forwardRef(TreeDSLInner) as <T = unknown>(
  props: TreeDSLProps<T> & { ref?: React.ForwardedRef<TreeViewHandle> }
) => ReturnType<typeof TreeDSLInner>;

export const Tree = Object.assign(TreeDSLBase, { Item: TreeDSLItem });

// Re-export types
export type { TreeNode, FlattenedNode, SelectionMode } from './hooks/useTreeMachine';
export type {
  TreeViewRenderProps,
  TreeViewSlots,
  TreeViewProps,
  TreeViewHandle,
  TreeViewRef,
  TreeDSLItemProps,
  TreeDSLProps,
} from './types';
