import { MoreHorizontalIcon, PlusIcon, SearchIcon, ShieldCheckIcon } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { ListPage } from '@/components/shell/page-shell'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { Segmented } from '@/components/report/shared/segmented'
import { useDimensionList, useDimensionsApi } from '@/lib/dimensions/client'
import { useAppNav, useSearchParam } from '@/lib/navigation'
import { KIND_LABEL, type DimensionKind, type DimensionListItem, type DefinitionOrigin } from '@/lib/dimensions/contract'
import { useMappingCountsByDimension, type DimensionMappingCounts } from '@/lib/mappings/client'
import { canHaveBridges, canHaveMappingSets } from '@/lib/mappings/contract'
import { cn } from '@/lib/utils'
import { CreateDimensionDialog } from './create-dimension-dialog'
import { DimensionDetail } from './dimension-detail'
import { DeleteDialog, EmptyState, KindBadge, Mono, PolicyBadge, StatusBadge, Td, Th, formatDate, useDimCall } from './shared'

export function DimensionsScreen({ dimensionId }: { dimensionId: string | null }) {
  const nav = useAppNav()
  if (dimensionId) return <DimensionDetail dimensionId={dimensionId} onBack={nav.dimensions} />
  return <DimensionList />
}

const TABS: DefinitionOrigin[] = ['user', 'system']
const TAB_LABEL: Record<DefinitionOrigin, string> = { user: 'User dimensions', system: 'System dimensions' }

type KindFilter = 'all' | DimensionKind
type StatusFilter = 'any' | 'frozen' | 'disabled'

function DimensionList() {
  const [listParam, setListParam] = useSearchParam('dimList')
  const origin: DefinitionOrigin = listParam === 'system' ? 'system' : 'user'
  const { data, isLoading } = useDimensionList(origin)
  const [query, setQuery] = useState('')
  const [kind, setKind] = useState<KindFilter>('all')
  const [status, setStatus] = useState<StatusFilter>('any')
  const [creating, setCreating] = useState(false)

  const q = query.trim().toLowerCase()
  const rows = (data ?? []).filter((d) => {
    if (q && !d.key.toLowerCase().includes(q) && !d.displayName.toLowerCase().includes(q)) return false
    if (kind !== 'all' && d.kind !== kind) return false
    if (status === 'frozen' && !d.versions.some((v) => v.status === 'retired')) return false
    if (status === 'disabled' && d.isEnabled) return false
    return true
  })
  const filtered = !!q || kind !== 'all' || status !== 'any'

  return (
    <ListPage
      title="Dimensions"
      description="Anything a query can group or filter by: branches, products, risk grades, balance bands, dates, plan scenarios. Define members, levels, range bands and roles, versioned and published."
      actions={
        <>
          {origin === 'user' && (
            <Button size="sm" onClick={() => setCreating(true)}>
              <PlusIcon /> New dimension
            </Button>
          )}
        </>
      }
    >

        <div role="tablist" aria-label="Dimension origin" className="flex gap-4 border-b">
          {TABS.map((o) => (
            <button
              key={o}
              role="tab"
              type="button"
              aria-selected={origin === o}
              onClick={() => setListParam(o === 'user' ? null : o)}
              className={cn(
                '-mb-px inline-flex items-center gap-1.5 border-b-2 px-0.5 pb-2 text-sm transition-colors',
                origin === o ? 'border-primary font-medium text-foreground' : 'border-transparent text-muted-foreground hover:text-foreground',
              )}
            >
              {o === 'system' && <ShieldCheckIcon aria-hidden className="size-3.5" />}
              {TAB_LABEL[o]}
            </button>
          ))}
        </div>

        <div className="flex flex-wrap items-center gap-3">
          <div className="relative w-full max-w-xs">
            <SearchIcon aria-hidden className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Search key or name" aria-label="Search dimensions" className="pl-8" />
          </div>
          <Segmented<KindFilter>
            aria-label="Kind"
            value={kind}
            onChange={setKind}
            options={[{ value: 'all', label: 'All' }, ...(Object.keys(KIND_LABEL) as DimensionKind[]).map((k) => ({ value: k, label: KIND_LABEL[k] }))]}
          />
          <Segmented<StatusFilter>
            aria-label="Status"
            value={status}
            onChange={setStatus}
            options={[
              { value: 'any', label: 'Any status' },
              { value: 'frozen', label: 'Has frozen versions' },
              { value: 'disabled', label: 'Disabled' },
            ]}
          />
        </div>

        {!isLoading && !rows.length ? (
          filtered ? (
            <EmptyState title="No dimensions match">Clear the search or filters to see every {origin} dimension.</EmptyState>
          ) : origin === 'user' ? (
            <EmptyState
              title="No user dimensions yet"
              action={
                <Button onClick={() => setCreating(true)}>
                  <PlusIcon /> New dimension
                </Button>
              }
            >
              User dimensions are yours to define: categorical lists, hierarchies, range bands or time axes.
            </EmptyState>
          ) : (
            <EmptyState title="No system dimensions">System dimensions are provided by product modules and appear here automatically.</EmptyState>
          )
        ) : (
          <DimensionTable rows={rows} origin={origin} loading={isLoading} />
        )}
      <CreateDimensionDialog open={creating} onOpenChange={setCreating} />
    </ListPage>
  )
}

