import { ChevronRightIcon, SearchIcon, XIcon } from 'lucide-react'
import { useMemo, useState } from 'react'
import { toast } from 'sonner'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { ScrollArea } from '@/components/ui/scroll-area'
import { leafAccounts, mappingCovers, mappingsOnRow, memberIndex, rowsMappingMember } from '@/lib/report/coverage'
import { uid, useEditor } from '@/lib/report/store'
import { findRow } from '@/lib/report/tree'
import type { DimMember, Mapping } from '@/lib/report/types'
import { cn } from '@/lib/utils'
import { FolderGlyph, KeyText, factorLabel } from '../shared/bits'
import { SimpleSelect } from '../shared/simple-select'

interface MappingDialogProps {
  sourceKey: string
  rowKey: string
  onClose: () => void
}

export function MappingDialog({ sourceKey, rowKey, onClose }: MappingDialogProps) {
  const { report, ui, mapping } = useEditor()
  const source = report.sources.find((s) => s.key === sourceKey)
  const row = findRow(report.rows, rowKey)
  const [draft, setDraft] = useState<Mapping[]>(() => (source ? structuredClone(mappingsOnRow(source, rowKey)) : []))
  const [query, setQuery] = useState('')
  const [collapsed, setCollapsed] = useState<string[]>([])
  const index = useMemo(() => memberIndex(report.accounts), [report.accounts])

  if (!source || !row) return null

  const positiveFor = (key: string) => draft.find((m) => m.memberKey === key && m.factor > 0)
  const coveringFolder = (leafKey: string) =>
    draft.find((m) => m.factor > 0 && m.memberKey !== leafKey && index.get(m.memberKey)?.isFolder && mappingCovers(m, leafKey, index))
  const isExcluded = (leafKey: string) => draft.some((m) => m.memberKey === leafKey && m.factor < 0)

  const toggleFolder = (folder: DimMember) => {
    const descendants = new Set(leafAccounts(folder.children ?? []).map((l) => l.key))
    setDraft((prev) => {
      const existing = prev.find((m) => m.memberKey === folder.key && m.factor > 0)
      if (existing) return prev.filter((m) => m !== existing && !(descendants.has(m.memberKey) && m.factor < 0))
      return [...prev.filter((m) => !descendants.has(m.memberKey)), { id: uid('m'), rowKey, memberKey: folder.key, factor: 1 }]
    })
  }

  const toggleLeaf = (leaf: DimMember) => {
    setDraft((prev) => {
      const existing = prev.find((m) => m.memberKey === leaf.key && m.factor > 0)
      if (existing) return prev.filter((m) => m !== existing)
      return [...prev, { id: uid('m'), rowKey, memberKey: leaf.key, factor: 1 }]
    })
  }

  const toggleExclusion = (leaf: DimMember) => {
    setDraft((prev) =>
      prev.some((m) => m.memberKey === leaf.key && m.factor < 0)
        ? prev.filter((m) => !(m.memberKey === leaf.key && m.factor < 0))
        : [...prev, { id: uid('m'), rowKey, memberKey: leaf.key, factor: -1 }],
    )
  }

  const splitFolder = (folder: DimMember) => {
    const leaves = leafAccounts(folder.children ?? [])
    const descendants = new Set(leaves.map((l) => l.key))
    setDraft((prev) => {
      const excluded = new Set(prev.filter((m) => descendants.has(m.memberKey) && m.factor < 0).map((m) => m.memberKey))
      const rest = prev.filter((m) => m.memberKey !== folder.key && !descendants.has(m.memberKey))
      return [...rest, ...leaves.filter((l) => !excluded.has(l.key)).map((l) => ({ id: uid('m'), rowKey, memberKey: l.key, factor: 1 }))]
    })
  }

  const setFactor = (id: string, factor: number) => setDraft((prev) => prev.map((m) => (m.id === id ? { ...m, factor } : m)))
  const removeDraft = (id: string) => setDraft((prev) => prev.filter((m) => m.id !== id))

  const q = query.trim().toLowerCase()
  const matches = (m: DimMember) => !q || m.key.toLowerCase().includes(q) || m.label.toLowerCase().includes(q)
  const landing = leafAccounts(report.accounts).filter((leaf) => {
    const net = draft.filter((m) => mappingCovers(m, leaf.key, index)).reduce((s, m) => s + m.factor, 0)
    return net > 0
  }).length

  const describe = (m: Mapping) => {
    const info = index.get(m.memberKey)
    const name = info?.isFolder ? `${m.memberKey} folder` : `${m.memberKey} ${info?.member.label ?? ''}`.trim()
    return m.factor < 0 ? `excluded ${name}` : name
  }

  const save = () => {
    mapping.replaceRow(source.key, rowKey, draft)
    toast(`Saved mappings for ${row.label}`)
    onClose()
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="flex max-h-[min(44rem,calc(100dvh-2rem))] flex-col gap-0 p-0 sm:max-w-4xl">
        <DialogHeader className="gap-1 border-b px-5 pt-4 pb-3">
          <DialogTitle className="text-base">
            {source.name} {'->'} <span className="font-mono">{row.key}</span>
          </DialogTitle>
          <DialogDescription>Pick the accounts whose values add into this row.</DialogDescription>
        </DialogHeader>

        <div className="grid min-h-0 flex-1 md:grid-cols-[1fr_18rem]">
          <section className="flex min-h-0 flex-col border-b md:border-r md:border-b-0" aria-label="Account dimension">
            <div className="relative border-b px-3 py-2">
              <SearchIcon className="pointer-events-none absolute top-1/2 left-5 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden />
              <Input
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="Search accounts"
                aria-label="Search accounts"
                className="pl-8"
              />
            </div>
            <ScrollArea className="min-h-0 flex-1">
              <ul role="tree" aria-label="Accounts" className="flex flex-col p-2">
                {report.accounts.map((folder) => {
                  const kids = (folder.children ?? []).filter((c) => matches(c) || matches(folder))
                  if (!kids.length && !matches(folder)) return null
                  const folderMapping = positiveFor(folder.key)
                  const open = q !== '' || !collapsed.includes(folder.key)
                  const others = rowsMappingMember(source, folder.key, report.accounts, rowKey)
                  return (
                    <li key={folder.key} role="treeitem" aria-expanded={open} aria-selected={!!folderMapping}>
                      <div className="group flex h-8 items-center gap-2 rounded-md px-1 hover:bg-muted/60">
                        <button
                          type="button"
                          className="flex size-5 items-center justify-center rounded text-muted-foreground outline-none focus-visible:ring-2 focus-visible:ring-ring/60"
                          aria-label={open ? `Collapse ${folder.key}` : `Expand ${folder.key}`}
                          onClick={() => setCollapsed((c) => (c.includes(folder.key) ? c.filter((k) => k !== folder.key) : [...c, folder.key]))}
                        >
                          <ChevronRightIcon className={cn('size-3.5 transition-transform', open && 'rotate-90')} />
                        </button>
                        <label className="flex min-w-0 flex-1 cursor-pointer items-center gap-2">
                          <Checkbox checked={!!folderMapping} onCheckedChange={() => toggleFolder(folder)} />
                          <FolderGlyph className="text-muted-foreground" />
                          <KeyText className="font-medium text-foreground">{folder.key}</KeyText>
                          <span className="text-xs text-muted-foreground">{folder.children?.length ?? 0} accounts</span>
                        </label>
                        <AlsoOn rows={others} />
                        {folderMapping && (
                          <Button variant="link" size="xs" className="h-auto px-1" onClick={() => splitFolder(folder)}>
                            Pick children individually
                          </Button>
                        )}
                      </div>
                      {open && (
                        <ul role="group" className="ml-7 flex flex-col border-l pl-2">
                          {kids.map((leaf) => {
                            const covered = !!coveringFolder(leaf.key)
                            const excluded = covered && isExcluded(leaf.key)
                            const direct = positiveFor(leaf.key)
                            const elsewhere = rowsMappingMember(source, leaf.key, report.accounts, rowKey)
                            return (
                              <li key={leaf.key} role="treeitem" aria-selected={covered ? !excluded : !!direct} className="group flex h-8 items-center gap-2 rounded-md px-1 hover:bg-muted/60">
                                {covered ? (
                                  <Checkbox
                                    checked={!excluded}
                                    disabled
                                    aria-label={`${leaf.key} ${excluded ? 'excluded from' : 'covered by'} ${folder.key} folder`}
                                    className="opacity-50"
                                  />
                                ) : (
                                  <Checkbox checked={!!direct} onCheckedChange={() => toggleLeaf(leaf)} aria-label={`${leaf.key} ${leaf.label}`} />
                                )}
                                <KeyText className={cn('text-foreground', (covered || excluded) && 'text-muted-foreground', excluded && 'line-through')}>
                                  {leaf.key}
                                </KeyText>
                                <span className={cn('min-w-0 flex-1 truncate', covered && 'text-muted-foreground', excluded && 'line-through')}>{leaf.label}</span>
                                {excluded && <span className="text-xs text-muted-foreground">excluded</span>}
                                <AlsoOn rows={elsewhere} />
                                {covered && (
                                  <Button
                                    variant="ghost"
                                    size="xs"
                                    className="opacity-0 group-hover:opacity-100 focus-visible:opacity-100"
                                    onClick={() => toggleExclusion(leaf)}
                                  >
                                    {excluded ? 'Include' : 'Exclude'}
                                    <span className="sr-only"> {leaf.label}</span>
                                  </Button>
                                )}
                              </li>
                            )
                          })}
                        </ul>
                      )}
                    </li>
                  )
                })}
              </ul>
            </ScrollArea>
          </section>

          <aside className="flex min-h-0 flex-col bg-muted/30" aria-label="Mapping summary">
            <div className="border-b px-4 py-3">
              <p className="text-pretty">
                <span className="font-medium">
                  {draft.length} {draft.length === 1 ? 'mapping' : 'mappings'}
                </span>
                {draft.length > 0 && <span className="text-muted-foreground">: {draft.map(describe).join(', ')}</span>}
              </p>
            </div>
            <ScrollArea className="min-h-0 flex-1">
              <ul className="flex flex-col gap-1 p-2">
                {draft.map((m) => (
                  <li key={m.id} className="flex items-center gap-2 rounded-md bg-background px-2 py-1 ring-1 ring-border">
                    <span className={cn('min-w-0 flex-1 truncate', m.factor < 0 && 'text-muted-foreground')}>{describe(m)}</span>
                    {ui.advanced && <FactorControl factor={m.factor} onChange={(f) => setFactor(m.id, f)} label={describe(m)} />}
                    <Button variant="ghost" size="icon-xs" aria-label={`Remove ${describe(m)}`} onClick={() => removeDraft(m.id)}>
                      <XIcon />
                    </Button>
                  </li>
                ))}
                {!draft.length && <li className="px-2 py-6 text-center text-muted-foreground">Nothing mapped. This row will show blank from this source.</li>}
              </ul>
            </ScrollArea>
          </aside>
        </div>

        <DialogFooter className="mx-0 mb-0 items-center sm:justify-between">
          <span className="text-muted-foreground tabular-nums">
            {landing} {landing === 1 ? 'account lands' : 'accounts land'} on {row.key}
          </span>
          <div className="flex gap-2">
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button onClick={save}>Save</Button>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function AlsoOn({ rows }: { rows: string[] }) {
  if (!rows.length) return null
  return (
    <Badge variant="outline" className="font-normal text-muted-foreground">
      also on <span className="font-mono">{rows.join(', ')}</span>
    </Badge>
  )
}

const PRESETS = [
  { value: '1', label: 'x1' },
  { value: '-1', label: 'excluded (x-1)' },
  { value: '0.5', label: 'half (x0.5)' },
  { value: 'other', label: 'other…' },
]

function FactorControl({ factor, onChange, label }: { factor: number; onChange: (f: number) => void; label: string }) {
  const preset = PRESETS.some((p) => p.value === String(factor)) ? String(factor) : 'other'
  const [custom, setCustom] = useState(preset === 'other')
  const showInput = custom || preset === 'other'
  return (
    <div className="flex items-center gap-1">
      {showInput ? (
        <Input
          type="number"
          step="0.1"
          defaultValue={factor}
          aria-label={`Factor for ${label}`}
          className="h-7 w-16 font-mono text-xs"
          onBlur={(e) => {
            const n = Number(e.target.value)
            if (Number.isFinite(n)) onChange(n)
            if ([1, -1, 0.5].includes(n)) setCustom(false)
          }}
        />
      ) : (
        <SimpleSelect
          size="sm"
          value={preset}
          options={PRESETS}
          aria-label={`Factor for ${label}`}
          className="h-7 w-24 font-mono text-xs"
          onChange={(v) => (v === 'other' ? setCustom(true) : onChange(Number(v)))}
        />
      )}
      <span className="sr-only">{factorLabel(factor)}</span>
    </div>
  )
}
