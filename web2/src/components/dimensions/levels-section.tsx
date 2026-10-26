import { GripVerticalIcon, PlusIcon, Trash2Icon } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { toast } from 'sonner'
import { useUndoAction } from '@/components/shell/history'
import { AddRow, InlineSelect, InlineSwitch, InlineText, focusRowClass, keyFromName, type AddRowField } from '@/components/shell/inline-table'
import { Button } from '@/components/ui/button'
import { useDimensionsApi, useRefreshDimensions } from '@/lib/dimensions/client'
import { KEY_HELP, type Level, type Periodicity } from '@/lib/dimensions/contract'
import { cn } from '@/lib/utils'
import type { SectionProps } from './dimension-detail'
import { DeleteDialog, EmptyState, Mono, TabIntro, Td, Th, useDimCall } from './shared'

const PERIODICITIES: Periodicity[] = ['day', 'month', 'quarter', 'year']
const PERIOD_OPTIONS = PERIODICITIES.map((p) => ({ value: p, label: p[0].toUpperCase() + p.slice(1) }))

export function LevelsSection({ dim, content, editable, focusId, clearFocus }: SectionProps) {
  const api = useDimensionsApi()
  const undoAction = useUndoAction()
  const refresh = useRefreshDimensions()
  const { run } = useDimCall()
  const [highlight, setHighlight] = useState<string | null>(null)
  const [deleting, setDeleting] = useState<Level | null>(null)
  const [dragId, setDragId] = useState<string | null>(null)
  const [overId, setOverId] = useState<string | null>(null)
  const addRef = useRef<HTMLTableSectionElement>(null)
  const versionId = content.version.id
  const time = dim.kind === 'time'
  const levels = [...content.levels].sort((a, b) => a.ordinal - b.ordinal)
  const usedBy = (id: string) => content.members.filter((m) => m.levelId === id).length

  useEffect(() => {
    if (!focusId) return
    if (content.levels.some((x) => x.id === focusId)) {
      setHighlight(focusId)
      requestAnimationFrame(() => document.getElementById(`level-${focusId}`)?.scrollIntoView({ block: 'center' }))
    }
    clearFocus()
  }, [focusId, content.levels, clearFocus])

  async function patch(l: Level, changes: Partial<Pick<Level, 'displayName' | 'isLeaf' | 'periodicity'>>) {
    await run(() => api.saveLevel(versionId, { id: l.id, key: l.key, displayName: l.displayName, isLeaf: l.isLeaf, periodicity: l.periodicity, ...changes }))
  }

  async function add(v: Record<string, string>) {
    const r = await api.saveLevel(versionId, { key: v.key, displayName: v.displayName, isLeaf: false, periodicity: null })
    if (!r.ok) return Object.keys(r.problem.errors).length ? r.problem.errors : { '': [r.problem.detail ?? r.problem.title] }
    await refresh()
  }

  async function reorder(fromId: string, toId: string) {
    if (fromId === toId) return
    const before = levels.map((l) => l.id)
    const ids = before.filter((id) => id !== fromId)
    ids.splice(ids.indexOf(toId) + (before.indexOf(fromId) < before.indexOf(toId) ? 1 : 0), 0, fromId)
    const r = await api.reorderLevels(versionId, ids)
    if (!r.ok) return toast.error(r.problem.title, { description: r.problem.detail })
    await refresh()
    toast.success('Reordered levels', { action: undoAction() })
  }

  const addFields: AddRowField[] = [
    { name: 'displayName', placeholder: 'Add level — name', required: true },
    { name: 'key', placeholder: 'KEY', mono: true, required: true, derive: (v) => keyFromName(v.displayName ?? '') },
  ]
  const trailing = time ? 4 : 3

  return (
    <div className="flex flex-col gap-4">
      <TabIntro
        action={
          editable && (
            <Button size="sm" onClick={() => addRef.current?.querySelector('input')?.focus()}>
              <PlusIcon /> Add level
            </Button>
          )
        }
      >
        Levels are labels for hierarchy tiers. The structure itself comes from member parentage.
      </TabIntro>
      {!levels.length && !editable ? (
        <EmptyState title="No levels">Members can still form a tree; levels only name its tiers.</EmptyState>
      ) : (
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-sm">
            <thead className="border-b bg-muted/40">
              <tr>
                <Th className="w-16">Ordinal</Th>
                <Th>Display name</Th>
                <Th>Key</Th>
                <Th className="w-20">Leaf</Th>
                {time && <Th className="w-36">Periodicity</Th>}
                <Th className="text-right">Members</Th>
                <Th className="w-20">
                  <span className="sr-only">Actions</span>
                </Th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {levels.map((l) => (
                <tr
                  key={l.id}
                  id={`level-${l.id}`}
                  draggable={editable}
                  onDragStart={(e) => {
                    if ((e.target as HTMLElement).closest('input,button,[role=combobox]')) return e.preventDefault()
                    setDragId(l.id)
                  }}
                  onDragOver={(e) => {
                    if (!dragId) return
                    e.preventDefault()
                    setOverId(l.id)
                  }}
                  onDragEnd={() => {
                    setDragId(null)
                    setOverId(null)
                  }}
                  onDrop={(e) => {
                    e.preventDefault()
                    if (dragId) reorder(dragId, l.id)
                    setDragId(null)
                    setOverId(null)
                  }}
                  onClick={() => highlight && highlight !== l.id && setHighlight(null)}
                  className={cn(focusRowClass(highlight === l.id), dragId === l.id && 'opacity-40', overId === l.id && dragId !== l.id && 'bg-primary/10')}
                >
                  <Td className="tabular-nums">
                    <span className="flex items-center gap-1.5">
                      {editable && <GripVerticalIcon aria-hidden className="size-3.5 cursor-grab text-muted-foreground/60" />}
                      {l.ordinal}
                    </span>
                  </Td>
                  <Td>{editable ? <InlineText aria-label={`Name of level ${l.key}`} value={l.displayName} onCommit={(v) => v && v !== l.displayName && patch(l, { displayName: v })} className="-ml-2" /> : l.displayName}</Td>
                  <Td>
                    <Mono>{l.key}</Mono>
                  </Td>
                  <Td>{editable ? <InlineSwitch aria-label={`${l.displayName} is the leaf level`} checked={l.isLeaf} onCommit={(v) => patch(l, { isLeaf: v })} /> : <span className="text-muted-foreground">{l.isLeaf ? 'Leaf' : '—'}</span>}</Td>
                  {time && (
                    <Td>
                      {editable ? (
                        <InlineSelect aria-label={`Periodicity of ${l.displayName}`} value={l.periodicity ?? ''} options={PERIOD_OPTIONS} onCommit={(v) => patch(l, { periodicity: v as Periodicity })} className="-ml-2.5 w-32" />
                      ) : (
                        <span className="text-muted-foreground capitalize">{l.periodicity ?? '—'}</span>
                      )}
                    </Td>
                  )}
                  <Td className="text-right tabular-nums">{usedBy(l.id)}</Td>
                  <Td className="text-right whitespace-nowrap">
                    {editable && (
                      <Button variant="ghost" size="icon-xs" aria-label={`Delete ${l.displayName}`} onClick={() => setDeleting(l)} className="opacity-0 group-hover:opacity-100 focus-visible:opacity-100">
                        <Trash2Icon />
                      </Button>
                    )}
                  </Td>
                </tr>
              ))}
            </tbody>
            {editable && (
              <tbody ref={addRef}>
                <AddRow label="New level" fields={addFields} onSubmit={add} leadingCells={1} trailingCells={trailing} hint={`New levels are added last; drag to reorder. ${KEY_HELP}`} />
              </tbody>
            )}
          </table>
        </div>
      )}
      <DeleteDialog
        target={deleting ? { type: 'level', versionId, levelId: deleting.id } : null}
        title={`Delete level ${deleting?.displayName ?? ''}?`}
        onOpenChange={(o) => !o && setDeleting(null)}
        onConfirm={async () => (deleting ? (await run(() => api.deleteLevel(versionId, deleting.id), `Deleted level ${deleting.displayName}`)).ok : false)}
      />
    </div>
  )
}
