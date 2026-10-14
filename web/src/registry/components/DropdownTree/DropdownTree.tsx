import {
  type ReactNode,
  forwardRef,
  useCallback,
  useEffect,
  useImperativeHandle,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { ChevronDown, ChevronRight, X } from 'lucide-react';
import { Checkbox } from '@registry/components/Checkbox';
import { Popover, PopoverContent, PopoverTrigger } from '@registry/components/custom-ui/popover';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';
import { TreeView, type TreeViewRef, type TreeViewRenderProps } from '../TreeView';
import { updateNodeChildrenInTree } from '../TreeView/hooks/useTreeMachine';
import { TruncatedText } from '../TruncatedText';
import { DROPDOWN_TREE_INDENT_SIZE, MULTIPLE_MODE, SINGLE_MODE } from './constants';
import type {
  DropdownTreeHandle,
  DropdownTreeItem,
  DropdownTreeProps,
  DropdownTreeTriggerRenderProps,
  DropdownTreeValue,
} from './types';
import {
  applyCascadeSelection,
  buildItemMap,
  buildLeafDescendantsMap,
  buildParentMap,
  collectAllParentIds,
  collectAncestorIds,
  compressSelectionToParents,
  expandSelectionIds,
  filterTreeBySearch,
  findFirstSelectedInTreeOrder,
  getCompressedDisplayLabels,
  getNodeCheckboxState,
  removeDisabledIdsFromSelection,
  sortTreeItems,
} from './utils';

function DropdownTreeInner<T = unknown>(
  {
    items,
    mode = SINGLE_MODE,
    value: controlledValue,
    defaultValue,
    onSelect,
    allowSearch = false,
    searchPlaceholder = 'Search...',
    onFilterItems,
    expandAllItems = true,
    renderTreeNode,
    onItemRender,
    renderIcon,
    renderTree,
    displayText,
    cleanable,
    onClean,
    countable,
    maxVisibleLabels = 3,
    placeholder = 'Select...',
    open: controlledOpen,
    popoverModal = false,
    onOpenChange,
    autoOpen = false,
    virtualized = false,
    checkable = false,
    disabled = false,
    className,
    triggerClassName,
    tokens = {
      popoverWidth: 'var(--popover-width, var(--radix-popover-trigger-width))',
      popoverMaxWidth: 'var(--popover-max-width)',
    },
    contentHeight = 320,
    headerActions,
    asyncChildLoad = false,
    onLoadChildren,
    defaultOpenIds,
    openIds: controlledOpenIds,
    popoverContainer,
    emptyContent,
    onSearchChange,
    disableFolderSelection = false,
    expandToSelected = false,
    onItemsChange,
    catalogItems,
    fitContentWidth = false,
    inline = false,
    renderTrigger,
    sort = 'asc',
  }: DropdownTreeProps<T>,
  ref: React.ForwardedRef<DropdownTreeHandle>
) {
  const [internalOpen, setInternalOpen] = useState(autoOpen);
  const [search, setSearch] = useState('');
  const [resolvedItems, setResolvedItems] = useState<DropdownTreeItem<T>[]>(items);
  const [internalValue, setInternalValue] = useState<DropdownTreeValue>(() => {
    if (defaultValue === undefined) return mode === SINGLE_MODE ? null : [];
    return defaultValue;
  });
  const treeRef = useRef<TreeViewRef | null>(null);
  const hasPerformedOpenScrollRef = useRef(false);

  const resolvedCleanable = cleanable ?? mode === MULTIPLE_MODE;
  const resolvedCountable = countable ?? mode === MULTIPLE_MODE;

  const isAsyncChildLoad = asyncChildLoad && Boolean(onLoadChildren);

  useEffect(() => {
    if (isAsyncChildLoad) {
      setResolvedItems(items);
    }
  }, [items, isAsyncChildLoad]);

  const isControlledOpen = controlledOpen !== undefined;
  const open = isControlledOpen ? controlledOpen : internalOpen;
  const setOpen = useCallback(
    (v: boolean) => {
      if (!isControlledOpen) setInternalOpen(v);
      onOpenChange?.(v);
      if (!v) {
        setSearch('');
        onSearchChange?.('');
      }
    },
    [isControlledOpen, onOpenChange, onSearchChange]
  );

  const isControlledValue = controlledValue !== undefined;
  const value = isControlledValue ? controlledValue : internalValue;
  const rawSelectedIds = useMemo(() => {
    if (value == null) return mode === SINGLE_MODE ? null : new Set<string>();
    if (mode === SINGLE_MODE) return typeof value === 'string' ? value : null;
    const arr = Array.isArray(value) ? value : [value];
    return new Set(arr);
  }, [value, mode]);

  const treeSourceItems = isAsyncChildLoad ? resolvedItems : items;
  const sortedTreeSourceItems = useMemo(
    () => (sort === 'none' ? treeSourceItems : sortTreeItems(treeSourceItems, sort)),
    [treeSourceItems, sort]
  );
  const metadataItems = catalogItems ?? sortedTreeSourceItems;

  const itemMap = useMemo(() => buildItemMap(sortedTreeSourceItems), [sortedTreeSourceItems]);
  const metadataItemMap = useMemo(() => buildItemMap(metadataItems), [metadataItems]);
  const selectedIds = useMemo(() => {
    if (rawSelectedIds instanceof Set) {
      return removeDisabledIdsFromSelection(rawSelectedIds, metadataItemMap);
    }
    if (typeof rawSelectedIds === 'string') {
      const item = metadataItemMap.get(rawSelectedIds);
      return item?.disabled ? null : rawSelectedIds;
    }
    return rawSelectedIds;
  }, [rawSelectedIds, metadataItemMap]);
  const descendantsMap = useMemo(
    () =>
      mode === MULTIPLE_MODE && checkable
        ? buildLeafDescendantsMap(metadataItems)
        : new Map<string, string[]>(),
    [metadataItems, mode, checkable]
  );
  const parentById = useMemo(
    () =>
      mode === MULTIPLE_MODE && checkable
        ? buildParentMap(metadataItems)
        : new Map<string, string | null>(),
    [metadataItems, mode, checkable]
  );
  const expandedSelectedIds = useMemo(() => {
    if (!(selectedIds instanceof Set) || descendantsMap.size === 0) {
      return selectedIds;
    }
    return expandSelectionIds(selectedIds, descendantsMap);
  }, [selectedIds, descendantsMap]);

  useEffect(() => {
    if (
      mode !== MULTIPLE_MODE ||
      !checkable ||
      !(selectedIds instanceof Set) ||
      descendantsMap.size === 0
    ) {
      return;
    }

    const withDescendants = expandSelectionIds(selectedIds, descendantsMap);
    const cascaded = applyCascadeSelection(
      selectedIds,
      withDescendants,
      descendantsMap,
      parentById
    );
    const compressed = compressSelectionToParents(metadataItems, cascaded, descendantsMap);
    const nextIds = removeDisabledIdsFromSelection(compressed, metadataItemMap);
    const unchanged =
      nextIds.size === selectedIds.size && [...nextIds].every(id => selectedIds.has(id));
    if (unchanged) return;

    const arr = Array.from(nextIds);
    const selectedItems = arr
      .map(id => metadataItemMap.get(id) ?? itemMap.get(id))
      .filter((n): n is DropdownTreeItem<T> => n != null);
    if (!isControlledValue) setInternalValue(arr);
    onSelect?.(arr, selectedItems);
  }, [
    mode,
    checkable,
    selectedIds,
    descendantsMap,
    parentById,
    metadataItems,
    metadataItemMap,
    itemMap,
    isControlledValue,
    onSelect,
  ]);

  const commitMultipleSelection = useCallback(
    (nextRawIds: Set<string>) => {
      const currentSet =
        expandedSelectedIds instanceof Set ? expandedSelectedIds : new Set<string>();
      const cascaded =
        checkable && descendantsMap.size > 0
          ? applyCascadeSelection(currentSet, nextRawIds, descendantsMap, parentById)
          : nextRawIds;
      const compressed =
        checkable && descendantsMap.size > 0
          ? compressSelectionToParents(metadataItems, cascaded, descendantsMap)
          : cascaded;
      const nextIds = removeDisabledIdsFromSelection(compressed, metadataItemMap);
      const arr = Array.from(nextIds);
      const selectedItems = arr
        .map(id => metadataItemMap.get(id) ?? itemMap.get(id))
        .filter((n): n is DropdownTreeItem<T> => n != null);
      if (!isControlledValue) setInternalValue(arr);
      onSelect?.(arr, selectedItems);
      return nextIds;
    },
    [
      expandedSelectedIds,
      checkable,
      descendantsMap,
      parentById,
      metadataItems,
      metadataItemMap,
      itemMap,
      isControlledValue,
      onSelect,
    ]
  );

  const handleCheckableNodeToggle = useCallback(
    (nodeId: string, selectSubtree: boolean) => {
      const currentSet =
        expandedSelectedIds instanceof Set ? new Set(expandedSelectedIds) : new Set<string>();
      const subtreeLeaves = descendantsMap.get(nodeId) ?? [];
      const nextSet = new Set(currentSet);

      if (subtreeLeaves.length === 0) {
        if (selectSubtree) nextSet.add(nodeId);
        else nextSet.delete(nodeId);
      } else if (selectSubtree) {
        subtreeLeaves.forEach(id => nextSet.add(id));
      } else {
        subtreeLeaves.forEach(id => nextSet.delete(id));
        nextSet.delete(nodeId);
      }

      commitMultipleSelection(nextSet);
    },
    [expandedSelectedIds, descendantsMap, commitMultipleSelection]
  );

  const isSearchActive = allowSearch && search.trim().length > 0;
  const filteredItems = useMemo(() => {
    if (!isSearchActive) return sortedTreeSourceItems;
    if (onFilterItems) return onFilterItems(sortedTreeSourceItems, search.trim());
    return filterTreeBySearch(sortedTreeSourceItems, search);
  }, [sortedTreeSourceItems, search, isSearchActive, onFilterItems]);

  const selectedAncestorIds = useMemo(() => {
    if (!expandToSelected || value == null) return new Set<string>();
    if (mode === SINGLE_MODE && typeof value === 'string') {
      return collectAncestorIds(sortedTreeSourceItems, value);
    }
    if (mode === MULTIPLE_MODE && Array.isArray(value)) {
      const ancestors = new Set<string>();
      for (const id of value) {
        collectAncestorIds(sortedTreeSourceItems, id).forEach(ancestorId => ancestors.add(ancestorId));
      }
      return ancestors;
    }
    return new Set<string>();
  }, [expandToSelected, value, mode, sortedTreeSourceItems]);

  const computedDefaultOpenIds = useMemo(() => {
    if (controlledOpenIds) return controlledOpenIds;
    const base = defaultOpenIds ? new Set(defaultOpenIds) : new Set<string>();
    selectedAncestorIds.forEach(id => base.add(id));
    if (base.size > 0) return base;
    if (expandAllItems) return collectAllParentIds(filteredItems);
    return new Set<string>();
  }, [controlledOpenIds, defaultOpenIds, expandAllItems, filteredItems, selectedAncestorIds]);

  const isPanelOpen = inline || open;

  useEffect(() => {
    if (
      isPanelOpen &&
      expandAllItems &&
      !controlledOpenIds &&
      !defaultOpenIds &&
      !expandToSelected
    ) {
      treeRef.current?.expandAll();
    }
  }, [
    isPanelOpen,
    expandAllItems,
    filteredItems,
    controlledOpenIds,
    defaultOpenIds,
    expandToSelected,
  ]);

  const scrollTargetId = useMemo(() => {
    if (!isPanelOpen || value == null) return null;
    if (mode === SINGLE_MODE && typeof selectedIds === 'string') return selectedIds;
    if (mode === MULTIPLE_MODE && expandedSelectedIds instanceof Set && expandedSelectedIds.size > 0) {
      return findFirstSelectedInTreeOrder(filteredItems, expandedSelectedIds);
    }
    return null;
  }, [isPanelOpen, value, mode, selectedIds, expandedSelectedIds, filteredItems]);

  useLayoutEffect(() => {
    if (!isPanelOpen) {
      hasPerformedOpenScrollRef.current = false;
      return;
    }

    const target = scrollTargetId;
    if (!target) {
      hasPerformedOpenScrollRef.current = true;
      return;
    }
    if (hasPerformedOpenScrollRef.current) return;

    let frame2 = 0;
    const frame1 = requestAnimationFrame(() => {
      frame2 = requestAnimationFrame(() => {
        const treeContainer = treeRef.current?.getContainer();
        const targetEl = treeContainer?.querySelector(`[data-tree-id="${CSS.escape(target)}"]`);
        if (!targetEl) return;

        treeRef.current?.scrollToNode(target);
        hasPerformedOpenScrollRef.current = true;
      });
    });

    return () => {
      cancelAnimationFrame(frame1);
      if (frame2) cancelAnimationFrame(frame2);
    };
  }, [isPanelOpen, scrollTargetId, filteredItems]);

  const selectedItemSingle = useMemo(
    () =>
      mode === SINGLE_MODE && selectedIds != null && typeof selectedIds === 'string'
        ? (itemMap.get(selectedIds) ?? null)
        : null,
    [mode, selectedIds, itemMap]
  );
  const selectionItemMap = catalogItems ? metadataItemMap : itemMap;
  const selectedItemsMultiple = useMemo(
    () =>
      mode === MULTIPLE_MODE && selectedIds instanceof Set
        ? Array.from(selectedIds)
          .map(id => selectionItemMap.get(id))
          .filter((n): n is DropdownTreeItem<T> => n != null)
        : [],
    [mode, selectedIds, selectionItemMap]
  );

  const hasSelection = useMemo(() => {
    if (mode === SINGLE_MODE) return selectedItemSingle != null;
    return selectedItemsMultiple.length > 0;
  }, [mode, selectedItemSingle, selectedItemsMultiple]);

  const displayLabels = useMemo(() => {
    if (mode !== MULTIPLE_MODE || !(selectedIds instanceof Set) || selectedIds.size === 0) {
      return [] as string[];
    }
    if (checkable && parentById.size > 0) {
      return getCompressedDisplayLabels(metadataItems, selectedIds, descendantsMap);
    }
    return selectedItemsMultiple.map(item => item.label);
  }, [
    mode,
    selectedIds,
    checkable,
    parentById,
    descendantsMap,
    metadataItemMap,
    metadataItems,
    selectedItemsMultiple,
  ]);

  const selectedCount = useMemo(() => {
    if (mode !== MULTIPLE_MODE || !(selectedIds instanceof Set)) {
      return selectedItemsMultiple.length;
    }
    if (checkable && displayLabels.length > 0 && displayLabels.length < selectedIds.size) {
      return displayLabels.length;
    }
    return selectedItemsMultiple.length;
  }, [mode, checkable, displayLabels, selectedIds, selectedItemsMultiple]);

  const triggerLabel = useMemo(() => {
    if (displayText) {
      if (mode === SINGLE_MODE && selectedItemSingle) return displayText(selectedItemSingle);
      if (mode === MULTIPLE_MODE && selectedItemsMultiple.length > 0)
        return displayText(selectedItemsMultiple);
    }
    if (mode === SINGLE_MODE && selectedItemSingle) return selectedItemSingle.label;
    if (mode === MULTIPLE_MODE && displayLabels.length > 0) {
      if (!resolvedCountable && displayLabels.length > maxVisibleLabels) {
        return `${selectedCount} items selected`;
      }
      const visibleLabels = displayLabels.slice(0, maxVisibleLabels);
      const labels = visibleLabels.join(', ');
      if (displayLabels.length > maxVisibleLabels) {
        return resolvedCountable
          ? `${labels}, ...`
          : `${labels}, +${displayLabels.length - maxVisibleLabels} more`;
      }
      return labels;
    }
    return placeholder;
  }, [
    displayText,
    displayLabels,
    mode,
    maxVisibleLabels,
    placeholder,
    resolvedCountable,
    selectedCount,
    selectedItemSingle,
    selectedItemsMultiple,
  ]);

  const showCleanButton = resolvedCleanable && hasSelection && !disabled;
  const showCountBadge =
    resolvedCountable && mode === MULTIPLE_MODE && selectedCount > 0 && !displayText;

  const handleClean = useCallback(
    (event: React.MouseEvent) => {
      event.stopPropagation();
      event.preventDefault();
      if (!isControlledValue) {
        setInternalValue(mode === SINGLE_MODE ? null : []);
      }
      if (mode === SINGLE_MODE) {
        onSelect?.(null, null);
      } else {
        onSelect?.([], []);
      }
      onClean?.(event);
    },
    [isControlledValue, mode, onClean, onSelect]
  );

  const handleSelectSingle = useCallback(
    (id: string | null) => {
      if (id == null) return;
      const item = itemMap.get(id);
      if (item?.disabled) return;
      if (item) {
        if (!isControlledValue) setInternalValue(id);
        onSelect?.(id, item);
        if (!inline) setOpen(false);
      }
    },
    [itemMap, isControlledValue, onSelect, setOpen, inline]
  );

  const handleSelectionChangeMultiple = useCallback(
    (ids: Set<string>) => {
      commitMultipleSelection(ids);
    },
    [commitMultipleSelection]
  );

  useImperativeHandle(
    ref,
    () => ({
      open: () => setOpen(true),
      close: () => setOpen(false),
      toggle: () => setOpen(!open),
      expandAll: () => treeRef.current?.expandAll(),
      collapseAll: () => treeRef.current?.collapseAll(),
    }),
    [open, setOpen]
  );

  const handleSearchChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const q = e.target.value;
      setSearch(q);
      onSearchChange?.(q);
    },
    [onSearchChange]
  );

  const handleLoadChildren = useCallback(
    async (nodeId: string, node: DropdownTreeItem<T>) => {
      if (!isAsyncChildLoad || isSearchActive) return undefined;
      const children = await onLoadChildren!(nodeId, node);
      if (children) {
        setResolvedItems(prev => {
          const next = updateNodeChildrenInTree(prev, nodeId, children);
          onItemsChange?.(next);
          return next;
        });
      }
      return children;
    },
    [isAsyncChildLoad, onLoadChildren, isSearchActive, onItemsChange]
  );

  const effectiveOnLoadChildren =
    isAsyncChildLoad && !isSearchActive ? handleLoadChildren : undefined;

  const selectionMode = mode === SINGLE_MODE ? SINGLE_MODE : MULTIPLE_MODE;
  const treeSelectedId =
    mode === SINGLE_MODE && selectedIds != null && typeof selectedIds === 'string'
      ? selectedIds
      : null;
  const treeSelectedIds =
    mode === MULTIPLE_MODE && expandedSelectedIds instanceof Set
      ? expandedSelectedIds
      : undefined;

  const defaultRenderItem = useCallback(
    (props: TreeViewRenderProps<T>) => {
      const { node, toggle, isExpanded, hasChildren, depth, isSelected, isLoading, isDisabled } =
        props;
      const showCheckbox = mode === MULTIPLE_MODE && checkable;
      const showSelectionCheck = mode === SINGLE_MODE;
      const checkboxState =
        showCheckbox && expandedSelectedIds instanceof Set
          ? isDisabled
            ? { checked: false, indeterminate: false }
            : getNodeCheckboxState(expandedSelectedIds, node.id, descendantsMap)
          : null;

      const labelClassName = cn(showSelectionCheck && isSelected ? 'font-bold' : 'font-medium');
      const indentPx = depth * DROPDOWN_TREE_INDENT_SIZE;

      const itemContent = onItemRender ? (
        onItemRender({ ...props, showCheckbox })
      ) : fitContentWidth ? (
        <span className={cn(labelClassName, 'whitespace-nowrap')}>{node.label}</span>
      ) : (
        <TruncatedText text={node.label} className={labelClassName} />
      );

      return (
        <div
          className={cn(
            'group flex items-center gap-1 transition-all duration-150 border border-transparent'
          )}
          style={{ paddingLeft: indentPx }}
        >
          <button
            type="button"
            disabled={isLoading}
            onClick={async e => {
              e.stopPropagation();
              if (hasChildren && !isLoading) await toggle();
            }}
            onDoubleClick={e => e.stopPropagation()}
            className={cn(
              'w-5 h-5 flex items-center justify-center rounded transition-colors shrink-0',
              hasChildren && 'hover:bg-muted flex',
              isLoading && 'opacity-50 cursor-wait'
            )}
          >
            {hasChildren ? (
              isLoading ? (
                <span className="w-3 h-3 border-2 border-muted-foreground border-t-transparent rounded-full animate-spin" />
              ) : (
                <ChevronRight
                  className={cn(
                    'w-4 h-4 transition-transform duration-200',
                    isExpanded && 'rotate-90'
                  )}
                />
              )
            ) : (
              <span className="w-4 h-4 shrink-0" />
            )}
          </button>
          <div className={cn('flex min-w-0 flex-1 items-center', showCheckbox ? 'gap-2' : 'gap-1')}>
            {showCheckbox && checkboxState && (
              <Checkbox
                checked={checkboxState.checked}
                indeterminate={checkboxState.indeterminate}
                disabled={isDisabled}
                onChange={nextValue => {
                  if (!isDisabled && nextValue !== 'indeterminate') {
                    handleCheckableNodeToggle(node.id, nextValue === true);
                  }
                }}
                onClick={e => e.stopPropagation()}
                onDoubleClick={e => e.stopPropagation()}
              />
            )}
            {renderIcon ? (
              <span className="inline-flex shrink-0 items-center justify-center">
                {renderIcon({ ...props, showCheckbox })}
              </span>
            ) : null}
            <div className={cn('flex items-center gap-1 flex-1', !fitContentWidth && 'min-w-0')}>
              {itemContent}
            </div>
          </div>
        </div>
      );
    },
    [
      mode,
      checkable,
      disableFolderSelection,
      onItemRender,
      renderIcon,
      fitContentWidth,
      expandedSelectedIds,
      descendantsMap,
      handleCheckableNodeToggle,
    ]
  );

  const effectiveRenderItem = useCallback(
    (props: TreeViewRenderProps<T>) => {
      if (renderTreeNode) {
        return renderTreeNode({
          ...props,
          showCheckbox: mode === MULTIPLE_MODE && checkable,
        });
      }
      return defaultRenderItem(props);
    },
    [renderTreeNode, defaultRenderItem, mode, checkable]
  );

  const showEmpty = filteredItems.length === 0;

  const treeContent: ReactNode =
    showEmpty && emptyContent ? (
      <div className="p-2">{emptyContent}</div>
    ) : (
      <div className="p-1">
        <TreeView<T>
          ref={treeRef}
          data={filteredItems}
          selectionMode={selectionMode}
          selectedId={treeSelectedId}
          selectedIds={treeSelectedIds}
          onSelect={mode === SINGLE_MODE ? handleSelectSingle : undefined}
          onSelectionChange={mode === MULTIPLE_MODE ? handleSelectionChangeMultiple : undefined}
          disableFolderSelection={disableFolderSelection}
          autoFocusFirstItem={false}
          {...(controlledOpenIds
            ? { openIds: controlledOpenIds }
            : { defaultOpenIds: computedDefaultOpenIds })}
          onLoadChildren={effectiveOnLoadChildren}
          renderItem={effectiveRenderItem}
          indentSize={0}
          virtualized={virtualized}
          className="border-0 p-0"
          itemClassName="py-1.5 px-2 rounded-md cursor-pointer hover:bg-muted text-sm data-[selected=true]:bg-accent data-[selected=true]:text-accent-foreground"
        />
      </div>
    );

  const panelContent = (
    <>
      {allowSearch && (
        <div className="p-2 border-b">
          <Input
            placeholder={searchPlaceholder}
            value={search}
            onChange={handleSearchChange}
            className="h-8"
          />
        </div>
      )}
      {headerActions && <div className="p-2 border-b flex flex-wrap gap-1">{headerActions}</div>}
      <div className="overflow-auto" style={{ maxHeight: `${contentHeight}px` }}>
        {renderTree ? renderTree(treeContent) : treeContent}
      </div>
    </>
  );

  const triggerRenderProps: DropdownTreeTriggerRenderProps = {
    open,
    onOpenChange: setOpen,
    disabled,
    hasSelection,
    triggerLabel,
    placeholder,
    toggle: () => setOpen(!open),
    clear: showCleanButton ? handleClean : undefined,
  };

  const defaultTrigger = (
    <button
      type="button"
      role="combobox"
      aria-expanded={open}
      disabled={disabled}
      className={cn(
        'flex h-8 w-full min-w-0 items-center justify-start gap-2 overflow-hidden rounded-md border border-input bg-background px-3 text-sm font-normal shadow-xs transition-all outline-none',
        'focus-visible:border-ring! data-[state=open]:border-ring!',
        'hover:bg-[color-mix(in_oklab,var(--accent)80%,transparent)]!',
        'disabled:pointer-events-none disabled:opacity-50',
        triggerClassName
      )}
    >
      <span className="flex min-w-0 flex-1 items-center justify-start overflow-hidden text-left">
        {hasSelection ? (
          showCountBadge ? (
            <span className="flex min-w-0 max-w-full items-center justify-start gap-1.5 overflow-hidden text-left">
              <span className={cn('min-w-0 truncate text-left', !displayText && 'text-foreground')}>
                {triggerLabel}
              </span>
              <Badge
                variant="default"
                className="h-5 min-w-5 shrink-0 rounded-full px-1.5 text-xs font-medium"
              >
                {selectedCount}
              </Badge>
            </span>
          ) : (
            <span
              className={cn('min-w-0 flex-1 truncate text-left', !displayText && 'text-foreground')}
            >
              {triggerLabel}
            </span>
          )
        ) : (
          <span className="min-w-0 flex-1 truncate text-left text-muted-foreground">{placeholder}</span>
        )}
      </span>
      <span className="ml-auto flex shrink-0 items-center gap-1 pl-2">
        {showCleanButton ? (
          <span
            role="button"
            tabIndex={0}
            aria-label="Clear selection"
            className="rounded-sm p-0.5 opacity-50 transition-opacity hover:opacity-100"
            onKeyDown={event => {
              if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                handleClean(event as unknown as React.MouseEvent);
              }
            }}
            onClick={handleClean}
          >
            <X className="h-4 w-4" />
          </span>
        ) : null}
        <ChevronDown className="h-4 w-4 shrink-0 opacity-50" />
      </span>
    </button>
  );

  if (inline) {
    return (
      <div
        data-slot="dropdowntree"
        data-inline
        className={cn('w-0 min-w-full max-w-full overflow-hidden', className)}
      >
        {panelContent}
      </div>
    );
  }

  return (
    <div
      data-slot="dropdowntree"
      className={cn('w-0 min-w-full max-w-full overflow-hidden', className)}
    >
      <Popover open={open} onOpenChange={setOpen} modal={popoverModal}>
        <PopoverTrigger asChild disabled={disabled}>
          {renderTrigger ? renderTrigger(triggerRenderProps) : defaultTrigger}
        </PopoverTrigger>
        <PopoverContent
          className={cn(
            'p-0 rounded-sm overflow-hidden',
            fitContentWidth ? 'w-max min-w-(--radix-popover-trigger-width)' : 'min-w-[200px]'
          )}
          align="start"
          container={popoverContainer}
          style={{
            width: fitContentWidth ? undefined : tokens?.popoverWidth,
            maxWidth: tokens?.popoverMaxWidth,
          }}
          onWheelCapture={event => {
            event.stopPropagation();
          }}
        >
          {panelContent}
        </PopoverContent>
      </Popover>
    </div>
  );
}

export const DropdownTree = forwardRef(DropdownTreeInner) as <T = unknown>(
  props: DropdownTreeProps<T> & { ref?: React.ForwardedRef<DropdownTreeHandle> }
) => ReturnType<typeof DropdownTreeInner>;
