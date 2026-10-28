import { MoreHorizontalIcon } from 'lucide-react'
import { toast } from 'sonner'
import { EnabledBadge } from '@/components/models/model-detail'
import { DetailHeader, DetailKey } from '@/components/shell/page-shell'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Switch } from '@/components/ui/switch'
import { useAppNav } from '@/lib/navigation'
import { refusalOf, useRefreshReports, useReportsApi } from '@/lib/reports/client'
import { useReportEditor } from './editor-state'
import { EditableLabel } from './shared/editable-label'

/** The report's name, key and model, its enabled switch, and the copy and delete commands. */
export function ReportHeader() {
  const { reportId, report, model, run, setUi } = useReportEditor()
  const api = useReportsApi()
  const refresh = useRefreshReports()
  const nav = useAppNav()

  /** Enabling runs every report rule on the stored report; a refusal opens the diagnostics. */
  const setEnabled = async (isEnabled: boolean) => {
    const result = await api.setEnabled(reportId, isEnabled)
    if (!result.ok) {
      const violations = refusalOf(result.problem)
      toast.error(result.problem.title, { description: violations.length > 1 ? `${violations.length} problems; see Diagnostics.` : violations[0]?.message })
      if (result.problem.status === 422) setUi({ view: 'settings', section: 'diagnostics' })
      return
    }
    toast.success(isEnabled ? 'Report enabled' : 'Report disabled')
    await refresh()
  }

  return (
    <DetailHeader
      section="Reports"
      onBack={nav.reports}
      title={
        <EditableLabel
          value={report.settings.displayName}
          onCommit={(displayName) => void run((a) => a.updateSettings(reportId, { displayName }))}
          aria-label="Report name"
          className="text-sm font-semibold"
          showIcon
        />
      }
      meta={
        <>
          <DetailKey>{report.key}</DetailKey>
          <span className="hidden text-xs text-muted-foreground sm:inline">· {model?.displayName ?? model?.key}</span>
          <EnabledBadge enabled={report.isEnabled} />
        </>
      }
    >
      <label className="flex items-center gap-2 text-sm">
        <Switch checked={report.isEnabled} onCheckedChange={(checked) => void setEnabled(checked)} aria-label="Enabled" />
        Enabled
      </label>
      <DropdownMenu>
        <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label="Report actions" />}>
          <MoreHorizontalIcon />
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-56">
          <DropdownMenuItem onClick={() => setUi({ dialog: { type: 'copy', changeModel: false } })}>Duplicate…</DropdownMenuItem>
          <DropdownMenuItem onClick={() => setUi({ dialog: { type: 'copy', changeModel: true } })}>Copy onto another model…</DropdownMenuItem>
          <DropdownMenuSeparator />
          <DropdownMenuItem variant="destructive" onClick={() => setUi({ dialog: { type: 'delete' } })}>
            Delete…
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </DetailHeader>
  )
}