function DimensionTable({ rows, origin, loading }: { rows: DimensionListItem[]; origin: DefinitionOrigin; loading: boolean }) {
  const system = origin === 'system'
  const cols = system ? 14 : 11
  const { data: counts } = useMappingCountsByDimension()
  return (
    <div className="overflow-x-auto rounded-lg border bg-card">
      <table className="w-full text-sm">
        <thead className="border-b bg-muted/40">
          <tr>
            <Th>Name</Th>
            {system && <Th>Type</Th>}
            {system && <Th>Required by</Th>}
            {system && <Th>Policy</Th>}
            <Th>Kind</Th>
            <Th className="text-right">Roles</Th>
            <Th className="text-right">Members</Th>
            <Th className="text-right">Mappings</Th>
            <Th className="text-right">Bridges</Th>
            <Th className="text-right">Report bridges</Th>
            <Th>Published</Th>
            <Th>Frozen</Th>
            <Th>Enabled</Th>
            <Th className="w-10">
              <span className="sr-only">Actions</span>
            </Th>
          </tr>
        </thead>
        <tbody className="divide-y">
          {loading && (
            <tr>
              <Td colSpan={cols} className="py-8 text-center text-muted-foreground">
                Loading dimensions…
              </Td>
            </tr>
          )}
          {rows.map((d) => (
            <DimensionRow key={d.id} d={d} system={system} counts={counts ? (counts.get(d.id) ?? NO_COUNTS) : undefined} />
          ))}
        </tbody>
      </table>
    </div>
  )
}

const NO_COUNTS: DimensionMappingCounts = { mappingSets: 0, bridges: 0, reportBridges: 0 }

function CountCell({ value, tab, dimId, label }: { value: number | undefined; tab: string; dimId: string; label: string }) {
  const nav = useAppNav()
  if (!value) return <Td className="text-right tabular-nums text-muted-foreground">{value === undefined ? '—' : 0}</Td>
  return (
    <Td className="text-right tabular-nums">
      <button
        type="button"
        onClick={(e) => {
          e.stopPropagation()
          nav.dimension(dimId, { tab })
        }}
        className="underline decoration-dotted underline-offset-4 hover:text-primary"
      >
        {value}
        <span className="sr-only"> {label}, open tab</span>
      </button>
    </Td>
  )
}

