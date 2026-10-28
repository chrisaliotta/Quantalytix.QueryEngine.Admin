import {
  closestCenter,
  DndContext,
  PointerSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
  type DragMoveEvent,
  type DragOverEvent,
  type DragStartEvent,
} from '@dnd-kit/core'
import { arrayMove, SortableContext, verticalListSortingStrategy } from '@dnd-kit/sortable'
import {
  ChevronDownIcon,
  ChevronsDownUpIcon,
  ChevronsUpDownIcon,
  DownloadIcon,
  FileStackIcon,
  PlusIcon,
  UploadIcon,
} from 'lucide-react'
import { Fragment, useMemo, useState, type KeyboardEvent } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { KIND_LABEL, KindGlyph } from '../shared/bits'
import { ScrollArea } from '@/components/ui/scroll-area'
import { readSourcesOnRow, rowGaps } from '@/lib/report/profiles'
import { useEditor } from '@/lib/report/store'
import { ProfileSwitcher, useActiveProfile } from '../shared/profile-switcher'
import { allRows, flatten, locate, type FlatRow } from '@/lib/report/tree'
import type { RowKind } from '@/lib/report/types'
import { PendingRow, RowItem } from './row-item'
import { focusRow, useRowActions } from './use-row-actions'

const ADD_LINE_TYPES: { kind: RowKind; hint: string }[] = [
  { kind: 'line', hint: 'Shows mapped ledger data' },
  { kind: 'subtotal', hint: 'Totals the rows nested under it' },
  { kind: 'heading', hint: 'Labels a group, shows no values' },
  { kind: 'formula', hint: 'Calculated from other rows' },
]

const INDENT = 18

interface Pending {
  parentKey: string | null
  index: number
  kind: RowKind
  afterKey: string | null
}

function project(items: FlatRow[], activeId: string, overId: string, offsetX: number) {
  const activeIndex = items.findIndex((i) => i.node.key === activeId)
  const overIndex = items.findIndex((i) => i.node.key === overId)
  if (activeIndex < 0 || overIndex < 0) return null
  const moved = arrayMove(items, activeIndex, overIndex)
  const prev = moved[overIndex - 1]
  const next = moved[overIndex + 1]
  const wanted = items[activeIndex].depth + Math.round(offsetX / INDENT)
  const maxDepth = prev ? prev.depth + 1 : 0
  const minDepth = next ? next.depth : 0
  const depth = Math.max(minDepth, Math.min(maxDepth, wanted))

  let parentKey: string | null = null
  if (depth > 0 && prev) {
    if (depth > prev.depth) parentKey = prev.node.key
    else if (depth === prev.depth) parentKey = prev.parentKey
    else parentKey = moved.slice(0, overIndex).reverse().find((i) => i.depth === depth)?.parentKey ?? null
  }
  const index = moved.slice(0, overIndex).filter((i) => i.parentKey === parentKey && i.depth === depth).length
  return { depth, parentKey, index }
}

