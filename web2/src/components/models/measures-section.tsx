import { Trash2Icon } from 'lucide-react'
import { useRef, useState } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { CommitInput, InlineSelect, InlineText, PanelSection, PropertiesPanel, SplitView, focusRowClass, keyFromName, usePanelDraft } from '@/components/shell/inline-table'
import { FormRow, RowEditActions, SectionHeader, Td, Th } from '@/components/datasets/shared'
import type { ModelDocument, ModelMeasure, RoundingMode, Violation } from '@/lib/models/contract'
import { cn } from '@/lib/utils'
import { AttributionChip } from './attribution-editor'
import { FormulaEditor, FormulaText } from './formula-editor'
import { RefusalAlert } from './refusal-alert'
import { FORMAT_OPTIONS, NEW_ROW_ID, PanelRefusal, ROUNDING_OPTIONS, metricKeyError, pendingRowKeys, rowInputClass, scrollToRow, type ModelSectionProps } from './table-kit'
import { useModelEdit } from './use-model-edit'

const blankMeasure = (): ModelMeasure => ({
  key: '',
  displayName: '',
  formula: '',
  outputDataType: 'decimal',
  outputPrecision: 28,
  outputScale: 6,
  roundingMode: 'half_even',
  unitKey: null,
  displayFormat: 'percent',
})

interface MeasureErrors {
  key: string | null
  name: string | null
  formula: string | null
}

function measureErrors(m: ModelMeasure, isNew: boolean): MeasureErrors {
  return {
    key: isNew ? metricKeyError(m.key) : null,
    name: m.displayName.trim() ? null : 'Enter a name.',
    formula: m.formula.trim() ? null : 'Enter a formula.',
  }
}
const hasErrors = (e: MeasureErrors) => !!(e.key || e.name || e.formula)

const tidy = (m: ModelMeasure): ModelMeasure => ({ ...m, displayName: m.displayName.trim(), unitKey: m.unitKey?.trim() || null })

const outputOf = (m: ModelMeasure) => (m.outputDataType === 'decimal' ? `decimal(${m.outputPrecision},${m.outputScale})` : m.outputDataType)

