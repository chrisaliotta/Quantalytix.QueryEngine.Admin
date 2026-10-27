import { ActivityIcon, CameraIcon, CheckIcon, CopyIcon, FileTextIcon, GitCompareIcon, MoreHorizontalIcon, PencilIcon, PinIcon, SearchIcon, SnowflakeIcon } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useAppNav, useSearchParam } from '@/lib/navigation'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { DetailHeader, DetailKey, DetailShell, DetailTitle, PageMessage, SectionNav, type NavGroup } from '@/components/shell/page-shell'
import { ConfirmDialog, EmptyState, ReadValue, TabIntro, Td, Th } from '@/components/dimensions/shared'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { useLookup, useRelCall, useReleasesApi, useSet, useSetComparison, useSetList } from '@/lib/releases/client'
import { SET_TABS, type PinKind, type PinRow, type PinState, type SetDetail as Detail, type SetTab } from '@/lib/releases/contract'
import { cn } from '@/lib/utils'
import { DiffList, diffSummary } from '@/components/releases/release-tabs'
import { AbsTime, DIFF_TONE, FilterChip, LinkText, Section, StatusBadge, VersionTag } from '@/components/releases/shared'
import { CaptureSheet, RelabelSheet } from './set-sheets'
import { deleteSetMessage } from './sets-screen'

const KIND_LABEL: Record<PinKind, string> = { dimension: 'Dimensions', mapping_set: 'Mapping sets', bridge: 'Bridges' }
const KIND_ONE: Record<PinKind, string> = { dimension: 'dimension', mapping_set: 'mapping set', bridge: 'bridge' }
const KINDS: PinKind[] = ['dimension', 'mapping_set', 'bridge']
const PAGE = 100

export function SetDetail({ setId, onBack }: { setId: string; onBack: () => void }) {
  const { data, error } = useSet(setId)
  if (error)
    return (
      <PageMessage
        action={
          <Button size="sm" variant="outline" onClick={onBack}>
            Back to assumption sets
          </Button>
        }
      >
        This assumption set no longer exists.
      </PageMessage>
    )
  if (!data) return <PageMessage>Loading assumption set…</PageMessage>
  return <SetPage detail={data} onBack={onBack} />
}

