import { ArrowLeftIcon, ArrowRightIcon, ClipboardPasteIcon, CopyIcon, FileTextIcon, PencilIcon, PlusIcon, Trash2Icon } from 'lucide-react'
import { useMemo, useState } from 'react'
import { toast } from 'sonner'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'
import { Textarea } from '@/components/ui/textarea'
import { ConfirmDialog, EmptyState, Field, Mono, ProblemBanner, StatusBadge, TabIntro, Td, Th, WithReason } from '@/components/dimensions/shared'
import { SearchCombobox } from '@/components/report/shared/search-combobox'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { SAVE_FIRST, useHasPendingChanges } from '@/components/shell/changes'
import { useHistory, useUndoAction } from '@/components/shell/history'
import { useAppNav, useSearchParam, useSetSearchParams } from '@/lib/navigation'
import { unwrap } from '@/lib/api/contract'
import type { DimensionDetail } from '@/lib/dimensions/contract'
import { BridgeEditScope, useBridge, useBridgeDeleteCheck, useBridges, useBridgeVersion, useDimensionRefs, useMappingsApi, usePublishedMembers } from '@/lib/mappings/client'
import {
  ALLOCATION_HELP,
  ALLOCATION_LABEL,
  allowedModes,
  CARDINALITY_HELP,
  CARDINALITY_LABEL,
  canHaveBridges,
  isEditableStatus,
  liveVersionOf,
  newTempId,
  SETTINGS_VIOLATION_CODES,
  type BridgeDetail,
  type BridgeListItem,
  type BridgeMapping,
  type BridgeVersion,
  type BridgeVersionContent,
  type BridgeVersionPatch,
  type MappingAllocationMode,
  type MappingCardinality,
  type MemberRef,
  type ValidationReport,
} from '@/lib/mappings/contract'
import { bridgeMappingsToText, fmt, indexMembers, nearlyEqual, previewBridgePaste, violationsByTarget, type MemberIndex } from '@/lib/mappings/rules'
import { useEditor } from '@/lib/report/store'
import { cn } from '@/lib/utils'
import { BlockedDeleteDialog, memberOptions, PinsList, PREVIEW_TONE, RowIssues, useMapCall, ValidationList, VersionBar, withoutKey } from './shared'

export function BridgesSection({ dim }: { dim: DimensionDetail }) {
  const [br] = useSearchParam('br')
  const setSearchParams = useSetSearchParams()
  if (br) return <BridgeEditor id={br} dimId={dim.id} onBack={() => setSearchParams({ br: null, brv: null })} />
  return <BridgeList dim={dim} />
}

/* ───────────────────────── list ───────────────────────── */

function BridgeList({ dim }: { dim: DimensionDetail }) {
  const { data: bridges } = useBridges(dim.id)
  const setSearchParams = useSetSearchParams()
  const [creating, setCreating] = useState(false)
  const open = (id: string) => setSearchParams({ br: id, brv: null })
  const isReportDim = dim.purpose === 'report'
  const outgoing = bridges?.filter((b) => b.sourceDimensionId === dim.id) ?? []
  const incoming = bridges?.filter((b) => b.targetDimensionId === dim.id && !b.owner) ?? []
  const visibleCount = outgoing.length + incoming.length

  return (
    <div className="flex flex-col gap-5">
      <TabIntro
        action={
          !isReportDim && (
            <Button size="sm" onClick={() => setCreating(true)}>
              <PlusIcon /> New bridge
            </Button>
          )
        }
      >
        Bridges re-express data keyed by one dimension in terms of another, with a factor and an allocation weight per pair.
        {isReportDim
          ? ' This is a report dimension: the report bridges into it are listed read-only on their source dimensions and edited on the report’s Rows page.'
          : ' Report bridges appear read-only under From on their source dimension and are edited on the report’s Rows page.'}
      </TabIntro>
      {!bridges ? (
        <p className="text-sm text-muted-foreground">Loading bridges…</p>
      ) : !visibleCount ? (
        <EmptyState title="No bridges yet" action={!isReportDim && <Button size="sm" variant="outline" onClick={() => setCreating(true)}><PlusIcon /> New bridge</Button>}>
          Add one to roll {dim.displayName} up into, or derive it from, another dimension.
        </EmptyState>
      ) : (
        <>
          <BridgeTable title={`From ${dim.displayName}`} empty={`No bridge starts at ${dim.displayName}.`} bridges={outgoing} other="target" onOpen={open} />
          <BridgeTable title={`Into ${dim.displayName}`} empty={`No bridge ends at ${dim.displayName}.`} bridges={incoming} other="source" onOpen={open} />
        </>
      )}
      <CreateBridgeDialog open={creating} onOpenChange={setCreating} dim={dim} onCreated={open} />
    </div>
  )
}

