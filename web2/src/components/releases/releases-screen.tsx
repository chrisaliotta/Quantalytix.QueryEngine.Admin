import { GitCompareIcon, MoreHorizontalIcon, PlusIcon, RadioIcon, SearchIcon } from 'lucide-react'
import { ListPage } from '@/components/shell/page-shell'
import { useAppNav } from '@/lib/navigation'
import { useMemo, useState } from 'react'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { ConfirmDialog, EmptyState, Mono, Td, Th } from '@/components/dimensions/shared'
import { useLookup, useRelCall, useReleaseList, useReleasesApi, type Lookup } from '@/lib/releases/client'
import type { ReleaseDiff, ReleaseListItem, VersionStatus } from '@/lib/releases/contract'
import { useTenantOverview } from '@/lib/tenant/client'
import { ActivateDialog } from './activate-dialog'
import { CreateReleaseSheet } from './create-release-sheet'
import { ReleaseDetail } from './release-detail'
import { ActiveBadge, AbsTime, DiffCounts, FilterChip, LinkText, RelTime, StatusBadge, useDebounced } from './shared'

export function ReleasesScreen({ releaseId }: { releaseId: string | null }) {
  const nav = useAppNav()
  if (releaseId) return <ReleaseDetail key={releaseId} releaseId={releaseId} onBack={nav.releases} />
  return <ReleaseList />
}

const STATUSES: VersionStatus[] = ['draft', 'validated', 'published', 'retired']

export function describeDiff(lookup: Lookup) {
  return (d: ReleaseDiff) => {
    const key = (id: string) => lookup.dataset(id)?.key ?? id
    const vn = (id: string) => `v${lookup.version(id)?.versionNumber ?? '?'}`
    return [
      ...d.added.map((a) => `+ ${key(a.datasetId)} ${vn(a.versionId)}`),
      ...d.changed.map((c) => `~ ${key(c.datasetId)} ${vn(c.fromVersionId)} → ${vn(c.toVersionId)}`),
      ...d.removed.map((r) => `− ${key(r.datasetId)} ${vn(r.versionId)}`),
    ]
  }
}

