import { ArrowDownIcon, ArrowUpIcon, ChevronRightIcon, ChevronsDownUpIcon, ChevronsUpDownIcon, FolderIcon, GripVerticalIcon, PlusIcon, SearchIcon, Trash2Icon, TriangleAlertIcon, UploadIcon } from 'lucide-react'
import { Fragment, useEffect, useMemo, useRef, useState, type DragEvent, type KeyboardEvent } from 'react'
import { toast } from 'sonner'
import { useUndoAction } from '@/components/shell/history'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Segmented } from '@/components/report/shared/segmented'
import { useDimensionsApi, useRefreshDimensions } from '@/lib/dimensions/client'
import type { ImportMode, ImportRow, Member, MemberInput, MovePosition } from '@/lib/dimensions/contract'
import { descendantIds, indexTree, type TreeIndex } from '@/lib/dimensions/tree'
import { cn } from '@/lib/utils'
import type { SectionProps } from './dimension-detail'
import { AddRow, InlineNumber, InlineSelect, InlineSwitch, InlineText, SplitView, focusRowClass, keyFromName, type AddRowField, type AddRowResult } from '@/components/shell/inline-table'
import { MemberPanel, usePatchMember } from './member-panel'
import { DeleteDialog, EmptyState, Mono, RequiredLock, TabIntro, Td, Th, useDimCall } from './shared'

const NONE = '__none__'
type View = 'tree' | 'table'
type DropZone = 'before' | 'inside' | 'after'
interface Row {
  member: Member
  depth: number
  hasChildren: boolean
}

