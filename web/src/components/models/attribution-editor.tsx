import { ArrowDownIcon, ArrowUpIcon, GripVerticalIcon, PlusIcon, Trash2Icon } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { Segmented } from '@/components/report/shared/segmented'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { attributionChip, attributionErrors, attributionFor, defaultDrivers, inputsOf, type Metric } from '@/lib/model/rules'
import { useModels } from '@/lib/model/store'
import type { AttributionMethod, CalcModel, Driver, RuleViolation } from '@/lib/model/types'
import { cn } from '@/lib/utils'
import { FormulaEditor } from './formula-editor'
import { EXAMPLE, METHOD_LABEL, MiniWaterfall } from './method-explainer'

const NO_WEIGHT = '__none'

export function AttributionChip({ model, metric, onOpen }: { model: CalcModel; metric: Metric; onOpen: () => void }) {
  const chip = attributionChip(model, metric)
  if (chip.kind === 'none') {
    return (
      <Tooltip>
        <TooltipTrigger
          render={
            <button
              type="button"
              onClick={onOpen}
              className="rounded-full border border-dashed px-2 py-0.5 text-xs text-muted-foreground hover:border-foreground/40 hover:text-foreground"
            />
          }
        >
          No drivers
        </TooltipTrigger>
        <TooltipContent className="max-w-64">
          A variable has no formula to split; give it an identity expression to attribute its change.
        </TooltipContent>
      </Tooltip>
    )
  }
  return (
    <button
      type="button"
      onClick={onOpen}
      className={cn(
        'rounded-full border px-2 py-0.5 text-xs whitespace-nowrap hover:bg-muted',
        chip.kind === 'own' ? 'border-primary/40 bg-primary/10 text-foreground' : 'text-foreground',
      )}
    >
      {chip.kind === 'formula' ? `Formula drivers (${chip.count})` : `${chip.count} drivers${chip.weight ? ` · weight ${chip.weight}` : ''}`}
    </button>
  )
}

interface AttributionEditorProps {
  model: CalcModel
  metric: Metric
  onClose: () => void
  onRefused: (violations: RuleViolation[]) => void
}

