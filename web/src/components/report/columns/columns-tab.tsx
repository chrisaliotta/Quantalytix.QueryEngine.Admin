import { ArrowDownIcon, ArrowUpIcon, PlusIcon, XIcon } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'
import type { ReportColumn, ReportColumnValueKind, Violation } from '@/lib/reports/contract'
import { useReportEditor } from '../editor-state'
import { Refusal } from '../shared/bits'
import { SimpleSelect } from '../shared/simple-select'

/** The report's columns in order, each a variable or calculated measure of the model, saved as one list. */
export function ColumnsTab() {
  const { reportId, report, model, run } = useReportEditor()
  const [columns, setColumns] = useState<ReportColumn[]>(report.columns)
  const [refusal, setRefusal] = useState<Violation[] | null>(null)
  const patch = (index: number, p: Partial<ReportColumn>) => setColumns(columns.map((c, i) => (i === index ? { ...c, ...p } : c)))
  const swap = (index: number, other: number) => {
    const next = [...columns]
    ;[next[index], next[other]] = [next[other], next[index]]
    setColumns(next)
  }
  const metrics = (kind: ReportColumnValueKind) =>
    (kind === 'variable' ? (model?.variables ?? []) : (model?.measures ?? [])).map((m) => ({ value: m.key, label: m.displayName, hint: m.key }))
  const add = () => {
    const metric = model?.variables[0]
    setColumns([...columns, { key: `column_${columns.length + 1}`, displayName: null, valueKind: 'variable', metricKey: metric?.key ?? '', displayFormat: null, isVisible: true }])
  }

  return (
    <div className="flex flex-col gap-4 p-4 md:p-6">
      <div>
        <h2 className="text-lg font-semibold">Columns</h2>
        <p className="text-sm text-muted-foreground">What each row returns, in order. A formula row places its value in one column, which must stay.</p>
      </div>
      <div className="overflow-x-auto rounded-lg border bg-card">
        <table className="w-full text-sm">
          <thead className="border-b text-xs text-muted-foreground">
            <tr className="[&>th]:h-9 [&>th]:px-3 [&>th]:text-left [&>th]:font-medium">
              <th>Key</th>
              <th>Label</th>
              <th>Kind</th>
              <th>Metric</th>
              <th>Format</th>
              <th>Visible</th>
              <th>
                <span className="sr-only">Order</span>
              </th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {columns.map((column, index) => (
              <tr key={index} className="[&>td]:px-3 [&>td]:py-1.5">
                <td>
                  <Input aria-label="Column key" className="h-7 w-32 font-mono" value={column.key} onChange={(e) => patch(index, { key: e.target.value.trim() })} />
                </td>
                <td>
                  <Input aria-label="Column label" className="h-7 w-40" value={column.displayName ?? ''} placeholder="Metric's label" onChange={(e) => patch(index, { displayName: e.target.value || null })} />
                </td>
                <td>
                  <SimpleSelect
                    aria-label="Column kind"
                    size="sm"
                    value={column.valueKind}
                    onChange={(v) => patch(index, { valueKind: v as ReportColumnValueKind, metricKey: metrics(v as ReportColumnValueKind)[0]?.value ?? '' })}
                    options={[
                      { value: 'variable', label: 'Variable' },
                      { value: 'calculated', label: 'Calculated' },
                    ]}
                  />
                </td>
                <td>
                  <SimpleSelect aria-label="Column metric" size="sm" value={column.metricKey} onChange={(metricKey) => patch(index, { metricKey })} options={metrics(column.valueKind)} className="w-48" />
                </td>
                <td>
                  <Input aria-label="Column format" className="h-7 w-24" value={column.displayFormat ?? ''} placeholder="Metric's" onChange={(e) => patch(index, { displayFormat: e.target.value || null })} />
                </td>
                <td>
                  <Switch aria-label="Visible" checked={column.isVisible} onCheckedChange={(isVisible) => patch(index, { isVisible })} />
                </td>
                <td className="whitespace-nowrap">
                  <Button size="icon-xs" variant="ghost" aria-label="Move up" disabled={index === 0} onClick={() => swap(index, index - 1)}>
                    <ArrowUpIcon />
                  </Button>
                  <Button size="icon-xs" variant="ghost" aria-label="Move down" disabled={index === columns.length - 1} onClick={() => swap(index, index + 1)}>
                    <ArrowDownIcon />
                  </Button>
                  <Button size="icon-xs" variant="ghost" aria-label="Remove column" onClick={() => setColumns(columns.filter((_, i) => i !== index))}>
                    <XIcon />
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Refusal violations={refusal} />
      <div className="flex gap-2">
        <Button variant="outline" onClick={add}>
          <PlusIcon /> Add column
        </Button>
        <Button
          className="ml-auto"
          disabled={JSON.stringify(columns) === JSON.stringify(report.columns)}
          onClick={async () => setRefusal(await run((api) => api.setColumns(reportId, columns)))}
        >
          Apply
        </Button>
      </div>
    </div>
  )
}
