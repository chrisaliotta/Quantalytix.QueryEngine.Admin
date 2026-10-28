import { PageMessage } from '@/components/shell/page-shell'
import { ReportEditScope } from '@/lib/reports/client'
import type { Id } from '@/lib/reports/contract'
import { ColumnsTab } from './columns/columns-tab'
import { DataTab } from './data/data-tab'
import { CopyReportDialog, DeleteReportDialog } from './dialogs/report-dialogs'
import { CoverageDialog } from './dialogs/coverage-dialog'
import { EditorNav } from './editor-nav'
import { ReportEditorProvider, useReportEditor } from './editor-state'
import { ReportHeader } from './report-header'
import { RowsTab } from './rows/rows-tab'
import { SettingsView } from './settings/settings-view'
import { StylesTab } from './styles/styles-tab'

/** The report editor (blueprint 16): every edit stages in the report's change set until Save. */
export function ReportEditor({ reportId }: { reportId: Id }) {
  return (
    <ReportEditScope reportId={reportId} label="Report">
      <ReportEditorProvider reportId={reportId} loading={<PageMessage>Loading report…</PageMessage>}>
        <ReportHeader />
        <div className="flex min-h-0 flex-1 flex-col md:flex-row">
          <EditorNav />
          <main className="flex min-h-0 min-w-0 flex-1 flex-col">
            <EditorView />
          </main>
        </div>
        <EditorDialogs />
      </ReportEditorProvider>
    </ReportEditScope>
  )
}

function EditorView() {
  const { ui } = useReportEditor()
  if (ui.view === 'settings') return <SettingsView />
  if (ui.tab === 'rows') return <RowsTab />
  return (
    <div className="min-h-0 flex-1 overflow-y-auto">
      {ui.tab === 'data' ? <DataTab /> : ui.tab === 'styles' ? <StylesTab /> : <ColumnsTab />}
    </div>
  )
}

function EditorDialogs() {
  const { ui, setUi } = useReportEditor()
  const close = () => setUi({ dialog: null })
  const dialog = ui.dialog
  if (!dialog) return null
  switch (dialog.type) {
    case 'coverage':
      return <CoverageDialog sourceKey={dialog.sourceKey} onClose={close} />
    case 'copy':
      return <CopyReportDialog changeModel={dialog.changeModel} onClose={close} />
    case 'delete':
      return <DeleteReportDialog onClose={close} />
  }
}
