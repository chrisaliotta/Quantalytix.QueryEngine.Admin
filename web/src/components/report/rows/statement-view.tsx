import { useMemo } from 'react'
import { ScrollArea } from '@/components/ui/scroll-area'
import { statementLines, type StatementLine } from '@/lib/report/layout'
import { resolveStyle, styleCss } from '@/lib/report/rowStyle'
import { useEditor } from '@/lib/report/store'
import { allRows } from '@/lib/report/tree'
import { cn } from '@/lib/utils'

const INDENT = 18

/** The rows as a renderer places them (layout.ts), styled by role; read-only, and it follows edits without a server call. */
export function StatementView() {
  const { report, ui, setUi } = useEditor()
  const lines = useMemo(() => statementLines(report.rows, report.settings.defaultPresentation), [report.rows, report.settings.defaultPresentation])
  const rows = useMemo(() => new Map(allRows(report.rows).map((r) => [r.key, r])), [report.rows])
  const headerStyle = report.styles.find((s) => s.defaultRole === 'header')

  const styleOf = (line: StatementLine) =>
    line.part === 'header' ? headerStyle : resolveStyle(report.styles, report.rows, rows.get(line.key)!).style

  if (!lines.length) {
    return <p className="flex flex-1 items-center justify-center p-6 text-center text-muted-foreground">No visible rows yet.</p>
  }

  return (
    <ScrollArea className="min-h-0 flex-1">
      <ul aria-label="Statement" className="flex flex-col p-2">
        {lines.map((line) => {
          const style = styleOf(line)
          return (
            <li key={`${line.key}:${line.part}`}>
              <button
                type="button"
                onClick={() => setUi({ selectedRowKey: line.key })}
                aria-current={ui.selectedRowKey === line.key || undefined}
                className={cn(
                  'flex min-h-7 w-full items-center gap-2 rounded-md py-1 pr-2 text-left outline-none hover:bg-muted/70 focus-visible:ring-2 focus-visible:ring-ring/60',
                  ui.selectedRowKey === line.key && 'ring-2 ring-ring/60',
                )}
                style={{ ...styleCss(style), paddingLeft: (line.indent + (style?.indent ?? 0)) * INDENT + 8 }}
              >
                <span className="min-w-0 flex-1 truncate">{line.label}</span>
                {line.reverseSign && (
                  <span className="shrink-0 font-mono text-xs text-muted-foreground" title="Shown with its sign reversed">
                    ±
                  </span>
                )}
              </button>
            </li>
          )
        })}
      </ul>
    </ScrollArea>
  )
}
