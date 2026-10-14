import type { DropdownTreeItem } from "../types"

/**
 * Collect the IDs of all nodes that have children (i.e. every node that can be
 * expanded). Returned as a Set so it can be passed directly to TreeView's
 * `defaultOpenIds` prop to pre-expand the whole tree on mount.
 */
export function collectAllParentIds<T>(nodes: DropdownTreeItem<T>[]): Set<string> {
  const ids = new Set<string>()
  function walk(list: DropdownTreeItem<T>[]) {
    for (const node of list) {
      if (node.children?.length) {
        ids.add(node.id)
        walk(node.children)
      }
    }
  }
  walk(nodes)
  return ids
}
