import { ArrowLeftIcon, ArrowRightIcon, ClockIcon, PlusIcon, Trash2Icon } from 'lucide-react'
import { useRef, useState, type KeyboardEvent } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { useCatalog, useDatasetsApi, useRefreshDatasets, useVocabularies } from '@/lib/datasets/client'
import type { DatasetComponentRole, DatasetVersion, DatasetMeasureInput, DatasetMeasureKind, DatasetMeasureTimePolicy, Problem } from '@/lib/datasets/contract'
import { cn } from '@/lib/utils'
import { bindingLabel } from './bindings-section'
import type { SectionProps } from './dataset-detail'
import { ExpressionInput } from './expression-input'
import { InlineSelect, InlineSwitch, InlineText, PanelSection, PropertiesPanel, SplitView, focusRowClass, usePanelDraft } from '@/components/shell/inline-table'
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

export function formulaRefs(expression: string | null) {
  return [...(expression ?? '').matchAll(/\[m\.([\w.-]+)\]/g)].map((m) => m[1])
}

const blankPolicy = (): DatasetMeasureTimePolicy => ({
  requiredTimeBindingId: null,
  selectionDatasetGrainId: null,
  observationSelection: 'all',
  valueTimeRole: 'point',
  boundaryMatch: 'exact',
  maxLookbackSeconds: null,
  durationUnit: 'calendar_day',
  partialIntervalBehavior: 'exact_only',
  missingPeriodBehavior: 'null',
})

const PRESETS: { label: string; description: string; patch: Partial<DatasetMeasureTimePolicy> }[] = [
  {
    label: 'Ending balance',
    description: 'Last value at or before the period end',
    patch: { observationSelection: 'at_end', boundaryMatch: 'on_or_before', maxLookbackSeconds: 2_678_400, valueTimeRole: 'point' },
  },
  { label: 'Daily activity sum', description: 'Every observation in the period', patch: { observationSelection: 'all', boundaryMatch: 'exact', maxLookbackSeconds: null } },
  { label: 'Period average', description: 'All observations, weighted by calendar days', patch: { observationSelection: 'all', durationUnit: 'calendar_day', boundaryMatch: 'exact', maxLookbackSeconds: null } },
]

const blankMeasure = (): DatasetMeasureInput => ({
  key: '',
  displayName: '',
  isEnabled: true,
  measureKind: 'aggregate',
  aggregationFunction: 'sum',
  expressionText: null,
  implementationKey: null,
  implementationVersion: null,
  configurationJson: null,
  components: [],
  rowFilterExpression: null,
  valueNullBehavior: 'exclude',
  negativeWeightBehavior: 'allow',
  emptySetBehavior: 'null',
  bridgeFactorBehavior: 'value',
  bridgeAllocationBehavior: 'apply',
  totalBehavior: 'sum_children',
  outputDataType: 'decimal',
  outputPrecision: 18,
  outputScale: 2,
  roundingMode: 'half_even',
  unitKey: null,
  accumulatorPrecision: 38,
  accumulatorScale: 12,
  timePolicy: null,
})

function measureSummary(m: DatasetMeasureInput) {
  if (m.measureKind === 'formula') return m.expressionText ?? '—'
  if (m.measureKind === 'specialized') return `${m.implementationKey ?? '?'}@${m.implementationVersion ?? '?'}`
  if (m.measureKind === 'aggregate') return m.aggregationFunction ?? '—'
  return m.measureKind.replace('_', ' ')
}

const NEW_ROW_ID = '__new__'
const scrollToRow = (id: string) => requestAnimationFrame(() => document.querySelector(`[data-row-id="${CSS.escape(id)}"]`)?.scrollIntoView({ block: 'nearest' }))

/** Switching kind drops components and kind-specific inputs that no longer apply. */
function withMeasureKind(x: DatasetMeasureInput, kind: DatasetMeasureKind, allowedRoles: Set<string>): DatasetMeasureInput {
  return {
    ...x,
    measureKind: kind,
    components: kind === 'formula' ? [] : x.components.filter((c) => allowedRoles.has(c.role)),
    aggregationFunction: kind === 'aggregate' ? (x.aggregationFunction ?? 'sum') : kind === 'specialized' ? x.aggregationFunction : null,
    expressionText: kind === 'formula' ? x.expressionText : null,
    implementationKey: kind === 'specialized' ? x.implementationKey : null,
    implementationVersion: kind === 'specialized' ? x.implementationVersion : null,
    configurationJson: kind === 'specialized' ? x.configurationJson : null,
    timePolicy: kind === 'period_average' ? (x.timePolicy ?? { ...blankPolicy(), durationUnit: 'calendar_day' }) : x.timePolicy,
    totalBehavior: kind === 'aggregate' && (x.aggregationFunction === 'sum' || x.aggregationFunction === 'count') ? x.totalBehavior : 'recompute',
  }
}

