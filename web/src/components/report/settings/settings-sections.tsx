import type { ReactNode } from 'react'
import { Checkbox } from '@/components/ui/checkbox'
import { Label } from '@/components/ui/label'
import { useEditor } from '@/lib/report/store'
import { filterValues } from '@/lib/report/dimensions'
import type { ReportSettings } from '@/lib/report/types'
import { KeyText } from '../shared/bits'
import { FilterBuilder } from '../shared/filter-builder'
import { Segmented } from '../shared/segmented'

function Field({ label, htmlFor, hint, children }: { label: string; htmlFor?: string; hint?: ReactNode; children: ReactNode }) {
  return (
    <div className="grid gap-1.5 sm:grid-cols-[11rem_1fr] sm:items-start sm:gap-4">
      <Label htmlFor={htmlFor} className="sm:pt-2">
        {label}
      </Label>
      <div className="flex min-w-0 flex-col gap-1">
        {children}
        {hint && <p className="text-xs text-muted-foreground">{hint}</p>}
      </div>
    </div>
  )
}

function useSettings() {
  const { report, mutate } = useEditor()
  const set = <K extends keyof ReportSettings>(key: K, value: ReportSettings[K]) =>
    mutate((d) => {
      d.settings[key] = value
    })
  return { settings: report.settings, set }
}

export function DefaultsSection() {
  const { report } = useEditor()
  const { settings, set } = useSettings()
  const dims = report.breakoutDims

  function toggleBreakout(key: string, on: boolean) {
    const next = on ? [...settings.defaultBreakout, key] : settings.defaultBreakout.filter((k) => k !== key)
    set(
      'defaultBreakout',
      dims.map((d) => d.key).filter((k) => next.includes(k)),
    )
  }

  return (
    <div className="flex flex-col gap-5">
      <Field label="Periodicity">
        <Segmented
          aria-label="Periodicity"
          value={settings.periodicity}
          onChange={(v) => set('periodicity', v)}
          options={[
            { value: 'Day', label: 'Day' },
            { value: 'Month', label: 'Month' },
            { value: 'Quarter', label: 'Quarter' },
            { value: 'Year', label: 'Year' },
          ]}
        />
      </Field>
      <Field label="Layout">
        <Segmented
          aria-label="Layout"
          value={settings.layout}
          onChange={(v) => set('layout', v)}
          options={[
            { value: 'Statement', label: 'Statement' },
            { value: 'List', label: 'List' },
          ]}
        />
      </Field>
      <Field label="Subtotals" hint="Show subtotal values above or below the rows they total. Viewers can change this.">
        <Segmented
          aria-label="Subtotals"
          value={settings.subtotals}
          onChange={(v) => set('subtotals', v)}
          options={[
            { value: 'Above', label: 'Above' },
            { value: 'Below', label: 'Below' },
          ]}
        />
      </Field>
      <Field label="Default breakout" hint={settings.defaultBreakout.length === 0 ? 'No breakout. The report opens ungrouped.' : 'Applied in the order listed.'}>
        <fieldset className="flex flex-col gap-2 pt-2" aria-label="Default breakout">
          {dims.filter((d) => d.groupable).map((d) => (
            <label key={d.key} className="flex items-center gap-2">
              <Checkbox checked={settings.defaultBreakout.includes(d.key)} onCheckedChange={(on) => toggleBreakout(d.key, !!on)} />
              {d.name}
              <KeyText className="text-xs">{d.key}</KeyText>
            </label>
          ))}
          {dims.every((d) => !d.groupable) && <p className="text-muted-foreground">No groupable dimensions yet.</p>}
        </fieldset>
      </Field>
      <Field label="Default filter">
        <div className="pt-1">
          <FilterBuilder
            filters={settings.defaultFilters}
            onChange={(f) => set('defaultFilters', f)}
            fields={dims.filter((d) => d.filterable).map((d) => ({ value: d.key, label: d.name, values: filterValues(report, d) }))}
          />
        </div>
      </Field>
    </div>
  )
}
