import { EyeOffIcon, FileTextIcon, MoreHorizontalIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { useHiddenDimensions } from '@/lib/dimensions/client'
import type { DimensionListItem, OwnerReport } from '@/lib/dimensions/contract'
import { useEditor } from '@/lib/report/store'
import { useAppNav } from '@/lib/navigation'
import { EmptyState, KindBadge, Mono, StatusBadge, Td, Th, formatDate } from './shared'

/** System-admin view of report-owned dimensions. Each row leads to its report, not to the dimension editor. */
export function HiddenDimensions({ query, onInspect }: { query: string; onInspect: (id: string) => void }) {
  const { data, isLoading, error } = useHiddenDimensions(true)
  const q = query.trim().toLowerCase()
  const rows = (data ?? []).filter(
    (d) => !q || d.key.toLowerCase().includes(q) || d.displayName.toLowerCase().includes(q) || d.ownerReport?.reportName.toLowerCase().includes(q),
  )

  if (error) return <EmptyState title="Not available">{error.message}</EmptyState>
  if (!isLoading && !rows.length)
    return <EmptyState title={q ? 'No hidden dimensions match' : 'No hidden dimensions'}>Each report creates one when its Rows tree is defined.</EmptyState>

  return (
    <div className="flex flex-col gap-3">
      <p className="flex items-start gap-2 text-sm text-pretty text-muted-foreground">
        <EyeOffIcon aria-hidden className="mt-0.5 size-4 shrink-0" />
        {
          "Every report's Rows tree is stored as a discrete hierarchical dimension bound to that one report. These never appear in pickers or the dimension lists; edit them from the report's Rows page."
        }
      </p>
      <div className="overflow-x-auto rounded-lg border bg-card">
        <table className="w-full text-sm">
          <thead className="border-b bg-muted/40">
            <tr>
              <Th>Report</Th>
              <Th>Dimension key</Th>
              <Th>Kind</Th>
              <Th className="text-right">Members</Th>
              <Th>Published</Th>
              <Th>Frozen</Th>
              <Th className="w-10">
                <span className="sr-only">Actions</span>
              </Th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {isLoading && (
              <tr>
                <Td colSpan={7} className="py-8 text-center text-muted-foreground">
                  Loading hidden dimensions…
                </Td>
              </tr>
            )}
            {rows.map((d) => (
              <HiddenRow key={d.id} d={d} onInspect={() => onInspect(d.id)} />
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

function HiddenRow({ d, onInspect }: { d: DimensionListItem; onInspect: () => void }) {
  const published = d.versions.find((v) => v.status === 'published')
  const frozen = d.versions.filter((v) => v.status === 'retired').length
  return (
    <tr className="hover:bg-muted/30">
      <Td>{d.ownerReport ? <ReportLink owner={d.ownerReport} /> : <span className="text-muted-foreground">Unbound</span>}</Td>
      <Td>
        <Mono>{d.key}</Mono>
      </Td>
      <Td>
        <KindBadge kind={d.kind} />
      </Td>
      <Td className="text-right tabular-nums">{d.publishedMemberCount ?? '—'}</Td>
      <Td>{published ? <span className="whitespace-nowrap">v{published.versionNumber}{published.publishedAt ? ` · ${formatDate(published.publishedAt)}` : ''}</span> : '—'}</Td>
      <Td className="tabular-nums text-muted-foreground">{frozen || '—'}</Td>
      <Td>
        <DropdownMenu>
          <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label={`Actions for ${d.key}`} />}>
            <MoreHorizontalIcon />
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem onClick={onInspect}>Inspect as dimension</DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </Td>
    </tr>
  )
}

export function ReportLink({ owner }: { owner: OwnerReport }) {
  const { library } = useEditor()
  const nav = useAppNav()
  const item = library.find((i) => i.report.settings.key === owner.reportKey)
  if (!item)
    return (
      <span className="flex flex-col">
        <span>{owner.reportName}</span>
        <Mono>{owner.reportKey}</Mono>
      </span>
    )
  return (
    <Button variant="link" size="sm" className="h-auto justify-start gap-1.5 p-0" onClick={() => nav.report(item.id)}>
      <FileTextIcon aria-hidden className="size-3.5" />
      {item.report.settings.name || owner.reportName}
      <span className="sr-only"> (open in report editor)</span>
    </Button>
  )
}
