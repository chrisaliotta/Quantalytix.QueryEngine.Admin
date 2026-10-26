import { Trash2Icon } from 'lucide-react'
import { useRef, useState } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Segmented } from '@/components/report/shared/segmented'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { CommitInput, InlineSelect, InlineText, PanelSection, PropertiesPanel, SplitView, focusRowClass, keyFromName, usePanelDraft } from '@/components/shell/inline-table'
import { FormRow, RowEditActions, SectionHeader, Td, Th } from '@/components/datasets/shared'
import type { CalcModel, MissingValue, ModelVariable, RuleViolation } from '@/lib/model/types'
import type { DisplayFormat } from '@/lib/report/types'
import { cn } from '@/lib/utils'
import { AttributionChip } from './attribution-editor'
import { RefusalAlert } from './refusal-alert'
import { FORMAT_OPTIONS, MISSING_OPTIONS, NEW_ROW_ID, PanelRefusal, metricKeyError, pendingRowKeys, rowInputClass, scrollToRow, type ModelSectionProps } from './table-kit'

const blankVariable = (): ModelVariable => ({ key: '', name: '', format: 'currency', unit: 'USD', missing: 'zero' })

function variableErrors(model: CalcModel, v: ModelVariable, isNew: boolean) {
  const taken = [...model.variables, ...model.measures].map((m) => m.key)
  return {
    key: isNew ? metricKeyError(v.key, taken) : null,
    name: v.name.trim() ? null : 'Enter a name.',
  }
}

