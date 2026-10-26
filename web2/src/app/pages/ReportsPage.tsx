import { ReportsScreen } from '@/components/reports/reports-screen'
import { WorkspaceBar } from '@/components/shell/workspace-bar'

export function ReportsPage() {
  return (
    <>
      <WorkspaceBar title="Reports" />
      <ReportsScreen />
    </>
  )
}