function SetPage({ detail, onBack }: { detail: Detail; onBack: () => void }) {
  const api = useReleasesApi()
  const lookup = useLookup()
  const nav = useAppNav()
  const { run } = useRelCall()
  const { set } = detail
  const historical = set.status === 'historical'
  const [tabParam, setTabParam] = useSearchParam('tab')
  const tab: SetTab = SET_TABS.includes(tabParam as SetTab) ? (tabParam as SetTab) : 'pins'
  const [compareTo, setCompareTo] = useState<string | null>(null)
  const [editing, setEditing] = useState(false)
  const [capturing, setCapturing] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const release = lookup.release(detail.readsReleaseId)

  const groups: NavGroup<SetTab>[] = [
    {
      items: [
        { id: 'pins', label: 'Pins', icon: PinIcon },
        { id: 'compare', label: 'Compare', icon: GitCompareIcon },
      ],
    },
  ]

  const header = (
    <DetailHeader
      section="Assumption sets"
      onBack={onBack}
      title={<DetailTitle>{set.displayName}</DetailTitle>}
      meta={
        <>
          <DetailKey>{set.key}</DetailKey>
          <span className={cn('rounded-full border px-2 py-0.5 text-xs capitalize', historical ? 'text-muted-foreground' : 'border-primary/40 text-primary')}>{set.status}</span>
          {release && (
            <span className="hidden text-xs text-muted-foreground lg:inline">
              reads{' '}
              <LinkText mono onClick={() => nav.release(release.id)}>
                {release.key}
              </LinkText>
            </span>
          )}
        </>
      }
    >
        {historical && (
          <Button variant="outline" size="sm" onClick={() => setEditing(true)}>
            <PencilIcon />
            Edit
          </Button>
        )}
        {!historical && (
          <Button variant="outline" size="sm" onClick={() => setCapturing(true)}>
            <CameraIcon />
            Capture
          </Button>
        )}
        <DropdownMenu>
          <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label="More actions" />}>
            <MoreHorizontalIcon />
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-64">
            {historical ? (
              <DropdownMenuItem variant="destructive" onClick={() => setDeleting(true)}>
                Delete
              </DropdownMenuItem>
            ) : (
              <DropdownMenuItem disabled className="flex-col items-start gap-0.5">
                Delete
                <span className="text-xs text-pretty text-muted-foreground">The active set holds the default versions and cannot be deleted.</span>
              </DropdownMenuItem>
            )}
          </DropdownMenuContent>
        </DropdownMenu>
    </DetailHeader>
  )

  return (
    <DetailShell
      header={header}
      nav={<SectionNav label="Assumption set sections" groups={groups} active={tab} onSelect={(v) => setTabParam(v === 'pins' ? null : v)} />}
    >
      <FactsCard detail={detail} />

      {tab === 'pins' ? <PinsTab detail={detail} /> : <CompareTab detail={detail} compareTo={compareTo} onCompareTo={setCompareTo} />}

      <RelabelSheet set={editing ? set : null} onClose={() => setEditing(false)} />
      <CaptureSheet
        open={capturing}
        onClose={() => setCapturing(false)}
        onCaptured={(id) => {
          setCapturing(false)
          nav.set(id)
        }}
      />
      <ConfirmDialog
        open={deleting}
        onOpenChange={setDeleting}
        title={`Delete ${set.key}?`}
        confirmLabel="Delete"
        destructive
        onConfirm={async () => {
          const res = await run(() => api.deleteSet(set.id), `Deleted ${set.key}.`)
          if (res.ok) {
            setDeleting(false)
            onBack()
          }
        }}
      >
        {deleteSetMessage(set.key)}
      </ConfirmDialog>
    </DetailShell>
  )
}

