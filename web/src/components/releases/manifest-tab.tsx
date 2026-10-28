import { PlusIcon, SearchIcon, SparklesIcon, TriangleAlertIcon, XIcon } from 'lucide-react'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useAppNav } from '@/lib/navigation'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { Td, Th } from '@/components/dimensions/shared'
import { useLookup, type Lookup } from '@/lib/releases/client'
import type { ManifestBinding, ManifestEntry, ReleaseViolation } from '@/lib/releases/contract'
import { cn } from '@/lib/utils'
import { FilterChip, LinkText, VersionTag, useDebounced } from './shared'

const PAGE = 100

export function bindingText(b: ManifestBinding, lookup: Lookup) {
  const dim = lookup.identity(b.dimensionId)?.displayName ?? b.dimensionId
  const via = b.mappingSetId ?? b.bridgeId
  if (via) return `${b.displayName} · ${b.kind} via ${lookup.identity(via)?.key ?? via} → ${dim}`
  return `${b.displayName} · ${b.kind} → ${dim}`
}

export function ManifestTab({
  manifest,
  editable,
  onChange,
  violations,
  highlight,
}: {
  manifest: ManifestEntry[]
  editable: boolean
  onChange: (m: ManifestEntry[]) => void
  violations: ReleaseViolation[]
  highlight: string | null
}) {
  const lookup = useLookup()
  const nav = useAppNav()
  const [q, setQ] = useState('')
  const query = useDebounced(q.trim().toLowerCase())
  const [newerOnly, setNewerOnly] = useState(false)
  const [limit, setLimit] = useState(PAGE)
  const [bulkOpen, setBulkOpen] = useState(false)

  const rows = useMemo(
    () =>
      manifest
        .map((e) => {
          const ds = lookup.dataset(e.datasetId)
          const pinned = lookup.version(e.datasetVersionId)
          const latest = lookup.latestPublished(e.datasetId)
          return { e, ds, pinned, latest, newer: !!latest && !!pinned && latest.versionNumber > pinned.versionNumber }
        })
        .sort((a, b) => (a.ds?.key ?? '').localeCompare(b.ds?.key ?? '')),
    [manifest, lookup],
  )
  const shown = rows.filter((r) => (!newerOnly || r.newer) && (!query || r.ds?.key.toLowerCase().includes(query) || r.ds?.displayName.toLowerCase().includes(query)))
  const moves = rows.filter((r) => r.newer)
  const notIn = lookup.datasets.filter((d) => !manifest.some((m) => m.datasetId === d.id)).sort((a, b) => a.key.localeCompare(b.key))
  const issuesFor = (datasetId: string) => violations.filter((v) => v.datasetId === datasetId)

  const setPin = (datasetId: string, versionId: string) => onChange(manifest.map((m) => (m.datasetId === datasetId ? { ...m, datasetVersionId: versionId } : m)))

  return (
    <div className="flex flex-col gap-4">
      {!editable && <p className="text-sm text-muted-foreground">Only a draft release&apos;s manifest changes. Clone this release to change its datasets.</p>}
      <div className="flex flex-wrap items-center gap-2">
        <div className="relative w-full sm:w-64">
          <SearchIcon aria-hidden className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search datasets" aria-label="Search datasets" className="h-8 pl-8" />
        </div>
        <FilterChip pressed={newerOnly} onPressedChange={setNewerOnly}>
          Newer version available
        </FilterChip>
        <span className="font-mono text-xs text-muted-foreground tabular-nums">
          {shown.length} of {rows.length}
        </span>
        {editable && (
          <div className="ml-auto flex gap-2">
            <Button size="sm" variant="outline" disabled={!moves.length} onClick={() => setBulkOpen(true)}>
              <SparklesIcon />
              Use latest published
            </Button>
            <DropdownMenu>
              <DropdownMenuTrigger render={<Button size="sm" variant="outline" disabled={!notIn.length} />}>
                <PlusIcon />
                Add dataset
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="max-h-80 w-72">
                {notIn.map((d) => {
                  const latest = lookup.latestPublished(d.id)
                  return (
                    <DropdownMenuItem key={d.id} disabled={!latest} onClick={() => onChange([...manifest, { datasetId: d.id, datasetVersionId: latest!.id }])} className="flex-col items-start gap-0.5">
                      <span className="font-mono text-xs">{d.key}</span>
                      <span className={cn('text-xs', latest ? 'text-muted-foreground' : 'text-warning-foreground')}>
                        {latest ? `Pins v${latest.versionNumber}, the latest published` : 'No published version; a release pins only published versions'}
                      </span>
                    </DropdownMenuItem>
                  )
                })}
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        )}
      </div>

      {!rows.length ? (
        <p className="rounded-lg border border-dashed px-6 py-10 text-center text-sm text-muted-foreground">The release pins no dataset versions.{editable && ' Add a dataset to start.'}</p>
      ) : (
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-sm">
            <thead className="border-b bg-muted/40">
              <tr>
                <Th>Dataset</Th>
                <Th>Pinned version</Th>
                <Th>Latest published</Th>
                <Th className="text-right">Bindings</Th>
                <Th>Issues</Th>
                {editable && (
                  <Th className="w-10">
                    <span className="sr-only">Actions</span>
                  </Th>
                )}
              </tr>
            </thead>
            <tbody className="divide-y">
              {shown.slice(0, limit).map(({ e, ds, pinned, latest, newer }) => (
                <ManifestRow key={e.datasetId} id={e.datasetId} highlight={highlight === e.datasetId}>
                  <Td>
                    <div className="flex flex-col">
                      <span className="flex items-center gap-2">
                        <LinkText onClick={() => nav.dataset(e.datasetId, e.datasetVersionId)}>{ds?.displayName ?? e.datasetId}</LinkText>
                        {ds && !ds.isEnabled && (
                          <Badge variant="outline" className="font-normal text-muted-foreground">
                            Disabled
                          </Badge>
                        )}
                      </span>
                      <span className="font-mono text-xs text-muted-foreground">{ds?.key}</span>
                    </div>
                  </Td>
                  <Td>
                    {editable ? (
                      <div className="flex flex-col gap-1">
                        <Select value={e.datasetVersionId} onValueChange={(v) => v && setPin(e.datasetId, String(v))}>
                          <SelectTrigger size="sm" className="w-40" aria-label={`Pinned version of ${ds?.key}`}>
                            <SelectValue>{pinned ? <VersionTag n={pinned.versionNumber} status={pinned.status} /> : '?'}</SelectValue>
                          </SelectTrigger>
                          <SelectContent>
                            {lookup.versionsOf(e.datasetId).filter((v) => v.status === 'published' || v.id === e.datasetVersionId).map((v) => (
                              <SelectItem key={v.id} value={v.id}>
                                <VersionTag n={v.versionNumber} status={v.status} />
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                        {pinned && pinned.status !== 'published' && (
                          <span className="text-xs text-warning-foreground">
                            v{pinned.versionNumber} is {pinned.status}; a release pins only published versions
                          </span>
                        )}
                      </div>
                    ) : pinned ? (
                      <VersionTag n={pinned.versionNumber} status={pinned.status} onClick={() => nav.dataset(e.datasetId, pinned.id)} />
                    ) : (
                      '?'
                    )}
                  </Td>
                  <Td>
                    {latest ? (
                      <span className="inline-flex items-center gap-2">
                        <span className="font-mono text-xs">v{latest.versionNumber}</span>
                        {newer && (
                          <button
                            type="button"
                            disabled={!editable}
                            onClick={() => setPin(e.datasetId, latest.id)}
                            title={editable ? `Pin v${latest.versionNumber}` : undefined}
                            className="rounded-full border border-warning/50 bg-warning-muted px-2 py-0.5 font-mono text-[11px] text-warning-foreground enabled:hover:bg-warning/20"
                          >
                            Newer: v{latest.versionNumber}
                          </button>
                        )}
                      </span>
                    ) : (
                      <span className="text-xs text-muted-foreground">None published</span>
                    )}
                  </Td>
                  <Td className="text-right">
                    {pinned?.bindings.length ? (
                      <Tooltip>
                        <TooltipTrigger render={<span tabIndex={0} className="cursor-default rounded-sm font-mono text-xs tabular-nums underline decoration-dotted underline-offset-2 outline-none focus-visible:ring-2 focus-visible:ring-ring" />}>
                          {pinned.bindings.length}
                        </TooltipTrigger>
                        <TooltipContent className="max-w-md">
                          <ul className="flex flex-col gap-0.5 font-mono text-[11px]">
                            {pinned.bindings.map((b) => (
                              <li key={b.displayName}>{bindingText(b, lookup)}</li>
                            ))}
                          </ul>
                        </TooltipContent>
                      </Tooltip>
                    ) : (
                      <span className="font-mono text-xs text-muted-foreground">0</span>
                    )}
                  </Td>
                  <Td>
                    <IssueIcons issues={issuesFor(e.datasetId)} />
                  </Td>
                  {editable && (
                    <Td>
                      <Button variant="ghost" size="icon-sm" aria-label={`Remove ${ds?.key}`} onClick={() => onChange(manifest.filter((m) => m.datasetId !== e.datasetId))}>
                        <XIcon />
                      </Button>
                    </Td>
                  )}
                </ManifestRow>
              ))}
              {!shown.length && (
                <tr>
                  <Td colSpan={6} className="py-8 text-center text-muted-foreground">
                    No datasets match.
                  </Td>
                </tr>
              )}
            </tbody>
          </table>
          {shown.length > limit && (
            <div className="flex justify-center border-t p-2">
              <Button variant="ghost" size="sm" onClick={() => setLimit((l) => l + PAGE)}>
                Show {Math.min(PAGE, shown.length - limit)} more
              </Button>
            </div>
          )}
        </div>
      )}

      <Dialog open={bulkOpen} onOpenChange={setBulkOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Use latest published</DialogTitle>
            <DialogDescription>Moves each dataset with a newer published version to it. Nothing is saved until you save the manifest.</DialogDescription>
          </DialogHeader>
          <ul className="flex flex-col divide-y rounded-md border font-mono text-xs">
            {moves.map((m) => (
              <li key={m.e.datasetId} className="flex items-center gap-2 px-3 py-2">
                <span className="flex-1">{m.ds?.key}</span>v{m.pinned?.versionNumber}
                <span className="text-muted-foreground">→</span>
                <span className="text-warning-foreground">v{m.latest?.versionNumber}</span>
              </li>
            ))}
          </ul>
          <DialogFooter>
            <Button variant="outline" onClick={() => setBulkOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => {
                const next = new Map(moves.map((m) => [m.e.datasetId, m.latest!.id]))
                onChange(manifest.map((e) => (next.has(e.datasetId) ? { ...e, datasetVersionId: next.get(e.datasetId)! } : e)))
                setBulkOpen(false)
              }}
            >
              Apply {moves.length}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}

function ManifestRow({ id, highlight, children }: { id: string; highlight: boolean; children: React.ReactNode }) {
  const ref = useRef<HTMLTableRowElement>(null)
  useEffect(() => {
    if (highlight) ref.current?.scrollIntoView({ block: 'center', behavior: 'smooth' })
  }, [highlight])
  return (
    <tr ref={ref} id={`manifest-${id}`} className={cn('transition-colors', highlight ? 'bg-warning-muted' : 'hover:bg-muted/40')}>
      {children}
    </tr>
  )
}

function IssueIcons({ issues }: { issues: ReleaseViolation[] }) {
  if (!issues.length) return <span className="text-muted-foreground">—</span>
  return (
    <Tooltip>
      <TooltipTrigger render={<span tabIndex={0} className="inline-flex items-center gap-1 rounded-sm text-destructive outline-none focus-visible:ring-2 focus-visible:ring-ring" />}>
        <TriangleAlertIcon aria-hidden className="size-4" />
        <span className="font-mono text-xs tabular-nums">{issues.length}</span>
        <span className="sr-only">{issues.length === 1 ? 'issue' : 'issues'}</span>
      </TooltipTrigger>
      <TooltipContent className="max-w-sm">
        <ul className="flex flex-col gap-1">
          {issues.map((i) => (
            <li key={i.message} className="text-pretty">
              {i.message}
            </li>
          ))}
        </ul>
      </TooltipContent>
    </Tooltip>
  )
}
