import { ArrowLeftIcon, ClipboardPasteIcon, CopyIcon, PencilIcon, PlusIcon, Trash2Icon, TriangleAlertIcon } from 'lucide-react'
import { useMemo, useState } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'
import { Textarea } from '@/components/ui/textarea'
import { EmptyState, Field, Mono, ProblemBanner, StatusBadge, TabIntro, Td, Th, WithReason } from '@/components/dimensions/shared'
import { SearchCombobox } from '@/components/report/shared/search-combobox'
import { SAVE_FIRST, useHasPendingChanges } from '@/components/shell/changes'
import { useUndoAction } from '@/components/shell/history'
import { useSearchParam, useSetSearchParams } from '@/lib/navigation'
import type { DimensionDetail } from '@/lib/dimensions/contract'
import { MappingSetEditScope, useMappingSet, useMappingSetDeleteCheck, useMappingSets, useMappingsApi, useMappingVersion, usePublishedMembers } from '@/lib/mappings/client'
import { isEditableStatus, liveVersionOf, newTempId, type MappingEntry, type MappingSetDetail, type MappingVersionContent, type MemberRef, type ValidationReport, type Violation } from '@/lib/mappings/contract'
import { entriesToText, indexMembers, previewEntryPaste, sourceKeyMarkers, violationsByTarget, type MemberIndex } from '@/lib/mappings/rules'
import { cn } from '@/lib/utils'
import { BlockedDeleteDialog, memberOptions, PinsList, PREVIEW_TONE, RowIssues, SourceKey, useMapCall, ValidationList, VersionBar, withoutKey } from './shared'

export function MappingSetsSection({ dim }: { dim: DimensionDetail }) {
  const [ms] = useSearchParam('ms')
  const setSearchParams = useSetSearchParams()
  if (ms) return <MappingSetEditor id={ms} onBack={() => setSearchParams({ ms: null, msv: null })} />
  return <MappingSetList dim={dim} />
}

/* ───────────────────────── list ───────────────────────── */

