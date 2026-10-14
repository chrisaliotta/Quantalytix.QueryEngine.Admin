import type { DropdownTreeItem } from '../types';

export function buildParentMap<T>(nodes: DropdownTreeItem<T>[]): Map<string, string | null> {
  const parentById = new Map<string, string | null>();

  function walk(list: DropdownTreeItem<T>[], parentId: string | null) {
    for (const node of list) {
      parentById.set(node.id, parentId);
      if (node.children?.length) walk(node.children, node.id);
    }
  }

  walk(nodes, null);
  return parentById;
}

/** Map each node id to itself plus all descendant ids (depth-first). */
export function buildSubtreeIdsMap<T>(nodes: DropdownTreeItem<T>[]): Map<string, string[]> {
  const subtreeIdsById = new Map<string, string[]>();

  function visit(node: DropdownTreeItem<T>): string[] {
    const ids = [node.id];
    for (const child of node.children ?? []) {
      ids.push(...visit(child));
    }
    subtreeIdsById.set(node.id, ids);
    return ids;
  }

  for (const node of nodes) {
    visit(node);
  }

  return subtreeIdsById;
}

export function getNodeCheckboxState(
  selectedIds: Set<string>,
  nodeId: string,
  descendantsById: Map<string, string[]>
): { checked: boolean; indeterminate: boolean } {
  const descendantIds = descendantsById.get(nodeId) ?? [];

  if (descendantIds.length === 0) {
    return { checked: selectedIds.has(nodeId), indeterminate: false };
  }

  const checked = descendantIds.every(id => selectedIds.has(id));
  const indeterminate = descendantIds.some(id => selectedIds.has(id)) && !checked;

  return { checked, indeterminate };
}
