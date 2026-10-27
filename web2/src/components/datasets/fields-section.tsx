import { ClockIcon, DatabaseZapIcon, Loader2Icon, MoreHorizontalIcon, PlusIcon, RefreshCwIcon, Trash2Icon, UndoIcon } from 'lucide-react'
import { useRef, useState, type KeyboardEvent } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { CommitInput, InlineSelect, InlineSwitch, InlineText, PanelSection, PropertiesPanel, SplitView, focusRowClass, keyFromName, usePanelDraft } from '@/components/shell/inline-table'
import { useDataset, useDatasetsApi, useRefreshDatasets, useSourceColumns, useSourceObjects, useVocabularies } from '@/lib/datasets/client'
import type { DatasetField, DatasetVersion, DatasetFieldInput, DatasetFieldMaterialization, Problem } from '@/lib/datasets/contract'
import { cn } from '@/lib/utils'
import type { SectionProps } from './dataset-detail'
import { ExpressionInput } from './expression-input'
import { CheckRow, Chip, DependentsList, FormRow, ProblemBanner, RowEditActions, SectionHeader, Td, Th, VocabSelect, useApiCall } from './shared'

const FLAGS: { key: 'isSelectable' | 'isFilterable' | 'isSortable' | 'isGroupable'; short: string; label: string; description: string }[] = [
  { key: 'isSelectable', short: 'select', label: 'Selectable', description: 'Detail queries may project it.' },
  { key: 'isFilterable', short: 'filter', label: 'Filterable', description: 'Row filters may reference it.' },
  { key: 'isSortable', short: 'sort', label: 'Sortable', description: 'Queries may order by it.' },
  { key: 'isGroupable', short: 'group', label: 'Groupable', description: 'May be an aggregate grouping on its own, without a binding.' },
]

export function timeFieldIds(version: DatasetVersion) {
  return new Set(version.bindings.filter((b) => b.bindingKind === 'time' && b.sourceFieldId).map((b) => b.sourceFieldId!))
}

export function fieldSourceText(f: DatasetField) {
  if (f.fieldSourceKind === 'column') return f.sourceColumn ?? '—'
  if (f.fieldSourceKind === 'json_path') return `${f.sourceColumn ?? '?'} ${f.jsonPath ?? ''}`
  return f.expressionText ?? '—'
}

const toInput = ({ materialization: _m, ...rest }: DatasetField): DatasetFieldInput => rest

const blankField = (): DatasetFieldInput => ({
  key: '',
  displayName: '',
  fieldSourceKind: 'column',
  sourceColumn: null,
  jsonPath: null,
  expressionText: null,
  createColumn: false,
  materialize: false,
  dataType: 'string',
  numericPrecision: null,
  numericScale: null,
  isNullable: true,
  unitKey: null,
  isSelectable: true,
  isFilterable: true,
  isSortable: true,
  isGroupable: false,
  isEnabled: true,
})

/** Changing the source kind clears inputs that only belong to the previous kind. */
function withSourceKind(d: DatasetFieldInput, k: DatasetFieldInput['fieldSourceKind']): DatasetFieldInput {
  return {
    ...d,
    fieldSourceKind: k,
    jsonPath: k === 'json_path' ? d.jsonPath : null,
    expressionText: k === 'expression' ? d.expressionText : null,
    createColumn: k === 'column' ? d.createColumn : false,
    sourceColumn: k === 'expression' || (k !== 'column' && d.createColumn) ? null : d.sourceColumn,
    materialize: k === 'json_path' ? d.materialize : false,
  }
}

const NEW_ROW_ID = '__new__'
const scrollToRow = (id: string) => requestAnimationFrame(() => document.querySelector(`[data-row-id="${CSS.escape(id)}"]`)?.scrollIntoView({ block: 'nearest' }))

/** Keeps the key following the display name, and a new column following the key, until the user types their own. */
function withIdentity(d: DatasetFieldInput, p: Partial<DatasetFieldInput>): DatasetFieldInput {
  const next = { ...d, ...p }
  if ('displayName' in p && !('key' in p) && d.key === keyFromName(d.displayName, 'lower')) next.key = keyFromName(next.displayName, 'lower')
  if (next.createColumn && !('sourceColumn' in p) && (d.sourceColumn ?? '') === d.key.toLowerCase()) next.sourceColumn = next.key.toLowerCase() || null
  return next
}

