import { ArchiveIcon, ArrowLeftIcon, CopyIcon, FilePlusIcon, KeyRoundIcon, MoreHorizontalIcon, RotateCcwIcon, ShieldCheckIcon, Trash2Icon, UploadIcon } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import { EditableLabel } from '@/components/report/shared/editable-label'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { DetailHeader, DetailKey } from '@/components/shell/page-shell'
import { SAVE_FIRST, useHasPendingChanges } from '@/components/shell/changes'
import { useDatasetsApi, useRefreshDatasets, useSourceObjects, useVocabularies } from '@/lib/datasets/client'
import type { ApiResult, DatasetListItem, DatasetVersionSummary, SourceObjectKind, ValidationProblemDetails, ValidationReport } from '@/lib/datasets/contract'
import { FormRow, ProblemBanner, STATUS_TONE, VocabSelect, useApiCall } from './shared'
import { cn } from '@/lib/utils'

type HeaderDialog = { type: 'newDraft'; mode: 'empty' | 'clone' } | { type: 'renameKey' } | { type: 'confirm'; action: 'deleteDraft' | 'retire' | 'deleteDataset' } | null

export function DatasetHeader({
  dataset,
  selected,
  onBack,
  onSelectVersion,
  onReport,
}: {
  dataset: DatasetListItem
  selected: DatasetVersionSummary
  onBack: () => void
  onSelectVersion: (id: string) => void
  onReport: (r: ValidationReport) => void
}) {
  const api = useDatasetsApi()
  const { run, saving, problem, setProblem } = useApiCall()
  const [dialog, setDialog] = useState<HeaderDialog>(null)
  const active = selected.releases.find((r) => r.isActive)
  const otherPins = selected.releases.filter((r) => !r.isActive)
  const hasDraft = dataset.versions.some((v) => v.status === 'draft')
  const pending = useHasPendingChanges()

  async function validate() {
    const r = await run(() => api.validateVersion(selected.id))
    if (!r) return
    onReport(r)
    if (r.violations.length) toast.error(`${r.violations.length} publication ${r.violations.length === 1 ? 'rule fails' : 'rules fail'}`)
    else toast.success(`Version ${selected.versionNumber} passed validation`)
  }
  async function publish() {
    const r = await run(() => api.publishVersion(selected.id))
    if (!r) return
    onReport(r)
    if (r.violations.length) toast.error('Not published: fix the readiness issues first')
    else toast.success(`Version ${selected.versionNumber} published`)
  }
  async function reopen() {
    if (await run(() => api.reopenVersion(selected.id))) toast.success(`Version ${selected.versionNumber} is a draft again`)
  }

  return (
    <DetailHeader
      section="Datasets"
      onBack={onBack}
      title={
        <EditableLabel
          value={dataset.displayName}
          aria-label="Dataset name"
          showIcon
          className="text-sm font-semibold"
          onCommit={async (displayName) => {
            const r = await api.updateDataset(dataset.id, { displayName })
            if (!r.ok) toast.error(r.problem.errors.displayName?.[0] ?? r.problem.title)
            else await run(async () => ({ ok: true, data: null }))
          }}
        />
      }
      meta={
        <>
          <DetailKey>{dataset.datasetKey}</DetailKey>
          <div className="ml-2 flex items-center gap-2">
            <Switch
              id="dataset-enabled"
              checked={dataset.isEnabled}
              onCheckedChange={(isEnabled) => run(() => api.updateDataset(dataset.id, { isEnabled }))}
            />
            <Label htmlFor="dataset-enabled" className="text-xs text-muted-foreground">
              {dataset.isEnabled ? 'Enabled' : 'Disabled'}
            </Label>
          </div>
        </>
      }
    >
        <div className="w-52">
          <SimpleSelect
            aria-label="Version"
            size="sm"
            value={selected.id}
            onChange={onSelectVersion}
            options={dataset.versions.map((v) => ({
              value: v.id,
              label: `Version ${v.versionNumber} · ${v.status}`,
              hint: v.releases.find((r) => r.isActive) ? 'active' : v.releases.length ? 'pinned' : undefined,
            }))}
          />
        </div>
        <span className={cn('rounded-md border px-2 py-0.5 text-xs capitalize', STATUS_TONE[selected.status])}>{selected.status}</span>
        <span className="text-xs text-muted-foreground" title={selected.releases.map((r) => r.releaseKey).join(', ') || undefined}>
          {active ? (
            <>
              Engine reads this version via <span className="font-mono text-foreground">{active.releaseKey}</span>
            </>
          ) : otherPins.length ? (
            <>
              Pinned by <span className="font-mono">{otherPins.map((r) => r.releaseKey).join(', ')}</span>
            </>
          ) : (
            'Not in any release'
          )}
        </span>

        <div className="flex items-center gap-1.5">
          {(selected.status === 'draft' || selected.status === 'validated') && (
            <>
              <Button size="sm" variant="outline" onClick={validate} disabled={saving || pending} title={pending ? SAVE_FIRST : undefined}>
                <ShieldCheckIcon /> Validate
              </Button>
              <Button size="sm" onClick={publish} disabled={saving || pending} title={pending ? SAVE_FIRST : undefined}>
                <UploadIcon /> Publish
              </Button>
            </>
          )}
          <DropdownMenu>
            <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label="Dataset actions" />}>
              <MoreHorizontalIcon />
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-60">
              {pending && <p className="px-2 pt-1 pb-1.5 text-[11px] text-muted-foreground">{SAVE_FIRST}.</p>}
              <DropdownMenuItem disabled={hasDraft || pending} onClick={() => setDialog({ type: 'newDraft', mode: 'empty' })}>
                <FilePlusIcon /> New empty draft
              </DropdownMenuItem>
              <DropdownMenuItem disabled={hasDraft || pending} onClick={() => setDialog({ type: 'newDraft', mode: 'clone' })}>
                <CopyIcon /> Clone into new draft…
              </DropdownMenuItem>
              {hasDraft && <p className="px-2 pb-1 text-[11px] text-muted-foreground">A dataset holds one draft at a time.</p>}
              {selected.status === 'validated' && (
                <DropdownMenuItem disabled={pending} onClick={reopen}>
                  <RotateCcwIcon /> Return to draft
                </DropdownMenuItem>
              )}
              {selected.status === 'published' && (
                <DropdownMenuItem disabled={pending} onClick={() => setDialog({ type: 'confirm', action: 'retire' })}>
                  <ArchiveIcon /> Retire version {selected.versionNumber}…
                </DropdownMenuItem>
              )}
              {selected.status === 'draft' && (
                <DropdownMenuItem disabled={pending} variant="destructive" onClick={() => setDialog({ type: 'confirm', action: 'deleteDraft' })}>
                  <Trash2Icon /> Delete draft…
                </DropdownMenuItem>
              )}
              <DropdownMenuSeparator />
              <DropdownMenuItem disabled={pending} onClick={() => setDialog({ type: 'renameKey' })}>
                <KeyRoundIcon /> Change dataset key…
              </DropdownMenuItem>
              <DropdownMenuItem disabled={pending} variant="destructive" onClick={() => setDialog({ type: 'confirm', action: 'deleteDataset' })}>
                <Trash2Icon /> Delete dataset…
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>

      {problem && !dialog && (
        <div className="lg:absolute lg:top-14 lg:right-4 lg:z-30 lg:w-96">
          <ProblemBanner problem={problem} onDismiss={() => setProblem(null)} />
        </div>
      )}

      {dialog?.type === 'newDraft' && (
        <NewDraftDialog
          dataset={dataset}
          selected={selected}
          initialMode={dialog.mode}
          onClose={() => setDialog(null)}
          onCreated={(id) => {
            setDialog(null)
            onSelectVersion(id)
          }}
        />
      )}
      {dialog?.type === 'renameKey' && <RenameKeyDialog dataset={dataset} onClose={() => setDialog(null)} />}
      {dialog?.type === 'confirm' && (
        <ConfirmDialog
          action={dialog.action}
          dataset={dataset}
          selected={selected}
          onClose={() => setDialog(null)}
          onDone={(action) => {
            setDialog(null)
            if (action === 'deleteDataset') onBack()
          }}
        />
      )}
    </DetailHeader>
  )
}

