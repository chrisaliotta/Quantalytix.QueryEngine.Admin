import { CameraIcon, MoreHorizontalIcon, RadioIcon, SearchIcon, SnowflakeIcon } from 'lucide-react'
import { useMemo, useState } from 'react'
import { ListPage } from '@/components/shell/page-shell'
import { useNavigationGuard } from '@/components/shell/changes'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { ConfirmDialog, EmptyState, Td, Th } from '@/components/dimensions/shared'
import { useAppNav } from '@/lib/navigation'
import { useSnapshotCall, useSnapshotList, useSnapshotsApi } from '@/lib/snapshots/client'
import { totalVersions, type SnapshotListItem } from '@/lib/snapshots/contract'
import { useTenantOverview } from '@/lib/tenant/client'
import { AbsTime, LinkText } from './shared'
import { deleteSnapshotMessage, RestoreDialog } from './restore-dialog'
import { SnapshotDetail } from './snapshot-detail'
import { CaptureSheet } from './snapshot-sheets'

export function SnapshotsScreen({ snapshotId }: { snapshotId: string | null }) {
  const nav = useAppNav()
  if (snapshotId) return <SnapshotDetail key={snapshotId} snapshotId={snapshotId} onBack={nav.snapshots} />
  return <SnapshotList />
}

function SnapshotList() {
  const { data } = useSnapshotList()
  const { data: tenant } = useTenantOverview()
  const nav = useAppNav()
  const guard = useNavigationGuard()
  const api = useSnapshotsApi()
  const { run } = useSnapshotCall()
  const [q, setQ] = useState('')
  const [capturing, setCapturing] = useState(false)
  const [restoring, setRestoring] = useState<SnapshotListItem | null>(null)
  const [deleting, setDeleting] = useState<SnapshotListItem | null>(null)
  const query = q.trim().toLowerCase()

  const rows = useMemo(
    () => (data?.frozen ?? []).filter((s) => !query || [s.key, s.displayName, s.description ?? ''].some((t) => t.toLowerCase().includes(query))),
    [data, query],
  )
  const capture = () => guard(() => setCapturing(true))

  return (
    <ListPage
      title="Snapshots"
      description="Live is edited in place and checked on every save. A frozen snapshot copies every live version at capture so requests and report comparisons can rerun it; restoring one makes live equal it again."
      actions={
        <Button size="sm" onClick={capture}>
          <CameraIcon />
          Capture live
        </Button>
      }
    >
      {data && (
        <section aria-label="Live snapshot" className="flex flex-col gap-3 rounded-lg border border-primary/30 bg-primary/5 p-4 sm:flex-row sm:items-center">
          <div className="flex size-9 shrink-0 items-center justify-center rounded-md bg-primary text-primary-foreground">
            <RadioIcon aria-hidden className="size-4" />
          </div>
          <p className="flex-1 text-sm text-pretty">
            <span className="font-mono font-semibold">{data.live.key}</span> · {totalVersions(data.live.counts)} versions
            {tenant && <> at revision <span className="font-mono tabular-nums">{tenant.semanticRevision}</span></>}. Requests that name no snapshot read it.
          </p>
          <div className="flex shrink-0 gap-2">
            <Button variant="outline" size="sm" onClick={() => nav.snapshot(data.live.id)}>
              Open
            </Button>
            <Button variant="outline" size="sm" onClick={capture}>
              <CameraIcon />
              Capture
            </Button>
          </div>
        </section>
      )}

      <div className="flex flex-wrap items-center gap-2">
        <h2 className="mr-2 text-sm font-medium">Frozen snapshots</h2>
        <div className="relative w-full sm:w-64">
          <SearchIcon aria-hidden className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search key, name, description" aria-label="Search snapshots" className="h-8 pl-8" />
        </div>
        <span className="font-mono text-xs text-muted-foreground tabular-nums">
          {rows.length} of {data?.frozen.length ?? 0}
        </span>
      </div>

      {!data ? (
        <p className="text-sm text-muted-foreground">Loading snapshots…</p>
      ) : !data.frozen.length ? (
        <EmptyState title="No frozen snapshots yet">Capture live at a period close so reports can compare as reported and analysts can rerun it.</EmptyState>
      ) : (
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-sm">
            <thead className="border-b bg-muted/40">
              <tr>
                <Th>Snapshot</Th>
                <Th>Captured</Th>
                <Th>By</Th>
                <Th className="text-right">Versions</Th>
                <Th>Last restored</Th>
                <Th className="w-10">
                  <span className="sr-only">Actions</span>
                </Th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {rows.map((s) => (
                <tr key={s.id} onClick={() => nav.snapshot(s.id)} className="cursor-pointer hover:bg-muted/40">
                  <Td>
                    <div className="flex items-start gap-2.5">
                      <SnowflakeIcon aria-hidden className="mt-0.5 size-4 shrink-0 text-muted-foreground" />
                      <div className="flex flex-col">
                        <LinkText onClick={() => nav.snapshot(s.id)} className="font-medium">
                          {s.displayName}
                        </LinkText>
                        <span className="font-mono text-xs text-muted-foreground">{s.key}</span>
                      </div>
                    </div>
                  </Td>
                  <Td className="text-muted-foreground">
                    <AbsTime iso={s.capturedAt} />
                  </Td>
                  <Td className="text-xs text-muted-foreground">{s.capturedBy}</Td>
                  <Td className="text-right font-mono text-xs tabular-nums">{totalVersions(s.counts)}</Td>
                  <Td className="text-muted-foreground">
                    <AbsTime iso={s.lastRestore?.restoredAt ?? null} />
                  </Td>
                  <Td>
                    <div onClick={(e) => e.stopPropagation()}>
                      <DropdownMenu>
                        <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label={`${s.key} actions`} />}>
                          <MoreHorizontalIcon />
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          <DropdownMenuItem onClick={() => nav.snapshot(s.id)}>Open</DropdownMenuItem>
                          <DropdownMenuItem onClick={() => guard(() => setRestoring(s))}>Restore into live…</DropdownMenuItem>
                          <DropdownMenuItem variant="destructive" onClick={() => setDeleting(s)}>
                            Delete
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </div>
                  </Td>
                </tr>
              ))}
              {!rows.length && (
                <tr>
                  <Td colSpan={6} className="py-8 text-center text-muted-foreground">
                    No snapshots match.
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
          nav.snapshot(id)
        }}
      />
      <RestoreDialog snapshot={restoring} onClose={() => setRestoring(null)} />
      <ConfirmDialog
        open={!!deleting}
        onOpenChange={(o) => !o && setDeleting(null)}
        title={`Delete ${deleting?.key}?`}
        confirmLabel="Delete"
        destructive
        onConfirm={async () => {
          const res = await run(() => api.deleteSnapshot(deleting!.id), () => `Deleted ${deleting!.key}.`)
          if (res.ok) setDeleting(null)
        }}
      >
        {deleting && deleteSnapshotMessage(deleting.key)}
      </ConfirmDialog>
    </ListPage>
  )
}
