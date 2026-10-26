import { useState } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import { useEditor } from '@/lib/report/store'
import { useCalcModel } from '@/lib/report/use-calc-model'
import type { Column, ColumnKind, DisplayFormat } from '@/lib/report/types'
import { KeyText } from '../shared/bits'
import { SearchCombobox } from '../shared/search-combobox'
import { Segmented } from '../shared/segmented'
import { SimpleSelect } from '../shared/simple-select'

export const FORMAT_LABEL: Record<DisplayFormat, string> = {
  currency: 'Currency',
  percent: 'Percent',
  number: 'Number',
  basisPoints: 'Basis points',
}

function toColumnKey(label: string) {
  const words = label.trim().toLowerCase().split(/[^a-z0-9]+/).filter(Boolean)
  return words.map((w, i) => (i === 0 ? w : w[0].toUpperCase() + w.slice(1))).join('') || 'column'
}

export function ColumnDialog({ columnKey, onClose }: { columnKey: string | null; onClose: () => void }) {
  const { report, ui, mutate } = useEditor()
  const calc = useCalcModel()
  const existing = columnKey ? report.columns.find((c) => c.key === columnKey) : undefined
  const [label, setLabel] = useState(existing?.label ?? '')
  const [kind, setKind] = useState<ColumnKind>(existing?.kind ?? 'variable')
  const [metric, setMetric] = useState(existing?.metric ?? '')
  const [format, setFormat] = useState<DisplayFormat>(existing?.format ?? 'currency')
  const [visible, setVisible] = useState(existing?.visible ?? true)
  const [touched, setTouched] = useState(false)

  const metricOptions =
    kind === 'variable'
      ? calc.variables.map((v) => ({ value: v.key, label: v.name, hint: v.key }))
      : calc.measures.map((m) => ({ value: m.key, label: m.name, hint: m.key, description: m.formula }))
  const selectedMeasure = kind === 'calculated' ? calc.measures.find((m) => m.key === metric) : undefined

  const newKey = (() => {
    const base = toColumnKey(label)
    let key = base
    let n = 2
    while (report.columns.some((c) => c.key === key)) key = `${base}${n++}`
    return key
  })()

  const labelError = touched && !label.trim() ? 'Give the column a label.' : null
  const metricError = touched && !metric ? 'Choose what this column returns.' : null

  function changeKind(next: ColumnKind) {
    setKind(next)
    setMetric('')
    if (!existing) setFormat(next === 'calculated' ? 'percent' : 'currency')
  }

  function save() {
    setTouched(true)
    if (!label.trim() || !metric) return
    const next: Column = { key: existing?.key ?? newKey, label: label.trim(), kind, metric, format, visible }
    mutate((d) => {
      if (existing) {
        const i = d.columns.findIndex((c) => c.key === existing.key)
        d.columns[i] = next
      } else {
        d.columns.push(next)
      }
    })
    toast(existing ? `Updated column "${next.label}"` : `Added column "${next.label}"`)
    onClose()
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{existing ? 'Edit column' : 'New column'}</DialogTitle>
          <DialogDescription>Each column shows one metric of {report.settings.model}.</DialogDescription>
        </DialogHeader>

        <form
          id="column-form"
          className="flex flex-col gap-4"
          onSubmit={(e) => {
            e.preventDefault()
            save()
          }}
        >
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="col-label">Label</Label>
            <Input id="col-label" value={label} onChange={(e) => setLabel(e.target.value)} aria-invalid={!!labelError} autoFocus />
            {labelError ? (
              <p className="text-xs text-destructive">{labelError}</p>
            ) : (
              (ui.advanced || existing) && (
                <p className="text-xs text-muted-foreground">
                  Key <KeyText>{existing?.key ?? newKey}</KeyText>
                </p>
              )
            )}
          </div>

          <div className="flex flex-col gap-1.5">
            <span className="text-sm font-medium">Returns</span>
            <Segmented
              aria-label="Returns"
              value={kind}
              onChange={changeKind}
              options={[
                { value: 'variable', label: 'Variable' },
                { value: 'calculated', label: 'Calculated measure' },
              ]}
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="col-metric">Metric</Label>
            <SearchCombobox
              id="col-metric"
              value={metric}
              onChange={setMetric}
              options={metricOptions}
              placeholder={kind === 'variable' ? 'Choose a variable…' : 'Choose a calculated measure…'}
              searchPlaceholder="Search by key or name…"
            />
            {metricError && <p className="text-xs text-destructive">{metricError}</p>}
            {selectedMeasure && (
              <p className="rounded-md bg-muted px-2 py-1.5 font-mono text-xs text-muted-foreground break-all">{selectedMeasure.formula}</p>
            )}
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="col-format">Display format</Label>
            <SimpleSelect
              id="col-format"
              value={format}
              onChange={(v) => setFormat(v as DisplayFormat)}
              options={(Object.keys(FORMAT_LABEL) as DisplayFormat[]).map((f) => ({ value: f, label: FORMAT_LABEL[f] }))}
            />
          </div>

          <label className="flex items-center justify-between gap-4">
            <span className="flex flex-col">
              <span className="text-sm font-medium">Visible</span>
              <span className="text-xs text-muted-foreground">Hidden columns are still computed but not shown.</span>
            </span>
            <Switch checked={visible} onCheckedChange={setVisible} />
          </label>
        </form>

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="column-form">
            {existing ? 'Apply' : 'Add column'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
