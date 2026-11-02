import { useParams } from 'react-router'
import { SnapshotsScreen } from '@/components/snapshots/snapshots-screen'
import { WorkspaceBar } from '@/components/shell/workspace-bar'
import { ModelsApiProvider } from '@/lib/models/client'
import { ReportsApiProvider } from '@/lib/reports/client'

/** The models and reports ports serve the read-only views of frozen model and report versions. */
export function SnapshotsPage() {
  const { snapshotId } = useParams()
  return (
    <ModelsApiProvider>
      <ReportsApiProvider>
        {!snapshotId && <WorkspaceBar title="Snapshots" />}
        <SnapshotsScreen snapshotId={snapshotId ?? null} />
      </ReportsApiProvider>
    </ModelsApiProvider>
  )
}
