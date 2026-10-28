import { methodLabel, sourceScenarios } from '@/lib/report/scenarios'

import { FilterIcon, MoreHorizontalIcon, PlusIcon, Trash2Icon } from 'lucide-react'
import { toast } from 'sonner'
import { useUndoAction } from '@/components/shell/history'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { sourceCoverage } from '@/lib/report/coverage'
import { uid, useEditor } from '@/lib/report/store'
import { useCalcModel } from '@/lib/report/use-calc-model'
import type { Source } from '@/lib/report/types'
import { cn } from '@/lib/utils'
import { KeyText, WARN_CHIP } from '../shared/bits'
import { filterSummary } from '../shared/filter-builder'
import { SearchCombobox } from '../shared/search-combobox'
import { SimpleSelect } from '../shared/simple-select'

interface SourceCardProps {
  source: Source
  onEditFilter: (bindingId: string) => void
  onOpenProfiles: (variable: string) => void
}

export function SourceCard({ source, onEditFilter, onOpenProfiles }: SourceCardProps) {
  const { report, mutate, openDialog } = useEditor()
  const undoAction = useUndoAction()
  const calc = useCalcModel()
  const dataset = report.datasets.find((d) => d.key === source.dataset)
  const coverage = source.method === 'mapped' ? sourceCoverage(source, report) : null
  const gaps = coverage?.unmapped.length ?? 0
  const supplierCount = (variable: string) => report.sources.filter((s) => s.bindings.some((b) => b.variable === variable)).length

  const editSource = (fn: (s: Source) => void) =>
    mutate((d) => {
      const s = d.sources.find((x) => x.key === source.key)
      if (s) fn(s)
    })

  const removeSource = () => {
    mutate((d) => {
      d.sources = d.sources.filter((s) => s.key !== source.key)
    })
    toast(`Removed "${source.name}"`, { action: undoAction() })
  }

  const unbound = calc.variables.filter((v) => !source.bindings.some((b) => b.variable === v.key))

  return (
    <article className="flex flex-col rounded-lg border bg-card" aria-labelledby={`src-${source.key}`}>
      <header className="flex flex-wrap items-start gap-x-3 gap-y-2 border-b px-4 py-3">
        <div className="flex min-w-0 flex-1 flex-col gap-0.5">
          <div className="flex flex-wrap items-center gap-2">
            <h3 id={`src-${source.key}`} className="font-semibold">
              {source.name}
            </h3>
            <Badge variant="outline" className="font-normal text-muted-foreground">
              {methodLabel(source)}
            </Badge>
            {sourceScenarios(report, source).map((sc) => (
              <Badge key={sc.role} variant="outline" className="font-normal" title={`Reads one ${sc.name.toLowerCase()} per run`}>
                {sc.name}
              </Badge>
            ))}
            {coverage && (
              <button
                type="button"
                onClick={() => openDialog({ type: 'coverage', sourceKey: source.key })}
                className="rounded-md outline-none focus-visible:ring-2 focus-visible:ring-ring/60"
                aria-label={`${gaps} unmapped accounts, review coverage`}
              >
                <Badge variant="outline" className={cn('tabular-nums', gaps > 0 ? WARN_CHIP : 'font-normal text-muted-foreground')}>
                  {gaps} unmapped
                </Badge>
              </button>
            )}
          </div>
          <p className="flex flex-wrap items-center gap-x-3 text-muted-foreground">
            <KeyText>{source.dataset}</KeyText>
            <span>
              Time field <KeyText className="text-foreground">{source.timeField}</KeyText>
            </span>
          </p>
        </div>
        <DropdownMenu>
          <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label={`${source.name} actions`} />}>
            <MoreHorizontalIcon />
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-48">
            {source.method === 'mapped' && (
              <DropdownMenuItem onClick={() => openDialog({ type: 'coverage', sourceKey: source.key })}>Review coverage…</DropdownMenuItem>
            )}
            <DropdownMenuSeparator />
            <DropdownMenuItem variant="destructive" onClick={removeSource}>
              <Trash2Icon /> Remove source
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </header>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="w-[34%] pl-4">Variable</TableHead>
            <TableHead className="w-[26%]">Measure</TableHead>
            <TableHead>Filter</TableHead>
            <TableHead className="w-12 pr-4">
              <span className="sr-only">Remove</span>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {source.bindings.map((b) => (
            <TableRow key={b.id}>
              <TableCell className="pl-4">
                <div className="flex items-center gap-2">
                  <KeyText className="text-foreground">{b.variable}</KeyText>
                  {supplierCount(b.variable) > 1 && (
                    <button
                      type="button"
                      onClick={() => onOpenProfiles(b.variable)}
                      className="rounded-md outline-none focus-visible:ring-2 focus-visible:ring-ring/60"
                    >
                      <Badge variant="secondary" className="font-normal">
                        Profiles
                      </Badge>
                      <span className="sr-only"> for {b.variable}</span>
                    </button>
                  )}
                </div>
              </TableCell>
              <TableCell>
                <SearchCombobox
                  value={b.measure}
                  onChange={(measure) =>
                    editSource((s) => {
                      const x = s.bindings.find((y) => y.id === b.id)
                      if (x) x.measure = measure
                    })
                  }
                  options={(dataset?.measures ?? []).map((m) => ({ value: m, label: m }))}
                  className="h-8 w-full font-mono"
                />
              </TableCell>
              <TableCell>
                {b.filters.length ? (
                  <button
                    type="button"
                    onClick={() => onEditFilter(b.id)}
                    className="inline-flex max-w-full items-center gap-1.5 rounded-md border bg-muted/50 px-2 py-1 font-mono text-xs outline-none hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring/60"
                    aria-label={`Edit filter on ${b.variable}`}
                  >
                    <FilterIcon className="size-3 shrink-0 text-muted-foreground" aria-hidden />
                    <span className="truncate">{b.filters.map(filterSummary).join(' and ')}</span>
                  </button>
                ) : (
                  <Button variant="ghost" size="xs" className="text-muted-foreground" onClick={() => onEditFilter(b.id)}>
                    <PlusIcon /> Add filter<span className="sr-only"> on {b.variable}</span>
                  </Button>
                )}
              </TableCell>
              <TableCell className="pr-4">
                <Button
                  variant="ghost"
                  size="icon-sm"
                  aria-label={`Remove ${b.variable} binding`}
                  onClick={() => editSource((s) => (s.bindings = s.bindings.filter((x) => x.id !== b.id)))}
                >
                  <Trash2Icon />
                </Button>
              </TableCell>
            </TableRow>
          ))}
          {source.bindings.length === 0 && (
            <TableRow>
              <TableCell colSpan={4} className="py-4 pl-4 text-muted-foreground">
                No variables yet. This source contributes nothing until one is bound.
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>

      {unbound.length > 0 && (
        <footer className="flex items-center gap-2 border-t px-4 py-2">
          <SimpleSelect
            size="sm"
            value=""
            placeholder="Supply a variable…"
            aria-label={`Add a variable to ${source.name}`}
            className="w-56"
            options={unbound.map((v) => ({ value: v.key, label: `${v.key}  ${v.name}` }))}
            onChange={(variable) =>
              editSource((s) => {
                s.bindings.push({ id: uid('b'), variable, measure: dataset?.measures[0] ?? '', filters: [] })
              })
            }
          />
        </footer>
      )}
    </article>
  )
}
