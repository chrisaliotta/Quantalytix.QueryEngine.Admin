import { useReducer, useCallback, useMemo, useRef, useEffect } from 'react'

// ─────────────────────────────────────────────────────────────────────────────
// Types
// ─────────────────────────────────────────────────────────────────────────────

export interface TreeNode<T = unknown> {
  id: string
  label: string
  children?: TreeNode<T>[]
  data?: T
  disabled?: boolean
}

export interface FlattenedNode<T = unknown> {
  id: string
  label: string
  depth: number
  isExpanded: boolean
  hasChildren: boolean
  isSelected: boolean
  isFocused: boolean
  isDisabled: boolean
  parentId: string | null
  node: TreeNode<T>
  index: number
  /** Path of ancestor ids including this node (root-first) */
  path: string[]
  /** Zero-based index among this node's siblings */
  siblingIndex: number
}

export interface TreeState {
  openIds: Set<string>
  /** Single selection (for single-select mode) */
  selectedId: string | null
  /** Multiple selections (for multi-select mode) */
  selectedIds: Set<string>
  focusedId: string | null
  /** Anchor for range selection (Shift+click) */
  anchorId: string | null
}

export type SelectionMode = 'single' | 'multiple' | 'none'

// ─────────────────────────────────────────────────────────────────────────────
// Actions
// ─────────────────────────────────────────────────────────────────────────────

export type TreeAction =
  | { type: 'TOGGLE'; id: string }
  | { type: 'EXPAND'; id: string }
  | { type: 'COLLAPSE'; id: string }
  | { type: 'EXPAND_TO'; id: string; ancestors: string[] }
  | { type: 'EXPAND_ALL'; ids: string[] }
  | { type: 'COLLAPSE_ALL' }
  // Single selection
  | { type: 'SELECT'; id: string | null }
  // Multi-selection
  | { type: 'SELECT_MULTI'; id: string; append?: boolean }
  | { type: 'TOGGLE_SELECT'; id: string }
  | { type: 'SELECT_RANGE'; fromId: string; toId: string; flatNodeIds: string[] }
  | { type: 'SELECT_ALL'; ids: string[] }
  | { type: 'CLEAR_SELECTION' }
  | { type: 'SET_SELECTED_IDS'; ids: Set<string> }
  | { type: 'SET_ANCHOR'; id: string | null }
  // Focus
  | { type: 'FOCUS'; id: string | null }
  | { type: 'SET_OPEN_IDS'; ids: Set<string> }
  | { type: 'SYNC_CONTROLLED'; openIds?: Set<string>; selectedId?: string | null; selectedIds?: Set<string> }

// ─────────────────────────────────────────────────────────────────────────────
// Reducer
// ─────────────────────────────────────────────────────────────────────────────

