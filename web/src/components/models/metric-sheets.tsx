import { useState, type ReactNode } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Sheet, SheetContent, SheetDescription, SheetFooter, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { FORMAT_LABEL } from '@/components/report/columns/column-dialog'
import { Segmented } from '@/components/report/shared/segmented'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { formulaError } from '@/lib/model/rules'
import type { AttributionMethod, CalcModel, FormulaLanguage, MissingValue, ModelMeasure, ModelVariable, Rounding } from '@/lib/model/types'
import type { DisplayFormat } from '@/lib/report/types'
import { FormulaEditor } from './formula-editor'

const FORMAT_OPTIONS = (Object.keys(FORMAT_LABEL) as DisplayFormat[]).map((f) => ({ value: f, label: FORMAT_LABEL[f] }))
const ROUNDING_OPTIONS: { value: Rounding; label: string }[] = [
  { value: 'half_even', label: 'Half even' },
  { value: 'half_up', label: 'Half up' },
  { value: 'down', label: 'Down' },
]
const KEY_PATTERN = /^[A-Za-z][A-Za-z0-9_.]*$/

function Field({ id, label, help, error, children }: { id: string; label: string; help?: string; error?: string | null; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      {children}
      {error ? <p className="text-xs text-destructive">{error}</p> : help && <p className="text-xs text-pretty text-muted-foreground">{help}</p>}
    </div>
  )
}

function SheetShell({
  open,
  onClose,
  title,
  description,
  children,
  onSubmit,
  submitLabel,
  wide,
}: {
  open: boolean
  onClose: () => void
  title: string
  description?: string
  children: ReactNode
  onSubmit: () => void
  submitLabel: string
  wide?: boolean
}) {
  return (
    <Sheet open={open} onOpenChange={(o) => !o && onClose()}>
      <SheetContent className={wide ? 'w-full gap-0 p-0 data-[side=right]:sm:max-w-2xl' : 'w-full gap-0 p-0 data-[side=right]:sm:max-w-md'}>
        <form
          className="flex h-full min-h-0 flex-col"
          onSubmit={(e) => {
            e.preventDefault()
            onSubmit()
          }}
        >
          <SheetHeader className="border-b p-4">
            <SheetTitle>{title}</SheetTitle>
            {description && <SheetDescription>{description}</SheetDescription>}
          </SheetHeader>
          <div className="flex min-h-0 flex-1 flex-col gap-5 overflow-y-auto p-4">{children}</div>
          <SheetFooter className="flex-row justify-end border-t p-4">
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit">{submitLabel}</Button>
          </SheetFooter>
        </form>
      </SheetContent>
    </Sheet>
  )
}

function keyError(key: string, taken: string[], original?: string) {
  if (!key.trim()) return 'Enter a key.'
  if (!KEY_PATTERN.test(key)) return 'Use letters, digits, dots and underscores; start with a letter.'
  if (key !== original && taken.includes(key)) return 'This key is already used.'
  return null
}

export function VariableSheet({
  model,
  variable,
  open,
  onClose,
  onSave,
}: {
  model: CalcModel
  variable: ModelVariable | null
  open: boolean
  onClose: () => void
  onSave: (v: ModelVariable, originalKey?: string) => void
}) {
  return (
    <SheetShell
      open={open}
      onClose={onClose}
      title={variable ? `Edit variable ${variable.key}` : 'New variable'}
      submitLabel={variable ? 'Save variable' : 'Add variable'}
      onSubmit={() => document.getElementById('variable-form-submit')?.click()}
    >
      {open && <VariableFields key={variable?.key ?? 'new'} model={model} variable={variable} onSave={onSave} />}
    </SheetShell>
  )
}