export function VariablesSection({ model, focusKey, onFocus, apply, onAttribution }: ModelSectionProps<CalcModel>) {
  const [newDraft, setNewDraftState] = useState<ModelVariable | null>(null)
  const [keyTouched, setKeyTouched] = useState(false)
  const [submitted, setSubmitted] = useState(false)
  const [refusal, setRefusal] = useState<RuleViolation[] | null>(null)
  const newRef = useRef(newDraft)
  const focused = focusKey ? model.variables.find((v) => v.key === focusKey) : undefined

  const setNewDraft = (d: ModelVariable | null) => {
    newRef.current = d
    setNewDraftState(d)
  }
  const editNew = (p: Partial<ModelVariable>) => {
    const cur = newRef.current
    if (!cur) return
    const next = { ...cur, ...p }
    if (p.name !== undefined && !keyTouched && !('key' in p)) next.key = keyFromName(p.name)
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
    setNewDraft(blankVariable())
    scrollToRow(NEW_ROW_ID)
  }
  const createNew = async () => {
    const v = newRef.current
    if (!v) return false
    setSubmitted(true)
    const errs = variableErrors(model, v, true)
    if (errs.key || errs.name) return false
    const violations = apply((d) => void d.variables.push({ ...v, name: v.name.trim() }))
    if (violations) {
      setRefusal(violations)
      return false
    }
    focus(v.key)
    scrollToRow(v.key)
    return true
  }
  const update = (key: string, patch: Partial<ModelVariable>) => {
    const violations = apply((d) => {
      const x = d.variables.find((y) => y.key === key)
      if (x) Object.assign(x, patch)
    })
    setRefusal(violations)
  }

  const newErrors = newDraft ? variableErrors(model, newDraft, true) : null
  const panel = newDraft ? (
    <VariablePanel
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
    <VariablePanel
      key={focused.key}
      model={model}
      variable={focused}
      seed={focused}
      apply={apply}
      onClose={() => onFocus(null)}
      onAttribution={() => onAttribution(focused.key)}
    />
  ) : null

  return (
    <div className="flex flex-col gap-4">
      <SectionHeader
        title="Variables"
        help="Values the model reads from data sources."
        action={<RowEditActions adding={!!newDraft} creating={false} canCreate={!!newDraft?.key.trim()} onStartNew={startNew} onCreate={createNew} onCancelNew={cancelNew} />}
      />
      {refusal && <RefusalAlert violations={refusal} onDismiss={() => setRefusal(null)} />}
      <SplitView panel={panel}>
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-sm">
            <thead className="border-b bg-muted/40">
              <tr>
                <Th>Name</Th>
                <Th>Key</Th>
                <Th>Format</Th>
                <Th>Unit</Th>
                <Th>Missing</Th>
                <Th>Attribution</Th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {model.variables.map((v) => (
                <tr key={v.key} data-row-id={v.key} aria-selected={v.key === focusKey} onClick={() => focus(v.key)} className={focusRowClass(v.key === focusKey)}>
                  <Td>
                    <InlineText aria-label={`Name of ${v.key}`} value={v.name} onCommit={(name) => name.trim() && update(v.key, { name: name.trim() })} />
                  </Td>
                  <Td className="font-mono text-xs">{v.key}</Td>
                  <Td>
                    <InlineSelect aria-label={`Format of ${v.key}`} value={v.format} options={FORMAT_OPTIONS} onCommit={(f) => update(v.key, { format: f as DisplayFormat })} className="w-32 text-xs" />
                  </Td>
                  <Td>
                    <InlineText aria-label={`Unit of ${v.key}`} value={v.unit} placeholder="—" onCommit={(unit) => update(v.key, { unit: unit.trim() })} className="w-20" />
                  </Td>
                  <Td>
                    <InlineSelect
                      aria-label={`Missing values of ${v.key}`}
                      value={v.missing}
                      options={MISSING_OPTIONS}
                      onCommit={(m) => update(v.key, { missing: m as MissingValue })}
                      className={cn('w-24 font-mono text-xs', v.missing === 'null' && 'text-warning-foreground')}
                    />
                  </Td>
                  <Td>
                    <AttributionChip model={model} metric={{ ...v, type: 'variable' }} onOpen={() => onAttribution(v.key)} />
                  </Td>
                </tr>
              ))}
              {newDraft && (
                <NewVariableRow
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
              {!model.variables.length && !newDraft && (
                <tr>
                  <Td colSpan={6} className="py-8 text-center text-muted-foreground">
                    No variables yet. Use “Add new row” to define one.
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

function NewVariableRow({
  draft,
  errors,
  onChange,
  onSubmit,
  onCancel,
}: {
  draft: ModelVariable
  errors: { key: string | null; name: string | null } | null
  onChange: (p: Partial<ModelVariable>) => void
  onSubmit: () => void
  onCancel: () => void
}) {
  const onKeyDown = pendingRowKeys(onSubmit, onCancel)
  const messages = [errors?.key, errors?.name].filter(Boolean)
  return (
    <tr data-row-id={NEW_ROW_ID} aria-selected aria-label="New variable" className={cn(focusRowClass(true), 'align-top')}>
      <Td>
        <input
          autoFocus
          aria-label="New variable: name"
          value={draft.name}
          placeholder="Name"
          aria-invalid={!!errors?.name}
          onChange={(e) => onChange({ name: e.target.value })}
          onKeyDown={onKeyDown}
          className={cn(rowInputClass, 'min-w-40')}
        />
      </Td>
      <Td>
        <input
          aria-label="New variable: key"
          value={draft.key}
          placeholder="KEY"
          aria-invalid={!!errors?.key}
          onChange={(e) => onChange({ key: e.target.value.toUpperCase() })}
          onKeyDown={onKeyDown}
          className={cn(rowInputClass, 'w-44 font-mono text-xs')}
        />
      </Td>
      <Td>
        <InlineSelect aria-label="New variable: format" value={draft.format} options={FORMAT_OPTIONS} onCommit={(f) => onChange({ format: f as DisplayFormat })} className="w-32 text-xs" />
      </Td>
      <Td>
        <input aria-label="New variable: unit" value={draft.unit} placeholder="Unit" onChange={(e) => onChange({ unit: e.target.value })} onKeyDown={onKeyDown} className={cn(rowInputClass, 'w-20')} />
      </Td>
      <Td>
        <InlineSelect aria-label="New variable: missing values" value={draft.missing} options={MISSING_OPTIONS} onCommit={(m) => onChange({ missing: m as MissingValue })} className="w-24 font-mono text-xs" />
      </Td>
      <Td>
        <span role={messages.length ? 'alert' : undefined} className={cn('block pt-1.5 text-xs text-pretty', messages.length ? 'text-destructive' : 'text-muted-foreground')}>
          {messages.length ? messages.join(' ') : 'Enter to add'}
        </span>
      </Td>
    </tr>
  )
}

function VariablePanel({
  model,
  variable,
  seed,
  errors,
  apply,
  onClose,
  onAttribution,
  onDraftCommit,
}: {
  model: CalcModel
  variable?: ModelVariable
  seed: ModelVariable
  errors?: { key: string | null; name: string | null } | null
  apply?: ModelSectionProps<CalcModel>['apply']
  onClose: () => void
  onAttribution?: () => void
  /** New variables only: panel edits update the pending row's draft instead of staging. */
  onDraftCommit?: (draft: ModelVariable) => void
}) {
  const [refusal, setRefusal] = useState<RuleViolation[] | null>(null)
  const { draft, setDraft, pending, bodyProps } = usePanelDraft(seed, async (next) => {
    if (!variable) {
      onDraftCommit?.(next)
      return true
    }
    if (variableErrors(model, next, false).name) return false
    const violations = apply?.((d) => {
      const i = d.variables.findIndex((x) => x.key === variable.key)
      if (i >= 0) d.variables[i] = { ...next, name: next.name.trim(), attribution: d.variables[i].attribution }
    })
    setRefusal(violations ?? null)
    return !violations
  })
  const shown = errors ?? (variable ? variableErrors(model, draft, false) : null)

  return (
    <PropertiesPanel
      label="Variable properties"
      title={<span className="font-mono">{draft.key || 'New variable'}</span>}
      subtitle={!variable ? 'Not added yet · needs a key and a name.' : pending && (refusal || shown?.name) ? <span className="text-destructive">Not applied — fix the highlighted inputs.</span> : draft.name}
      onClose={onClose}
      bodyProps={bodyProps}
      actions={
        variable && (
          <Button
            variant="ghost"
            size="icon-xs"
            aria-label={`Delete ${variable.key}`}
            title="Delete variable"
            onClick={() => {
              const violations = apply?.((d) => void (d.variables = d.variables.filter((x) => x.key !== variable.key)))
              if (violations) setRefusal(violations)
              else {
                toast.success(`Deleted ${variable.key}`, { description: 'Takes effect when you save.' })
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
        <FormRow label="Key" htmlFor="var-key" errors={shown?.key ? [shown.key] : undefined} hint={variable ? 'Fixed after creation; formulas read it as [m.KEY].' : 'Formulas read it as [m.KEY].'}>
          <CommitInput id="var-key" value={draft.key} disabled={!!variable} onCommit={(k) => setDraft((d) => ({ ...d, key: k.toUpperCase() }))} className="font-mono" aria-invalid={!!shown?.key} />
        </FormRow>
        <FormRow label="Name" htmlFor="var-name" errors={shown?.name ? [shown.name] : undefined}>
          <CommitInput id="var-name" value={draft.name} onCommit={(name) => setDraft((d) => ({ ...d, name }))} aria-invalid={!!shown?.name} />
        </FormRow>
      </PanelSection>
      <PanelSection title="Display">
        <div className="grid grid-cols-2 gap-4">
          <FormRow label="Format" htmlFor="var-format">
            <SimpleSelect id="var-format" value={draft.format} onChange={(f) => setDraft((d) => ({ ...d, format: f as DisplayFormat }))} options={FORMAT_OPTIONS} />
          </FormRow>
          <FormRow label="Unit" htmlFor="var-unit">
            <CommitInput id="var-unit" value={draft.unit} onCommit={(unit) => setDraft((d) => ({ ...d, unit: unit.trim() }))} />
          </FormRow>
        </div>
      </PanelSection>
      <PanelSection title="Missing values" description="Zero makes a missing value 0; null leaves it empty.">
        <Segmented<MissingValue> aria-label="Missing values" value={draft.missing} onChange={(missing) => setDraft((d) => ({ ...d, missing }))} options={MISSING_OPTIONS} />
      </PanelSection>
      {variable && (
        <PanelSection
          title="Attribution"
          description={variable.attribution ? 'How a change in this variable splits across its drivers.' : 'A variable has no formula to split until you give it an identity expression.'}
        >
          <div className="flex items-center gap-2">
            <AttributionChip model={model} metric={{ ...variable, type: 'variable' }} onOpen={() => onAttribution?.()} />
            <Button variant="outline" size="sm" onClick={onAttribution}>
              {variable.attribution ? 'Edit attribution' : 'Add attribution'}
            </Button>
          </div>
        </PanelSection>
      )}
    </PropertiesPanel>
  )
}