/** Dataset name, key and the table it reads, editable in place. A new dataset's table follows its key until renamed. */
function DatasetIdentity({ version, autoFocus }: { version: DatasetVersion; autoFocus?: boolean }) {
  const api = useDatasetsApi()
  const { data: dataset } = useDataset(version.datasetId)
  const { data: sources } = useSourceObjects()
  const { run, errorsFor, problem, setProblem } = useApiCall()
  const table = version.settings.sourceObject
  const source = sources?.find((s) => s.name === table && s.kind === version.settings.sourceObjectKind)
  const canRename = version.status === 'draft' && version.settings.sourceObjectKind === 'table' && !!source?.isManaged && dataset?.versions.length === 1
  if (!dataset) return null
  const tableHint = canRename ? 'Managed table created for this dataset.' : source?.isManaged ? 'Managed table. Shared by other versions, so it keeps its name.' : `Existing ${version.settings.sourceObjectKind} registered with this dataset.`

  return (
    <div className="flex flex-col gap-3 rounded-lg border bg-card p-4">
      {problem && !problem.errors.displayName && !problem.errors.tableName && <ProblemBanner problem={problem} onDismiss={() => setProblem(null)} />}
      <div className="grid gap-4 md:grid-cols-3">
        <FormRow label="Display name" htmlFor="ds-name" errors={errorsFor('displayName')}>
          <CommitInput
            id="ds-name"
            value={dataset.displayName}
            autoFocus={autoFocus}
            onFocus={(e) => autoFocus && e.currentTarget.select()}
            aria-invalid={!!errorsFor('displayName')}
            onCommit={(v) => v.trim() && run(() => api.updateDataset(dataset.id, { displayName: v.trim() }))}
          />
        </FormRow>
        <FormRow label="Dataset key" htmlFor="ds-key" hint="Query requests name the dataset by this key. Fixed after creation.">
          <Input id="ds-key" value={dataset.key} readOnly className="bg-muted/40 font-mono text-muted-foreground" />
        </FormRow>
        <FormRow label="Table name" htmlFor="ds-table" errors={errorsFor('tableName')} hint={tableHint}>
          <CommitInput
            id="ds-table"
            value={table}
            readOnly={!canRename}
            className={cn('font-mono', !canRename && 'bg-muted/40 text-muted-foreground')}
            aria-invalid={!!errorsFor('tableName')}
            onCommit={(v) => canRename && v.trim() && run(() => api.renameSourceTable(version.id, v.trim()))}
          />
        </FormRow>
      </div>
    </div>
  )
}