function VariableFields({ model, variable, onSave }: { model: CalcModel; variable: ModelVariable | null; onSave: (v: ModelVariable, originalKey?: string) => void }) {
  const [v, setV] = useState<ModelVariable>(variable ?? { key: '', name: '', format: 'currency', unit: 'USD', missing: 'zero' })
  const [submitted, setSubmitted] = useState(false)
  const taken = [...model.variables, ...model.measures].map((m) => m.key)
  const kErr = keyError(v.key, taken, variable?.key)
  const nErr = v.name.trim() ? null : 'Enter a name.'
  return (
    <>
      <Field id="var-key" label="Key" error={submitted ? kErr : null}>
        <Input id="var-key" value={v.key} onChange={(e) => setV({ ...v, key: e.target.value.toUpperCase() })} className="font-mono" autoFocus />
      </Field>
      <Field id="var-name" label="Name" error={submitted ? nErr : null}>
        <Input id="var-name" value={v.name} onChange={(e) => setV({ ...v, name: e.target.value })} />
      </Field>
      <div className="grid grid-cols-2 gap-4">
        <Field id="var-format" label="Display format">
          <SimpleSelect id="var-format" value={v.format} onChange={(f) => setV({ ...v, format: f as DisplayFormat })} options={FORMAT_OPTIONS} />
        </Field>
        <Field id="var-unit" label="Unit">
          <Input id="var-unit" value={v.unit} onChange={(e) => setV({ ...v, unit: e.target.value })} />
        </Field>
      </div>
      <Field id="var-missing" label="Missing values" help="Zero makes a missing value 0; null leaves it empty.">
        <Segmented<MissingValue>
          aria-label="Missing values"
          value={v.missing}
          onChange={(missing) => setV({ ...v, missing })}
          options={[
            { value: 'null', label: 'Null' },
            { value: 'zero', label: 'Zero' },
          ]}
        />
      </Field>
      <button
        id="variable-form-submit"
        type="button"
        hidden
        onClick={() => {
          setSubmitted(true)
          if (!kErr && !nErr) onSave(v, variable?.key)
        }}
      />
    </>
  )
}

export function MeasureSheet({
  model,
  measure,
  open,
  onClose,
  onSave,
}: {
  model: CalcModel
  measure: ModelMeasure | null
  open: boolean
  onClose: () => void
  onSave: (m: ModelMeasure, originalKey?: string) => void
}) {
  return (
    <SheetShell
      wide
      open={open}
      onClose={onClose}
      title={measure ? `Edit calculated measure ${measure.key}` : 'New calculated measure'}
      submitLabel={measure ? 'Save measure' : 'Add measure'}
      onSubmit={() => document.getElementById('measure-form-submit')?.click()}
    >
      {open && <MeasureFields key={measure?.key ?? 'new'} model={model} measure={measure} onSave={onSave} />}
    </SheetShell>
  )
}

function MeasureFields({ model, measure, onSave }: { model: CalcModel; measure: ModelMeasure | null; onSave: (m: ModelMeasure, originalKey?: string) => void }) {
  const [m, setM] = useState<ModelMeasure>(
    measure ?? { key: '', name: '', formula: '', precision: 28, scale: 6, rounding: 'half_even', unit: '', format: 'percent' },
  )
  const [submitted, setSubmitted] = useState(false)
  const taken = [...model.variables, ...model.measures].map((x) => x.key)
  const kErr = keyError(m.key, taken, measure?.key)
  const nErr = m.name.trim() ? null : 'Enter a name.'
  const fErr = formulaError(model, m.formula, m.key || undefined)
  const showPower = m.formula && fErr?.startsWith('POWER')
  return (
    <>
      <div className="grid grid-cols-2 gap-4">
        <Field id="ms-key" label="Key" error={submitted ? kErr : null}>
          <Input id="ms-key" value={m.key} onChange={(e) => setM({ ...m, key: e.target.value.toUpperCase() })} className="font-mono" autoFocus />
        </Field>
        <Field id="ms-name" label="Name" error={submitted ? nErr : null}>
          <Input id="ms-name" value={m.name} onChange={(e) => setM({ ...m, name: e.target.value })} />
        </Field>
      </div>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="ms-formula">Formula</Label>
        <FormulaEditor
          id="ms-formula"
          model={model}
          value={m.formula}
          onChange={(formula) => setM({ ...m, formula })}
          placeholder="[m.INTEREST_INCOME] / [m.AVERAGE_BALANCE] * @yearDays / @periodDays"
          excludeKey={m.key}
          error={submitted || showPower ? fErr : null}
        />
      </div>
      <fieldset className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        <legend className="mb-2 text-sm font-medium">Output</legend>
        <Field id="ms-type" label="Type">
          <Input id="ms-type" value={`decimal(${m.precision},${m.scale})`} readOnly className="font-mono text-muted-foreground" />
        </Field>
        <Field id="ms-precision" label="Precision">
          <Input id="ms-precision" type="number" min={1} max={38} value={m.precision} onChange={(e) => setM({ ...m, precision: Number(e.target.value) })} />
        </Field>
        <Field id="ms-scale" label="Scale">
          <Input id="ms-scale" type="number" min={0} max={m.precision} value={m.scale} onChange={(e) => setM({ ...m, scale: Number(e.target.value) })} />
        </Field>
        <Field id="ms-rounding" label="Rounding">
          <SimpleSelect id="ms-rounding" value={m.rounding} onChange={(r) => setM({ ...m, rounding: r as Rounding })} options={ROUNDING_OPTIONS} />
        </Field>
      </fieldset>
      <div className="grid grid-cols-2 gap-4">
        <Field id="ms-unit" label="Unit">
          <Input id="ms-unit" value={m.unit} onChange={(e) => setM({ ...m, unit: e.target.value })} />
        </Field>
        <Field id="ms-format" label="Display format">
          <SimpleSelect id="ms-format" value={m.format} onChange={(f) => setM({ ...m, format: f as DisplayFormat })} options={FORMAT_OPTIONS} />
        </Field>
      </div>
      <button
        id="measure-form-submit"
        type="button"
        hidden
        onClick={() => {
          setSubmitted(true)
          if (!kErr && !nErr && !fErr) onSave(m, measure?.key)
        }}
      />
    </>
  )
}