export function treeReducer(state: TreeState, action: TreeAction): TreeState {
  switch (action.type) {
    case 'TOGGLE': {
      const newOpenIds = new Set(state.openIds)
      if (newOpenIds.has(action.id)) {
        newOpenIds.delete(action.id)
      } else {
        newOpenIds.add(action.id)
      }
      return { ...state, openIds: newOpenIds }
    }
    case 'EXPAND': {
      if (state.openIds.has(action.id)) return state
      const newOpenIds = new Set(state.openIds)
      newOpenIds.add(action.id)
      return { ...state, openIds: newOpenIds }
    }
    case 'COLLAPSE': {
      if (!state.openIds.has(action.id)) return state
      const newOpenIds = new Set(state.openIds)
      newOpenIds.delete(action.id)
      return { ...state, openIds: newOpenIds }
    }
    case 'EXPAND_TO': {
      const newOpenIds = new Set(state.openIds)
      action.ancestors.forEach((id) => newOpenIds.add(id))
      return { ...state, openIds: newOpenIds }
    }
    case 'EXPAND_ALL': {
      return { ...state, openIds: new Set(action.ids) }
    }
    case 'COLLAPSE_ALL': {
      return { ...state, openIds: new Set() }
    }
    // Single selection
    case 'SELECT': {
      if (state.selectedId === action.id) return state
      return { ...state, selectedId: action.id, anchorId: action.id }
    }
    // Multi-selection: select one item, optionally appending to existing selection
    case 'SELECT_MULTI': {
      const newSelectedIds = action.append ? new Set(state.selectedIds) : new Set<string>()
      newSelectedIds.add(action.id)
      return { ...state, selectedIds: newSelectedIds, anchorId: action.id }
    }
    // Multi-selection: toggle a single item
    case 'TOGGLE_SELECT': {
      const newSelectedIds = new Set(state.selectedIds)
      if (newSelectedIds.has(action.id)) {
        newSelectedIds.delete(action.id)
      } else {
        newSelectedIds.add(action.id)
      }
      return { ...state, selectedIds: newSelectedIds, anchorId: action.id }
    }
    // Multi-selection: range select (Shift+click)
    case 'SELECT_RANGE': {
      const fromIndex = action.flatNodeIds.indexOf(action.fromId)
      const toIndex = action.flatNodeIds.indexOf(action.toId)
      if (fromIndex === -1 || toIndex === -1) return state
      
      const start = Math.min(fromIndex, toIndex)
      const end = Math.max(fromIndex, toIndex)
      const rangeIds = action.flatNodeIds.slice(start, end + 1)
      
      const newSelectedIds = new Set(state.selectedIds)
      rangeIds.forEach((id) => newSelectedIds.add(id))
      return { ...state, selectedIds: newSelectedIds }
    }
    // Multi-selection: select all
    case 'SELECT_ALL': {
      return { ...state, selectedIds: new Set(action.ids) }
    }
    // Multi-selection: clear all
    case 'CLEAR_SELECTION': {
      return { ...state, selectedIds: new Set(), selectedId: null, anchorId: null }
    }
    case 'SET_SELECTED_IDS': {
      return { ...state, selectedIds: action.ids }
    }
    case 'SET_ANCHOR': {
      return { ...state, anchorId: action.id }
    }
    case 'FOCUS': {
      if (state.focusedId === action.id) return state
      return { ...state, focusedId: action.id }
    }
    case 'SET_OPEN_IDS': {
      return { ...state, openIds: action.ids }
    }
    case 'SYNC_CONTROLLED': {
      return {
        ...state,
        ...(action.openIds !== undefined && { openIds: action.openIds }),
        ...(action.selectedId !== undefined && { selectedId: action.selectedId }),
        ...(action.selectedIds !== undefined && { selectedIds: action.selectedIds }),
      }
    }
    default:
      return state
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Tree Utilities
// ─────────────────────────────────────────────────────────────────────────────

/** Build a map from node id to parent id */
export function buildParentMap<T>(nodes: TreeNode<T>[]): Map<string, string | null> {
  const map = new Map<string, string | null>()

  function traverse(node: TreeNode<T>, parentId: string | null) {
    map.set(node.id, parentId)
    node.children?.forEach((child) => traverse(child, node.id))
  }

  nodes.forEach((node) => traverse(node, null))
  return map
}

/** Build a map from id to node for O(1) lookups */
export function buildNodeMap<T>(nodes: TreeNode<T>[]): Map<string, TreeNode<T>> {
  const map = new Map<string, TreeNode<T>>()

  function traverse(node: TreeNode<T>) {
    map.set(node.id, node)
    node.children?.forEach(traverse)
  }

  nodes.forEach(traverse)
  return map
}

/** Get all ancestor ids for a given node id */
export function getAncestorIds(id: string, parentMap: Map<string, string | null>): string[] {
  const ancestors: string[] = []
  let current = parentMap.get(id)
  while (current !== null && current !== undefined) {
    ancestors.push(current)
    current = parentMap.get(current)
  }
  return ancestors
}

/** Get all parent node ids (nodes with children) */
export function getAllParentIds<T>(nodes: TreeNode<T>[]): string[] {
  const ids: string[] = []

  function traverse(node: TreeNode<T>) {
    if (node.children && node.children.length > 0) {
      ids.push(node.id)
      node.children.forEach(traverse)
    }
  }

  nodes.forEach(traverse)
  return ids
}

/** Get all node ids from tree */
export function getAllNodeIds<T>(nodes: TreeNode<T>[]): string[] {
  const ids: string[] = []

  function traverse(node: TreeNode<T>) {
    ids.push(node.id)
    node.children?.forEach(traverse)
  }

  nodes.forEach(traverse)
  return ids
}

/** Whether a node should show as expandable */
export function nodeHasChildren<T>(node: TreeNode<T>, lazyLoad = false): boolean {
  if (lazyLoad) {
    return Array.isArray(node.children)
  }
  return !!node.children && node.children.length > 0
}

/** Immutably replace children for a node by id */
export function updateNodeChildrenInTree<T>(
  nodes: TreeNode<T>[],
  nodeId: string,
  children: TreeNode<T>[]
): TreeNode<T>[] {
  return nodes.map(node => {
    if (node.id === nodeId) {
      return { ...node, children }
    }
    if (node.children) {
      return { ...node, children: updateNodeChildrenInTree(node.children, nodeId, children) }
    }
    return node
  })
}

/** Flatten tree into visible rows based on open state */
export function flattenTree<T>(
  nodes: TreeNode<T>[],
  state: TreeState,
  _parentMap: Map<string, string | null>,
  selectionMode: SelectionMode = 'single',
  lazyLoad = false
): FlattenedNode<T>[] {
  const result: FlattenedNode<T>[] = []

  function traverse(
    node: TreeNode<T>,
    depth: number,
    parentId: string | null,
    path: string[],
    siblingIndex: number
  ) {
    const hasChildren = nodeHasChildren(node, lazyLoad)
    const isExpanded = state.openIds.has(node.id)
    
    // Determine selection based on mode
    const isSelected = selectionMode === 'multiple' 
      ? state.selectedIds.has(node.id)
      : state.selectedId === node.id

    result.push({
      id: node.id,
      label: node.label,
      depth,
      isExpanded,
      hasChildren,
      isSelected,
      isFocused: state.focusedId === node.id,
      isDisabled: !!node.disabled,
      parentId,
      node,
      index: result.length,
      path,
      siblingIndex,
    })

    if (hasChildren && isExpanded) {
      node.children!.forEach((child, index) =>
        traverse(child, depth + 1, node.id, [...path, child.id], index)
      )
    }
  }

  nodes.forEach((node, index) => traverse(node, 0, null, [node.id], index))
  return result
}

// ─────────────────────────────────────────────────────────────────────────────
// Keyboard Navigation Helpers
// ─────────────────────────────────────────────────────────────────────────────

export function getNextVisibleNode(
  flatNodes: FlattenedNode[],
  currentId: string | null
): string | null {
  if (!currentId) return flatNodes[0]?.id ?? null
  const currentIndex = flatNodes.findIndex((n) => n.id === currentId)
  if (currentIndex === -1 || currentIndex >= flatNodes.length - 1) return currentId
  // Skip disabled nodes
  for (let i = currentIndex + 1; i < flatNodes.length; i++) {
    if (!flatNodes[i].isDisabled) return flatNodes[i].id
  }
  return currentId
}

export function getPrevVisibleNode(
  flatNodes: FlattenedNode[],
  currentId: string | null
): string | null {
  if (!currentId) return flatNodes[flatNodes.length - 1]?.id ?? null
  const currentIndex = flatNodes.findIndex((n) => n.id === currentId)
  if (currentIndex <= 0) return currentId
  // Skip disabled nodes
  for (let i = currentIndex - 1; i >= 0; i--) {
    if (!flatNodes[i].isDisabled) return flatNodes[i].id
  }
  return currentId
}

export function getFirstVisibleNode(flatNodes: FlattenedNode[]): string | null {
  const first = flatNodes.find((n) => !n.isDisabled)
  return first?.id ?? null
}

export function getLastVisibleNode(flatNodes: FlattenedNode[]): string | null {
  for (let i = flatNodes.length - 1; i >= 0; i--) {
    if (!flatNodes[i].isDisabled) return flatNodes[i].id
  }
  return null
}

// ─────────────────────────────────────────────────────────────────────────────
// Hook: useTreeMachine
// ─────────────────────────────────────────────────────────────────────────────

export interface UseTreeMachineOptions<T = unknown> {
  data: TreeNode<T>[]
  /** Selection mode: 'single', 'multiple', or 'none' */
  selectionMode?: SelectionMode
  /** Controlled open ids */
  openIds?: Set<string>
  /** Controlled selected id (single-select mode) */
  selectedId?: string | null
  /** Controlled selected ids (multi-select mode) */
  selectedIds?: Set<string>
  /** Default open ids for uncontrolled mode */
  defaultOpenIds?: Set<string>
  /** Default selected id for uncontrolled single-select mode */
  defaultSelectedId?: string | null
  /** Default selected ids for uncontrolled multi-select mode */
  defaultSelectedIds?: Set<string>
  /** Callbacks */
  onToggle?: (id: string, isOpen: boolean) => void
  /** Callback for single selection changes */
  onSelect?: (id: string | null) => void
  /** Callback for multi-selection changes */
  onSelectionChange?: (ids: Set<string>) => void
  onFocus?: (id: string | null) => void
  /** Treat nodes with `children: []` as expandable (for async loading) */
  lazyLoad?: boolean
}

export interface UseTreeMachineReturn<T = unknown> {
  /** Current state */
  state: TreeState
  /** Selection mode */
  selectionMode: SelectionMode
  /** Flattened visible nodes */
  flatNodes: FlattenedNode<T>[]
  /** Parent map for ancestry lookups */
  parentMap: Map<string, string | null>
  /** Node map for O(1) lookups */
  nodeMap: Map<string, TreeNode<T>>
  /** Imperative actions */
  toggle: (id: string) => void
  expand: (id: string) => void
  collapse: (id: string) => void
  expandTo: (id: string) => void
  expandAll: () => void
  collapseAll: () => void
  /** Single select */
  select: (id: string | null) => void
  /** Multi-select: select one item (replace or append based on modifier) */
  selectMulti: (id: string, append?: boolean) => void
  /** Multi-select: toggle selection of one item */
  toggleSelect: (id: string) => void
  /** Multi-select: range select from anchor to target */
  selectRange: (toId: string) => void
  /** Multi-select: select all visible nodes */
  selectAll: () => void
  /** Clear all selections */
  clearSelection: () => void
  /** Check if an id is selected */
  isSelected: (id: string) => boolean
  /** Get array of selected ids */
  getSelectedIds: () => string[]
  /** Focus */
  focus: (id: string | null) => void
  /** Keyboard navigation */
  focusNext: () => void
  focusPrev: () => void
  focusFirst: () => void
  focusLast: () => void
  /** Check if controlled */
  isControlled: boolean
}

export function useTreeMachine<T = unknown>(
  options: UseTreeMachineOptions<T>
): UseTreeMachineReturn<T> {
  const {
    data,
    selectionMode = 'single',
    openIds: controlledOpenIds,
    selectedId: controlledSelectedId,
    selectedIds: controlledSelectedIds,
    defaultOpenIds = new Set(),
    defaultSelectedId = null,
    defaultSelectedIds = new Set(),
    onToggle,
    onSelect,
    onSelectionChange,
    onFocus,
    lazyLoad = false,
  } = options

  const isOpenControlled = controlledOpenIds !== undefined
  const isSelectedControlled = controlledSelectedId !== undefined
  const isSelectedIdsControlled = controlledSelectedIds !== undefined

  // Initialize state
  const [state, dispatch] = useReducer(treeReducer, {
    openIds: controlledOpenIds ?? defaultOpenIds,
    selectedId: controlledSelectedId ?? defaultSelectedId,
    selectedIds: controlledSelectedIds ?? defaultSelectedIds,
    focusedId: null,
    anchorId: null,
  })

  // Track previous controlled values to detect changes
  const prevControlled = useRef({ 
    openIds: controlledOpenIds, 
    selectedId: controlledSelectedId,
    selectedIds: controlledSelectedIds 
  })

  // Sync controlled state when props change
  useEffect(() => {
    const prev = prevControlled.current
    const updates: Partial<TreeState> = {}

    if (isOpenControlled && controlledOpenIds !== prev.openIds) {
      updates.openIds = controlledOpenIds
    }
    if (isSelectedControlled && controlledSelectedId !== prev.selectedId) {
      updates.selectedId = controlledSelectedId
    }
    if (isSelectedIdsControlled && controlledSelectedIds !== prev.selectedIds) {
      updates.selectedIds = controlledSelectedIds
    }

    if (Object.keys(updates).length > 0) {
      dispatch({ type: 'SYNC_CONTROLLED', ...updates })
    }

    prevControlled.current = { 
      openIds: controlledOpenIds, 
      selectedId: controlledSelectedId,
      selectedIds: controlledSelectedIds 
    }
  }, [controlledOpenIds, controlledSelectedId, controlledSelectedIds, isOpenControlled, isSelectedControlled, isSelectedIdsControlled])

  // Effective state (use controlled values if provided)
  const effectiveState = useMemo(
    () => ({
      openIds: isOpenControlled ? controlledOpenIds! : state.openIds,
      selectedId: isSelectedControlled ? controlledSelectedId! : state.selectedId,
      selectedIds: isSelectedIdsControlled ? controlledSelectedIds! : state.selectedIds,
      focusedId: state.focusedId,
      anchorId: state.anchorId,
    }),
    [
      isOpenControlled,
      controlledOpenIds,
      isSelectedControlled,
      controlledSelectedId,
      isSelectedIdsControlled,
      controlledSelectedIds,
      state.openIds,
      state.selectedId,
      state.selectedIds,
      state.focusedId,
      state.anchorId,
    ]
  )

  // Memoize tree structure helpers
  const parentMap = useMemo(() => buildParentMap(data), [data])
  const nodeMap = useMemo(() => buildNodeMap(data), [data])
  const flatNodes = useMemo(
    () => flattenTree(data, effectiveState, parentMap, selectionMode, lazyLoad),
    [data, effectiveState, parentMap, selectionMode]
  )

  // Actions with callbacks
  const toggle = useCallback(
    (id: string) => {
      const isOpen = effectiveState.openIds.has(id)
      const newIsOpen = !isOpen
      if (!isOpenControlled) {
        dispatch({ type: 'TOGGLE', id })
      }
      onToggle?.(id, newIsOpen)
    },
    [effectiveState.openIds, isOpenControlled, onToggle]
  )

  const expand = useCallback(
    (id: string) => {
      if (!effectiveState.openIds.has(id)) {
        if (!isOpenControlled) {
          dispatch({ type: 'EXPAND', id })
        }
        onToggle?.(id, true)
      }
    },
    [effectiveState.openIds, isOpenControlled, onToggle]
  )

  const collapse = useCallback(
    (id: string) => {
      if (effectiveState.openIds.has(id)) {
        if (!isOpenControlled) {
          dispatch({ type: 'COLLAPSE', id })
        }
        onToggle?.(id, false)
      }
    },
    [effectiveState.openIds, isOpenControlled, onToggle]
  )

  const expandTo = useCallback(
    (id: string) => {
      const ancestors = getAncestorIds(id, parentMap)
      if (ancestors.length > 0) {
        if (!isOpenControlled) {
          dispatch({ type: 'EXPAND_TO', id, ancestors })
        }
        ancestors.forEach((ancestorId) => {
          if (!effectiveState.openIds.has(ancestorId)) {
            onToggle?.(ancestorId, true)
          }
        })
      }
    },
    [parentMap, isOpenControlled, effectiveState.openIds, onToggle]
  )

  const expandAll = useCallback(() => {
    const allParentIds = getAllParentIds(data)
    if (!isOpenControlled) {
      dispatch({ type: 'EXPAND_ALL', ids: allParentIds })
    }
    allParentIds.forEach((id) => {
      if (!effectiveState.openIds.has(id)) {
        onToggle?.(id, true)
      }
    })
  }, [data, isOpenControlled, effectiveState.openIds, onToggle])

  const collapseAll = useCallback(() => {
    if (!isOpenControlled) {
      dispatch({ type: 'COLLAPSE_ALL' })
    }
    effectiveState.openIds.forEach((id) => {
      onToggle?.(id, false)
    })
  }, [isOpenControlled, effectiveState.openIds, onToggle])

  // Single selection
  const select = useCallback(
    (id: string | null) => {
      if (effectiveState.selectedId !== id) {
        if (!isSelectedControlled) {
          dispatch({ type: 'SELECT', id })
        }
      }
      onSelect?.(id)
    },
    [effectiveState.selectedId, isSelectedControlled, onSelect]
  )

  // Multi-selection: select one item
  const selectMulti = useCallback(
    (id: string, append = false) => {
      if (!isSelectedIdsControlled) {
        dispatch({ type: 'SELECT_MULTI', id, append })
      }
      const newSelectedIds = append ? new Set(effectiveState.selectedIds) : new Set<string>()
      newSelectedIds.add(id)
      onSelectionChange?.(newSelectedIds)
    },
    [effectiveState.selectedIds, isSelectedIdsControlled, onSelectionChange]
  )

  // Multi-selection: toggle one item
  const toggleSelect = useCallback(
    (id: string) => {
      if (!isSelectedIdsControlled) {
        dispatch({ type: 'TOGGLE_SELECT', id })
      }
      const newSelectedIds = new Set(effectiveState.selectedIds)
      if (newSelectedIds.has(id)) {
        newSelectedIds.delete(id)
      } else {
        newSelectedIds.add(id)
      }
      onSelectionChange?.(newSelectedIds)
    },
    [effectiveState.selectedIds, isSelectedIdsControlled, onSelectionChange]
  )

  // Multi-selection: range select
  const selectRange = useCallback(
    (toId: string) => {
      const fromId = effectiveState.anchorId
      if (!fromId) {
        selectMulti(toId)
        return
      }
      
      const flatNodeIds = flatNodes.filter(n => !n.isDisabled).map((n) => n.id)
      const fromIndex = flatNodeIds.indexOf(fromId)
      const toIndex = flatNodeIds.indexOf(toId)
      
      if (fromIndex === -1 || toIndex === -1) return
      
      const start = Math.min(fromIndex, toIndex)
      const end = Math.max(fromIndex, toIndex)
      const rangeIds = flatNodeIds.slice(start, end + 1)
      
      if (!isSelectedIdsControlled) {
        dispatch({ type: 'SELECT_RANGE', fromId, toId, flatNodeIds })
      }
      
      const newSelectedIds = new Set(effectiveState.selectedIds)
      rangeIds.forEach((id) => newSelectedIds.add(id))
      onSelectionChange?.(newSelectedIds)
    },
    [effectiveState.anchorId, effectiveState.selectedIds, flatNodes, isSelectedIdsControlled, onSelectionChange, selectMulti]
  )

  // Multi-selection: select all
  const selectAll = useCallback(() => {
    const allVisibleIds = flatNodes.filter(n => !n.isDisabled).map((n) => n.id)
    if (!isSelectedIdsControlled) {
      dispatch({ type: 'SELECT_ALL', ids: allVisibleIds })
    }
    onSelectionChange?.(new Set(allVisibleIds))
  }, [flatNodes, isSelectedIdsControlled, onSelectionChange])

  // Clear selection
  const clearSelection = useCallback(() => {
    if (!isSelectedControlled && !isSelectedIdsControlled) {
      dispatch({ type: 'CLEAR_SELECTION' })
    }
    onSelect?.(null)
    onSelectionChange?.(new Set())
  }, [isSelectedControlled, isSelectedIdsControlled, onSelect, onSelectionChange])

  // Check if selected
  const isSelected = useCallback(
    (id: string) => {
      if (selectionMode === 'multiple') {
        return effectiveState.selectedIds.has(id)
      }
      return effectiveState.selectedId === id
    },
    [selectionMode, effectiveState.selectedIds, effectiveState.selectedId]
  )

  // Get selected ids
  const getSelectedIds = useCallback(() => {
    if (selectionMode === 'multiple') {
      return Array.from(effectiveState.selectedIds)
    }
    return effectiveState.selectedId ? [effectiveState.selectedId] : []
  }, [selectionMode, effectiveState.selectedIds, effectiveState.selectedId])

  const focus = useCallback(
    (id: string | null) => {
      if (state.focusedId !== id) {
        dispatch({ type: 'FOCUS', id })
        onFocus?.(id)
      }
    },
    [state.focusedId, onFocus]
  )

  // Keyboard navigation
  const focusNext = useCallback(() => {
    const nextId = getNextVisibleNode(flatNodes, state.focusedId)
    if (nextId) focus(nextId)
  }, [flatNodes, state.focusedId, focus])

  const focusPrev = useCallback(() => {
    const prevId = getPrevVisibleNode(flatNodes, state.focusedId)
    if (prevId) focus(prevId)
  }, [flatNodes, state.focusedId, focus])

  const focusFirst = useCallback(() => {
    const firstId = getFirstVisibleNode(flatNodes)
    if (firstId) focus(firstId)
  }, [flatNodes, focus])

  const focusLast = useCallback(() => {
    const lastId = getLastVisibleNode(flatNodes)
    if (lastId) focus(lastId)
  }, [flatNodes, focus])

  return {
    state: effectiveState,
    selectionMode,
    flatNodes,
    parentMap,
    nodeMap,
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
    isSelected,
    getSelectedIds,
    focus,
    focusNext,
    focusPrev,
    focusFirst,
    focusLast,
    isControlled: isOpenControlled || isSelectedControlled || isSelectedIdsControlled,
  }
}

export type { TreeNode as TreeNodeType, FlattenedNode as FlattenedNodeType }
