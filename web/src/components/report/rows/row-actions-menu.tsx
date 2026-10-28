import {
  ArrowDownIcon,
  ArrowUpIcon,
  EyeIcon,
  EyeOffIcon,
  PencilIcon,
  PlusIcon,
  Repeat2Icon,
  SlidersHorizontalIcon,
  Trash2Icon,
} from 'lucide-react'
import type { ComponentType, ReactNode } from 'react'
import {
  ContextMenuItem,
  ContextMenuSeparator,
  ContextMenuShortcut,
  ContextMenuSub,
  ContextMenuSubContent,
  ContextMenuSubTrigger,
} from '@/components/ui/context-menu'
import {
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuShortcut,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
} from '@/components/ui/dropdown-menu'
import { useEditor } from '@/lib/report/store'
import { locate } from '@/lib/report/tree'
import type { RowKind, RowNode } from '@/lib/report/types'
import { KIND_LABEL, KindGlyph } from '../shared/bits'
import { focusRow, type RowActions } from './use-row-actions'

interface MenuKit {
  Item: ComponentType<{ onClick?: () => void; disabled?: boolean; variant?: 'default' | 'destructive'; children: ReactNode }>
  Separator: ComponentType
  Shortcut: ComponentType<{ children: ReactNode }>
  Sub: ComponentType<{ children: ReactNode }>
  SubTrigger: ComponentType<{ children: ReactNode }>
  SubContent: ComponentType<{ children: ReactNode }>
}

export const contextKit: MenuKit = {
  Item: ContextMenuItem,
  Separator: ContextMenuSeparator,
  Shortcut: ContextMenuShortcut,
  Sub: ContextMenuSub,
  SubTrigger: ContextMenuSubTrigger,
  SubContent: ContextMenuSubContent,
}

export const dropdownKit: MenuKit = {
  Item: DropdownMenuItem,
  Separator: DropdownMenuSeparator,
  Shortcut: DropdownMenuShortcut,
  Sub: DropdownMenuSub,
  SubTrigger: DropdownMenuSubTrigger,
  SubContent: DropdownMenuSubContent,
}

const ADD_ITEMS: { kind: RowKind; label: string; shortcut?: string }[] = [
  { kind: 'line', label: 'Add line below', shortcut: 'Enter' },
  { kind: 'subtotal', label: 'Add subtotal' },
  { kind: 'heading', label: 'Add heading' },
  { kind: 'formula', label: 'Add formula row' },
]

interface RowMenuItemsProps {
  kit: MenuKit
  row: RowNode
  actions: RowActions
  onAdd: (kind: RowKind) => void
  onRename: () => void
}

export function RowMenuItems({ kit: K, row, actions, onAdd, onRename }: RowMenuItemsProps) {
  const { report, openDialog, setUi } = useEditor()
  const openProperties = () => setUi({ selectedRowKey: row.key, rowPanelTab: 'properties' })
  const loc = locate(report.rows, row.key)
  const isFirst = !loc || loc.index === 0
  const isLast = !loc || loc.index === loc.siblings.length - 1
  const after = (fn: () => void) => () => {
    fn()
    focusRow(row.key)
  }

  return (
    <>
      {ADD_ITEMS.map((item) => (
        <K.Item key={item.kind} onClick={() => onAdd(item.kind)}>
          {item.kind === 'line' ? <PlusIcon /> : <KindGlyph kind={item.kind} className="size-4" />}
          {item.label}
          {item.shortcut && <K.Shortcut>{item.shortcut}</K.Shortcut>}
        </K.Item>
      ))}
      <K.Separator />
      <K.Item onClick={onRename}>
        <PencilIcon />
        Rename
        <K.Shortcut>F2</K.Shortcut>
      </K.Item>
      <K.Sub>
        <K.SubTrigger>
          <Repeat2Icon />
          Convert to…
        </K.SubTrigger>
        <K.SubContent>
          {(Object.keys(KIND_LABEL) as RowKind[])
            .filter((k) => k !== row.kind)
            .map((k) => (
              <K.Item
                key={k}
                onClick={() => {
                  actions.setKind(row.key, k)
                  if (k === 'formula') openProperties()
                  else focusRow(row.key)
                }}
              >
                <KindGlyph kind={k} className="size-4" />
                {KIND_LABEL[k]}
              </K.Item>
            ))}
        </K.SubContent>
      </K.Sub>
      <K.Item onClick={after(() => actions.toggleHidden(row.key))}>
        {row.hidden ? <EyeIcon /> : <EyeOffIcon />}
        {row.hidden ? 'Show' : 'Hide'}
      </K.Item>
      <K.Separator />
      <K.Item disabled={isFirst} onClick={() => actions.move(row.key, -1)}>
        <ArrowUpIcon />
        Move up
        <K.Shortcut>Alt ↑</K.Shortcut>
      </K.Item>
      <K.Item disabled={isLast} onClick={() => actions.move(row.key, 1)}>
        <ArrowDownIcon />
        Move down
        <K.Shortcut>Alt ↓</K.Shortcut>
      </K.Item>
      <K.Separator />
      <K.Item onClick={openProperties}>
        <SlidersHorizontalIcon />
        Properties
      </K.Item>
      <K.Item variant="destructive" onClick={() => openDialog({ type: 'deleteRow', rowKey: row.key })}>
        <Trash2Icon />
        Delete…
        <K.Shortcut>Del</K.Shortcut>
      </K.Item>
    </>
  )
}
