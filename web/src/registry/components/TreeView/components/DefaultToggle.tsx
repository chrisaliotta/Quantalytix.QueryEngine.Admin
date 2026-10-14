import { ChevronRight } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { TreeViewRenderProps } from '../types'

export function DefaultToggle<T>({ hasChildren, isExpanded }: TreeViewRenderProps<T>) {
  if (!hasChildren) {
    return <span className="w-4 h-4 shrink-0" aria-hidden="true" />
  }

  return (
    <ChevronRight
      className={cn(
        'w-4 h-4 shrink-0 text-muted-foreground transition-transform duration-150',
        isExpanded && 'rotate-90'
      )}
      aria-hidden="true"
    />
  )
}