function NewDraftDialog({
  dataset,
  selected,
  initialMode,
  onClose,
  onCreated,
}: {
  dataset: DatasetListItem
  selected: DatasetVersionSummary
  initialMode: 'empty' | 'clone'
  onClose: () => void
  onCreated: (id: string) => void
}) {
  const api = useDatasetsApi()
  const { data: vocab } = useVocabularies()
  const { data: sources } = useSourceObjects()
  const { run, saving, problem, errorsFor } = useApiCall()
  const [mode, setMode] = useState(initialMode)
  const [from, setFrom] = useState(selected.id)
  const [kind, setKind] = useState<SourceObjectKind>('table')
  const [object, setObject] = useState(selected.sourceObject)

  async function create() {
    const v = await run(() => api.createDraft(dataset.id, mode === 'clone' ? { mode, fromVersionId: from } : { mode, sourceObjectKind: kind, sourceObject: object }))
    if (v) {
      toast.success(`Draft version ${v.versionNumber} created`)
      onCreated(v.id)
    }
  }

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>New draft version</DialogTitle>
          <DialogDescription>Published versions never change. Start a draft from scratch or copy every field, grain, binding and measure from an existing version.</DialogDescription>
        </DialogHeader>
        <div className="flex flex-col gap-4">
          <div role="radiogroup" aria-label="Start from" className="grid grid-cols-2 gap-2">
            {(['clone', 'empty'] as const).map((m) => (
              <button
                key={m}
                type="button"
                role="radio"
                aria-checked={mode === m}
                onClick={() => setMode(m)}
                className={cn('flex flex-col gap-0.5 rounded-md border p-3 text-left text-sm', mode === m ? 'border-primary bg-primary/5' : 'hover:bg-muted')}
              >
                <span className="font-medium">{m === 'clone' ? 'Clone a version' : 'Empty draft'}</span>
                <span className="text-xs text-muted-foreground">{m === 'clone' ? 'Copies all metadata' : 'Pick a source object'}</span>
              </button>
            ))}
          </div>
          {problem && <ProblemBanner problem={problem} />}
          {mode === 'clone' ? (
            <FormRow label="Copy from" htmlFor="nd-from" errors={errorsFor('fromVersionId')}>
              <SimpleSelect
                id="nd-from"
                value={from}
                onChange={setFrom}
                options={dataset.versions.map((v) => ({ value: v.id, label: `Version ${v.versionNumber} · ${v.status}`, hint: v.sourceObject }))}
              />
            </FormRow>
          ) : (
            <div className="grid gap-4 sm:grid-cols-2">
              <FormRow label="Source kind" htmlFor="nd-kind" errors={errorsFor('sourceObjectKind')}>
                <VocabSelect
                  id="nd-kind"
                  terms={vocab?.sourceObjectKind}
                  value={kind}
                  onChange={(v) => {
                    setKind(v ?? 'table')
                    setObject('')
                  }}
                />
              </FormRow>
              <FormRow label="Source object" htmlFor="nd-object" errors={errorsFor('sourceObject')}>
                <SimpleSelect
                  id="nd-object"
                  value={object}
                  onChange={setObject}
                  placeholder="Choose…"
                  options={(sources ?? []).filter((s) => s.kind === kind).map((s) => ({ value: s.name, label: s.name }))}
                />
              </FormRow>
            </div>
          )}
        </div>
        <DialogFooter>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={create} disabled={saving}>
            {saving ? 'Creating…' : 'Create draft'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function RenameKeyDialog({ dataset, onClose }: { dataset: DatasetListItem; onClose: () => void }) {
  const api = useDatasetsApi()
  const { run, saving, errorsFor, problem } = useApiCall()
  const [key, setKey] = useState(dataset.datasetKey)
  async function save() {
    if (await run(() => api.updateDataset(dataset.id, { datasetKey: key.trim() }))) {
      toast.success('Dataset key changed')
      onClose()
    }
  }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Change dataset key</DialogTitle>
          <DialogDescription>Query requests name the dataset by this key. Requests using the old key stop resolving once you save.</DialogDescription>
        </DialogHeader>
        {problem && !problem.errors.datasetKey && <ProblemBanner problem={problem} />}
        <FormRow label="Dataset key" htmlFor="rk-key" errors={errorsFor('datasetKey')}>
          <Input id="rk-key" value={key} onChange={(e) => setKey(e.target.value)} className="font-mono" aria-invalid={!!errorsFor('datasetKey')} />
        </FormRow>
        <DialogFooter>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={save} disabled={saving || key.trim() === dataset.datasetKey}>
            Change key
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

const CONFIRM = {
  deleteDraft: { title: 'Delete this draft?', body: 'Every field, grain, binding and measure in the draft is removed. Published versions are not affected.', cta: 'Delete draft', done: 'Draft deleted' },
  retire: { title: 'Retire this version?', body: 'A retired version stays readable for reference but can no longer be pinned by a release.', cta: 'Retire version', done: 'Version retired' },
  deleteDataset: { title: 'Delete this dataset?', body: 'Only possible while every version is a draft and no release lists the dataset.', cta: 'Delete dataset', done: 'Dataset deleted' },
} as const

function ConfirmDialog({
  action,
  dataset,
  selected,
  onClose,
  onDone,
}: {
  action: keyof typeof CONFIRM
  dataset: DatasetListItem
  selected: DatasetVersionSummary
  onClose: () => void
  onDone: (action: keyof typeof CONFIRM) => void
}) {
  const api = useDatasetsApi()
  const refresh = useRefreshDatasets()
  const [saving, setSaving] = useState(false)
  const [problem, setProblem] = useState<ValidationProblemDetails | null>(null)
  const copy = CONFIRM[action]

  async function confirm() {
    setSaving(true)
    const result: ApiResult<unknown> =
      action === 'deleteDraft' ? await api.deleteDraft(selected.id) : action === 'retire' ? await api.retireVersion(selected.id) : await api.deleteDataset(dataset.id)
    setSaving(false)
    if (!result.ok) {
      setProblem(result.problem)
      return
    }
    toast.success(copy.done)
    onDone(action)
    await refresh()
  }

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{copy.title}</DialogTitle>
          <DialogDescription>{copy.body}</DialogDescription>
        </DialogHeader>
        {problem && <ProblemBanner problem={problem} />}
        <DialogFooter>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="destructive" disabled={saving} onClick={confirm}>
            {copy.cta}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