export function MembersSection({ dim, content, editable, policy, focusId, clearFocus }: SectionProps) {
  const api = useDimensionsApi()
  const undoAction = useUndoAction()
  const refresh = useRefreshDimensions()
  const { run } = useDimCall()
  const hierarchical = dim.kind === 'hierarchical'
  const index = useMemo(() => indexTree(content.members), [content.members])
  const required = useMemo(() => new Set(dim.profile?.requiredMemberKeys ?? []), [dim.profile])
  const imageKey = dim.profile?.memberPropertySchema.find((f) => f.type === 'image')?.key ?? null

  const [view, setView] = useState<View>('tree')
  const [query, setQuery] = useState('')
  const [collapsed, setCollapsed] = useState<Set<string>>(() => new Set(content.members.filter((m) => (index.depth.get(m.id) ?? 0) >= 1).map((m) => m.id)))
  const [focusedId, setFocusedId] = useState<string | null>(null)
  const [addingUnder, setAddingUnder] = useState<string | null>(null)
  const [deleting, setDeleting] = useState<Member | null>(null)
  const [importing, setImporting] = useState(false)
  const [clearing, setClearing] = useState(false)
  const [drag, setDrag] = useState<{ id: string; overId: string | null; zone: DropZone | null } | null>(null)
  const patch = usePatchMember(content.version.id)
  const focused = focusedId ? (index.byId.get(focusedId) ?? null) : null
  const rootAddRef = useRef<HTMLDivElement>(null)
  const focusRootAdd = () => rootAddRef.current?.querySelector('input')?.focus()

  useEffect(() => {
    if (!focusId) return
    if (index.byId.has(focusId)) setFocusedId(focusId)
    clearFocus()
  }, [focusId, index, clearFocus])

  async function addMember(parentId: string | null, v: Record<string, string>) {
    const siblings = index.children.get(parentId) ?? []
    const r = await api.saveMember(content.version.id, {
      key: v.key,
      displayName: v.displayName,
      description: null,
      parentId,
      levelId: siblings[0]?.levelId ?? null,
      isAssignable: true,
      sortOrder: siblings.length + 1,
      properties: {},
    })
    if (!r.ok) return Object.keys(r.problem.errors).length ? r.problem.errors : { '': [r.problem.detail ?? r.problem.title] }
    await refresh()
    return null
  }
  const addFields: AddRowField[] = [
    { name: 'displayName', placeholder: 'Add member — name', required: true, className: 'flex-1' },
    { name: 'key', placeholder: 'KEY', mono: true, required: true, className: 'w-40 flex-none', derive: (v) => keyFromName(v.displayName) },
  ]
  const startChild = (parentId: string) => {
    setAddingUnder(parentId)
    setCollapsed((c) => {
      const n = new Set(c)
      n.delete(parentId)
      return n
    })
  }

  const q = query.trim().toLowerCase()
  const visibleIds = useMemo(() => {
    if (!q) return null
    const keep = new Set<string>()
    for (const m of content.members) {
      if (!m.key.toLowerCase().includes(q) && !m.displayName.toLowerCase().includes(q)) continue
      keep.add(m.id)
      let p = m.parentId
      const seen = new Set<string>()
      while (p && !seen.has(p)) {
        seen.add(p)
        keep.add(p)
        p = index.byId.get(p)?.parentId ?? null
      }
    }
    return keep
  }, [q, content.members, index])

  const rows = useMemo(() => flatten(index, hierarchical ? collapsed : new Set(), visibleIds), [index, collapsed, visibleIds, hierarchical])
  const canDrag = editable && !q && view === 'tree'
  const canDelete = (m: Member) => editable && !(policy === 'extend' && required.has(m.key))

  async function move(member: Member, to: MovePosition) {
    const r = await api.moveMember(content.version.id, member.id, to)
    if (!r.ok) return toast.error(r.problem.title, { description: r.problem.detail })
    await refresh()
    toast.success(`Moved ${member.displayName}`, { action: undoAction() })
  }

  function drop(target: Member, zone: DropZone) {
    if (!drag) return
    const dragged = index.byId.get(drag.id)
    setDrag(null)
    if (!dragged || dragged.id === target.id) return
    if (zone === 'inside') {
      if (descendantIds(index, dragged.id).has(target.id)) return toast.error('A member cannot move under itself or its descendants')
      const kids = (index.children.get(target.id) ?? []).filter((c) => c.id !== dragged.id)
      setCollapsed((c) => {
        const n = new Set(c)
        n.delete(target.id)
        return n
      })
      return move(dragged, { parentId: target.id, index: kids.length })
    }
    const parentId = hierarchical ? target.parentId : null
    if (parentId && (parentId === dragged.id || descendantIds(index, dragged.id).has(parentId))) return toast.error('A member cannot move under itself or its descendants')
    const siblings = (index.children.get(parentId && index.byId.has(parentId) ? parentId : null) ?? []).filter((c) => c.id !== dragged.id)
    const at = siblings.findIndex((s) => s.id === target.id)
    move(dragged, { parentId, index: zone === 'before' ? at : at + 1 })
  }

  const rowRefs = useRef(new Map<string, HTMLDivElement>())
  function onRowKey(e: KeyboardEvent, row: Row, i: number) {
    const focusAt = (j: number) => rowRefs.current.get(rows[Math.max(0, Math.min(rows.length - 1, j))]?.member.id)?.focus()
    const m = row.member
    if (e.target !== e.currentTarget) return
    if (e.key === 'Enter') return setFocusedId(m.id)
    if (e.key === 'Escape' && focusedId) return setFocusedId(null)
    if (e.altKey && editable && !q && (e.key === 'ArrowUp' || e.key === 'ArrowDown')) {
      e.preventDefault()
      const sibs = index.children.get(hierarchical && m.parentId && index.byId.has(m.parentId) ? m.parentId : null) ?? []
      const at = sibs.findIndex((s) => s.id === m.id)
      const next = e.key === 'ArrowUp' ? at - 1 : at + 1
      if (next >= 0 && next < sibs.length) move(m, { parentId: hierarchical ? m.parentId : null, index: next })
      return
    }
    if (e.key === 'ArrowDown') return (e.preventDefault(), focusAt(i + 1))
    if (e.key === 'ArrowUp') return (e.preventDefault(), focusAt(i - 1))
    if (!hierarchical) return
    if (e.key === 'ArrowRight' && row.hasChildren && collapsed.has(m.id)) return toggle(m.id)
    if (e.key === 'ArrowLeft') {
      if (row.hasChildren && !collapsed.has(m.id)) return toggle(m.id)
      if (m.parentId) rowRefs.current.get(m.parentId)?.focus()
    }
  }
  const toggle = (id: string) =>
    setCollapsed((c) => {
      const n = new Set(c)
      if (n.has(id)) n.delete(id)
      else n.add(id)
      return n
    })

  return (
    <div className="flex flex-col gap-4">
      {dim.kind === 'time' && <TabIntro>Time dimensions derive periods from dates; members are needed only for custom calendars.</TabIntro>}

      <div className="flex flex-wrap items-center gap-2">
        <div className="relative w-full max-w-xs">
          <SearchIcon aria-hidden className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Search members" aria-label="Search members" className="pl-8" />
        </div>
        <span className="text-xs text-muted-foreground tabular-nums">
          {visibleIds ? `${[...visibleIds].filter((id) => index.byId.get(id)?.key.toLowerCase().includes(q) || index.byId.get(id)?.displayName.toLowerCase().includes(q)).length} of ` : ''}
          {content.members.length} member{content.members.length === 1 ? '' : 's'}
        </span>
        {hierarchical && view === 'tree' && (
          <>
            <Button variant="ghost" size="sm" onClick={() => setCollapsed(new Set())}>
              <ChevronsUpDownIcon /> Expand all
            </Button>
            <Button variant="ghost" size="sm" onClick={() => setCollapsed(new Set(content.members.filter((m) => index.children.get(m.id)?.length).map((m) => m.id)))}>
              <ChevronsDownUpIcon /> Collapse all
            </Button>
          </>
        )}
        <div className="ml-auto flex items-center gap-2">
          <Segmented<View>
            aria-label="View"
            value={view}
            onChange={setView}
            options={[
              { value: 'tree', label: hierarchical ? 'Tree' : 'List' },
              { value: 'table', label: 'Table' },
            ]}
          />
          {editable && (
            <>
              {content.members.length > 0 && (
                <Button variant="ghost" size="sm" className="text-muted-foreground hover:text-destructive" onClick={() => setClearing(true)}>
                  <Trash2Icon /> Clear all
                </Button>
              )}
              <Button variant="outline" size="sm" onClick={() => setImporting(true)}>
                <UploadIcon /> Import…
              </Button>
              <Button size="sm" onClick={() => (setView('tree'), setQuery(''), requestAnimationFrame(focusRootAdd))}>
                <PlusIcon /> New member
              </Button>
            </>
          )}
        </div>
      </div>
      {editable && q && view === 'tree' && <p className="text-xs text-muted-foreground">Drag and drop is off while searching.</p>}

      <SplitView panel={focused && <MemberPanel key={focused.id} dim={dim} content={content} editable={editable} member={focused} onClose={() => setFocusedId(null)} />}>
      {!content.members.length && !editable ? (
        <EmptyState title="No members in this version">{dim.kind === 'time' ? 'Leave this empty unless you need a custom calendar.' : 'This version has no members.'}</EmptyState>
      ) : view === 'table' ? (
        <MemberTable
          dim={dim}
          content={content}
          index={index}
          editable={editable}
          members={visibleIds ? content.members.filter((m) => visibleIds.has(m.id)) : content.members}
          focusedId={focusedId}
          onFocus={setFocusedId}
          onPatch={patch}
          onAdd={(v) => addMember(null, v)}
          onDelete={canDelete}
          setDeleting={setDeleting}
        />
      ) : (
        <div role="tree" aria-label={`${dim.displayName} members`} className="flex flex-col rounded-lg border bg-card py-1" onDragEnd={() => setDrag(null)}>
          {!content.members.length && (
            <p className="px-3 py-4 text-sm text-pretty text-muted-foreground">
              {dim.kind === 'time' ? 'Leave this empty unless you need a custom calendar.' : 'No members yet. Type a name below and press Enter, or paste a list with Import.'}
            </p>
          )}
          {rows.map((row, i) => {
            const m = row.member
            const isRequired = required.has(m.key)
            const level = content.levels.find((l) => l.id === m.levelId)
            const over = drag?.overId === m.id ? drag.zone : null
            const thumb = imageKey ? (m.properties[imageKey] as string | undefined) : undefined
            const addingChild = addingUnder === m.id
            return (
              <Fragment key={m.id}>
              <div
                ref={(el) => {
                  if (el) rowRefs.current.set(m.id, el)
                  else rowRefs.current.delete(m.id)
                }}
                role="treeitem"
                aria-level={row.depth + 1}
                aria-expanded={row.hasChildren ? !collapsed.has(m.id) : undefined}
                tabIndex={0}
                aria-selected={focusedId === m.id}
                onKeyDown={(e) => onRowKey(e, row, i)}
                onClick={() => setFocusedId(m.id)}
                draggable={canDrag}
                onDragStart={(e) => {
                  e.dataTransfer.effectAllowed = 'move'
                  e.dataTransfer.setData('text/plain', m.id)
                  setDrag({ id: m.id, overId: null, zone: null })
                }}
                onDragOver={(e) => {
                  if (!drag || drag.id === m.id) return
                  e.preventDefault()
                  const zone = zoneFor(e, hierarchical)
                  if (drag.overId !== m.id || drag.zone !== zone) setDrag({ ...drag, overId: m.id, zone })
                }}
                onDrop={(e) => {
                  e.preventDefault()
                  drop(m, zoneFor(e, hierarchical))
                }}
                className={cn(
                  'group relative flex h-9 items-center gap-1.5 pr-2 text-sm outline-none hover:bg-muted/50 focus-visible:bg-muted focus-visible:ring-2 focus-visible:ring-ring/60 focus-visible:ring-inset',
                  focusedId === m.id && 'bg-primary/5 shadow-[inset_2px_0_0_var(--primary)] hover:bg-primary/10',
                  drag?.id === m.id && 'opacity-40',
                  over === 'inside' && 'bg-primary/10 ring-1 ring-primary ring-inset',
                )}
                style={{ paddingLeft: `${row.depth * 20 + 6}px` }}
              >
                {over === 'before' && <span aria-hidden className="absolute inset-x-2 top-0 h-0.5 bg-primary" />}
                {over === 'after' && <span aria-hidden className="absolute inset-x-2 bottom-0 h-0.5 bg-primary" />}
                {canDrag ? <GripVerticalIcon aria-hidden className="size-3.5 shrink-0 cursor-grab text-muted-foreground/60" /> : <span className="w-3.5 shrink-0" />}
                {hierarchical &&
                  (row.hasChildren ? (
                    <button type="button" tabIndex={-1} aria-label={collapsed.has(m.id) ? 'Expand' : 'Collapse'} onClick={() => toggle(m.id)} className="rounded p-0.5 text-muted-foreground hover:bg-muted">
                      <ChevronRightIcon className={cn('size-3.5 transition-transform', !collapsed.has(m.id) && 'rotate-90')} />
                    </button>
                  ) : (
                    <span className="w-4.5 shrink-0" />
                  ))}
                {thumb && <img src={thumb} alt="" className="size-6 shrink-0 rounded object-cover" />}
                {!m.isAssignable && <FolderIcon aria-hidden className="size-3.5 shrink-0 text-muted-foreground" />}
                {editable ? (
                  <span className="flex min-w-0 shrink-0" onKeyDown={(e) => e.stopPropagation()}>
                    <InlineText aria-label={`Name of ${m.key}`} value={m.displayName} onCommit={(v) => v && patch(m, { displayName: v })} className="h-7 -ml-2 w-auto max-w-80" />
                  </span>
                ) : (
                  <span className="truncate">{m.displayName}</span>
                )}
                <Mono className="shrink-0">{m.key}</Mono>
                {level && (
                  <Badge variant="outline" className="h-5 shrink-0 font-normal text-muted-foreground">
                    {level.displayName}
                  </Badge>
                )}
                {!m.isAssignable && (
                  <Badge variant="outline" className="h-5 shrink-0 font-normal text-muted-foreground">
                    folder
                  </Badge>
                )}
                {isRequired && <RequiredLock />}
                <span className="ml-auto flex shrink-0 items-center gap-0.5 opacity-0 group-focus-within:opacity-100 group-hover:opacity-100">
                  {hierarchical && editable && (
                    <Button variant="ghost" size="xs" tabIndex={-1} onClick={(e) => (e.stopPropagation(), startChild(m.id))}>
                      <PlusIcon /> Child
                    </Button>
                  )}
                  {canDelete(m) && (
                    <Button variant="ghost" size="icon-xs" tabIndex={-1} aria-label={`Delete ${m.displayName}`} onClick={(e) => (e.stopPropagation(), setDeleting(m))}>
                      <Trash2Icon />
                    </Button>
                  )}
                </span>
              </div>
              {addingChild && (
                <AddRow
                  variant="list"
                  label={`New child of ${m.displayName}`}
                  indent={row.depth + 1}
                  autoFocus
                  fields={addFields}
                  onSubmit={(v) => addMember(m.id, v)}
                  onCancel={() => setAddingUnder(null)}
                  hint="Enter adds and keeps the row open for the next sibling. Esc closes."
                />
              )}
              </Fragment>
            )
          })}
          {editable && !q && (
            <div ref={rootAddRef}>
              <AddRow variant="list" label="New member" fields={addFields} onSubmit={(v) => addMember(null, v)} hint={hierarchical ? 'Adds at the root. Use “+ Child” on a row to add beneath it.' : 'Enter adds and keeps the row ready for the next one.'} />
            </div>
          )}
          {!rows.length && <p className="px-3 py-6 text-center text-sm text-muted-foreground">No members match “{query}”.</p>}
          {index.unreachable.length > 0 && !q && (
            <div className="mt-1 flex flex-col border-t pt-1">
              <p className="flex items-center gap-1.5 px-3 py-1.5 text-xs text-destructive">
                <TriangleAlertIcon aria-hidden className="size-3.5" /> Not reachable from a root — their parent links form a cycle
              </p>
              {index.unreachable.map((m) => (
                <button
                  key={m.id}
                  type="button"
                  onClick={() => setFocusedId(m.id)}
                  className="flex h-8 items-center gap-2 px-3 text-left text-sm hover:bg-muted/50"
                >
                  <span>{m.displayName}</span>
                  <Mono>{m.key}</Mono>
                  <span className="text-xs text-muted-foreground">parent {index.byId.get(m.parentId ?? '')?.key ?? '—'}</span>
                </button>
              ))}
            </div>
          )}
        </div>
      )}
      </SplitView>

      <DeleteDialog
        target={deleting ? { type: 'member', versionId: content.version.id, memberId: deleting.id } : null}
        title={`Delete ${deleting?.displayName ?? 'member'}?`}
        onOpenChange={(o) => !o && setDeleting(null)}
        onConfirm={async () => (deleting ? (await run(() => api.deleteMember(content.version.id, deleting.id), `Deleted ${deleting.displayName}`)).ok : false)}
      />
      {editable && <ImportDialog open={importing} onOpenChange={setImporting} versionId={content.version.id} hierarchical={hierarchical} members={content.members} />}
      {editable && <ClearMembersDialog open={clearing} onOpenChange={setClearing} versionId={content.version.id} count={content.members.length} />}
    </div>
  )
}