function BridgeTable({ title, empty, bridges, other, onOpen }: { title: string; empty: string; bridges: BridgeListItem[]; other: 'source' | 'target'; onOpen: (id: string) => void }) {
  return (
    <section className="flex flex-col gap-2" aria-label={title}>
      <h3 className="text-sm font-medium">{title}</h3>
      {!bridges.length ? (
        <p className="text-sm text-muted-foreground">{empty}</p>
      ) : (
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-sm">
            <thead className="border-b bg-muted/40">
              <tr>
                <Th>Bridge</Th>
                <Th>{other === 'target' ? 'Target' : 'Source'}</Th>
                <Th>Settings</Th>
                <Th>Live</Th>
                <Th>Frozen</Th>
                <Th className="text-right">Used by</Th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {bridges.map((b) => {
                const live = liveVersionOf(b.versions)
                const frozenCount = b.versions.length - 1
                return (
                  <tr key={b.id} className={cn('hover:bg-muted/40', !b.isEnabled && 'text-muted-foreground')}>
                    <Td>
                      {b.owner ? (
                        <div className="flex flex-col items-start">
                          <span className="flex items-center gap-2 font-medium">
                            {b.displayName}
                            <Badge variant="outline" className="gap-1 font-normal">
                              <FileTextIcon aria-hidden className="size-3" /> Report
                            </Badge>
                          </span>
                          <Mono>{b.key}</Mono>
                        </div>
                      ) : (
                        <button type="button" onClick={() => onOpen(b.id)} className="flex flex-col items-start text-left hover:underline">
                          <span className="flex items-center gap-2 font-medium">
                            {b.displayName}
                            {!b.isEnabled && <Badge variant="outline" className="font-normal">Disabled</Badge>}
                          </span>
                          <Mono>{b.key}</Mono>
                        </button>
                      )}
                    </Td>
                    <Td>{(other === 'target' ? b.target : b.source).displayName}</Td>
                    <Td className="whitespace-nowrap">
                      {CARDINALITY_LABEL[live.cardinality]} · {ALLOCATION_LABEL[live.allocationMode]}
                    </Td>
                    <Td className="tabular-nums">
                      v{live.versionNumber} · {b.publishedMappingCount} rows
                    </Td>
                    <Td className="tabular-nums">{frozenCount ? `${frozenCount} version${frozenCount === 1 ? '' : 's'}` : <span className="text-muted-foreground">—</span>}</Td>
                    <Td className="text-right tabular-nums">
                      {b.owner ? <OpenInReportEditor reportKey={b.owner.reportKey} /> : b.bindings.length ? `${b.bindings.length} binding${b.bindings.length === 1 ? '' : 's'}` : <span className="text-muted-foreground">Unused</span>}
                    </Td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}

function CreateBridgeDialog({ open, onOpenChange, dim, onCreated }: { open: boolean; onOpenChange: (o: boolean) => void; dim: DimensionDetail; onCreated: (id: string) => void }) {
  const api = useMappingsApi()
  const mc = useMapCall()
  const { data: refs } = useDimensionRefs()
  const blank = { key: '', displayName: '', otherId: '', direction: 'out' as 'out' | 'in', isEnabled: true }
  const [form, setForm] = useState(blank)
  const others = (refs ?? []).filter((r) => r.id !== dim.id && r.purpose === 'analytical' && canHaveBridges(r))
  const other = others.find((r) => r.id === form.otherId)
  const close = (o: boolean) => {
    onOpenChange(o)
    if (!o) {
      setForm(blank)
      mc.setProblem(null)
    }
  }
  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent className="sm:max-w-md">
        <form
          className="flex flex-col gap-4"
          onSubmit={async (e) => {
            e.preventDefault()
            const [sourceDimensionId, targetDimensionId] = form.direction === 'out' ? [dim.id, form.otherId] : [form.otherId, dim.id]
            const r = await mc.run(() => api.createBridge({ key: form.key, displayName: form.displayName, sourceDimensionId, targetDimensionId, isEnabled: form.isEnabled }), 'Bridge created')
            if (r.ok) {
              close(false)
              onCreated(r.data.bridgeId)
            }
          }}
        >
          <DialogHeader>
            <DialogTitle>New bridge</DialogTitle>
            <DialogDescription>Starts empty, live and many-to-one. Settings can change at any time.</DialogDescription>
          </DialogHeader>
          <Field label="Direction">
            <SimpleSelect
              aria-label="Direction"
              value={form.direction}
              onChange={(v) => setForm({ ...form, direction: v as 'out' | 'in' })}
              options={[
                { value: 'out', label: `From ${dim.displayName} into another dimension` },
                { value: 'in', label: `From another dimension into ${dim.displayName}` },
              ]}
            />
          </Field>
          <Field label={form.direction === 'out' ? 'Target dimension' : 'Source dimension'} required errors={mc.errorsFor(form.direction === 'out' ? 'targetDimensionId' : 'sourceDimensionId')}>
            <SimpleSelect aria-label="Other dimension" value={form.otherId} onChange={(otherId) => setForm({ ...form, otherId })} placeholder="Choose dimension…" options={others.map((r) => ({ value: r.id, label: r.displayName }))} />
          </Field>
          {other && (
            <p className="flex items-center gap-2 text-sm text-muted-foreground">
              {form.direction === 'out' ? dim.displayName : other.displayName}
              <ArrowRightIcon aria-hidden className="size-3.5" />
              {form.direction === 'out' ? other.displayName : dim.displayName}
            </p>
          )}
          <Field label="Key" htmlFor="br-key" required errors={mc.errorsFor('key')} hint="E.g. branch_to_region. Can't change later.">
            <Input id="br-key" className="font-mono" value={form.key} onChange={(e) => setForm({ ...form, key: e.target.value })} />
          </Field>
          <Field label="Display name" htmlFor="br-name" required errors={mc.errorsFor('displayName')}>
            <Input id="br-name" value={form.displayName} onChange={(e) => setForm({ ...form, displayName: e.target.value })} />
          </Field>
          <label className="flex items-center gap-2 text-sm">
            <Switch checked={form.isEnabled} onCheckedChange={(isEnabled) => setForm({ ...form, isEnabled })} />
            Enabled for dataset bindings
          </label>
          <ProblemBanner problem={mc.problem && !Object.keys(mc.problem.errors).some((k) => k !== '') ? mc.problem : null} />
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => close(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={mc.saving || !form.otherId}>
              {mc.saving ? 'Creating…' : 'Create'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function OpenInReportEditor({ reportKey }: { reportKey: string }) {
  const { library } = useEditor()
  const nav = useAppNav()
  const item = library.find((i) => i.report.settings.key === reportKey)
  if (!item) return <Mono>{reportKey}</Mono>
  return (
    <Button variant="link" size="sm" className="h-auto gap-1 p-0" onClick={() => nav.report(item.id)}>
      <FileTextIcon aria-hidden className="size-3.5" />
      Open {item.report.settings.name || reportKey}
      <span className="sr-only"> in the report editor</span>
    </Button>
  )
}

/* ───────────────────────── editor ───────────────────────── */

function BridgeEditor({ id, dimId, onBack }: { id: string; dimId: string; onBack: () => void }) {
  const { data: bridge, error } = useBridge(id)
  if (error)
    return (
      <EmptyState title="Bridge not found" action={<Button size="sm" variant="outline" onClick={onBack}>Back to bridges</Button>}>
        It may have been deleted.
      </EmptyState>
    )
  if (!bridge) return <p className="text-sm text-muted-foreground">Loading bridge…</p>
  if (bridge.owner)
    return (
      <EmptyState
        title="Report bridges have no page here"
        action={
          <div className="flex items-center gap-3">
            <Button size="sm" variant="outline" onClick={onBack}>Back to bridges</Button>
            <OpenInReportEditor reportKey={bridge.owner.reportKey} />
          </div>
        }
      >
        <Mono>{bridge.key}</Mono> is owned by report <Mono>{bridge.owner.reportKey}</Mono> and is edited on its Rows page.
      </EmptyState>
    )
  return (
    <BridgeEditScope bridgeId={bridge.id} label={bridge.displayName}>
      <BridgePage bridge={bridge} dimId={dimId} onBack={onBack} />
    </BridgeEditScope>
  )
}

function BridgePage({ bridge, dimId, onBack }: { bridge: BridgeDetail; dimId: string; onBack: () => void }) {
  const [brv, setBrv] = useSearchParam('brv')
  const selected = bridge.versions.find((v) => v.id === brv) ?? liveVersionOf(bridge.versions)
  const { data: content } = useBridgeVersion(selected.id)
  const { data: srcMembers } = usePublishedMembers(bridge.sourceDimensionId)
  const { data: tgtMembers } = usePublishedMembers(bridge.targetDimensionId)
  const [reports, setReports] = useState<Record<string, ValidationReport>>({})
  const [filter, setFilter] = useState('')
  const ready = content && content.version.id === selected.id ? content : null
  const report = reports[selected.id]

  return (
    <div className="flex flex-col gap-5">
      <BridgeHeader bridge={bridge} dimId={dimId} onBack={onBack} />
      <VersionBar kind="bridge" versions={bridge.versions} selected={selected} pins={bridge.pins} onSelect={setBrv} onReport={(r) => setReports((all) => ({ ...all, [r.versionId]: r }))} />
      {report && <ValidationList report={report} onFilter={setFilter} onDismiss={() => setReports((all) => withoutKey(all, selected.id))} />}
      {!ready ? (
        <p className="text-sm text-muted-foreground">Loading mappings…</p>
      ) : (
        <div className="grid gap-5 xl:grid-cols-[minmax(0,1fr)_20rem]">
          <MappingsTable bridge={bridge} content={ready} srcMembers={srcMembers} tgtMembers={tgtMembers} filter={filter} setFilter={setFilter} />
          <aside className="flex flex-col gap-5">
            <SettingsCard version={ready.version} />
            <BridgeUsage bridge={bridge} />
          </aside>
        </div>
      )}
    </div>
  )
}

function BridgeHeader({ bridge, dimId, onBack }: { bridge: BridgeDetail; dimId: string; onBack: () => void }) {
  const api = useMappingsApi()
  const mc = useMapCall()
  const dirty = useHasPendingChanges()
  const [deleting, setDeleting] = useState(false)
  const { data: deleteCheck } = useBridgeDeleteCheck(deleting ? bridge.id : null)

  return (
    <div className="flex flex-col gap-3">
      <Button variant="ghost" size="sm" className="-ml-2 self-start text-muted-foreground" onClick={onBack}>
        <ArrowLeftIcon /> All bridges
      </Button>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex min-w-0 flex-col gap-1">
          <h2 className="text-lg font-semibold text-balance">{bridge.displayName}</h2>
          <p className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-muted-foreground">
            <Mono>{bridge.key}</Mono>
            <span className="flex items-center gap-1.5">
              <span className={cn(bridge.sourceDimensionId === dimId && 'text-foreground')}>{bridge.source.displayName}</span>
              <ArrowRightIcon aria-label="to" className="size-3.5" />
              <span className={cn(bridge.targetDimensionId === dimId && 'text-foreground')}>{bridge.target.displayName}</span>
            </span>
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <label className="flex items-center gap-2 text-sm">
            <Switch checked={bridge.isEnabled} disabled={mc.saving} onCheckedChange={(isEnabled) => mc.run(() => api.updateBridge(bridge.id, { isEnabled }))} />
            Enabled
          </label>
          <WithReason reason={dirty ? SAVE_FIRST : null}>
            <Button variant="ghost" size="sm" className="text-destructive hover:text-destructive" disabled={dirty} onClick={() => setDeleting(true)}>
              <Trash2Icon /> Delete bridge
            </Button>
          </WithReason>
        </div>
      </div>
      <BlockedDeleteDialog
        open={deleting}
        onOpenChange={setDeleting}
        title={`Delete ${bridge.displayName}?`}
        description="Deleting removes every version and its rows."
        check={deleteCheck}
        onConfirm={async () => {
          const r = await mc.run(() => api.deleteBridge(bridge.id), 'Bridge deleted')
          if (r.ok) onBack()
          return r.ok
        }}
      />
    </div>
  )
}

function SettingsCard({ version }: { version: BridgeVersion }) {
  const api = useMappingsApi()
  const mc = useMapCall()
  const history = useHistory()
  const editable = isEditableStatus(version.status)
  const [flagged, setFlagged] = useState<{ settings: BridgeVersionPatch; count: number } | null>(null)

  const apply = async (next: BridgeVersionPatch) => {
    const settings = allowedModes(next.cardinality).includes(next.allocationMode) ? next : { ...next, allocationMode: allowedModes(next.cardinality)[0] }
    const r = await mc.run(() => api.updateBridgeVersion(version.id, settings))
    if (!r.ok) return
    const { violations } = await unwrap(api.getBridgeVersion(version.id))
    const count = violations.filter((v) => (SETTINGS_VIOLATION_CODES as string[]).includes(v.code)).length
    if (count) setFlagged({ settings, count })
  }
  const settingsLabel = (s: BridgeVersionPatch) => `${CARDINALITY_LABEL[s.cardinality].toLowerCase()} · ${ALLOCATION_LABEL[s.allocationMode].toLowerCase()}`

  return (
    <section aria-labelledby="bs-h" className="flex flex-col gap-3 rounded-lg border bg-card p-3">
      <h3 id="bs-h" className="text-sm font-medium">
        Settings · v{version.versionNumber}
      </h3>
      {editable ? (
        <>
          <Field label="Cardinality" hint={CARDINALITY_HELP[version.cardinality]}>
            <SimpleSelect
              aria-label="Cardinality"
              size="sm"
              value={version.cardinality}
              onChange={(v) => apply({ cardinality: v as MappingCardinality, allocationMode: version.allocationMode })}
              options={(Object.keys(CARDINALITY_LABEL) as MappingCardinality[]).map((c) => ({ value: c, label: CARDINALITY_LABEL[c] }))}
            />
          </Field>
          <Field label="Allocation" hint={ALLOCATION_HELP[version.allocationMode]}>
            <SimpleSelect
              aria-label="Allocation"
              size="sm"
              value={version.allocationMode}
              onChange={(v) => apply({ cardinality: version.cardinality, allocationMode: v as MappingAllocationMode })}
              options={allowedModes(version.cardinality).map((m) => ({ value: m, label: ALLOCATION_LABEL[m] }))}
            />
          </Field>
        </>
      ) : (
        <div className="flex flex-col gap-2 text-sm">
          <p>
            {CARDINALITY_LABEL[version.cardinality]} · {ALLOCATION_LABEL[version.allocationMode]}
          </p>
          <p className="text-xs text-pretty text-muted-foreground">{ALLOCATION_HELP[version.allocationMode]}</p>
        </div>
      )}
      <ConfirmDialog
        open={!!flagged}
        onOpenChange={async (o) => {
          if (o) return
          setFlagged(null)
          await history.undo()
        }}
        title="Keep the new settings?"
        confirmLabel="Keep change"
        onConfirm={() => setFlagged(null)}
      >
        {flagged && (
          <>
            {flagged.count} mapping{flagged.count === 1 ? '' : 's'} won&apos;t satisfy {settingsLabel(flagged.settings)}. They&apos;re flagged, and the change can&apos;t be saved until
            they&apos;re fixed. Cancel undoes the change.
          </>
        )}
      </ConfirmDialog>
    </section>
  )
}

function BridgeUsage({ bridge }: { bridge: BridgeDetail }) {
  return (
    <section aria-labelledby="bu-h" className="flex flex-col gap-3 rounded-lg border bg-card p-3">
      <h3 id="bu-h" className="text-sm font-medium">
        Used by
      </h3>
      {bridge.bindings.length ? (
        <ul className="flex flex-col gap-2 text-sm">
          {bridge.bindings.map((b) => (
            <li key={`${b.datasetVersionId}-${b.fieldKey}-${b.roleKey}`} className="flex flex-col">
              <span className="flex items-center gap-2">
                <Mono className="text-foreground">{b.datasetKey}</Mono>
                <StatusBadge status={b.status} label={`v${b.datasetVersionNumber}`} />
              </span>
              <span className="text-xs text-muted-foreground">
                <Mono>{b.fieldKey}</Mono>: role <Mono>{b.sourceRoleKey}</Mono> → <Mono>{b.roleKey}</Mono>
              </span>
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-sm text-muted-foreground">No dataset binds through this bridge yet.</p>
      )}
      <PinsList pins={bridge.pins} />
    </section>
  )
}

/* ───────────────────────── mappings ───────────────────────── */

type RowDraft = { id: string; sourceMemberKey: string; targetMemberKey: string; factor: string; allocationWeight: string }
const blankRow = (): RowDraft => ({ id: newTempId(), sourceMemberKey: '', targetMemberKey: '', factor: '1', allocationWeight: '1' })

function MappingsTable({
  bridge,
  content,
  srcMembers,
  tgtMembers,
  filter,
  setFilter,
}: {
  bridge: BridgeDetail
  content: BridgeVersionContent
  srcMembers: MemberRef[] | null | undefined
  tgtMembers: MemberRef[] | null | undefined
  filter: string
  setFilter: (v: string) => void
}) {
  const api = useMappingsApi()
  const mc = useMapCall()
  const undoAction = useUndoAction()
  const { version, mappings } = content
  const srcIdx = useMemo(() => indexMembers(srcMembers), [srcMembers])
  const tgtIdx = useMemo(() => indexMembers(tgtMembers), [tgtMembers])
  const srcOptions = useMemo(() => memberOptions(srcMembers), [srcMembers])
  const tgtOptions = useMemo(() => memberOptions(tgtMembers, { assignableOnly: true }), [tgtMembers])
  const issues = useMemo(() => violationsByTarget(content.violations), [content.violations])
  const sums = useMemo(() => new Map(content.allocation.sources.map((s) => [s.sourceMemberKey, s])), [content.allocation])
  const [issuesOnly, setIssuesOnly] = useState(false)
  const [draft, setDraft] = useState<RowDraft>(blankRow)
  const [editing, setEditing] = useState<RowDraft | null>(null)
  const [pasting, setPasting] = useState(false)
  const editable = isEditableStatus(version.status)
  const showSums = version.allocationMode === 'partition'

  const rowIssues = (m: BridgeMapping) => [...(issues.byId.get(m.id) ?? []), ...(issues.byKey.get(m.sourceMemberKey) ?? [])]
  const flaggedCount = mappings.filter((m) => rowIssues(m).length).length

  const q = filter.trim().toLowerCase()
  const label = (idx: MemberIndex | null, key: string) => idx?.get(key)?.displayName ?? ''
  const rows = [...mappings]
    .filter((m) => !issuesOnly || rowIssues(m).length)
    .filter((m) => !q || [m.sourceMemberKey, m.targetMemberKey, label(srcIdx, m.sourceMemberKey), label(tgtIdx, m.targetMemberKey)].some((s) => s.toLowerCase().includes(q)))
    .sort((a, b) => a.sourceMemberKey.localeCompare(b.sourceMemberKey, undefined, { numeric: true }) || a.targetMemberKey.localeCompare(b.targetMemberKey))

  const toInput = (r: RowDraft) => ({ id: r.id, sourceMemberKey: r.sourceMemberKey, targetMemberKey: r.targetMemberKey, factor: Number(r.factor), allocationWeight: Number(r.allocationWeight) })

  const remove = async (m: BridgeMapping) => {
    const r = await mc.run(() => api.deleteBridgeMapping(version.id, m.id))
    if (r.ok) toast.success(`Removed ${m.sourceMemberKey} → ${m.targetMemberKey}`, { action: undoAction() })
  }

  const rowEditor = (r: RowDraft, set: (r: RowDraft) => void, idPrefix: string) => (
    <>
      <Td className="align-top">
        <SearchCombobox aria-label={`${idPrefix} source member`} className="h-8 w-full max-w-56" value={r.sourceMemberKey} onChange={(sourceMemberKey) => set({ ...r, sourceMemberKey })} options={srcOptions} limit={200} placeholder="Source…" />
      </Td>
      <Td className="align-top">
        <SearchCombobox aria-label={`${idPrefix} target member`} className="h-8 w-full max-w-56" value={r.targetMemberKey} onChange={(targetMemberKey) => set({ ...r, targetMemberKey })} options={tgtOptions} limit={200} placeholder="Target…" />
      </Td>
      <Td className="align-top">
        <Input aria-label={`${idPrefix} factor`} inputMode="decimal" className="h-8 w-20 text-right font-mono" value={r.factor} onChange={(e) => set({ ...r, factor: e.target.value })} />
      </Td>
      <Td className="align-top">
        <Input aria-label={`${idPrefix} weight`} inputMode="decimal" className="h-8 w-20 text-right font-mono" value={r.allocationWeight} onChange={(e) => set({ ...r, allocationWeight: e.target.value })} />
      </Td>
    </>
  )

  const errors = mc.problem ? Object.values(mc.problem.errors).flat() : []

  return (
    <section aria-label="Bridge mappings" className="flex min-w-0 flex-col gap-3">
      <div className="flex flex-wrap items-center gap-2">
        <Input type="search" placeholder="Search members" aria-label="Search mappings" className="h-8 w-64" value={filter} onChange={(e) => setFilter(e.target.value)} />
        <label className="flex items-center gap-2 text-sm text-muted-foreground">
          <Switch checked={issuesOnly} onCheckedChange={setIssuesOnly} />
          Issues only{flaggedCount > 0 && <span className="rounded-sm bg-destructive/15 px-1.5 font-mono text-[11px] text-destructive tabular-nums">{flaggedCount}</span>}
        </label>
        <span className="flex-1" />
        <Button
          size="sm"
          variant="ghost"
          onClick={async () => {
            await navigator.clipboard.writeText(bridgeMappingsToText(mappings))
            toast.success(`Copied ${mappings.length} rows`)
          }}
        >
          <CopyIcon /> Copy
        </Button>
        <WithReason reason={editable ? null : `v${version.versionNumber} is frozen and read-only`}>
          <Button size="sm" variant="outline" disabled={!editable} onClick={() => setPasting(true)}>
            <ClipboardPasteIcon /> Paste rows
          </Button>
        </WithReason>
      </div>
      <p className="text-sm text-muted-foreground">
        {editable ? 'Live version. Edits stay pending until you save; saving is refused while any row is flagged.' : `v${version.versionNumber} is a frozen snapshot and read-only.`}
      </p>
      {errors.length > 0 && (
        <ul role="alert" className="flex flex-col gap-0.5 text-sm text-destructive">
          {errors.map((e) => (
            <li key={e}>{e}</li>
          ))}
        </ul>
      )}

      <div className="overflow-x-auto rounded-lg border bg-card">
        <table className="w-full text-sm">
          <thead className="border-b bg-muted/40">
            <tr>
              <Th>{bridge.source.displayName}</Th>
              <Th>{bridge.target.displayName}</Th>
              <Th className="text-right">Factor</Th>
              <Th className="text-right">Weight</Th>
              <Th className="w-24">
                <span className="sr-only">Actions</span>
              </Th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {editable && (
              <tr className="bg-muted/20">
                {rowEditor(draft, setDraft, 'New')}
                <Td className="text-right align-top">
                  <Button
                    size="sm"
                    disabled={mc.saving || !draft.sourceMemberKey || !draft.targetMemberKey}
                    onClick={async () => {
                      const r = await mc.run(() => api.saveBridgeMapping(version.id, toInput(draft)))
                      if (r.ok) setDraft({ ...blankRow(), sourceMemberKey: draft.sourceMemberKey })
                    }}
                  >
                    Add
                  </Button>
                </Td>
              </tr>
            )}
            {rows.map((m, i) => {
              const firstOfSource = i === 0 || rows[i - 1].sourceMemberKey !== m.sourceMemberKey
              const sum = sums.get(m.sourceMemberKey)!
              if (editing?.id === m.id)
                return (
                  <tr key={m.id} className="bg-accent/40">
                    {rowEditor(editing, (r) => setEditing(r), 'Edit')}
                    <Td className="text-right align-top whitespace-nowrap">
                      <Button size="sm" variant="ghost" onClick={() => { setEditing(null); mc.setProblem(null) }}>
                        Cancel
                      </Button>
                      <Button
                        size="sm"
                        disabled={mc.saving}
                        onClick={async () => {
                          const r = await mc.run(() => api.saveBridgeMapping(version.id, toInput(editing)))
                          if (r.ok) setEditing(null)
                        }}
                      >
                        Apply
                      </Button>
                    </Td>
                  </tr>
                )
              const found = rowIssues(m)
              return (
                <tr key={m.id} className={cn('group', found.length && 'bg-destructive/5', !firstOfSource && 'border-t-transparent')}>
                  <Td>
                    {firstOfSource ? (
                      <div className="flex flex-col">
                        <span className="flex items-center gap-2">
                          {label(srcIdx, m.sourceMemberKey) || <span className="text-muted-foreground">Unknown</span>}
                          <Mono>{m.sourceMemberKey}</Mono>
                          {m.sourceSelection === 'descendants' && <Badge variant="outline" className="font-normal">+ descendants</Badge>}
                        </span>
                        {showSums && (
                          <span className={cn('font-mono text-[11px] tabular-nums', nearlyEqual(sum.weightSum, 1) ? 'text-muted-foreground' : 'text-destructive')}>Σ weight {fmt(sum.weightSum)}</span>
                        )}
                      </div>
                    ) : (
                      <span aria-label={`${m.sourceMemberKey}, continued`} className="pl-3 text-muted-foreground">
                        ↳
                      </span>
                    )}
                  </Td>
                  <Td>
                    <div className="flex flex-col">
                      <span className="flex items-center gap-2">
                        {label(tgtIdx, m.targetMemberKey) || <span className="text-muted-foreground">Unknown</span>}
                        <Mono>{m.targetMemberKey}</Mono>
                      </span>
                      <RowIssues messages={[...new Set(found.map((v) => v.message))]} />
                    </div>
                  </Td>
                  <Td className="text-right font-mono text-xs tabular-nums">{fmt(m.factor)}</Td>
                  <Td className="text-right font-mono text-xs tabular-nums">{fmt(m.allocationWeight)}</Td>
                  <Td className="text-right whitespace-nowrap">
                    {editable && (
                      <span className="inline-flex opacity-0 transition-opacity group-focus-within:opacity-100 group-hover:opacity-100">
                        <Button
                          size="icon-sm"
                          variant="ghost"
                          aria-label={`Edit ${m.sourceMemberKey} to ${m.targetMemberKey}`}
                          onClick={() => {
                            mc.setProblem(null)
                            setEditing({ id: m.id, sourceMemberKey: m.sourceMemberKey, targetMemberKey: m.targetMemberKey, factor: String(m.factor), allocationWeight: String(m.allocationWeight) })
                          }}
                        >
                          <PencilIcon />
                        </Button>
                        <Button size="icon-sm" variant="ghost" aria-label={`Delete ${m.sourceMemberKey} to ${m.targetMemberKey}`} onClick={() => remove(m)}>
                          <Trash2Icon />
                        </Button>
                      </span>
                    )}
                  </Td>
                </tr>
              )
            })}
            {!rows.length && (
              <tr>
                <Td colSpan={5} className="py-8 text-center text-muted-foreground">
                  {mappings.length ? 'No rows match.' : 'No rows yet. Add one above or paste a list.'}
                </Td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
      <p className="text-xs text-muted-foreground tabular-nums">
        {rows.length === mappings.length ? `${mappings.length} rows` : `${rows.length} of ${mappings.length} rows`} · {sums.size} source member{sums.size === 1 ? '' : 's'} · value × factor × weight lands in the target
      </p>
      {editable && <PasteBridgeDialog open={pasting} onOpenChange={setPasting} versionId={version.id} mappings={mappings} />}
    </section>
  )
}

function PasteBridgeDialog({ open, onOpenChange, versionId, mappings }: { open: boolean; onOpenChange: (o: boolean) => void; versionId: string; mappings: BridgeMapping[] }) {
  const api = useMappingsApi()
  const mc = useMapCall()
  const [text, setText] = useState('')
  const [keep, setKeep] = useState(true)
  const preview = useMemo(() => (text.trim() ? previewBridgePaste(mappings, text, keep) : null), [mappings, text, keep])
  const changes = preview ? preview.counts.new + preview.counts.changed + preview.counts.removed : 0
  const close = (o: boolean) => {
    onOpenChange(o)
    if (!o) setText('')
  }
  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent className="sm:max-w-3xl">
        <DialogHeader>
          <DialogTitle>Paste rows</DialogTitle>
          <DialogDescription>
            One per line: source key, target key, then optional factor and weight (both default to 1). Tab or comma separated; rows that break a rule are flagged after applying.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-4 md:grid-cols-2">
          <div className="flex flex-col gap-3">
            <Textarea aria-label="Rows to paste" className="h-64 font-mono text-xs" placeholder={'BR01\tNORTH\nBR02\tNORTH\t1\t0.5'} value={text} onChange={(e) => setText(e.target.value)} />
            <label className="flex items-center gap-2 text-sm">
              <Switch checked={keep} onCheckedChange={setKeep} />
              Keep rows that aren&apos;t in the paste
            </label>
          </div>
          <div className="flex min-h-0 flex-col gap-2">
            {!preview ? (
              <p className="text-sm text-muted-foreground">The preview shows what changes before anything is applied.</p>
            ) : (
              <>
                <p className="flex flex-wrap gap-x-3 text-xs tabular-nums">
                  {(Object.keys(preview.counts) as (keyof typeof preview.counts)[]).map((k) => (
                    <span key={k} className={PREVIEW_TONE[k]}>
                      {preview.counts[k]} {k}
                    </span>
                  ))}
                </p>
                <ul className="max-h-64 divide-y overflow-y-auto rounded-md border text-xs">
                  {preview.rows
                    .filter((r) => r.status !== 'unchanged')
                    .map((r) => (
                      <li key={r.key} className="flex items-center gap-2 px-2 py-1">
                        <span className={cn('w-16 shrink-0', PREVIEW_TONE[r.status])}>{r.status}</span>
                        <span className="font-mono">
                          {r.sourceMemberKey} → {r.targetMemberKey}
                        </span>
                        <span className="font-mono text-muted-foreground">
                          ×{fmt(r.factor)} w{fmt(r.allocationWeight)}
                          {r.previous && <s className="ml-1">{r.previous}</s>}
                        </span>
                        {r.error && <span className="ml-auto text-destructive">{r.line ? `Line ${r.line}: ` : ''}{r.error}</span>}
                      </li>
                    ))}
                </ul>
                {preview.counts.error > 0 && <p className="text-xs text-destructive">Lines with errors are skipped.</p>}
              </>
            )}
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => close(false)}>
            Cancel
          </Button>
          <Button
            disabled={!preview || !changes || mc.saving}
            onClick={async () => {
              const r = await mc.run(() => api.replaceBridgeMappings(versionId, preview!.result), `Applied ${changes} change${changes === 1 ? '' : 's'}`)
              if (r.ok) close(false)
            }}
          >
            Apply {changes || ''} change{changes === 1 ? '' : 's'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