function MappingSetList({ dim }: { dim: DimensionDetail }) {
  const { data: sets } = useMappingSets(dim.id)
  const [creating, setCreating] = useState(false)
  const setSearchParams = useSetSearchParams()
  const open = (id: string) => setSearchParams({ ms: id, msv: null })

  return (
    <div className="flex flex-col gap-4">
      <TabIntro
        action={
          <Button size="sm" onClick={() => setCreating(true)}>
            <PlusIcon /> New mapping set
          </Button>
        }
      >
        Mapping sets translate raw source codes into {dim.displayName} members. Each set covers one source system; datasets pick a set when they bind a field to this dimension.
      </TabIntro>
      {!sets ? (
        <p className="text-sm text-muted-foreground">Loading mapping sets…</p>
      ) : !sets.length ? (
        <EmptyState
          title="No mapping sets yet"
          action={
            <Button size="sm" variant="outline" onClick={() => setCreating(true)}>
              <PlusIcon /> New mapping set
            </Button>
          }
        >
          Add one for each source system whose codes should resolve to {dim.displayName} members.
        </EmptyState>
      ) : (
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-sm">
            <thead className="border-b bg-muted/40">
              <tr>
                <Th>Mapping set</Th>
                <Th>Source namespace</Th>
                <Th>Live</Th>
                <Th>Frozen</Th>
                <Th className="text-right">Used by</Th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {sets.map((s) => {
                const live = liveVersionOf(s.versions)
                const frozenCount = s.versions.length - 1
                return (
                  <tr key={s.id} className="hover:bg-muted/40">
                    <Td>
                      <button type="button" onClick={() => open(s.id)} className="flex flex-col items-start text-left hover:underline">
                        <span className="font-medium">{s.displayName}</span>
                        <Mono>{s.key}</Mono>
                      </button>
                    </Td>
                    <Td>
                      <Mono className="text-foreground">{s.sourceNamespace}</Mono>
                    </Td>
                    <Td className="tabular-nums">
                      v{live.versionNumber} · {s.publishedEntryCount} entries
                    </Td>
                    <Td className="tabular-nums">{frozenCount ? `${frozenCount} version${frozenCount === 1 ? '' : 's'}` : <span className="text-muted-foreground">—</span>}</Td>
                    <Td className="text-right tabular-nums">{s.bindings.length ? `${s.bindings.length} binding${s.bindings.length === 1 ? '' : 's'}` : <span className="text-muted-foreground">Unused</span>}</Td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}
      <CreateMappingSetDialog open={creating} onOpenChange={setCreating} dim={dim} onCreated={open} />
    </div>
  )
}

function CreateMappingSetDialog({ open, onOpenChange, dim, onCreated }: { open: boolean; onOpenChange: (o: boolean) => void; dim: DimensionDetail; onCreated: (id: string) => void }) {
  const api = useMappingsApi()
  const mc = useMapCall()
  const [form, setForm] = useState({ key: '', displayName: '', sourceNamespace: '' })
  const close = (o: boolean) => {
    onOpenChange(o)
    if (!o) {
      setForm({ key: '', displayName: '', sourceNamespace: '' })
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
            const r = await mc.run(() => api.createMappingSet({ ...form, dimensionId: dim.id }), 'Mapping set created')
            if (r.ok) {
              close(false)
              onCreated(r.data.mappingSetId)
            }
          }}
        >
          <DialogHeader>
            <DialogTitle>New mapping set</DialogTitle>
            <DialogDescription>Maps one source system&apos;s codes to {dim.displayName} members. Starts empty and live.</DialogDescription>
          </DialogHeader>
          <Field label="Key" htmlFor="ms-key" required errors={mc.errorsFor('key')} hint="E.g. loan.branch. Can't change later.">
            <Input id="ms-key" className="font-mono" value={form.key} onChange={(e) => setForm({ ...form, key: e.target.value })} autoFocus />
          </Field>
          <Field label="Display name" htmlFor="ms-name" required errors={mc.errorsFor('displayName')}>
            <Input id="ms-name" value={form.displayName} onChange={(e) => setForm({ ...form, displayName: e.target.value })} />
          </Field>
          <Field label="Source namespace" htmlFor="ms-ns" required errors={mc.errorsFor('sourceNamespace')} hint="The system the codes come from, e.g. loan_servicing.">
            <Input id="ms-ns" className="font-mono" value={form.sourceNamespace} onChange={(e) => setForm({ ...form, sourceNamespace: e.target.value })} />
          </Field>
          <ProblemBanner problem={mc.problem && !Object.keys(mc.problem.errors).length ? mc.problem : null} />
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => close(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={mc.saving}>
              {mc.saving ? 'Creating…' : 'Create'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

/* ───────────────────────── editor ───────────────────────── */

function MappingSetEditor({ id, onBack }: { id: string; onBack: () => void }) {
  const { data: set, error } = useMappingSet(id)
  if (error)
    return (
      <EmptyState title="Mapping set not found" action={<Button size="sm" variant="outline" onClick={onBack}>Back to mapping sets</Button>}>
        It may have been deleted.
      </EmptyState>
    )
  if (!set) return <p className="text-sm text-muted-foreground">Loading mapping set…</p>
  return (
    <MappingSetEditScope mappingSetId={set.id} label={set.displayName}>
      <MappingSetPage set={set} onBack={onBack} />
    </MappingSetEditScope>
  )
}

function MappingSetPage({ set, onBack }: { set: MappingSetDetail; onBack: () => void }) {
  const [msv, setMsv] = useSearchParam('msv')
  const selected = set.versions.find((v) => v.id === msv) ?? liveVersionOf(set.versions)
  const { data: content } = useMappingVersion(selected.id)
  const { data: members } = usePublishedMembers(set.dimensionId)
  const [reports, setReports] = useState<Record<string, ValidationReport>>({})
  const [filter, setFilter] = useState('')
  const ready = content && content.version.id === selected.id ? content : null
  const report = reports[selected.id]

  return (
    <div className="flex flex-col gap-5">
      <SetHeader set={set} onBack={onBack} />
      <VersionBar kind="mapping" versions={set.versions} selected={selected} pins={set.pins} onSelect={setMsv} onReport={(r) => setReports((all) => ({ ...all, [r.versionId]: r }))} />
      {report && <ValidationList report={report} onFilter={setFilter} onDismiss={() => setReports((all) => withoutKey(all, selected.id))} />}
      {!ready ? (
        <p className="text-sm text-muted-foreground">Loading entries…</p>
      ) : (
        <div className="grid gap-5 xl:grid-cols-[minmax(0,1fr)_20rem]">
          <EntriesTable set={set} content={ready} members={members} filter={filter} setFilter={setFilter} />
          <aside className="flex flex-col gap-5">
            <Coverage content={ready} members={members} />
            <SetUsage set={set} />
          </aside>
        </div>
      )}
    </div>
  )
}

function SetHeader({ set, onBack }: { set: MappingSetDetail; onBack: () => void }) {
  const api = useMappingsApi()
  const mc = useMapCall()
  const dirty = useHasPendingChanges()
  const [renaming, setRenaming] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const { data: deleteCheck } = useMappingSetDeleteCheck(deleting ? set.id : null)

  return (
    <div className="flex flex-col gap-3">
      <Button variant="ghost" size="sm" className="-ml-2 self-start text-muted-foreground" onClick={onBack}>
        <ArrowLeftIcon /> All mapping sets
      </Button>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex min-w-0 flex-col gap-1">
          <h2 className="text-lg font-semibold text-balance">{set.displayName}</h2>
          <p className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-muted-foreground">
            <Mono>{set.key}</Mono>
            <span>
              from <Mono className="text-foreground">{set.sourceNamespace}</Mono>
            </span>
            <span>
              to {set.dimension.displayName}
              {set.dimension.publishedVersionNumber ? ` v${set.dimension.publishedVersionNumber}` : ''}
            </span>
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Button variant="ghost" size="sm" onClick={() => setRenaming(true)}>
            <PencilIcon /> Edit details
          </Button>
          <WithReason reason={dirty ? SAVE_FIRST : null}>
            <Button variant="ghost" size="sm" className="text-destructive hover:text-destructive" disabled={dirty} onClick={() => setDeleting(true)}>
              <Trash2Icon /> Delete set
            </Button>
          </WithReason>
        </div>
      </div>
      <RenameSetDialog set={set} open={renaming} onOpenChange={setRenaming} />
      <BlockedDeleteDialog
        open={deleting}
        onOpenChange={setDeleting}
        title={`Delete ${set.displayName}?`}
        description="Deleting removes every version and its entries."
        check={deleteCheck}
        onConfirm={async () => {
          const r = await mc.run(() => api.deleteMappingSet(set.id), 'Mapping set deleted')
          if (r.ok) onBack()
          return r.ok
        }}
      />
    </div>
  )
}

function RenameSetDialog({ set, open, onOpenChange }: { set: MappingSetDetail; open: boolean; onOpenChange: (o: boolean) => void }) {
  const api = useMappingsApi()
  const mc = useMapCall()
  const [form, setForm] = useState({ displayName: set.displayName, sourceNamespace: set.sourceNamespace })
  return (
    <Dialog
      open={open}
      onOpenChange={(o) => {
        onOpenChange(o)
        if (o) setForm({ displayName: set.displayName, sourceNamespace: set.sourceNamespace })
      }}
    >
      <DialogContent className="sm:max-w-md">
        <form
          className="flex flex-col gap-4"
          onSubmit={async (e) => {
            e.preventDefault()
            const r = await mc.run(() => api.updateMappingSet(set.id, form))
            if (r.ok) onOpenChange(false)
          }}
        >
          <DialogHeader>
            <DialogTitle>Edit mapping set</DialogTitle>
            <DialogDescription>
              The key <Mono>{set.key}</Mono> and dimension are fixed.
            </DialogDescription>
          </DialogHeader>
          <Field label="Display name" htmlFor="rs-name" required errors={mc.errorsFor('displayName')}>
            <Input id="rs-name" value={form.displayName} onChange={(e) => setForm({ ...form, displayName: e.target.value })} />
          </Field>
          <Field label="Source namespace" htmlFor="rs-ns" required errors={mc.errorsFor('sourceNamespace')}>
            <Input id="rs-ns" className="font-mono" value={form.sourceNamespace} onChange={(e) => setForm({ ...form, sourceNamespace: e.target.value })} />
          </Field>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={mc.saving}>
              Apply
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

/* ───────────────────────── entries ───────────────────────── */

function EntriesTable({
  set,
  content,
  members,
  filter,
  setFilter,
}: {
  set: MappingSetDetail
  content: MappingVersionContent
  members: MemberRef[] | null | undefined
  filter: string
  setFilter: (v: string) => void
}) {
  const api = useMappingsApi()
  const mc = useMapCall()
  const undoAction = useUndoAction()
  const { version, entries } = content
  const index = useMemo(() => indexMembers(members), [members])
  const issues = useMemo(() => violationsByTarget(content.violations).byId, [content.violations])
  const options = useMemo(() => memberOptions(members, { assignableOnly: true }), [members])
  const [issuesOnly, setIssuesOnly] = useState(false)
  const [draft, setDraft] = useState({ sourceKeyValue: '', memberKey: '' })
  const [editing, setEditing] = useState<{ id: string; sourceKeyValue: string; memberKey: string } | null>(null)
  const [pasting, setPasting] = useState(false)
  const editable = isEditableStatus(version.status)

  const q = filter.trim().toLowerCase()
  const rows = [...entries]
    .filter((e) => !issuesOnly || issues.has(e.id))
    .filter((e) => !q || e.sourceKeyValue.toLowerCase().includes(q) || e.memberKey.toLowerCase().includes(q) || index?.get(e.memberKey)?.displayName.toLowerCase().includes(q))
    .sort((a, b) => a.sourceKeyValue.localeCompare(b.sourceKeyValue, undefined, { numeric: true }))

  const add = async () => {
    const r = await mc.run(() => api.saveEntry(version.id, { id: newTempId(), ...draft }))
    if (r.ok) setDraft({ sourceKeyValue: '', memberKey: draft.memberKey })
  }

  const remove = async (e: MappingEntry) => {
    const r = await mc.run(() => api.deleteEntry(version.id, e.id))
    if (r.ok) toast.success(`Removed '${e.sourceKeyValue}'`, { action: undoAction() })
  }

  return (
    <section aria-label="Entries" className="flex min-w-0 flex-col gap-3">
      <div className="flex flex-wrap items-center gap-2">
        <Input type="search" placeholder="Search source keys or members" aria-label="Search entries" className="h-8 w-64" value={filter} onChange={(e) => setFilter(e.target.value)} />
        <label className="flex items-center gap-2 text-sm text-muted-foreground">
          <Switch checked={issuesOnly} onCheckedChange={setIssuesOnly} />
          Issues only{issues.size > 0 && <span className="rounded-sm bg-destructive/15 px-1.5 font-mono text-[11px] text-destructive tabular-nums">{issues.size}</span>}
        </label>
        <span className="flex-1" />
        <Button
          size="sm"
          variant="ghost"
          onClick={async () => {
            await navigator.clipboard.writeText(entriesToText(entries))
            toast.success(`Copied ${entries.length} entries`)
          }}
        >
          <CopyIcon /> Copy
        </Button>
        <WithReason reason={editable ? null : `v${version.versionNumber} is frozen and read-only`}>
          <Button size="sm" variant="outline" disabled={!editable} onClick={() => setPasting(true)}>
            <ClipboardPasteIcon /> Paste entries
          </Button>
        </WithReason>
      </div>

      <p className="text-sm text-muted-foreground">
        {editable ? 'Live version. Edits stay pending until you save; saving is refused while any entry is flagged.' : `v${version.versionNumber} is a frozen snapshot and read-only.`}
      </p>

      <div className="overflow-x-auto rounded-lg border bg-card">
        <table className="w-full text-sm">
          <thead className="border-b bg-muted/40">
            <tr>
              <Th className="w-[30%]">Source key</Th>
              <Th>{set.dimension.displayName} member</Th>
              <Th className="w-24">
                <span className="sr-only">Actions</span>
              </Th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {editable && (
              <tr className="bg-muted/20">
                <Td className="align-top">
                  <Input
                    aria-label="New source key"
                    placeholder="Source key, as sent"
                    className="h-8 font-mono"
                    value={draft.sourceKeyValue}
                    aria-invalid={!!mc.errorsFor('sourceKeyValue') && !editing}
                    onChange={(e) => setDraft({ ...draft, sourceKeyValue: e.target.value })}
                  />
                  {!editing && mc.errorsFor('sourceKeyValue')?.map((m) => <p key={m} className="mt-1 text-xs text-destructive">{m}</p>)}
                </Td>
                <Td className="align-top">
                  <SearchCombobox aria-label="New entry member" className="h-8 w-full max-w-sm" value={draft.memberKey} onChange={(memberKey) => setDraft({ ...draft, memberKey })} options={options} limit={200} placeholder="Choose member…" />
                  {!editing && mc.errorsFor('memberKey')?.map((m) => <p key={m} className="mt-1 text-xs text-destructive">{m}</p>)}
                </Td>
                <Td className="text-right align-top">
                  <Button size="sm" disabled={mc.saving || !draft.memberKey} onClick={add}>
                    Add
                  </Button>
                </Td>
              </tr>
            )}
            {rows.map((e) =>
              editing?.id === e.id ? (
                <tr key={e.id} className="bg-accent/40">
                  <Td className="align-top">
                    <Input aria-label="Source key" className="h-8 font-mono" value={editing.sourceKeyValue} onChange={(ev) => setEditing({ ...editing, sourceKeyValue: ev.target.value })} autoFocus />
                    {mc.errorsFor('sourceKeyValue')?.map((m) => <p key={m} className="mt-1 text-xs text-destructive">{m}</p>)}
                  </Td>
                  <Td className="align-top">
                    <SearchCombobox aria-label="Member" className="h-8 w-full max-w-sm" value={editing.memberKey} onChange={(memberKey) => setEditing({ ...editing, memberKey })} options={options} limit={200} />
                    {mc.errorsFor('memberKey')?.map((m) => <p key={m} className="mt-1 text-xs text-destructive">{m}</p>)}
                  </Td>
                  <Td className="text-right align-top whitespace-nowrap">
                    <Button size="sm" variant="ghost" onClick={() => { setEditing(null); mc.setProblem(null) }}>
                      Cancel
                    </Button>
                    <Button
                      size="sm"
                      disabled={mc.saving}
                      onClick={async () => {
                        const r = await mc.run(() => api.saveEntry(version.id, editing))
                        if (r.ok) setEditing(null)
                      }}
                    >
                      Apply
                    </Button>
                  </Td>
                </tr>
              ) : (
                <EntryRow
                  key={e.id}
                  entry={e}
                  index={index}
                  issues={issues.get(e.id)}
                  editable={editable}
                  onEdit={() => {
                    mc.setProblem(null)
                    setEditing({ id: e.id, sourceKeyValue: e.sourceKeyValue, memberKey: e.memberKey })
                  }}
                  onDelete={() => remove(e)}
                />
              ),
            )}
            {!rows.length && (
              <tr>
                <Td colSpan={3} className="py-8 text-center text-muted-foreground">
                  {entries.length ? 'No entries match.' : 'No entries yet. Add one above or paste a list.'}
                </Td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
      <p className="text-xs text-muted-foreground tabular-nums">
        {rows.length === entries.length ? `${entries.length} entries` : `${rows.length} of ${entries.length} entries`} · source keys match exactly, including spaces and leading zeros
      </p>
      <PasteEntriesDialog open={pasting} onOpenChange={setPasting} versionId={version.id} entries={entries} />
    </section>
  )
}

function EntryRow({ entry, index, issues, editable, onEdit, onDelete }: { entry: MappingEntry; index: MemberIndex | null; issues?: Violation[]; editable: boolean; onEdit: () => void; onDelete: () => void }) {
  const m = index?.get(entry.memberKey)
  return (
    <tr className={cn('group', issues && 'bg-destructive/5')}>
      <Td>
        <SourceKey value={entry.sourceKeyValue} markers={sourceKeyMarkers(entry.sourceKeyValue)} />
      </Td>
      <Td>
        <div className="flex flex-col">
          <span className="flex items-center gap-2">
            {m ? m.displayName : <span className="text-muted-foreground">Unknown member</span>}
            <Mono>{entry.memberKey}</Mono>
          </span>
          {issues && <RowIssues messages={issues.map((i) => i.message)} />}
        </div>
      </Td>
      <Td className="text-right whitespace-nowrap">
        {editable && (
          <span className="inline-flex opacity-0 transition-opacity group-focus-within:opacity-100 group-hover:opacity-100">
            <Button size="icon-sm" variant="ghost" aria-label={`Edit ${entry.sourceKeyValue}`} onClick={onEdit}>
              <PencilIcon />
            </Button>
            <Button size="icon-sm" variant="ghost" aria-label={`Delete ${entry.sourceKeyValue}`} onClick={onDelete}>
              <Trash2Icon />
            </Button>
          </span>
        )}
      </Td>
    </tr>
  )
}

function PasteEntriesDialog({ open, onOpenChange, versionId, entries }: { open: boolean; onOpenChange: (o: boolean) => void; versionId: string; entries: MappingEntry[] }) {
  const api = useMappingsApi()
  const mc = useMapCall()
  const [text, setText] = useState('')
  const [keep, setKeep] = useState(true)
  const preview = useMemo(() => (text.trim() ? previewEntryPaste(entries, text, keep) : null), [entries, text, keep])
  const changes = preview ? preview.counts.new + preview.counts.changed + preview.counts.removed : 0
  const close = (o: boolean) => {
    onOpenChange(o)
    if (!o) setText('')
  }

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent className="sm:max-w-3xl">
        <DialogHeader>
          <DialogTitle>Paste entries</DialogTitle>
          <DialogDescription>One per line: source key, then member key, separated by a tab or comma. Keys are taken exactly as pasted; entries that break a rule are flagged after applying.</DialogDescription>
        </DialogHeader>
        <div className="grid gap-4 md:grid-cols-2">
          <div className="flex flex-col gap-3">
            <Textarea aria-label="Entries to paste" className="h-64 font-mono text-xs" placeholder={'001\tBR01\n002\tBR02'} value={text} onChange={(e) => setText(e.target.value)} />
            <label className="flex items-center gap-2 text-sm">
              <Switch checked={keep} onCheckedChange={setKeep} />
              Keep entries that aren&apos;t in the paste
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
                        <code className="font-mono whitespace-pre">{r.sourceKeyValue.replace(/ /g, '·')}</code>
                        <span className="text-muted-foreground">→</span>
                        <span className="font-mono">
                          {r.previousMemberKey && <s className="mr-1 text-muted-foreground">{r.previousMemberKey}</s>}
                          {r.memberKey}
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
              const r = await mc.run(() => api.replaceEntries(versionId, preview!.result), `Applied ${changes} change${changes === 1 ? '' : 's'}`)
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

/* ───────────────────────── side panels ───────────────────────── */

function Coverage({ content, members }: { content: MappingVersionContent; members: MemberRef[] | null | undefined }) {
  const [all, setAll] = useState(false)
  const { assignableMemberCount: assignable, uncoveredMemberKeys: uncovered } = content.coverage
  const index = useMemo(() => indexMembers(members), [members])
  const shown = all ? uncovered : uncovered.slice(0, 10)
  return (
    <section aria-labelledby="cov-h" className="flex flex-col gap-2 rounded-lg border bg-card p-3">
      <h3 id="cov-h" className="text-sm font-medium">
        Coverage
      </h3>
      <p className="text-sm tabular-nums">
        {assignable - uncovered.length} of {assignable} assignable members receive at least one source key.
      </p>
      <div className="h-1.5 overflow-hidden rounded-full bg-muted" aria-hidden>
        <div className="h-full bg-primary" style={{ width: `${assignable ? ((assignable - uncovered.length) / assignable) * 100 : 0}%` }} />
      </div>
      {uncovered.length > 0 && (
        <>
          <p className="flex items-center gap-1.5 text-xs text-muted-foreground">
            <TriangleAlertIcon aria-hidden className="size-3 shrink-0" />
            No source key maps to these yet. That&apos;s allowed; their rows simply stay empty.
          </p>
          <ul className="flex flex-wrap gap-1">
            {shown.map((key) => (
              <li key={key} title={index?.get(key)?.displayName} className="rounded-sm border px-1.5 font-mono text-[11px]">
                {key}
              </li>
            ))}
          </ul>
          {uncovered.length > 10 && (
            <Button variant="link" size="sm" className="h-auto self-start p-0" onClick={() => setAll(!all)}>
              {all ? 'Show fewer' : `Show all ${uncovered.length}`}
            </Button>
          )}
        </>
      )}
    </section>
  )
}

function SetUsage({ set }: { set: MappingSetDetail }) {
  return (
    <section aria-labelledby="use-h" className="flex flex-col gap-3 rounded-lg border bg-card p-3">
      <h3 id="use-h" className="text-sm font-medium">
        Used by
      </h3>
      {set.bindings.length ? (
        <ul className="flex flex-col gap-2 text-sm">
          {set.bindings.map((b) => (
            <li key={`${b.datasetVersionId}-${b.fieldKey}`} className="flex flex-col">
              <span className="flex items-center gap-2">
                <Mono className="text-foreground">{b.datasetKey}</Mono>
                <StatusBadge status={b.status} label={`v${b.datasetVersionNumber}`} />
              </span>
              <span className="text-xs text-muted-foreground">
                <Mono>{b.fieldKey}</Mono> as role <Mono>{b.roleKey}</Mono>
              </span>
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-sm text-muted-foreground">No dataset binds through this set yet.</p>
      )}
      <PinsList pins={set.pins} />
    </section>
  )
}
