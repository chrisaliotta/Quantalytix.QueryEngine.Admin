import { InfoIcon, Trash2Icon } from 'lucide-react'
import { useRef, useState } from 'react'
import { toast } from 'sonner'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { Segmented } from '@/components/report/shared/segmented'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { CommitInput, CommitTextarea, InlineText, PanelSection, PropertiesPanel, SplitView, focusRowClass, keyFromName, usePanelDraft } from '@/components/shell/inline-table'
import { FormRow, RowEditActions, SectionHeader, Td, Th } from '@/components/datasets/shared'
import { DAY_COUNTS, PARAM_KEY, RESERVED_PARAMS } from '@/lib/model/rules'
import type { CalcModel, ModelParameter, ParameterType, RuleViolation } from '@/lib/model/types'
import { useEditor } from '@/lib/report/store'
import type { RowNode } from '@/lib/report/types'
import { cn } from '@/lib/utils'
import { RefusalAlert } from './refusal-alert'
import { NEW_ROW_ID, PanelRefusal, pendingRowKeys, rowInputClass, scrollToRow } from './table-kit'

const CONVENTION_VALUES = DAY_COUNTS.map((d) => d.value)
const DECIMALS_12 = /^-?\d+(\.\d{1,12})?$/
const TYPE_OPTIONS: { value: ParameterType; label: string }[] = [
  { value: 'number', label: 'Number' },
  { value: 'text', label: 'Text' },
]

/** Panel/row draft: the default number stays as typed text so a half-typed value can show its error. */
type ParamDraft = Omit<ModelParameter, 'defaultNumber'> & { defaultNumber: string }

const toDraft = (p: ModelParameter): ParamDraft => ({ ...p, defaultNumber: p.defaultNumber != null ? String(p.defaultNumber) : '' })
const fromDraft = (d: ParamDraft): ModelParameter => ({
  key: d.key,
  name: d.name.trim(),
  type: d.type,
  allowedValues: d.type === 'text' ? d.allowedValues : [],
  defaultNumber: d.type === 'number' ? Number(d.defaultNumber) : null,
  defaultText: d.type === 'text' ? d.defaultText : null,
  description: d.description.trim(),
})
const blankParam = (): ParamDraft => ({ key: '', name: '', type: 'text', allowedValues: [], defaultNumber: '0', defaultText: '', description: '' })

interface ParamErrors {
  key: string | null
  name: string | null
  allowed: string | null
  default: string | null
  description: string | null
}

function paramErrors(d: ParamDraft, taken: string[], isNew: boolean): ParamErrors {
  const key = !isNew
    ? null
    : !d.key
      ? 'Enter a key.'
      : d.key.length > 128
        ? 'At most 128 characters.'
        : !PARAM_KEY.test(d.key)
          ? 'Start with a letter, then letters, digits or _.'
          : RESERVED_PARAMS.includes(d.key)
            ? `@${d.key} is always available; pick another key.`
            : taken.includes(d.key)
              ? 'This key is already used.'
              : null
  const allowed =
    d.type !== 'text'
      ? null
      : new Set(d.allowedValues).size !== d.allowedValues.length
        ? 'Each value must be unique.'
        : d.allowedValues.some((v) => v.length > 256)
          ? 'Each value can be at most 256 characters.'
          : null
  const def =
    d.type === 'number'
      ? !DECIMALS_12.test(d.defaultNumber.trim())
        ? 'Enter a number with up to 12 decimal places.'
        : null
      : !d.defaultText
        ? 'Choose a default.'
        : d.defaultText.length > 256
          ? 'At most 256 characters.'
          : d.allowedValues.length && !d.allowedValues.includes(d.defaultText)
            ? 'The default must be one of the allowed values.'
            : null
  return {
    key,
    name: !d.name.trim() ? 'Enter a name.' : d.name.length > 256 ? 'At most 256 characters.' : null,
    allowed,
    default: def,
    description: d.description.length > 1024 ? 'At most 1,024 characters.' : null,
  }
}
const hasErrors = (e: ParamErrors) => Object.values(e).some(Boolean)

