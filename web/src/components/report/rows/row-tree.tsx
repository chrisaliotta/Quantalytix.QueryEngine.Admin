import { ArrowDownIcon, ArrowUpIcon, ChevronDownIcon, ChevronRightIcon, EyeOffIcon, IndentIcon, OutdentIcon, PlusIcon, Trash2Icon } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import type { ReportRow, ReportRowKind, Violation } from '@/lib/reports/contract'
import { cn } from '@/lib/utils'
import { useReportEditor } from '../editor-state'
import { KIND_LABEL, KeyText, KindGlyph, Refusal } from '../shared/bits'
import { childrenOf, depthOf, indexOf, isMemberKind, newRow } from './row-model'

/** The row tree in hierarchy order, with the commands that add, move, indent and delete rows. */
export function RowTree() {
  const { reportId, report, model, ui, setUi, run } = useReportEditor()
  const [refusal, setRefusal] = useState<Violation[] | null>(null)
  const selected = report.rows.find((r) => r.key === ui.selectedRowKey) ?? report.rows[0]
  const hidden = (row: ReportRow) => {
    for (let parent = row.parentKey; parent !== null; parent = report.rows.find((r) => r.key === parent)!.parentKey)
      if (ui.collapsed.includes(parent)) return true
    return false
  }
  const edit = async (call: Parameters<typeof run>[0]) => setRefusal(await run(call))
  const move = (row: ReportRow, parentKey: string | null, index: number) => edit((api) => api.moveRow(reportId, row.key, { parentKey, index }))

  /** Rows a new row of a kind may sit under: members under members, nothing under a formula row. */
  const add = async (kind: ReportRowKind) => {
    const parent = isMemberKind(kind) && !isMemberKind(selected.kind) ? report.rows.find((r) => r.key === selected.parentKey)! : selected
    const under = parent.kind === 'formula' ? report.rows.find((r) => r.key === parent.parentKey)! : parent
    const row = newRow(report, kind, under.key, model?.measures[0]?.key ?? null)
    const violations = await run((api) => api.saveRow(reportId, { ...row, index: childrenOf(report, under.key).length }))
    setRefusal(violations)
    if (!violations) setUi({ selectedRowKey: row.key })
  }

  const siblings = childrenOf(report, selected.parentKey)
  const at = indexOf(report, selected)
  const previous = siblings[at - 1]
  const parent = report.rows.find((r) => r.key === selected.parentKey)
  const canIndent = !!previous && previous.kind !== 'formula' && (!isMemberKind(selected.kind) || isMemberKind(previous.kind))
  const canOutdent = !!parent && parent.parentKey !== null && (!isMemberKind(selected.kind) || isMemberKind(parent.kind))

  return (
    <div className="flex flex-col">
      <div className="sticky top-0 z-10 flex flex-wrap items-center gap-1 border-b bg-card px-3 py-2">
        <DropdownMenu>
          <DropdownMenuTrigger render={<Button size="sm" variant="outline" />}>
            <PlusIcon /> Add row
          </DropdownMenuTrigger>
          <DropdownMenuContent align="start">
            {(['data', 'group', 'section', 'formula'] as const).map((kind) => (
              <DropdownMenuItem key={kind} onClick={() => void add(kind)}>
                <KindGlyph kind={kind} /> {KIND_LABEL[kind]}
              </DropdownMenuItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>
        <span className="ml-auto flex items-center gap-0.5">
          <Button size="icon-sm" variant="ghost" aria-label="Move up" disabled={at <= 0} onClick={() => void move(selected, selected.parentKey, at - 1)}>
            <ArrowUpIcon />
          </Button>
          <Button size="icon-sm" variant="ghost" aria-label="Move down" disabled={at < 0 || at >= siblings.length - 1} onClick={() => void move(selected, selected.parentKey, at + 1)}>
            <ArrowDownIcon />
          </Button>
          <Button size="icon-sm" variant="ghost" aria-label="Indent" disabled={!canIndent} onClick={() => void move(selected, previous!.key, childrenOf(report, previous!.key).length)}>
            <IndentIcon />
          </Button>
          <Button size="icon-sm" variant="ghost" aria-label="Outdent" disabled={!canOutdent} onClick={() => void move(selected, parent!.parentKey, indexOf(report, parent!) + 1)}>
            <OutdentIcon />
          </Button>
          <Button
            size="icon-sm"
            variant="ghost"
            aria-label="Delete row"
            disabled={selected.parentKey === null}
            onClick={() => void edit((api) => api.deleteRow(reportId, selected.key)).then(() => setUi({ selectedRowKey: selected.parentKey }))}
          >
            <Trash2Icon />
          </Button>
        </span>
      </div>
      <div className="px-3 pt-2">
        <Refusal violations={refusal} />
      </div>
      <ul role="tree" aria-label="Rows" className="flex flex-col py-1">
        {report.rows
          .filter((row) => !hidden(row))
          .map((row) => {
            const hasChildren = report.rows.some((r) => r.parentKey === row.key)
            const collapsed = ui.collapsed.includes(row.key)
            return (
              <li key={row.key} role="treeitem" aria-selected={row.key === selected.key} aria-expanded={hasChildren ? !collapsed : undefined}>
                <div
                  className={cn('flex cursor-pointer items-center gap-1.5 py-1 pr-3 text-sm hover:bg-muted/50', row.key === selected.key && 'bg-accent')}
                  style={{ paddingLeft: `${0.75 + depthOf(report, row) * 1.1}rem` }}
                  onClick={() => setUi({ selectedRowKey: row.key })}
                >
                  <button
                    type="button"
                    aria-label={collapsed ? 'Expand' : 'Collapse'}
                    className={cn('text-muted-foreground', !hasChildren && 'invisible')}
                    onClick={(e) => {
                      e.stopPropagation()
                      setUi({ collapsed: collapsed ? ui.collapsed.filter((k) => k !== row.key) : [...ui.collapsed, row.key] })
                    }}
                  >
                    {collapsed ? <ChevronRightIcon className="size-3.5" /> : <ChevronDownIcon className="size-3.5" />}
                  </button>
                  <KindGlyph kind={row.kind} />
                  <span className={cn('truncate', row.kind !== 'data' && 'font-medium', !row.isVisible && 'text-muted-foreground')}>{row.label}</span>
                  {!row.isVisible && <EyeOffIcon aria-label="Hidden" className="size-3.5 text-muted-foreground" />}
                  <KeyText className="ml-auto truncate">{row.key}</KeyText>
                </div>
              </li>
            )
          })}
      </ul>
    </div>
  )
}
