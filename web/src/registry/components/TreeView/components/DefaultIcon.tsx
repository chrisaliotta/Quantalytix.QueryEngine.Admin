import { File, Folder, FolderOpen } from 'lucide-react'
import type { TreeViewRenderProps } from '../types'

export function DefaultIcon<T>({ hasChildren, isExpanded }: TreeViewRenderProps<T>) {
  if (hasChildren) {
    return isExpanded ? (
      <FolderOpen className="w-4 h-4 shrink-0 text-amber-500" aria-hidden="true" />
    ) : (
      <Folder className="w-4 h-4 shrink-0 text-amber-500" aria-hidden="true" />
    )
  }
  return <File className="w-4 h-4 shrink-0 text-muted-foreground" aria-hidden="true" />
}