function rowValues(rows: RowNode[], key: string): { row: string; value: string | number }[] {
  const out: { row: string; value: string | number }[] = []
  const walk = (list: RowNode[]) =>
    list.forEach((r) => {
      const v = r.parameters?.[key]
      if (v !== undefined && v !== '') out.push({ row: r.key, value: v })
      walk(r.children)
    })
  walk(rows)
  return out
}

/** Refusals the server makes against the reports that use this model. */
function useReportRefusals(model: CalcModel) {
  const { report } = useEditor()
  const { settings } = report
  const onModel = settings.model === model.key
  return (p: ModelParameter, isNew: boolean): RuleViolation[] => {
    if (!onModel) return []
    const out: RuleViolation[] = []
    if (isNew && settings.defaultFilters.some((f) => f.field === p.key))
      out.push({ rule: 'parameter_filter_clash', message: `Report '${settings.key}' already filters on '${p.key}' by default, so a parameter can't take that name.` })
    if (p.type === 'text' && p.allowedValues.length)
      for (const { row, value } of rowValues(report.rows, p.key))
        if (!p.allowedValues.includes(String(value)))
          out.push({ rule: 'parameter_allowed_values', message: `Row ${row} of report '${settings.key}' sets @${p.key} to '${value}', which the allowed values leave out.` })
    return out
  }
}

type Apply = (fn: (d: CalcModel) => void) => RuleViolation[] | null

