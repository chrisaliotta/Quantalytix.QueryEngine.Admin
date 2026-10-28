import { Trash2Icon } from 'lucide-react'
import { useRef, useState } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Segmented } from '@/components/report/shared/segmented'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { CommitInput, InlineSelect, InlineText, PanelSection, PropertiesPanel, SplitView, focusRowClass, keyFromName, usePanelDraft } from '@/components/shell/inline-table'
import { FormRow, RowEditActions, SectionHeader, Td, Th } from '@/components/datasets/shared'
import type { ModelDocument, ModelVariable, VariableMissingBehavior, Violation } from '@/lib/models/contract'
import { attributionOf } from '@/lib/models/metrics'
import { cn } from '@/lib/utils'
import { AttributionChip } from './attribution-editor'
import { RefusalAlert } from './refusal-alert'
import { FORMAT_OPTIONS, MISSING_OPTIONS, NEW_ROW_ID, PanelRefusal, metricKeyError, pendingRowKeys, rowInputClass, scrollToRow, type ModelSectionProps } from './table-kit'
import { useModelEdit } from './use-model-edit'

const blankVariable = (): ModelVariable => ({ key: '', displayName: '', displayFormat: 'currency', unitKey: 'USD', missingBehavior: 'zero' })

function variableErrors(v: ModelVariable, isNew: boolean) {
  return {
    key: isNew ? metricKeyError(v.key) : null,
    name: v.displayName.trim() ? null : 'Enter a name.',
  }
}

const tidy = (v: ModelVariable): ModelVariable => ({ ...v, displayName: v.displayName.trim(), unitKey: v.unitKey?.trim() || null })

