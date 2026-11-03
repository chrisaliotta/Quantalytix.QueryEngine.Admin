import { CameraIcon, CheckIcon, CopyIcon, GitCompareIcon, HistoryIcon, LayersIcon, MoreHorizontalIcon, PencilIcon, RotateCcwIcon, SearchIcon } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { useNavigationGuard } from '@/components/shell/changes'
import { DetailHeader, DetailKey, DetailShell, DetailTitle, PageMessage, SectionNav, type NavGroup } from '@/components/shell/page-shell'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { ConfirmDialog, EmptyState, ReadValue, TabIntro, Td, Th } from '@/components/dimensions/shared'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { useAppNav, useSearchParam } from '@/lib/navigation'
import { useSnapshot, useSnapshotCall, useSnapshotComparison, useSnapshotList, useSnapshotsApi } from '@/lib/snapshots/client'
import { KIND_LABEL, KIND_ONE, SNAPSHOT_KINDS, SNAPSHOT_TABS, type SnapshotDetail as Detail, type SnapshotEntry, type SnapshotKind, type SnapshotTab } from '@/lib/snapshots/contract'
import { cn } from '@/lib/utils'
import { useEntryBrowser } from './entry-browser'
import { deleteSnapshotMessage, RestoreDialog } from './restore-dialog'
import { AbsTime, DIFF_TONE, FilterChip, LinkText, Section, VersionBadge } from './shared'
import { CaptureSheet, RelabelSheet } from './snapshot-sheets'

export function SnapshotDetail({ snapshotId, onBack }: { snapshotId: string; onBack: () => void }) {
  const { data, error } = useSnapshot(snapshotId)
  if (error)
    return (
      <PageMessage
        action={
          <Button size="sm" variant="outline" onClick={onBack}>
            Back to snapshots
          </Button>
        }
      >
        This snapshot no longer exists.
      </PageMessage>
    )
  if (!data) return <PageMessage>Loading snapshot…</PageMessage>
  return <SnapshotPage detail={data} onBack={onBack} />
}

function SnapshotPage({ detail, onBack }: { detail: Detail; onBack: () => void }) {
  const api = useSnapshotsApi()
  const nav = useAppNav()
  const guard = useNavigationGuard()
  const { run } = useSnapshotCall()
  const { snapshot } = detail
  const [tabParam, setTabParam] = useSearchParam('tab')
  const tab: SnapshotTab = SNAPSHOT_TABS.includes(tabParam as SnapshotTab) ? (tabParam as SnapshotTab) : 'contents'
  const [editing, setEditing] = useState(false)
  const [capturing, setCapturing] = useState(false)
  const [restoring, setRestoring] = useState(false)
  const [deleting, setDeleting] = useState(false)

  const groups: NavGroup<SnapshotTab>[] = [
    {
      items: [
        { id: 'contents', label: 'Contents', icon: LayersIcon, badge: detail.entries.length },
        { id: 'compare', label: 'Compare', icon: GitCompareIcon },
        ...(snapshot.isLive ? [] : [{ id: 'restores' as const, label: 'Restores', icon: HistoryIcon, badge: detail.restores.length }]),
      ],
    },
  ]

  const header = (
    <DetailHeader
      section="Snapshots"
      onBack={onBack}
      title={<DetailTitle>{snapshot.displayName}</DetailTitle>}
      meta={
        <>
          <DetailKey>{snapshot.key}</DetailKey>
          <VersionBadge version={{ snapshotId: snapshot.id, isLive: snapshot.isLive }} />
        </>
      }
    >
      {snapshot.isLive ? (
        <Button variant="outline" size="sm" onClick={() => guard(() => setCapturing(true))}>
          <CameraIcon />
          Capture
        </Button>
      ) : (
        <>
          <Button variant="outline" size="sm" onClick={() => setEditing(true)}>
            <PencilIcon />
            Edit
          </Button>
          <Button variant="outline" size="sm" onClick={() => guard(() => setRestoring(true))}>
            <RotateCcwIcon />
            Restore into live
          </Button>
          <DropdownMenu>
            <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label="More actions" />}>
              <MoreHorizontalIcon />
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem variant="destructive" onClick={() => setDeleting(true)}>
                Delete
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </>
      )}
    </DetailHeader>
  )

  return (
    <DetailShell header={header} nav={<SectionNav label="Snapshot sections" groups={groups} active={tab} onSelect={(v) => setTabParam(v === 'contents' ? null : v)} />}>
      <FactsCard detail={detail} />
      {tab === 'contents' && <ContentsTab detail={detail} />}
      {tab === 'compare' && <CompareTab detail={detail} />}
      {tab === 'restores' && <RestoresTab detail={detail} />}

      <RelabelSheet snapshot={editing ? snapshot : null} onClose={() => setEditing(false)} />
      <RestoreDialog snapshot={restoring ? snapshot : null} onClose={() => setRestoring(false)} />
      <CaptureSheet
        open={capturing}
        onClose={() => setCapturing(false)}
        onCaptured={(id) => {
          setCapturing(false)
          nav.snapshot(id)
        }}
      />
      <ConfirmDialog
        open={deleting}
        onOpenChange={setDeleting}
        title={`Delete ${snapshot.key}?`}
        confirmLabel="Delete"
        destructive
        onConfirm={async () => {
          const res = await run(() => api.deleteSnapshot(snapshot.id), () => `Deleted ${snapshot.key}.`)
          if (res.ok) {
            setDeleting(false)
            onBack()
          }
        }}
      >
        {deleteSnapshotMessage(snapshot.key)}
      </ConfirmDialog>
    </DetailShell>
  )
}