function zoneFor(e: DragEvent, hierarchical: boolean): DropZone {
  const r = (e.currentTarget as HTMLElement).getBoundingClientRect()
  const y = (e.clientY - r.top) / r.height
  if (!hierarchical) return y < 0.5 ? 'before' : 'after'
  return y < 0.25 ? 'before' : y > 0.75 ? 'after' : 'inside'
}

function flatten(index: TreeIndex, collapsed: Set<string>, visible: Set<string> | null): Row[] {
  const out: Row[] = []
  const walk = (parent: string | null, depth: number) => {
    for (const m of index.children.get(parent) ?? []) {
      if (!index.depth.has(m.id)) continue
      if (visible && !visible.has(m.id)) continue
      const kids = index.children.get(m.id) ?? []
      out.push({ member: m, depth, hasChildren: kids.length > 0 })
      if (kids.length && (visible || !collapsed.has(m.id))) walk(m.id, depth + 1)
    }
  }
  walk(null, 0)
  return out
}

/* ───────────────────────── table view ───────────────────────── */

type SortKey = 'key' | 'displayName' | 'parent' | 'level' | 'isAssignable' | 'sortOrder' | `prop:${string}`
const PAGE = 100

function MemberTable({
  dim,
  content,
  index,
  editable,
  members,
  focusedId,
  onFocus,
  onPatch,
  onAdd,
  onDelete,
  setDeleting,
}: {
  dim: SectionProps['dim']
  content: SectionProps['content']
  index: TreeIndex
  editable: boolean
  members: Member[]
  focusedId: string | null
  onFocus: (id: string) => void
  onPatch: (m: Member, patch: Partial<MemberInput>) => Promise<unknown>
  onAdd: (values: Record<string, string>) => Promise<AddRowResult>
  onDelete: (m: Member) => boolean
  setDeleting: (m: Member) => void
}) {
  const levelOptions = [{ value: NONE, label: 'None' }, ...content.levels.map((l) => ({ value: l.id, label: l.displayName }))]
  const schemaType = (k: string) => dim.profile?.memberPropertySchema.find((f) => f.key === k)?.type
  const hierarchical = dim.kind === 'hierarchical'
  const propKeys = dim.profile?.memberPropertySchema.length
    ? dim.profile.memberPropertySchema.slice(0, 2).map((f) => ({ key: f.key, label: f.label }))
    : [...new Set(content.members.flatMap((m) => Object.keys(m.properties)))].slice(0, 2).map((k) => ({ key: k, label: k }))
  const [sort, setSort] = useState<{ key: SortKey; dir: 1 | -1 }>({ key: 'sortOrder', dir: 1 })
  const [page, setPage] = useState(0)
  const levelName = (id: string | null) => content.levels.find((l) => l.id === id)?.displayName ?? ''
  const value = (m: Member, k: SortKey): string | number => {
    if (k === 'parent') return index.byId.get(m.parentId ?? '')?.key ?? ''
    if (k === 'level') return levelName(m.levelId)
    if (k === 'isAssignable') return m.isAssignable ? 1 : 0
    if (k.startsWith('prop:')) {
      const v = m.properties[k.slice(5)]
      return typeof v === 'number' ? v : v == null ? '' : typeof v === 'object' ? JSON.stringify(v) : String(v)
    }
    return m[k as 'key' | 'displayName' | 'sortOrder']
  }
  const sorted = [...members].sort((a, b) => {
    const x = value(a, sort.key)
    const y = value(b, sort.key)
    return (typeof x === 'number' && typeof y === 'number' ? x - y : String(x).localeCompare(String(y))) * sort.dir
  })
  const pages = Math.max(1, Math.ceil(sorted.length / PAGE))
  const current = Math.min(page, pages - 1)
  const shown = sorted.slice(current * PAGE, current * PAGE + PAGE)

  const head = (k: SortKey, label: string, className?: string) => (
    <Th className={className}>
      <button type="button" onClick={() => setSort((s) => ({ key: k, dir: s.key === k ? (s.dir === 1 ? -1 : 1) : 1 }))} className="inline-flex items-center gap-1 hover:text-foreground">
        {label}
        {sort.key === k && (sort.dir === 1 ? <ArrowUpIcon className="size-3" /> : <ArrowDownIcon className="size-3" />)}
      </button>
    </Th>
  )
  const display = (v: unknown) => (v == null ? '' : typeof v === 'object' ? ('lat' in (v as object) ? `${(v as { lat: number }).lat}, ${(v as { lng: number }).lng}` : JSON.stringify(v)) : String(v))

  return (
    <div className="flex flex-col gap-2">
      <div className="overflow-x-auto rounded-lg border bg-card">
        <table className="w-full text-sm">
          <thead className="border-b bg-muted/40">
            <tr>
              {head('key', 'Key')}
              {head('displayName', 'Name')}
              {hierarchical && head('parent', 'Parent key')}
              {head('level', 'Level')}
              {head('isAssignable', 'Assignable')}
              {head('sortOrder', 'Sort', 'text-right')}
              {propKeys.map((p) => head(`prop:${p.key}`, p.label))}
              <Th className="w-24">
                <span className="sr-only">Actions</span>
              </Th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {shown.map((m) => (
              <tr key={m.id} aria-selected={focusedId === m.id} onClick={() => onFocus(m.id)} className={focusRowClass(focusedId === m.id)}>
                <Td>
                  <Mono>{m.key}</Mono>
                </Td>
                <Td className="py-1">
                  {editable ? <InlineText aria-label={`Name of ${m.key}`} value={m.displayName} onCommit={(v) => v && onPatch(m, { displayName: v })} /> : m.displayName}
                </Td>
                {hierarchical && (
                  <Td>
                    <Mono>{index.byId.get(m.parentId ?? '')?.key ?? '—'}</Mono>
                  </Td>
                )}
                <Td className="py-1 text-muted-foreground">
                  {editable && content.levels.length ? (
                    <InlineSelect aria-label={`Level of ${m.key}`} value={m.levelId ?? NONE} options={levelOptions} onCommit={(v) => onPatch(m, { levelId: v === NONE ? null : v })} className="min-w-28" />
                  ) : (
                    levelName(m.levelId) || '—'
                  )}
                </Td>
                <Td className="text-muted-foreground">
                  {editable ? <InlineSwitch aria-label={`${m.key} assignable`} checked={m.isAssignable} onCommit={(v) => onPatch(m, { isAssignable: v })} /> : m.isAssignable ? 'Yes' : 'Folder'}
                </Td>
                <Td className="w-20 py-1 text-right tabular-nums">
                  {editable ? <InlineNumber aria-label={`Sort order of ${m.key}`} value={m.sortOrder} onCommit={(v) => onPatch(m, { sortOrder: v })} /> : m.sortOrder}
                </Td>
                {propKeys.map((p) => {
                  const t = schemaType(p.key)
                  const v = m.properties[p.key]
                  const scalar = v == null || typeof v === 'string' || typeof v === 'number'
                  return (
                    <Td key={p.key} className="max-w-48 truncate py-1 text-muted-foreground">
                      {editable && scalar && (!t || t === 'text' || t === 'number' || t === 'url') ? (
                        <InlineText
                          aria-label={`${p.label} of ${m.key}`}
                          value={v == null ? '' : String(v)}
                          placeholder="—"
                          onCommit={(raw) => onPatch(m, { properties: { ...m.properties, [p.key]: raw === '' ? undefined : t === 'number' ? Number(raw) : raw } })}
                        />
                      ) : (
                        display(v) || '—'
                      )}
                    </Td>
                  )
                })}
                <Td className="text-right whitespace-nowrap">
                  {onDelete(m) && (
                    <Button variant="ghost" size="icon-xs" aria-label={`Delete ${m.displayName}`} onClick={(e) => (e.stopPropagation(), setDeleting(m))} className="opacity-0 group-hover:opacity-100 focus-visible:opacity-100">
                      <Trash2Icon />
                    </Button>
                  )}
                </Td>
              </tr>
            ))}
            {editable && (
              <AddRow
                label="New member"
                fields={[
                  { name: 'key', placeholder: 'KEY', mono: true, required: true, derive: (v) => keyFromName(v.displayName) },
                  { name: 'displayName', placeholder: 'Add member — name', required: true },
                ]}
                onSubmit={onAdd}
                trailingCells={(hierarchical ? 1 : 0) + 4 + propKeys.length}
                hint="Adds at the root. Enter keeps the row ready for the next one."
              />
            )}
          </tbody>
        </table>
      </div>
      {pages > 1 && (
        <div className="flex items-center justify-end gap-2 text-xs text-muted-foreground">
          {current * PAGE + 1}–{Math.min(sorted.length, (current + 1) * PAGE)} of {sorted.length}
          <Button variant="outline" size="xs" disabled={current === 0} onClick={() => setPage(current - 1)}>
            Previous
          </Button>
          <Button variant="outline" size="xs" disabled={current >= pages - 1} onClick={() => setPage(current + 1)}>
            Next
          </Button>
        </div>
      )}
    </div>
  )
}

