import { ArrowDownIcon, ArrowUpIcon, ClockIcon, GripVerticalIcon, PlusIcon, Trash2Icon, XIcon } from 'lucide-react'
import { useRef, useState, type KeyboardEvent } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { CommitInput, InlineSelect, PanelSection, PropertiesPanel, SplitView, focusRowClass, usePanelDraft } from '@/components/shell/inline-table'
import { useDatasetsApi, useRefreshDatasets, useVocabularies } from '@/lib/datasets/client'
import type { DatasetGrain, DatasetVersion, DatasetGrainInput, Problem } from '@/lib/datasets/contract'
import { cn } from '@/lib/utils'
import type { SectionProps } from './dataset-detail'
import { timeFieldIds } from './fields-section'
import { Chip, DependentsList, FormRow, ProblemBanner, RowEditActions, SectionHeader, Td, Th, VocabSelect, useApiCall } from './shared'

const NEW_ROW_ID = '__new__'
const scrollToRow = (id: string) => requestAnimationFrame(() => document.querySelector(`[data-row-id="${CSS.escape(id)}"]`)?.scrollIntoView({ block: 'nearest' }))
const blankGrain = (): DatasetGrainInput => ({ key: '', grainKind: 'row', fieldIds: [] })

export function GrainsSection({ version, readOnly, editingId, onEdit, onOpen }: SectionProps) {
  const api = useDatasetsApi()
  const refresh = useRefreshDatasets()
  const { data: vocab } = useVocabularies()
  const { run, problem, setProblem } = useApiCall()
  const [newDraft, setNewDraftState] = useState<DatasetGrainInput | null>(null)
  const [newProblem, setNewProblem] = useState<Problem | null>(null)
  const [creating, setCreating] = useState(false)
  const newRef = useRef(newDraft)
  const fieldKey = (id: string) => version.fields.find((f) => f.id === id)?.key ?? '?'
  const focused = editingId ? version.grains.find((g) => g.id === editingId) : undefined
  const kindOptions = (vocab?.grainKind ?? []).map((t) => ({ value: t.value, label: t.label }))

  const setNewDraft = (d: DatasetGrainInput | null) => {
    newRef.current = d
    setNewDraftState(d)
  }
  const editNew = (p: Partial<DatasetGrainInput>) => {
    if (!newRef.current) return
    setNewDraft({ ...newRef.current, ...p })
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
    setNewDraft(blankGrain())
    scrollToRow(NEW_ROW_ID)
  }

  async function createNew() {
    const input = newRef.current
    if (!input || creating) return false
    setCreating(true)
    const r = await api.saveGrain(version.id, input)
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
    <GrainPanel
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
    <GrainPanel key={focused.id} version={version} readOnly={readOnly} grain={focused} seed={focused} onClose={() => onEdit(null)} onOpen={onOpen} />
  ) : null

  return (
    <div className="flex flex-col gap-4">
      <SectionHeader
        title="Grains"
        help="What makes a row, or a series over time, unique. The row grain anchors detail paging; time-aware measures pick first, last or boundary observations within each series."
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
                <Th>Grain</Th>
                <Th>Kind</Th>
                <Th>Components, in order</Th>
                <Th>Selected by</Th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {version.grains.map((g: DatasetGrain) => {
                const deps = version.dependents[g.id] ?? []
                return (
                  <tr key={g.id} data-row-id={g.id} aria-selected={g.id === editingId} onClick={() => focus(g.id)} className={focusRowClass(g.id === editingId)}>
                    <Td className="font-mono text-xs">{g.key}</Td>
                    <Td>
                      {readOnly ? (
                        <span className="text-xs capitalize">{g.grainKind}</span>
                      ) : (
                        <InlineSelect
                          aria-label={`Kind of ${g.key}`}
                          value={g.grainKind}
                          options={kindOptions}
                          onCommit={(k) => run(() => api.saveGrain(version.id, { ...g, grainKind: k as DatasetGrainInput['grainKind'] }))}
                          className="w-28 text-xs"
                        />
                      )}
                    </Td>
                    <Td>
                      <span className="flex flex-wrap items-center gap-1">
                        {g.fieldIds.map((id, i) => (
                          <Chip key={id}>
                            <span className="text-muted-foreground">{i + 1}</span> {fieldKey(id)}
                          </Chip>
                        ))}
                      </span>
                    </Td>
                    <Td>
                      {deps.length ? (
                        <span className="flex flex-wrap gap-1">
                          {deps.map((d) => (
                            <button key={`${d.itemId}-${d.relation}`} type="button" onClick={() => onOpen(d.tab, d.itemId)} className="font-mono text-xs text-primary hover:underline">
                              {d.itemKey}
                            </button>
                          ))}
                        </span>
                      ) : (
                        <span className="text-xs text-muted-foreground">—</span>
                      )}
                    </Td>
                  </tr>
                )
              })}
              {!readOnly && newDraft && (
                <NewGrainRow draft={newDraft} problem={newProblem} creating={creating} kindOptions={kindOptions} fieldKey={fieldKey} onChange={editNew} onSubmit={createNew} onCancel={cancelNew} />
              )}
              {!version.grains.length && !newDraft && (
                <tr>
                  <Td colSpan={4} className="py-8 text-center text-muted-foreground">
                    {readOnly ? 'No grains in this version.' : 'No grains yet. Use “Add new row” to define one.'}
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

const rowInputClass =
  'h-8 w-full min-w-0 rounded-md border border-input bg-background px-2 text-sm outline-none placeholder:text-muted-foreground/70 focus:border-ring focus:ring-2 focus:ring-ring/30 aria-invalid:border-destructive'

/** The pending grain: a selected, editable row bound to the same draft as the open properties panel. */
function NewGrainRow({
  draft,
  problem,
  creating,
  kindOptions,
  fieldKey,
  onChange,
  onSubmit,
  onCancel,
}: {
  draft: DatasetGrainInput
  problem: Problem | null
  creating: boolean
  kindOptions: { value: string; label: string }[]
  fieldKey: (id: string) => string
  onChange: (p: Partial<DatasetGrainInput>) => void
  onSubmit: () => void
  onCancel: () => void
}) {
  const errs = problem?.errors ?? {}
  const messages = Object.values(errs).flat()
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

  return (
    <tr data-row-id={NEW_ROW_ID} aria-selected aria-label="New grain" className={cn(focusRowClass(true), 'align-top')}>
      <Td>
        <input
          autoFocus
          aria-label="New grain: grain key"
          value={draft.key}
          placeholder="key"
          disabled={creating}
          aria-invalid={!!errs.key}
          onChange={(e) => onChange({ key: e.target.value })}
          onKeyDown={onKeyDown}
          className={cn(rowInputClass, 'w-44 font-mono text-xs')}
        />
      </Td>
      <Td className="whitespace-nowrap">
        <InlineSelect aria-label="New grain: kind" value={draft.grainKind} options={kindOptions} onCommit={(k) => onChange({ grainKind: k as DatasetGrainInput['grainKind'] })} className="w-28 text-xs" />
      </Td>
      <Td>
        <span className="flex flex-wrap items-center gap-1 pt-1.5">
          {draft.fieldIds.length ? (
            draft.fieldIds.map((id, i) => (
              <Chip key={id}>
                <span className="text-muted-foreground">{i + 1}</span> {fieldKey(id)}
              </Chip>
            ))
          ) : (
            <span className="text-xs text-muted-foreground">Pick components in the panel</span>
          )}
        </span>
      </Td>
      <Td>
        <span className="flex items-start justify-between gap-3 pt-1">
          <span role={messages.length ? 'alert' : undefined} className={cn('text-xs text-pretty', messages.length ? 'text-destructive' : 'text-muted-foreground')}>
            {messages.length ? messages.join(' ') : null}
          </span>
        </span>
      </Td>
    </tr>
  )
}

function GrainPanel({
  version,
  readOnly,
  grain,
  seed,
  onClose,
  onOpen,
  onDraftCommit,
  createProblem,
  onDismissCreateProblem,
}: {
  version: DatasetVersion
  readOnly: boolean
  grain?: DatasetGrain
  seed: DatasetGrainInput
  onClose: () => void
  onOpen: SectionProps['onOpen']
  /** New grains only: panel edits update the pending row's draft instead of saving. */
  onDraftCommit?: (draft: DatasetGrainInput) => void
  createProblem?: Problem | null
  onDismissCreateProblem?: () => void
}) {
  const api = useDatasetsApi()
  const { data: vocab } = useVocabularies()
  const { run, runOk, saving, problem, setProblem } = useApiCall()
  const shown = problem ?? createProblem ?? null
  const errorsFor = (path: string) => shown?.errors[path]
  const [dragIndex, setDragIndex] = useState<number | null>(null)

  const { draft, setDraft, pending, bodyProps } = usePanelDraft(seed, async (next) => {
    if (readOnly) return false
    if (!grain) {
      onDraftCommit?.(next)
      return true
    }
    return !!(await run(() => api.saveGrain(version.id, next)))
  })

  const timeIds = timeFieldIds(version)
  const deps = grain ? (version.dependents[grain.id] ?? []) : []
  const blocking = deps.filter((d) => d.blocksDelete)
  const field = (id: string) => version.fields.find((f) => f.id === id)
  const remaining = version.fields.filter((f) => !draft.fieldIds.includes(f.id))
  const move = (from: number, to: number) =>
    setDraft((d) => {
      if (to < 0 || to >= d.fieldIds.length) return d
      const ids = [...d.fieldIds]
      const [x] = ids.splice(from, 1)
      ids.splice(to, 0, x)
      return { ...d, fieldIds: ids }
    })

  return (
    <PropertiesPanel
      size="lg"
      label="Grain properties"
      title={<span className="font-mono">{draft.key || 'New grain'}</span>}
      subtitle={!grain ? 'Not added yet · needs a key and at least one component.' : pending && shown ? <span className="text-destructive">Not applied — fix the highlighted inputs.</span> : undefined}
      onClose={onClose}
      bodyProps={bodyProps}
      actions={
        grain &&
        !readOnly && (
          <Button
            variant="ghost"
            size="icon-xs"
            aria-label={`Delete ${grain.key}`}
            title={blocking.length ? `Selected by ${blocking.map((d) => d.itemKey).join(', ')}` : 'Delete grain'}
            disabled={!!blocking.length || saving}
            onClick={async () => {
              if (await runOk(() => api.deleteGrain(version.id, grain.id))) {
                toast.success(`Deleted ${grain.key}`)
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
            <FormRow label="Grain key" htmlFor="g-key" errors={errorsFor('key')} hint={grain ? 'Fixed after creation.' : 'e.g. rowIdentity, temporalSeries'}>
              <CommitInput id="g-key" value={draft.key} disabled={!!grain} onCommit={(v) => setDraft((d) => ({ ...d, key: v }))} className="font-mono" aria-invalid={!!errorsFor('key')} />
            </FormRow>
            <FormRow label="Kind" htmlFor="g-kind" errors={errorsFor('grainKind')} hint={vocab?.grainKind.find((t) => t.value === draft.grainKind)?.description}>
              <VocabSelect id="g-kind" terms={vocab?.grainKind} value={draft.grainKind} onChange={(k) => setDraft((d) => ({ ...d, grainKind: k ?? 'row' }))} />
            </FormRow>
          </div>
        </PanelSection>

        <PanelSection
          title="Components"
          description={draft.grainKind === 'series' ? 'Fields that identify one trajectory over time. Leave out the observation time.' : 'Fields that together identify one logical row. Order is the component ordinal.'}
        >
          {errorsFor('fieldIds') && (
            <ul className="text-xs text-destructive">
              {errorsFor('fieldIds')!.map((e) => (
                <li key={e}>{e}</li>
              ))}
            </ul>
          )}
          <ol className="flex flex-col gap-1.5">
            {draft.fieldIds.map((id, i) => {
              const f = field(id)
              const isTime = timeIds.has(id)
              return (
                <li
                  key={id}
                  draggable={!readOnly}
                  onDragStart={() => setDragIndex(i)}
                  onDragOver={(e) => e.preventDefault()}
                  onDrop={() => {
                    if (dragIndex != null) move(dragIndex, i)
                    setDragIndex(null)
                  }}
                  onDragEnd={() => setDragIndex(null)}
                  className={cn(
                    'flex items-center gap-2 rounded-md border bg-background px-2 py-1.5 text-sm',
                    dragIndex === i && 'opacity-50',
                    isTime && draft.grainKind === 'series' && 'border-warning/60 bg-warning-muted',
                  )}
                >
                  {!readOnly && <GripVerticalIcon aria-hidden className="size-4 cursor-grab text-muted-foreground" />}
                  <span className="w-5 text-right font-mono text-xs text-muted-foreground">{i + 1}</span>
                  <span className="font-mono text-xs">{f?.key ?? '?'}</span>
                  <span className="truncate text-xs text-muted-foreground">{f?.displayName}</span>
                  {isTime && (
                    <Chip tone={draft.grainKind === 'series' ? 'warn' : 'primary'} className="ml-1">
                      <ClockIcon className="size-3" /> time
                    </Chip>
                  )}
                  {!readOnly && (
                    <span className="ml-auto flex items-center">
                      <Button variant="ghost" size="icon-sm" aria-label={`Move ${f?.key} up`} disabled={i === 0} onClick={() => move(i, i - 1)}>
                        <ArrowUpIcon />
                      </Button>
                      <Button variant="ghost" size="icon-sm" aria-label={`Move ${f?.key} down`} disabled={i === draft.fieldIds.length - 1} onClick={() => move(i, i + 1)}>
                        <ArrowDownIcon />
                      </Button>
                      <Button variant="ghost" size="icon-sm" aria-label={`Remove ${f?.key}`} onClick={() => setDraft((d) => ({ ...d, fieldIds: d.fieldIds.filter((x) => x !== id) }))}>
                        <XIcon />
                      </Button>
                    </span>
                  )}
                </li>
              )
            })}
            {!draft.fieldIds.length && <li className="rounded-md border border-dashed px-3 py-4 text-center text-xs text-muted-foreground">No components yet.</li>}
          </ol>
          {!readOnly && (
            <SimpleSelect
              aria-label="Add a component field"
              value=""
              placeholder="Add a field…"
              onChange={(id) => setDraft((d) => ({ ...d, fieldIds: [...d.fieldIds, id] }))}
              options={remaining.map((f) => ({ value: f.id, label: f.key, hint: timeIds.has(f.id) ? (draft.grainKind === 'series' ? 'time field · excluded from series' : 'time field') : f.dataType }))}
            />
          )}
        </PanelSection>
      </fieldset>

      {grain && (
        <PanelSection title="Selected by">
          <DependentsList dependents={deps} onOpen={(d) => onOpen(d.tab, d.itemId)} empty="No measure time policy selects by this grain." />
        </PanelSection>
      )}
    </PropertiesPanel>
  )
}