function FactsCard({ detail }: { detail: Detail }) {
  const { snapshot } = detail
  return (
    <section aria-label="Facts" className="grid gap-4 rounded-lg border bg-card p-4 sm:grid-cols-2 lg:grid-cols-4">
      <ReadValue label="Key" mono>
        {snapshot.key}
      </ReadValue>
      <ReadValue label="Captured">{snapshot.isLive ? 'Live is edited in place' : <AbsTime iso={snapshot.capturedAt} />}</ReadValue>
      <ReadValue label="Captured by">{snapshot.capturedBy}</ReadValue>
      <ReadValue label="Created">
        <AbsTime iso={snapshot.createdAt} />
      </ReadValue>
      <div className="sm:col-span-2 lg:col-span-4">
        <ReadValue label="Description">{snapshot.description}</ReadValue>
      </div>
      {!snapshot.isLive && (
        <div className="flex flex-col gap-2 sm:col-span-2 lg:col-span-4">
          <span className="text-xs text-muted-foreground">How to name it</span>
          <div className="grid gap-3 md:grid-cols-2">
            <Snippet caption="In a query" code={`{ "snapshot": "${snapshot.key}" }`} />
            <Snippet caption="In a report comparison" code={`{ "compareWith": { "snapshot": "${snapshot.key}" } }`} />
          </div>
        </div>
      )}
    </section>
  )
}

function Snippet({ caption, code }: { caption: string; code: string }) {
  const [copied, setCopied] = useState(false)
  return (
    <figure className="flex flex-col gap-1">
      <div className="flex items-center gap-2 rounded-md border bg-muted/40 py-1.5 pr-1.5 pl-3">
        <pre className="min-w-0 flex-1 overflow-x-auto font-mono text-xs">
          <code>{code}</code>
        </pre>
        <Button
          variant="ghost"
          size="icon-sm"
          aria-label={`Copy ${caption.toLowerCase()} snippet`}
          onClick={async () => {
            try {
              await navigator.clipboard.writeText(code)
              setCopied(true)
              setTimeout(() => setCopied(false), 1500)
            } catch {
              toast.error('Could not copy to the clipboard.')
            }
          }}
        >
          {copied ? <CheckIcon /> : <CopyIcon />}
        </Button>
      </div>
      <figcaption className="text-xs text-muted-foreground">{caption}</figcaption>
    </figure>
  )
}

