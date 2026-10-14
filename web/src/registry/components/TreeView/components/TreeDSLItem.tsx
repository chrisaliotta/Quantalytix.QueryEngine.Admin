import type { TreeDSLItemProps } from '../types'
import { TREE_ITEM_MARKER } from '../utils'

export function TreeDSLItem<T = unknown>({ children }: TreeDSLItemProps<T>) {
  return <>{children}</>
}

;(TreeDSLItem as any)[TREE_ITEM_MARKER] = true
TreeDSLItem.displayName = 'TreeView.Item'