export function MeasuresSection({ model, focusKey, onFocus, onAttribution }: ModelSectionProps) {
  const run = useModelEdit()
  const [newDraft, setNewDraftState] = useState<ModelMeasure | null>(null)
  const [keyTouched, setKeyTouched] = useState(false)
  const [submitted, setSubmitted] = useState(false)
  const [refusal, setRefusal] = useState<Violation[] | null>(null)
  const newRef = useRef(newDraft)
  const focused = focusKey ? model.measures.find((m) => m.key === focusKey) : undefined

  const setNewDraft = (d: ModelMeasure | null) => {
    newRef.current = d
    setNewDraftState(d)
  }
  const editNew = (p: Partial<ModelMeasure>) => {
    const cur = newRef.current
    if (!cur) return
    const next = { ...cur, ...p }
    if (p.displayName !== undefined && !keyTouched && !('key' in p)) next.key = keyFromName(p.displayName)
    setNewDraft(next)
  }
  const cancelNew = () => {
    setNewDraft(null)
    setSubmitted(false)
    setKeyTouched(false)
  }
  const focus = (key: string | null) => {
    cancelNew()
    onFocus(key)
  }
  const startNew = () => {
    onFocus(null)
    setRefusal(null)
    setSubmitted(false)
    setKeyTouched(false)
    setNewDraft(blankMeasure())
    scrollToRow(NEW_ROW_ID)
  }
  const createNew = async () => {
    const m = newRef.current
    if (!m) return false
    setSubmitted(true)
    if (hasErrors(measureErrors(m, true))) return false
    const violations = await run((api) => api.saveMeasure(model.id, tidy(m)))
    if (violations) {
      setRefusal(violations)
      return false
    }
    focus(m.key)
    scrollToRow(m.key)
    return true
  }
  const update = async (measure: ModelMeasure, patch: Partial<ModelMeasure>) => {
    setRefusal(await run((api) => api.saveMeasure(model.id, { ...tidy({ ...measure, ...patch }), originalKey: measure.key })))
  }

  const newErrors = newDraft ? measureErrors(newDraft, true) : null
  const panel = newDraft ? (
    <MeasurePanel
      key="new"
      model={model}
      seed={newDraft}
      errors={submitted ? newErrors : null}
      onClose={cancelNew}
      onDraftCommit={(d) => {
        if (d.key !== newRef.current?.key) setKeyTouched(true)
        setNewDraft(d)
      }}
    />
  ) : focused ? (
    <MeasurePanel key={focused.key} model={model} measure={focused} seed={focused} onClose={() => onFocus(null)} onAttribution={() => onAttribution(focused.key)} />
  ) : null

  return (
    <div className="flex flex-col gap-4">
      <SectionHeader
        title="Calculated measures"
        help="Values the model computes from variables and parameters."
        action={<RowEditActions adding={!!newDraft} creating={false} canCreate={!!newDraft?.key.trim()} onStartNew={startNew} onCreate={createNew} onCancelNew={cancelNew} />}
      />
      {refusal && <RefusalAlert violations={refusal} onDismiss={() => setRefusal(null)} />}
      <SplitView panel={panel}>
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full table-fixed text-sm">
            <colgroup>
              <col className="w-48" />
              <col className="w-48" />
              <col />
              <col className="w-36" />
              <col className="w-32" />
              <col className="w-44" />
            </colgroup>
            <thead className="border-b bg-muted/40">
              <tr>
                <Th>Name</Th>
                <Th>Key</Th>
                <Th>Formula</Th>
                <Th>Output</Th>
                <Th>Format</Th>
                <Th>Attribution</Th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {model.measures.map((m) => (
                <tr key={m.key} data-row-id={m.key} aria-selected={m.key === focusKey} onClick={() => focus(m.key)} className={focusRowClass(m.key === focusKey)}>
                  <Td>
                    <InlineText aria-label={`Name of ${m.key}`} value={m.displayName} onCommit={(name) => name.trim() && update(m, { displayName: name })} />
                  </Td>
                  <Td className="truncate font-mono text-xs">{m.key}</Td>
                  <Td>
                    <Tooltip>
                      <TooltipTrigger render={<div tabIndex={0} className="truncate rounded-sm outline-none focus-visible:ring-2 focus-visible:ring-ring" />}>
                        <FormulaText formula={m.formula} className="whitespace-nowrap" />
                      </TooltipTrigger>
                      <TooltipContent className="max-w-lg">
                        <span className="font-mono text-xs break-words">{m.formula}</span>
                      </TooltipContent>
                    </Tooltip>
                  </Td>
                  <Td className="font-mono text-xs text-muted-foreground">{outputOf(m)}</Td>
                  <Td>
                    <InlineSelect aria-label={`Format of ${m.key}`} value={m.displayFormat} options={FORMAT_OPTIONS} onCommit={(f) => update(m, { displayFormat: f })} className="w-28 text-xs" />
                  </Td>
                  <Td>
                    <AttributionChip model={model} metric={{ ...m, type: 'measure' }} onOpen={() => onAttribution(m.key)} />
                  </Td>
                </tr>
              ))}
              {newDraft && (
                <NewMeasureRow
                  draft={newDraft}
                  errors={submitted ? newErrors : null}
                  onChange={(p) => {
                    if ('key' in p) setKeyTouched(true)
                    editNew(p)
                  }}
                  onSubmit={createNew}
                  onCancel={cancelNew}
                />
              )}
              {!model.measures.length && !newDraft && (
                <tr>
                  <Td colSpan={6} className="py-8 text-center text-muted-foreground">
                    No calculated measures yet. Use “Add new row” to define one.
                  </Td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </SplitView>
    </div>
  )
}

function NewMeasureRow({
  draft,
  errors,
  onChange,
  onSubmit,
  onCancel,
}: {
  draft: ModelMeasure
  errors: MeasureErrors | null
  onChange: (p: Partial<ModelMeasure>) => void
  onSubmit: () => void
  onCancel: () => void
}) {
  const onKeyDown = pendingRowKeys(onSubmit, onCancel)
  const messages = errors ? [errors.key, errors.name, errors.formula].filter(Boolean) : []
  return (
    <>
      <tr data-row-id={NEW_ROW_ID} aria-selected aria-label="New calculated measure" className={cn(focusRowClass(true), 'align-top')}>
        <Td>
          <input
            autoFocus
            aria-label="New measure: name"
            value={draft.displayName}
            placeholder="Name"
            aria-invalid={!!errors?.name}
            onChange={(e) => onChange({ displayName: e.target.value })}
            onKeyDown={onKeyDown}
            className={rowInputClass}
          />
        </Td>
        <Td>
          <input
            aria-label="New measure: key"
            value={draft.key}
            placeholder="KEY"
            aria-invalid={!!errors?.key}
            onChange={(e) => onChange({ key: e.target.value.toUpperCase() })}
            onKeyDown={onKeyDown}
            className={cn(rowInputClass, 'font-mono text-xs')}
          />
        </Td>
        <Td className="pt-3.5">
          {draft.formula.trim() ? (
            <FormulaText formula={draft.formula} className="block truncate whitespace-nowrap" />
          ) : (
            <span className={cn('block truncate text-xs', errors?.formula ? 'text-destructive' : 'text-muted-foreground')}>Set in properties panel</span>
          )}
        </Td>
        <Td className="pt-3.5 font-mono text-xs text-muted-foreground">{outputOf(draft)}</Td>
        <Td>
          <InlineSelect aria-label="New measure: format" value={draft.displayFormat} options={FORMAT_OPTIONS} onCommit={(f) => onChange({ displayFormat: f })} className="w-28 text-xs" />
        </Td>
        <Td className="pt-3.5 text-xs text-muted-foreground">Enter to add</Td>
      </tr>
      {messages.length > 0 && (
        <tr className={focusRowClass(true)}>
          <Td colSpan={6} className="pt-0">
            <p role="alert" className="text-xs text-pretty text-destructive">
              {messages.join(' ')}
            </p>
          </Td>
        </tr>
      )}
    </>
  )
}

function MeasurePanel({
  model,
  measure,
  seed,
  errors,
  onClose,
  onAttribution,
  onDraftCommit,
}: {
  model: ModelDocument
  measure?: ModelMeasure
  seed: ModelMeasure
  errors?: MeasureErrors | null
  onClose: () => void
  onAttribution?: () => void
  /** New measures only: panel edits update the pending row's draft instead of staging. */
  onDraftCommit?: (draft: ModelMeasure) => void
}) {
  const run = useModelEdit()
  const [refusal, setRefusal] = useState<Violation[] | null>(null)
  const { draft, setDraft, pending, bodyProps } = usePanelDraft(seed, async (next) => {
    if (!measure) {
      onDraftCommit?.(next)
      return true
    }
    if (hasErrors(measureErrors(next, false))) return false
    const violations = await run((api) => api.saveMeasure(model.id, { ...tidy(next), originalKey: measure.key }))
    setRefusal(violations)
    return !violations
  })
  const live = measureErrors(draft, !measure)
  const shown: MeasureErrors | null = errors ?? (measure ? live : null)
  const err = (k: keyof MeasureErrors) => (shown?.[k] ? [shown[k] as string] : undefined)

  return (
    <PropertiesPanel
      size="lg"
      label="Calculated measure properties"
      title={<span className="font-mono">{draft.key || 'New calculated measure'}</span>}
      subtitle={!measure ? 'Not added yet · needs a key, a name and a formula.' : pending && (refusal || hasErrors(live)) ? <span className="text-destructive">Not applied — fix the highlighted inputs.</span> : draft.displayName}
      onClose={onClose}
      bodyProps={bodyProps}
      actions={
        measure && (
          <Button
            variant="ghost"
            size="icon-xs"
            aria-label={`Delete ${measure.key}`}
            title="Delete calculated measure"
            onClick={async () => {
              const violations = await run((api) => api.deleteMeasure(model.id, measure.key))
              if (violations) setRefusal(violations)
              else {
                toast.success(`Deleted ${measure.key}`, { description: 'Takes effect when you save.' })
                onClose()
              }
            }}
          >
            <Trash2Icon />
          </Button>
        )
      }
    >
      {refusal && <PanelRefusal violations={refusal} onDismiss={() => setRefusal(null)} />}
      <PanelSection title="Identity">
        <div className="grid gap-4 @md:grid-cols-2">
          <FormRow label="Key" htmlFor="ms-key" errors={err('key')} hint={measure ? 'Fixed after creation.' : 'Other formulas read it as [m.KEY].'}>
            <CommitInput id="ms-key" value={draft.key} disabled={!!measure} onCommit={(k) => setDraft((d) => ({ ...d, key: k.toUpperCase() }))} className="font-mono" aria-invalid={!!shown?.key} />
          </FormRow>
          <FormRow label="Name" htmlFor="ms-name" errors={err('name')}>
            <CommitInput id="ms-name" value={draft.displayName} onCommit={(displayName) => setDraft((d) => ({ ...d, displayName }))} aria-invalid={!!shown?.name} />
          </FormRow>
        </div>
      </PanelSection>
      <PanelSection title="Formula" description="Type [m. for metrics or @ for parameters.">
        <FormulaEditor
          id="ms-formula"
          model={model}
          value={draft.formula}
          onChange={(formula) => setDraft((d) => ({ ...d, formula }))}
          placeholder="[m.INTEREST_INCOME] / [m.AVERAGE_BALANCE] * @yearDays / @periodDays"
          selfKey={draft.key || null}
        />
      </PanelSection>
      <PanelSection title="Output" description={`Computed as ${outputOf(draft)}.`}>
        <div className="grid grid-cols-3 gap-4">
          <FormRow label="Precision" htmlFor="ms-precision">
            <Input
              id="ms-precision"
              type="number"
              min={1}
              max={38}
              value={draft.outputPrecision ?? ''}
              onChange={(e) => setDraft((d) => ({ ...d, outputPrecision: e.target.value === '' ? null : Number(e.target.value) }))}
              className="tabular-nums"
            />
          </FormRow>
          <FormRow label="Scale" htmlFor="ms-scale">
            <Input
              id="ms-scale"
              type="number"
              min={0}
              max={draft.outputPrecision ?? 38}
              value={draft.outputScale ?? ''}
              onChange={(e) => setDraft((d) => ({ ...d, outputScale: e.target.value === '' ? null : Number(e.target.value) }))}
              className="tabular-nums"
            />
          </FormRow>
          <FormRow label="Rounding" htmlFor="ms-rounding">
            <SimpleSelect
              id="ms-rounding"
              value={draft.roundingMode ?? 'half_even'}
              onChange={(r) => setDraft((d) => ({ ...d, roundingMode: r as RoundingMode }))}
              options={ROUNDING_OPTIONS}
            />
          </FormRow>
        </div>
      </PanelSection>
      <PanelSection title="Display">
        <div className="grid grid-cols-2 gap-4">
          <FormRow label="Format" htmlFor="ms-format">
            <SimpleSelect id="ms-format" value={draft.displayFormat} onChange={(displayFormat) => setDraft((d) => ({ ...d, displayFormat }))} options={FORMAT_OPTIONS} />
          </FormRow>
          <FormRow label="Unit" htmlFor="ms-unit">
            <CommitInput id="ms-unit" value={draft.unitKey ?? ''} onCommit={(unitKey) => setDraft((d) => ({ ...d, unitKey }))} />
          </FormRow>
        </div>
      </PanelSection>
      {measure && (
        <PanelSection title="Attribution" description="How a change in this measure splits across its drivers.">
          <div className="flex items-center gap-2">
            <AttributionChip model={model} metric={{ ...measure, type: 'measure' }} onOpen={() => onAttribution?.()} />
            <Button variant="outline" size="sm" onClick={onAttribution}>
              Edit attribution
            </Button>
          </div>
        </PanelSection>
      )}
    </PropertiesPanel>
  )
}