/* ───────────────────────── Contents ───────────────────────── */

function ContentsTab({ detail }: { detail: Detail }) {
  const browser = useEntryBrowser()
  const [q, setQ] = useState('')
  const [kinds, setKinds] = useState<Set<SnapshotKind>>(new Set(SNAPSHOT_KINDS))
  const query = q.trim().toLowerCase()
  const isLive = detail.snapshot.isLive

  const filtered = detail.entries.filter((e) => kinds.has(e.kind) && (!query || e.key.toLowerCase().includes(query) || e.displayName.toLowerCase().includes(query)))
  const toggle = (kind: SnapshotKind, on: boolean) =>
    setKinds((cur) => {
      const next = new Set(cur)
      if (on) next.add(kind)
      else next.delete(kind)
      return next
    })

  return (
    <div className="flex flex-col gap-4">
      <TabIntro>
        {isLive
          ? 'Every identity live holds, with its live version. Open one to edit it.'
          : 'The version this snapshot holds of each identity. Versions open read-only; restore the snapshot to make live equal it.'}
      </TabIntro>
      <div className="flex flex-wrap items-center gap-2">
        <div className="relative w-full sm:w-64">
          <SearchIcon aria-hidden className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search key or name" aria-label="Search contents" className="h-8 pl-8" />
        </div>
        <div role="group" aria-label="Kind" className="flex flex-wrap gap-1.5">
          {SNAPSHOT_KINDS.map((k) => (
            <FilterChip key={k} pressed={kinds.has(k)} onPressedChange={(on) => toggle(k, on)}>
              {KIND_LABEL[k]}
            </FilterChip>
          ))}
        </div>
        <span className="ml-auto font-mono text-xs text-muted-foreground tabular-nums">
          {filtered.length} of {detail.entries.length}
        </span>
      </div>

      {SNAPSHOT_KINDS.filter((k) => kinds.has(k)).map((kind) => {
        const rows = filtered.filter((e) => e.kind === kind)
        return (
          <Section key={kind} title={KIND_LABEL[kind]} count={detail.entries.filter((e) => e.kind === kind).length}>
            {rows.length ? (
              <div className="overflow-x-auto rounded-lg border bg-card">
                <table className="w-full text-sm">
                  <thead className="border-b bg-muted/40">
                    <tr>
                      <Th>Key</Th>
                      <Th>Name</Th>
                      <Th>Version</Th>
                    </tr>
                  </thead>
                  <tbody className="divide-y">
                    {rows.map((e) => (
                      <tr key={e.identityId} className="hover:bg-muted/40">
                        <Td>
                          <LinkText mono onClick={() => browser.open(e, isLive)}>
                            {e.key}
                          </LinkText>
                        </Td>
                        <Td>{e.displayName}</Td>
                        <Td className="font-mono text-xs text-muted-foreground">{e.versionId}</Td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <p className="text-sm text-muted-foreground">No {KIND_LABEL[kind].toLowerCase()} match.</p>
            )}
          </Section>
        )
      })}
      {browser.sheet}
    </div>
  )
}

/* ───────────────────────── Compare ───────────────────────── */

function CompareTab({ detail }: { detail: Detail }) {
  const browser = useEntryBrowser()
  const { data: list } = useSnapshotList()
  const [compareTo, setCompareTo] = useState<string | null>(null)
  const thisId = detail.snapshot.id
  const others = list ? [list.live, ...list.frozen].filter((s) => s.id !== thisId) : []
  const fallback = detail.snapshot.isLive ? list?.frozen[0]?.id : list?.live.id
  const otherId = compareTo && others.some((s) => s.id === compareTo) ? compareTo : (fallback ?? null)
  const other = others.find((s) => s.id === otherId)
  const { data: c } = useSnapshotComparison(otherId, thisId)

  const entry = (e: SnapshotEntry) => (
    <LinkText mono onClick={() => browser.open(e, detail.snapshot.isLive)}>
      {e.key}
    </LinkText>
  )
  const groups = c
    ? [
        { title: 'Only here', tone: DIFF_TONE.added, mark: '+', empty: `Everything here is also in ${other?.key}.`, rows: c.added.map((e) => ({ id: e.identityId, kind: e.kind, body: entry(e) })) },
        { title: `Only in ${other?.key}`, tone: DIFF_TONE.removed, mark: '−', empty: `Everything ${other?.key} holds is here too.`, rows: c.removed.map((e) => ({ id: e.identityId, kind: e.kind, body: <span className="font-mono text-xs">{e.key}</span> })) },
        {
          title: 'In both',
          tone: DIFF_TONE.common,
          mark: '=',
          empty: 'The two snapshots share no identity.',
          rows: c.common.map((p) => ({
            id: p.identityId,
            kind: p.kind,
            body: (
              <>
                <span className="font-mono text-xs">{p.key}</span>
                <span className={cn('text-xs', p.differs ? 'font-medium text-foreground' : 'text-muted-foreground')}>{p.differs ? 'changed' : 'unchanged'}</span>
              </>
            ),
          })),
        },
      ]
    : []

  return (
    <div className="flex max-w-4xl flex-col gap-6">
      <TabIntro
        action={
          <div className="flex items-center gap-2">
            <label htmlFor="compare-snapshot" className="text-xs text-muted-foreground">
              Compare against
            </label>
            <SimpleSelect
              id="compare-snapshot"
              size="sm"
              className="w-56"
              value={otherId ?? ''}
              onChange={setCompareTo}
              placeholder="Choose a snapshot"
              options={others.map((s) => ({ value: s.id, label: s.key, hint: s.displayName }))}
            />
          </div>
        }
      >
        Identities only this snapshot holds, only the chosen one holds, and both hold. Each snapshot holds its own copy of every version; an identity both hold is changed when the two copies' content differs.
      </TabIntro>
      {!list ? (
        <p className="text-sm text-muted-foreground">Loading snapshots…</p>
      ) : !otherId ? (
        <EmptyState title="Nothing to compare with">There is no other snapshot yet.</EmptyState>
      ) : !c ? (
        <p className="text-sm text-muted-foreground">Comparing…</p>
      ) : (
        <div className="flex flex-col gap-4">
          {groups.map((g) => (
            <div key={g.title} className="flex flex-col gap-1.5">
              <h3 className="flex items-center gap-2 text-xs font-medium text-muted-foreground">
                <span className={cn('font-mono', g.tone)}>{g.mark}</span>
                {g.title}
                <span className="font-mono tabular-nums">{g.rows.length}</span>
              </h3>
              {g.rows.length ? (
                <ul className="flex flex-col divide-y rounded-md border bg-card">
                  {g.rows.map((r) => (
                    <li key={r.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2 text-sm">
                      <span aria-hidden className={cn('w-3 font-mono', g.tone)}>
                        {g.mark}
                      </span>
                      {r.body}
                      <span className="ml-auto text-xs text-muted-foreground">{KIND_ONE[r.kind]}</span>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="px-1 text-xs text-muted-foreground">{g.empty}</p>
              )}
            </div>
          ))}
        </div>
      )}
      {browser.sheet}
    </div>
  )
}

/* ───────────────────────── Restores ───────────────────────── */

function RestoresTab({ detail }: { detail: Detail }) {
  return (
    <Section title="Restores into live" count={detail.restores.length}>
      {detail.restores.length ? (
        <div className="max-w-2xl overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-sm">
            <thead className="border-b bg-muted/40">
              <tr>
                <Th>Restored</Th>
                <Th>By</Th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {detail.restores.map((r) => (
                <tr key={r.restoredAt}>
                  <Td>
                    <AbsTime iso={r.restoredAt} />
                  </Td>
                  <Td className="text-muted-foreground">{r.restoredBy}</Td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">Never restored.</p>
      )}
    </Section>
  )
}
