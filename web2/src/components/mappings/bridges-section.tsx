import { ArrowLeftIcon, ArrowRightIcon, ChevronDownIcon, ClipboardPasteIcon, CopyIcon, FileTextIcon, PencilIcon, PlusIcon, Trash2Icon, TriangleAlertIcon } from 'lucide-react'
import { useMemo, useState } from 'react'
import { toast } from 'sonner'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'
import { Textarea } from '@/components/ui/textarea'
import { ConfirmDialog, EmptyState, Field, Mono, ProblemBanner, StatusBadge, TabIntro, Td, Th, WithReason } from '@/components/dimensions/shared'
import { SearchCombobox } from '@/components/report/shared/search-combobox'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { useHistory, useUndoAction } from '@/components/shell/history'
import { useAppNav, useSearchParam, useSetSearchParams } from '@/lib/navigation'
import type { DimensionDetail } from '@/lib/dimensions/contract'
import { useBridge, useBridgeDeleteCheck, useBridges, useBridgeVersion, useDimensionRefs, useMappingsApi, usePublishedMembers } from '@/lib/mappings/client'
import {
  ALLOCATION_HELP,
  ALLOCATION_LABEL,
  allowedModes,
  CARDINALITY_HELP,
  CARDINALITY_LABEL,
  canHaveBridges,
  defaultVersionOf,
  isEditableStatus,
  type AllocationMode,
  type BridgeDetail,
  type BridgeListItem,
  type BridgeMapping,
  type BridgeVersion,
  type BridgeVersionSummary,
  type Cardinality,
  type SourceSelection,
  type ValidationReport,
} from '@/lib/mappings/contract'
import { BRIDGE_ISSUE_TEXT, bridgeIssues, bridgeMappingsToText, fmt, indexMembers, nonCompliantCount, previewBridgePaste, summarizeSources, sumsToOne, type MemberIndex } from '@/lib/mappings/rules'
import { useEditor } from '@/lib/report/store'
import { ReportLink } from '@/components/dimensions/hidden-dimensions'
import { cn } from '@/lib/utils'
import { PinsList } from './mapping-sets-section'
import { BlockedDeleteDialog, memberOptions, useMapCall, ValidationList, VersionPills } from './shared'

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
                <Th>Draft</Th>
                <Th className="text-right">Used by</Th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {bridges.map((b) => {
                const live = b.versions.find((v) => v.status === 'published')
                const draft = b.versions.find((v) => v.status === 'draft' || v.status === 'validated')
                const shown = live ?? draft ?? b.versions[0]
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
                    <Td>
                      {b.owner ? (
                        <ReportLink owner={{ reportKey: b.owner.reportKey, reportName: b.target.displayName }} />
                      ) : (
                        (other === 'target' ? b.target : b.source).displayName
                      )}
                    </Td>
                    <Td className="whitespace-nowrap">{shown ? `${CARDINALITY_LABEL[shown.cardinality]} · ${ALLOCATION_LABEL[shown.allocationMode]}` : '—'}</Td>
                    <Td className="tabular-nums">{live ? `v${live.versionNumber} · ${live.mappingCount} rows` : <span className="text-muted-foreground">Not published</span>}</Td>
                    <Td>{draft ? <StatusBadge status={draft.status} label={`v${draft.versionNumber} ${draft.status}`} /> : <span className="text-muted-foreground">—</span>}</Td>
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
            const r = await mc.run(() => api.createBridge({ key: form.key, displayName: form.displayName, sourceDimensionId, targetDimensionId, isEnabled: form.isEnabled }), 'Bridge created with draft v1')
            if (r.ok) {
              close(false)
              onCreated(r.data.bridgeId)
            }
          }}
        >
          <DialogHeader>
            <DialogTitle>New bridge</DialogTitle>
            <DialogDescription>Starts as an empty many-to-one draft. Settings can change while it&apos;s a draft.</DialogDescription>
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
          <Field label="Key" htmlFor="br-key" required errors={mc.errorsFor('key')} hint="Lowercase, e.g. branch_to_region. Can't change later.">
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
  const [brv, setBrv] = useSearchParam('brv')
  const selected = bridge ? (bridge.versions.find((v) => v.id === brv) ?? defaultVersionOf(bridge.versions)) : undefined
  const { data: content } = useBridgeVersion(selected?.id ?? null)
  const { data: srcMembers } = usePublishedMembers(bridge?.sourceDimensionId ?? null)
  const { data: tgtMembers } = usePublishedMembers(bridge?.targetDimensionId ?? null)
  const [reports, setReports] = useState<Record<string, ValidationReport>>({})
  const [filter, setFilter] = useState('')

  if (error)
    return (
      <EmptyState title="Bridge not found" action={<Button size="sm" variant="outline" onClick={onBack}>Back to bridges</Button>}>
        It may have been deleted.
      </EmptyState>
    )
  if (!bridge || !selected) return <p className="text-sm text-muted-foreground">Loading bridge…</p>
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

  const ready = content && content.version.id === selected.id ? content : null
  const report = reports[selected.id]
  const readOnlyReport = !!bridge.owner

  return (
    <div className="flex flex-col gap-5">
      <BridgeHeader bridge={bridge} dimId={dimId} selected={selected} onBack={onBack} onSelect={setBrv} onReport={(r) => setReports((all) => ({ ...all, [r.versionId]: r }))} />
      {report && <ValidationList report={report} onFilter={setFilter} onDismiss={() => setReports(({ [selected.id]: _, ...rest }) => rest)} />}
      {!ready ? (
        <p className="text-sm text-muted-foreground">Loading mappings…</p>
      ) : (
        <div className="grid gap-5 xl:grid-cols-[minmax(0,1fr)_20rem]">
          <MappingsTable bridge={bridge} version={ready.version} mappings={ready.mappings} srcMembers={srcMembers} tgtMembers={tgtMembers} filter={filter} setFilter={setFilter} locked={readOnlyReport} />
          <aside className="flex flex-col gap-5">
            <SettingsCard version={ready.version} mappings={ready.mappings} locked={readOnlyReport} />
            <BridgeUsage bridge={bridge} />
          </aside>
        </div>
      )}
    </div>
  )
}