export function ParametersTab({ model, apply, focusKey, onFocus }: { model: CalcModel; apply: Apply; focusKey: string | null; onFocus: (key: string | null) => void }) {
  const [newDraft, setNewDraftState] = useState<ParamDraft | null>(null)
  const [keyTouched, setKeyTouched] = useState(false)
  const [submitted, setSubmitted] = useState(false)
  const [refusal, setRefusal] = useState<RuleViolation[] | null>(null)
  const [deleting, setDeleting] = useState<ModelParameter | null>(null)
  const newRef = useRef(newDraft)
  const reportRefusals = useReportRefusals(model)
  const taken = model.parameters.map((p) => p.key)
  const focused = focusKey ? model.parameters.find((p) => p.key === focusKey) : undefined

  /** Stages a parameter after the report-side checks; returns the refusal, if any. */
  const save = (p: ModelParameter, isNew: boolean) => {
    const external = reportRefusals(p, isNew)
    if (external.length) return external
    return apply((d) => {
      const i = d.parameters.findIndex((x) => x.key === p.key)
      if (i >= 0) d.parameters[i] = p
      else d.parameters.push(p)
    })
  }

  const setNewDraft = (d: ParamDraft | null) => {
    newRef.current = d
    setNewDraftState(d)
  }
  const editNew = (p: Partial<ParamDraft>) => {
    const cur = newRef.current
    if (!cur) return
    const next = { ...cur, ...p }
    if (p.name !== undefined && !keyTouched && !('key' in p)) {
      const k = keyFromName(p.name, 'lower').replace(/_([a-z0-9])/g, (_, c: string) => c.toUpperCase())
      next.key = k
    }
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
    setNewDraft(blankParam())
    scrollToRow(NEW_ROW_ID)
  }
  const createNew = async () => {
    const d = newRef.current
    if (!d) return false
    setSubmitted(true)
    if (hasErrors(paramErrors(d, taken, true))) return false
    const violations = save(fromDraft(d), true)
    if (violations?.length) {
      setRefusal(violations)
      return false
    }
    focus(d.key)
    scrollToRow(d.key)
    return true
  }
  const rename = (p: ModelParameter, name: string) => {
    if (!name.trim()) return
    setRefusal(save({ ...p, name: name.trim() }, false))
  }

  const newErrors = newDraft ? paramErrors(newDraft, taken, true) : null
  const panel = newDraft ? (
    <ParameterPanel
      key="new"
      seed={newDraft}
      taken={taken}
      errors={submitted ? newErrors : null}
      onClose={cancelNew}
      onDraftCommit={(d) => {
        if (d.key !== newRef.current?.key) setKeyTouched(true)
        setNewDraft(d)
      }}
    />
  ) : focused ? (
    <ParameterPanel key={focused.key} param={focused} seed={toDraft(focused)} taken={taken} onSave={(p) => save(p, false)} onClose={() => onFocus(null)} onDelete={() => setDeleting(focused)} />
  ) : null

  return (
    <div className="flex flex-col gap-4">
      <SectionHeader
        title="Parameters"
        help="Named constants formulas can reference with @key."
        action={<RowEditActions adding={!!newDraft} creating={false} canCreate={!!newDraft?.key.trim()} onStartNew={startNew} onCreate={createNew} onCancelNew={cancelNew} />}
      />
      {model.language !== '1.2' && (
        <p className="flex items-center gap-2 rounded-md border bg-muted/40 px-3 py-2 text-sm text-muted-foreground">
          <InfoIcon className="size-4 shrink-0" aria-hidden />
          Formulas can read parameters once the model uses formula language 1.2.
        </p>
      )}
      {refusal && <RefusalAlert violations={refusal} onDismiss={() => setRefusal(null)} />}
      <SplitView panel={panel}>
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-sm">
            <thead className="border-b bg-muted/40">
              <tr>
                <Th>Name</Th>
                <Th>Key</Th>
                <Th>Type</Th>
                <Th>Allowed values</Th>
                <Th>Default</Th>
                <Th>Description</Th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {model.parameters.map((p) => (
                <tr key={p.key} data-row-id={p.key} aria-selected={p.key === focusKey} onClick={() => focus(p.key)} className={focusRowClass(p.key === focusKey)}>
                  <Td>
                    <InlineText aria-label={`Name of @${p.key}`} value={p.name} onCommit={(name) => rename(p, name)} />
                  </Td>
                  <Td className="font-mono text-xs">@{p.key}</Td>
                  <Td className="text-muted-foreground">{p.type}</Td>
                  <Td className="max-w-64 truncate font-mono text-xs text-muted-foreground">{p.type === 'text' && p.allowedValues.length ? p.allowedValues.join(', ') : 'any'}</Td>
                  <Td className="font-mono text-xs">{p.type === 'number' ? p.defaultNumber : p.defaultText}</Td>
                  <Td className="max-w-80 truncate text-muted-foreground">{p.description || '—'}</Td>
                </tr>
              ))}
              {newDraft && (
                <NewParameterRow
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
              {!model.parameters.length && !newDraft && (
                <tr>
                  <Td colSpan={6} className="py-8 text-center text-muted-foreground">
                    No parameters yet. Use “Add new row” to define one.
                  </Td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </SplitView>

      <AlertDialog open={!!deleting} onOpenChange={(o) => !o && setDeleting(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete @{deleting?.key}?</AlertDialogTitle>
            <AlertDialogDescription>Rows that set a value for it keep nothing. A parameter that a formula reads can&apos;t be deleted.</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              onClick={() => {
                if (deleting) {
                  const violations = apply((d) => void (d.parameters = d.parameters.filter((x) => x.key !== deleting.key)))
                  setRefusal(violations)
                  if (!violations) {
                    toast.success(`Deleted @${deleting.key}`, { description: 'Takes effect when you save.' })
                    onFocus(null)
                  }
                }
                setDeleting(null)
              }}
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}

function NewParameterRow({
  draft,
  errors,
  onChange,
  onSubmit,
  onCancel,
}: {
  draft: ParamDraft
  errors: ParamErrors | null
  onChange: (p: Partial<ParamDraft>) => void
  onSubmit: () => void
  onCancel: () => void
}) {
  const onKeyDown = pendingRowKeys(onSubmit, onCancel)
  const messages = errors ? Object.values(errors).filter(Boolean) : []
  return (
    <>
      <tr data-row-id={NEW_ROW_ID} aria-selected aria-label="New parameter" className={cn(focusRowClass(true), 'align-top')}>
        <Td>
          <input
            autoFocus
            aria-label="New parameter: name"
            value={draft.name}
            placeholder="Name"
            aria-invalid={!!errors?.name}
            onChange={(e) => onChange({ name: e.target.value })}
            onKeyDown={onKeyDown}
            className={cn(rowInputClass, 'min-w-40')}
          />
        </Td>
        <Td>
          <div className="flex items-center rounded-md border border-input bg-background focus-within:border-ring focus-within:ring-2 focus-within:ring-ring/30">
            <span className="pl-2 font-mono text-xs text-muted-foreground">@</span>
            <input
              aria-label="New parameter: key"
              value={draft.key}
              placeholder="dayCount"
              aria-invalid={!!errors?.key}
              onChange={(e) => onChange({ key: e.target.value })}
              onKeyDown={onKeyDown}
              className="h-8 w-36 min-w-0 bg-transparent pr-2 pl-0.5 font-mono text-xs outline-none placeholder:text-muted-foreground/70"
            />
          </div>
        </Td>
        <Td>
          <SimpleSelect size="sm" aria-label="New parameter: type" value={draft.type} onChange={(t) => onChange({ type: t as ParameterType })} options={TYPE_OPTIONS} className="w-24 text-xs" />
        </Td>
        <Td className="pt-3.5 font-mono text-xs text-muted-foreground">{draft.type === 'text' && draft.allowedValues.length ? draft.allowedValues.join(', ') : 'any · set in panel'}</Td>
        <Td>
          <input
            aria-label="New parameter: default"
            value={draft.type === 'number' ? draft.defaultNumber : (draft.defaultText ?? '')}
            placeholder={draft.type === 'number' ? '0' : 'Default'}
            inputMode={draft.type === 'number' ? 'decimal' : undefined}
            aria-invalid={!!errors?.default}
            onChange={(e) => onChange(draft.type === 'number' ? { defaultNumber: e.target.value } : { defaultText: e.target.value })}
            onKeyDown={onKeyDown}
            className={cn(rowInputClass, 'w-32 font-mono text-xs')}
          />
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

function ParameterPanel({
  param,
  seed,
  taken,
  errors,
  onSave,
  onClose,
  onDelete,
  onDraftCommit,
}: {
  param?: ModelParameter
  seed: ParamDraft
  taken: string[]
  errors?: ParamErrors | null
  onSave?: (p: ModelParameter) => RuleViolation[] | null
  onClose: () => void
  onDelete?: () => void
  /** New parameters only: panel edits update the pending row's draft instead of staging. */
  onDraftCommit?: (draft: ParamDraft) => void
}) {
  const [refusal, setRefusal] = useState<RuleViolation[] | null>(null)
  const { draft, setDraft, pending, bodyProps } = usePanelDraft(seed, async (next) => {
    if (!param) {
      onDraftCommit?.(next)
      return true
    }
    if (hasErrors(paramErrors(next, taken, false))) return false
    const violations = onSave?.(fromDraft(next)) ?? null
    setRefusal(violations?.length ? violations : null)
    return !violations?.length
  })
  const live = paramErrors(draft, taken, !param)
  const shown = errors ?? (param ? live : null)
  const err = (k: keyof ParamErrors) => (shown?.[k] ? [shown[k] as string] : undefined)

  return (
    <PropertiesPanel
      label="Parameter properties"
      title={<span className="font-mono">{draft.key ? `@${draft.key}` : 'New parameter'}</span>}
      subtitle={!param ? 'Not added yet · needs a key, a name and a default.' : pending && (refusal || hasErrors(live)) ? <span className="text-destructive">Not applied — fix the highlighted inputs.</span> : draft.name}
      onClose={onClose}
      bodyProps={bodyProps}
      actions={
        param && (
          <Button variant="ghost" size="icon-xs" aria-label={`Delete @${param.key}`} title="Delete parameter" onClick={onDelete}>
            <Trash2Icon />
          </Button>
        )
      }
    >
      {refusal && <PanelRefusal violations={refusal} onDismiss={() => setRefusal(null)} />}
      <PanelSection title="Identity" description="Formulas read it as @key; each report row sets its own value or inherits one.">
        <FormRow label="Key" htmlFor="pm-key" errors={err('key')} hint={param ? "A parameter's key can't be changed." : undefined}>
          <div className="flex items-center rounded-md border bg-background focus-within:ring-2 focus-within:ring-ring has-disabled:opacity-50">
            <span className="pl-3 font-mono text-sm text-muted-foreground">@</span>
            <CommitInput id="pm-key" value={draft.key} disabled={!!param} onCommit={(key) => setDraft((d) => ({ ...d, key }))} className="border-0 pl-0.5 font-mono shadow-none focus-visible:ring-0 disabled:opacity-100" aria-invalid={!!shown?.key} />
          </div>
        </FormRow>
        <FormRow label="Name" htmlFor="pm-name" errors={err('name')}>
          <CommitInput id="pm-name" value={draft.name} onCommit={(name) => setDraft((d) => ({ ...d, name }))} aria-invalid={!!shown?.name} />
        </FormRow>
        <FormRow label="Description" htmlFor="pm-description" errors={err('description')}>
          <CommitTextarea id="pm-description" rows={3} value={draft.description} onCommit={(description) => setDraft((d) => ({ ...d, description }))} />
        </FormRow>
      </PanelSection>

      <PanelSection title="Value">
        <FormRow label="Type" htmlFor="pm-type">
          <Segmented<ParameterType>
            aria-label="Type"
            value={draft.type}
            onChange={(type) => setDraft((d) => ({ ...d, type, defaultNumber: d.defaultNumber || '0' }))}
            options={TYPE_OPTIONS}
          />
        </FormRow>
        {draft.type === 'number' ? (
          <FormRow label="Default" htmlFor="pm-default-number" errors={err('default')}>
            <CommitInput id="pm-default-number" inputMode="decimal" value={draft.defaultNumber} onCommit={(v) => setDraft((d) => ({ ...d, defaultNumber: v.trim() }))} className="font-mono tabular-nums" aria-invalid={!!shown?.default} />
          </FormRow>
        ) : (
          <>
            <div className="flex flex-col gap-1.5">
              <div className="flex items-center justify-between gap-2">
                <label htmlFor="pm-allowed" className="text-sm font-medium">
                  Allowed values
                </label>
                <Button type="button" variant="link" size="sm" className="h-auto p-0 text-xs" onClick={() => setDraft((d) => ({ ...d, allowedValues: CONVENTION_VALUES }))}>
                  Allow the day count conventions
                </Button>
              </div>
              <CommitTextarea
                id="pm-allowed"
                rows={5}
                value={draft.allowedValues.join('\n')}
                onCommit={(v) =>
                  setDraft((d) => ({
                    ...d,
                    allowedValues: v
                      .split('\n')
                      .map((x) => x.trim())
                      .filter(Boolean),
                  }))
                }
                className="font-mono text-xs"
                placeholder="One per line"
              />
              {shown?.allowed ? <p className="text-xs text-destructive">{shown.allowed}</p> : <p className="text-xs text-muted-foreground">One per line. Leave empty to allow any text.</p>}
            </div>
            <FormRow label="Default" htmlFor="pm-default-text" errors={err('default')}>
              {draft.allowedValues.length ? (
                <SimpleSelect
                  id="pm-default-text"
                  value={draft.defaultText ?? ''}
                  onChange={(defaultText) => setDraft((d) => ({ ...d, defaultText }))}
                  placeholder="Choose a value"
                  options={draft.allowedValues.map((v) => ({ value: v, label: v }))}
                />
              ) : (
                <CommitInput id="pm-default-text" value={draft.defaultText ?? ''} onCommit={(defaultText) => setDraft((d) => ({ ...d, defaultText }))} className="font-mono" aria-invalid={!!shown?.default} />
              )}
            </FormRow>
          </>
        )}
      </PanelSection>
    </PropertiesPanel>
  )
}