export function MeasuresSection({ version, readOnly, editingId, onEdit, onOpen }: SectionProps) {
  const api = useDatasetsApi()
  const refresh = useRefreshDatasets()
  const { data: vocab } = useVocabularies()
  const { run, problem, setProblem } = useApiCall()
  const [newDraft, setNewDraftState] = useState<DatasetMeasureInput | null>(null)
  const [newProblem, setNewProblem] = useState<Problem | null>(null)
  const [creating, setCreating] = useState(false)
  const newRef = useRef(newDraft)
  const fieldKey = (id: string) => version.fields.find((f) => f.id === id)?.key ?? '?'
  const kindLabel = (k: string) => vocab?.measureKind.find((t) => t.value === k)?.label ?? k
  const kindOptions = (vocab?.measureKind ?? []).map((t) => ({ value: t.value, label: t.label }))
  const aggOptions = (vocab?.aggregationFunction ?? []).map((t) => ({ value: t.value, label: t.label }))
  const allowedRoles = (k: DatasetMeasureKind) => new Set<string>((vocab?.componentRolesByKind[k] ?? []).map((r) => r.role))
  const focused = editingId ? version.measures.find((m) => m.id === editingId) : undefined
  const patch = (m: DatasetMeasureInput & { id: string }, p: Partial<DatasetMeasureInput>) => run(() => api.saveMeasure(version.id, { ...m, ...p }))

  const setNewDraft = (d: DatasetMeasureInput | null) => {
    newRef.current = d
    setNewDraftState(d)
  }
  const editNew = (update: (d: DatasetMeasureInput) => DatasetMeasureInput) => {
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
    setNewDraft(blankMeasure())
    scrollToRow(NEW_ROW_ID)
  }

  async function createNew() {
    const input = newRef.current
    if (!input || creating) return false
    setCreating(true)
    const r = await api.saveMeasure(version.id, input)
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

  const side = newDraft ? (
    <MeasurePanel
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
    <MeasurePanel key={focused.id} version={version} readOnly={readOnly} existing={focused} seed={focused} onClose={() => onEdit(null)} onOpen={onOpen} />
  ) : null

  return (
    <div className="flex flex-col gap-4">
      <SectionHeader
        title="Measures"
        help="Backend-defined calculations that aggregate requests ask for by key: which fields feed them, how they aggregate and treat nulls, weights, bridges and totals, and which observation counts per period."
        action={
          !readOnly && (
            <RowEditActions adding={!!newDraft} creating={creating} canCreate={!!newDraft?.key.trim()} onStartNew={startNew} onCreate={createNew} onCancelNew={cancelNew} />
          )
        }
      />
      {problem && <ProblemBanner problem={problem} onDismiss={() => setProblem(null)} onOpen={onOpen} />}
      <SplitView panel={side}>
      <div className="overflow-x-auto rounded-lg border bg-card">
        <table className="w-full text-sm">
          <thead className="border-b bg-muted/40">
            <tr>
              <Th>Measure</Th>
              <Th>Display name</Th>
              <Th>Kind</Th>
              <Th>Aggregation or formula</Th>
              <Th>Components</Th>
              <Th>Output</Th>
              <Th>Time policy</Th>
              <Th>Status</Th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {version.measures.map((m) => (
              <tr key={m.id} data-row-id={m.id} aria-selected={m.id === editingId} onClick={() => focus(m.id)} className={cn(focusRowClass(m.id === editingId), !m.isEnabled && 'text-muted-foreground')}>
                <Td className="font-mono text-xs">{m.key}</Td>
                <Td>
                  {readOnly ? (
                    <span className="text-sm">{m.displayName}</span>
                  ) : (
                    <InlineText aria-label={`Display name of ${m.key}`} value={m.displayName} onCommit={(v) => patch(m, { displayName: v })} placeholder="Display name" />
                  )}
                </Td>
                <Td className="text-xs">{kindLabel(m.measureKind)}</Td>
                <Td className="max-w-60">
                  <span className="block truncate font-mono text-xs" title={measureSummary(m)}>
                    {measureSummary(m)}
                  </span>
                </Td>
                <Td>
                  <span className="flex flex-wrap gap-1">
                    {m.components.length ? (
                      m.components.map((c) => (
                        <Chip key={c.role}>
                          <span className="text-muted-foreground">{c.role}</span> {fieldKey(c.fieldId)}
                        </Chip>
                      ))
                    ) : (
                      <span className="text-xs text-muted-foreground">—</span>
                    )}
                  </span>
                </Td>
                <Td className="font-mono text-xs whitespace-nowrap">
                  {m.outputDataType}
                  {m.outputDataType === 'decimal' && m.outputPrecision != null && `(${m.outputPrecision},${m.outputScale ?? 0})`}
                  {m.unitKey && <span className="ml-1 text-muted-foreground">{m.unitKey}</span>}
                </Td>
                <Td>
                  {m.timePolicy ? (
                    <Chip tone="primary">
                      <ClockIcon className="size-3" /> {m.timePolicy.observationSelection.replaceAll('_', ' ')}
                    </Chip>
                  ) : (
                    <span className="text-xs text-muted-foreground">—</span>
                  )}
                </Td>
                <Td>
                  <InlineSwitch aria-label={`${m.key} enabled`} checked={m.isEnabled} onCommit={(v) => !readOnly && patch(m, { isEnabled: v })} />
                </Td>
              </tr>
            ))}
            {!readOnly && newDraft && (
              <NewMeasureRow
                draft={newDraft}
                problem={newProblem}
                creating={creating}
                kindOptions={kindOptions}
                aggOptions={aggOptions}
                fieldKey={fieldKey}
                onChange={editNew}
                onKindChange={(k) => editNew((x) => withMeasureKind(x, k, allowedRoles(k)))}
                onSubmit={createNew}
                onCancel={cancelNew}
              />
            )}
            {!version.measures.length && !newDraft && (
              <tr>
                <Td colSpan={8} className="py-8 text-center text-muted-foreground">
                  {readOnly ? 'No measures yet.' : 'No measures yet. Use “Add new row” to define one.'}
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

const humanize = (key: string) => key.replace(/[_.-]+/g, ' ').replace(/([a-z])([A-Z])/g, '$1 $2').replace(/^\w/, (c) => c.toUpperCase())

const rowInputClass =
  'h-8 w-full min-w-0 rounded-md border border-input bg-background px-2 text-sm outline-none placeholder:text-muted-foreground/70 focus:border-ring focus:ring-2 focus:ring-ring/30 aria-invalid:border-destructive'

/** The pending measure: a selected, editable row bound to the same draft as the open properties panel. */
function NewMeasureRow({
  draft,
  problem,
  creating,
  kindOptions,
  aggOptions,
  fieldKey,
  onChange,
  onKindChange,
  onSubmit,
  onCancel,
}: {
  draft: DatasetMeasureInput
  problem: Problem | null
  creating: boolean
  kindOptions: { value: string; label: string }[]
  aggOptions: { value: string; label: string }[]
  fieldKey: (id: string) => string
  onChange: (update: (d: DatasetMeasureInput) => DatasetMeasureInput) => void
  onKindChange: (kind: DatasetMeasureKind) => void
  onSubmit: () => void
  onCancel: () => void
}) {
  const errs = problem?.errors ?? {}
  const messages = Object.values(errs).flat()
  const hasAggregation = draft.measureKind === 'aggregate' || draft.measureKind === 'specialized'
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
  const setKey = (key: string) =>
    onChange((x) => ({ ...x, key: key, displayName: !x.displayName || x.displayName === humanize(x.key) ? humanize(key) : x.displayName }))

  return (
    <tr data-row-id={NEW_ROW_ID} aria-selected aria-label="New measure" className={cn(focusRowClass(true), 'align-top')}>
      <Td>
        <input
          autoFocus
          aria-label="New measure: measure key"
          value={draft.key}
          placeholder="key"
          disabled={creating}
          aria-invalid={!!errs.key}
          onChange={(e) => setKey(e.target.value)}
          onKeyDown={onKeyDown}
          className={cn(rowInputClass, 'w-40 font-mono text-xs')}
        />
      </Td>
      <Td>
        <input
          aria-label="New measure: display name"
          value={draft.displayName}
          placeholder="Display name"
          disabled={creating}
          aria-invalid={!!errs.displayName}
          onChange={(e) => onChange((x) => ({ ...x, displayName: e.target.value }))}
          onKeyDown={onKeyDown}
          className={cn(rowInputClass, 'w-44')}
        />
      </Td>
      <Td>
        <InlineSelect aria-label="New measure: kind" value={draft.measureKind} options={kindOptions} onCommit={(k) => onKindChange(k as DatasetMeasureKind)} className="w-32 text-xs" />
      </Td>
      <Td>
        {hasAggregation ? (
          <InlineSelect
            aria-label="New measure: aggregation"
            value={draft.aggregationFunction ?? ''}
            placeholder="None"
            options={aggOptions}
            onCommit={(v) => onChange((x) => ({ ...x, aggregationFunction: v as DatasetMeasureInput['aggregationFunction'] }))}
            className="w-28 text-xs"
          />
        ) : (
          <span className="block pt-2 text-xs text-muted-foreground">{draft.measureKind === 'formula' ? 'Write the formula in the panel' : 'Set in the panel'}</span>
        )}
      </Td>
      <Td>
        <span className="flex flex-wrap items-center gap-1 pt-1.5">
          {draft.components.length ? (
            draft.components.map((c) => (
              <Chip key={c.role}>
                <span className="text-muted-foreground">{c.role}</span> {fieldKey(c.fieldId)}
              </Chip>
            ))
          ) : (
            <span className="text-xs text-muted-foreground">{draft.measureKind === 'formula' ? '—' : 'Pick in the panel'}</span>
          )}
        </span>
      </Td>
      <Td colSpan={3}>
        <span className="flex items-start justify-between gap-3 pt-1">
          <span role={messages.length ? 'alert' : undefined} className={cn('text-xs text-pretty', messages.length ? 'text-destructive' : 'text-muted-foreground')}>
            {messages.length ? messages.join(' ') : null}
          </span>
        </span>
      </Td>
    </tr>
  )
}

function MeasurePanel({
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
  existing?: DatasetVersion['measures'][number]
  seed: DatasetMeasureInput
  onClose: () => void
  onOpen: SectionProps['onOpen']
  /** New measures only: panel edits update the pending row's draft instead of saving. */
  onDraftCommit?: (draft: DatasetMeasureInput) => void
  createProblem?: Problem | null
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
    return !!(await run(() => api.saveMeasure(version.id, next)))
  })

  const set = <K extends keyof DatasetMeasureInput>(k: K, v: DatasetMeasureInput[K]) => setD((x) => ({ ...x, [k]: v }))
  const setPolicy = (patch: Partial<DatasetMeasureTimePolicy>) => setD((x) => ({ ...x, timePolicy: { ...(x.timePolicy ?? blankPolicy()), ...patch } }))
  const roles = vocab?.componentRolesByKind[d.measureKind] ?? []
  const deps = existing ? (version.dependents[existing.id] ?? []) : []
  const blocking = deps.filter((x) => x.blocksDelete)
  const reads = d.measureKind === 'formula' ? formulaRefs(d.expressionText) : []
  const timeBindings = version.bindings.filter((b) => b.bindingKind === 'time')
  const seriesGrains = version.grains.filter((g) => g.grainKind === 'series')
  const fieldOptions = version.fields.map((f) => ({ value: f.id, label: f.key, hint: f.dataType }))
  const p = d.timePolicy
  const policyForced = d.measureKind === 'period_average'

  function changeKind(kind: DatasetMeasureKind) {
    const allowed = new Set<string>((vocab?.componentRolesByKind[kind] ?? []).map((r) => r.role))
    setD((x) => withMeasureKind(x, kind, allowed))
  }

  function setComponent(role: DatasetComponentRole, fieldId: string | null) {
    setD((x) => ({
      ...x,
      components: fieldId ? [...x.components.filter((c) => c.role !== role), { role, fieldId }] : x.components.filter((c) => c.role !== role),
    }))
  }

  const relations = (
    <>
      {d.measureKind === 'formula' && (
        <PanelSection title="Reads">
                {reads.length ? (
                  <ul className="flex flex-col gap-1">
                    {reads.map((k) => {
                      const target = version.measures.find((m) => m.key === k)
                      return (
                        <li key={k} className="flex items-center gap-1.5 text-xs">
                          <ArrowRightIcon aria-hidden className="size-3 text-muted-foreground" />
                          {target ? (
                            <button type="button" onClick={() => onOpen('measures', target.id)} className="font-mono text-primary hover:underline">
                              {k}
                            </button>
                          ) : (
                            <span className="font-mono text-destructive">{k} · unknown</span>
                          )}
                        </li>
                      )
                    })}
                  </ul>
                ) : (
                  <p className="text-xs text-muted-foreground">The formula references no measures yet.</p>
                )}
        </PanelSection>
      )}
      {existing && (
        <PanelSection title="Read by">
          <DependentsList dependents={deps} onOpen={(x) => onOpen(x.tab, x.itemId)} empty="No formula reads this measure." />
          {!!deps.length && (
            <p className="flex items-center gap-1 text-[11px] text-muted-foreground">
              <ArrowLeftIcon aria-hidden className="size-3" /> Deleting is refused while formulas read it.
            </p>
          )}
        </PanelSection>
      )}
    </>
  )

  return (
    <PropertiesPanel
      size="lg"
      label="Measure properties"
      title={existing ? existing.displayName || existing.key : d.key || 'New measure'}
      subtitle={
        !existing ? (
          'Not added yet · needs a key and its kind’s inputs.'
        ) : pending && problem ? (
          <span className="text-destructive">Not applied — fix the highlighted inputs.</span>
        ) : (
          <span className="font-mono">{existing.key}</span>
        )
      }
      onClose={onClose}
      bodyProps={bodyProps}
      actions={
        existing &&
        !readOnly && (
          <Button
            variant="ghost"
            size="icon-xs"
            aria-label={`Delete ${existing.key}`}
            disabled={!!blocking.length || saving}
            title={blocking.length ? `Read by ${blocking.map((x) => x.itemKey).join(', ')}` : 'Delete measure'}
            onClick={async () => {
              if (await runOk(() => api.deleteMeasure(version.id, existing.id))) {
                toast.success(`Deleted ${existing.key}`)
                onClose()
              }
            }}
          >
            <Trash2Icon />
          </Button>
        )
      }
    >
      {problem && <ProblemBanner problem={problem} onDismiss={() => (setProblem(null), onDismissCreateProblem?.())} onOpen={onOpen} />}
        <fieldset disabled={readOnly} className="contents">
          <PanelSection title="Identity and kind" description={vocab?.measureKind.find((t) => t.value === d.measureKind)?.description}>
            <div className="grid gap-4 @md:grid-cols-2">
              <FormRow label="Measure key" htmlFor="m-key" errors={errorsFor('key')} hint={existing ? 'Fixed. Formulas reference it as [m.key].' : 'e.g. ending_balance'}>
                <Input id="m-key" value={d.key} disabled={!!existing} onChange={(e) => set('key', e.target.value)} className="font-mono" aria-invalid={!!errorsFor('key')} />
              </FormRow>
              <FormRow label="Display name" htmlFor="m-name" errors={errorsFor('displayName')}>
                <Input id="m-name" value={d.displayName} onChange={(e) => set('displayName', e.target.value)} />
              </FormRow>
              <FormRow label="Kind" htmlFor="m-kind" errors={errorsFor('measureKind')}>
                <VocabSelect id="m-kind" terms={vocab?.measureKind} value={d.measureKind} onChange={(k) => k && changeKind(k)} />
              </FormRow>
              {(d.measureKind === 'aggregate' || d.measureKind === 'specialized') && (
                <FormRow label="Aggregation" htmlFor="m-agg" errors={errorsFor('aggregationFunction')}>
                  <VocabSelect
                    id="m-agg"
                    terms={vocab?.aggregationFunction}
                    value={d.aggregationFunction}
                    noneLabel={d.measureKind === 'specialized' ? 'None' : undefined}
                    onChange={(v) => set('aggregationFunction', v)}
                  />
                </FormRow>
              )}
              {d.measureKind === 'formula' && (
                <FormRow label="Formula" htmlFor="m-expr" errors={errorsFor('expressionText')} className="@md:col-span-2">
                  <ExpressionInput
                    id="m-expr"
                    prefix="m"
                    value={d.expressionText ?? ''}
                    onChange={(v) => set('expressionText', v || null)}
                    items={version.measures.filter((m) => m.id !== existing?.id).map((m) => ({ key: m.key, label: m.displayName }))}
                    placeholder="[m.interest_income] / [m.average_balance]"
                    invalid={!!errorsFor('expressionText')}
                    disabled={readOnly}
                  />
                </FormRow>
              )}
              {d.measureKind === 'specialized' && (
                <>
                  <FormRow label="Implementation key" htmlFor="m-impl" errors={errorsFor('implementationKey')}>
                    <Input id="m-impl" value={d.implementationKey ?? ''} onChange={(e) => set('implementationKey', e.target.value || null)} className="font-mono" />
                  </FormRow>
                  <FormRow label="Implementation version" htmlFor="m-implv" errors={errorsFor('implementationVersion')}>
                    <Input id="m-implv" value={d.implementationVersion ?? ''} onChange={(e) => set('implementationVersion', e.target.value || null)} className="font-mono" />
                  </FormRow>
                  <FormRow label="Configuration JSON" htmlFor="m-cfg" errors={errorsFor('configurationJson')} className="@md:col-span-2">
                    <Textarea id="m-cfg" rows={3} value={d.configurationJson ?? ''} onChange={(e) => set('configurationJson', e.target.value || null)} className="font-mono text-xs" spellCheck={false} />
                  </FormRow>
                </>
              )}
            </div>
          </PanelSection>

          {d.measureKind !== 'formula' && (
            <PanelSection title="Components" description="One field per role. The kind decides which roles apply.">
              {errorsFor('components')?.map((e) => (
                <p key={e} className="text-xs text-destructive">
                  {e}
                </p>
              ))}
              <div className="grid gap-4 @md:grid-cols-2">
                {roles.map(({ role, required }) => (
                  <FormRow key={role} label={`${vocab?.componentRole.find((t) => t.value === role)?.label ?? role}${required ? '' : ' (optional)'}`} htmlFor={`m-c-${role}`} errors={errorsFor(`components.${role}`)}>
                    <SimpleSelect
                      id={`m-c-${role}`}
                      value={d.components.find((c) => c.role === role)?.fieldId ?? (required ? '' : NONE)}
                      placeholder="Choose a field…"
                      onChange={(v) => setComponent(role, v === NONE ? null : v)}
                      options={[...(required ? [] : [{ value: NONE, label: 'None' }]), ...fieldOptions]}
                      className="font-mono"
                    />
                  </FormRow>
                ))}
              </div>
            </PanelSection>
          )}

          {d.measureKind !== 'formula' && (
            <PanelSection title="Row filter" description="Optional predicate restricting which rows feed this measure only.">
              <FormRow label="Filter expression" htmlFor="m-filter" errors={errorsFor('rowFilterExpression')}>
                <ExpressionInput
                  id="m-filter"
                  prefix="f"
                  value={d.rowFilterExpression ?? ''}
                  onChange={(v) => set('rowFilterExpression', v || null)}
                  items={version.fields.map((f) => ({ key: f.key, label: f.displayName }))}
                  placeholder="[f.status] = 'open'"
                  invalid={!!errorsFor('rowFilterExpression')}
                  disabled={readOnly}
                />
              </FormRow>
            </PanelSection>
          )}

          <PanelSection title="Behaviors">
            <div className="grid gap-4 @md:grid-cols-2">
              {(
                [
                  ['valueNullBehavior', 'Null values'],
                  ['negativeWeightBehavior', 'Negative weights'],
                  ['emptySetBehavior', 'Empty set'],
                  ['bridgeFactorBehavior', 'Bridge factor'],
                  ['bridgeAllocationBehavior', 'Bridge allocation'],
                  ['totalBehavior', 'Totals'],
                ] as const
              ).map(([k, label]) => (
                <FormRow key={k} label={label} htmlFor={`m-${k}`} errors={errorsFor(k)} hint={(vocab?.[k] as { value: string; description?: string }[] | undefined)?.find((t) => t.value === d[k])?.description}>
                  <VocabSelect id={`m-${k}`} terms={vocab?.[k] as { value: string; label: string }[] | undefined} value={d[k]} onChange={(v) => v && set(k, v as never)} />
                </FormRow>
              ))}
            </div>
          </PanelSection>

          <PanelSection title="Output" description="Rounding applies to client display only; the engine never rounds. An accumulator that overflows yields null.">
            <div className="grid gap-4 @md:grid-cols-4">
              <FormRow label="Output type" htmlFor="m-otype" errors={errorsFor('outputDataType')} className="@md:col-span-2">
                <VocabSelect
                  id="m-otype"
                  terms={vocab?.dataType}
                  value={d.outputDataType}
                  onChange={(t) => setD((x) => ({ ...x, outputDataType: t ?? 'decimal', outputPrecision: t === 'decimal' ? (x.outputPrecision ?? 18) : null, outputScale: t === 'decimal' ? (x.outputScale ?? 2) : null }))}
                />
              </FormRow>
              {d.outputDataType === 'decimal' && (
                <>
                  <FormRow label="Precision" htmlFor="m-oprec" errors={errorsFor('outputPrecision')}>
                    <Input id="m-oprec" type="number" value={d.outputPrecision ?? ''} onChange={(e) => set('outputPrecision', e.target.value === '' ? null : Number(e.target.value))} />
                  </FormRow>
                  <FormRow label="Scale" htmlFor="m-oscale" errors={errorsFor('outputScale')}>
                    <Input id="m-oscale" type="number" value={d.outputScale ?? ''} onChange={(e) => set('outputScale', e.target.value === '' ? null : Number(e.target.value))} />
                  </FormRow>
                </>
              )}
              <FormRow label="Rounding" htmlFor="m-round" errors={errorsFor('roundingMode')} className="@md:col-span-2">
                <VocabSelect id="m-round" terms={vocab?.roundingMode} value={d.roundingMode} onChange={(v) => v && set('roundingMode', v)} />
              </FormRow>
              <FormRow label="Unit" htmlFor="m-unit" errors={errorsFor('unitKey')} className="@md:col-span-2">
                <Input id="m-unit" value={d.unitKey ?? ''} onChange={(e) => set('unitKey', e.target.value || null)} placeholder="USD, percent…" />
              </FormRow>
              <FormRow label="Accumulator precision" htmlFor="m-aprec" errors={errorsFor('accumulatorPrecision')} className="@md:col-span-2">
                <Input id="m-aprec" type="number" value={d.accumulatorPrecision} onChange={(e) => set('accumulatorPrecision', Number(e.target.value))} />
              </FormRow>
              <FormRow label="Accumulator scale" htmlFor="m-ascale" errors={errorsFor('accumulatorScale')} className="@md:col-span-2">
                <Input id="m-ascale" type="number" value={d.accumulatorScale} onChange={(e) => set('accumulatorScale', Number(e.target.value))} />
              </FormRow>
            </div>
          </PanelSection>

          <PanelSection
            title="Time policy"
            description={policyForced ? 'Period averages always carry a time policy.' : 'Optional. Decides which observation per series counts in a period.'}
            action={
              !policyForced &&
              !readOnly && (
                <Button variant="outline" size="sm" onClick={() => set('timePolicy', p ? null : blankPolicy())}>
                  {p ? 'Remove time policy' : 'Add time policy'}
                </Button>
              )
            }
          >
            {errorsFor('timePolicy')?.map((e) => (
              <p key={e} className="text-xs text-destructive">
                {e}
              </p>
            ))}
            {p ? (
              <>
                {!readOnly && (
                  <div className="flex flex-wrap gap-2">
                    {PRESETS.map((pr) => (
                      <button key={pr.label} type="button" onClick={() => setPolicy(pr.patch)} className="flex flex-col rounded-md border px-3 py-1.5 text-left hover:bg-muted">
                        <span className="text-xs font-medium">{pr.label}</span>
                        <span className="text-[11px] text-muted-foreground">{pr.description}</span>
                      </button>
                    ))}
                  </div>
                )}
                <div className="grid gap-4 @md:grid-cols-2">
                  <FormRow label="Time binding" htmlFor="tp-bind" errors={errorsFor('timePolicy.requiredTimeBindingId')} hint="Empty uses the query's axis or the version default.">
                    <SimpleSelect
                      id="tp-bind"
                      value={p.requiredTimeBindingId ?? NONE}
                      onChange={(v) => setPolicy({ requiredTimeBindingId: v === NONE ? null : v })}
                      options={[{ value: NONE, label: 'Query axis or default' }, ...timeBindings.map((b) => ({ value: b.id, label: bindingLabel(b, version, catalog), hint: version.settings.defaultTimeBindingId === b.id ? 'default' : undefined }))]}
                    />
                  </FormRow>
                  <FormRow label="Observation selection" htmlFor="tp-sel" errors={errorsFor('timePolicy.observationSelection')}>
                    <VocabSelect id="tp-sel" terms={vocab?.observationSelection} value={p.observationSelection} onChange={(v) => v && setPolicy({ observationSelection: v, selectionDatasetGrainId: v === 'all' ? null : p.selectionDatasetGrainId })} />
                  </FormRow>
                  {p.observationSelection !== 'all' && (
                    <FormRow label="Series grain" htmlFor="tp-grain" errors={errorsFor('timePolicy.selectionDatasetGrainId')} hint="Partition the selection runs within.">
                      <SimpleSelect
                        id="tp-grain"
                        value={p.selectionDatasetGrainId ?? ''}
                        placeholder={seriesGrains.length ? 'Choose…' : 'No series grain yet'}
                        onChange={(v) => setPolicy({ selectionDatasetGrainId: v })}
                        options={seriesGrains.map((g) => ({ value: g.id, label: g.key }))}
                        className="font-mono"
                      />
                    </FormRow>
                  )}
                  <FormRow label="Value time role" htmlFor="tp-role" errors={errorsFor('timePolicy.valueTimeRole')}>
                    <VocabSelect id="tp-role" terms={vocab?.valueTimeRole} value={p.valueTimeRole} onChange={(v) => v && setPolicy({ valueTimeRole: v })} />
                  </FormRow>
                  <FormRow label="Boundary match" htmlFor="tp-bm" errors={errorsFor('timePolicy.boundaryMatch')}>
                    <VocabSelect id="tp-bm" terms={vocab?.boundaryMatch} value={p.boundaryMatch} onChange={(v) => v && setPolicy({ boundaryMatch: v, maxLookbackSeconds: v === 'exact' ? null : p.maxLookbackSeconds })} />
                  </FormRow>
                  {p.boundaryMatch !== 'exact' && (
                    <FormRow label="Max lookback (seconds)" htmlFor="tp-lb" errors={errorsFor('timePolicy.maxLookbackSeconds')} hint={p.maxLookbackSeconds ? `≈ ${Math.round(p.maxLookbackSeconds / 86400)} days` : 'Required and finite.'}>
                      <Input id="tp-lb" type="number" value={p.maxLookbackSeconds ?? ''} onChange={(e) => setPolicy({ maxLookbackSeconds: e.target.value === '' ? null : Number(e.target.value) })} />
                    </FormRow>
                  )}
                  <FormRow label="Duration unit" htmlFor="tp-du" errors={errorsFor('timePolicy.durationUnit')}>
                    <VocabSelect id="tp-du" terms={vocab?.durationUnit} value={p.durationUnit} onChange={(v) => v && setPolicy({ durationUnit: v })} />
                  </FormRow>
                  <FormRow label="Partial intervals" htmlFor="tp-pi" errors={errorsFor('timePolicy.partialIntervalBehavior')}>
                    <VocabSelect id="tp-pi" terms={vocab?.partialIntervalBehavior} value={p.partialIntervalBehavior} onChange={(v) => v && setPolicy({ partialIntervalBehavior: v })} />
                  </FormRow>
                  <FormRow label="Missing periods" htmlFor="tp-mp" errors={errorsFor('timePolicy.missingPeriodBehavior')}>
                    <VocabSelect id="tp-mp" terms={vocab?.missingPeriodBehavior} value={p.missingPeriodBehavior} onChange={(v) => v && setPolicy({ missingPeriodBehavior: v })} />
                  </FormRow>
                </div>
              </>
            ) : (
              <p className="text-xs text-muted-foreground">No time policy: every row in range contributes.</p>
            )}
          </PanelSection>

          <PanelSection title="Availability">
            <CheckRow checked={d.isEnabled} onChange={(v) => set('isEnabled', v)} label="Enabled" description="Disabled measures can't be requested." disabled={readOnly} />
          </PanelSection>
        </fieldset>
      {relations}
    </PropertiesPanel>
  )
}