export function RowTree() {
  const { report, ui, setUi, openDialog } = useEditor()
  const { key: profileKey, dimming } = useActiveProfile()
  const actions = useRowActions()
  const [pending, setPending] = useState<Pending | null>(null)
  const [renaming, setRenaming] = useState<string | null>(null)
  const [drag, setDrag] = useState<{ activeId: string; overId: string; offsetX: number } | null>(null)

  const collapsed = useMemo(() => new Set(ui.collapsed), [ui.collapsed])
  const items = useMemo(
    () => flatten(report.rows, drag ? new Set([...collapsed, drag.activeId]) : collapsed),
    [report.rows, collapsed, drag],
  )
  const projection = drag ? project(items, drag.activeId, drag.overId, drag.offsetX) : null
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 4 } }))

  const select = (key: string) => setUi({ selectedRowKey: key })
  const toggle = (key: string) =>
    setUi((u) => ({ collapsed: u.collapsed.includes(key) ? u.collapsed.filter((k) => k !== key) : [...u.collapsed, key] }))

  const startAdd = (fromKey: string | null, kind: RowKind) => {
    setRenaming(null)
    const loc = fromKey ? locate(report.rows, fromKey) : null
    setPending(
      loc
        ? { parentKey: loc.parent?.key ?? null, index: loc.index + 1, kind, afterKey: fromKey }
        : { parentKey: null, index: report.rows.length, kind, afterKey: items.at(-1)?.node.key ?? null },
    )
  }

  const commitPending = (label: string) => {
    if (!pending) return
    const key = actions.create(pending.parentKey, pending.index, pending.kind, label)
    if (pending.kind === 'formula') setUi({ selectedRowKey: key, rowPanelTab: 'properties' })
    setPending(null)
  }

  const cancelPending = () => {
    const back = pending?.afterKey ?? ui.selectedRowKey
    setPending(null)
    if (back) focusRow(back)
  }

  const onKeyDown = (e: KeyboardEvent<HTMLDivElement>, flat: FlatRow, at: number) => {
    const key = flat.node.key
    const go = (target?: FlatRow) => {
      if (!target) return
      select(target.node.key)
      focusRow(target.node.key)
    }
    const hasChildren = flat.node.children.length > 0
    const loc = locate(report.rows, key)

    switch (e.key) {
      case 'ArrowDown':
      case 'ArrowUp': {
        e.preventDefault()
        const delta = e.key === 'ArrowDown' ? 1 : -1
        if (e.altKey) actions.move(key, delta)
        else go(items[at + delta])
        break
      }
      case 'Home':
        e.preventDefault()
        go(items[0])
        break
      case 'End':
        e.preventDefault()
        go(items.at(-1))
        break
      case 'ArrowRight':
        e.preventDefault()
        if (hasChildren && collapsed.has(key)) toggle(key)
        else if (hasChildren) go(items[at + 1])
        break
      case 'ArrowLeft':
        e.preventDefault()
        if (hasChildren && !collapsed.has(key)) toggle(key)
        else go(items.find((i) => i.node.key === flat.parentKey))
        break
      case 'Tab':
        if (e.shiftKey ? !loc?.parent : !loc || loc.index === 0) return
        e.preventDefault()
        if (e.shiftKey) actions.outdent(key)
        else actions.indent(key)
        break
      case 'Enter':
        e.preventDefault()
        startAdd(key, 'line')
        break
      case 'F2':
        e.preventDefault()
        setPending(null)
        setRenaming(key)
        break
      case 'Delete':
      case 'Backspace':
        e.preventDefault()
        openDialog({ type: 'deleteRow', rowKey: key })
        break
    }
  }

  const onDragStart = ({ active }: DragStartEvent) => {
    setPending(null)
    setRenaming(null)
    select(String(active.id))
    setDrag({ activeId: String(active.id), overId: String(active.id), offsetX: 0 })
  }
  const onDragMove = ({ delta }: DragMoveEvent) => setDrag((d) => (d ? { ...d, offsetX: delta.x } : d))
  const onDragOver = ({ over }: DragOverEvent) => setDrag((d) => (d && over ? { ...d, overId: String(over.id) } : d))
  const onDragEnd = ({ active, over }: DragEndEvent) => {
    const result = drag && over ? project(items, String(active.id), String(over.id), drag.offsetX) : null
    setDrag(null)
    if (!result) return
    const loc = locate(report.rows, String(active.id))
    const sameSpot = loc && (loc.parent?.key ?? null) === result.parentKey && loc.index === result.index
    if (!sameSpot) actions.moveTo(String(active.id), result.parentKey, result.index)
    focusRow(String(active.id))
  }

  const pendingAt = useMemo(() => {
    if (!pending) return -1
    if (!pending.afterKey) return -1
    const start = items.findIndex((i) => i.node.key === pending.afterKey)
    if (start < 0) return items.length - 1
    let end = start
    while (end + 1 < items.length && items[end + 1].depth > items[start].depth) end++
    return end
  }, [pending, items])

  const pendingDepth = pending ? (pending.parentKey ? (items.find((i) => i.node.key === pending.parentKey)?.depth ?? -1) + 1 : 0) : 0

  const renderPending = () =>
    pending && (
      <PendingRow depth={pendingDepth} kind={pending.kind} onCommit={commitPending} onCancel={cancelPending} />
    )

  return (
    <div className="flex h-full min-h-0 flex-col">
      <div className="flex h-11 shrink-0 items-center justify-between gap-2 border-b px-3">
        <div className="flex min-w-0 items-center gap-3">
          <h2 className="font-medium">Rows</h2>
          <ProfileSwitcher />
        </div>
        <div className="flex shrink-0 items-center gap-0.5">
          <Button
            variant="ghost"
            size="icon-sm"
            aria-label="Expand all"
            onClick={() => setUi({ collapsed: [] })}
          >
            <ChevronsUpDownIcon />
          </Button>
          <Button
            variant="ghost"
            size="icon-sm"
            aria-label="Collapse all"
            onClick={() => setUi({ collapsed: allRows(report.rows).filter((r) => r.children.length).map((r) => r.key) })}
          >
            <ChevronsDownUpIcon />
          </Button>
          <DropdownMenu>
            <DropdownMenuTrigger render={<Button variant="ghost" size="sm" />}>
              <FileStackIcon />
              Import/Export
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-64">
              <DropdownMenuGroup>
                <DropdownMenuLabel>Hierarchy &amp; mappings</DropdownMenuLabel>
                <DropdownMenuItem onClick={() => toast('Import is coming soon')} className="items-start">
                  <UploadIcon className="mt-0.5 size-4" />
                  <span className="flex flex-col">
                    <span>Import…</span>
                    <span className="text-xs text-muted-foreground">Replace rows and mappings from a file</span>
                  </span>
                </DropdownMenuItem>
                <DropdownMenuItem onClick={() => toast('Export is coming soon')} className="items-start">
                  <DownloadIcon className="mt-0.5 size-4" />
                  <span className="flex flex-col">
                    <span>Export…</span>
                    <span className="text-xs text-muted-foreground">Download rows and mappings as a file</span>
                  </span>
                </DropdownMenuItem>
              </DropdownMenuGroup>
            </DropdownMenuContent>
          </DropdownMenu>
          <DropdownMenu>
            <DropdownMenuTrigger render={<Button variant="outline" size="sm" />}>
              <PlusIcon data-icon="inline-start" />
              Add line
              <ChevronDownIcon data-icon="inline-end" />
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-60">
              <DropdownMenuGroup>
                {ADD_LINE_TYPES.map((t) => (
                  <DropdownMenuItem key={t.kind} onClick={() => startAdd(ui.selectedRowKey, t.kind)} className="items-start">
                    <KindGlyph kind={t.kind} className="mt-0.5 size-4" />
                    <span className="flex flex-col">
                      <span>{KIND_LABEL[t.kind]}</span>
                      <span className="text-xs text-muted-foreground">{t.hint}</span>
                    </span>
                  </DropdownMenuItem>
                ))}
              </DropdownMenuGroup>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </div>

      {items.length === 0 && !pending ? (
        <EmptyRows onAdd={() => startAdd(null, 'line')} />
      ) : (
        <ScrollArea className="min-h-0 flex-1">
          <DndContext
            id="row-tree-dnd"
            sensors={sensors}
            collisionDetection={closestCenter}
            onDragStart={onDragStart}
            onDragMove={onDragMove}
            onDragOver={onDragOver}
            onDragEnd={onDragEnd}
            onDragCancel={() => setDrag(null)}
          >
            <SortableContext items={items.map((i) => i.node.key)} strategy={verticalListSortingStrategy}>
              <div role="tree" aria-label="Report rows" className="flex flex-col p-2">
                {pendingAt === -1 && renderPending()}
                {items.map((flat, at) => {
                  const siblings = locate(report.rows, flat.node.key)?.siblings ?? []
                  const isLine = flat.node.kind === 'line'
                  const split = isLine ? readSourcesOnRow(report, flat.node.key, profileKey) : null
                  return (
                    <Fragment key={flat.node.key}>
                      <RowItem
                        row={flat.node}
                        depth={drag?.activeId === flat.node.key && projection ? projection.depth : flat.depth}
                        level={flat.depth + 1}
                        posInSet={siblings.indexOf(flat.node) + 1}
                        setSize={siblings.length}
                        selected={ui.selectedRowKey === flat.node.key}
                        collapsed={collapsed.has(flat.node.key)}
                        advanced={ui.advanced}
                        sourceCount={
                          split ? (dimming ? split.read.length : split.read.length + split.unread.length) : 0
                        }
                        unreadCount={split && dimming ? split.unread.length : 0}
                        gapCount={isLine ? rowGaps(report, flat.node.key, profileKey).length : 0}
                        profileKey={profileKey}
                        renaming={renaming === flat.node.key}
                        actions={actions}
                        onSelect={() => select(flat.node.key)}
                        onToggle={() => toggle(flat.node.key)}
                        onKeyDown={(e) => onKeyDown(e, flat, at)}
                        onAdd={(kind) => startAdd(flat.node.key, kind)}
                        onStartRename={() => {
                          setPending(null)
                          setRenaming(flat.node.key)
                        }}
                        onEndRename={() => {
                          setRenaming(null)
                          focusRow(flat.node.key)
                        }}
                      />
                      {pendingAt === at && renderPending()}
                    </Fragment>
                  )
                })}
              </div>
            </SortableContext>
          </DndContext>
        </ScrollArea>
      )}

      <p className="shrink-0 border-t px-3 py-2 text-xs text-muted-foreground">
        <kbd className="font-mono">Enter</kbd> adds a line · <kbd className="font-mono">F2</kbd> renames ·{' '}
        <kbd className="font-mono">Tab</kbd> nests · <kbd className="font-mono">Alt ↑↓</kbd> moves
      </p>
    </div>
  )
}

export function EmptyRows({ onAdd }: { onAdd: () => void }) {
  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-3 p-6 text-center">
      <div className="flex flex-col gap-1">
        <p className="font-medium">No rows yet</p>
        <p className="max-w-60 text-pretty text-muted-foreground">
          Rows are the lines of the statement. Add a line, then map data onto it.
        </p>
      </div>
      <Button onClick={onAdd}>
        <PlusIcon data-icon="inline-start" />
        Add your first line
      </Button>
    </div>
  )
}