function FactsCard({ detail }: { detail: Detail }) {
  const lookup = useLookup()
  const nav = useAppNav()
  const { set } = detail
  const release = lookup.release(detail.readsReleaseId)
  const historical = set.status === 'historical'
  return (
    <section aria-label="Facts" className="grid gap-4 rounded-lg border bg-card p-4 sm:grid-cols-2 lg:grid-cols-4">
      <ReadValue label="Key" mono>
        {set.key}
      </ReadValue>
      <ReadValue label="Status">
        <span className="capitalize">{set.status}</span>
      </ReadValue>
      <ReadValue label="Release">
        {release && (
          <span className="inline-flex flex-wrap items-center gap-2">
            {!historical && <span className="text-muted-foreground">The active release,</span>}
            <LinkText mono onClick={() => nav.release(release.id)}>
              {release.key}
            </LinkText>
            <StatusBadge status={release.status} />
          </span>
        )}
      </ReadValue>
      <ReadValue label="Captured">{set.capturedAt ? <AbsTime iso={set.capturedAt} /> : null}</ReadValue>
      <ReadValue label="Created">
        <AbsTime iso={set.createdAt} />
      </ReadValue>
      <div className="sm:col-span-2 lg:col-span-3">
        <ReadValue label="Description">{set.description}</ReadValue>
      </div>
      {historical && (
        <div className="flex flex-col gap-2 sm:col-span-2 lg:col-span-4">
          <span className="text-xs text-muted-foreground">How to name it</span>
          <div className="grid gap-3 md:grid-cols-2">
            <Snippet caption="In a query" code={`{ "assumptionSet": "${set.key}" }`} />
            <Snippet caption="In a report comparison" code={`{ "compareWith": { "assumptionSet": "${set.key}" } }`} />
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

/* ───────────────────────── Pins ───────────────────────── */

const STATE_LABEL: Record<PinState, string> = { live: 'Live', frozen: 'Frozen', report_owned: 'Report-owned' }

function PinStateCell({ state }: { state: PinState }) {
  if (state === 'live')
    return (
      <span className="inline-flex items-center gap-1.5 text-sm" title="The pinned version is still published, so edits to it still change what the set reads.">
        <span aria-hidden className="size-2 rounded-full bg-success" />
        <ActivityIcon aria-hidden className="size-3.5 text-success-foreground" />
        Live
      </span>
    )
  if (state === 'frozen')
    return (
      <span className="inline-flex items-center gap-1.5 text-sm text-muted-foreground" title="The pinned version is retired and never changes again.">
        <SnowflakeIcon aria-hidden className="size-3.5" />
        Frozen
      </span>
    )
  return (
    <span className="inline-flex items-center gap-1.5 text-sm text-muted-foreground" title="A report's dimension or a bridge into one. Captures leave these out.">
      <FileTextIcon aria-hidden className="size-3.5" />
      Report-owned
    </span>
  )
}

function PinsTab({ detail }: { detail: Detail }) {
  const lookup = useLookup()
  const nav = useAppNav()
  const historical = detail.set.status === 'historical'
  const [q, setQ] = useState('')
  const [kinds, setKinds] = useState<Set<PinKind>>(new Set(KINDS))
  const [states, setStates] = useState<Set<PinState>>(new Set())
  const [limits, setLimits] = useState<Record<PinKind, number>>({ dimension: PAGE, mapping_set: PAGE, bridge: PAGE })
  const query = q.trim().toLowerCase()

  const presentStates = useMemo(() => [...new Set(detail.pins.map((p) => p.state))], [detail.pins])
  const live = detail.pins.filter((p) => p.state === 'live').length
  const filtered = detail.pins.filter((p) => {
    if (!kinds.has(p.kind)) return false
    if (states.size && !states.has(p.state)) return false
    if (!query) return true
    const i = lookup.identity(p.identityId)
    return !!i && (i.key.toLowerCase().includes(query) || i.displayName.toLowerCase().includes(query))
  })
  const toggle = <T,>(set: Set<T>, v: T, on: boolean) => {
    const n = new Set(set)
    if (on) n.add(v)
    else n.delete(v)
    return n
  }

  return (
    <div className="flex flex-col gap-4">
      {historical && live > 0 && (
        <p role="status" className="flex items-start gap-2 rounded-md border border-success/40 bg-success-muted p-3 text-sm text-pretty text-success-foreground">
          <ActivityIcon aria-hidden className="mt-0.5 size-4 shrink-0" />
          <span>
            {live} {live === 1 ? 'pin still points at a published version' : 'pins still point at published versions'}. Until each gets a successor, edits to it also change what{' '}
            <span className="font-mono">{detail.set.key}</span> reads.
          </span>
        </p>
      )}
      <div className="flex flex-wrap items-center gap-2">
        <div className="relative w-full sm:w-64">
          <SearchIcon aria-hidden className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search identities" aria-label="Search pins" className="h-8 pl-8" />
        </div>
        <div role="group" aria-label="Kind" className="flex flex-wrap gap-1.5">
          {KINDS.map((k) => (
            <FilterChip key={k} pressed={kinds.has(k)} onPressedChange={(on) => setKinds((s) => toggle(s, k, on))}>
              {KIND_LABEL[k]}
            </FilterChip>
          ))}
        </div>
        <span aria-hidden className="h-5 w-px bg-border" />
        <div role="group" aria-label="State" className="flex flex-wrap gap-1.5">
          {presentStates.map((s) => (
            <FilterChip key={s} pressed={states.has(s)} onPressedChange={(on) => setStates((cur) => toggle(cur, s, on))}>
              {STATE_LABEL[s]}
            </FilterChip>
          ))}
        </div>
        <span className="ml-auto font-mono text-xs text-muted-foreground tabular-nums">
          {filtered.length} of {detail.pins.length}
        </span>
      </div>

      {KINDS.filter((k) => kinds.has(k)).map((kind) => {
        const rows = filtered.filter((p) => p.kind === kind).sort((a, b) => (lookup.identity(a.identityId)?.key ?? '').localeCompare(lookup.identity(b.identityId)?.key ?? ''))
        const total = detail.pins.filter((p) => p.kind === kind).length
        return (
          <Section key={kind} title={KIND_LABEL[kind]} count={total}>
            {rows.length ? (
              <div className="overflow-x-auto rounded-lg border bg-card">
                <table className="w-full text-sm">
                  <thead className="border-b bg-muted/40">
                    <tr>
                      <Th>Identity</Th>
                      <Th>Pinned version</Th>
                      {historical && <Th>Published now</Th>}
                      <Th>State</Th>
                    </tr>
                  </thead>
                  <tbody className="divide-y">
                    {rows.slice(0, limits[kind]).map((p) => (
                      <PinTableRow key={p.identityId} pin={p} historical={historical} onOpen={(versionId) => {
                        const i = lookup.identity(p.identityId)
                        if (i) nav.identity(i, versionId)
                      }} />
                    ))}
                  </tbody>
                </table>
                {rows.length > limits[kind] && (
                  <div className="flex justify-center border-t p-2">
                    <Button variant="ghost" size="sm" onClick={() => setLimits((l) => ({ ...l, [kind]: l[kind] + PAGE }))}>
                      Show {Math.min(PAGE, rows.length - limits[kind])} more
                    </Button>
                  </div>
                )}
              </div>
            ) : (
              <p className="text-sm text-muted-foreground">No {KIND_LABEL[kind].toLowerCase()} match.</p>
            )}
          </Section>
        )
      })}
    </div>
  )
}

function PinTableRow({ pin, historical, onOpen }: { pin: PinRow; historical: boolean; onOpen: (versionId?: string) => void }) {
  const lookup = useLookup()
  const i = lookup.identity(pin.identityId)
  const v = lookup.identityVersion(pin.versionId)
  const now = pin.publishedVersionId ? lookup.identityVersion(pin.publishedVersionId) : undefined
  const changed = historical && !!now && now.id !== pin.versionId
  return (
    <tr className="hover:bg-muted/40">
      <Td>
        <div className="flex flex-col">
          <LinkText mono onClick={() => onOpen()}>
            {i?.key ?? pin.identityId}
          </LinkText>
          <span className="text-xs text-muted-foreground">{i?.displayName}</span>
        </div>
      </Td>
      <Td>{v ? <VersionTag n={v.versionNumber} status={v.status} onClick={() => onOpen(v.id)} /> : '?'}</Td>
      {historical && (
        <Td>
          {now ? (
            <button type="button" onClick={() => onOpen(now.id)} className={cn('rounded-sm font-mono text-xs underline-offset-2 outline-none hover:underline focus-visible:ring-2 focus-visible:ring-ring', changed ? 'text-warning-foreground' : 'text-muted-foreground')}>
              v{now.versionNumber}
            </button>
          ) : (
            <span className="text-xs text-muted-foreground">None</span>
          )}
        </Td>
      )}
      <Td>
        <PinStateCell state={pin.state} />
      </Td>
    </tr>
  )
}

/* ───────────────────────── Compare ───────────────────────── */

function CompareTab({ detail, compareTo, onCompareTo }: { detail: Detail; compareTo: string | null; onCompareTo: (id: string) => void }) {
  const lookup = useLookup()
  const nav = useAppNav()
  const { data: list } = useSetList()
  const thisId = detail.set.id
  const all = list ? [list.active, ...list.historical] : []
  const others = all.filter((s) => s.id !== thisId)
  const fallback = detail.set.status === 'historical' ? list?.active.id : list?.historical[0]?.id
  const otherId = compareTo && others.some((s) => s.id === compareTo) ? compareTo : (fallback ?? null)
  const other = others.find((s) => s.id === otherId)
  const { data: c } = useSetComparison(otherId, thisId)

  const open = (identityId: string, versionId?: string) => {
    const i = lookup.identity(identityId)
    if (i) nav.identity(i, versionId)
  }
  const ver = (identityId: string, versionId: string) => {
    const v = lookup.identityVersion(versionId)
    return v ? <VersionTag n={v.versionNumber} status={v.status} onClick={() => open(identityId, versionId)} /> : <span className="font-mono text-xs">?</span>
  }
  const releaseKey = (id: string) => lookup.release(id)?.key ?? id

  const groups = c
    ? [
        { title: 'Added', tone: DIFF_TONE.added, mark: '+', empty: `Nothing pinned here that ${other?.key} leaves out.`, rows: c.added.map((a) => ({ id: a.identityId, kind: a.kind, body: ver(a.identityId, a.versionId) })) },
        { title: 'Removed', tone: DIFF_TONE.removed, mark: '−', empty: `Everything ${other?.key} pins is pinned here too.`, rows: c.removed.map((r) => ({ id: r.identityId, kind: r.kind, body: ver(r.identityId, r.versionId) })) },
        {
          title: 'Version changes',
          tone: DIFF_TONE.changed,
          mark: '~',
          empty: 'Shared pins point at the same versions.',
          rows: c.changed.map((ch) => ({
            id: ch.identityId,
            kind: ch.kind,
            body: (
              <span className="inline-flex items-center gap-1.5">
                {ver(ch.identityId, ch.fromVersionId)}
                <span aria-label="to" className="text-muted-foreground">
                  →
                </span>
                {ver(ch.identityId, ch.toVersionId)}
              </span>
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
            <label htmlFor="compare-set" className="text-xs text-muted-foreground">
              Compare against
            </label>
            <SimpleSelect
              id="compare-set"
              size="sm"
              className="w-56"
              value={otherId ?? ''}
              onChange={onCompareTo}
              placeholder="Choose a set"
              options={others.map((s) => ({ value: s.id, label: s.status === 'active' ? `${s.key} (active)` : s.key, hint: s.displayName }))}
            />
          </div>
        }
      >
        Identities this set pins differently from the chosen set, and how the releases they read differ. Report-owned identities are left out.
      </TabIntro>
      {!list ? (
        <p className="text-sm text-muted-foreground">Loading sets…</p>
      ) : !otherId ? (
        <EmptyState title="Nothing to compare with">There is no other assumption set yet.</EmptyState>
      ) : !c ? (
        <p className="text-sm text-muted-foreground">Comparing…</p>
      ) : (
        <>
          <p className="text-sm">
            <span className="font-mono">{other?.key}</span> <span className="text-muted-foreground">→</span> <span className="font-mono">{detail.set.key}</span>: {c.changed.length} changed, {c.added.length} added,{' '}
            {c.removed.length} removed, {c.unchanged} unchanged.
          </p>
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
                        <LinkText mono onClick={() => open(r.id)}>
                          {lookup.identity(r.id)?.key ?? r.id}
                        </LinkText>
                        <span className="text-xs text-muted-foreground">{KIND_ONE[r.kind]}</span>
                        <span className="ml-auto">{r.body}</span>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="px-1 text-xs text-muted-foreground">{g.empty}</p>
                )}
              </div>
            ))}
          </div>
          <Section title="Release">
            {c.release ? (
              <>
                <p className="text-sm">
                  <span className="font-mono">{releaseKey(c.release.fromReleaseId)}</span> <span className="text-muted-foreground">→</span> <span className="font-mono">{releaseKey(c.release.toReleaseId)}</span>:{' '}
                  {diffSummary(c.release)}.
                </p>
                <DiffList diff={c.release} />
              </>
            ) : (
              <p className="text-sm text-muted-foreground">Both sets read the same release.</p>
            )}
          </Section>
        </>
      )}
    </div>
  )
}