/* ───────────────────────── import ───────────────────────── */

function parseRows(text: string): ImportRow[] {
  return text
    .split(/\r?\n/)
    .map((l) => l.trim())
    .filter(Boolean)
    .map((l) => l.split(l.includes('\t') ? '\t' : ',').map((c) => c.trim().replace(/^"|"$/g, '')))
    .filter((c, i) => !(i === 0 && c[0]?.toLowerCase() === 'key'))
    .map(([key = '', displayName = '', parentKey = '']) => ({ key, displayName: displayName || key, parentKey: parentKey || null }))
}

function ClearMembersDialog({ open, onOpenChange, versionId, count }: { open: boolean; onOpenChange: (o: boolean) => void; versionId: string; count: number }) {
  const api = useDimensionsApi()
  const { run, saving, problem, setProblem } = useDimCall()
  const close = (o: boolean) => {
    onOpenChange(o)
    if (!o) setProblem(null)
  }
  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Clear all members?</DialogTitle>
          <DialogDescription>
            All {count} member{count === 1 ? '' : 's'} in this version will be removed. Nothing is saved until you save your changes, and you can discard to restore them.
          </DialogDescription>
        </DialogHeader>
        {problem && <p className="text-sm text-destructive">{problem.detail ?? problem.title}</p>}
        <DialogFooter>
          <Button variant="outline" onClick={() => close(false)}>
            Cancel
          </Button>
          <Button
            variant="destructive"
            disabled={saving}
            onClick={async () => {
              const r = await run(() => api.clearMembers(versionId), ({ removed }) => `Cleared ${removed} member${removed === 1 ? '' : 's'}`)
              if (r.ok) close(false)
            }}
          >
            {saving ? 'Clearing…' : 'Clear all'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function ImportDialog({ open, onOpenChange, versionId, hierarchical, members }: { open: boolean; onOpenChange: (o: boolean) => void; versionId: string; hierarchical: boolean; members: Member[] }) {
  const api = useDimensionsApi()
  const { run, saving, problem, setProblem } = useDimCall()
  const [text, setText] = useState('')
  const [mode, setMode] = useState<ImportMode>('merge')
  const rows = parseRows(text)
  const existingKeys = new Set(members.map((m) => m.key))
  const importedKeys = new Set(rows.map((r) => r.key))
  const updates = rows.filter((r) => existingKeys.has(r.key)).length
  const removals = mode === 'replace' ? members.filter((m) => !importedKeys.has(m.key)).length : 0
  const close = (o: boolean) => {
    onOpenChange(o)
    if (!o) {
      setText('')
      setMode('merge')
      setProblem(null)
    }
  }
  const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? '' : 's'}`
  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>Import members</DialogTitle>
          <DialogDescription>Paste CSV or TSV with columns: key, name{hierarchical ? ', parent key' : ''}. A header row is skipped.</DialogDescription>
        </DialogHeader>
        <Textarea
          rows={6}
          className="font-mono text-xs"
          value={text}
          onChange={(e) => {
            setText(e.target.value)
            setProblem(null)
          }}
          placeholder={hierarchical ? 'BR40,Lakeside,D_NORTH\nBR41,Harbor Point,D_SOUTH' : 'GRADE_9,Grade 9\nGRADE_10,Grade 10'}
          aria-label="Rows to import"
          spellCheck={false}
        />
        {members.length > 0 && (
          <div className="flex flex-wrap items-center gap-3">
            <Segmented<ImportMode>
              aria-label="Existing members"
              value={mode}
              onChange={(m) => (setMode(m), setProblem(null))}
              options={[
                { value: 'merge', label: 'Merge' },
                { value: 'replace', label: 'Replace all' },
              ]}
            />
            <p className="text-xs text-muted-foreground">
              {mode === 'merge' ? 'Matching keys are updated; everything else is kept.' : 'Members not in the import are removed. Members still in use block the import.'}
            </p>
          </div>
        )}
        {problem?.detail && <p className="text-sm text-destructive">{problem.detail}</p>}
        {rows.length > 0 && (
          <div className="max-h-56 overflow-auto rounded-md border">
            <table className="w-full text-sm">
              <thead className="sticky top-0 border-b bg-muted">
                <tr>
                  <Th>Key</Th>
                  <Th>Name</Th>
                  {hierarchical && <Th>Parent key</Th>}
                  <Th>Check</Th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {rows.map((r, i) => {
                  const err = problem?.errors[`rows.${i}`]
                  return (
                    <tr key={i} className={cn(err && 'bg-destructive/10')}>
                      <Td>
                        <Mono>{r.key}</Mono>
                      </Td>
                      <Td>{r.displayName}</Td>
                      {hierarchical && (
                        <Td>
                          <Mono>{r.parentKey ?? '—'}</Mono>
                        </Td>
                      )}
                      <Td className={cn('text-xs', err ? 'text-destructive' : 'text-muted-foreground')}>{err?.join(' ') ?? (existingKeys.has(r.key) ? 'Updates existing' : 'New')}</Td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
        <DialogFooter className="items-center">
          {rows.length > 0 && (
            <p className="mr-auto text-xs text-muted-foreground">
              {[rows.length - updates ? `${rows.length - updates} new` : null, updates ? `${updates} updated` : null, removals ? `${removals} removed` : null].filter(Boolean).join(' · ')}
            </p>
          )}
          <Button variant="outline" onClick={() => close(false)}>
            Cancel
          </Button>
          <Button
            variant={removals ? 'destructive' : 'default'}
            disabled={!rows.length || saving}
            onClick={async () => {
              const r = await run(
                () => api.importMembers(versionId, rows, mode),
                ({ added, updated, removed }) => [`Imported ${plural(added.length, 'member')}`, updated.length ? `${updated.length} updated` : null, removed ? `${removed} removed` : null].filter(Boolean).join(', '),
              )
              if (r.ok) close(false)
            }}
          >
            {saving ? 'Importing…' : mode === 'replace' && members.length ? `Replace with ${plural(rows.length, 'member')}` : updates ? `Import ${plural(rows.length, 'row')}` : `Add ${plural(rows.length, 'member')}`}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
