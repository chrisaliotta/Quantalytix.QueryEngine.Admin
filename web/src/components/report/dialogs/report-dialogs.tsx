import { useState } from 'react'
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
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { DOTTED_KEY_PATTERN } from '@/lib/api/contract'
import { useModelList } from '@/lib/models/client'
import { useAppNav } from '@/lib/navigation'
import { refusalOf, useRefreshReports, useReportsApi } from '@/lib/reports/client'
import { useReportEditor } from '../editor-state'
import { Refusal } from '../shared/bits'
import { SimpleSelect } from '../shared/simple-select'

/**
 * Duplicates the open report, or copies it onto another model, which is how a report changes its model (OI-47): the copy takes
 * the rows, sources and content, drops what does not resolve in the new model, and starts disabled.
 */
export function CopyReportDialog({ changeModel, onClose }: { changeModel: boolean; onClose: () => void }) {
  const { reportId, report } = useReportEditor()
  const api = useReportsApi()
  const refresh = useRefreshReports()
  const nav = useAppNav()
  const { data: models } = useModelList()
  const [name, setName] = useState(`${report.settings.displayName} (copy)`)
  const [key, setKey] = useState(`${report.key}_copy`)
  const [modelId, setModelId] = useState(changeModel ? '' : report.modelId)
  const [refusal, setRefusal] = useState<ReturnType<typeof refusalOf> | null>(null)
  const keyError = DOTTED_KEY_PATTERN.test(key) ? null : 'Start with a letter; use letters, digits, _ and dots.'

  const submit = async () => {
    if (keyError || !name.trim() || !modelId) return
    const result = await api.createReport({ key, displayName: name.trim(), reportType: report.settings.reportType, modelId, copyFromReportId: reportId })
    if (!result.ok) return setRefusal(refusalOf(result.problem))
    await refresh()
    onClose()
    toast.success(`Created ${result.data.displayName}`)
    nav.report(result.data.id)
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{changeModel ? 'Copy onto another model' : 'Duplicate report'}</DialogTitle>
          <DialogDescription>
            {changeModel
              ? 'A report keeps its model. The copy reads the model you choose and drops columns, formula rows, variables and scopes that it does not have. It starts disabled.'
              : 'The copy takes the rows, sources, profiles and settings, with report bridges of its own. It starts disabled.'}
          </DialogDescription>
        </DialogHeader>
        <form
          className="flex flex-col gap-4"
          onSubmit={(e) => {
            e.preventDefault()
            void submit()
          }}
        >
          <div className="flex flex-col gap-2">
            <Label htmlFor="copy-name">Name</Label>
            <Input id="copy-name" value={name} onChange={(e) => setName(e.target.value)} autoFocus />
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor="copy-key">Key</Label>
            <Input id="copy-key" value={key} onChange={(e) => setKey(e.target.value.trim())} className="font-mono" aria-invalid={!!keyError} />
            {keyError && <p className="text-xs text-destructive">{keyError}</p>}
          </div>
          {changeModel && (
            <div className="flex flex-col gap-2">
              <Label htmlFor="copy-model">Model</Label>
              <SimpleSelect
                id="copy-model"
                value={modelId}
                onChange={setModelId}
                placeholder="Choose a model"
                options={(models ?? []).filter((m) => m.id !== report.modelId).map((m) => ({ value: m.id, label: m.displayName, hint: m.key }))}
              />
            </div>
          )}
          <Refusal violations={refusal} />
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={!!keyError || !name.trim() || !modelId}>
              Create copy
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

/** Deletes the open report with its report dimension, report bridges and content; nothing blocks it (P12). */
export function DeleteReportDialog({ onClose }: { onClose: () => void }) {
  const { reportId, report } = useReportEditor()
  const api = useReportsApi()
  const refresh = useRefreshReports()
  const nav = useAppNav()

  const remove = async () => {
    const result = await api.deleteReport(reportId)
    if (!result.ok) return toast.error(result.problem.title, { description: result.problem.detail })
    onClose()
    toast(`${report.settings.displayName} deleted`)
    await refresh()
    nav.reports()
  }

  return (
    <AlertDialog open onOpenChange={(open) => !open && onClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Delete {report.settings.displayName}?</AlertDialogTitle>
          <AlertDialogDescription>
            This removes the report with its rows, sources, report bridges, profiles and columns. The engine stops offering it. This cannot be undone.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          <AlertDialogAction variant="destructive" onClick={() => void remove()}>
            Delete report
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
