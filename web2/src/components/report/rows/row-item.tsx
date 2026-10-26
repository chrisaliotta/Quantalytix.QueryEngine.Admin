import { useSortable } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { ChevronRightIcon, EyeOffIcon, GripVerticalIcon, MoreHorizontalIcon } from 'lucide-react'
import { useEffect, useRef, useState, type KeyboardEvent } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { ContextMenu, ContextMenuContent, ContextMenuTrigger } from '@/components/ui/context-menu'
import { DropdownMenu, DropdownMenuContent, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { dayCountLabel } from '@/lib/model/rules'
import type { RowKind, RowNode } from '@/lib/report/types'
import { cn } from '@/lib/utils'
import { KIND_LABEL, KeyText, KindGlyph } from '../shared/bits'
import { contextKit, dropdownKit, RowMenuItems } from './row-actions-menu'
import type { RowActions } from './use-row-actions'

const INDENT = 18

interface RowItemProps {
  row: RowNode
  depth: number
  level: number
  posInSet: number
  setSize: number
  selected: boolean
  collapsed: boolean
  advanced: boolean
  /** Sources on the row the active profile reads. */
  sourceCount: number
  /** Sources on the row the active profile doesn't read. */
  unreadCount?: number
  gapCount?: number
  profileKey?: string
  renaming: boolean
  actions: RowActions
  onSelect: () => void
  onToggle: () => void
  onKeyDown: (e: KeyboardEvent<HTMLDivElement>) => void
  onAdd: (kind: RowKind) => void
  onStartRename: () => void
  onEndRename: () => void
}

export function RowItem(props: RowItemProps) {
  const { row, depth, selected, collapsed, advanced, sourceCount, renaming, actions } = props
  const { attributes, listeners, setNodeRef, setActivatorNodeRef, transform, transition, isDragging } = useSortable({
    id: row.key,
    disabled: renaming,
  })
  const hasChildren = row.children.length > 0
  const { role: _role, tabIndex: _tabIndex, ...dragAttributes } = attributes

  return (
    <div
      ref={setNodeRef}
      style={{ transform: CSS.Translate.toString(transform), transition }}
      className={cn('relative', isDragging && 'z-10 opacity-80')}
    >
      <ContextMenu>
        <ContextMenuTrigger
          render={
            <div
              role="treeitem"
              aria-level={props.level}
              aria-posinset={props.posInSet}
              aria-setsize={props.setSize}
              aria-selected={selected}
              aria-expanded={hasChildren ? !collapsed : undefined}
              data-row-key={row.key}
              data-selected={selected}
              tabIndex={selected ? 0 : -1}
              onClick={props.onSelect}
              onContextMenu={props.onSelect}
              onKeyDown={renaming ? undefined : props.onKeyDown}
              className={cn(
                'group flex h-8 cursor-default items-center gap-1.5 rounded-md pr-1 outline-none select-none',
                'hover:bg-muted/70 focus-visible:ring-2 focus-visible:ring-ring/60',
                selected && 'bg-accent text-accent-foreground hover:bg-accent',
                isDragging && 'bg-card shadow-md ring-1 ring-border',
              )}
              style={{ paddingLeft: depth * INDENT + 2 }}
            />
          }
        >
          <button
            type="button"
            ref={setActivatorNodeRef}
            {...dragAttributes}
            {...listeners}
            tabIndex={-1}
            aria-label={`Drag ${row.label}`}
            className="flex h-6 w-4 shrink-0 cursor-grab items-center justify-center rounded text-muted-foreground opacity-0 group-hover:opacity-100 group-data-[selected=true]:opacity-60 active:cursor-grabbing"
          >
            <GripVerticalIcon className="size-3.5" />
          </button>
          {hasChildren ? (
            <button
              type="button"
              tabIndex={-1}
              aria-label={collapsed ? `Expand ${row.label}` : `Collapse ${row.label}`}
              onClick={(e) => {
                e.stopPropagation()
                props.onToggle()
              }}
              className="flex size-5 shrink-0 items-center justify-center rounded text-muted-foreground hover:bg-foreground/5"
            >
              <ChevronRightIcon className={cn('size-3.5 transition-transform', !collapsed && 'rotate-90')} />
            </button>
          ) : (
            <span aria-hidden className="w-5 shrink-0" />
          )}
          {row.kind !== 'line' && <KindGlyph kind={row.kind} />}

          {renaming ? (
            <RowLabelInput
              initial={row.label}
              ariaLabel={`Rename ${row.label}`}
              onCommit={(label) => {
                actions.rename(row.key, label)
                props.onEndRename()
              }}
              onCancel={props.onEndRename}
            />
          ) : (
            <span
              className={cn('min-w-0 truncate', row.kind === 'line' && !sourceCount && 'text-muted-foreground', row.kind === 'subtotal' && 'font-medium', row.kind === 'heading' && 'font-medium text-muted-foreground', row.hidden && 'text-muted-foreground line-through decoration-muted-foreground/40')}
              onDoubleClick={props.onStartRename}
            >
              {row.label}
            </span>
          )}
          {row.hidden && !renaming && (
            <EyeOffIcon className="size-3.5 shrink-0 text-muted-foreground" aria-label="Hidden" role="img" />
          )}
          {advanced && !renaming && <KeyText className="shrink-0">{row.key}</KeyText>}

          <span className="ml-auto flex shrink-0 items-center gap-1 pl-2">
            {row.styleKey && (
              <Badge variant="outline" className="font-normal text-muted-foreground" title={`Style override: ${row.styleKey}`}>
                Styled
              </Badge>
            )}
            {row.parameters && Object.keys(row.parameters).length > 0 && (
              <span className="flex items-center gap-1">
                {Object.entries(row.parameters).map(([k, v]) => (
                  <Badge key={k} variant="outline" className="font-normal text-muted-foreground" title={`@${k} = ${v}`}>
                    {dayCountLabel(String(v))}
                  </Badge>
                ))}
              </span>
            )}
            {row.kind === 'formula' && (
              <KeyText className={cn(!row.measureKey && 'text-warning-foreground')}>
                {row.measureKey && row.columnKey ? `${row.measureKey} → ${row.columnKey}` : 'not set up'}
              </KeyText>
            )}
            {row.kind === 'line' &&
              (sourceCount ? (
                <Badge variant="outline" className="font-normal text-muted-foreground">
                  {sourceCount} {sourceCount === 1 ? 'source' : 'sources'}
                </Badge>
              ) : (
                <Badge
                  variant="outline"
                  className="font-normal text-muted-foreground opacity-70"
                  title={props.unreadCount ? `Has sources, but none are read by ${props.profileKey}` : 'No source is mapped to this row'}
                >
                  Unbound
                </Badge>
              ))}
            {props.gapCount ? (
              <span
                role="img"
                aria-label={`${props.gapCount} profile ${props.gapCount === 1 ? 'gap' : 'gaps'} under ${props.profileKey}`}
                title={`${props.gapCount} ${props.gapCount === 1 ? 'variable has' : 'variables have'} a gap under ${props.profileKey}`}
                className="size-2 shrink-0 rounded-full bg-warning"
              />
            ) : null}
            <DropdownMenu>
              <DropdownMenuTrigger
                render={
                  <Button
                    variant="ghost"
                    size="icon-xs"
                    tabIndex={-1}
                    aria-label={`Actions for ${row.label}`}
                    onClick={(e) => {
                      e.stopPropagation()
                      props.onSelect()
                    }}
                    className="opacity-0 group-hover:opacity-100 group-data-[selected=true]:opacity-100 data-popup-open:opacity-100"
                  />
                }
              >
                <MoreHorizontalIcon />
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-52" finalFocus={false}>
                <RowMenuItems kit={dropdownKit} row={row} actions={actions} onAdd={props.onAdd} onRename={props.onStartRename} />
              </DropdownMenuContent>
            </DropdownMenu>
          </span>
        </ContextMenuTrigger>
        <ContextMenuContent className="w-52" finalFocus={false}>
          <RowMenuItems kit={contextKit} row={row} actions={actions} onAdd={props.onAdd} onRename={props.onStartRename} />
        </ContextMenuContent>
      </ContextMenu>
    </div>
  )
}

interface RowLabelInputProps {
  initial?: string
  ariaLabel: string
  keyPreview?: (label: string) => string
  onCommit: (label: string) => void
  onCancel: () => void
}

export function RowLabelInput({ initial = '', ariaLabel, keyPreview, onCommit, onCancel }: RowLabelInputProps) {
  const [draft, setDraft] = useState(initial)
  const ref = useRef<HTMLInputElement>(null)
  const done = useRef(false)

  useEffect(() => {
    const el = ref.current
    const t = setTimeout(() => {
      el?.focus()
      el?.select()
    }, 0)
    return () => clearTimeout(t)
  }, [])

  const finish = (commit: boolean) => {
    if (done.current) return
    done.current = true
    if (commit && draft.trim()) onCommit(draft)
    else onCancel()
  }

  return (
    <span className="flex min-w-0 flex-1 items-center gap-2">
      <input
        ref={ref}
        value={draft}
        aria-label={ariaLabel}
        placeholder="Row label"
        onChange={(e) => setDraft(e.target.value)}
        onClick={(e) => e.stopPropagation()}
        onKeyDown={(e) => {
          e.stopPropagation()
          if (e.nativeEvent.isComposing || e.keyCode === 229) return
          if (e.key === 'Enter') {
            e.preventDefault()
            finish(true)
          } else if (e.key === 'Escape') {
            e.preventDefault()
            finish(false)
          }
        }}
        onBlur={() => finish(true)}
        className="h-6 min-w-0 flex-1 rounded border border-ring bg-background px-1.5 text-sm outline-none ring-2 ring-ring/30"
      />
      {keyPreview && <KeyText className="shrink-0">{keyPreview(draft)}</KeyText>}
    </span>
  )
}

export function PendingRow({
  depth,
  kind,
  onCommit,
  onCancel,
}: {
  depth: number
  kind: RowKind
  onCommit: (label: string) => void
  onCancel: () => void
}) {
  return (
    <div className="flex h-8 items-center gap-1.5 rounded-md bg-accent/60 pr-2" style={{ paddingLeft: depth * INDENT + 2 }}>
      <span aria-hidden className="w-4 shrink-0" />
      <span aria-hidden className="w-5 shrink-0" />
      {kind !== 'line' && <KindGlyph kind={kind} />}
      <RowLabelInput ariaLabel={`New ${KIND_LABEL[kind].toLowerCase()} label`} onCommit={onCommit} onCancel={onCancel} />
    </div>
  )
}
