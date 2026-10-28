import { MoreHorizontalIcon, PlusIcon } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { EnabledBadge } from '@/components/models/model-detail'
import { Refusal } from '@/components/report/shared/bits'
import { Segmented } from '@/components/report/shared/segmented'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { ListPage } from '@/components/shell/page-shell'
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
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { DOTTED_KEY_PATTERN } from '@/lib/api/contract'
import { useModelList } from '@/lib/models/client'
import { useAppNav } from '@/lib/navigation'
import { refusalOf, useRefreshReports, useReportList, useReportsApi } from '@/lib/reports/client'
import type { ReportCreateInput, ReportSummary, Violation } from '@/lib/reports/contract'
import { useTenantCatalog } from '@/lib/tenant/client'

/** The report library: every report with its model, profiles, sources and whether the engine offers it. */
export function ReportsScreen() {
  const { data: reports } = useReportList()
  const api = useReportsApi()
  const refresh = useRefreshReports()
  const nav = useAppNav()
  const [creating, setCreating] = useState<ReportSummary | 'blank' | null>(null)
  const [deleting, setDeleting] = useState<ReportSummary | null>(null)

  const setEnabled = async (report: ReportSummary, isEnabled: boolean) => {
    const result = await api.setEnabled(report.id, isEnabled)
    if (!result.ok) return toast.error(result.problem.title, { description: `${refusalOf(result.problem).length} report rules fail; open the report's diagnostics.` })
    await refresh()
  }

  return (
    <>
      <ListPage
        title="Reports"
        description="Report definitions, the profiles they run under and the model they read."
        actions={
          <Button onClick={() => setCreating('blank')}>
            <PlusIcon /> New report
          </Button>
        }
      >
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-sm">
            <thead className="border-b text-xs text-muted-foreground">
              <tr className="[&>th]:h-9 [&>th]:px-3 [&>th]:text-left [&>th]:font-medium [&>th]:whitespace-nowrap">
                <th>Name</th>
                <th>Model</th>
                <th>Periodicity</th>
                <th className="text-right!">Profiles</th>
                <th className="text-right!">Sources</th>
                <th>Status</th>
                <th>
                  <span className="sr-only">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {(reports ?? []).map((report) => (
                <tr key={report.id} onClick={() => nav.report(report.id)} className="cursor-pointer hover:bg-muted/40 [&>td]:px-3 [&>td]:py-2.5">
                  <td>
                    <div className="flex flex-col">
                      <span className="font-medium">{report.displayName}</span>
                      <span className="font-mono text-xs text-muted-foreground">{report.key}</span>
                    </div>
                  </td>
                  <td className="font-mono text-xs">{report.modelKey}</td>
                  <td>{report.periodicity ?? '—'}</td>
                  <td className="text-right tabular-nums">{report.profileCount}</td>
                  <td className="text-right tabular-nums">{report.sourceCount}</td>
                  <td>
                    <EnabledBadge enabled={report.isEnabled} />
                  </td>
                  <td className="w-10" onClick={(e) => e.stopPropagation()}>
                    <DropdownMenu>
                      <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label={`${report.displayName} actions`} />}>
                        <MoreHorizontalIcon />
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end" className="w-44">
                        <DropdownMenuItem onClick={() => nav.report(report.id)}>Open</DropdownMenuItem>
                        <DropdownMenuItem onClick={() => setCreating(report)}>Duplicate…</DropdownMenuItem>
                        <DropdownMenuItem onClick={() => void setEnabled(report, !report.isEnabled)}>{report.isEnabled ? 'Disable' : 'Enable'}</DropdownMenuItem>
                        <DropdownMenuSeparator />
                        <DropdownMenuItem variant="destructive" onClick={() => setDeleting(report)}>
                          Delete…
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </ListPage>
      {creating && <NewReportDialog copyOf={creating === 'blank' ? null : creating} onClose={() => setCreating(null)} />}
      {deleting && <DeleteReportConfirm report={deleting} onClose={() => setDeleting(null)} />}
    </>
  )
}

type Start = 'blank' | 'dimension' | 'report'

/** A new disabled report over a model: a single root row, the published rows of a dimension, or a copy of another report (OI-47). */
function NewReportDialog({ copyOf, onClose }: { copyOf: ReportSummary | null; onClose: () => void }) {
  const api = useReportsApi()
  const refresh = useRefreshReports()
  const nav = useAppNav()
  const { data: reports } = useReportList()
  const { data: models } = useModelList()
  const { data: catalog } = useTenantCatalog()
  const [name, setName] = useState(copyOf ? `${copyOf.displayName} (copy)` : '')
  const [key, setKey] = useState(copyOf ? `${copyOf.key}_copy` : '')
  const [reportType, setReportType] = useState(copyOf?.reportType ?? 'financial')
  const [modelId, setModelId] = useState(copyOf?.modelId ?? '')
  const [start, setStart] = useState<Start>(copyOf ? 'report' : 'blank')
  const [from, setFrom] = useState(copyOf?.id ?? '')
  const [refusal, setRefusal] = useState<Violation[] | null>(null)
  const keyError = key && !DOTTED_KEY_PATTERN.test(key) ? 'Start with a letter; use letters, digits, _ and dots.' : null
  const dimensions = (catalog?.dimensions ?? []).filter((d) => d.purpose === 'analytical' && (d.kind === 'categorical' || d.kind === 'hierarchical') && d.publishedVersionId)
  const valid = !!key && !keyError && !!name.trim() && !!modelId && (start === 'blank' || !!from)

  const submit = async () => {
    const input: ReportCreateInput = { key, displayName: name.trim(), reportType, modelId }
    if (start === 'dimension') input.startFromDimensionId = from
    if (start === 'report') input.copyFromReportId = from
    const result = await api.createReport(input)
    if (!result.ok) return setRefusal(refusalOf(result.problem))
    await refresh()
    onClose()
    nav.report(result.data.id)
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{copyOf ? `Duplicate ${copyOf.displayName}` : 'New report'}</DialogTitle>
          <DialogDescription>A new report starts disabled. Its key and model are fixed; copy it onto another model to change the model.</DialogDescription>
        </DialogHeader>
        <form
          className="flex flex-col gap-4"
          onSubmit={(e) => {
            e.preventDefault()
            void submit()
          }}
        >
          <div className="flex flex-col gap-2">
            <Label htmlFor="new-report-name">Name</Label>
            <Input id="new-report-name" value={name} onChange={(e) => setName(e.target.value)} autoFocus />
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor="new-report-key">Key</Label>
            <Input id="new-report-key" value={key} onChange={(e) => setKey(e.target.value.trim())} placeholder="bank.fee_income" className="font-mono" aria-invalid={!!keyError} />
            {keyError && <p className="text-xs text-destructive">{keyError}</p>}
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-2">
              <Label htmlFor="new-report-type">Report type</Label>
              <Input id="new-report-type" value={reportType} onChange={(e) => setReportType(e.target.value)} />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="new-report-model">Model</Label>
              <SimpleSelect id="new-report-model" value={modelId} onChange={setModelId} placeholder="Choose" options={(models ?? []).map((m) => ({ value: m.id, label: m.displayName, hint: m.key }))} />
            </div>
          </div>
          <div className="flex flex-col gap-2">
            <Label>Rows</Label>
            <Segmented<Start>
              aria-label="Start from"
              value={start}
              onChange={(s) => {
                setStart(s)
                setFrom('')
              }}
              options={[
                { value: 'blank', label: 'A single total' },
                { value: 'dimension', label: 'A dimension' },
                { value: 'report', label: 'A copy' },
              ]}
            />
            {start === 'dimension' && (
              <SimpleSelect aria-label="Dimension" value={from} onChange={setFrom} placeholder="Choose a dimension" options={dimensions.map((d) => ({ value: d.id, label: d.displayName, hint: d.key }))} />
            )}
            {start === 'report' && (
              <SimpleSelect aria-label="Report" value={from} onChange={setFrom} placeholder="Choose a report" options={(reports ?? []).map((r) => ({ value: r.id, label: r.displayName, hint: r.key }))} />
            )}
          </div>
          <Refusal violations={refusal} />
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={!valid}>
              Create report
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function DeleteReportConfirm({ report, onClose }: { report: ReportSummary; onClose: () => void }) {
  const api = useReportsApi()
  const refresh = useRefreshReports()
  const remove = async () => {
    const result = await api.deleteReport(report.id)
    onClose()
    if (!result.ok) return toast.error(result.problem.title, { description: result.problem.detail })
    toast(`${report.displayName} deleted`)
    await refresh()
  }
  return (
    <AlertDialog open onOpenChange={(open) => !open && onClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Delete {report.displayName}?</AlertDialogTitle>
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
