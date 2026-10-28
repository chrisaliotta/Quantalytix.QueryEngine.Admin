import { methodLabel } from '@/lib/report/scenarios'

import { AlertTriangleIcon, DatabaseIcon, MoreHorizontalIcon } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { MappingTable } from './mapping-table'
import { ReportBridgeLine } from './report-bridge-line'
import { sourceCoverage, sourceSuppliesOnRow } from '@/lib/report/coverage'
import { pickedSource, variableUsage } from '@/lib/report/profiles'
import { useEditor } from '@/lib/report/store'
import { useActiveProfile } from '../shared/profile-switcher'
import type { Source } from '@/lib/report/types'
import { cn } from '@/lib/utils'
import { KeyText, WARN_CHIP } from '../shared/bits'

export function RowSourceCard({ source, rowKey, dimmed = false }: { source: Source; rowKey: string; dimmed?: boolean }) {
  const { report, ui, openDialog, mapping, setUi } = useEditor()
  const { key: profileKey, profile, dimming } = useActiveProfile()
  const coverage = sourceCoverage(source, report)
  const gaps = coverage.unmapped.length
  const used = variableUsage(report)
  const switchable = dimmed ? source.bindings.filter((b) => used.has(b.variable)) : []

  return (
    <article className={cn('flex flex-col rounded-lg border bg-card', dimmed && 'border-dashed bg-transparent')} aria-label={source.name}>
      <header className="flex items-start gap-2 px-3 pt-2.5 pb-2">
        <div className="flex min-w-0 flex-1 flex-col">
          <div className="flex items-center gap-2">
            <DatabaseIcon className="size-4 shrink-0 text-muted-foreground" aria-hidden />
            <h4 className="truncate font-medium">{source.name}</h4>
            <Badge variant="outline" className="font-normal text-muted-foreground">
              {methodLabel(source)}
            </Badge>
          </div>
          {ui.advanced && <KeyText>{source.dataset}</KeyText>}
        </div>
        <DropdownMenu>
          <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label={`${source.name} actions`} />}>
            <MoreHorizontalIcon />
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-48">
            {source.method === 'mapped' ? (
              <>
                <DropdownMenuItem onClick={() => openDialog({ type: 'mapping', sourceKey: source.key, rowKey })}>Edit mappings…</DropdownMenuItem>
                <DropdownMenuItem onClick={() => openDialog({ type: 'coverage', sourceKey: source.key })}>Review coverage…</DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem variant="destructive" onClick={() => mapping.unmapRow(source.key, rowKey)}>
                  Unmap from this row
                </DropdownMenuItem>
              </>
            ) : (
              <DropdownMenuItem onClick={() => setUi({ tab: 'data' })}>Open in Data</DropdownMenuItem>
            )}
          </DropdownMenuContent>
        </DropdownMenu>
      </header>

      <div className={cn('flex flex-col gap-2 px-3 pb-3', dimmed && 'opacity-60')}>
        {source.method === 'mapped' ? (
          <>
            <MappingTable source={source} rowKey={rowKey} advanced={ui.advanced} />
            <ReportBridgeLine sourceKey={source.key} />
          </>
        ) : (
          <p className="text-muted-foreground">
            rows where <KeyText className="text-foreground">{source.rowKeyField}</KeyText> ={' '}
            <KeyText className="text-foreground">{rowKey}</KeyText>
          </p>
        )}
      </div>

      {(ui.advanced || switchable.length > 0) && (
      <footer className="flex flex-col gap-2 border-t px-3 py-2">
        {ui.advanced && (
        <div className="flex flex-wrap items-center gap-1.5">
          <span className="text-xs text-muted-foreground">Supplies</span>
          {source.bindings.map((b) => {
            const supply = sourceSuppliesOnRow(source, b.variable, rowKey, report)
            const picked = pickedSource(report, profile, b.variable)
            const readHere = !dimming || picked?.key === source.key
            const readElsewhere = dimming && picked && picked.key !== source.key ? picked : undefined
            const chip = (
              <span
                className={cn(
                  'inline-flex items-center gap-1 rounded border px-1.5 py-0.5 font-mono text-xs',
                  supply.ok ? 'bg-muted/60' : 'border-dashed text-muted-foreground',
                  !readHere && 'border-dashed bg-transparent font-normal text-muted-foreground',
                  readHere && supply.ok && 'font-medium',
                )}
              >
                {b.variable}
                <span className="font-normal text-muted-foreground">{'<-'} {b.measure}</span>
                {b.filters.length > 0 && <span className="text-muted-foreground">(filtered)</span>}
                {readElsewhere && (
                  <span className="font-sans font-normal text-muted-foreground">· read from {readElsewhere.name}</span>
                )}
              </span>
            )
            return supply.ok ? (
              <span key={b.id}>{chip}</span>
            ) : (
              <Tooltip key={b.id}>
                <TooltipTrigger render={<span tabIndex={0} className="rounded outline-none focus-visible:ring-2 focus-visible:ring-ring/60" />}>
                  {chip}
                </TooltipTrigger>
                <TooltipContent>Not on this row: {supply.reason}</TooltipContent>
              </Tooltip>
            )
          })}
        </div>
        )}
        {switchable.length > 0 && (
          <div className="flex flex-wrap gap-1.5">
            {switchable.map((b) => (
              <Button
                key={b.id}
                variant="outline"
                size="xs"
                onClick={() => openDialog({ type: 'switchPick', profileKey, variable: b.variable, sourceKey: source.key })}
              >
                Use for <span className="font-mono">{b.variable}</span> in <span className="font-mono">{profileKey}</span>
              </Button>
            ))}
          </div>
        )}
        {ui.advanced && gaps > 0 && !dimmed && (
          <div className={cn('flex items-center gap-2 rounded-md border px-2 py-1.5 text-xs', WARN_CHIP)}>
            <AlertTriangleIcon className="size-3.5 shrink-0" aria-hidden />
            <span className="flex-1">
              {gaps} {source.method === 'mapped' ? (gaps === 1 ? 'account' : 'accounts') : gaps === 1 ? 'value' : 'values'} not mapped to any row
            </span>
            {source.method === 'mapped' && (
              <Button variant="link" size="xs" className="h-auto p-0 text-warning-foreground" onClick={() => openDialog({ type: 'coverage', sourceKey: source.key })}>
                Review
              </Button>
            )}
          </div>
        )}
      </footer>
      )}
    </article>
  )
}
