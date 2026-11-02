import { MoreHorizontalIcon, Trash2Icon } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import { EditableLabel } from '@/components/report/shared/editable-label'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { DetailHeader, DetailKey } from '@/components/shell/page-shell'
import { SAVE_FIRST, useHasPendingChanges } from '@/components/shell/changes'
import { useVersionLabel, VersionBadge } from '@/components/snapshots/shared'
import { useDatasetList, useDatasetsApi, useRefreshDatasets, useSourceObjects } from '@/lib/datasets/client'
import { managedTablesDroppedWith } from '@/lib/datasets/managed-tables'
import type { DatasetListItem, DatasetVersionSummary, Problem } from '@/lib/datasets/contract'
import { ProblemBanner, useApiCall } from './shared'

export function DatasetHeader({
  dataset,
  selected,
  onBack,
  onSelectVersion,
}: {
  dataset: DatasetListItem
  selected: DatasetVersionSummary
  onBack: () => void
  onSelectVersion: (id: string) => void
}) {
  const api = useDatasetsApi()
  const { run, problem, setProblem } = useApiCall()
  const [deleting, setDeleting] = useState(false)
  const pending = useHasPendingChanges()
  const versionLabel = useVersionLabel()

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
          <DetailKey>{dataset.key}</DetailKey>
          <div className="ml-2 flex items-center gap-2">
            <Switch
              id="dataset-enabled"
              checked={dataset.isEnabled}
              disabled={!selected.isLive}
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
            options={dataset.versions.map((v) => ({ value: v.id, label: versionLabel(v), hint: v.isLive ? 'edited in place' : 'frozen' }))}
          />
        </div>
        <VersionBadge version={selected} />

        <div className="flex items-center gap-1.5">
          <DropdownMenu>
            <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label="Dataset actions" />}>
              <MoreHorizontalIcon />
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-60">
              {pending && <p className="px-2 pt-1 pb-1.5 text-[11px] text-muted-foreground">{SAVE_FIRST}.</p>}
              <DropdownMenuItem disabled={pending} variant="destructive" onClick={() => setDeleting(true)}>
                <Trash2Icon /> Delete dataset…
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>

      {problem && !deleting && (
        <div className="lg:absolute lg:top-14 lg:right-4 lg:z-30 lg:w-96">
          <ProblemBanner problem={problem} onDismiss={() => setProblem(null)} />
        </div>
      )}
      {deleting && <DeleteDatasetDialog dataset={dataset} onClose={() => setDeleting(false)} onDeleted={onBack} />}
    </DetailHeader>
  )
}

function DeleteDatasetDialog({ dataset, onClose, onDeleted }: { dataset: DatasetListItem; onClose: () => void; onDeleted: () => void }) {
  const api = useDatasetsApi()
  const refresh = useRefreshDatasets()
  const { data: list } = useDatasetList()
  const { data: sources } = useSourceObjects()
  const [saving, setSaving] = useState(false)
  const [problem, setProblem] = useState<Problem | null>(null)
  const dropped = managedTablesDroppedWith(dataset.id, (list ?? []).flatMap((d) => d.versions), sources ?? [])

  async function confirm() {
    setSaving(true)
    const result = await api.deleteDataset(dataset.id)
    setSaving(false)
    if (!result.ok) {
      setProblem(result.problem)
      return
    }
    toast.success('Dataset deleted')
    onClose()
    onDeleted()
    await refresh()
  }

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Delete this dataset?</DialogTitle>
          <DialogDescription>Only possible while no frozen snapshot holds a version of it. A managed table only this dataset reads is dropped with it.</DialogDescription>
        </DialogHeader>
        {dropped.length > 0 && (
          <p className="text-sm text-destructive">
            The managed {dropped.length === 1 ? 'table' : 'tables'} <span className="font-mono">{dropped.join(', ')}</span>{' '}
            {dropped.length === 1 ? 'is' : 'are'} read by no other dataset and will be dropped with {dropped.length === 1 ? 'its' : 'their'} data
            and materialized view.
          </p>
        )}
        {problem && <ProblemBanner problem={problem} />}
        <DialogFooter>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="destructive" disabled={saving} onClick={confirm}>
            Delete dataset
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