export function FieldsSection({ version, readOnly, editingId, onEdit, onOpen, isNew }: SectionProps) {
  const api = useDatasetsApi()
  const refresh = useRefreshDatasets()
  const { data: vocab } = useVocabularies()
  const { run, runOk, problem, setProblem } = useApiCall()
  const [newDraft, setNewDraftState] = useState<DatasetFieldInput | null>(null)
  const [newProblem, setNewProblem] = useState<Problem | null>(null)
  const [creating, setCreating] = useState(false)
  const newRef = useRef(newDraft)
  const timeIds = timeFieldIds(version)
  const canMaterialize = version.status !== 'retired'
  const kindLabel = (k: string) => vocab?.fieldSourceKind.find((t) => t.value === k)?.label ?? k
  const kindOptions = (vocab?.fieldSourceKind ?? []).map((t) => ({ value: t.value, label: t.label }))
  const focused = editingId ? version.fields.find((f) => f.id === editingId) : undefined
  const patch = (f: DatasetField, p: Partial<DatasetFieldInput>) => run(() => api.saveField(version.id, { ...toInput(f), ...p }))

  const setNewDraft = (d: DatasetFieldInput | null) => {
    newRef.current = d
    setNewDraftState(d)
  }
  const editNew = (p: Partial<DatasetFieldInput>) => {
    if (!newRef.current) return
    setNewDraft(withIdentity(newRef.current, p))
    if (newProblem) setNewProblem(null)
  }
  const cancelNew = () => {
    setNewDraft(null)
    setNewProblem(null)
  }

  const focus = (id: string | null) => {
    cancelNew()
    onEdit(id)
  }

  function startNew() {
    onEdit(null)
    setNewProblem(null)
    setNewDraft({ ...blankField(), createColumn: version.settings.sourceObjectKind === 'table' })
    scrollToRow(NEW_ROW_ID)
  }

  async function createNew() {
    const input = newRef.current
    if (!input || creating) return false
    setCreating(true)
    const r = await api.saveField(version.id, input)
    setCreating(false)
    if (!r.ok) {
      setNewProblem(r.problem)
      return false
    }
    await refresh()
    focus(r.data.id)
    scrollToRow(r.data.id)
    return true
  }

  const side =
    newDraft ? (
      <FieldPanel
        key="new"
        version={version}
        readOnly={readOnly}
        seed={newDraft}
        onClose={cancelNew}
        onOpen={onOpen}
        onDraftCommit={(d) => {
          setNewDraft(d)
          setNewProblem(null)
        }}
        createProblem={newProblem}
        onDismissCreateProblem={() => setNewProblem(null)}
      />
    ) : focused ? (
      <FieldPanel key={focused.id} version={version} readOnly={readOnly} field={focused} seed={toInput(focused)} onClose={() => onEdit(null)} onOpen={onOpen} />
    ) : null

  return (
    <div className="flex flex-col gap-4">
      <DatasetIdentity version={version} autoFocus={isNew} />
      <SectionHeader
        title="Fields"
        help="Logical columns of the dataset. Grains, bindings, measures and row filters all point at fields, so a JSON property can later become a real column without changing a request."
        action={
          !readOnly && (
            <RowEditActions adding={!!newDraft} creating={creating} canCreate={!!newDraft?.displayName.trim() && !!newDraft?.key.trim()} onStartNew={startNew} onCreate={createNew} onCancelNew={cancelNew} />
          )
        }
      />
      {problem && <ProblemBanner problem={problem} onDismiss={() => setProblem(null)} onOpen={onOpen} />}
      <SplitView panel={side}>
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-sm">
            <thead className="border-b bg-muted/40">
              <tr>
                <Th>Display name</Th>
                <Th>Key</Th>
                <Th>Source kind</Th>
                <Th>Source</Th>
                <Th>Type</Th>
                <Th>Capabilities</Th>
                <Th>Materialized</Th>
                <Th>Used by</Th>
                <Th>Enabled</Th>
                <Th className="w-10">
                  <span className="sr-only">Actions</span>
                </Th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {version.fields.map((f) => {
                const deps = version.dependents[f.id] ?? []
                const blocking = deps.filter((d) => d.blocksDelete)
                return (
                  <tr key={f.id} data-row-id={f.id} aria-selected={f.id === editingId} onClick={() => focus(f.id)} className={cn(focusRowClass(f.id === editingId), !f.isEnabled && 'text-muted-foreground')}>
                    <Td className="min-w-40">
                      {readOnly ? f.displayName : <InlineText aria-label={`Display name of ${f.key}`} value={f.displayName} onCommit={(v) => patch(f, { displayName: v })} />}
                    </Td>
                    <Td>
                      <span className="flex items-center gap-1.5">
                        <span className="font-mono text-xs">{f.key}</span>
                        {timeIds.has(f.id) && <ClockIcon aria-label="Time field" className="size-3.5 shrink-0 text-primary" />}
                      </span>
                    </Td>
                    <Td className="whitespace-nowrap">
                      {readOnly ? (
                        <span className="text-xs">{kindLabel(f.fieldSourceKind)}</span>
                      ) : (
                        <InlineSelect
                          aria-label={`Source kind of ${f.key}`}
                          value={f.fieldSourceKind}
                          options={kindOptions}
                          onCommit={(k) => {
                            if (k === f.fieldSourceKind) return
                            run(() => api.saveField(version.id, withSourceKind(toInput(f), k as DatasetFieldInput['fieldSourceKind']))).then(() => focus(f.id))
                          }}
                          className="w-32 text-xs"
                        />
                      )}
                    </Td>
                    <Td className="max-w-56">
                      <span className="block truncate font-mono text-xs" title={fieldSourceText(f)}>
                        {fieldSourceText(f)}
                      </span>
                    </Td>
                    <Td className="whitespace-nowrap">
                      {readOnly ? (
                        <span className="font-mono text-xs">{f.dataType}</span>
                      ) : (
                        <InlineSelect
                          aria-label={`Data type of ${f.key}`}
                          value={f.dataType}
                          options={(vocab?.dataType ?? []).map((t) => ({ value: t.value, label: t.label }))}
                          onCommit={(t) =>
                            patch(f, {
                              dataType: t as DatasetFieldInput['dataType'],
                              numericPrecision: t === 'decimal' ? (f.numericPrecision ?? 18) : null,
                              numericScale: t === 'decimal' ? (f.numericScale ?? 2) : null,
                            })
                          }
                          className="w-32 font-mono text-xs"
                        />
                      )}
                    </Td>
                    <Td>
                      <span className="flex flex-wrap gap-1">
                        {FLAGS.map((fl) => (
                          <button
                            key={fl.key}
                            type="button"
                            title={fl.description}
                            aria-pressed={f[fl.key]}
                            disabled={readOnly}
                            onClick={() => patch(f, { [fl.key]: !f[fl.key] })}
                            className={cn(
                              'rounded-sm border px-1.5 py-0.5 font-mono text-[11px] leading-none transition-colors',
                              f[fl.key] ? 'border-primary/40 bg-primary/10 text-primary' : 'border-dashed text-muted-foreground/70',
                              !readOnly && 'hover:border-primary/60',
                            )}
                          >
                            {fl.short}
                          </button>
                        ))}
                      </span>
                    </Td>
                    <Td>
                      <MaterializationStatus field={f} />
                    </Td>
                    <Td className="text-xs tabular-nums">{deps.length ? `${deps.length} item${deps.length === 1 ? '' : 's'}` : <span className="text-muted-foreground">—</span>}</Td>
                    <Td>
                      <InlineSwitch aria-label={`${f.key} enabled`} checked={f.isEnabled} onCommit={(v) => !readOnly && patch(f, { isEnabled: v })} />
                    </Td>
                    <Td>
                      {(!readOnly || (canMaterialize && f.fieldSourceKind === 'json_path')) && (
                        <DropdownMenu>
                          <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label={`Actions for ${f.key}`} />}>
                            <MoreHorizontalIcon />
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end" className="w-64">
                            {canMaterialize && f.fieldSourceKind === 'json_path' && (
                              <MaterializationMenuItems field={f} onRun={(call, done) => run(() => call(api, version.id, f.id)).then((r) => r && toast.success(done(r)))} />
                            )}
                            {!readOnly && (
                              <>
                                {canMaterialize && f.fieldSourceKind === 'json_path' && <DropdownMenuSeparator />}
                                <DropdownMenuItem
                                  variant="destructive"
                                  disabled={!!blocking.length}
                                  onClick={async () => {
                                    if (await runOk(() => api.deleteField(version.id, f.id))) {
                                      if (editingId === f.id) onEdit(null)
                                      toast.success(`Deleted ${f.key}`)
                                    }
                                  }}
                                >
                                  <Trash2Icon /> Delete
                                </DropdownMenuItem>
                                {!!blocking.length && <p className="px-2 pb-1 text-[11px] text-muted-foreground">Kept by {blocking.map((d) => d.itemKey).join(', ')}</p>}
                              </>
                            )}
                          </DropdownMenuContent>
                        </DropdownMenu>
                      )}
                    </Td>
                  </tr>
                )
              })}
              {!readOnly && newDraft && (
                <NewFieldRow
                  draft={newDraft}
                  problem={newProblem}
                  creating={creating}
                  kindOptions={kindOptions}
                  dataTypes={(vocab?.dataType ?? []).map((t) => ({ value: t.value, label: t.label }))}
                  onChange={editNew}
                  onSubmit={createNew}
                  onCancel={cancelNew}
                />
              )}
              {!version.fields.length && !newDraft && (
                <tr>
                  <Td colSpan={10} className="py-8 text-center text-muted-foreground">
                    {readOnly ? 'No fields in this version.' : 'No fields yet. Use “Add new row” to add a column to the table.'}
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

/* ───────────────────────── new field row ───────────────────────── */

const rowInputClass =
  'h-8 w-full min-w-0 rounded-md border border-input bg-background px-2 text-sm outline-none placeholder:text-muted-foreground/70 focus:border-ring focus:ring-2 focus:ring-ring/30 aria-invalid:border-destructive'

/** The pending field: a selected, editable row bound to the same draft as the open properties panel. */
function NewFieldRow({
  draft,
  problem,
  creating,
  kindOptions,
  dataTypes,
  onChange,
  onSubmit,
  onCancel,
}: {
  draft: DatasetFieldInput
  problem: Problem | null
  creating: boolean
  kindOptions: { value: string; label: string }[]
  dataTypes: { value: string; label: string }[]
  onChange: (p: Partial<DatasetFieldInput>) => void
  onSubmit: () => void
  onCancel: () => void
}) {
  const errs = problem?.errors ?? {}
  const onKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.nativeEvent.isComposing || e.keyCode === 229) return
    if (e.key === 'Enter') {
      e.preventDefault()
      onSubmit()
    } else if (e.key === 'Escape') {
      e.preventDefault()
      e.stopPropagation()
      onCancel()
    }
  }
  const messages = Object.values(errs).flat()
  const newColumn = draft.createColumn && draft.fieldSourceKind === 'column'

  return (
    <tr data-row-id={NEW_ROW_ID} aria-selected aria-label="New field" className={cn(focusRowClass(true), 'align-top')}>
      <Td className="min-w-40">
        <input autoFocus aria-label="New field: display name" value={draft.displayName} placeholder="Display name" disabled={creating} aria-invalid={!!errs.displayName} onChange={(e) => onChange({ displayName: e.target.value })} onKeyDown={onKeyDown} className={rowInputClass} />
      </Td>
      <Td>
        <input aria-label="New field: field key" value={draft.key} placeholder="field_key" disabled={creating} aria-invalid={!!errs.key} onChange={(e) => onChange({ key: e.target.value })} onKeyDown={onKeyDown} className={cn(rowInputClass, 'w-36 font-mono text-xs')} />
      </Td>
      <Td className="whitespace-nowrap">
        <InlineSelect
          aria-label="New field: source kind"
          value={draft.fieldSourceKind}
          options={kindOptions}
          onCommit={(k) => {
            const next = withSourceKind(draft, k as DatasetFieldInput['fieldSourceKind'])
            onChange(k === 'column' && draft.fieldSourceKind !== 'column' ? { ...next, createColumn: true, sourceColumn: draft.key.toLowerCase() || null } : next)
          }}
          className="w-32 text-xs"
        />
      </Td>
      <Td className="max-w-56">
        {newColumn ? (
          <span className="flex flex-col gap-0.5">
            <input
              aria-label="New field: new column name"
              value={draft.sourceColumn ?? ''}
              placeholder="column_name"
              disabled={creating}
              aria-invalid={!!errs.sourceColumn}
              onChange={(e) => onChange({ sourceColumn: e.target.value || null })}
              onKeyDown={onKeyDown}
              className={cn(rowInputClass, 'font-mono text-xs')}
            />
            <span className="px-2 text-[11px] text-muted-foreground">New column</span>
          </span>
        ) : (
          <span className="block truncate pt-1.5 font-mono text-xs">{(draft.fieldSourceKind === 'json_path' ? draft.jsonPath : draft.fieldSourceKind === 'expression' ? draft.expressionText : draft.sourceColumn) || '—'}</span>
        )}
      </Td>
      <Td className="whitespace-nowrap">
        <InlineSelect
          aria-label="New field: data type"
          value={draft.dataType}
          options={dataTypes}
          onCommit={(t) => onChange({ dataType: t as DatasetFieldInput['dataType'], numericPrecision: t === 'decimal' ? (draft.numericPrecision ?? 18) : null, numericScale: t === 'decimal' ? (draft.numericScale ?? 2) : null })}
          className="w-32 font-mono text-xs"
        />
      </Td>
      <Td colSpan={5}>
        <span className="flex items-start justify-between gap-3 pt-1">
          <span role={messages.length ? 'alert' : undefined} className={cn('text-xs text-pretty', messages.length ? 'text-destructive' : 'text-muted-foreground')}>
            {messages.length ? messages.join(' ') : newColumn ? 'Adds a column to the table when saved. Fine-tune it in the panel.' : 'Finish the source in the panel.'}
          </span>
        </span>
      </Td>
    </tr>
  )
}

/* ───────────────────────── field panel ───────────────────────── */

function FieldPanel({
  version,
  readOnly,
  field,
  seed,
  onClose,
  onOpen,
  onDraftCommit,
  createProblem,
  onDismissCreateProblem,
}: {
  version: DatasetVersion
  readOnly: boolean
  field?: DatasetField
  seed: DatasetFieldInput
  onClose: () => void
  onOpen: SectionProps['onOpen']
  /** New fields only: panel edits update the pending row's draft instead of saving. */
  onDraftCommit?: (draft: DatasetFieldInput) => void
  createProblem?: Problem | null
  onDismissCreateProblem?: () => void
}) {
  const api = useDatasetsApi()
  const { data: vocab } = useVocabularies()
  const { data: columns } = useSourceColumns(version.settings.sourceObjectKind, version.settings.sourceObject)
  const { run, runOk, saving, problem, setProblem } = useApiCall()
  const shown = problem ?? createProblem ?? null
  const errorsFor = (path: string) => shown?.errors[path]

  const { draft, setDraft, pending, bodyProps } = usePanelDraft(seed, async (next) => {
    if (readOnly) return false
    if (!field) {
      onDraftCommit?.(next)
      return true
    }
    return !!(await run(() => api.saveField(version.id, next)))
  })
  const set = <K extends keyof DatasetFieldInput>(k: K, v: DatasetFieldInput[K]) => setDraft((d) => ({ ...d, [k]: v }))

  const deps = field ? (version.dependents[field.id] ?? []) : []
  const blocking = deps.filter((d) => d.blocksDelete)
  const isTime = field ? timeFieldIds(version).has(field.id) : false
  const otherFields = version.fields.filter((f) => f.id !== field?.id)
  const colOptions = (columns ?? []).map((c) => ({ value: c.name, label: c.name, hint: c.mappedType ?? 'unreadable' }))
  const canAddColumn = !field && version.settings.sourceObjectKind === 'table'
  const NEW_COLUMN = '__new_column__'

  return (
    <PropertiesPanel
      size="lg"
      label="Field properties"
      title={field ? field.displayName || field.key : 'New field'}
      subtitle={
        <span className="flex items-center gap-2">
          <span className="font-mono">{draft.key || 'field_key'}</span>
          {isTime && (
            <span className="inline-flex items-center gap-1 text-primary">
              <ClockIcon className="size-3" /> time field
            </span>
          )}
          {!field && <span>· not added yet</span>}
          {field && pending && shown && <span className="text-destructive">· not applied</span>}
        </span>
      }
      onClose={onClose}
      bodyProps={bodyProps}
      actions={
        field &&
        !readOnly && (
          <Button
            variant="ghost"
            size="icon-xs"
            aria-label={`Delete ${field.key}`}
            title={blocking.length ? `Kept by ${blocking.map((d) => d.itemKey).join(', ')}` : 'Delete field'}
            disabled={!!blocking.length || saving}
            onClick={async () => {
              if (await runOk(() => api.deleteField(version.id, field.id))) {
                toast.success(`Deleted ${field.key}`)
                onClose()
              }
            }}
          >
            <Trash2Icon />
          </Button>
        )
      }
    >
      {shown && <ProblemBanner problem={shown} onDismiss={() => (setProblem(null), onDismissCreateProblem?.())} onOpen={onOpen} />}
      <fieldset disabled={readOnly} className="contents">
        <PanelSection title="Identity">
          <div className="grid gap-4 @md:grid-cols-2">
            <FormRow label="Field key" htmlFor="f-key" errors={errorsFor('key')} hint={field ? 'Fixed after creation.' : 'Request-facing ID, e.g. as_of_date'}>
              <CommitInput id="f-key" value={draft.key} disabled={!!field} onCommit={(v) => set('key', v)} className="font-mono" aria-invalid={!!errorsFor('key')} />
            </FormRow>
            <FormRow label="Display name" htmlFor="f-name" errors={errorsFor('displayName')}>
              <CommitInput id="f-name" value={draft.displayName} onCommit={(v) => set('displayName', v)} aria-invalid={!!errorsFor('displayName')} />
            </FormRow>
          </div>
        </PanelSection>

        <PanelSection title="Source" description="Where the value comes from. Only the inputs of the chosen kind apply.">
          <div className="grid gap-4 @md:grid-cols-2">
            <FormRow label="Source kind" htmlFor="f-kind" errors={errorsFor('fieldSourceKind')} hint={vocab?.fieldSourceKind.find((t) => t.value === draft.fieldSourceKind)?.description}>
              <VocabSelect
                id="f-kind"
                terms={vocab?.fieldSourceKind}
                value={draft.fieldSourceKind}
                onChange={(k) => setDraft((d) => withSourceKind(d, (k ?? 'column') as DatasetFieldInput['fieldSourceKind']))}
              />
            </FormRow>
            {draft.fieldSourceKind !== 'expression' && (
              <FormRow label={draft.fieldSourceKind === 'json_path' ? 'JSON column' : 'Source column'} htmlFor="f-col" errors={errorsFor('sourceColumn')}>
                <SimpleSelect
                  id="f-col"
                  value={draft.createColumn ? NEW_COLUMN : (draft.sourceColumn ?? '')}
                  placeholder="Choose a column…"
                  options={[...(canAddColumn && draft.fieldSourceKind === 'column' ? [{ value: NEW_COLUMN, label: '+ New column on the table' }] : []), ...colOptions]}
                  className={cn('font-mono', errorsFor('sourceColumn') && 'border-destructive')}
                  onChange={(name) => {
                    if (name === NEW_COLUMN) return setDraft((d) => ({ ...d, createColumn: true, sourceColumn: null }))
                    const c = columns?.find((x) => x.name === name)
                    setDraft((d) =>
                      d.fieldSourceKind === 'column' && c?.mappedType
                        ? { ...d, createColumn: false, sourceColumn: name, dataType: c.mappedType, numericPrecision: c.numericPrecision, numericScale: c.numericScale, isNullable: c.isNullable }
                        : { ...d, createColumn: false, sourceColumn: name },
                    )
                  }}
                />
              </FormRow>
            )}
            {draft.createColumn && draft.fieldSourceKind === 'column' && (
              <FormRow label="New column name" htmlFor="f-newcol" errors={errorsFor('sourceColumn')} hint={`Added to ${version.settings.sourceObject} when the batch is saved, typed from Type below.`}>
                <CommitInput id="f-newcol" value={draft.sourceColumn ?? ''} onCommit={(v) => set('sourceColumn', v || null)} placeholder="risk_segment" className="font-mono" aria-invalid={!!errorsFor('sourceColumn')} />
              </FormRow>
            )}
            {draft.fieldSourceKind === 'json_path' && (
              <FormRow label="JSON path" htmlFor="f-path" errors={errorsFor('jsonPath')} hint="$.a.b[0] form. A value that does not convert to the data type reads as null.">
                <CommitInput id="f-path" value={draft.jsonPath ?? ''} onCommit={(v) => set('jsonPath', v || null)} placeholder="$.risk.rating" className="font-mono" aria-invalid={!!errorsFor('jsonPath')} />
              </FormRow>
            )}
            {draft.fieldSourceKind === 'expression' && (
              <FormRow label="Expression" htmlFor="f-expr" errors={errorsFor('expressionText')} className="@md:col-span-2">
                <ExpressionInput
                  id="f-expr"
                  prefix="f"
                  value={draft.expressionText ?? ''}
                  onChange={(v) => set('expressionText', v || null)}
                  items={otherFields.map((f) => ({ key: f.key, label: f.displayName }))}
                  placeholder="[f.principal] - [f.repaid]"
                  invalid={!!errorsFor('expressionText')}
                  disabled={readOnly}
                />
              </FormRow>
            )}
            {draft.fieldSourceKind === 'json_path' && !field?.materialization && (
              <div className="@md:col-span-2">
                <CheckRow
                  checked={!!draft.materialize}
                  onChange={(v) => set('materialize', v)}
                  label="Materialize when saved"
                  description={`Asks the engine to store this value in mv_${version.settings.sourceObject}. Queries read the JSON path until the view column is ready.`}
                  disabled={readOnly}
                />
              </div>
            )}
          </div>
        </PanelSection>

        <PanelSection title="Type">
          <div className="grid gap-4 @md:grid-cols-4">
            <FormRow label="Data type" htmlFor="f-type" errors={errorsFor('dataType')} className="@md:col-span-2">
              <VocabSelect
                id="f-type"
                terms={vocab?.dataType}
                value={draft.dataType}
                onChange={(t) => setDraft((d) => ({ ...d, dataType: t ?? 'string', numericPrecision: t === 'decimal' ? (d.numericPrecision ?? 18) : null, numericScale: t === 'decimal' ? (d.numericScale ?? 2) : null }))}
              />
            </FormRow>
            {draft.dataType === 'decimal' && (
              <>
                <FormRow label="Precision" htmlFor="f-prec" errors={errorsFor('numericPrecision')}>
                  <CommitInput id="f-prec" inputMode="numeric" value={draft.numericPrecision == null ? '' : String(draft.numericPrecision)} onCommit={(v) => set('numericPrecision', v === '' ? null : Number(v))} />
                </FormRow>
                <FormRow label="Scale" htmlFor="f-scale" errors={errorsFor('numericScale')}>
                  <CommitInput id="f-scale" inputMode="numeric" value={draft.numericScale == null ? '' : String(draft.numericScale)} onCommit={(v) => set('numericScale', v === '' ? null : Number(v))} />
                </FormRow>
              </>
            )}
            <FormRow label="Unit" htmlFor="f-unit" errors={errorsFor('unitKey')} hint="USD, percent, days…" className="@md:col-span-2">
              <CommitInput id="f-unit" value={draft.unitKey ?? ''} onCommit={(v) => set('unitKey', v || null)} />
            </FormRow>
            <div className="flex items-end @md:col-span-2">
              <CheckRow checked={draft.isNullable} onChange={(v) => set('isNullable', v)} label="Nullable" disabled={readOnly} />
            </div>
          </div>
        </PanelSection>

        <PanelSection title="Capabilities">
          <div className="grid gap-3 @md:grid-cols-2">
            {FLAGS.map((fl) => (
              <CheckRow key={fl.key} checked={draft[fl.key]} onChange={(v) => set(fl.key, v)} label={fl.label} description={fl.description} disabled={readOnly} />
            ))}
            <CheckRow checked={draft.isEnabled} onChange={(v) => set('isEnabled', v)} label="Enabled" description="Disabled fields are hidden from requests." disabled={readOnly} />
          </div>
        </PanelSection>
      </fieldset>

      {field && field.fieldSourceKind === 'json_path' && draft.fieldSourceKind === 'json_path' && <MaterializationSection version={version} field={field} />}
      {field && (
        <PanelSection title="Used by">
          <DependentsList dependents={deps} onOpen={(d) => onOpen(d.tab, d.itemId)} empty="No grain, binding or measure reads this field." />
          {!!blocking.length && <p className="text-xs text-muted-foreground">Remove these references before deleting the field.</p>}
        </PanelSection>
      )}
    </PropertiesPanel>
  )
}

/* ───────────────────────── materialization ───────────────────────── */

type MatCall = (api: ReturnType<typeof useDatasetsApi>, versionId: string, fieldId: string) => ReturnType<ReturnType<typeof useDatasetsApi>['materializeField']>

const materialize: MatCall = (api, v, f) => api.materializeField(v, f)
const refreshMat: MatCall = (api, v, f) => api.refreshMaterialization(v, f)
const dematerialize: MatCall = (api, v, f) => api.dematerializeField(v, f)

function MaterializationMenuItems({ field, onRun }: { field: DatasetField; onRun: (call: MatCall, done: (f: DatasetField) => string) => void }) {
  const m = field.materialization
  return (
    <>
      {(!m || m.state === 'failed') && (
        <DropdownMenuItem onClick={() => onRun(m ? refreshMat : materialize, (f) => `Building ${f.materialization?.viewName}.${f.materialization?.columnName}`)}>
          <DatabaseZapIcon /> {m ? 'Retry materialization' : 'Materialize in view'}
        </DropdownMenuItem>
      )}
      {m?.state === 'ready' && (
        <DropdownMenuItem onClick={() => onRun(refreshMat, () => `Refreshing ${field.key}`)}>
          <RefreshCwIcon /> Refresh stored values
        </DropdownMenuItem>
      )}
      {m && (
        <DropdownMenuItem onClick={() => onRun(dematerialize, () => `${field.key} reads the JSON path again`)}>
          <UndoIcon /> Remove from view
        </DropdownMenuItem>
      )}
    </>
  )
}

function MaterializationStatus({ field }: { field: DatasetField }) {
  if (field.fieldSourceKind !== 'json_path') return <span className="text-xs text-muted-foreground">—</span>
  const m = field.materialization
  if (!m) return <span className="text-xs text-muted-foreground">Reads JSON</span>
  return <MaterializationBadge m={m} />
}

function MaterializationBadge({ m }: { m: DatasetFieldMaterialization }) {
  if (m.state === 'building')
    return (
      <span className="inline-flex items-center gap-1.5 text-xs text-muted-foreground">
        <Loader2Icon aria-hidden className="size-3.5 animate-spin" /> Building
      </span>
    )
  if (m.state === 'failed')
    return (
      <Chip tone="warn" title={m.error ?? undefined}>
        failed
      </Chip>
    )
  return (
    <span className="inline-flex items-center gap-1.5 font-mono text-xs" title={`${m.viewName}.${m.columnName}`}>
      <span aria-hidden className="size-2 rounded-full bg-primary" />
      <span className="max-w-48 truncate">{m.viewName}</span>
    </span>
  )
}

const when = (iso: string | null) => (iso ? new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' }) : '—')

function MaterializationSection({ version, field }: { version: DatasetVersion; field: DatasetField }) {
  const api = useDatasetsApi()
  const call = useApiCall()
  const m = field.materialization
  const canAct = version.status !== 'retired'
  const go = async (fn: MatCall, msg: string) => {
    const r = await call.run(() => fn(api, version.id, field.id))
    if (r) toast.success(msg)
  }
  return (
    <PanelSection title="Materialization">
      {!m ? (
        <p className="text-xs leading-relaxed text-muted-foreground">
          Every query extracts <span className="font-mono">{field.jsonPath}</span> from <span className="font-mono">{field.sourceColumn}</span> row by row. Materializing asks the database engine to store the value in the
          source&apos;s materialized view so grouping and filtering read a typed column.
        </p>
      ) : (
        <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1.5 text-xs">
          <dt className="text-muted-foreground">State</dt>
          <dd>
            {m.state === 'ready' ? (
              <span className="inline-flex items-center gap-1.5">
                <span aria-hidden className="size-2 rounded-full bg-primary" /> Ready
              </span>
            ) : (
              <MaterializationBadge m={m} />
            )}
          </dd>
          <dt className="text-muted-foreground">View</dt>
          <dd className="truncate font-mono">{m.viewName}</dd>
          <dt className="text-muted-foreground">Column</dt>
          <dd className="truncate font-mono">{m.columnName}</dd>
          <dt className="text-muted-foreground">Requested</dt>
          <dd className="tabular-nums">{when(m.requestedAt)}</dd>
          <dt className="text-muted-foreground">Refreshed</dt>
          <dd className="tabular-nums">{when(m.refreshedAt)}</dd>
        </dl>
      )}
      {m?.error && <p className="text-xs text-destructive">{m.error}</p>}
      {call.problem && <ProblemBanner problem={call.problem} onDismiss={() => call.setProblem(null)} />}
      {m?.state === 'building' && <p className="text-xs text-muted-foreground">Queries keep reading the JSON path until the view column is ready.</p>}
      {canAct && (
        <div className="flex flex-wrap gap-2">
          {!m && (
            <Button size="sm" disabled={call.saving} onClick={() => go(materialize, `Building ${field.key} in mv_${version.settings.sourceObject}`)}>
              <DatabaseZapIcon /> Materialize in view
            </Button>
          )}
          {m && m.state !== 'building' && (
            <Button size="sm" variant="outline" disabled={call.saving} onClick={() => go(refreshMat, `Refreshing ${field.key}`)}>
              <RefreshCwIcon /> {m.state === 'failed' ? 'Retry' : 'Refresh'}
            </Button>
          )}
          {m && (
            <Button size="sm" variant="ghost" disabled={call.saving} onClick={() => go(dematerialize, `${field.key} reads the JSON path again`)}>
              <UndoIcon /> Remove
            </Button>
          )}
        </div>
      )}
    </PanelSection>
  )
}
