import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import type { FilterNode, Periodicity, ReportLayout, ReportSettings, ReportSubtotalPosition, Violation } from '@/lib/reports/contract'
import { useReportEditor } from '../editor-state'
import { Refusal } from '../shared/bits'
import { Segmented } from '../shared/segmented'
import { SimpleSelect } from '../shared/simple-select'

const NONE = '__none__'

/**
 * The report's settings and defaults as typed fields, which the server stores in the documented JSON columns (OI-54): the default
 * breakout names calculation dimensions, and the default filter is a structured filter over them.
 */
export function DefaultsSection() {
  const { reportId, report, run } = useReportEditor()
  const [settings, setSettings] = useState<ReportSettings>(report.settings)
  const [filterText, setFilterText] = useState(report.settings.defaultFilter ? JSON.stringify(report.settings.defaultFilter, null, 2) : '')
  const [refusal, setRefusal] = useState<Violation[] | null>(null)
  const patch = (p: Partial<ReportSettings>) => setSettings((s) => ({ ...s, ...p }))
  const filter = parseFilter(filterText)

  const apply = async () => {
    if (filter === undefined) return
    const { displayName: _, ...rest } = { ...settings, defaultFilter: filter }
    setRefusal(await run((api) => api.updateSettings(reportId, rest)))
  }

  return (
    <div className="flex flex-col gap-5">
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="flex flex-col gap-2">
          <Label htmlFor="report-type">Report type</Label>
          <Input id="report-type" value={settings.reportType} onChange={(e) => patch({ reportType: e.target.value })} />
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="default-profile">Default profile</Label>
          <SimpleSelect
            id="default-profile"
            value={settings.defaultProfileKey ?? NONE}
            onChange={(v) => patch({ defaultProfileKey: v === NONE ? null : v })}
            options={[{ value: NONE, label: 'None' }, ...report.profiles.map((p) => ({ value: p.key, label: p.displayName }))]}
          />
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="periodicity">Periodicity</Label>
          <SimpleSelect
            id="periodicity"
            value={settings.periodicity ?? NONE}
            onChange={(v) => patch({ periodicity: v === NONE ? null : (v as Periodicity) })}
            options={[
              { value: NONE, label: 'None' },
              { value: 'day', label: 'Day' },
              { value: 'month', label: 'Month' },
              { value: 'quarter', label: 'Quarter' },
              { value: 'year', label: 'Year' },
            ]}
          />
        </div>
        <div className="flex flex-col gap-2">
          <Label>Layout</Label>
          <Segmented<ReportLayout>
            aria-label="Layout"
            value={settings.layout}
            onChange={(layout) => patch({ layout })}
            options={[
              { value: 'statement', label: 'Statement' },
              { value: 'list', label: 'List' },
            ]}
          />
        </div>
        <div className="flex flex-col gap-2">
          <Label>Subtotals</Label>
          <Segmented<ReportSubtotalPosition>
            aria-label="Subtotal position"
            value={settings.subtotalPosition}
            onChange={(subtotalPosition) => patch({ subtotalPosition })}
            options={[
              { value: 'above', label: 'Above' },
              { value: 'below', label: 'Below' },
            ]}
          />
        </div>
      </div>
      <fieldset className="flex flex-col gap-2">
        <legend className="mb-1 text-sm font-medium">Default breakout</legend>
        {report.calculationDimensions.length === 0 && <p className="text-sm text-muted-foreground">The report has no calculation dimensions.</p>}
        {report.calculationDimensions.map((d) => (
          <label key={d.key} className="flex items-center gap-2 text-sm">
            <Checkbox
              checked={settings.defaultBreakout.includes(d.key)}
              onCheckedChange={(checked) =>
                patch({ defaultBreakout: checked ? [...settings.defaultBreakout, d.key] : settings.defaultBreakout.filter((k) => k !== d.key) })
              }
            />
            {d.displayName}
          </label>
        ))}
      </fieldset>
      <div className="flex flex-col gap-2">
        <Label htmlFor="default-filter">Default filter</Label>
        <p className="text-xs text-muted-foreground">A structured filter over calculation dimension keys, such as {'{"operator":"eq","left":{"type":"dimension","id":"region"},"right":{"type":"literal","value":"RG01"}}'}. Empty for none.</p>
        <Textarea id="default-filter" className="font-mono text-xs" rows={6} value={filterText} onChange={(e) => setFilterText(e.target.value)} aria-invalid={filter === undefined} />
        {filter === undefined && <p className="text-xs text-destructive">Not valid JSON.</p>}
      </div>
      <Refusal violations={refusal} />
      <div>
        <Button onClick={() => void apply()} disabled={filter === undefined}>
          Apply
        </Button>
      </div>
    </div>
  )
}

/** The filter a text holds: null when empty, undefined when it is no JSON object. */
function parseFilter(text: string): FilterNode | null | undefined {
  if (!text.trim()) return null
  try {
    const value: unknown = JSON.parse(text)
    return typeof value === 'object' && value !== null && !Array.isArray(value) ? (value as FilterNode) : undefined
  } catch {
    return undefined
  }
}
