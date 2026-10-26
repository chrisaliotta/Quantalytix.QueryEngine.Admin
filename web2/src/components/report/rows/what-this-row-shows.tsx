import { InfoIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { HoverCard, HoverCardContent, HoverCardTrigger } from '@/components/ui/hover-card'
import { dayCountLabel } from '@/lib/model/rules'
import { resolveVariableReportLevel } from '@/lib/report/coverage'
import { leafVariables, metricTree, type MetricNode } from '@/lib/report/metricGraph'
import { resolveForProfile, type VarResolution } from '@/lib/report/profiles'
import { resolveParameter } from '@/lib/report/rowStyle'
import { useEditor } from '@/lib/report/store'
import { useCalcModel } from '@/lib/report/use-calc-model'
import { findRow } from '@/lib/report/tree'
import type { Column } from '@/lib/report/types'
import { cn } from '@/lib/utils'
import { KeyText, SectionLabel, StatusDot } from '../shared/bits'
import { useActiveProfile } from '../shared/profile-switcher'

type Resolve = (variable: string) => VarResolution

const isProblem = (r: VarResolution) => r.status === 'gap' || r.status === 'unresolved'
const dotStatus = (r: VarResolution) => (isProblem(r) ? 'warn' : 'pass') as 'warn' | 'pass'

function ProfileTag() {
  const { key } = useActiveProfile()
  return (
    <span className="text-xs text-muted-foreground">
      as <KeyText className="text-foreground">{key}</KeyText>
    </span>
  )
}

type ColumnCheck = { column: Column; problems: VarResolution[]; direct?: VarResolution }

export function WhatThisRowShows({ rowKey }: { rowKey: string }) {
  const { report } = useEditor()
  const calc = useCalcModel()
  const { key } = useActiveProfile()
  const visible = report.columns.filter((c) => c.visible)
  const hiddenCount = report.columns.length - visible.length
  const resolve: Resolve = (v) => resolveForProfile(report, rowKey, v, key)

  const checks: ColumnCheck[] = visible.map((column) => {
    if (column.kind === 'variable') {
      const r = resolve(column.metric)
      return { column, direct: r, problems: isProblem(r) ? [r] : [] }
    }
    return { column, problems: leafVariables(column.metric, calc).map(resolve).filter(isProblem) }
  })
  const issues = checks.filter((c) => c.problems.length > 0).length

  return (
    <section aria-labelledby="row-shows" className="flex flex-col gap-3">
      <div className="flex items-center justify-between gap-2">
        <SectionLabel id="row-shows">Columns</SectionLabel>
        <ProfileTag />
      </div>
      <p className="flex items-center gap-2">
        <StatusDot status={issues ? 'warn' : 'pass'} />
        <span className={cn(issues && 'text-warning-foreground')}>
          {issues
            ? `${issues} of ${checks.length} ${checks.length === 1 ? 'column needs' : 'columns need'} attention`
            : `All ${checks.length} columns resolve`}
        </span>
      </p>
      <ul className="flex flex-col divide-y rounded-lg border bg-card">
        {checks.map((check) => (
          <ColumnCheckRow key={check.column.key} check={check} resolve={resolve} rowKey={rowKey} />
        ))}
      </ul>
      {hiddenCount > 0 && (
        <p className="text-xs text-muted-foreground">
          {hiddenCount} hidden {hiddenCount === 1 ? 'column' : 'columns'} not checked.
        </p>
      )}
    </section>
  )
}

function ColumnCheckRow({ check, resolve, rowKey }: { check: ColumnCheck; resolve: Resolve; rowKey: string }) {
  const { column, direct, problems } = check
  const hasProblem = problems.length > 0

  return (
    <li className="flex flex-col gap-1 px-3 py-2">
      <div className="flex min-w-0 items-center gap-2">
        <StatusDot status={hasProblem ? 'warn' : 'pass'} />
        <span className="min-w-0 flex-1 truncate font-medium">{column.label}</span>
        {direct ? <SourceSummary r={direct} /> : <MetricChip metric={column.metric} resolve={resolve} />}
      </div>
      {hasProblem && (direct ? <DirectProblem r={direct} rowKey={rowKey} /> : <BlankInputs problems={problems} />)}
      {direct?.status === 'shadowed' && <AlsoSupplied r={direct} />}
    </li>
  )
}

function SourceSummary({ r }: { r: VarResolution }) {
  if (r.status === 'ok' || r.status === 'shadowed') {
    const filtered = (r.binding?.filters.length ?? 0) > 0
    return (
      <span className="flex min-w-0 shrink items-center gap-1.5 text-muted-foreground">
        <span className="truncate">{r.picked?.name}</span>
        {filtered && <span className="shrink-0 rounded bg-muted px-1 text-[11px]">filtered</span>}
      </span>
    )
  }
  if (r.status === 'na') return <span className="shrink-0 text-muted-foreground">Not applicable</span>
  return <span className="shrink-0 text-warning-foreground">{r.status === 'gap' ? 'Gap' : 'Unresolved'}</span>
}

function DirectProblem({ r, rowKey }: { r: VarResolution; rowKey: string }) {
  const { openDialog } = useEditor()
  const canMap = r.status === 'gap' && r.picked?.method === 'mapped'
  return (
    <div className="flex flex-wrap items-center gap-x-2 gap-y-1 pl-4 text-xs">
      <span className="text-pretty text-muted-foreground">{r.reason}</span>
      {canMap && (
        <Button
          variant="link"
          size="xs"
          className="h-auto p-0"
          onClick={() => openDialog({ type: 'mapping', sourceKey: r.picked!.key, rowKey })}
        >
          Map {r.picked!.name}
        </Button>
      )}
    </div>
  )
}

function BlankInputs({ problems }: { problems: VarResolution[] }) {
  return (
    <div className="flex flex-wrap items-center gap-1.5 pl-4 text-xs">
      <span className="text-muted-foreground">Blank inputs</span>
      {problems.map((p) => (
        <span key={p.variable} title={p.reason} className="rounded bg-warning/15 px-1.5 py-0.5 font-mono text-warning-foreground">
          {p.variable}
        </span>
      ))}
    </div>
  )
}

function AlsoSupplied({ r }: { r: VarResolution }) {
  const { openDialog } = useEditor()
  const { key } = useActiveProfile()
  const other = r.others[0]
  if (!other) return null
  return (
    <div className="flex flex-wrap items-center gap-x-2 gap-y-1 pl-4 text-xs text-muted-foreground">
      <InfoIcon className="size-3.5 shrink-0" aria-hidden />
      <span>
        Also mapped: <span className="text-foreground">{other.name}</span>
      </span>
      <Button
        variant="link"
        size="xs"
        className="h-auto p-0"
        onClick={() => openDialog({ type: 'switchPick', profileKey: key, variable: r.variable, sourceKey: other.key })}
      >
        Use instead
      </Button>
    </div>
  )
}

export function MetricChip({ metric, resolve }: { metric: string; resolve: Resolve }) {
  const calc = useCalcModel()
  const measure = calc.measures.find((m) => m.key === metric)
  return (
    <HoverCard>
      <HoverCardTrigger
        delay={150}
        render={
          <button
            type="button"
            className="inline-flex shrink-0 items-center gap-1 rounded border bg-muted/60 px-1.5 py-0.5 font-mono text-xs outline-none hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring/60"
          />
        }
      >
        <span className="text-muted-foreground italic">fx</span>
        {metric}
      </HoverCardTrigger>
      <HoverCardContent align="end" className="w-80">
        <div className="flex flex-col gap-3">
          <div className="flex flex-col gap-0.5">
            <p className="font-medium">{measure?.name ?? metric}</p>
            {measure && <code className="font-mono text-xs break-words text-muted-foreground">{measure.formula}</code>}
          </div>
          <MetricBreakdown node={metricTree(metric, calc)} resolve={resolve} root />
        </div>
      </HoverCardContent>
    </HoverCard>
  )
}

function parametersIn(node: MetricNode, measures: { key: string; formula: string }[], declared: string[]): string[] {
  const found = new Set<string>()
  const walk = (n: MetricNode) => {
    const formula = measures.find((m) => m.key === n.key)?.formula ?? ''
    for (const match of formula.matchAll(/@([A-Za-z0-9_]+)/g)) if (declared.includes(match[1])) found.add(match[1])
    n.children.forEach(walk)
  }
  walk(node)
  return [...found]
}

function shortStatus(r: VarResolution) {
  if (r.status === 'ok') return r.picked?.name
  if (r.status === 'shadowed') return `${r.picked?.name} (also ${r.others[0]?.name})`
  if (r.status === 'na') return 'not applicable'
  return r.status
}

export function MetricBreakdown({ node, resolve, root }: { node: MetricNode; resolve: Resolve; root?: boolean }) {
  const items = root ? node.children : [node]
  return (
    <ul role="tree" aria-label={`Breakdown of ${node.key}`} className="flex flex-col">
      {items.map((child) => (
        <li key={child.key} role="treeitem" aria-selected={false} className="py-0.5">
          <BreakdownNode node={child} resolve={resolve} />
        </li>
      ))}
    </ul>
  )
}

function BreakdownChildren({ nodes, resolve }: { nodes: MetricNode[]; resolve: Resolve }) {
  return (
    <ul role="group" className="ml-1.5 flex flex-col">
      {nodes.map((child) => (
        <li
          key={child.key}
          role="treeitem"
          aria-selected={false}
          className={cn(
            'relative py-0.5 pl-5',
            "before:absolute before:top-0 before:bottom-0 before:left-0 before:w-px before:bg-border before:content-['']",
            'last:before:bottom-auto last:before:h-3',
            "after:absolute after:top-3 after:left-0 after:h-px after:w-3.5 after:bg-border after:content-['']",
          )}
        >
          <BreakdownNode node={child} resolve={resolve} />
        </li>
      ))}
    </ul>
  )
}

function BreakdownNode({ node, resolve }: { node: MetricNode; resolve: Resolve }) {
  const calc = useCalcModel()

  if (node.kind === 'variable') {
    const r = resolve(node.key)
    return (
      <div className="flex flex-col">
        <div className="flex min-h-5 items-center gap-2">
          <StatusDot status={dotStatus(r)} />
          <KeyText className="text-foreground">{node.key}</KeyText>
          <span className={cn('ml-auto truncate text-xs text-muted-foreground', isProblem(r) && 'text-warning-foreground')}>
            {shortStatus(r)}
          </span>
        </div>
        {isProblem(r) && <p className="pl-4 text-xs text-pretty text-muted-foreground">{r.reason}</p>}
      </div>
    )
  }

  if (node.kind === 'unknown') {
    return (
      <div className="flex min-h-5 items-center gap-2">
        <StatusDot status="fail" />
        <KeyText className="text-destructive">{node.key}</KeyText>
        <span className="ml-auto text-xs text-muted-foreground">unknown metric</span>
      </div>
    )
  }

  const formula = calc.measures.find((m) => m.key === node.key)?.formula
  return (
    <div className="flex flex-col">
      <div className="flex min-h-5 min-w-0 items-center gap-2">
        <span className="w-3 shrink-0 text-center font-mono text-xs text-muted-foreground italic">fx</span>
        <KeyText className="shrink-0 font-medium text-foreground">{node.key}</KeyText>
        {formula && <code className="min-w-0 truncate font-mono text-xs text-muted-foreground">= {formula}</code>}
      </div>
      {node.children.length > 0 && <BreakdownChildren nodes={node.children} resolve={resolve} />}
    </div>
  )
}

export function FormulaRowShows({ rowKey }: { rowKey: string }) {
  const { report, setUi } = useEditor()
  const calc = useCalcModel()
  const { key } = useActiveProfile()
  const row = findRow(report.rows, rowKey)
  const column = report.columns.find((c) => c.key === row?.columnKey)
  const tree = row?.measureKey ? metricTree(row.measureKey, calc) : null
  const usedParams = tree ? parametersIn(tree, calc.measures, calc.parameters.map((p) => p.key)) : []
  const resolve: Resolve = (v) => {
    const s = resolveVariableReportLevel(report, v, key)
    return s.ok
      ? { variable: v, status: 'ok', picked: s.source, binding: s.binding, others: [] }
      : { variable: v, status: 'unresolved', reason: s.reason, others: [] }
  }

  return (
    <section aria-labelledby="row-shows" className="flex flex-col gap-3">
      <div className="flex items-center justify-between gap-2">
        <SectionLabel id="row-shows">What this row will show</SectionLabel>
        <ProfileTag />
      </div>
      {row?.measureKey && column && tree ? (
        <div className="flex flex-col gap-3 rounded-lg border bg-card p-3">
          <p className="text-pretty">
            This row shows <MetricChip metric={row.measureKey} resolve={resolve} /> in column{' '}
            <span className="font-medium">{column.label}</span>
            <KeyText className="ml-1.5">{column.key}</KeyText>, using report-level totals.
          </p>
          <MetricBreakdown node={tree} resolve={resolve} />
          {usedParams.length > 0 && (
            <ul className="flex flex-col gap-1 border-t pt-3">
              {calc.parameters
                .filter((p) => usedParams.includes(p.key))
                .map((p) => {
                  const r = resolveParameter(report.rows, rowKey, p)
                  return (
                    <li key={p.key} className="flex items-baseline gap-2">
                      <KeyText className="text-foreground">@{p.key}</KeyText>
                      <span className="text-xs">{r.value == null ? '—' : dayCountLabel(String(r.value))}</span>
                      <span className="ml-auto text-xs text-muted-foreground">
                        {r.source ? (
                          <>
                            from <KeyText>{r.source}</KeyText>
                          </>
                        ) : (
                          'model default'
                        )}
                      </span>
                    </li>
                  )
                })}
            </ul>
          )}
          <p className="text-xs text-muted-foreground">
            Variables resolve across the whole report through {key}&apos;s picks, not through this row&apos;s mappings.
          </p>
        </div>
      ) : (
        <div className="flex flex-col items-start gap-2 rounded-lg border border-dashed p-3">
          <p className="text-pretty text-muted-foreground">Pick a calculated measure and a column for this row to display.</p>
          <button
            type="button"
            className="font-medium text-primary underline-offset-4 hover:underline"
            onClick={() => setUi({ rowPanelTab: 'properties' })}
          >
            Set up formula…
          </button>
        </div>
      )}
    </section>
  )
}
