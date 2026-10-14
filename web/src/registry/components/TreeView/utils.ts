import { Children, type ReactElement, type ReactNode, isValidElement } from 'react';
import type { TreeNode } from './hooks/useTreeMachine';
import type { TreeDSLItemProps } from './types';

export const TREE_ITEM_MARKER = '__treeview_item__';

export function getTreeItemPaddingLeft(depth: number, indentSize: number): number {
  if (indentSize === 0) return 0;

  return depth * indentSize + 8;
}

/**
 * Type guard to check if a React element is a TreeView.Item
 */
export function isTreeItemElement<T>(
  child: ReactElement<unknown>
): child is ReactElement<TreeDSLItemProps<T>> {
  return Boolean((child.type as any)?.[TREE_ITEM_MARKER]);
}

/**
 * Builds a TreeNode array from React children (DSL syntax)
 */
export function buildTreeNodesFromChildren<T>(children: ReactNode): TreeNode<T>[] {
  return Children.toArray(children)
    .map(child => {
      if (!isValidElement(child)) return null;
      if (!isTreeItemElement<T>(child)) return null;

      const { id, label, data, disabled, children: nested } = child.props as TreeDSLItemProps<T>;
      return {
        id,
        label,
        data,
        disabled,
        children: nested ? buildTreeNodesFromChildren<T>(nested) : undefined,
      } satisfies TreeNode<T>;
    })
    .filter(Boolean) as TreeNode<T>[];
}