export interface ModelSettings {
  key: string
  name: string
  currency: string
  calendar: string
  language: FormulaLanguage
  method: AttributionMethod
}

export function ModelSheet({
  model,
  open,
  takenKeys,
  onClose,
  onSave,
}: {
  model: ModelSettings | null
  open: boolean
  takenKeys: string[]
  onClose: () => void
  onSave: (s: ModelSettings) => void
}) {
  return (
    <SheetShell
      open={open}
      onClose={onClose}
      title={model ? `Edit ${model.name}` : 'New model'}
      description={model ? undefined : 'A calculation model holds the variables and calculated measures reports read.'}
      submitLabel={model ? 'Save model' : 'Create model'}
      onSubmit={() => document.getElementById('model-form-submit')?.click()}
    >
      {open && <ModelFields key={model?.key ?? 'new'} model={model} takenKeys={takenKeys} onSave={onSave} />}
    </SheetShell>
  )
}

function ModelFields({ model, takenKeys, onSave }: { model: ModelSettings | null; takenKeys: string[]; onSave: (s: ModelSettings) => void }) {
  const [s, setS] = useState<ModelSettings>(model ?? { key: '', name: '', currency: 'USD', calendar: 'gregorian', language: '1.0', method: 'shapley' })
  const [submitted, setSubmitted] = useState(false)
  const kErr = keyError(s.key, takenKeys, model?.key)
  const nErr = s.name.trim() ? null : 'Enter a name.'
  return (
    <>
      <Field id="md-key" label="Key" error={submitted ? kErr : null}>
        <Input id="md-key" value={s.key} disabled={!!model} onChange={(e) => setS({ ...s, key: e.target.value.toLowerCase() })} className="font-mono" placeholder="bank.margin_model" autoFocus />
      </Field>
      <Field id="md-name" label="Name" error={submitted ? nErr : null}>
        <Input id="md-name" value={s.name} onChange={(e) => setS({ ...s, name: e.target.value })} />
      </Field>
      <div className="grid grid-cols-2 gap-4">
        <Field id="md-currency" label="Currency">
          <SimpleSelect id="md-currency" value={s.currency} onChange={(currency) => setS({ ...s, currency })} options={['USD', 'EUR', 'GBP', 'CAD'].map((c) => ({ value: c, label: c }))} />
        </Field>
        <Field id="md-calendar" label="Calendar">
          <SimpleSelect id="md-calendar" value={s.calendar} onChange={(calendar) => setS({ ...s, calendar })} options={['gregorian', 'fiscal_4_4_5'].map((c) => ({ value: c, label: c }))} />
        </Field>
      </div>
      <Field id="md-language" label="Formula language" help="1.1 adds POWER. 1.2 adds declared parameters (@key), text comparisons and YEARFRAC(convention).">
        <Segmented<FormulaLanguage>
          aria-label="Formula language"
          value={s.language}
          onChange={(language) => setS({ ...s, language })}
          options={[
            { value: '1.0', label: '1.0' },
            { value: '1.1', label: '1.1' },
            { value: '1.2', label: '1.2' },
          ]}
        />
      </Field>
      <Field id="md-method" label="Attribution method" help="Shapley averages over every switching order; Sequential follows the drivers' listed order.">
        <Segmented<AttributionMethod>
          aria-label="Attribution method"
          value={s.method}
          onChange={(method) => setS({ ...s, method })}
          options={[
            { value: 'shapley', label: 'Shapley' },
            { value: 'sequential', label: 'Sequential' },
          ]}
        />
      </Field>
      <button
        id="model-form-submit"
        type="button"
        hidden
        onClick={() => {
          setSubmitted(true)
          if (!kErr && !nErr) onSave(s)
        }}
      />
    </>
  )
}
