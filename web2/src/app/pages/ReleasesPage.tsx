import { useParams } from 'react-router'
import { ReleasesScreen } from '@/components/releases/releases-screen'
import { WorkspaceBar } from '@/components/shell/workspace-bar'

export function ReleasesPage() {
  const { releaseId } = useParams()
  return (
    <>
      {!releaseId && <WorkspaceBar title="Releases" />}
      <ReleasesScreen releaseId={releaseId ?? null} />
    </>
  )
}