function DimensionRow({ d, system, counts }: { d: DimensionListItem; system: boolean; counts: DimensionMappingCounts | undefined }) {
  const nav = useAppNav()
  const published = d.versions.find((v) => v.status === 'published')
  const frozen = d.versions.filter((v) => v.status === 'retired').length
  const open = () => nav.dimension(d.id)
  const mappable = canHaveMappingSets(d)
  const bridgeable = canHaveBridges(d)
  return (
    <tr className="cursor-pointer hover:bg-muted/40" onClick={open}>
      <Td>
        <button
          type="button"
          onClick={(e) => {
            e.stopPropagation()
            open()
          }}
          className="flex flex-col text-left"
        >
          <span className="font-medium hover:underline">{d.displayName}</span>
          <Mono>{d.key}</Mono>
        </button>
      </Td>
      {system && (
        <Td>
          <span className="inline-flex items-center gap-1.5 whitespace-nowrap">
            <ShieldCheckIcon aria-hidden className="size-3.5 text-primary" />
            {d.profile?.displayName}
          </span>
        </Td>
      )}
      {system && <Td className="whitespace-nowrap">{d.profile?.owner}</Td>}
      {system && <Td>{d.profile && <PolicyBadge policy={d.profile.editPolicy} />}</Td>}
      <Td>
        <KindBadge kind={d.kind} />
      </Td>
      <Td className="text-right tabular-nums">
        {d.roles.length ? (
          <Tooltip>
            <TooltipTrigger render={<span className="cursor-default underline decoration-dotted underline-offset-4" onClick={(e) => e.stopPropagation()} />}>{d.roles.length}</TooltipTrigger>
            <TooltipContent>
              <span className="font-mono text-xs">{d.roles.map((r) => r.key).join(', ')}</span>
            </TooltipContent>
          </Tooltip>
        ) : (
          <span className="text-muted-foreground">0</span>
        )}
      </Td>
      <Td className="text-right tabular-nums">{d.publishedMemberCount ?? <span className="text-muted-foreground">—</span>}</Td>
      <CountCell dimId={d.id} tab="mappings" label="mapping sets" value={mappable ? counts?.mappingSets : undefined} />
      <CountCell dimId={d.id} tab="bridges" label="bridges" value={bridgeable ? counts?.bridges : undefined} />
      <CountCell dimId={d.id} tab="bridges" label="report bridges" value={bridgeable ? counts?.reportBridges : undefined} />
      <Td className="whitespace-nowrap">
        {published ? (
          <span className="text-xs">
            <span className="font-mono">v{published.versionNumber}</span> <span className="text-muted-foreground">· {formatDate(published.publishedAt)}</span>
          </span>
        ) : (
          <span className="text-muted-foreground">—</span>
        )}
      </Td>
      <Td className="tabular-nums text-muted-foreground">{frozen ? frozen : '—'}</Td>
      <Td>
        <span className="inline-flex items-center gap-1.5 text-xs">
          <span aria-hidden className={cn('size-2 rounded-full', d.isEnabled ? 'bg-success' : 'bg-muted-foreground/40')} />
          <span className="sr-only">{d.isEnabled ? 'Enabled' : 'Disabled'}</span>
        </span>
      </Td>
      <Td className="text-right">
        <span onClick={(e) => e.stopPropagation()}>
          <RowActions d={d} system={system} />
        </span>
      </Td>
    </tr>
  )
}

function RowActions({ d, system }: { d: DimensionListItem; system: boolean }) {
  const nav = useAppNav()
  const api = useDimensionsApi()
  const { run } = useDimCall()
  const [deleting, setDeleting] = useState(false)
  const locked = d.profile?.editPolicy === 'locked'

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label={`${d.displayName} actions`} />}>
          <MoreHorizontalIcon />
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-48">
          <DropdownMenuItem onClick={() => nav.dimension(d.id)}>Open</DropdownMenuItem>
          {!locked && (
            <DropdownMenuItem onClick={() => run(() => api.updateDimension(d.id, { isEnabled: !d.isEnabled }), `${d.displayName} ${d.isEnabled ? 'disabled' : 'enabled'}`)}>
              {d.isEnabled ? 'Disable' : 'Enable'}
            </DropdownMenuItem>
          )}
          {!system && (
            <>
              <DropdownMenuSeparator />
              <DropdownMenuItem variant="destructive" onClick={() => setDeleting(true)}>
                Delete
              </DropdownMenuItem>
            </>
          )}
        </DropdownMenuContent>
      </DropdownMenu>
      {!system && (
        <DeleteDialog
          target={deleting ? { type: 'dimension', dimensionId: d.id } : null}
          title={`Delete ${d.displayName}?`}
          onOpenChange={setDeleting}
          onConfirm={async () => (await run(() => api.deleteDimension(d.id), `Deleted ${d.displayName}`)).ok}
        />
      )}
    </>
  )
}

