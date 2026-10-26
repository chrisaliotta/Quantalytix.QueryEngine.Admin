import { AlertTriangleIcon, ArrowRightIcon, ClockIcon, PlusIcon, Trash2Icon } from 'lucide-react'
import { useEffect, useRef, useState, type KeyboardEvent } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { useCatalog, useDatasetsApi, useRefreshDatasets, useVocabularies } from '@/lib/datasets/client'
import type { BindingInput, DatasetBinding, DatasetVersion, Periodicity, TenantCatalog, TimeBindingExtension, ValidationProblemDetails } from '@/lib/datasets/contract'
import { cn } from '@/lib/utils'
import type { SectionProps } from './dataset-detail'
import { InlineSelect, InlineSwitch, PanelSection, PropertiesPanel, SplitView, focusRowClass, usePanelDraft } from '@/components/shell/inline-table'
import {
  CheckRow,
  Chip,
  DependentsList,
  FormRow,
  NONE,
  ProblemBanner,
  SectionHeader,
  RowEditActions,
  Td,
  Th,
  VocabSelect,
  useApiCall,
} from './shared'

const PERIOD_ORDER: Periodicity[] = ['day', 'month', 'quarter', 'year']

export function bindingDimensionId(b: Pick<DatasetBinding, 'bindingKind' | 'dimensionId' | 'dimensionRoleId'>, catalog: TenantCatalog | undefined) {
  if (b.bindingKind === 'time') return b.dimensionId
  return catalog?.roles.find((r) => r.id === b.dimensionRoleId)?.dimensionId ?? null
}

export function bindingLabel(b: DatasetBinding, version: DatasetVersion, catalog: TenantCatalog | undefined) {
  if (b.bindingKind === 'time') return version.fields.find((f) => f.id === b.sourceFieldId)?.fieldKey ?? 'time'
  return catalog?.roles.find((r) => r.id === b.dimensionRoleId)?.roleKey ?? '?'
}

/** Enabled measures aggregating on this time binding that lack uniform interval estimates. */
function measuresBlockingFinerPeriods(version: DatasetVersion, bindingId: string | undefined) {
  if (!bindingId) return []
  return version.measures.filter((m) => {
    if (!m.isEnabled) return false
    const required = m.timePolicy?.requiredTimeBindingId ?? null
    const onThis = required === bindingId || (!required && version.settings.defaultTimeBindingId === bindingId)
    return onThis && m.timePolicy?.partialIntervalBehavior !== 'uniform_estimate'
  })
}

const NEW_ROW_ID = '__new__'
const scrollToRow = (id: string) => requestAnimationFrame(() => document.querySelector(`[data-row-id="${CSS.escape(id)}"]`)?.scrollIntoView({ block: 'nearest' }))

/** References that belong to the old dimension no longer apply. */
const clearRefs = { mappingSetId: null, bridgeId: null, unknownMemberKey: null } as const

/** Switching kind keeps only the inputs that still mean something for the new kind. */
function withBindingKind(x: BindingInput, k: BindingInput['bindingKind']): BindingInput {
  return {
    ...x,
    ...clearRefs,
    bindingKind: k,
    sourceFieldId: k === 'bridge' ? null : x.sourceFieldId,
    dimensionRoleId: k === 'time' ? null : x.dimensionRoleId,
    dimensionId: k === 'time' ? x.dimensionId : null,
    sourceDimensionBindingId: k === 'bridge' ? x.sourceDimensionBindingId : null,
    time: k === 'time' ? (x.time ?? blankTime()) : null,
  }
}

function roleOptionsFor(d: BindingInput, version: DatasetVersion, catalog: TenantCatalog | undefined, excludeId?: string) {
  const bound = new Set(version.bindings.filter((b) => b.id !== excludeId && b.dimensionRoleId).map((b) => b.dimensionRoleId))
  const kindOf = (dimensionId: string) => catalog?.dimensions.find((x) => x.id === dimensionId)?.dimensionKind
  return (catalog?.roles ?? [])
    .filter((r) => !bound.has(r.id))
    .filter((r) => (d.bindingKind === 'range' ? kindOf(r.dimensionId) === 'range' : kindOf(r.dimensionId) !== 'time'))
    .map((r) => ({ value: r.id, label: r.displayName, hint: catalog?.dimensions.find((x) => x.id === r.dimensionId)?.displayName }))
}

/** Role → source field: the arrow reads as "this role is classified from that field". */
function SourceFieldCell({ children }: { children: React.ReactNode }) {
  return (
    <span className="flex items-center gap-2">
      <ArrowRightIcon aria-hidden className="size-3.5 shrink-0 text-muted-foreground" />
      <span className="sr-only">from field</span>
      {children ?? <span className="text-xs text-muted-foreground">—</span>}
    </span>
  )
}