export function AttributionEditor({ model, metric, onClose, onRefused }: AttributionEditorProps) {
  const { edit } = useModels()
  const initial = attributionFor(model, metric)
  const [expression, setExpression] = useState(metric.attribution?.expression ?? '')
  const [weight, setWeight] = useState(initial.weight ?? '')
  const [useDefault, setUseDefault] = useState(initial.useDefault)
  const [custom, setCustom] = useState<Driver[]>(initial.useDefault ? initial.drivers : initial.drivers)
  const [method, setMethod] = useState<AttributionMethod>(model.method)
  const [dragIndex, setDragIndex] = useState<number | null>(null)
  const [submitted, setSubmitted] = useState(false)

  const isVariable = metric.type === 'variable'
  const effective = expression.trim() || (metric.type === 'measure' ? metric.formula : '')
  const generated = defaultDrivers(model, effective)
  const drivers = useDefault ? generated : custom
  const inputs = inputsOf(effective)
  const errors = [
    ...(isVariable && !expression.trim() ? ['A variable needs an expression to attribute its change.'] : []),
    ...(effective ? attributionErrors(model, effective, drivers) : []),
  ]

  const setDriver = (i: number, patch: Partial<Driver>) => setCustom((ds) => ds.map((d, j) => (j === i ? { ...d, ...patch } : d)))
  const move = (from: number, to: number) =>
    setCustom((ds) => {
      if (to < 0 || to >= ds.length) return ds
      const next = [...ds]
      const [d] = next.splice(from, 1)
      next.splice(to, 0, d)
      return next
    })

  const save = () => {
    setSubmitted(true)
    if (errors.length) return
    const result = edit(model.key, (draft) => {
      const list = metric.type === 'variable' ? draft.variables : draft.measures
      const target = list.find((m) => m.key === metric.key)
      if (target) target.attribution = { expression: expression.trim() || undefined, weight: weight || undefined, useDefault, drivers: useDefault ? [] : custom }
    })
    if (result.ok) onClose()
    else onRefused(result.violations)
  }

  const clear = () => {
    const result = edit(model.key, (draft) => {
      const target = draft.measures.find((m) => m.key === metric.key)
      if (target) delete target.attribution
    })
    if (result.ok) onClose()
    else onRefused(result.violations)
  }

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-col gap-1">
        <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
          <h1 className="text-2xl font-semibold tracking-tight text-balance">{metric.name}</h1>
          <span className="font-mono text-sm text-muted-foreground">{metric.key}</span>
          <span className="rounded-full border px-2 py-0.5 text-xs text-muted-foreground">{isVariable ? 'Variable' : 'Calculated measure'}</span>
        </div>
        <p className="text-sm text-muted-foreground">Attribution splits a change in this metric into the drivers that caused it.</p>
      </header>
      <div className="grid items-start gap-6 overflow-hidden rounded-lg border bg-card lg:grid-cols-[1fr_24rem] lg:gap-0">
        <div className="flex flex-col gap-6 p-4 md:p-5">
          <div className="flex flex-col gap-2">
            <Label htmlFor="attr-expr">
              Expression {isVariable ? <span className="text-destructive">*</span> : <span className="font-normal text-muted-foreground">optional</span>}
            </Label>
            <FormulaEditor
              id="attr-expr"
              model={model}
              value={expression}
              onChange={setExpression}
              placeholder={metric.type === 'measure' ? metric.formula : `e.g. [m.${metric.key}]`}
              aria-describedby="attr-expr-help"
            />
            <p id="attr-expr-help" className="text-xs text-muted-foreground">
              Without an expression, the metric&apos;s own formula splits.
            </p>
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="attr-weight">Weight variable</Label>
            <SimpleSelect
              id="attr-weight"
              value={weight || NO_WEIGHT}
              onChange={(v) => setWeight(v === NO_WEIGHT ? '' : v)}
              options={[{ value: NO_WEIGHT, label: 'None' }, ...model.variables.map((v) => ({ value: v.key, label: v.name, hint: v.key }))]}
              className="sm:w-80"
            />
            <p className="text-xs text-pretty text-muted-foreground">
              Weighs rows and members when a ratio&apos;s change splits into rate, mix and interaction, such as average balance for a yield.
            </p>
          </div>

          <div className="flex flex-col gap-3">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <h3 className="text-sm font-medium">Drivers</h3>
                <p className="text-xs text-muted-foreground">The order is the sequential method&apos;s switching order and the waterfall&apos;s bar order.</p>
              </div>
              <label className="flex items-center gap-2 text-sm">
                <Switch
                  checked={useDefault}
                  onCheckedChange={(on) => {
                    if (!on) setCustom(generated)
                    setUseDefault(on)
                  }}
                />
                Use default drivers
              </label>
            </div>

            <ol className="flex flex-col gap-2">
              {drivers.map((d, i) => (
                <li
                  key={useDefault ? d.key : i}
                  draggable={!useDefault}
                  onDragStart={() => setDragIndex(i)}
                  onDragOver={(e) => !useDefault && e.preventDefault()}
                  onDrop={() => {
                    if (dragIndex !== null) move(dragIndex, i)
                    setDragIndex(null)
                  }}
                  className={cn('flex flex-col gap-2 rounded-lg border bg-card p-3', dragIndex === i && 'opacity-50')}
                >
                  <div className="flex items-center gap-2">
                    {!useDefault && <GripVerticalIcon aria-hidden className="size-4 shrink-0 cursor-grab text-muted-foreground" />}
                    <span className="w-5 shrink-0 text-xs text-muted-foreground tabular-nums">{i + 1}</span>
                    {useDefault ? (
                      <>
                        <span className="font-mono text-sm">{d.key}</span>
                        <span className="text-sm text-muted-foreground">{d.name}</span>
                      </>
                    ) : (
                      <>
                        <Input aria-label={`Driver ${i + 1} key`} value={d.key} onChange={(e) => setDriver(i, { key: e.target.value })} className="h-8 w-32 font-mono" />
                        <Input aria-label={`Driver ${i + 1} name`} value={d.name} onChange={(e) => setDriver(i, { name: e.target.value })} className="h-8 min-w-0 flex-1" />
                        <Button variant="ghost" size="icon-sm" aria-label="Move up" disabled={i === 0} onClick={() => move(i, i - 1)}>
                          <ArrowUpIcon />
                        </Button>
                        <Button variant="ghost" size="icon-sm" aria-label="Move down" disabled={i === drivers.length - 1} onClick={() => move(i, i + 1)}>
                          <ArrowDownIcon />
                        </Button>
                        <Button variant="ghost" size="icon-sm" aria-label={`Remove driver ${d.key}`} onClick={() => setCustom((ds) => ds.filter((_, j) => j !== i))}>
                          <Trash2Icon />
                        </Button>
                      </>
                    )}
                  </div>
                  <div className="flex flex-wrap items-center gap-1.5 pl-7">
                    <span className="text-xs text-muted-foreground">Inputs</span>
                    {(useDefault ? d.inputs : inputs).map((input) => {
                      const on = d.inputs.includes(input)
                      return useDefault ? (
                        <span key={input} className="rounded-sm border bg-muted px-1.5 py-0.5 font-mono text-xs">
                          {input}
                        </span>
                      ) : (
                        <button
                          key={input}
                          type="button"
                          aria-pressed={on}
                          onClick={() => setDriver(i, { inputs: on ? d.inputs.filter((x) => x !== input) : [...d.inputs, input] })}
                          className={cn(
                            'rounded-sm border px-1.5 py-0.5 font-mono text-xs',
                            on ? 'border-primary/50 bg-primary/15 text-foreground' : 'text-muted-foreground hover:text-foreground',
                          )}
                        >
                          {input}
                        </button>
                      )
                    })}
                  </div>
                </li>
              ))}
              {drivers.length === 0 && <li className="rounded-lg border border-dashed p-3 text-sm text-muted-foreground">Enter an expression to generate drivers.</li>}
            </ol>
            {!useDefault && (
              <Button
                variant="outline"
                size="sm"
                className="w-fit"
                onClick={() => setCustom((ds) => [...ds, { key: `driver_${ds.length + 1}`, name: 'New driver', inputs: [] }])}
              >
                <PlusIcon /> Add driver
              </Button>
            )}
          </div>

          {(submitted || errors.length > 0) && errors.length > 0 && (
            <ul role="alert" className="flex flex-col gap-1 rounded-lg border border-destructive/50 bg-destructive/10 p-3 text-xs text-destructive">
              {errors.map((e) => (
                <li key={e}>{e}</li>
              ))}
            </ul>
          )}
        </div>

        <aside className="flex h-full flex-col gap-4 border-t bg-muted/30 p-4 md:p-5 lg:border-t-0 lg:border-l">
          <div className="flex items-center justify-between gap-2">
            <h3 className="text-sm font-medium">How a change splits</h3>
            <Segmented
              aria-label="Attribution method"
              value={method}
              onChange={setMethod}
              options={(['shapley', 'sequential'] as const).map((m) => ({ value: m, label: METHOD_LABEL[m] }))}
            />
          </div>
          <p className="text-xs text-pretty text-muted-foreground">
            Sample: interest income as balance × rate, balance 10 → 12 and rate 0.40 → 0.45.
          </p>
          <MiniWaterfall start={EXAMPLE.start} end={EXAMPLE.end} bars={EXAMPLE.split[method]} />
          <p className="text-xs text-muted-foreground">
            The model uses <span className="font-medium text-foreground">{METHOD_LABEL[model.method]}</span>; the toggle only previews.
          </p>
        </aside>
      </div>
      <div className="sticky bottom-0 -mx-4 flex flex-row justify-between gap-2 border-t bg-background px-4 py-3 md:-mx-6 md:px-6">
        {metric.type === 'measure' && metric.attribution ? (
          <Button variant="ghost" onClick={clear}>
            Use own formula only
          </Button>
        ) : (
          <span />
        )}
        <div className="flex gap-2">
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={save} disabled={submitted && errors.length > 0} title="Saved with the model when you press Save">
            Apply
          </Button>
        </div>
      </div>
    </div>
  )
}