export function VariablesSection({ model, focusKey, onFocus, onAttribution }: ModelSectionProps) {
  const run = useModelEdit()
  const [newDraft, setNewDraftState] = useState<ModelVariable | null>(null)
  const [keyTouched, setKeyTouched] = useState(false)
  const [submitted, setSubmitted] = useState(false)
  const [refusal, setRefusal] = useState<Violation[] | null>(null)
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
    setNewDraft(blankVariable())
    scrollToRow(NEW_ROW_ID)
  }
  const createNew = async () => {
    const v = newRef.current
    if (!v) return false
    setSubmitted(true)
    const errs = variableErrors(v, true)
    if (errs.key || errs.name) return false
    const violations = await run((api) => api.saveVariable(model.id, tidy(v)))
    if (violations) {
      setRefusal(violations)
      return false
    }
    focus(v.key)
    scrollToRow(v.key)
    return true
  }
  const update = async (variable: ModelVariable, patch: Partial<ModelVariable>) => {
    setRefusal(await run((api) => api.saveVariable(model.id, { ...tidy({ ...variable, ...patch }), originalKey: variable.key })))
  }

  const newErrors = newDraft ? variableErrors(newDraft, true) : null
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
    <VariablePanel key={focused.key} model={model} variable={focused} seed={focused} onClose={() => onFocus(null)} onAttribution={() => onAttribution(focused.key)} />
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
                    <InlineText aria-label={`Name of ${v.key}`} value={v.displayName} onCommit={(name) => name.trim() && update(v, { displayName: name })} />
                  </Td>
                  <Td className="font-mono text-xs">{v.key}</Td>
                  <Td>
                    <InlineSelect aria-label={`Format of ${v.key}`} value={v.displayFormat} options={FORMAT_OPTIONS} onCommit={(f) => update(v, { displayFormat: f })} className="w-32 text-xs" />
                  </Td>
                  <Td>
                    <InlineText aria-label={`Unit of ${v.key}`} value={v.unitKey ?? ''} placeholder="—" onCommit={(unit) => update(v, { unitKey: unit })} className="w-20" />
                  </Td>
                  <Td>
                    <InlineSelect
                      aria-label={`Missing values of ${v.key}`}
                      value={v.missingBehavior}
                      options={MISSING_OPTIONS}
                      onCommit={(m) => update(v, { missingBehavior: m as VariableMissingBehavior })}
                      className={cn('w-24 font-mono text-xs', v.missingBehavior === 'null' && 'text-warning-foreground')}
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
          value={draft.displayName}
          placeholder="Name"
          aria-invalid={!!errors?.name}
          onChange={(e) => onChange({ displayName: e.target.value })}
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
        <InlineSelect aria-label="New variable: format" value={draft.displayFormat} options={FORMAT_OPTIONS} onCommit={(f) => onChange({ displayFormat: f })} className="w-32 text-xs" />
      </Td>
      <Td>
        <input
          aria-label="New variable: unit"
          value={draft.unitKey ?? ''}
          placeholder="Unit"
          onChange={(e) => onChange({ unitKey: e.target.value })}
          onKeyDown={onKeyDown}
          className={cn(rowInputClass, 'w-20')}
        />
      </Td>
      <Td>
        <InlineSelect
          aria-label="New variable: missing values"
          value={draft.missingBehavior}
          options={MISSING_OPTIONS}
          onCommit={(m) => onChange({ missingBehavior: m as VariableMissingBehavior })}
          className="w-24 font-mono text-xs"
        />
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
  onClose,
  onAttribution,
  onDraftCommit,
}: {
  model: ModelDocument
  variable?: ModelVariable
  seed: ModelVariable
  errors?: { key: string | null; name: string | null } | null
  onClose: () => void
  onAttribution?: () => void
  /** New variables only: panel edits update the pending row's draft instead of staging. */
  onDraftCommit?: (draft: ModelVariable) => void
}) {
  const run = useModelEdit()
  const [refusal, setRefusal] = useState<Violation[] | null>(null)
  const { draft, setDraft, pending, bodyProps } = usePanelDraft(seed, async (next) => {
    if (!variable) {
      onDraftCommit?.(next)
      return true
    }
    if (variableErrors(next, false).name) return false
    const violations = await run((api) => api.saveVariable(model.id, { ...tidy(next), originalKey: variable.key }))
    setRefusal(violations)
    return !violations
  })
  const shown = errors ?? (variable ? variableErrors(draft, false) : null)
  const attributed = variable ? !!attributionOf(model, variable.key) : false

  return (
    <PropertiesPanel
      label="Variable properties"
      title={<span className="font-mono">{draft.key || 'New variable'}</span>}
      subtitle={!variable ? 'Not added yet · needs a key and a name.' : pending && (refusal || shown?.name) ? <span className="text-destructive">Not applied — fix the highlighted inputs.</span> : draft.displayName}
      onClose={onClose}
      bodyProps={bodyProps}
      actions={
        variable && (
          <Button
            variant="ghost"
            size="icon-xs"
            aria-label={`Delete ${variable.key}`}
            title="Delete variable"
            onClick={async () => {
              const violations = await run((api) => api.deleteVariable(model.id, variable.key))
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
          <CommitInput id="var-name" value={draft.displayName} onCommit={(displayName) => setDraft((d) => ({ ...d, displayName }))} aria-invalid={!!shown?.name} />
        </FormRow>
      </PanelSection>
      <PanelSection title="Display">
        <div className="grid grid-cols-2 gap-4">
          <FormRow label="Format" htmlFor="var-format">
            <SimpleSelect id="var-format" value={draft.displayFormat} onChange={(displayFormat) => setDraft((d) => ({ ...d, displayFormat }))} options={FORMAT_OPTIONS} />
          </FormRow>
          <FormRow label="Unit" htmlFor="var-unit">
            <CommitInput id="var-unit" value={draft.unitKey ?? ''} onCommit={(unitKey) => setDraft((d) => ({ ...d, unitKey }))} />
          </FormRow>
        </div>
      </PanelSection>
      <PanelSection title="Missing values" description="Zero makes a missing value 0; null leaves it empty.">
        <Segmented<VariableMissingBehavior>
          aria-label="Missing values"
          value={draft.missingBehavior}
          onChange={(missingBehavior) => setDraft((d) => ({ ...d, missingBehavior }))}
          options={MISSING_OPTIONS}
        />
      </PanelSection>
      {variable && (
        <PanelSection
          title="Attribution"
          description={attributed ? 'How a change in this variable splits across its drivers.' : 'A variable has no formula to split until you give it an identity expression.'}
        >
          <div className="flex items-center gap-2">
            <AttributionChip model={model} metric={{ ...variable, type: 'variable' }} onOpen={() => onAttribution?.()} />
            <Button variant="outline" size="sm" onClick={onAttribution}>
              {attributed ? 'Edit attribution' : 'Add attribution'}
            </Button>
          </div>
        </PanelSection>
      )}
    </PropertiesPanel>
  )
}