function mappingSetOptionsFor(d: BindingInput, catalog: TenantCatalog | undefined) {
  const dimId = bindingDimensionId(d, catalog)
  return (catalog?.mappingSets ?? []).filter((m) => m.dimensionId === dimId).map((m) => ({ value: m.id, label: m.displayName, hint: m.mappingSetKey }))
}

function fieldOptionsFor(d: BindingInput, version: DatasetVersion) {
  const isTime = d.bindingKind === 'time'
  return version.fields
    .filter((f) => !isTime || f.dataType === 'date' || f.dataType === 'timestamp' || f.dataType === 'datetime')
    .map((f) => ({ value: f.id, label: f.fieldKey, hint: f.dataType }))
}

export function BindingsSection({ version, readOnly, editingId, onEdit, onOpen }: SectionProps) {
  const api = useDatasetsApi()
  const refresh = useRefreshDatasets()
  const { data: catalog } = useCatalog()
  const { data: vocab } = useVocabularies()
  const { run, problem, setProblem } = useApiCall()
  const [newDraft, setNewDraftState] = useState<BindingInput | null>(null)
  const [newProblem, setNewProblem] = useState<ValidationProblemDetails | null>(null)
  const [creating, setCreating] = useState(false)
  const newRef = useRef(newDraft)
  const kindLabel = (k: string) => vocab?.bindingKind.find((t) => t.value === k)?.label ?? k
  const kindOptions = (vocab?.bindingKind ?? []).map((t) => ({ value: t.value, label: t.label }))
  const fieldKey = (id: string | null) => (id ? (version.fields.find((f) => f.id === id)?.fieldKey ?? '?') : null)
  const focused = editingId ? version.bindings.find((b) => b.id === editingId) : undefined

  const setNewDraft = (d: BindingInput | null) => {
    newRef.current = d
    setNewDraftState(d)
  }
  const editNew = (update: (d: BindingInput) => BindingInput) => {
    if (!newRef.current) return
    setNewDraft(update(newRef.current))
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
    setNewDraft(blankBinding())
    scrollToRow(NEW_ROW_ID)
  }

  async function createNew() {
    const input = newRef.current
    if (!input || creating) return false
    setCreating(true)
    const r = await api.saveBinding(version.id, input)
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
  const patch = (b: DatasetBinding, p: Partial<BindingInput>) => run(() => api.saveBinding(version.id, { ...b, ...p }))
  const toggle = (b: DatasetBinding, key: 'isGroupable' | 'isFilterable' | 'isEnabled', label: string, disabled = false) => (
    <button
      type="button"
      aria-pressed={b[key]}
      disabled={readOnly || disabled}
      onClick={() => patch(b, { [key]: !b[key] })}
      className={cn(
        'rounded-sm border px-1.5 py-0.5 font-mono text-[11px] leading-none transition-colors disabled:cursor-not-allowed',
        b[key] ? 'border-primary/40 bg-primary/10 text-primary' : 'border-dashed text-muted-foreground/70',
        !readOnly && !disabled && 'hover:border-primary/60',
      )}
    >
      {label}
    </button>
  )

  const side = newDraft ? (
    <BindingPanel
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
    <BindingPanel key={focused.id} version={version} readOnly={readOnly} existing={focused} seed={focused} onClose={() => onEdit(null)} onOpen={onOpen} />
  ) : null

  return (
    <div className="flex flex-col gap-4">
      <SectionHeader
        title="Bindings"
        help="Give a field business meaning: classify each row into a dimension member so queries group and filter by hierarchy, mapping, range band, bridge or time axis. Requests address a binding by its role key; a time binding by its field key."
        action={
          !readOnly && (
            <RowEditActions adding={!!newDraft} creating={creating} onStartNew={startNew} onCreate={createNew} onCancelNew={cancelNew} />
          )
        }
      />
      {problem && <ProblemBanner problem={problem} onDismiss={() => setProblem(null)} onOpenDependent={(d) => onOpen(d.tab, d.itemId)} />}
      <SplitView panel={side}>
      <div className="overflow-x-auto rounded-lg border bg-card">
        <table className="w-full text-sm">
          <thead className="border-b bg-muted/40">
            <tr>
              <Th>Kind</Th>
              <Th>Role</Th>
              <Th>Source field</Th>
              <Th>Mapping set / bridge</Th>
              <Th>References</Th>
              <Th>Flags</Th>
              <Th>Enabled</Th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {version.bindings.map((b) => {
              const role = catalog?.roles.find((r) => r.id === b.dimensionRoleId)
              const dim = catalog?.dimensions.find((d) => d.id === bindingDimensionId(b, catalog))
              const mappingSet = b.mappingSetId ? catalog?.mappingSets.find((m) => m.id === b.mappingSetId) : undefined
              const bridge = b.bridgeId ? catalog?.bridges.find((x) => x.id === b.bridgeId) : undefined
              const mappingOrBridge = mappingSet?.displayName ?? bridge?.displayName
              const refs = [
                b.sourceDimensionBindingId && `from ${bindingLabel(version.bindings.find((x) => x.id === b.sourceDimensionBindingId)!, version, catalog)}`,
                b.unknownMemberKey && `unknown → ${b.unknownMemberKey}`,
              ].filter(Boolean) as string[]
              const isDefault = version.settings.defaultTimeBindingId === b.id
              return (
                <tr key={b.id} onClick={() => focus(b.id)} className={cn(focusRowClass(b.id === editingId), !b.isEnabled && 'text-muted-foreground')}>
                  <Td className="text-xs">{kindLabel(b.bindingKind)}</Td>
                  <Td>
                    <span className="flex items-center gap-1.5">
                      {b.bindingKind === 'time' && <ClockIcon aria-hidden className="size-3.5 shrink-0 text-primary" />}
                      <span className="text-sm">{b.bindingKind === 'time' ? 'Time' : (role?.displayName ?? '?')}</span>
                      {dim && <span className="text-xs text-muted-foreground">({dim.displayName})</span>}
                    </span>
                  </Td>
                  <Td>
                    {b.bindingKind === 'bridge' ? <span className="text-xs text-muted-foreground">—</span> : <SourceFieldCell>{fieldKey(b.sourceFieldId)}</SourceFieldCell>}
                  </Td>
                  <Td className="text-xs">{mappingOrBridge ?? <span className="text-muted-foreground">—</span>}</Td>
                  <Td>
                    <span className="flex flex-wrap gap-1">
                      {refs.length ? refs.map((r) => <Chip key={r}>{r}</Chip>) : <span className="text-xs text-muted-foreground">—</span>}
                    </span>
                  </Td>
                  <Td>
                    <span className="flex flex-wrap items-center gap-1">
                      {isDefault && <Chip tone="primary">default time</Chip>}
                      {b.isRequired && <Chip tone="muted">required</Chip>}
                      {toggle(b, 'isGroupable', 'group')}
                      {toggle(b, 'isFilterable', 'filter')}
                    </span>
                  </Td>
                  <Td>
                    <InlineSwitch aria-label={`Binding ${bindingLabel(b, version, catalog)} enabled`} checked={b.isEnabled} onCommit={(v) => !readOnly && !b.isRequired && patch(b, { isEnabled: v })} />
                  </Td>
                </tr>
              )
            })}
            {!readOnly && newDraft && (
              <NewBindingRow
                draft={newDraft}
                problem={newProblem}
                creating={creating}
                version={version}
                catalog={catalog}
                kindOptions={kindOptions}
                onChange={editNew}
                onSubmit={createNew}
                onCancel={cancelNew}
              />
            )}
            {!version.bindings.length && !newDraft && (
              <tr>
                <Td colSpan={7} className="py-8 text-center text-muted-foreground">
                  {readOnly ? 'No bindings. Fields marked groupable can still be grouped directly.' : 'No bindings yet. Use “Add new row” to bind a field to a dimension.'}
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

const blankTime = (): TimeBindingExtension => ({
  timeValueType: 'date',
  sourceTimeZone: null,
  defaultQueryTimeZone: 'UTC',
  observationKind: 'snapshot',
  nativePeriodicity: 'day',
  intervalEndFieldId: null,
  allowedPeriodicities: ['day', 'month', 'quarter', 'year'],
  cumulativeOrigin: 'output_range_start',
  cumulativeFixedStart: null,
})

const blankBinding = (): BindingInput => ({
  bindingKind: 'direct',
  dimensionRoleId: null,
  dimensionId: null,
  sourceFieldId: null,
  mappingSetId: null,
  bridgeId: null,
  sourceDimensionBindingId: null,
  raggedBehavior: null,
  defaultPeriodAlignment: 'observation',
  unmappedBehavior: null,
  unknownMemberKey: null,
  isRequired: false,
  isGroupable: true,
  isFilterable: true,
  isEnabled: true,
  time: null,
})

/** The pending binding: a selected, editable row bound to the same draft as the open properties panel. */
function NewBindingRow({
  draft,
  problem,
  creating,
  version,
  catalog,
  kindOptions,
  onChange,
  onSubmit,
  onCancel,
}: {
  draft: BindingInput
  problem: ValidationProblemDetails | null
  creating: boolean
  version: DatasetVersion
  catalog: TenantCatalog | undefined
  kindOptions: { value: string; label: string }[]
  onChange: (update: (d: BindingInput) => BindingInput) => void
  onSubmit: () => void
  onCancel: () => void
}) {
  const rowRef = useRef<HTMLTableRowElement>(null)
  const messages = Object.values(problem?.errors ?? {}).flat()
  const isTime = draft.bindingKind === 'time'
  const dim = catalog?.dimensions.find((x) => x.id === bindingDimensionId(draft, catalog))
  const timeDims = (catalog?.dimensions ?? []).filter((x) => x.dimensionKind === 'time').map((x) => ({ value: x.id, label: x.displayName }))
  const fieldOptions = fieldOptionsFor(draft, version)
  const sourceBinding = draft.sourceDimensionBindingId ? version.bindings.find((x) => x.id === draft.sourceDimensionBindingId) : undefined
  const sourceDimId = sourceBinding ? bindingDimensionId(sourceBinding, catalog) : null
  const bridgeOptions = (catalog?.bridges ?? [])
    .filter((b) => b.toDimensionId === dim?.id && (!sourceDimId || b.fromDimensionId === sourceDimId))
    .map((b) => ({ value: b.id, label: b.displayName, hint: b.bridgeKey }))

  useEffect(() => {
    rowRef.current?.querySelector<HTMLElement>('button')?.focus()
  }, [])

  const onKeyDown = (e: KeyboardEvent<HTMLTableRowElement>) => {
    if (e.defaultPrevented || e.nativeEvent.isComposing || e.keyCode === 229) return
    if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) {
      e.preventDefault()
      onSubmit()
    } else if (e.key === 'Escape') {
      e.preventDefault()
      e.stopPropagation()
      onCancel()
    }
  }

  return (
    <tr ref={rowRef} data-row-id={NEW_ROW_ID} aria-selected aria-label="New binding" onKeyDown={onKeyDown} className={cn(focusRowClass(true), 'align-top')}>
      <Td>
        <InlineSelect aria-label="New binding: kind" value={draft.bindingKind} options={kindOptions} onCommit={(k) => onChange((x) => withBindingKind(x, k as BindingInput['bindingKind']))} className="w-28 text-xs" />
      </Td>
      <Td>
        <span className="flex items-center gap-1.5">
          {isTime ? (
            <InlineSelect
              aria-label="New binding: time dimension"
              value={draft.dimensionId ?? ''}
              placeholder="Time dimension…"
              options={timeDims}
              onCommit={(v) => onChange((x) => ({ ...x, ...clearRefs, dimensionId: v }))}
              className="w-40 text-xs"
            />
          ) : (
            <InlineSelect
              aria-label="New binding: role"
              value={draft.dimensionRoleId ?? ''}
              placeholder="Choose a role…"
              options={roleOptionsFor(draft, version, catalog)}
              onCommit={(v) => onChange((x) => ({ ...x, ...clearRefs, dimensionRoleId: v }))}
              className="w-40 text-xs"
            />
          )}
          {dim && !isTime && <span className="text-xs text-muted-foreground">({dim.displayName})</span>}
        </span>
      </Td>
      <Td>
        {draft.bindingKind === 'bridge' ? (
          <span className="text-xs text-muted-foreground">—</span>
        ) : (
          <SourceFieldCell>
            <InlineSelect
              aria-label="New binding: source field"
              value={draft.sourceFieldId ?? ''}
              placeholder="Choose a field…"
              options={fieldOptions}
              onCommit={(v) => onChange((x) => ({ ...x, sourceFieldId: v }))}
              className="w-40 font-mono text-xs"
            />
          </SourceFieldCell>
        )}
      </Td>
      <Td>
        {draft.bindingKind === 'bridge' ? (
          <InlineSelect
            aria-label="New binding: bridge"
            value={draft.bridgeId ?? ''}
            placeholder={!dim ? 'Pick a role first' : bridgeOptions.length ? 'Choose a bridge…' : 'No matching bridge'}
            options={bridgeOptions}
            onCommit={(v) => onChange((x) => ({ ...x, bridgeId: v }))}
            className="w-40 text-xs"
          />
        ) : draft.bindingKind === 'mapped' ? (
          <InlineSelect
            aria-label="New binding: mapping set"
            value={draft.mappingSetId ?? ''}
            placeholder={dim ? 'Choose a set…' : 'Pick a role first'}
            options={mappingSetOptionsFor(draft, catalog)}
            onCommit={(v) => onChange((x) => ({ ...x, mappingSetId: v }))}
            className="w-40 text-xs"
          />
        ) : (
          <span className="block pt-2 text-xs text-muted-foreground">—</span>
        )}
      </Td>
      <Td colSpan={3}>
        <span className="flex items-start justify-between gap-3 pt-1">
          <span role={messages.length ? 'alert' : undefined} className={cn('text-xs text-pretty', messages.length ? 'text-destructive' : 'text-muted-foreground')}>
            {messages.length ? messages.join(' ') : 'References, members and flags in the panel.'}
          </span>
        </span>
      </Td>
    </tr>
  )
}

function BindingPanel({
  version,
  readOnly,
  existing,
  seed,
  onClose,
  onOpen,
  onDraftCommit,
  createProblem,
  onDismissCreateProblem,
}: {
  version: DatasetVersion
  readOnly: boolean
  existing?: DatasetBinding
  seed: BindingInput
  onClose: () => void
  onOpen: SectionProps['onOpen']
  /** New bindings only: panel edits update the pending row's draft instead of saving. */
  onDraftCommit?: (draft: BindingInput) => void
  createProblem?: ValidationProblemDetails | null
  onDismissCreateProblem?: () => void
}) {
  const api = useDatasetsApi()
  const { data: vocab } = useVocabularies()
  const { data: catalog } = useCatalog()
  const { run, runOk, saving, problem: callProblem, setProblem } = useApiCall()
  const problem = callProblem ?? createProblem ?? null
  const errorsFor = (path: string) => problem?.errors[path]
  const { draft: d, setDraft: setD, pending, bodyProps } = usePanelDraft(seed, async (next) => {
    if (readOnly) return false
    if (!existing) {
      onDraftCommit?.(next)
      return true
    }
    return !!(await run(() => api.saveBinding(version.id, next)))
  })

  const set = <K extends keyof BindingInput>(k: K, v: BindingInput[K]) => setD((x) => ({ ...x, [k]: v }))
  const setTime = <K extends keyof TimeBindingExtension>(k: K, v: TimeBindingExtension[K]) => setD((x) => ({ ...x, time: { ...(x.time ?? blankTime()), [k]: v } }))

  const isTime = d.bindingKind === 'time'
  const dimId = bindingDimensionId(d, catalog)
  const dim = catalog?.dimensions.find((x) => x.id === dimId)
  const roleOptions = roleOptionsFor(d, version, catalog, existing?.id)
  const timeDims = (catalog?.dimensions ?? []).filter((x) => x.dimensionKind === 'time')
  const fieldOptions = fieldOptionsFor(d, version)
  const sourceBinding = version.bindings.find((b) => b.id === d.sourceDimensionBindingId)
  const sourceDimId = sourceBinding ? bindingDimensionId(sourceBinding, catalog) : null
  const bridges = (catalog?.bridges ?? []).filter((b) => b.toDimensionId === dimId && (!sourceDimId || b.fromDimensionId === sourceDimId))
  const mappingSets = (catalog?.mappingSets ?? []).filter((m) => m.dimensionId === dimId)

  const deps = existing ? (version.dependents[existing.id] ?? []) : []
  const blocking = deps.filter((x) => x.blocksDelete)
  const isDefault = existing && version.settings.defaultTimeBindingId === existing.id
  const native = d.time?.nativePeriodicity ?? null
  const finerTicked = native ? (d.time?.allowedPeriodicities ?? []).filter((p) => PERIOD_ORDER.indexOf(p) < PERIOD_ORDER.indexOf(native)) : []
  const offenders = finerTicked.length ? measuresBlockingFinerPeriods(version, existing?.id) : []

  const title = existing ? (isTime ? `Time · ${version.fields.find((f) => f.id === existing.sourceFieldId)?.fieldKey ?? ''}` : (catalog?.roles.find((r) => r.id === existing.dimensionRoleId)?.roleKey ?? 'Binding')) : 'New binding'

  return (
    <PropertiesPanel
      size="lg"
      label="Binding properties"
      title={<span className={cn(existing && !isTime && 'font-mono')}>{title}</span>}
      subtitle={
        !existing ? (
          'Not added yet · needs a kind, role and source field.'
        ) : pending && problem ? (
          <span className="text-destructive">Not applied — fix the highlighted inputs.</span>
        ) : isDefault ? (
          'Default time binding'
        ) : undefined
      }
      onClose={onClose}
      bodyProps={bodyProps}
      actions={
        existing &&
        !readOnly && (
          <Button
            variant="ghost"
            size="icon-xs"
            aria-label="Delete binding"
            disabled={!!blocking.length || existing.isRequired || saving}
            title={existing.isRequired ? 'Required bindings are fixtures.' : blocking.length ? `Kept by ${blocking.map((x) => x.itemKey).join(', ')}` : 'Delete binding'}
            onClick={async () => {
              if (await runOk(() => api.deleteBinding(version.id, existing.id))) {
                toast.success('Binding deleted')
                onClose()
              }
            }}
          >
            <Trash2Icon />
          </Button>
        )
      }
    >
      {problem && <ProblemBanner problem={problem} onDismiss={() => (setProblem(null), onDismissCreateProblem?.())} onOpenDependent={(x) => onOpen(x.tab, x.itemId)} />}
        <fieldset disabled={readOnly} className="contents">
          <PanelSection title="Kind and references" description={vocab?.bindingKind.find((t) => t.value === d.bindingKind)?.description}>
            <div className="grid gap-4 @md:grid-cols-2">
              <FormRow label="Binding kind" htmlFor="b-kind" errors={errorsFor('bindingKind')}>
                <VocabSelect
                  id="b-kind"
                  terms={vocab?.bindingKind}
                  value={d.bindingKind}
                  onChange={(k) => setD((x) => withBindingKind(x, k ?? 'direct'))}
                />
              </FormRow>
              {isTime ? (
                <FormRow label="Time dimension" htmlFor="b-dim" errors={errorsFor('dimensionId')}>
                  <SimpleSelect
                    id="b-dim"
                    value={d.dimensionId ?? ''}
                    placeholder="Choose…"
                    onChange={(v) => setD((x) => ({ ...x, ...clearRefs, dimensionId: v }))}
                    options={timeDims.map((x) => ({ value: x.id, label: x.displayName, hint: x.dimensionKey }))}
                  />
                </FormRow>
              ) : (
                <FormRow label="Role" htmlFor="b-role" errors={errorsFor('dimensionRoleId')} hint="Roles already bound in this version are hidden.">
                  <SimpleSelect
                    id="b-role"
                    value={d.dimensionRoleId ?? ''}
                    placeholder="Choose a role…"
                    onChange={(v) => setD((x) => ({ ...x, ...clearRefs, dimensionRoleId: v }))}
                    options={roleOptions}
                    className="font-mono"
                  />
                </FormRow>
              )}
              {d.bindingKind !== 'bridge' && (
                <FormRow label="Source field" htmlFor="b-field" errors={errorsFor('sourceFieldId')} hint={isTime ? 'A date or timestamp field.' : undefined}>
                  <SimpleSelect
                    id="b-field"
                    value={d.sourceFieldId ?? ''}
                    placeholder="Choose a field…"
                    onChange={(v) => set('sourceFieldId', v)}
                    options={fieldOptions}
                    className="font-mono"
                  />
                </FormRow>
              )}
              {d.bindingKind === 'mapped' && (
                <FormRow label="Mapping set" htmlFor="b-map" errors={errorsFor('mappingSetId')} hint={dim ? `Sets of ${dim.displayName}. The query's assumption set pins the version read.` : 'Choose a role first.'}>
                  <SimpleSelect
                    id="b-map"
                    value={d.mappingSetId ?? ''}
                    placeholder="Choose…"
                    onChange={(v) => set('mappingSetId', v)}
                    options={mappingSets.map((m) => ({ value: m.id, label: m.displayName, hint: m.mappingSetKey }))}
                  />
                </FormRow>
              )}
              {d.bindingKind === 'bridge' && (
                <>
                  <FormRow label="Source binding" htmlFor="b-src" errors={errorsFor('sourceDimensionBindingId')}>
                    <SimpleSelect
                      id="b-src"
                      value={d.sourceDimensionBindingId ?? ''}
                      placeholder="Choose…"
                      onChange={(v) => setD((x) => ({ ...x, bridgeId: null, sourceDimensionBindingId: v }))}
                      options={version.bindings.filter((b) => b.id !== existing?.id && b.bindingKind !== 'time').map((b) => ({ value: b.id, label: bindingLabel(b, version, catalog), hint: b.bindingKind }))}
                      className="font-mono"
                    />
                  </FormRow>
                  <FormRow label="Bridge" htmlFor="b-bridge" errors={errorsFor('bridgeId')} hint="Bridges leading from the source binding's dimension into this role's dimension.">
                    <SimpleSelect
                      id="b-bridge"
                      value={d.bridgeId ?? ''}
                      placeholder={bridges.length ? 'Choose…' : 'No matching bridge'}
                      onChange={(v) => set('bridgeId', v)}
                      options={bridges.map((b) => ({ value: b.id, label: b.displayName, hint: b.bridgeKey }))}
                    />
                  </FormRow>
                </>
              )}
            </div>
          </PanelSection>

          <PanelSection title="Members" description="How rows that don't land neatly on a member are treated.">
            <div className="grid gap-4 @md:grid-cols-2">
              <FormRow label="Ragged behavior" htmlFor="b-ragged" errors={errorsFor('raggedBehavior')}>
                <VocabSelect id="b-ragged" terms={vocab?.raggedBehavior} value={d.raggedBehavior} noneLabel="Not set" onChange={(v) => set('raggedBehavior', v)} />
              </FormRow>
              <FormRow label="Period alignment" htmlFor="b-align" errors={errorsFor('defaultPeriodAlignment')}>
                <VocabSelect id="b-align" terms={vocab?.periodAlignment} value={d.defaultPeriodAlignment} onChange={(v) => set('defaultPeriodAlignment', v ?? 'observation')} />
              </FormRow>
              <FormRow label="Unmapped values" htmlFor="b-unmapped" errors={errorsFor('unmappedBehavior')}>
                <VocabSelect
                  id="b-unmapped"
                  terms={vocab?.unmappedBehavior}
                  value={d.unmappedBehavior}
                  noneLabel="Not set"
                  onChange={(v) => setD((x) => ({ ...x, unmappedBehavior: v, unknownMemberKey: v === 'unknown' ? x.unknownMemberKey : null }))}
                />
              </FormRow>
              {d.unmappedBehavior === 'unknown' && (
                <FormRow label="Unknown member" htmlFor="b-unknown" errors={errorsFor('unknownMemberKey')} hint={dim ? `From the published version of ${dim.displayName}.` : 'Choose a role first.'}>
                  <SimpleSelect
                    id="b-unknown"
                    value={d.unknownMemberKey ?? ''}
                    placeholder="Choose…"
                    onChange={(v) => set('unknownMemberKey', v)}
                    options={(dim?.publishedMembers ?? []).map((m) => ({ value: m.key, label: m.label, hint: m.key }))}
                  />
                </FormRow>
              )}
            </div>
          </PanelSection>

          {isTime && d.time && (
            <PanelSection title="Time axis">
              <div className="grid gap-4 @md:grid-cols-2">
                <FormRow label="Value type" htmlFor="t-vt" errors={errorsFor('time.timeValueType')}>
                  <VocabSelect id="t-vt" terms={vocab?.timeValueType} value={d.time.timeValueType} onChange={(v) => setD((x) => ({ ...x, time: { ...x.time!, timeValueType: v ?? 'date', sourceTimeZone: v === 'timestamp' ? x.time!.sourceTimeZone : null } }))} />
                </FormRow>
                {d.time.timeValueType === 'timestamp' && (
                  <FormRow label="Source time zone" htmlFor="t-stz" errors={errorsFor('time.sourceTimeZone')}>
                    <VocabSelect id="t-stz" terms={vocab?.timeZone} value={d.time.sourceTimeZone} noneLabel="Not set" onChange={(v) => setTime('sourceTimeZone', v)} />
                  </FormRow>
                )}
                <FormRow label="Default query time zone" htmlFor="t-qtz" errors={errorsFor('time.defaultQueryTimeZone')}>
                  <VocabSelect id="t-qtz" terms={vocab?.timeZone} value={d.time.defaultQueryTimeZone} onChange={(v) => setTime('defaultQueryTimeZone', v ?? 'UTC')} />
                </FormRow>
                <FormRow label="Observation kind" htmlFor="t-ok" errors={errorsFor('time.observationKind')} hint={vocab?.observationKind.find((t) => t.value === d.time!.observationKind)?.description}>
                  <VocabSelect id="t-ok" terms={vocab?.observationKind} value={d.time.observationKind} onChange={(v) => setTime('observationKind', v ?? 'snapshot')} />
                </FormRow>
                <FormRow label="Native periodicity" htmlFor="t-np" errors={errorsFor('time.nativePeriodicity')}>
                  <VocabSelect id="t-np" terms={vocab?.periodicity} value={d.time.nativePeriodicity} noneLabel="Irregular" onChange={(v) => setTime('nativePeriodicity', v)} />
                </FormRow>
                <FormRow label="Interval end field" htmlFor="t-end" errors={errorsFor('time.intervalEndFieldId')} hint="Exclusive end, when intervals can't come from the periodicity.">
                  <SimpleSelect
                    id="t-end"
                    value={d.time.intervalEndFieldId ?? NONE}
                    onChange={(v) => setTime('intervalEndFieldId', v === NONE ? null : v)}
                    options={[{ value: NONE, label: 'None' }, ...fieldOptions.filter((f) => f.value !== d.sourceFieldId)]}
                    className="font-mono"
                  />
                </FormRow>
              </div>

              <div className="flex flex-col gap-2">
                <span className="text-sm font-medium">Allowed periodicities</span>
                <div className="flex flex-wrap gap-4">
                  {(vocab?.periodicity ?? []).map((p) => {
                    const checked = d.time!.allowedPeriodicities.includes(p.value)
                    const finer = native != null && PERIOD_ORDER.indexOf(p.value) < PERIOD_ORDER.indexOf(native)
                    return (
                      <label key={p.value} className="flex items-center gap-2 text-sm">
                        <Checkbox
                          checked={checked}
                          disabled={readOnly}
                          onCheckedChange={(v) =>
                            setTime('allowedPeriodicities', v ? PERIOD_ORDER.filter((x) => x === p.value || d.time!.allowedPeriodicities.includes(x)) : d.time!.allowedPeriodicities.filter((x) => x !== p.value))
                          }
                        />
                        {p.label}
                        {finer && <span className="text-xs text-muted-foreground">finer than native</span>}
                      </label>
                    )
                  })}
                </div>
                {errorsFor('time.allowedPeriodicities')?.map((e) => (
                  <p key={e} className="text-xs text-destructive">
                    {e}
                  </p>
                ))}
                {!!offenders.length && (
                  <div role="status" className="flex gap-2 rounded-md border border-warning/50 bg-warning-muted p-3 text-xs">
                    <AlertTriangleIcon aria-hidden className="size-4 shrink-0 text-warning-foreground" />
                    <div className="flex flex-col gap-1">
                      <p>
                        {finerTicked.join(', ')} {finerTicked.length === 1 ? 'is' : 'are'} finer than {native}. Publishing needs every enabled measure on this axis to estimate partial intervals uniformly. These don&apos;t:
                      </p>
                      <p className="flex flex-wrap gap-1.5">
                        {offenders.map((m) => (
                          <button key={m.id} type="button" onClick={() => onOpen('measures', m.id)} className="font-mono text-primary hover:underline">
                            {m.measureKey}
                          </button>
                        ))}
                      </p>
                    </div>
                  </div>
                )}
              </div>

              <div className="grid gap-4 @md:grid-cols-2">
                <FormRow label="Cumulative origin" htmlFor="t-co" errors={errorsFor('time.cumulativeOrigin')}>
                  <VocabSelect id="t-co" terms={vocab?.cumulativeOrigin} value={d.time.cumulativeOrigin} onChange={(v) => setD((x) => ({ ...x, time: { ...x.time!, cumulativeOrigin: v ?? 'output_range_start', cumulativeFixedStart: v === 'fixed_start' ? x.time!.cumulativeFixedStart : null } }))} />
                </FormRow>
                {d.time.cumulativeOrigin === 'fixed_start' && (
                  <FormRow label="Fixed start" htmlFor="t-fs" errors={errorsFor('time.cumulativeFixedStart')}>
                    <Input id="t-fs" type="date" value={d.time.cumulativeFixedStart ?? ''} onChange={(e) => setTime('cumulativeFixedStart', e.target.value || null)} />
                  </FormRow>
                )}
              </div>
            </PanelSection>
          )}

          <PanelSection title="Flags">
            <div className="grid gap-3 @md:grid-cols-2">
              <CheckRow checked={d.isRequired} onChange={(v) => set('isRequired', v)} label="Required" description="A fixture: can't be deleted or disabled." disabled={readOnly} />
              <CheckRow checked={d.isGroupable} onChange={(v) => set('isGroupable', v)} label="Groupable" description="The source field must be groupable too." disabled={readOnly} />
              <CheckRow checked={d.isFilterable} onChange={(v) => set('isFilterable', v)} label="Filterable" disabled={readOnly} />
              <CheckRow checked={d.isEnabled} onChange={(v) => set('isEnabled', v)} label="Enabled" disabled={readOnly || d.isRequired} />
            </div>
            {errorsFor('isGroupable')?.map((e) => (
              <p key={e} className="text-xs text-destructive">
                {e}
              </p>
            ))}
          </PanelSection>
        </fieldset>
      {existing && (
        <PanelSection title="Used by">
          <DependentsList dependents={deps} onOpen={(x) => onOpen(x.tab, x.itemId)} empty={isTime ? 'No measure requires this time binding.' : 'No other binding bridges from this one.'} />
        </PanelSection>
      )}
    </PropertiesPanel>
  )
}
