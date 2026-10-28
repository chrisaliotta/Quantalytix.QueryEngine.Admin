import { useEditor } from '@/lib/report/store'
import { ColumnsTab } from './columns/columns-tab'
import { DataTab } from './data/data-tab'
import { EditorNav } from './editor-nav'
import { ReportHeader } from './report-header'
import { RowsTab } from './rows/rows-tab'
import { SettingsView } from './settings/settings-view'
import { StylesTab } from './styles/styles-tab'

export function ReportEditor() {
  const { ui } = useEditor()
  return (
    <>
      <ReportHeader />
      <div className="flex min-h-0 flex-1 flex-col md:flex-row">
        <EditorNav />
        <main className="flex min-h-0 min-w-0 flex-1 flex-col">
          {ui.view === 'settings' ? (
            <SettingsView />
          ) : ui.tab === 'rows' ? (
            <div className="min-h-0 flex-1">
              <RowsTab />
            </div>
          ) : (
            <div className="min-h-0 flex-1 overflow-y-auto">
              {ui.tab === 'data' ? (
                <DataTab />
              ) : ui.tab === 'styles' ? (
                <StylesTab />
              ) : (
                <ColumnsTab />
              )}
            </div>
          )}
        </main>
      </div>
    </>
  )
}
