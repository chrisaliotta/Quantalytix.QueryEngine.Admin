import type { DropdownTreeItem } from '../types';

/** Remove disabled node ids from a selection set. */
export function removeDisabledIdsFromSelection<T>(
  ids: Set<string>,
  itemMap: Map<string, DropdownTreeItem<T>>
): Set<string> {
  const result = new Set<string>();
  for (const id of ids) {
    const item = itemMap.get(id);
    if (item?.disabled) continue;
    result.add(id);
  }
  return result;
}

/**
 * Apply cascade selection: when an id is added, add it and all its descendants;
 * when an id is removed, remove it and all its descendants.
 * When parentById is provided, bubble parent ids up when all descendants are selected
 */
export function applyCascadeSelection(
  currentIds: Set<string>,
  nextIds: Set<string>,
  descendantsById: Map<string, string[]>,
  parentById?: Map<string, string | null>
): Set<string> {
  const added = new Set<string>();
  const removed = new Set<string>();
  for (const id of nextIds) {
    if (!currentIds.has(id)) added.add(id);
  }
  for (const id of currentIds) {
    if (!nextIds.has(id)) removed.add(id);
  }
  const result = new Set(nextIds);
  for (const id of added) {
    const descendants = descendantsById.get(id);
    if (descendants?.length) {
      descendants.forEach(d => result.add(d));
    }
  }
  for (const id of removed) {
    const descendants = descendantsById.get(id);
    if (descendants?.length) {
      descendants.forEach(d => result.delete(d));
    }
  }

  if (parentById) {
    const changedIds = new Set([...added, ...removed]);
    for (const changedId of changedIds) {
      let currentParentId = parentById.get(changedId) ?? null;
      while (currentParentId) {
        const descendantIds = descendantsById.get(currentParentId) ?? [];
        const isFullySelected =
          descendantIds.length > 0 && descendantIds.every(id => result.has(id));

        if (isFullySelected) {
          result.add(currentParentId);
        } else {
          result.delete(currentParentId);
        }

        currentParentId = parentById.get(currentParentId) ?? null;
      }
    }
  }

  return result;
}

/** Keep only leaf ids in a selection set (omit intermediate folder nodes). */
export function pruneSelectionToLeaves(
  ids: Set<string>,
  descendantsById: Map<string, string[]>
): Set<string> {
  const pruned = new Set<string>();
  for (const id of ids) {
    if ((descendantsById.get(id) ?? []).length === 0) {
      pruned.add(id);
    }
  }
  return pruned;
}

/**
 * Expand parent ids in a selection set to include all leaf descendants.
 * Used for internal UI state when the controlled value contains parent ids only.
 */
export function expandSelectionIds(
  ids: Set<string>,
  descendantsById: Map<string, string[]>
): Set<string> {
  const expanded = new Set(ids);
  for (const id of ids) {
    const descendants = descendantsById.get(id);
    if (descendants?.length) {
      descendants.forEach(d => expanded.add(d));
    }
  }
  return expanded;
}

/**
 * Compress a selection set to the highest-level fully-selected parent ids.
 * Matches rsuite CheckTree cascade behavior: when all leaf descendants are
 * selected, emit only the parent id and omit the descendant ids.
 */
export function compressSelectionToParents<T>(
  nodes: DropdownTreeItem<T>[],
  ids: Set<string>,
  descendantsById: Map<string, string[]>
): Set<string> {
  const result = new Set<string>();

  function walk(list: DropdownTreeItem<T>[]) {
    for (const node of list) {
      const descendants = descendantsById.get(node.id) ?? [];

      if (descendants.length > 0) {
        const parentInSet = ids.has(node.id);
        const allLeavesSelected = descendants.every(id => ids.has(id));

        if (parentInSet || allLeavesSelected) {
          result.add(node.id);
          continue;
        }

        if (node.children?.length) walk(node.children);
        continue;
      }

      if (ids.has(node.id)) {
        result.add(node.id);
      }
    }
  }

  walk(nodes);
  return result;
}