function ReleaseList() {
  const { data: list } = useReleaseList()
  const lookup = useLookup()
  const nav = useAppNav()
  const api = useReleasesApi()
  const { run } = useRelCall()
  const [q, setQ] = useState('')
  const query = useDebounced(q.trim().toLowerCase())
  const [statuses, setStatuses] = useState<Set<VersionStatus>>(new Set(['draft', 'validated', 'published']))
  const [creating, setCreating] = useState<{ copyFrom: string | null } | null>(null)
  const [activating, setActivating] = useState<ReleaseListItem | null>(null)
  const [deleting, setDeleting] = useState<ReleaseListItem | null>(null)

  const active = list?.find((r) => r.isActive)
  const rows = useMemo(() => (list ?? []).filter((r) => statuses.has(r.status) && (!query || r.key.toLowerCase().includes(query))), [list, statuses, query])
  const describe = describeDiff(lookup)

  return (
    <ListPage
      title="Releases"
      description="A release pins one dataset version per dataset. Only the active release reaches queries; activating an older published release rolls back."
      actions={
        <Button size="sm" onClick={() => setCreating({ copyFrom: active?.id ?? null })}>
          <PlusIcon />
          New release
        </Button>
      }
    >

        {active && <ActiveReleaseCard release={active} onCompare={(id) => nav.release(id, 'changes')} others={list?.filter((r) => !r.isActive && r.status !== 'draft') ?? []} />}

        <div className="flex flex-wrap items-center gap-2">
          <div className="relative w-full sm:w-64">
            <SearchIcon aria-hidden className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search release keys" aria-label="Search releases" className="h-8 pl-8" />
          </div>
          <div role="group" aria-label="Status" className="flex flex-wrap gap-1.5">
            {STATUSES.map((s) => (
              <FilterChip
                key={s}
                pressed={statuses.has(s)}
                onPressedChange={(p) =>
                  setStatuses((cur) => {
                    const next = new Set(cur)
                    if (p) next.add(s)
                    else next.delete(s)
                    return next
                  })
                }
              >
                <span className="capitalize">{s}</span>
              </FilterChip>
            ))}
          </div>
          <span className="font-mono text-xs text-muted-foreground tabular-nums">
            {rows.length} of {list?.length ?? 0}
          </span>
        </div>

        {!list ? (
          <p className="text-sm text-muted-foreground">Loading releases…</p>
        ) : !list.length ? (
          <EmptyState title="No releases" action={<Button size="sm" onClick={() => setCreating({ copyFrom: null })}>New release</Button>}>
            A release pins one dataset version per dataset. Create one to choose which dataset versions queries read.
          </EmptyState>
        ) : (
          <div className="overflow-x-auto rounded-lg border bg-card">
            <table className="w-full text-sm">
              <thead className="border-b bg-muted/40">
                <tr>
                  <Th>Release</Th>
                  <Th>Status</Th>
                  <Th>Active</Th>
                  <Th className="text-right">Datasets</Th>
                  <Th>Changes vs active</Th>
                  <Th>Created</Th>
                  <Th>Published</Th>
                  <Th className="w-10">
                    <span className="sr-only">Actions</span>
                  </Th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {rows.map((r) => (
                  <tr key={r.id} onClick={() => nav.release(r.id)} className="cursor-pointer hover:bg-muted/40">
                    <Td>
                      <LinkText mono onClick={() => nav.release(r.id)} className="font-medium">
                        {r.key}
                      </LinkText>
                    </Td>
                    <Td>
                      <StatusBadge status={r.status} />
                    </Td>
                    <Td>{r.isActive ? <ActiveBadge /> : null}</Td>
                    <Td className="text-right font-mono text-xs tabular-nums">{r.datasetCount}</Td>
                    <Td>{r.diffVsActive ? <DiffCounts diff={r.diffVsActive} describe={describe} /> : <span className="text-muted-foreground">—</span>}</Td>
                    <Td className="text-muted-foreground">
                      <RelTime iso={r.createdAt} />
                    </Td>
                    <Td className="text-muted-foreground">
                      <RelTime iso={r.publishedAt} />
                    </Td>
                    <Td>
                      <div onClick={(e) => e.stopPropagation()}>
                        <DropdownMenu>
                          <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label={`${r.key} actions`} />}>
                            <MoreHorizontalIcon />
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end" className="w-56">
                            <DropdownMenuItem onClick={() => nav.release(r.id)}>Open</DropdownMenuItem>
                            <DropdownMenuItem onClick={() => setCreating({ copyFrom: r.id })}>Clone</DropdownMenuItem>
                            {r.status === 'published' && !r.isActive && <DropdownMenuItem onClick={() => setActivating(r)}>Activate</DropdownMenuItem>}
                            <DropdownMenuSeparator />
                            {r.status === 'draft' ? (
                              <DropdownMenuItem variant="destructive" onClick={() => setDeleting(r)}>
                                Delete
                              </DropdownMenuItem>
                            ) : (
                              <DropdownMenuItem disabled className="flex-col items-start gap-0.5">
                                Delete
                                <span className="text-xs text-pretty text-muted-foreground">Only a draft can be deleted. Retire a published release instead.</span>
                              </DropdownMenuItem>
                            )}
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </div>
                    </Td>
                  </tr>
                ))}
                {!rows.length && (
                  <tr>
                    <Td colSpan={8} className="py-8 text-center text-muted-foreground">
                      No releases match.
                    </Td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        )}

      <CreateReleaseSheet
        open={!!creating}
        copyFrom={creating?.copyFrom ?? null}
        onClose={() => setCreating(null)}
        onCreated={(id) => {
          setCreating(null)
          nav.release(id)
        }}
      />
      {activating && <ActivateDialog releaseId={activating.id} onClose={() => setActivating(null)} />}
      <ConfirmDialog
        open={!!deleting}
        onOpenChange={(o) => !o && setDeleting(null)}
        title={`Delete ${deleting?.key}?`}
        confirmLabel="Delete"
        destructive
        onConfirm={async () => {
          if (!deleting) return
          const res = await run(() => api.deleteRelease(deleting.id), `Deleted ${deleting.key}.`)
          if (res.ok) setDeleting(null)
        }}
      >
        The draft and its manifest are deleted.
      </ConfirmDialog>
    </ListPage>
  )
}

function ActiveReleaseCard({ release, others, onCompare }: { release: ReleaseListItem; others: ReleaseListItem[]; onCompare: (id: string) => void }) {
  const nav = useAppNav()
  const { data: tenant } = useTenantOverview()
  return (
    <section aria-label="Active release" className="flex flex-col gap-3 rounded-lg border border-primary/30 bg-primary/5 p-4 sm:flex-row sm:items-center">
      <div className="flex size-9 shrink-0 items-center justify-center rounded-md bg-primary text-primary-foreground">
        <RadioIcon aria-hidden className="size-4" />
      </div>
      <p className="flex-1 text-sm text-pretty">
        Queries and reports run on <span className="font-mono font-semibold">{release.key}</span>: {release.datasetCount} datasets, published{' '}
        <AbsTime iso={release.publishedAt} />. <span className="text-muted-foreground">Activated</span> <AbsTime iso={release.activatedAt} />{' '}
        <span className="text-muted-foreground">by</span> <Mono className="text-foreground">{release.activatedBy}</Mono>.{' '}
        <span className="text-muted-foreground">Semantic revision</span> <Mono className="text-foreground">{tenant?.semanticRevision ?? '…'}</Mono>.
      </p>
      <div className="flex shrink-0 gap-2">
        <Button variant="outline" size="sm" onClick={() => nav.release(release.id)}>
          Open
        </Button>
        <DropdownMenu>
          <DropdownMenuTrigger render={<Button variant="outline" size="sm" disabled={!others.length} />}>
            <GitCompareIcon />
            Compare with…
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-56">
            {others.map((o) => (
              <DropdownMenuItem key={o.id} onClick={() => onCompare(o.id)}>
                <span className="flex-1 font-mono text-xs">{o.key}</span>
                <StatusBadge status={o.status} />
              </DropdownMenuItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </section>
  )
}
