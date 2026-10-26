import { CameraIcon, HistoryIcon, MoreHorizontalIcon, SearchIcon, SlidersHorizontalIcon } from 'lucide-react'
import { ListPage } from '@/components/shell/page-shell'
import { useAppNav } from '@/lib/navigation'
import { useMemo, useState } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { ConfirmDialog, EmptyState, Td, Th } from '@/components/dimensions/shared'
import { useLookup, useRelCall, useReleasesApi, useSetList } from '@/lib/releases/client'
import type { SetListItem } from '@/lib/releases/contract'
import { AbsTime, LinkText, useDebounced } from '@/components/releases/shared'
import { SetDetail } from './set-detail'
import { CaptureSheet } from './set-sheets'

export function AssumptionSetsScreen({ setId }: { setId: string | null }) {
  const nav = useAppNav()
  if (setId) return <SetDetail key={setId} setId={setId} onBack={nav.sets} />
  return <SetList />
}

export function deleteSetMessage(key: string) {
  return `The set and its pins are deleted, so requests and report comparisons can no longer name ${key}. The versions it pins stay.`
}

function SetList() {
  const { data } = useSetList()
  const lookup = useLookup()
  const nav = useAppNav()
  const api = useReleasesApi()
  const { run } = useRelCall()
  const [q, setQ] = useState('')
  const query = useDebounced(q.trim().toLowerCase())
  const [capturing, setCapturing] = useState(false)
  const [deleting, setDeleting] = useState<SetListItem | null>(null)

  const rows = useMemo(
    () =>
      (data?.historical ?? [])
        .filter((s) => !query || [s.key, s.displayName, s.description ?? ''].some((t) => t.toLowerCase().includes(query)))
        .sort((a, b) => (b.capturedAt ?? '').localeCompare(a.capturedAt ?? '')),
    [data, query],
  )
  const activeRelease = lookup.catalog ? lookup.release(lookup.catalog.tenant.activeReleaseId) : undefined

  return (
    <ListPage
      title="Assumption sets"
      description="An assumption set pins one version of each dimension, mapping set and bridge. Requests that name none use the active set; historical sets reproduce an earlier result."
      actions={
        <Button size="sm" onClick={() => setCapturing(true)}>
          <CameraIcon />
          Capture active set
        </Button>
      }
    >

        {data && (
          <section aria-label="Active set" className="flex flex-col gap-3 rounded-lg border border-primary/30 bg-primary/5 p-4 sm:flex-row sm:items-center">
            <div className="flex size-9 shrink-0 items-center justify-center rounded-md bg-primary text-primary-foreground">
              <SlidersHorizontalIcon aria-hidden className="size-4" />
            </div>
            <p className="flex-1 text-sm text-pretty">
              <span className="font-mono font-semibold">{data.active.key}</span> · Active — pins every published version (<span className="font-mono tabular-nums">{data.active.pinCount}</span> pins)
              and reads the active release{' '}
              {activeRelease ? (
                <LinkText mono onClick={() => nav.release(activeRelease.id)} className="font-semibold">
                  {activeRelease.key}
                </LinkText>
              ) : (
                '…'
              )}
              . Every publish repoints it.
            </p>
            <div className="flex shrink-0 gap-2">
              <Button variant="outline" size="sm" onClick={() => nav.set(data.active.id)}>
                Open
              </Button>
              <Button variant="outline" size="sm" onClick={() => setCapturing(true)}>
                <CameraIcon />
                Capture
              </Button>
            </div>
          </section>
        )}

        <div className="flex flex-wrap items-center gap-2">
          <h2 className="mr-2 text-sm font-medium">Historical sets</h2>
          <div className="relative w-full sm:w-64">
            <SearchIcon aria-hidden className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search key, name, description" aria-label="Search assumption sets" className="h-8 pl-8" />
          </div>
          <span className="font-mono text-xs text-muted-foreground tabular-nums">
            {rows.length} of {data?.historical.length ?? 0}
          </span>
        </div>

        {!data ? (
          <p className="text-sm text-muted-foreground">Loading assumption sets…</p>
        ) : !data.historical.length ? (
          <EmptyState title="No historical sets yet">Capture the active set at a period close so reports can compare as reported and analysts can backtest.</EmptyState>
        ) : (
          <div className="overflow-x-auto rounded-lg border bg-card">
            <table className="w-full text-sm">
              <thead className="border-b bg-muted/40">
                <tr>
                  <Th>Set</Th>
                  <Th>Release</Th>
                  <Th>Captured</Th>
                  <Th className="text-right">Pins</Th>
                  <Th className="text-right">Live pins</Th>
                  <Th className="text-right">Changed since capture</Th>
                  <Th className="w-10">
                    <span className="sr-only">Actions</span>
                  </Th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {rows.map((s) => {
                  const rel = s.releaseId ? lookup.release(s.releaseId) : undefined
                  return (
                    <tr key={s.id} onClick={() => nav.set(s.id)} className="cursor-pointer hover:bg-muted/40">
                      <Td>
                        <div className="flex items-start gap-2.5">
                          <HistoryIcon aria-hidden className="mt-0.5 size-4 shrink-0 text-muted-foreground" />
                          <div className="flex flex-col">
                            <LinkText onClick={() => nav.set(s.id)} className="font-medium">
                              {s.displayName}
                            </LinkText>
                            <span className="font-mono text-xs text-muted-foreground">{s.key}</span>
                          </div>
                        </div>
                      </Td>
                      <Td>
                        {rel ? (
                          <span className="inline-flex items-center gap-2" onClick={(e) => e.stopPropagation()}>
                            <LinkText mono onClick={() => nav.release(rel.id)}>
                              {rel.key}
                            </LinkText>
                            {rel.status === 'retired' && (
                              <Badge variant="outline" className="font-normal text-muted-foreground">
                                Retired
                              </Badge>
                            )}
                          </span>
                        ) : (
                          '—'
                        )}
                      </Td>
                      <Td className="text-muted-foreground">
                        <AbsTime iso={s.capturedAt} />
                      </Td>
                      <Td className="text-right font-mono text-xs tabular-nums">{s.pinCount}</Td>
                      <Td className="text-right">
                        <Tooltip>
                          <TooltipTrigger render={<span tabIndex={0} className="cursor-default rounded-sm font-mono text-xs tabular-nums underline decoration-dotted underline-offset-2 outline-none focus-visible:ring-2 focus-visible:ring-ring" />}>
                            {s.livePins}
                          </TooltipTrigger>
                          <TooltipContent>Edits to these versions still change what this set reads</TooltipContent>
                        </Tooltip>
                      </Td>
                      <Td className="text-right font-mono text-xs tabular-nums">{s.changedSinceCapture}</Td>
                      <Td>
                        <div onClick={(e) => e.stopPropagation()}>
                          <DropdownMenu>
                            <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label={`${s.key} actions`} />}>
                              <MoreHorizontalIcon />
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              <DropdownMenuItem onClick={() => nav.set(s.id)}>Open</DropdownMenuItem>
                              <DropdownMenuItem variant="destructive" onClick={() => setDeleting(s)}>
                                Delete
                              </DropdownMenuItem>
                            </DropdownMenuContent>
                          </DropdownMenu>
                        </div>
                      </Td>
                    </tr>
                  )
                })}
                {!rows.length && (
                  <tr>
                    <Td colSpan={7} className="py-8 text-center text-muted-foreground">
                      No sets match.
                    </Td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        )}

      <CaptureSheet
        open={capturing}
        onClose={() => setCapturing(false)}
        onCaptured={(id) => {
          setCapturing(false)
          nav.set(id)
        }}
      />
      <ConfirmDialog
        open={!!deleting}
        onOpenChange={(o) => !o && setDeleting(null)}
        title={`Delete ${deleting?.key}?`}
        confirmLabel="Delete"
        destructive
        onConfirm={async () => {
          if (!deleting) return
          const res = await run(() => api.deleteSet(deleting.id), `Deleted ${deleting.key}.`)
          if (res.ok) setDeleting(null)
        }}
      >
        {deleting && deleteSetMessage(deleting.key)}
      </ConfirmDialog>
    </ListPage>
  )
}