function BridgeHeader({
  bridge,
  dimId,
  selected,
  onBack,
  onSelect,
  onReport,
}: {
  bridge: BridgeDetail
  dimId: string
  selected: BridgeVersionSummary
  onBack: () => void
  onSelect: (id: string) => void
  onReport: (r: ValidationReport) => void
}) {
  const api = useMappingsApi()
  const mc = useMapCall()
  const [publishing, setPublishing] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const { data: deleteCheck } = useBridgeDeleteCheck(deleting ? bridge.id : null)
  const openDraft = bridge.versions.find((v) => v.status === 'draft' || v.status === 'validated')
  const live = bridge.versions.find((v) => v.status === 'published')
  const isOpen = selected.status === 'draft' || selected.status === 'validated'
  const owned = !!bridge.owner

  const check = async (call: () => ReturnType<typeof api.validateBridgeVersion>, okText: string) => {
    const r = await mc.run(call)
    if (r.ok) {
      onReport(r.data)
      if (!r.data.violations.length) toast.success(okText)
    }
    return r
  }
  const newDraft = async (cloneFromVersionId: string | null) => {
    const r = await mc.run(
      () => api.createBridgeVersion(bridge.id, { cardinality: selected.cardinality, allocationMode: selected.allocationMode, cloneFromVersionId }),
      (v) => `Draft v${v.versionNumber} created`,
    )
    if (r.ok) onSelect(r.data.id)
  }

  return (
    <div className="flex flex-col gap-3">
      <Button variant="ghost" size="sm" className="-ml-2 self-start text-muted-foreground" onClick={onBack}>
        <ArrowLeftIcon /> All bridges
      </Button>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex min-w-0 flex-col gap-1">
          <h2 className="flex items-center gap-2 text-lg font-semibold text-balance">
            {bridge.displayName}
            {owned && (
              <Badge variant="outline" className="gap-1 font-normal">
                <FileTextIcon aria-hidden className="size-3" /> Report bridge
              </Badge>
            )}
          </h2>
          <p className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-muted-foreground">
            <Mono>{bridge.key}</Mono>
            <span className="flex items-center gap-1.5">
              <span className={cn(bridge.sourceDimensionId === dimId && 'text-foreground')}>{bridge.source.displayName}</span>
              <ArrowRightIcon aria-label="to" className="size-3.5" />
              <span className={cn(bridge.targetDimensionId === dimId && 'text-foreground')}>{bridge.target.displayName}</span>
            </span>
          </p>
        </div>
        {!owned && (
          <div className="flex flex-wrap items-center gap-3">
            <label className="flex items-center gap-2 text-sm">
              <Switch checked={bridge.isEnabled} disabled={mc.saving} onCheckedChange={(isEnabled) => mc.run(() => api.updateBridge(bridge.id, { isEnabled }), isEnabled ? 'Bridge enabled' : 'Bridge disabled')} />
              Enabled
            </label>
            <Button variant="ghost" size="sm" className="text-destructive hover:text-destructive" onClick={() => setDeleting(true)}>
              <Trash2Icon /> Delete bridge
            </Button>
          </div>
        )}
      </div>
      {owned && (
        <p className="flex items-center gap-2 rounded-md border border-primary/40 bg-primary/10 px-3 py-2 text-sm">
          <FileTextIcon aria-hidden className="size-4 shrink-0 text-primary" />
          Owned by report <Mono className="text-foreground">{bridge.owner!.reportKey}</Mono> (source <Mono className="text-foreground">{bridge.owner!.sourceKey}</Mono>). Edit its row mappings on that report&apos;s Rows page.
        </p>
      )}
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border bg-card p-2.5">
        <VersionPills versions={bridge.versions} selectedId={selected.id} pins={bridge.pins} onSelect={onSelect} />
        {!owned && (
          <div className="flex flex-wrap items-center gap-2">
            {selected.status === 'draft' && (
              <Button size="sm" variant="outline" disabled={mc.saving} onClick={() => check(() => api.validateBridgeVersion(selected.id), `v${selected.versionNumber} validated`)}>
                Validate
              </Button>
            )}
            {selected.status === 'validated' && (
              <Button size="sm" variant="outline" disabled={mc.saving} onClick={() => mc.run(() => api.reopenBridgeVersion(selected.id), 'Returned to draft')}>
                Return to draft
              </Button>
            )}
            {isOpen && (
              <Button
                size="sm"
                variant="ghost"
                className="text-destructive hover:text-destructive"
                disabled={mc.saving}
                onClick={async () => {
                  const r = await mc.run(() => api.deleteBridgeVersion(selected.id), `Draft v${selected.versionNumber} deleted`)
                  if (r.ok) onSelect('')
                }}
              >
                Delete draft
              </Button>
            )}
            {isOpen && (
              <Button size="sm" disabled={mc.saving} onClick={() => setPublishing(true)}>
                Publish v{selected.versionNumber}
              </Button>
            )}
            {!openDraft && (
              <DropdownMenu>
                <DropdownMenuTrigger render={<Button size="sm" variant="outline" />}>
                  New draft <ChevronDownIcon />
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuItem onClick={() => newDraft(selected.id)}>Copy of v{selected.versionNumber}</DropdownMenuItem>
                  <DropdownMenuItem onClick={() => newDraft(null)}>Empty draft</DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            )}
          </div>
        )}
      </div>
      <ConfirmDialog
        open={publishing}
        onOpenChange={setPublishing}
        title={`Publish v${selected.versionNumber}?`}
        confirmLabel="Publish"
        onConfirm={async () => {
          const r = await check(() => api.publishBridgeVersion(selected.id), `v${selected.versionNumber} is live`)
          setPublishing(false)
          return r
        }}
      >
        <ul className="flex list-disc flex-col gap-1 pl-4">
          <li>Publication checks run first, including the {ALLOCATION_LABEL[selected.allocationMode].toLowerCase()} allocation rules.</li>
          {live && <li>v{live.versionNumber} will be retired.</li>}
          <li>The active assumption set will point at v{selected.versionNumber}.</li>
          {bridge.bindings.length > 0 && <li>{bridge.bindings.length} dataset binding{bridge.bindings.length === 1 ? '' : 's'} remap through it on their next run.</li>}
        </ul>
      </ConfirmDialog>
      <BlockedDeleteDialog
        open={deleting}
        onOpenChange={setDeleting}
        title={`Delete ${bridge.displayName}?`}
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

function SettingsCard({ version, mappings, locked }: { version: BridgeVersion; mappings: BridgeMapping[]; locked: boolean }) {
  const api = useMappingsApi()
  const mc = useMapCall()
  const editable = !locked && version.status === 'draft'
  const [pending, setPending] = useState<{ cardinality: Cardinality; allocationMode: AllocationMode } | null>(null)
  const apply = (next: { cardinality: Cardinality; allocationMode: AllocationMode }) => {
    if (!allowedModes(next.cardinality).includes(next.allocationMode)) next = { ...next, allocationMode: allowedModes(next.cardinality)[0] }
    const breaking = nonCompliantCount(next.cardinality, next.allocationMode, mappings)
    if (breaking > 0) setPending(next)
    else mc.run(() => api.updateBridgeVersion(version.id, next), 'Settings saved')
  }
  const pendingCount = pending ? nonCompliantCount(pending.cardinality, pending.allocationMode, mappings) : 0

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
              onChange={(v) => apply({ cardinality: v as Cardinality, allocationMode: version.allocationMode })}
              options={(Object.keys(CARDINALITY_LABEL) as Cardinality[]).map((c) => ({ value: c, label: CARDINALITY_LABEL[c] }))}
            />
          </Field>
          <Field label="Allocation" hint={ALLOCATION_HELP[version.allocationMode]}>
            <SimpleSelect
              aria-label="Allocation"
              size="sm"
              value={version.allocationMode}
              onChange={(v) => apply({ cardinality: version.cardinality, allocationMode: v as AllocationMode })}
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
          {!locked && <p className="text-xs text-muted-foreground">Settings change only on a draft. Start a new draft to change them.</p>}
        </div>
      )}
      <ConfirmDialog
        open={!!pending}
        onOpenChange={(o) => !o && setPending(null)}
        title="Change settings?"
        confirmLabel="Change anyway"
        onConfirm={async () => {
          await mc.run(() => api.updateBridgeVersion(version.id, pending!), 'Settings saved')
          setPending(null)
        }}
      >
        {pendingCount} mapping{pendingCount === 1 ? '' : 's'} won&apos;t satisfy {pending && `${CARDINALITY_LABEL[pending.cardinality].toLowerCase()} · ${ALLOCATION_LABEL[pending.allocationMode].toLowerCase()}`}. They stay in the draft and are flagged; the version can&apos;t be published until they&apos;re fixed.
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
      {bridge.owner ? (
        <p className="text-sm">
          Report <Mono className="text-foreground">{bridge.owner.reportKey}</Mono> uses it to place source rows on its row axis.
        </p>
      ) : bridge.bindings.length ? (
        <ul className="flex flex-col gap-2 text-sm">
          {bridge.bindings.map((b) => (
            <li key={`${b.datasetKey}-${b.fieldKey}-${b.roleKey}`} className="flex flex-col">
              <span className="flex items-center gap-2">
                <Mono className="text-foreground">{b.datasetKey}</Mono>
                <StatusBadge status={b.status} label={`v${b.datasetVersion}`} />
              </span>
              <span className="text-xs text-muted-foreground">
                <Mono>{b.fieldKey}</Mono>: role <Mono>{b.sourceRoleKey ?? '?'}</Mono> → <Mono>{b.roleKey}</Mono>
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

type RowDraft = { id?: string; sourceMemberKey: string; targetMemberKey: string; factor: string; allocationWeight: string; sourceSelection: SourceSelection }
const blankRow = (): RowDraft => ({ sourceMemberKey: '', targetMemberKey: '', factor: '1', allocationWeight: '1', sourceSelection: 'member' })

function MappingsTable({
  bridge,
  version,
  mappings,
  srcMembers,
  tgtMembers,
  filter,
  setFilter,
  locked,
}: {
  bridge: BridgeDetail
  version: BridgeVersion
  mappings: BridgeMapping[]
  srcMembers: ReturnType<typeof usePublishedMembers>['data']
  tgtMembers: ReturnType<typeof usePublishedMembers>['data']
  filter: string
  setFilter: (v: string) => void
  locked: boolean
}) {
  const api = useMappingsApi()
  const mc = useMapCall()
  const history = useHistory()
  const undoAction = useUndoAction()
  const srcIdx = useMemo(() => indexMembers(srcMembers), [srcMembers])
  const tgtIdx = useMemo(() => indexMembers(tgtMembers), [tgtMembers])
  const srcOptions = useMemo(() => memberOptions(srcMembers), [srcMembers])
  const tgtOptions = useMemo(() => memberOptions(tgtMembers, { assignableOnly: true }), [tgtMembers])
  const issues = useMemo(() => bridgeIssues(version.cardinality, version.allocationMode, mappings, srcIdx, tgtIdx), [version, mappings, srcIdx, tgtIdx])
  const sums = useMemo(() => summarizeSources(mappings), [mappings])
  const [issuesOnly, setIssuesOnly] = useState(false)
  const [draft, setDraft] = useState<RowDraft>(blankRow)
  const [editing, setEditing] = useState<RowDraft | null>(null)
  const [pasting, setPasting] = useState(false)
  const editable = !locked && isEditableStatus(version.status)
  const showSums = version.allocationMode === 'partition'

  const q = filter.trim().toLowerCase()
  const label = (idx: MemberIndex | null, key: string) => idx?.get(key)?.displayName ?? ''
  const rows = [...mappings]
    .filter((m) => !issuesOnly || issues.has(m.id))
    .filter((m) => !q || [m.sourceMemberKey, m.targetMemberKey, label(srcIdx, m.sourceMemberKey), label(tgtIdx, m.targetMemberKey)].some((s) => s.toLowerCase().includes(q)))
    .sort((a, b) => a.sourceMemberKey.localeCompare(b.sourceMemberKey, undefined, { numeric: true }) || a.targetMemberKey.localeCompare(b.targetMemberKey))

  const toInput = (r: RowDraft) => ({ id: r.id, sourceMemberKey: r.sourceMemberKey, targetMemberKey: r.targetMemberKey, factor: Number(r.factor), allocationWeight: Number(r.allocationWeight), sourceSelection: r.sourceSelection })

  const remove = async (m: BridgeMapping) => {
    const r = await mc.run(() => api.deleteBridgeMapping(version.id, m.id))
    if (!r.ok) return
    let mappingId = m.id
    const versionId = version.id
    history.record(
      `bridge:${versionId}`,
      `Remove ${m.sourceMemberKey} → ${m.targetMemberKey}`,
      async () => {
        const res = await mc.run(() => api.saveBridgeMapping(versionId, { ...m, id: undefined }))
        if (!res.ok) throw new Error('The mapping could not be restored.')
        mappingId = res.data.id
      },
      async () => {
        const res = await mc.run(() => api.deleteBridgeMapping(versionId, mappingId))
        if (!res.ok) throw new Error('The mapping could not be removed.')
      },
    )
    toast.success(`Removed ${m.sourceMemberKey} → ${m.targetMemberKey}`, { action: undoAction() })
  }

  const rowEditor = (r: RowDraft, set: (r: RowDraft) => void, idPrefix: string) => (
    <>
      <Td className="align-top">
        <div className="flex flex-col gap-1">
          <SearchCombobox aria-label={`${idPrefix} source member`} className="h-8 w-full max-w-56" value={r.sourceMemberKey} onChange={(sourceMemberKey) => set({ ...r, sourceMemberKey })} options={srcOptions} limit={200} placeholder="Source…" />
          {srcIdx?.get(r.sourceMemberKey)?.isAssignable === false && (
            <label className="flex items-center gap-1.5 text-xs text-muted-foreground">
              <Switch size="sm" checked={r.sourceSelection === 'descendants'} onCheckedChange={(on) => set({ ...r, sourceSelection: on ? 'descendants' : 'member' })} />
              Include descendants
            </label>
          )}
        </div>
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
          Issues only{issues.size > 0 && <span className="rounded-sm bg-destructive/15 px-1.5 font-mono text-[11px] text-destructive tabular-nums">{issues.size}</span>}
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
        {!locked && (
          <WithReason reason={editable ? null : `v${version.versionNumber} is ${version.status} and read-only`}>
            <Button size="sm" variant="outline" disabled={!editable} onClick={() => setPasting(true)}>
              <ClipboardPasteIcon /> Paste rows
            </Button>
          </WithReason>
        )}
      </div>
      {!locked && version.status === 'published' && <p className="text-sm text-muted-foreground">Live version. Edits apply immediately and must keep the version valid.</p>}
      {!locked && !isEditableStatus(version.status) && <p className="text-sm text-muted-foreground">v{version.versionNumber} is {version.status} and read-only.</p>}
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
              const sum = sums.get(m.sourceMemberKey)
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
                          const r = await mc.run(() => api.saveBridgeMapping(version.id, toInput(editing)), 'Row saved')
                          if (r.ok) setEditing(null)
                        }}
                      >
                        Save
                      </Button>
                    </Td>
                  </tr>
                )
              const rowIssues = issues.get(m.id)
              return (
                <tr key={m.id} className={cn('group', rowIssues && 'bg-destructive/5', !firstOfSource && 'border-t-transparent')}>
                  <Td>
                    {firstOfSource ? (
                      <div className="flex flex-col">
                        <span className="flex items-center gap-2">
                          {label(srcIdx, m.sourceMemberKey) || <span className="text-muted-foreground">Unknown</span>}
                          <Mono>{m.sourceMemberKey}</Mono>
                          {m.sourceSelection === 'descendants' && <Badge variant="outline" className="font-normal">+ descendants</Badge>}
                        </span>
                        {showSums && sum && sum.targets.length > 0 && (
                          <span className={cn('font-mono text-[11px] tabular-nums', sumsToOne(sum.weightSum) ? 'text-muted-foreground' : 'text-destructive')}>Σ weight {fmt(sum.weightSum)}</span>
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
                      {rowIssues?.map((iss) => (
                        <span key={iss.code} className="flex items-center gap-1 text-xs text-destructive">
                          <TriangleAlertIcon aria-hidden className="size-3" />
                          {iss.code === 'badSum' ? `${BRIDGE_ISSUE_TEXT.badSum} (Σ ${fmt(iss.sum)})` : BRIDGE_ISSUE_TEXT[iss.code]}
                        </span>
                      ))}
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
                            setEditing({ id: m.id, sourceMemberKey: m.sourceMemberKey, targetMemberKey: m.targetMemberKey, factor: String(m.factor), allocationWeight: String(m.allocationWeight), sourceSelection: m.sourceSelection })
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
                  {mappings.length ? 'No rows match.' : locked ? 'No rows yet.' : 'No rows yet. Add one above or paste a list.'}
                </Td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
      <p className="text-xs text-muted-foreground tabular-nums">
        {rows.length === mappings.length ? `${mappings.length} rows` : `${rows.length} of ${mappings.length} rows`} · {sums.size} source member{sums.size === 1 ? '' : 's'} · value × factor × weight lands in the target
      </p>
      {editable && <PasteBridgeDialog open={pasting} onOpenChange={setPasting} versionId={version.id} mappings={mappings} srcIdx={srcIdx} tgtIdx={tgtIdx} />}
    </section>
  )
}

const PREVIEW_TONE = {
  new: 'text-success-foreground',
  changed: 'text-primary',
  removed: 'text-destructive',
  unchanged: 'text-muted-foreground',
  error: 'text-destructive',
} as const

function PasteBridgeDialog({ open, onOpenChange, versionId, mappings, srcIdx, tgtIdx }: { open: boolean; onOpenChange: (o: boolean) => void; versionId: string; mappings: BridgeMapping[]; srcIdx: MemberIndex | null; tgtIdx: MemberIndex | null }) {
  const api = useMappingsApi()
  const mc = useMapCall()
  const [text, setText] = useState('')
  const [keep, setKeep] = useState(true)
  const preview = useMemo(() => (text.trim() ? previewBridgePaste(mappings, text, keep, srcIdx, tgtIdx) : null), [mappings, text, keep, srcIdx, tgtIdx])
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
          <DialogDescription>One per line: source key, target key, then optional factor and weight (both default to 1). Tab or comma separated.</DialogDescription>
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
              <p className="text-sm text-muted-foreground">The preview shows what changes before anything is saved.</p>
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
